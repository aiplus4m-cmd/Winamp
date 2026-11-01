using System;
using System.Windows;
using System.Windows.Media;
using RetroAmp.Core;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    /// <summary>Scrolling song title + info line (kbps / kHz / stereo / source).</summary>
    public sealed class MarqueeDisplay : FrameworkElement
    {
        readonly Player player;
        double offset;
        string lastText;

        public MarqueeDisplay(Player p)
        {
            player = p;
            Loaded += (s, e) => FrameTicker.Tick += Tick;
            Unloaded += (s, e) => FrameTicker.Tick -= Tick;
            ClipToBounds = true;
        }

        void Tick()
        {
            offset += FrameTicker.Dt * 32;
            InvalidateVisual();
        }

        string TitleText()
        {
            var cur = player.Current;
            if (cur == null) return "RetroAmp  ·  drop music, videos or a YouTube / SoundCloud link here";
            string s = cur.Index + ". " + cur.DisplayTitle;
            if (cur.Duration > TimeSpan.Zero) s += " (" + Util.FormatTime(cur.Duration) + ")";
            return s;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            Theme.Bevel(dc, new Rect(0, 0, w, h), Theme.LcdBg, true);

            // line 1: title (scrolling) or status
            string status = player.ActiveStatus;
            bool isStatus = !string.IsNullOrEmpty(status);
            string text = isStatus ? status : TitleText();
            if (text != lastText) { lastText = text; offset = 0; }
            var ft = Theme.Text(this, text, 12, isStatus ? Theme.LcdAmber : Theme.LcdText, false, Theme.LcdFont);
            var clip = new Rect(3, 2, w - 6, 17);
            dc.PushClip(new RectangleGeometry(clip));
            if (ft.Width <= clip.Width || isStatus)
                dc.DrawText(ft, new Point(5, 3));
            else
            {
                double gap = 50, span = ft.Width + gap;
                double x = 5 - ((offset > 40 ? offset - 40 : 0) % span);
                dc.DrawText(ft, new Point(x, 3));
                dc.DrawText(ft, new Point(x + span, 3));
            }
            dc.Pop();

            // line 2: info
            double y = 21;
            var cur = player.Current;
            string kbps = player.InfoKbps > 0 ? player.InfoKbps.ToString() : "---";
            string khz = player.InfoKhz > 0 ? player.InfoKhz.ToString() : "--";
            DrawBox(dc, 5, y, kbps, "kbps", 30);
            DrawBox(dc, 72, y, khz, "kHz", 20);
            bool stereo = player.InfoChannels >= 2 || (player.UsingMedia && player.State == PlayState.Playing);
            bool mono = player.InfoChannels == 1;
            var on = Theme.LcdText; var off = Theme.LcdOff;
            var mf = Theme.Text(this, "mono", 10, mono ? on : off, true);
            var sf = Theme.Text(this, "stereo", 10, stereo ? on : off, true);
            dc.DrawText(mf, new Point(w - sf.Width - mf.Width - 14, y + 1));
            dc.DrawText(sf, new Point(w - sf.Width - 6, y + 1));
            if (cur != null)
            {
                string tag = cur.IsVideo ? "VIDEO" : cur.SourceTag;
                if (!string.IsNullOrEmpty(tag))
                {
                    var tf = Theme.Text(this, tag, 10, Theme.LcdAmber, true);
                    dc.DrawText(tf, new Point(126, y + 1));
                }
            }
        }

        void DrawBox(DrawingContext dc, double x, double y, string val, string unit, double boxW)
        {
            var vf = Theme.Text(this, val, 11, Theme.LcdText, true, Theme.LcdFont);
            dc.DrawRectangle(Theme.LcdOff, null, new Rect(x, y, boxW, 14));
            dc.DrawText(vf, new Point(x + boxW - vf.Width - 2, y));
            var uf = Theme.Text(this, unit, 10, Theme.LcdDim, false);
            dc.DrawText(uf, new Point(x + boxW + 3, y + 1));
        }
    }
}
