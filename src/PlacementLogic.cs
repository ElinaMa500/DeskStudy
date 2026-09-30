using System;
using System.Collections.Generic;
using System.Drawing;

namespace DeskStudy
{
    // Pure geometry for keeping widgets on screen and apart. All rectangles are the visible window area in screen pixels.
    public static class PlacementLogic
    {
        public const int SnapDistance = 10;
        public const int Gap = 8;

        // Moves (and if needed shrinks) a rectangle so it lies fully inside the work area.
        public static Rectangle Fit(Rectangle r, Rectangle work)
        {
            int width = Math.Min(r.Width, work.Width), height = Math.Min(r.Height, work.Height);
            int x = Math.Max(work.Left, Math.Min(r.X, work.Right - width));
            int y = Math.Max(work.Top, Math.Min(r.Y, work.Bottom - height));
            return new Rectangle(x, y, width, height);
        }

        public static bool Overlaps(Rectangle r, IEnumerable<Rectangle> others)
        {
            foreach (var o in others) if (r.IntersectsWith(o)) return true;
            return false;
        }

        // Keeps a rectangle apart from the others by at least the gap.
        private static bool Clear(Rectangle r, IList<Rectangle> others, int gap)
        {
            foreach (var o in others) if (Rectangle.Inflate(r, gap, gap).IntersectsWith(o)) return false;
            return true;
        }

        // The nearest place for a dropped widget: inside the work area and not covering another widget.
        // Only the dropped widget moves. When nothing fits, it stays where it was dropped, pulled back on screen.
        public static Rectangle Resolve(Rectangle dropped, Rectangle work, IList<Rectangle> others, int gap)
        {
            Rectangle fitted = Fit(dropped, work);
            if (!Overlaps(fitted, others)) return fitted;
            Rectangle best;
            if (Nearest(dropped, fitted, work, others, gap, out best)) return best;
            if (gap > 0 && Nearest(dropped, fitted, work, others, 0, out best)) return best;
            return fitted;
        }

        private static bool Nearest(Rectangle dropped, Rectangle fitted, Rectangle work, IList<Rectangle> others, int gap, out Rectangle best)
        {
            int w = fitted.Width, h = fitted.Height;
            var xs = new List<int> { fitted.X, work.Left, work.Right - w };
            var ys = new List<int> { fitted.Y, work.Top, work.Bottom - h };
            foreach (var o in others)
            {
                xs.Add(o.Left - gap - w); xs.Add(o.Right + gap); xs.Add(o.Left); xs.Add(o.Right - w);
                ys.Add(o.Top - gap - h); ys.Add(o.Bottom + gap); ys.Add(o.Top); ys.Add(o.Bottom - h);
            }
            best = fitted; long bestDistance = long.MaxValue;
            foreach (int x in xs)
                foreach (int y in ys)
                {
                    var candidate = new Rectangle(x, y, w, h);
                    if (!work.Contains(candidate) || !Clear(candidate, others, gap)) continue;
                    long dx = x - dropped.X, dy = y - dropped.Y, distance = dx * dx + dy * dy;
                    if (distance < bestDistance) { bestDistance = distance; best = candidate; }
                }
            return bestDistance != long.MaxValue;
        }

        // Resize edges as reported by WM_SIZING.
        public const int EdgeLeft = 1, EdgeRight = 2, EdgeTop = 3, EdgeTopLeft = 4, EdgeTopRight = 5, EdgeBottom = 6, EdgeBottomLeft = 7, EdgeBottomRight = 8;
        public static bool MovesLeft(int edge) { return edge == EdgeLeft || edge == EdgeTopLeft || edge == EdgeBottomLeft; }
        public static bool MovesRight(int edge) { return edge == EdgeRight || edge == EdgeTopRight || edge == EdgeBottomRight; }
        public static bool MovesTop(int edge) { return edge == EdgeTop || edge == EdgeTopLeft || edge == EdgeTopRight; }
        public static bool MovesBottom(int edge) { return edge == EdgeBottom || edge == EdgeBottomLeft || edge == EdgeBottomRight; }

        // After a resize: the dragged edges are pulled back to the screen edge, and stopped short of a neighbour.
        // If that would make the widget smaller than its minimum, the widget is moved instead.
        public static Rectangle AfterResize(Rectangle r, int edge, Rectangle work, IList<Rectangle> others, Size minimum, int gap)
        {
            int left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;
            if (MovesLeft(edge)) left = Math.Max(left, work.Left);
            if (MovesRight(edge)) right = Math.Min(right, work.Right);
            if (MovesTop(edge)) top = Math.Max(top, work.Top);
            if (MovesBottom(edge)) bottom = Math.Min(bottom, work.Bottom);
            for (int pass = 0; pass < others.Count; pass++)
            {
                bool changed = false;
                foreach (var o in others)
                {
                    var current = Rectangle.FromLTRB(left, top, right, bottom);
                    if (!current.IntersectsWith(o)) continue;
                    // Each dragged edge that could back off gives one way out; the one keeping the most area wins.
                    int bestArea = -1, nl = left, nt = top, nr = right, nb = bottom;
                    Action<int, int, int, int> consider = delegate(int l, int t, int rr, int b)
                    {
                        if (rr - l < minimum.Width || b - t < minimum.Height) return;
                        var option = Rectangle.FromLTRB(l, t, rr, b);
                        if (option.IntersectsWith(o)) return;
                        int area = option.Width * option.Height;
                        if (area > bestArea) { bestArea = area; nl = l; nt = t; nr = rr; nb = b; }
                    };
                    if (MovesRight(edge) && o.Left > left) consider(left, top, Math.Min(right, o.Left - gap), bottom);
                    if (MovesLeft(edge) && o.Right < right) consider(Math.Max(left, o.Right + gap), top, right, bottom);
                    if (MovesBottom(edge) && o.Top > top) consider(left, top, right, Math.Min(bottom, o.Top - gap));
                    if (MovesTop(edge) && o.Bottom < bottom) consider(left, Math.Max(top, o.Bottom + gap), right, bottom);
                    if (bestArea < 0) continue;
                    left = nl; top = nt; right = nr; bottom = nb; changed = true;
                }
                if (!changed) break;
            }
            var result = Rectangle.FromLTRB(left, top, right, bottom);
            if (result.Width < minimum.Width || result.Height < minimum.Height)
            {
                // Too small once trimmed: keep the size the user dragged to (at least the minimum) and move the widget instead.
                int width = Math.Max(minimum.Width, Math.Min(r.Width, Math.Max(result.Width, minimum.Width)));
                int height = Math.Max(minimum.Height, Math.Min(r.Height, Math.Max(result.Height, minimum.Height)));
                result = new Rectangle(MovesLeft(edge) ? r.Right - width : r.Left, MovesTop(edge) ? r.Bottom - height : r.Top, width, height);
            }
            if (!work.Contains(result) || Overlaps(result, others)) return Resolve(result, work, others, gap);
            return result;
        }

