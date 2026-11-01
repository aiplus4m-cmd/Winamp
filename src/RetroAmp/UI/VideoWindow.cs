using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    /// <summary>Shows the video of the Media Foundation backend.</summary>
    public sealed class VideoWindow : Window
    {
        readonly Player player;
        readonly VideoDrawing drawing;
        readonly Border host;
        bool fullscreen;
        WindowStyle prevStyle;

        public VideoWindow(Player player)
        {
            this.player = player;
            Title = "RetroAmp Video";
            Width = 854; Height = 510;
            Background = Brushes.Black;
            MinWidth = 200; MinHeight = 120;

            drawing = new VideoDrawing { Player = player.Media.Player, Rect = new Rect(0, 0, 16, 9) };
            var brush = new DrawingBrush(drawing) { Stretch = Stretch.Uniform };
            host = new Border { Background = brush };
            var root = new Grid { Background = Brushes.Black };
            root.Children.Add(host);
            Content = root;

            player.Media.Opened += Resize;
            player.TrackChanged += () => Title = (player.Current?.DisplayTitle ?? "") + " - RetroAmp Video";

            root.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) ToggleFullscreen(); };
            root.MouseWheel += (s, e) =>
            {
                var t = player.Position + TimeSpan.FromSeconds(e.Delta > 0 ? 10 : -10);
                player.Seek(t < TimeSpan.Zero ? TimeSpan.Zero : t);
            };
            KeyDown += OnKey;
            Closing += (s, e) =>
            {
                if (VisualizerWindow.AppClosing) return;
                e.Cancel = true;
                if (fullscreen) ToggleFullscreen();
                Hide();
                if (player.UsingMedia && player.Media.HasVideo) player.Stop();
            };
        }

        void Resize()
        {
            var p = player.Media.Player;
            if (p.NaturalVideoWidth > 0 && p.NaturalVideoHeight > 0)
                drawing.Rect = new Rect(0, 0, p.NaturalVideoWidth, p.NaturalVideoHeight);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space: player.TogglePlayPause(); break;
                case Key.Escape: if (fullscreen) ToggleFullscreen(); break;
                case Key.F: case Key.Enter: case Key.F11: ToggleFullscreen(); break;
                case Key.Right: player.Seek(player.Position + TimeSpan.FromSeconds(5)); break;
                case Key.Left: { var t = player.Position - TimeSpan.FromSeconds(5); player.Seek(t < TimeSpan.Zero ? TimeSpan.Zero : t); break; }
                case Key.B: player.Next(); break;
                case Key.Z: player.Previous(); break;
                default: return;
            }
            e.Handled = true;
        }

        public void ToggleFullscreen()
        {
            fullscreen = !fullscreen;
            if (fullscreen)
            {
                prevStyle = WindowStyle;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
                Topmost = true;
            }
            else
            {
                WindowStyle = prevStyle;
                WindowState = WindowState.Normal;
                Topmost = false;
            }
        }
    }
}
