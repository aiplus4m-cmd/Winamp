using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RetroAmp.Core;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    public sealed class VisualizerWindow : Window
    {
        readonly VisualizerView view = new VisualizerView();
        readonly TitleBar title;
        readonly Settings settings;
        readonly Player player;
        bool fullscreen;
        WindowState prevState;
        public static bool AppClosing;

        public VisualizerWindow(Player player, Settings settings)
        {
            this.player = player;
            this.settings = settings;
            Title = "RetroAmp Visualizer";
            Width = 760; Height = 440;
            MinWidth = 240; MinHeight = 160;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Background = Theme.WindowBg;
            UseLayoutRounding = true;

            view.Mode = (VisMode)Util.Clamp(settings.VisMode, 0, VisualizerView.ModeNames.Length - 1);
            title = new TitleBar("VISUALIZER");
            title.AddButton(GlyphKind.Prev, "Previous style", () => Cycle(-1));
            title.AddButton(GlyphKind.Next, "Next style", () => Cycle(1));
            title.AddButton(GlyphKind.Shade, "Full screen (double-click)", ToggleFullscreen);
            title.AddButton(GlyphKind.Close, "Close", Close);
            title.DoubleClicked += ToggleFullscreen;

            var dock = new DockPanel();
            DockPanel.SetDock(title, Dock.Top);
            dock.Children.Add(title);
            dock.Children.Add(new Border { BorderBrush = Theme.BevelDark, BorderThickness = new Thickness(1), Child = view });
            Content = dock;

            view.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) ToggleFullscreen();
                else if (!fullscreen) { try { DragMove(); } catch { } }
            };
            view.MouseRightButtonUp += (s, e) => ShowModeMenu();
            KeyDown += OnKey;
            player.TrackChanged += UpdateCaption;
            UpdateCaption();
            Loaded += (s, e) => view.ShowModeName();
            Closing += (s, e) =>
            {
                if (AppClosing) return;
                e.Cancel = true;
                if (fullscreen) ToggleFullscreen();
                Hide();
                settings.ShowVisWindow = false;
            };
        }

        void UpdateCaption()
        {
            view.Caption = player.Current?.DisplayTitle;
        }

        void ShowModeMenu()
        {
            var menu = new ContextMenu();
            for (int i = 0; i < VisualizerView.ModeNames.Length; i++)
            {
                int k = i;
                var mi = new MenuItem { Header = VisualizerView.ModeNames[i], IsCheckable = true, IsChecked = (int)view.Mode == i };
                mi.Click += (s, e) => SetMode(k);
                menu.Items.Add(mi);
            }
            menu.Items.Add(new Separator());
            var fs = new MenuItem { Header = fullscreen ? "Exit full screen" : "Full screen", InputGestureText = "F" };
            fs.Click += (s, e) => ToggleFullscreen();
            menu.Items.Add(fs);
            menu.IsOpen = true;
        }

        void Cycle(int d)
        {
            int n = VisualizerView.ModeNames.Length;
            SetMode((((int)view.Mode + d) % n + n) % n);
        }

        void SetMode(int m)
        {
            view.Mode = (VisMode)m;
            settings.VisMode = m;
            view.ShowModeName();
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Right: case Key.Down: Cycle(1); break;
                case Key.Left: case Key.Up: Cycle(-1); break;
                case Key.F: case Key.Enter: case Key.F11: ToggleFullscreen(); break;
                case Key.Escape: if (fullscreen) ToggleFullscreen(); else Close(); break;
                case Key.Space: player.TogglePlayPause(); break;
                case Key.B: player.Next(); break;
                case Key.Z: player.Previous(); break;
                default: return;
            }
            e.Handled = true;
        }

        void ToggleFullscreen()
        {
            fullscreen = !fullscreen;
            if (fullscreen)
            {
                prevState = WindowState;
                title.Visibility = Visibility.Collapsed;
                ResizeMode = ResizeMode.NoResize;
                Topmost = true;
                WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
                Cursor = Cursors.None;
            }
            else
            {
                title.Visibility = Visibility.Visible;
                ResizeMode = ResizeMode.CanResizeWithGrip;
                Topmost = false;
                WindowState = prevState == WindowState.Maximized ? WindowState.Normal : prevState;
                Cursor = null;
            }
        }
    }
}
