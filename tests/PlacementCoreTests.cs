using System;
using System.Collections.Generic;
using System.Drawing;
using DeskStudy;

// Placing widgets after a drag: screen-edge rule, nearest free spot, resize limits and snapping on release. No UI.
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
        const int G = PlacementLogic.Gap, D = PlacementLogic.SnapDistance, C = PlacementLogic.EdgeCatch, K = PlacementLogic.KeepVisible;
        var work = R(0, 0, 1920, 1140); // taskbar below 1140
        var none = L();
        try
        {
            Assert(G == 5 && D == 10 && C == 60 && K == 100, "gap 5 px, snapping within 10 px, edges catch up to 60 px past them, 100 px always stays on screen");
            // Top: always back.
            Assert(PlacementLogic.Resolve(R(300, -60, 400, 500), work, none, G) == R(300, 0, 400, 500), "past the top edge: back down");
            Assert(PlacementLogic.Resolve(R(300, -400, 400, 500), work, none, G) == R(300, 0, 400, 500), "far past the top edge: still back down");
            // Other edges: slightly past snaps back, far past stays.
            Assert(PlacementLogic.Resolve(R(-40, 300, 400, 500), work, none, G) == R(0, 300, 400, 500), "40 px past the left edge: back to it");
            Assert(PlacementLogic.Resolve(R(-C, 300, 400, 500), work, none, G) == R(0, 300, 400, 500), "exactly 60 px past the left edge: back to it");
            Assert(PlacementLogic.Resolve(R(-C - 1, 300, 400, 500), work, none, G) == R(-C - 1, 300, 400, 500), "61 px past the left edge: left where it was put");
            Assert(PlacementLogic.Resolve(R(1920 - 400 + 50, 300, 400, 500), work, none, G) == R(1520, 300, 400, 500), "50 px past the right edge: back to it");
            Assert(PlacementLogic.Resolve(R(1700, 300, 400, 500), work, none, G) == R(1700, 300, 400, 500), "180 px past the right edge: left there");
            Assert(PlacementLogic.Resolve(R(300, 680, 400, 500), work, none, G) == R(300, 640, 400, 500), "40 px into the taskbar: back above it");
            Assert(PlacementLogic.Resolve(R(300, 900, 400, 500), work, none, G) == R(300, 900, 400, 500), "260 px into the taskbar: left there");
            Assert(PlacementLogic.Resolve(R(-30, -30, 400, 500), work, none, G) == R(0, 0, 400, 500), "slightly past a corner: back on both axes");
            Assert(PlacementLogic.Resolve(R(-300, -200, 400, 500), work, none, G) == R(-300, 0, 400, 500), "far past the top-left corner: top comes back, left stays out");
            Assert(PlacementLogic.Resolve(R(300, 300, 400, 500), work, none, G) == R(300, 300, 400, 500), "inside and alone: unchanged");
            // Never lost: at least 100 px stays on screen.
            Assert(PlacementLogic.Resolve(R(1900, 300, 400, 500), work, none, G) == R(1920 - K, 300, 400, 500), "almost entirely past the right edge: 100 px kept on screen");
            Assert(PlacementLogic.Resolve(R(-390, 300, 400, 500), work, none, G) == R(K - 400, 300, 400, 500), "almost entirely past the left edge: 100 px kept on screen");
            Assert(PlacementLogic.Resolve(R(300, 1130, 400, 500), work, none, G) == R(300, 1140 - K, 400, 500), "almost entirely under the taskbar: 100 px kept above it");
            var leftBar = R(48, 0, 1872, 1200);
            Assert(PlacementLogic.Resolve(R(10, 50, 400, 500), leftBar, none, G).X == 48, "taskbar on the left: slightly under it comes back");
            Assert(PlacementLogic.Resolve(R(-50, -50, 2500, 1500), work, none, G) == work, "larger than the screen: shrunk to the work area");

            // Overlap: only the dropped widget moves, to the nearest free spot, 5 px from its neighbour.
            var todo = R(1000, 100, 440, 520);
            Assert(PlacementLogic.Resolve(R(1300, 150, 440, 520), work, L(todo), G) == R(1440 + G, 150, 440, 520), "dropped over the right half of a widget: pushed right, 5 px apart");
            Assert(PlacementLogic.Resolve(R(700, 130, 440, 520), work, L(todo), G) == R(560 - G, 130, 440, 520), "dropped over the left half: pushed left, 5 px apart");
            Assert(PlacementLogic.Resolve(R(1000, 450, 440, 400), work, L(todo), G) == R(1000, 620 + G, 440, 400), "dropped mostly below: pushed down, 5 px apart");
            var moved = PlacementLogic.Resolve(R(1000, 450, 440, 520), work, L(todo), G);
            Assert(moved.Y == 450 && Apart(moved, L(todo), G), "no room below (taskbar): pushed sideways instead");
            var edgeWidget = R(1480, 100, 440, 520);
            moved = PlacementLogic.Resolve(R(1450, 120, 440, 520), work, L(edgeWidget), G);
            Assert(work.Contains(moved) && Apart(moved, L(edgeWidget), G), "dropped on screen with no room on the far side: stays on screen and apart");
            var calendar = R(0, 0, 850, 650);
            var ddl = R(1480, 600, 440, 520);
            moved = PlacementLogic.Resolve(R(800, 100, 440, 520), work, L(calendar, ddl), G);
            Assert(moved == R(850 + G, 100, 440, 520), "with two neighbours: clear of both, nearest spot");
            // Put partly off screen on top of a widget near the edge: may stay partly off screen, but not on the widget.
            var nearEdge = R(1600, 100, 320, 520);
            moved = PlacementLogic.Resolve(R(1700, 150, 440, 520), work, L(nearEdge), G);
            Assert(!moved.IntersectsWith(nearEdge) && PlacementLogic.Contain(moved, work) == moved, "dropped far past the edge onto a widget: moved clear, still allowed off screen: " + moved);
            var tight = R(0, 0, 1000, 1000);
            var dense = PlacementLogic.Resolve(R(400, 0, 498, 1000), tight, L(R(0, 0, 500, 1000)), G);
            Assert(dense.X == 500 && !dense.IntersectsWith(R(0, 0, 500, 1000)), "no room for the gap: placed touching, not overlapping");
            var crowded = PlacementLogic.Resolve(R(100, 100, 800, 800), tight, L(R(0, 0, 600, 1000)), G);
            Assert(crowded == R(100, 100, 800, 800), "no free spot anywhere: left where dropped instead of jumping around");

            // Resizing.
            var min = new Size(350, 430);
            Assert(PlacementLogic.AfterResize(R(1400, 100, 560, 500), PlacementLogic.EdgeRight, work, none, min, G) == R(1400, 100, 520, 500), "right edge 40 px past the screen: back to the edge, left edge stays");
            Assert(PlacementLogic.AfterResize(R(1400, 100, 700, 500), PlacementLogic.EdgeRight, work, none, min, G) == R(1400, 100, 700, 500), "right edge 180 px past the screen: left there");
            Assert(PlacementLogic.AfterResize(R(300, -80, 400, 580), PlacementLogic.EdgeTop, work, none, min, G) == R(300, 0, 400, 500), "top edge above the screen: always back to the top");
            Assert(PlacementLogic.AfterResize(R(300, 500, 400, 680), PlacementLogic.EdgeBottom, work, none, min, G) == R(300, 500, 400, 640), "bottom edge 40 px into the taskbar: stops at the taskbar");
            Assert(PlacementLogic.AfterResize(R(-40, 100, 500, 500), PlacementLogic.EdgeLeft, work, none, min, G) == R(0, 100, 460, 500), "left edge 40 px past the screen: back, right edge stays");
            Assert(PlacementLogic.AfterResize(R(1600, 100, 360, 500), PlacementLogic.EdgeRight, work, none, min, G) == R(1570, 100, 350, 500), "trimmed below the minimum width: minimum width against the edge");
            Assert(PlacementLogic.AfterResize(R(500, 100, 560, 520), PlacementLogic.EdgeRight, work, L(todo), min, G) == R(500, 100, 500 - G, 520), "right edge dragged into a neighbour: stops 5 px before it");
            Assert(PlacementLogic.AfterResize(R(1000, 0, 440, 140), PlacementLogic.EdgeBottom, work, L(todo), new Size(100, 60), G) == R(1000, 0, 440, 100 - G), "bottom edge dragged into a neighbour below: stops 5 px above it");
            var r = PlacementLogic.AfterResize(R(600, 100, 600, 600), PlacementLogic.EdgeBottomRight, work, L(todo), min, G);
            Assert(!r.IntersectsWith(todo) && r.Location == new Point(600, 100) && r.Width >= min.Width && r.Height >= min.Height, "corner dragged into a neighbour: one side backs off, top-left corner stays");
            r = PlacementLogic.AfterResize(R(700, 100, 360, 520), PlacementLogic.EdgeRight, work, L(todo), min, G);
            Assert(!r.IntersectsWith(todo) && r.Width >= min.Width && r.Height >= min.Height, "backing off would go below the minimum size: the widget moves instead");

            // Snapping on release.
            Assert(PlacementLogic.SnapMove(R(7, 300, 400, 500), work, none, D, G).X == 0, "released 7 px from the left edge: snaps to it");
            Assert(PlacementLogic.SnapMove(R(11, 300, 400, 500), work, none, D, G).X == 11, "11 px from the left edge: no snap");
            var s = PlacementLogic.SnapMove(R(1515, 632, 400, 500), work, none, D, G);
            Assert(s.X == 1520 && s.Y == 640, "near the right edge and the taskbar: snaps to both");
            Assert(PlacementLogic.SnapMove(R(1450, 300, 440, 520), work, L(todo), D, G).X == 1440 + G, "near a widget's right side: snaps 5 px beside it");
            s = PlacementLogic.SnapMove(R(1440 + G + 2, 104, 440, 520), work, L(todo), D, G);
            Assert(s.X == 1440 + G && s.Y == 100, "side by side and nearly level: tops line up");
            s = PlacementLogic.SnapMove(R(1005, 630, 440, 400), work, L(todo), D, G);
            Assert(s.Y == 620 + G && s.X == 1000, "stacked below and nearly in line: snaps under it with left edges aligned");
            Assert(PlacementLogic.SnapMove(R(1450, 900, 440, 200), work, L(todo), D, G).X == 1450, "far below a widget: no sideways snap to it");
            var z = PlacementLogic.SnapResize(R(1000, 100, 912, 500), PlacementLogic.EdgeRight, work, none, min, D, G);
            Assert(z.Right == 1920 && z.Left == 1000, "right edge released near the screen edge: snaps to it");
            Assert(PlacementLogic.SnapResize(R(400, 100, 598, 500), PlacementLogic.EdgeRight, work, L(todo), min, D, G).Right == 1000 - G, "right edge released near a neighbour: snaps 5 px before it");
            Assert(PlacementLogic.SnapResize(R(1448, 100, 440, 515), PlacementLogic.EdgeBottom, work, L(todo), min, D, G).Bottom == 620, "bottom edge released near a neighbour's bottom: lines up with it");
            Assert(PlacementLogic.SnapResize(R(500, 300, 400, 500), PlacementLogic.EdgeTopLeft, work, none, min, D, G) == R(500, 300, 400, 500), "nothing nearby: resize untouched");

            Console.WriteLine("Placement core tests passed: " + count);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex.Message); return 1; }
    }
}
