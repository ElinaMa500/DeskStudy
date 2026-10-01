using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeskStudy
{
    // After a drag or resize is released: snapping, sliding back from the screen edges and away from other widgets.
    public partial class WidgetForm
    {
        private int sizingEdge;
        private Timer slideTimer;
        private Rectangle slideFrom, slideTo;
        private DateTime slideStart;
        private const int SlideMilliseconds = 160;

        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
        [DllImport("user32.dll")] private static extern bool IsWindowArranged(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint param, ref bool value, uint winIni);

        // The part of the window that is actually drawn. A standard window has invisible resize borders around it.
        private Padding VisualInsets()
        {
            if (desktopMode || !IsHandleCreated) return Padding.Empty;
            RECT frame;
            try { if (DwmGetWindowAttribute(Handle, 9, out frame, Marshal.SizeOf(typeof(RECT))) != 0) return Padding.Empty; }
            catch (DllNotFoundException) { return Padding.Empty; }
            Rectangle b = Bounds;
            var insets = new Padding(frame.Left - b.Left, frame.Top - b.Top, b.Right - frame.Right, b.Bottom - frame.Bottom);
            if (insets.Left < 0 || insets.Top < 0 || insets.Right < 0 || insets.Bottom < 0 || insets.Horizontal > 60 || insets.Vertical > 60) return Padding.Empty;
            return insets;
        }
        private static Rectangle Deflate(Rectangle r, Padding p) { return Rectangle.FromLTRB(r.Left + p.Left, r.Top + p.Top, r.Right - p.Right, r.Bottom - p.Bottom); }
        private static Rectangle Inflate(Rectangle r, Padding p) { return Rectangle.FromLTRB(r.Left - p.Left, r.Top - p.Top, r.Right + p.Right, r.Bottom + p.Bottom); }
        public Rectangle VisualBounds { get { return Deflate(Bounds, VisualInsets()); } }
        // Where the widget is, or where it is sliding to.
        internal Rectangle PlacementBounds { get { return slideTimer != null && slideTimer.Enabled ? slideTo : VisualBounds; } }
        internal Size VisualMinimum { get { Padding p = VisualInsets(); return new Size(MinimumSize.Width - p.Horizontal, MinimumSize.Height - p.Vertical); } }
        internal bool Placeable { get { return !IsDisposed && IsHandleCreated && Visible && WindowState == FormWindowState.Normal && !Arranged(); } }
        // Windows' own snap layouts (standard windows only) are left as the user arranged them.
        private bool Arranged()
        {
            if (desktopMode) return false;
            try { return IsWindowArranged(Handle); }
            catch (EntryPointNotFoundException) { return false; }
        }

        // While the mouse is held the widget follows it freely; snapping and bouncing happen only on release.
        private void PlacementWndProc(ref Message m)
        {
            if (m.Msg == 0x0231) { StopSlide(); sizingEdge = 0; }
            else if (m.Msg == 0x0214) sizingEdge = m.WParam.ToInt32();
        }
        private void AfterPlacementWndProc(ref Message m)
        {
            // The drag or resize is over: snap, settle on screen and off other widgets.
            if (m.Msg == 0x0232 && !IsDisposed) { int edge = sizingEdge; sizingEdge = 0; App.SettleAfterDrag(this, edge); }
        }

        // Moves the widget so its visible area becomes the target, gliding there unless animations are off.
        internal void SlideTo(Rectangle visualTarget, bool animate)
        {
            StopSlide();
            Rectangle from = VisualBounds;
            if (from == visualTarget) return;
            bool effects = true;
            try { SystemParametersInfo(0x1042, 0, ref effects, 0); } catch (EntryPointNotFoundException) { }
            if (!animate || !effects || !Visible) { Bounds = Inflate(visualTarget, VisualInsets()); Remember(); return; }
            slideFrom = from; slideTo = visualTarget; slideStart = DateTime.UtcNow;
            if (slideTimer == null)
            {
                slideTimer = new Timer { Interval = 15 };
                slideTimer.Tick += delegate { SlideStep(); };
            }
            slideTimer.Start();
        }
        private void SlideStep()
        {
            if (IsDisposed) return;
            double t = Math.Min(1, (DateTime.UtcNow - slideStart).TotalMilliseconds / SlideMilliseconds);
            double eased = 1 - Math.Pow(1 - t, 3);
            Func<int, int, int> at = (a, b) => a + (int)Math.Round((b - a) * eased);
            var step = Rectangle.FromLTRB(at(slideFrom.Left, slideTo.Left), at(slideFrom.Top, slideTo.Top), at(slideFrom.Right, slideTo.Right), at(slideFrom.Bottom, slideTo.Bottom));
            Bounds = Inflate(step, VisualInsets());
            if (t >= 1) { slideTimer.Stop(); Remember(); }
        }
        private void StopSlide()
        {
            if (slideTimer == null || !slideTimer.Enabled) return;
            slideTimer.Stop(); Bounds = Inflate(slideTo, VisualInsets()); Remember();
        }
        public bool Sliding { get { return slideTimer != null && slideTimer.Enabled; } }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // A widget coming back into view must not land on top of another one.
            if (Visible && ready && App != null && IsHandleCreated)
                BeginInvoke(new Action(delegate { if (!IsDisposed && Visible) App.SettleWidget(this, 0, false); }));
        }
    }

    public sealed partial class AppController
    {
        private int placementSuspended;
        private EventHandler displayHandler;

        internal List<Rectangle> PlacementObstacles(WidgetForm self)
        {
            return Widgets.Where(w => w != self && w.Placeable).Select(w => w.PlacementBounds).ToList();
        }

        // After a drag or resize (edge = 0 for a move) or when a widget appears.
        public void SettleWidget(WidgetForm w, int edge, bool animate)
        {
            if (placementSuspended > 0 || Exiting || !w.Placeable) return;
            Settle(w, edge, PlacementObstacles(w), animate, false);
        }
        // Where a widget would end up if put at the given place (used when unfolding).
        internal Rectangle ResolveFor(WidgetForm w, Rectangle visual)
        {
            Rectangle work = Screen.FromRectangle(visual).WorkingArea;
            return w.PositionLocked ? PlacementLogic.Contain(visual, work) : PlacementLogic.Resolve(visual, work, PlacementObstacles(w), PlacementLogic.Gap);
        }
        // Released after a drag or resize: first snap to a screen edge or neighbour within reach, then settle.
        public void SettleAfterDrag(WidgetForm w, int edge)
        {
            if (placementSuspended > 0 || Exiting || !w.Placeable) return;
            Settle(w, edge, PlacementObstacles(w), true, true);
        }
        private static Rectangle Settle(WidgetForm w, int edge, List<Rectangle> others, bool animate, bool snap)
        {
            Rectangle visual = w.VisualBounds;
            Rectangle work = Screen.FromRectangle(visual).WorkingArea;
            if (w.Collapsed)
            {
                // Folded: a bar on the bottom edge; dragging it only chooses where along the bottom it sits.
                if (snap) visual = PlacementLogic.SnapMove(visual, work, others, PlacementLogic.SnapDistance, PlacementLogic.Gap);
                Rectangle dock = PlacementLogic.Dock(visual, work, others, PlacementLogic.Gap);
                w.SlideTo(dock, animate);
                return dock;
            }
            if (snap && !w.PositionLocked)
                visual = edge == 0 ? PlacementLogic.SnapMove(visual, work, others, PlacementLogic.SnapDistance, PlacementLogic.Gap)
                    : PlacementLogic.SnapResize(visual, edge, work, others, w.VisualMinimum, PlacementLogic.SnapDistance, PlacementLogic.Gap);
            Rectangle target = w.PositionLocked ? PlacementLogic.Contain(visual, work)
                : edge == 0 ? PlacementLogic.Resolve(visual, work, others, PlacementLogic.Gap)
                : PlacementLogic.AfterResize(visual, edge, work, others, w.VisualMinimum, PlacementLogic.Gap);
            w.SlideTo(target, animate);
            return target;
        }

        // Puts every showing widget on screen and apart. Locked widgets keep their place; the rest are settled in order.
        public void ArrangeWidgets()
        {
            if (Exiting) return;
            var placed = new List<Rectangle>();
            var showing = Widgets.Where(w => w.Placeable).ToList();
            foreach (var w in showing.Where(w => w.PositionLocked)) placed.Add(Settle(w, 0, placed, false, false));
            foreach (var w in showing.Where(w => !w.PositionLocked)) placed.Add(Settle(w, 0, placed, false, false));
        }

        // Several windows change together (restore layout, reset, rescue): settle once, after all of them moved.
        private void WithPlacementSuspended(Action change)
        {
            placementSuspended++;
            try { change(); }
            finally { placementSuspended--; }
            if (placementSuspended == 0) ArrangeWidgets();
        }

        private void WatchDisplays()
        {
            displayHandler = delegate
            {
                if (!Exiting && Widgets.Count > 0 && Widgets[0].IsHandleCreated)
                    Widgets[0].BeginInvoke(new Action(delegate { if (!Exiting) ArrangeWidgets(); }));
            };
            SystemEvents.DisplaySettingsChanged += displayHandler;
        }
        private void UnwatchDisplays()
        {
            if (displayHandler != null) SystemEvents.DisplaySettingsChanged -= displayHandler;
            displayHandler = null;
        }
    }
}
