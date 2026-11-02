using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RetroAmp.Core;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    /// <summary>Scrolling, karaoke-style lyrics (current line highlighted with a colour wipe).</summary>
    public sealed class LyricsView : FrameworkElement
    {
        readonly Player player;
        readonly LyricsController lyrics;
        public double FontSize = 18;
        double scroll;            // smoothed scroll position in px (centre of the current line)
        double userScroll;        // manual offset from wheel
        DateTime userScrollAt = DateTime.MinValue;
        readonly List<double> tops = new List<double>();
        readonly List<FormattedText> cache = new List<FormattedText>();
        Lyrics cachedFor; double cachedWidth; double cachedSize; ThemeDef cachedTheme;
        double totalHeight;

        public LyricsView(Player p, LyricsController l)
        {
            player = p; lyrics = l;
            ClipToBounds = true;
            Loaded += (s, e) => FrameTicker.Tick += InvalidateVisual;
            Unloaded += (s, e) => FrameTicker.Tick -= InvalidateVisual;
            lyrics.Changed += () => { cachedFor = null; userScroll = 0; };
            Cursor = Cursors.Hand;
        }

        double LineGap => FontSize * 0.75;

        void BuildCache(Lyrics ly, double width)
        {
            cache.Clear(); tops.Clear();
            double y = 0;
            foreach (var line in ly.Lines)
            {
                string text = string.IsNullOrWhiteSpace(line.Text) ? "♪" : line.Text;
                var ft = Theme.Text(this, text, FontSize, Theme.LcdDim, true);
                ft.MaxTextWidth = Math.Max(40, width - 40);
                ft.TextAlignment = TextAlignment.Center;
                tops.Add(y);
                cache.Add(ft);
                y += ft.Height + LineGap;
            }
            totalHeight = y;
            cachedFor = ly; cachedWidth = width; cachedSize = FontSize; cachedTheme = Theme.Current;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            userScroll -= e.Delta * 0.6;
            userScrollAt = DateTime.Now;
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            var ly = lyrics.Current;
            if (ly == null || !ly.Synced || cache.Count == 0) return;
            double y = e.GetPosition(this).Y - ActualHeight / 2 + scroll + userScroll;
            for (int i = 0; i < tops.Count; i++)
            {
                if (y >= tops[i] - LineGap / 2 && y < tops[i] + cache[i].Height + LineGap / 2)
                {
                    player.Seek(ly.Lines[i].Time - TimeSpan.FromMilliseconds(ly.OffsetMs) + TimeSpan.FromMilliseconds(player.UsingMedia ? 0 : 170));
                    userScroll = 0;
                    e.Handled = true;
                    return;
                }
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 20 || h < 20) return;
            dc.DrawRectangle(Theme.LcdBg, null, new Rect(0, 0, w, h));
            var ly = lyrics.Current;
            if (ly == null)
            {
                string msg = lyrics.Busy ? "♪  Searching lyrics…  ♪" : (player.Current == null ? "♪  RetroAmp lyrics  ♪" : "♪  " + (string.IsNullOrEmpty(lyrics.Status) ? "No lyrics" : lyrics.Status) + "  ♪");
                var ft = Theme.Text(this, msg, FontSize * 0.9, Theme.LcdDim, true);
                ft.MaxTextWidth = Math.Max(40, w - 30); ft.TextAlignment = TextAlignment.Center;
                dc.DrawText(ft, new Point(15, (h - ft.Height) / 2));
                if (!lyrics.Busy && player.Current != null)
                {
                    var hint = Theme.Text(this, "Right-click to search manually or load a .lrc file", 11, Theme.LcdDim);
                    hint.MaxTextWidth = Math.Max(40, w - 30); hint.TextAlignment = TextAlignment.Center;
                    dc.DrawText(hint, new Point(15, (h + ft.Height) / 2 + 8));
                }
                return;
            }
            if (cachedFor != ly || Math.Abs(cachedWidth - w) > 0.5 || cachedSize != FontSize || cachedTheme != Theme.Current) BuildCache(ly, w);
            if (cache.Count == 0) return;

            var pos = lyrics.Position;
            int cur = ly.Synced ? ly.IndexAt(pos) : -1;
            double target;
            if (ly.Synced)
                target = cur < 0 ? 0 : tops[cur] + cache[cur].Height / 2;
            else
            {
                var dur = player.Duration;
                double frac = dur.TotalSeconds > 0 ? Util.Clamp(pos.TotalSeconds / dur.TotalSeconds, 0, 1) : 0;
                target = frac * Math.Max(0, totalHeight - h * 0.5) + h * 0.25;
            }
            if ((DateTime.Now - userScrollAt).TotalSeconds > 4) userScroll *= 0.85;
            double k = 1 - Math.Exp(-FrameTicker.Dt * 9);
            scroll += (target - scroll) * k;
            double origin = h / 2 - scroll - userScroll;

            for (int i = 0; i < cache.Count; i++)
            {
                double y = origin + tops[i];
                if (y + cache[i].Height < -20 || y > h + 20) continue;
                double dist = Math.Abs((y + cache[i].Height / 2) - h / 2) / (h / 2);
                double opacity = Util.Clamp(1.15 - dist, 0.18, 1);
                if (i == cur)
                {
                    var ft = cache[i];
                    double scale = 1.12;
                    dc.PushTransform(new ScaleTransform(scale, scale, w / 2, y + ft.Height / 2));
                    ft.SetForegroundBrush(Theme.LcdText);
                    dc.DrawText(ft, new Point(20, y));
                    double prog = ly.Progress(i, pos, player.Duration);
                    // karaoke wipe in the accent colour
                    double textW = Math.Min(ft.WidthIncludingTrailingWhitespace, ft.MaxTextWidth);
                    double left = 20 + (ft.MaxTextWidth - textW) / 2;
                    dc.PushClip(new RectangleGeometry(new Rect(left - 2, y - 4, textW * prog + 2, ft.Height + 8)));
                    ft.SetForegroundBrush(Theme.LcdAmber);
                    dc.DrawText(ft, new Point(20, y));
                    dc.Pop();
                    ft.SetForegroundBrush(Theme.LcdDim);
                    dc.Pop();
                }
                else
                {
                    dc.PushOpacity(opacity);
                    dc.DrawText(cache[i], new Point(20, y));
                    dc.Pop();
                }
            }

            // soft fade at top and bottom
            var fadeTop = new LinearGradientBrush(Theme.LcdBgColor, Color.FromArgb(0, Theme.LcdBgColor.R, Theme.LcdBgColor.G, Theme.LcdBgColor.B), 90);
            dc.DrawRectangle(fadeTop, null, new Rect(0, 0, w, h * 0.12));
            var fadeBottom = new LinearGradientBrush(Color.FromArgb(0, Theme.LcdBgColor.R, Theme.LcdBgColor.G, Theme.LcdBgColor.B), Theme.LcdBgColor, 90);
            dc.DrawRectangle(fadeBottom, null, new Rect(0, h * 0.88, w, h * 0.12 + 1));
        }
    }
}
