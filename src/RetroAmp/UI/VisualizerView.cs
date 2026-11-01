using System;
using System.Windows;
using System.Windows.Media;
using RetroAmp.Audio;

namespace RetroAmp.UI
{
    public enum VisMode { Spectrum = 0, Scope = 1, Speakers = 2, Radial = 3, Starfield = 4 }

    /// <summary>Full-size visualizer surface with several modes. Redrawn every frame.</summary>
    public sealed class VisualizerView : FrameworkElement
    {
        public static readonly string[] ModeNames = { "Spectrum Analyzer", "Oscilloscope", "Speakers & VU Meters", "Radial Pulse", "Starfield" };
        public VisMode Mode { get; set; } = VisMode.Speakers;
        public string Caption { get; set; }
        double hue;
        double modeShownAt = -10;
        float needleL, needleR;
        readonly Star[] stars = new Star[420];
        readonly Random rnd = new Random();
        static readonly Brush[] spectrumBrushes = new Brush[64];

        struct Star { public double X, Y, Z; }

        static VisualizerView()
        {
            for (int i = 0; i < spectrumBrushes.Length; i++)
            {
                var b = new SolidColorBrush(Theme.SpectrumColor(i / 63.0)); b.Freeze(); spectrumBrushes[i] = b;
            }
        }

        public VisualizerView()
        {
            for (int i = 0; i < stars.Length; i++) stars[i] = NewStar(true);
            Loaded += (s, e) => FrameTicker.Tick += InvalidateVisual;
            Unloaded += (s, e) => FrameTicker.Tick -= InvalidateVisual;
            ClipToBounds = true;
        }

        public void ShowModeName() => modeShownAt = Analyzer.Instance.Time;

        Star NewStar(bool anyDepth) => new Star
        {
            X = rnd.NextDouble() * 2 - 1,
            Y = rnd.NextDouble() * 2 - 1,
            Z = anyDepth ? rnd.NextDouble() * 0.95 + 0.05 : 1
        };

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 10 || h < 10) return;
            var a = Analyzer.Instance;
            hue += FrameTicker.Dt * (12 + a.Bass * 60);
            switch (Mode)
            {
                case VisMode.Spectrum: DrawSpectrum(dc, w, h, a); break;
                case VisMode.Scope: DrawScope(dc, w, h, a); break;
                case VisMode.Speakers: DrawSpeakers(dc, w, h, a); break;
                case VisMode.Radial: DrawRadial(dc, w, h, a); break;
                case VisMode.Starfield: DrawStars(dc, w, h, a); break;
            }