        // While dragging: pulls the window onto a nearby screen edge or next to a nearby widget.
        public static Rectangle SnapMove(Rectangle r, Rectangle work, IList<Rectangle> others, int distance, int gap)
        {
            int dx = Closest(distance, new[] { work.Left - r.Left, work.Right - r.Right });
            int dy = Closest(distance, new[] { work.Top - r.Top, work.Bottom - r.Bottom });
            foreach (var o in others)
            {
                bool besideRows = r.Top < o.Bottom + distance && r.Bottom > o.Top - distance;
                bool besideColumns = r.Left < o.Right + distance && r.Right > o.Left - distance;
                if (besideRows)
                {
                    dx = Closer(dx, distance, o.Right + gap - r.Left, o.Left - gap - r.Right);
                    // Side by side: line the tops or bottoms up.
                    if (Adjacent(r.Left, r.Right, o.Left, o.Right, gap, distance)) dy = Closer(dy, distance, o.Top - r.Top, o.Bottom - r.Bottom);
                }
                if (besideColumns)
                {
                    dy = Closer(dy, distance, o.Bottom + gap - r.Top, o.Top - gap - r.Bottom);
                    // Stacked: line the left or right edges up.
                    if (Adjacent(r.Top, r.Bottom, o.Top, o.Bottom, gap, distance)) dx = Closer(dx, distance, o.Left - r.Left, o.Right - r.Right);
                }
            }
            r.Offset(dx == int.MaxValue ? 0 : dx, dy == int.MaxValue ? 0 : dy);
            return r;
        }

        // While resizing: the dragged edges snap to a nearby screen edge or next to / in line with a nearby widget.
        public static Rectangle SnapResize(Rectangle r, int edge, Rectangle work, IList<Rectangle> others, Size minimum, int distance, int gap)
        {
            int left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;
            if (MovesLeft(edge) || MovesRight(edge))
            {
                bool leftEdge = MovesLeft(edge); int x = leftEdge ? left : right;
                var targets = new List<int> { leftEdge ? work.Left : work.Right };
                foreach (var o in others)
                {
                    if (!(top < o.Bottom + distance && bottom > o.Top - distance)) continue;
                    targets.Add(leftEdge ? o.Right + gap : o.Left - gap);
                    targets.Add(leftEdge ? o.Left : o.Right);
                }
                int snapped = SnapValue(x, targets, distance);
                if (leftEdge && right - snapped >= minimum.Width) left = snapped;
                if (!leftEdge && snapped - left >= minimum.Width) right = snapped;
            }
            if (MovesTop(edge) || MovesBottom(edge))
            {
                bool topEdge = MovesTop(edge); int y = topEdge ? top : bottom;
                var targets = new List<int> { topEdge ? work.Top : work.Bottom };
                foreach (var o in others)
                {
                    if (!(left < o.Right + distance && right > o.Left - distance)) continue;
                    targets.Add(topEdge ? o.Bottom + gap : o.Top - gap);
                    targets.Add(topEdge ? o.Top : o.Bottom);
                }
                int snapped = SnapValue(y, targets, distance);
                if (topEdge && bottom - snapped >= minimum.Height) top = snapped;
                if (!topEdge && snapped - top >= minimum.Height) bottom = snapped;
            }
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static bool Adjacent(int start, int end, int otherStart, int otherEnd, int gap, int distance)
        {
            return Math.Abs(start - (otherEnd + gap)) <= distance || Math.Abs(end - (otherStart - gap)) <= distance;
        }
        private static int SnapValue(int value, List<int> targets, int distance)
        {
            int best = value, bestDelta = distance + 1;
            foreach (int t in targets) { int d = Math.Abs(t - value); if (d < bestDelta) { bestDelta = d; best = t; } }
            return best;
        }
        private static int Closest(int distance, int[] deltas)
        {
            int best = int.MaxValue;
            foreach (int d in deltas) best = Closer(best, distance, d);
            return best;
        }
        private static int Closer(int current, int distance, params int[] deltas)
        {
            foreach (int d in deltas)
                if (Math.Abs(d) <= distance && (current == int.MaxValue || Math.Abs(d) < Math.Abs(current))) current = d;
            return current;
        }
    }
}
