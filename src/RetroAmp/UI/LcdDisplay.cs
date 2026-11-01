using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RetroAmp.Audio;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    /// <summary>The left LCD: play-state icon, 7-segment time and a mini visualizer.</summary>
    public sealed class LcdDisplay : FrameworkElement
    {
        readonly Player player;
        readonly SevenSegment digits = new SevenSegment(15, 26, 3.4);
        public int VisMode; // 0 bars, 1 scope, 2 speakers, 3 off
        public bool ShowRemaining;
        public event Action VisModeChanged;
        public event Action TimeModeChanged;
        static readonly Brush[] barBrushes = new Brush[20];

        static LcdDisplay()
        {
            for (int i = 0; i < barBrushes.Length; i++)
            {
                var b = new SolidColorBrush(Theme.SpectrumColor(i / (double)(barBrushes.Length - 1)));
                b.Freeze();
                barBrushes[i] = b;
            }
        }

        public LcdDisplay(Player p)
        {
            player = p;
            Loaded += (s, e) => FrameTicker.Tick += InvalidateVisual;
            Unloaded += (s, e) => FrameTicker.Tick -= InvalidateVisual;
            Cursor = Cursors.Hand;
            ToolTip = "Click the time to toggle elapsed/remaining · click the analyzer to change its style";
        }

        Rect VisRect => new Rect(8, ActualHeight - 26, ActualWidth - 16, 20);

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var p = e.GetPosition(this);
            if (VisRect.Contains(p)) { VisMode = (VisMode + 1) % 4; VisModeChanged?.Invoke(); }
            else { ShowRemaining = !ShowRemaining; TimeModeChanged?.Invoke(); }
            e.Handled = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            var r = new Rect(0, 0, w, h);
            Theme.Bevel(dc, r, Theme.LcdBg, true);

            // state icon
            var st = player.State;
            double ix = 9, iy = 10;
            var stateBrush = st == PlayState.Playing ? Theme.LcdOn : (st == PlayState.Loading ? Theme.LcdAmber : Theme.LcdDim);
            if (st == PlayState.Playing || st == PlayState.Loading)
                RetroButton.DrawGlyph(dc, GlyphKind.Play, ix + 5, iy + 6, 22, stateBrush);
            else if (st == PlayState.Paused)
            {
                bool blink = (DateTime.Now.Millisecond / 500) == 0;
                RetroButton.DrawGlyph(dc, GlyphKind.Pause, ix + 5, iy + 6, 22, blink ? Theme.LcdOn : Theme.LcdDim);
            }
            else RetroButton.DrawGlyph(dc, GlyphKind.Stop, ix + 5, iy + 6, 22, Theme.LcdDim);

            // time
            var pos = player.Position;
            var dur = player.Duration;
            bool showTime = st != PlayState.Stopped;
            TimeSpan t = pos;
            bool neg = false;
            if (ShowRemaining && dur > TimeSpan.Zero) { t = dur - pos; neg = true; }
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            int mins = (int)t.TotalMinutes, secs = t.Seconds;
            string mm = mins >= 100 ? Math.Min(mins, 999).ToString("000") : mins.ToString("00");
            string text = (neg ? "-" : " ") + mm + secs.ToString("00");
            if (!showTime) text = new string(' ', text.Length);
            if (st == PlayState.Paused && (DateTime.Now.Millisecond / 500) == 1) text = new string(' ', text.Length);

            double x = 24, y = 6;
            for (int i = 0; i < text.Length; i++)
            {
                digits.Draw(dc, text[i], x, y, Theme.LcdOn, Theme.LcdOff);
                x += digits.W + 4;
                if (i == text.Length - 3)
                {
                    dc.DrawRectangle(showTime ? Theme.LcdOn : Theme.LcdOff, null, new Rect(x - 1, y + 7, 3, 3));
                    dc.DrawRectangle(showTime ? Theme.LcdOn : Theme.LcdOff, null, new Rect(x - 2, y + 17, 3, 3));
                    x += 5;
                }
            }

            DrawMiniVis(dc, VisRect);
        }

        void DrawMiniVis(DrawingContext dc, Rect vr)
        {
            var a = Analyzer.Instance;
            switch (VisMode)
            {
                case 0:
                    {
                        int bars = 19;
                        double bw = vr.Width / bars;
                        for (int i = 0; i < bars; i++)
                        {
                            int b0 = i * Analyzer.BandCount / bars, b1 = (i + 1) * Analyzer.BandCount / bars;
                            float v = 0, pk = 0;
                            for (int b = b0; b < b1; b++) { v = Math.Max(v, a.Bands[b]); pk = Math.Max(pk, a.Peaks[b]); }
                            int segs = (int)(v * 10);
                            for (int s = 0; s < segs; s++)
                                dc.DrawRectangle(barBrushes[s * 2], null, new Rect(vr.Left + i * bw, vr.Bottom - (s + 1) * 2, bw - 1, 1.5));
                            int ps = (int)(pk * 10);
                            if (ps > 0) dc.DrawRectangle(Theme.LcdText, null, new Rect(vr.Left + i * bw, vr.Bottom - ps * 2 - 1, bw - 1, 1));
                        }
                        break;
                    }
                case 1:
                    {
                        var g = new StreamGeometry();
                        using (var c = g.Open())
                        {
                            int n = 80;
                            for (int i = 0; i < n; i++)
                            {
                                int k = i * (Analyzer.WaveLength / n);
                                float v = (a.WaveL[k] + a.WaveR[k]) * 0.5f;
                                var p = new Point(vr.Left + i * vr.Width / (n - 1), vr.Top + vr.Height / 2 - Math.Max(-1, Math.Min(1, v * 1.6)) * vr.Height / 2);
                                if (i == 0) c.BeginFigure(p, false, false); else c.LineTo(p, true, false);
                            }
                        }
                        g.Freeze();
                        dc.DrawGeometry(null, new Pen(Theme.LcdText, 1), g);
                        break;
                    }
                case 2:
                    {
                        // two tiny pumping speakers
                        double cy = vr.Top + vr.Height / 2;
                        for (int s = 0; s < 2; s++)
                        {
                            double cx = s == 0 ? vr.Left + 14 : vr.Right - 14;
                            float lvl = s == 0 ? a.LevelL : a.LevelR;
                            double rr = 6 + a.Bass * 4 + a.BeatPulse * 1.5;
                            dc.DrawEllipse(Theme.LcdOff, new Pen(Theme.LcdDim, 1), new Point(cx, cy), 9.5, 9.5);
                            dc.DrawEllipse(null, new Pen(Theme.LcdText, 1), new Point(cx, cy), rr, rr);
                            dc.DrawEllipse(Theme.LcdText, null, new Point(cx, cy), 1.5 + lvl * 3, 1.5 + lvl * 3);
                        }
                        // VU between
                        double mx = vr.Left + 30, mw = vr.Width - 60;
                        dc.DrawRectangle(Theme.LcdOff, null, new Rect(mx, cy - 6, mw, 4));
                        dc.DrawRectangle(Theme.LcdOff, null, new Rect(mx, cy + 2, mw, 4));
                        dc.DrawRectangle(Theme.LcdText, null, new Rect(mx, cy - 6, mw * Math.Min(1, a.VuL), 4));
                        dc.DrawRectangle(Theme.LcdText, null, new Rect(mx, cy + 2, mw * Math.Min(1, a.VuR), 4));
                        break;
                    }
            }
        }
    }
}