            double since = a.Time - modeShownAt;
            if (since < 2.5)
            {
                byte alpha = (byte)(255 * Math.Min(1, (2.5 - since) / 0.8));
                var ft = Theme.Text(this, ModeNames[(int)Mode], 16, new SolidColorBrush(Color.FromArgb(alpha, 255, 200, 90)), true);
                dc.DrawText(ft, new Point(14, 10));
            }
            if (!string.IsNullOrEmpty(Caption))
            {
                var ft = Theme.Text(this, Caption, 12, new SolidColorBrush(Color.FromArgb(150, 220, 225, 240)), false);
                ft.MaxTextWidth = Math.Max(10, w - 28);
                ft.MaxLineCount = 1;
                ft.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(ft, new Point(14, h - ft.Height - 8));
            }
        }

        static void Background(DrawingContext dc, double w, double h, Color top, Color bottom)
        {
            var g = new LinearGradientBrush(top, bottom, 90);
            dc.DrawRectangle(g, null, new Rect(0, 0, w, h));
        }

        // ------------------------------------------------------------------ spectrum
        void DrawSpectrum(DrawingContext dc, double w, double h, Analyzer a)
        {
            Background(dc, w, h, Color.FromRgb(6, 8, 14), Color.FromRgb(14, 16, 28));
            int n = Analyzer.BandCount;
            double floor = h * 0.78, maxH = floor - 20;
            double bw = (w - 20) / n;
            for (int i = 0; i < n; i++)
            {
                double x = 10 + i * bw;
                double bh = a.Bands[i] * maxH;
                int segs = (int)(bh / 6);
                for (int s = 0; s < segs; s++)
                {
                    int ci = Math.Min(63, (int)(s * 6 / maxH * 63));
                    dc.DrawRectangle(spectrumBrushes[ci], null, new Rect(x + 1, floor - (s + 1) * 6 + 1, bw - 2, 4.5));
                }
                double py = floor - a.Peaks[i] * maxH;
                if (a.Peaks[i] > 0.02f) dc.DrawRectangle(Brushes.White, null, new Rect(x + 1, py - 3, bw - 2, 2));
                // reflection
                if (bh > 1)
                {
                    var rc = Theme.SpectrumColor(a.Bands[i]);
                    var refl = new SolidColorBrush(Color.FromArgb(55, rc.R, rc.G, rc.B));
                    dc.DrawRectangle(refl, null, new Rect(x + 1, floor + 3, bw - 2, Math.Min(h - floor - 3, bh * 0.25)));
                }
            }
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(80, 120, 140, 200)), null, new Rect(0, floor + 1, w, 1));
        }

        // ------------------------------------------------------------------ scope
        void DrawScope(DrawingContext dc, double w, double h, Analyzer a)
        {
            Background(dc, w, h, Color.FromRgb(2, 10, 8), Color.FromRgb(4, 18, 16));
            var grid = Theme.P(new SolidColorBrush(Color.FromArgb(40, 60, 255, 140)), 1);
            for (int i = 1; i < 10; i++) dc.DrawLine(grid, new Point(w * i / 10, 0), new Point(w * i / 10, h));
            for (int i = 1; i < 6; i++) dc.DrawLine(grid, new Point(0, h * i / 6), new Point(w, h * i / 6));
            DrawWave(dc, a.WaveL, w, h * 0.30, h * 0.26, Theme.Hsv(hue + 120, 0.8, 1));
            DrawWave(dc, a.WaveR, w, h * 0.72, h * 0.26, Theme.Hsv(hue + 200, 0.8, 1));
        }

        static void DrawWave(DrawingContext dc, float[] data, double w, double cy, double amp, Color col)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                int n = Math.Min(data.Length, (int)w);
                for (int i = 0; i < n; i++)
                {
                    int k = i * data.Length / n;
                    var p = new Point(i * w / (n - 1), cy - Math.Max(-1.2, Math.Min(1.2, data[k] * 1.4)) * amp);
                    if (i == 0) c.BeginFigure(p, false, false); else c.LineTo(p, true, false);
                }
            }
            g.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(60, col.R, col.G, col.B)), 6), g);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(col), 1.6), g);
        }

        // ------------------------------------------------------------------ speakers
        void DrawSpeakers(DrawingContext dc, double w, double h, Analyzer a)
        {
            // room
            var bg = new RadialGradientBrush(Color.FromRgb(40, 30, 60), Color.FromRgb(6, 6, 12)) { Center = new Point(0.5, 0.35), GradientOrigin = new Point(0.5, 0.35), RadiusX = 0.8, RadiusY = 0.9 };
            dc.DrawRectangle(bg, null, new Rect(0, 0, w, h));
            double floorY = h * 0.9;
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(24, 18, 30), Color.FromRgb(8, 6, 10), 90), null, new Rect(0, floorY, w, h - floorY));

            double ch = Math.Min(h * 0.8, w * 0.52);
            double cw = ch * 0.56;
            double top = floorY - ch;
            double shake = a.BeatPulse * 3.0;
            var glow = Theme.Hsv(hue, 0.85, 1);

            // beat flash on the wall
            if (a.BeatPulse > 0.01)
            {
                var flash = new RadialGradientBrush(Color.FromArgb((byte)(a.BeatPulse * 70), glow.R, glow.G, glow.B), Color.FromArgb(0, glow.R, glow.G, glow.B));
                dc.DrawRectangle(flash, null, new Rect(0, 0, w, floorY));
            }

            DrawCabinet(dc, w * 0.04 + (w * 0.18 - cw / 2 > 0 ? w * 0.18 - cw / 2 : 0), top - shake, cw, ch, a.LevelL, a, glow, true);
            DrawCabinet(dc, w * 0.96 - cw - (w * 0.18 - cw / 2 > 0 ? w * 0.18 - cw / 2 : 0), top + shake * 0.6, cw, ch, a.LevelR, a, glow, false);

            // centre: VU meters + mini spectrum
            double leftEdge = w * 0.04 + Math.Max(0, w * 0.18 - cw / 2) + cw + 16;
            double rightEdge = w * 0.96 - Math.Max(0, w * 0.18 - cw / 2) - cw - 16;
            double cwid = rightEdge - leftEdge;
            if (cwid > 80)
            {
                needleL += (a.VuL - needleL) * 0.35f;
                needleR += (a.VuR - needleR) * 0.35f;
                double mh = Math.Min(ch * 0.36, cwid * 0.42);
                double mw = Math.Min(cwid / 2 - 6, mh * 1.5);
                double my = top + ch * 0.08;
                double mx = leftEdge + (cwid - mw * 2 - 12) / 2;
                DrawVu(dc, new Rect(mx, my, mw, mh), needleL, "L");
                DrawVu(dc, new Rect(mx + mw + 12, my, mw, mh), needleR, "R");

                // equalizer bars under the meters
                double sy = my + mh + 18, sh = floorY - sy - 14;
                if (sh > 20)
                {
                    int n = 32;
                    double bw = (mw * 2 + 12) / n;
                    for (int i = 0; i < n; i++)
                    {
                        float v = Math.Max(a.Bands[i * 2], a.Bands[i * 2 + 1]);
                        int segs = (int)(v * 14);
                        for (int s = 0; s < 14; s++)
                        {
                            bool on = s < segs;
                            var col = Theme.Hsv(hue + i * 6 + s * 4, on ? 0.9 : 0.4, on ? 1 : 0.18);
                            dc.DrawRectangle(new SolidColorBrush(col), null, new Rect(mx + i * bw + 1, sy + sh - (s + 1) * (sh / 14) + 1, bw - 2, sh / 14 - 2));
                        }
                    }
                }
            }
        }

        void DrawCabinet(DrawingContext dc, double x, double y, double cw, double ch, float level, Analyzer a, Color glow, bool left)
        {
            // shadow on floor
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), null, new Point(x + cw / 2, y + ch + 4), cw * 0.6, 6);
            var body = new LinearGradientBrush(Color.FromRgb(46, 44, 52), Color.FromRgb(18, 17, 22), new Point(0, 0), new Point(1, 1));
            dc.DrawRoundedRectangle(body, new Pen(new SolidColorBrush(Color.FromRgb(70, 68, 80)), 1.5), new Rect(x, y, cw, ch), 8, 8);
            // wood side trim
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(110, 66, 30), Color.FromRgb(60, 34, 14), 0), null,
                new Rect(left ? x + cw - 6 : x, y + 8, 6, ch - 16));

            // tweeter
            double tcx = x + cw / 2, tcy = y + ch * 0.17, tr = cw * 0.14;
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(10, 10, 12)), new Pen(new SolidColorBrush(Color.FromRgb(90, 90, 100)), 1.5), new Point(tcx, tcy), tr * 1.25, tr * 1.25);
            double dome = tr * (0.62 + a.Treble * 0.35);
            var domeBrush = new RadialGradientBrush(Color.FromRgb(230, 230, 240), Color.FromRgb(60, 60, 70)) { GradientOrigin = new Point(0.35, 0.3) };
            dc.DrawEllipse(domeBrush, null, new Point(tcx, tcy), dome, dome);

            // woofer
            double wcx = x + cw / 2, wcy = y + ch * 0.58, wr = cw * 0.40;
            float bass = Math.Max(a.Bass, a.BassRaw) * (0.6f + level * 0.6f);
            double pump = 1 + bass * 0.10 + a.BeatPulse * 0.05;
            // LED ring
            var ring = Color.FromArgb((byte)(80 + Math.Min(175, bass * 200 + a.BeatPulse * 100)), glow.R, glow.G, glow.B);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(ring), 3 + a.BeatPulse * 3), new Point(wcx, wcy), wr * 1.12, wr * 1.12);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(14, 14, 16)), new Pen(new SolidColorBrush(Color.FromRgb(100, 100, 112)), 2), new Point(wcx, wcy), wr * 1.05, wr * 1.05);
            // rubber surround
            var surround = new RadialGradientBrush(Color.FromRgb(70, 70, 78), Color.FromRgb(20, 20, 24)) { GradientOrigin = new Point(0.4, 0.35) };
            dc.DrawEllipse(surround, null, new Point(wcx, wcy), wr * pump, wr * pump);
            // cone
            double cone = wr * 0.84 * pump;
            var coneBrush = new RadialGradientBrush();
            coneBrush.GradientOrigin = new Point(0.5 - bass * 0.08, 0.45 - bass * 0.08);
            coneBrush.GradientStops.Add(new GradientStop(Color.FromRgb((byte)(50 + bass * 60), (byte)(50 + bass * 60), (byte)(58 + bass * 60)), 0));
            coneBrush.GradientStops.Add(new GradientStop(Color.FromRgb(28, 28, 32), 0.75));
            coneBrush.GradientStops.Add(new GradientStop(Color.FromRgb(12, 12, 14), 1));
            dc.DrawEllipse(coneBrush, null, new Point(wcx, wcy), cone, cone);
            // concentric ridges
            var ridge = new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1);
            for (int i = 1; i <= 3; i++) dc.DrawEllipse(null, ridge, new Point(wcx, wcy), cone * (0.4 + i * 0.15), cone * (0.4 + i * 0.15));
            // dust cap
            double cap = wr * 0.28 * (1 + bass * 0.35 + a.BeatPulse * 0.15);
            var capBrush = new RadialGradientBrush(Color.FromRgb(200, 200, 210), Color.FromRgb(30, 30, 36)) { GradientOrigin = new Point(0.35, 0.3) };
            dc.DrawEllipse(capBrush, null, new Point(wcx, wcy), cap, cap);

            // bass port
            dc.DrawEllipse(Brushes.Black, new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 90)), 1.5), new Point(wcx, y + ch * 0.92), cw * 0.12, cw * 0.05);
            // power LED
            dc.DrawEllipse(new SolidColorBrush(level > 0.02 ? Color.FromRgb(80, 255, 120) : Color.FromRgb(30, 60, 30)), null, new Point(x + cw - 14, y + ch - 10), 2.5, 2.5);
        }

        void DrawVu(DrawingContext dc, Rect r, float level, string label)
        {
            var face = new LinearGradientBrush(Color.FromRgb(250, 238, 200), Color.FromRgb(225, 205, 150), 90);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(30, 28, 34)), null, new Rect(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8), 6, 6);
            dc.DrawRoundedRectangle(face, null, r, 4, 4);
            // backlight glow
            dc.DrawRoundedRectangle(new RadialGradientBrush(Color.FromArgb(70, 255, 190, 80), Color.FromArgb(0, 255, 190, 80)), null, r, 4, 4);
            var pivot = new Point(r.X + r.Width / 2, r.Bottom + r.Height * 0.15);
            double radius = r.Height * 0.95;
            double a0 = -50, a1 = 50;
            var tick = new Pen(new SolidColorBrush(Color.FromRgb(40, 30, 20)), 1.2);
            var red = new Pen(new SolidColorBrush(Color.FromRgb(200, 30, 20)), 2.4);
            dc.PushClip(new RectangleGeometry(new Rect(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4)));
            for (int i = 0; i <= 10; i++)
            {
                double ang = (a0 + (a1 - a0) * i / 10.0) * Math.PI / 180;
                var p1 = new Point(pivot.X + Math.Sin(ang) * radius * 0.78, pivot.Y - Math.Cos(ang) * radius * 0.78);
                var p2 = new Point(pivot.X + Math.Sin(ang) * radius * (i % 5 == 0 ? 0.92 : 0.87), pivot.Y - Math.Cos(ang) * radius * (i % 5 == 0 ? 0.92 : 0.87));
                dc.DrawLine(i >= 8 ? red : tick, p1, p2);
            }
            dc.Pop();
            var vu = Theme.Text(this, "VU", Math.Max(8, r.Height * 0.14), new SolidColorBrush(Color.FromRgb(60, 40, 20)), true);
            dc.DrawText(vu, new Point(r.X + r.Width / 2 - vu.Width / 2, r.Y + r.Height * 0.52));
            var lb = Theme.Text(this, label, Math.Max(8, r.Height * 0.12), new SolidColorBrush(Color.FromRgb(90, 60, 30)), true);
            dc.DrawText(lb, new Point(r.X + 6, r.Bottom - lb.Height - 3));
            double na = (a0 + (a1 - a0) * Math.Min(1.05, level)) * Math.PI / 180;
            dc.PushClip(new RectangleGeometry(r));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(20, 16, 12)), 1.8), pivot,
                new Point(pivot.X + Math.Sin(na) * radius * 0.93, pivot.Y - Math.Cos(na) * radius * 0.93));
            dc.Pop();
            // peak LED
            dc.DrawEllipse(new SolidColorBrush(level > 0.85 ? Color.FromRgb(255, 40, 30) : Color.FromRgb(80, 20, 20)), null, new Point(r.Right - 9, r.Y + 9), 3.5, 3.5);
        }

        // ------------------------------------------------------------------ radial
        void DrawRadial(DrawingContext dc, double w, double h, Analyzer a)
        {
            Background(dc, w, h, Color.FromRgb(4, 2, 10), Color.FromRgb(10, 4, 20));
            var c = new Point(w / 2, h / 2);
            double baseR = Math.Min(w, h) * 0.18 * (1 + a.Bass * 0.35 + a.BeatPulse * 0.12);
            var core = Theme.Hsv(hue, 0.9, 1);
            var glow = new RadialGradientBrush(Color.FromArgb(160, core.R, core.G, core.B), Color.FromArgb(0, core.R, core.G, core.B));
            dc.DrawEllipse(glow, null, c, baseR * 2.2, baseR * 2.2);
            int spokes = 128;
            double rot = a.Time * 0.25;
            double maxLen = Math.Min(w, h) * 0.32;
            for (int i = 0; i < spokes; i++)
            {
                int bi = i < spokes / 2 ? i : spokes - 1 - i;
                float v = a.Bands[Math.Min(Analyzer.BandCount - 1, bi * Analyzer.BandCount / (spokes / 2))];
                double ang = rot + i * 2 * Math.PI / spokes;
                double len = 4 + v * maxLen;
                var p1 = new Point(c.X + Math.Cos(ang) * baseR, c.Y + Math.Sin(ang) * baseR);
                var p2 = new Point(c.X + Math.Cos(ang) * (baseR + len), c.Y + Math.Sin(ang) * (baseR + len));
                var col = Theme.Hsv(hue + i * 2.8, 0.85, 0.6 + v * 0.4);
                dc.DrawLine(new Pen(new SolidColorBrush(col), Math.Max(1.5, 2 * Math.PI * baseR / spokes * 0.6)), p1, p2);
            }
            var inner = new RadialGradientBrush(Color.FromRgb(20, 10, 30), Color.FromRgb(0, 0, 0));
            dc.DrawEllipse(inner, new Pen(new SolidColorBrush(core), 2), c, baseR * 0.92, baseR * 0.92);
            // inner oscilloscope ring
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                int n = 180;
                for (int i = 0; i <= n; i++)
                {
                    int k = (i % n) * Analyzer.WaveLength / n;
                    double rr = baseR * (0.6 + Math.Max(-0.3, Math.Min(0.3, a.WaveL[k] * 0.5)));
                    double ang = -rot + i * 2 * Math.PI / n;
                    var p = new Point(c.X + Math.Cos(ang) * rr, c.Y + Math.Sin(ang) * rr);
                    if (i == 0) ctx.BeginFigure(p, false, true); else ctx.LineTo(p, true, false);
                }
            }
            g.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Theme.Hsv(hue + 180, 0.7, 1)), 1.5), g);
        }

        // ------------------------------------------------------------------ starfield
        void DrawStars(DrawingContext dc, double w, double h, Analyzer a)
        {
            byte flash = (byte)(a.BeatPulse * 40);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(flash, flash, (byte)Math.Min(255, flash + 8))), null, new Rect(0, 0, w, h));
            double speed = (0.12 + a.Bass * 1.6 + a.BeatPulse * 0.8) * FrameTicker.Dt;
            double cx = w / 2, cy = h / 2, scale = Math.Max(w, h) * 0.5;
            for (int i = 0; i < stars.Length; i++)
            {
                var s = stars[i];
                double oz = s.Z;
                s.Z -= speed;
                if (s.Z <= 0.02) { stars[i] = NewStar(false); continue; }
                double x = cx + s.X / s.Z * scale, y = cy + s.Y / s.Z * scale;
                double px = cx + s.X / oz * scale, py = cy + s.Y / oz * scale;
                if (x < 0 || y < 0 || x > w || y > h) { stars[i] = NewStar(false); continue; }
                double bright = 1 - s.Z;
                var col = Theme.Hsv(hue + i * 0.7, 0.5 * a.Mid + 0.15, Math.Min(1, 0.3 + bright));
                dc.DrawLine(new Pen(new SolidColorBrush(col), 0.6 + bright * 2.4), new Point(px, py), new Point(x, y));
                stars[i] = s;
            }
        }
    }
}
