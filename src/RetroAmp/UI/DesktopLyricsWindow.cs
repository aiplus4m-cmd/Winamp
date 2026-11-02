using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetroAmp.Core;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    /// <summary>Transparent, always-on-top karaoke line floating on the desktop.</summary>
    public sealed class DesktopLyricsWindow : Window
    {
        readonly Player player;
        readonly LyricsController lyrics;
        readonly Settings s;
        readonly Surface surface;
        bool hover;

        public DesktopLyricsWindow(Player p, LyricsController l, Settings settings)
        {
            player = p; lyrics = l; s = settings;
            Title = "RetroAmp Desktop Lyrics";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Width = Math.Min(1100, SystemParameters.WorkArea.Width * 0.8);
            Height = s.DesktopLyricsSize * 3.6;
            if (!double.IsNaN(s.DesktopLyricsLeft) && !double.IsNaN(s.DesktopLyricsTop))
            {
                Left = Util.Clamp(s.DesktopLyricsLeft, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 200);
                Top = Util.Clamp(s.DesktopLyricsTop, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60);
            }
            else
            {
                Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
                Top = SystemParameters.WorkArea.Bottom - Height - 40;
            }
            surface = new Surface(this);
            Content = surface;
            MouseEnter += (o, e) => hover = true;
            MouseLeave += (o, e) => hover = false;
            MouseLeftButtonDown += (o, e) => { try { DragMove(); } catch { } s.DesktopLyricsLeft = Left; s.DesktopLyricsTop = Top; };
            MouseRightButtonUp += (o, e) => Menu();
            MouseWheel += (o, e) => SetSize(s.DesktopLyricsSize + Math.Sign(e.Delta) * 2);
        }

        void SetSize(double size)
        {
            s.DesktopLyricsSize = Util.Clamp(size, 16, 72);
            Height = s.DesktopLyricsSize * 3.6;
        }

        void Menu()
        {
            var m = new ContextMenu();
            var big = new MenuItem { Header = "Bigger (mouse wheel)" }; big.Click += (o, e) => SetSize(s.DesktopLyricsSize + 4); m.Items.Add(big);
            var small = new MenuItem { Header = "Smaller" }; small.Click += (o, e) => SetSize(s.DesktopLyricsSize - 4); m.Items.Add(small);
            var later = new MenuItem { Header = "Lyrics later (+0.5 s)" }; later.Click += (o, e) => lyrics.AdjustOffset(500); m.Items.Add(later);
            var earlier = new MenuItem { Header = "Lyrics earlier (−0.5 s)" }; earlier.Click += (o, e) => lyrics.AdjustOffset(-500); m.Items.Add(earlier);
            m.Items.Add(new Separator());
            var close = new MenuItem { Header = "Hide desktop lyrics" }; close.Click += (o, e) => { s.DesktopLyrics = false; Hide(); }; m.Items.Add(close);
            m.IsOpen = true;
        }

        sealed class Surface : FrameworkElement
        {
            readonly DesktopLyricsWindow w;
            public Surface(DesktopLyricsWindow w)
            {
                this.w = w;
                Loaded += (o, e) => FrameTicker.Tick += InvalidateVisual;
                Unloaded += (o, e) => FrameTicker.Tick -= InvalidateVisual;
            }

            protected override void OnRender(DrawingContext dc)
            {
                double W = ActualWidth, H = ActualHeight;
                // almost invisible background so the whole window can be dragged
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb((byte)(w.hover ? 110 : 1), 0, 0, 0)), null, new Rect(0, 0, W, H), 12, 12);
                var ly = w.lyrics.Current;
                double size = w.s.DesktopLyricsSize;
                string line1, line2 = null;
                double prog = 0;
                if (ly == null || !ly.Synced)
                {
                    if (!w.hover && (w.player.Current == null || ly == null)) return;
                    line1 = w.player.Current?.DisplayTitle ?? "RetroAmp";
                    if (ly == null) line2 = w.lyrics.Busy ? "Searching lyrics…" : w.lyrics.Status;
                }
                else
                {
                    var pos = w.lyrics.Position;
                    int i = ly.IndexAt(pos);
                    if (i < 0) { line1 = w.player.Current?.DisplayTitle ?? ""; line2 = ly.Lines.Count > 0 ? ly.Lines[0].Text : null; }
                    else
                    {
                        line1 = string.IsNullOrWhiteSpace(ly.Lines[i].Text) ? "♪ ♪ ♪" : ly.Lines[i].Text;
                        prog = ly.Progress(i, pos, w.player.Duration);
                        line2 = i + 1 < ly.Lines.Count ? ly.Lines[i + 1].Text : null;
                    }
                }
                DrawOutlined(dc, line1, size, W, H * 0.06, prog, true);
                if (!string.IsNullOrWhiteSpace(line2)) DrawOutlined(dc, line2, size * 0.72, W, H * 0.06 + size * 1.55, 0, false);
            }

            void DrawOutlined(DrawingContext dc, string text, double size, double W, double y, double prog, bool main)
            {
                var ft = Theme.Text(this, text, size, Brushes.White, true);
                ft.MaxTextWidth = Math.Max(50, W - 30);
                ft.MaxLineCount = 1;
                ft.Trimming = TextTrimming.CharacterEllipsis;
                double x = Math.Max(15, (W - ft.Width) / 2);
                var geo = ft.BuildGeometry(new Point(x, y));
                var outline = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)), Math.Max(2, size / 9)) { LineJoin = PenLineJoin.Round };
                dc.DrawGeometry(null, outline, geo);
                var baseFill = main
                    ? new LinearGradientBrush(Colors.White, Theme.Mix(Theme.LcdTextColor, Colors.White, 0.35), 90)
                    : (Brush)new SolidColorBrush(Color.FromArgb(220, 230, 230, 240));
                dc.DrawGeometry(baseFill, null, geo);
                if (main && prog > 0)
                {
                    var acc = Theme.AccentColor;
                    dc.PushClip(new RectangleGeometry(new Rect(x - 4, y - 4, ft.Width * prog + 4, ft.Height + 8)));
                    dc.DrawGeometry(new LinearGradientBrush(Theme.Lighten(acc, 0.35), acc, 90), null, geo);
                    dc.Pop();
                }
            }
        }
    }
}
