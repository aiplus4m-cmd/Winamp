using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace RetroAmp.UI
{
    /// <summary>Renders LCD-style 7-segment digits.</summary>
    public sealed class SevenSegment
    {
        readonly Geometry[] seg = new Geometry[7];
        public readonly double W, H;
        static readonly Dictionary<char, string> Map = new Dictionary<char, string>
        {
            ['0'] = "abcdef", ['1'] = "bc", ['2'] = "abged", ['3'] = "abgcd", ['4'] = "fgbc",
            ['5'] = "afgcd", ['6'] = "afgedc", ['7'] = "abc", ['8'] = "abcdefg", ['9'] = "abcdfg",
            ['-'] = "g", [' '] = ""
        };

        public SevenSegment(double w, double h, double thick)
        {
            W = w; H = h;
            double t = thick, g = 0.8;
            double left = t / 2, right = w - t / 2, top = t / 2, mid = h / 2, bottom = h - t / 2;
            seg[0] = Hz(top, left + g, right - g, t);
            seg[1] = Vt(right, top + g, mid - g, t);
            seg[2] = Vt(right, mid + g, bottom - g, t);
            seg[3] = Hz(bottom, left + g, right - g, t);
            seg[4] = Vt(left, mid + g, bottom - g, t);
            seg[5] = Vt(left, top + g, mid - g, t);
            seg[6] = Hz(mid, left + g, right - g, t);
        }

        static Geometry Hz(double c, double xa, double xb, double t) => Poly(
            new Point(xa, c), new Point(xa + t / 2, c - t / 2), new Point(xb - t / 2, c - t / 2),
            new Point(xb, c), new Point(xb - t / 2, c + t / 2), new Point(xa + t / 2, c + t / 2));

        static Geometry Vt(double c, double ya, double yb, double t) => Poly(
            new Point(c, ya), new Point(c + t / 2, ya + t / 2), new Point(c + t / 2, yb - t / 2),
            new Point(c, yb), new Point(c - t / 2, yb - t / 2), new Point(c - t / 2, ya + t / 2));

        static Geometry Poly(params Point[] pts)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(pts[0], true, true);
                for (int i = 1; i < pts.Length; i++) ctx.LineTo(pts[i], true, false);
            }
            g.Freeze();
            return g;
        }

        public void Draw(DrawingContext dc, char c, double x, double y, Brush on, Brush off)
        {
            Map.TryGetValue(c, out string lit);
            lit = lit ?? "";
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new SkewTransform(-7, 0, W / 2, H / 2));
            for (int i = 0; i < 7; i++)
            {
                bool isOn = lit.IndexOf((char)('a' + i)) >= 0;
                if (isOn || off != null) dc.DrawGeometry(isOn ? on : off, null, seg[i]);
            }
            dc.Pop(); dc.Pop();
        }
    }
}
