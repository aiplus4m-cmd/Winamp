using System;
using System.Windows;
using System.Windows.Media;
using RetroAmp.Core;

namespace RetroAmp.UI
{
    /// <summary>Small graph showing the EQ response curve.</summary>
    public sealed class EqCurve : FrameworkElement
    {
        readonly Settings s;
        public EqCurve(Settings settings) { s = settings; }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            Theme.Bevel(dc, new Rect(0, 0, w, h), Theme.LcdBg, true);
            dc.DrawLine(Theme.P(Theme.LcdOff, 1), new Point(2, h / 2), new Point(w - 2, h / 2));
            var pre = s.Preamp / 12.0;
            dc.DrawLine(Theme.P(Theme.LcdAmber, 1), new Point(2, h / 2 - pre * (h / 2 - 3)), new Point(10, h / 2 - pre * (h / 2 - 3)));
            var bands = s.EqBands;
            int n = bands.Length;
            var pts = new Point[n];
            for (int i = 0; i < n; i++)
            {
                double x = 14 + i * (w - 20) / (n - 1);
                double y = h / 2 - Util.Clamp(bands[i] / 12.0, -1, 1) * (h / 2 - 3);
                pts[i] = new Point(x, y);
            }
            // Catmull-Rom smoothing
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(pts[0], false, false);
                for (int i = 0; i < n - 1; i++)
                {
                    var p0 = pts[Math.Max(0, i - 1)]; var p1 = pts[i]; var p2 = pts[i + 1]; var p3 = pts[Math.Min(n - 1, i + 2)];
                    for (int k = 1; k <= 8; k++)
                    {
                        double t = k / 8.0, t2 = t * t, t3 = t2 * t;
                        double x = 0.5 * (2 * p1.X + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
                        double y = 0.5 * (2 * p1.Y + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
                        c.LineTo(new Point(x, Util.Clamp(y, 1, h - 1)), true, false);
                    }
                }
            }
            g.Freeze();
            dc.DrawGeometry(null, Theme.P(s.EqEnabled ? Theme.LcdText : Theme.LcdDim, 1.3), g);
        }
    }
}
