using System;
using System.Collections.Generic;
using System.Drawing;
using DeskStudy;

// Keeping widgets on screen and apart: bounce back, nearest free spot, resize limits and snapping. No UI.
public static class PlacementCoreTests
{
    static int count;
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
    static Rectangle R(int x, int y, int w, int h) { return new Rectangle(x, y, w, h); }
    static List<Rectangle> L(params Rectangle[] r) { return new List<Rectangle>(r); }
    static bool Apart(Rectangle a, List<Rectangle> others, int gap)
    {
        foreach (var o in others) if (Rectangle.Inflate(a, gap, gap).IntersectsWith(o)) return false;
        return true;
    }

    public static int Main()
    {
        const int G = PlacementLogic.Gap, D = PlacementLogic.SnapDistance;
        var work = R(0, 0, 1920, 1140); // taskbar below 1140
        var none = L();
        try
        {
            Assert(G == 8 && D == 10, "gap is 8 px and snapping starts within 10 px");
            // Bounce back from every side, including under the taskbar.
            Assert(PlacementLogic.Resolve(R(-120, 300, 400, 500), work, none, G) == R(0, 300, 400, 500), "past the left edge: back to x = 0, same size");
            Assert(PlacementLogic.Resolve(R(1700, 300, 400, 500), work, none, G) == R(1520, 300, 400, 500), "past the right edge: back inside");
            Assert(PlacementLogic.Resolve(R(300, 900, 400, 500), work, none, G) == R(300, 640, 400, 500), "into the taskbar: back above it");
            Assert(PlacementLogic.Resolve(R(300, -60, 400, 500), work, none, G) == R(300, 0, 400, 500), "past the top edge: back down");
            Assert(PlacementLogic.Resolve(R(1800, 1000, 400, 500), work, none, G) == R(1520, 640, 400, 500), "past a corner: back on both axes");
            Assert(PlacementLogic.Resolve(R(300, 300, 400, 500), work, none, G) == R(300, 300, 400, 500), "inside and alone: unchanged");
            var leftBar = R(48, 0, 1872, 1200);
            Assert(PlacementLogic.Resolve(R(10, 50, 400, 500), leftBar, none, G).X == 48, "taskbar on the left: kept right of it");
            var huge = PlacementLogic.Resolve(R(-50, -50, 2500, 1500), work, none, G);
            Assert(huge == work, "larger than the screen: shrunk to the work area");

            // Overlap: only the dropped widget moves, to the nearest free spot, 8 px from its neighbour.
            var todo = R(1000, 100, 440, 520);
            var dropped = R(1300, 150, 440, 520);
            var moved = PlacementLogic.Resolve(dropped, work, L(todo), G);
            Assert(moved == R(1448, 150, 440, 520), "dropped over the right half of a widget: pushed right, 8 px apart");
            moved = PlacementLogic.Resolve(R(700, 130, 440, 520), work, L(todo), G);
            Assert(moved == R(552, 130, 440, 520), "dropped over the left half: pushed left, 8 px apart");
            moved = PlacementLogic.Resolve(R(1000, 450, 440, 400), work, L(todo), G);
            Assert(moved == R(1000, 628, 440, 400), "dropped mostly below: pushed down, 8 px apart");
            moved = PlacementLogic.Resolve(R(1000, 450, 440, 520), work, L(todo), G);
            Assert(moved.Y == 450 && Apart(moved, L(todo), G), "no room below (taskbar): pushed sideways instead");
            // Right of the widget does not fit on screen: the next nearest free spot is used instead.
            var edgeWidget = R(1480, 100, 440, 520);
            moved = PlacementLogic.Resolve(R(1600, 120, 440, 520), work, L(edgeWidget), G);
            Assert(work.Contains(moved) && Apart(moved, L(edgeWidget), G), "no room on the far side: still ends up on screen and apart");
            // Between two widgets.
            var calendar = R(0, 0, 850, 650);
            var ddl = R(1480, 600, 440, 520);
            moved = PlacementLogic.Resolve(R(800, 100, 440, 520), work, L(calendar, ddl), G);
            Assert(moved == R(858, 100, 440, 520) && Apart(moved, L(calendar, ddl), G), "with two neighbours: clear of both, nearest spot");
            moved = PlacementLogic.Resolve(R(1400, 500, 440, 520), work, L(calendar, ddl), G);
            Assert(work.Contains(moved) && Apart(moved, L(calendar, ddl), G), "dropped between the screen edge and another widget: apart and on screen");
            // Only an 8 px gap is impossible: touching is accepted before overlapping.
            var tight = R(0, 0, 1000, 1000);
            var twoWide = R(0, 0, 1000, 1000);
            var dense = PlacementLogic.Resolve(R(400, 0, 496, 1000), tight, L(R(0, 0, 500, 1000)), G);
            Assert(dense.X == 500 && !dense.IntersectsWith(R(0, 0, 500, 1000)), "no room for the gap: placed touching, not overlapping");
            // No room at all: stays where it was dropped (on screen), overlap accepted.
            var crowded = PlacementLogic.Resolve(R(100, 100, 800, 800), twoWide, L(R(0, 0, 600, 1000)), G);
            Assert(crowded == R(100, 100, 800, 800), "no free spot anywhere: left where dropped instead of jumping around");
            Assert(PlacementLogic.Resolve(R(1300, 150, 440, 520), work, L(todo), 0).X == 1440, "gap 0 places widgets edge to edge");

            // Resizing into the screen edge or into a neighbour: the dragged edge stops, the window does not jump.
            var min = new Size(350, 430);
            var r = PlacementLogic.AfterResize(R(1600, 100, 400, 500), PlacementLogic.EdgeRight, work, none, min, G);
            Assert(r == R(1570, 100, 350, 500), "right edge dragged past the screen, trimmed below the minimum width: minimum width against the edge");
            r = PlacementLogic.AfterResize(R(1400, 100, 600, 500), PlacementLogic.EdgeRight, work, none, min, G);
            Assert(r == R(1400, 100, 520, 500), "right edge dragged past the screen: pulled back to the edge, left edge stays");
            r = PlacementLogic.AfterResize(R(300, 800, 400, 500), PlacementLogic.EdgeBottom, work, none, min, G);
            Assert(r == R(300, 710, 400, 430), "bottom edge into the taskbar where trimming would go below the minimum: minimum size, moved up");
            r = PlacementLogic.AfterResize(R(300, 500, 400, 700), PlacementLogic.EdgeBottom, work, none, min, G);
            Assert(r == R(300, 500, 400, 640), "bottom edge into the taskbar: stops at the taskbar");
            r = PlacementLogic.AfterResize(R(-40, 100, 500, 500), PlacementLogic.EdgeLeft, work, none, min, G);
            Assert(r == R(0, 100, 460, 500), "left edge past the screen: pulled back, right edge stays");
            r = PlacementLogic.AfterResize(R(500, 100, 560, 520), PlacementLogic.EdgeRight, work, L(todo), min, G);
            Assert(r == R(500, 100, 492, 520), "right edge dragged into a neighbour: stops 8 px before it");
            r = PlacementLogic.AfterResize(R(1000, 0, 440, 140), PlacementLogic.EdgeBottom, work, L(R(1000, 100, 440, 520)), new Size(100, 60), G);
            Assert(r == R(1000, 0, 440, 92), "bottom edge dragged into a neighbour below: stops 8 px above it");
            r = PlacementLogic.AfterResize(R(600, 100, 600, 600), PlacementLogic.EdgeBottomRight, work, L(todo), min, G);
            Assert(!r.IntersectsWith(todo) && r.Location == new Point(600, 100) && r.Width >= min.Width && r.Height >= min.Height, "corner dragged into a neighbour: one side backs off, top-left corner stays");
            r = PlacementLogic.AfterResize(R(700, 100, 360, 520), PlacementLogic.EdgeRight, work, L(todo), min, G);
            Assert(!r.IntersectsWith(todo) && r.Width >= min.Width && r.Height >= min.Height, "backing off would go below the minimum size: the widget moves instead");

            // Snapping while dragging.
            var s = PlacementLogic.SnapMove(R(7, 300, 400, 500), work, none, D, G);
            Assert(s.X == 0, "7 px from the left edge: snaps to it");
            s = PlacementLogic.SnapMove(R(11, 300, 400, 500), work, none, D, G);
            Assert(s.X == 11, "11 px from the left edge: no snap");
            s = PlacementLogic.SnapMove(R(1515, 632, 400, 500), work, none, D, G);
            Assert(s.X == 1520 && s.Y == 640, "near the right edge and the taskbar: snaps to both");
            s = PlacementLogic.SnapMove(R(1450, 300, 440, 520), work, L(todo), D, G);
            Assert(s.X == 1448, "near a widget's right side: snaps 8 px beside it");
            s = PlacementLogic.SnapMove(R(1448, 104, 440, 520), work, L(todo), D, G);
            Assert(s.X == 1448 && s.Y == 100, "side by side and nearly level: tops line up");
            s = PlacementLogic.SnapMove(R(1005, 630, 440, 400), work, L(todo), D, G);
            Assert(s.Y == 628 && s.X == 1000, "stacked below and nearly in line: snaps under it with left edges aligned");
            s = PlacementLogic.SnapMove(R(1450, 900, 440, 200), work, L(todo), D, G);
            Assert(s.X == 1450, "far below a widget: no sideways snap to it");
            s = PlacementLogic.SnapMove(R(-30, 300, 400, 500), work, none, D, G);
            Assert(s.X == -30, "already past the edge while dragging: not held, bounces on release");

            // Snapping while resizing.
            var z = PlacementLogic.SnapResize(R(1000, 100, 912, 500), PlacementLogic.EdgeRight, work, none, min, D, G);
            Assert(z.Right == 1920 && z.Left == 1000, "right edge near the screen edge: snaps to it");
            z = PlacementLogic.SnapResize(R(400, 100, 595, 500), PlacementLogic.EdgeRight, work, L(todo), min, D, G);
            Assert(z.Right == 992, "right edge near a neighbour: snaps 8 px before it");
            z = PlacementLogic.SnapResize(R(1448, 100, 440, 515), PlacementLogic.EdgeBottom, work, L(todo), min, D, G);
            Assert(z.Bottom == 620, "bottom edge near a neighbour's bottom: lines up with it");
            z = PlacementLogic.SnapResize(R(500, 300, 400, 500), PlacementLogic.EdgeTopLeft, work, none, min, D, G);
            Assert(z == R(500, 300, 400, 500), "nothing nearby: resize untouched");

            Console.WriteLine("Placement core tests passed: " + count);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex.Message); return 1; }
    }
}
