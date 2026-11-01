using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using RetroAmp.Audio;
using RetroAmp.Core;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    public sealed class MainWindow : Window
    {
        const double W = 460;
        readonly Player player;
        readonly Settings s;
        readonly ScaleTransform scale;
        readonly StackPanel stack = new StackPanel();

        LcdDisplay lcd;
        MarqueeDisplay marquee;
        RetroSlider seek, vol, bal;
        RetroButton btnShuffle, btnRepeat, btnEq, btnPl, btnVis;
        FrameworkElement mainPanel, eqPanel, plPanel;

        RetroButton eqOn, vocal;
        RetroSlider preamp, width, reverb, xfade;
        readonly RetroSlider[] bands = new RetroSlider[10];
        EqCurve curve;

        ListBox list;
        TextBox search;
        TextBlock plInfo, searchHint;
        ICollectionView view;

        VisualizerWindow visWin;
        VideoWindow videoWin;
        readonly DispatcherTimer timer;
        ThumbButtonInfo thumbPlay;
        ImageSource imgPlay, imgPause;
        Task<bool> ytDlpInstall;

        public MainWindow(Player player, Settings settings)
        {
            this.player = player;
            s = settings;
            Title = "RetroAmp";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanMinimize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Background = Theme.WindowBg;
            AllowDrop = true;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Topmost = s.AlwaysOnTop;
            FontFamily = Theme.UiFont;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Icon = LoadIcon();

            scale = new ScaleTransform(s.Scale, s.Scale);
            stack.LayoutTransform = scale;
            Content = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Child = stack };

            mainPanel = BuildMain();
            eqPanel = BuildEq();
            plPanel = BuildPlaylist();
            stack.Children.Add(mainPanel);
            stack.Children.Add(eqPanel);
            stack.Children.Add(plPanel);
            ApplyPanelVisibility();

            player.EnsureYtDlp = EnsureYtDlpInteractive;
            player.TrackChanged += OnTrackChanged;
            player.StateChanged += OnStateChanged;
            player.VideoStarted += ShowVideo;
            player.Items.CollectionChanged += (o, e) => UpdatePlInfo();

            timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (o, e) => OnTimer();
            timer.Start();

            if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Util.Clamp(s.WindowLeft, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 120);
                Top = Util.Clamp(s.WindowTop, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60);
            }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            PreviewKeyDown += OnKey;
            Drop += OnDrop;
            DragOver += (o, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            SourceInitialized += (o, e) => HookWindow();
            Closing += OnClosing;
            Loaded += (o, e) =>
            {
                SetupTaskbar();
                if (s.ShowVisWindow) ToggleVis();
                OnTrackChanged();
            };
        }

        static ImageSource LoadIcon()
        {
            try
            {
                var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("RetroAmp.icon.ico");
                if (st == null) return null;
                var dec = new IconBitmapDecoder(st, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                return dec.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault(f => f.PixelWidth <= 64) ?? dec.Frames[0];
            }
            catch { return null; }
        }

        // =====================================================================================
        // Main panel
        // =====================================================================================
        FrameworkElement BuildMain()
        {
            var c = new Canvas { Width = W, Height = 152, Background = Brushes.Transparent };
            c.Children.Add(new PanelFace { Width = W, Height = 152 });
            var tb = new TitleBar("R E T R O A M P") { Width = W };
            tb.AddButton(GlyphKind.Menu, "Main menu", ShowMainMenu);
            tb.AddButton(GlyphKind.Minimize, "Minimize", () => WindowState = WindowState.Minimized);
            tb.AddButton(GlyphKind.Close, "Close", Close);
            c.Children.Add(tb.At(0, 0));

            lcd = new LcdDisplay(player) { Width = 170, Height = 74, VisMode = s.MiniVisMode, ShowRemaining = s.ShowRemaining };
            lcd.VisModeChanged += () => s.MiniVisMode = lcd.VisMode;
            lcd.TimeModeChanged += () => s.ShowRemaining = lcd.ShowRemaining;
            c.Children.Add(lcd.At(8, 24));

            marquee = new MarqueeDisplay(player) { Width = 266, Height = 40 };
            c.Children.Add(marquee.At(186, 24));

            vol = new RetroSlider { Kind = SliderStyle.Volume, Width = 150, Height = 14, Value = s.Volume, DefaultValue = 0.8 };
            vol.TipFormatter = v => "Volume " + (int)(v * 100) + "%";
            vol.ToolTip = vol.TipFormatter(s.Volume);
            vol.ValueChanged += v => { s.Volume = v; player.ApplyVolume(); player.ShowStatus("Volume: " + (int)(v * 100) + "%", 1.2); };
            c.Children.Add(vol.At(186, 69));
            bal = new RetroSlider { Kind = SliderStyle.Balance, Width = 108, Height = 14, Value = (s.Balance + 1) / 2, DefaultValue = 0.5 };
            bal.TipFormatter = v => BalanceText(v * 2 - 1);
            bal.ValueChanged += v => { s.Balance = v * 2 - 1; player.ApplyVolume(); player.ShowStatus(BalanceText(s.Balance), 1.2); };
            c.Children.Add(bal.At(344, 69));
            c.Children.Add(Ui.Label("VOLUME", 8).At(186, 84));
            c.Children.Add(Ui.Label("BALANCE", 8).At(344, 84));

            seek = new RetroSlider { Kind = SliderStyle.Seek, Width = W - 16, Height = 14, WheelStep = 0.02 };
            seek.ValueChanged += v =>
            {
                var d = player.Duration;
                if (d > TimeSpan.Zero) player.ShowStatus("Seek to " + Util.FormatTime(TimeSpan.FromSeconds(v * d.TotalSeconds)) + " / " + Util.FormatTime(d), 1.5);
            };
            seek.DragCompleted += v =>
            {
                var d = player.Duration;
                if (d > TimeSpan.Zero) player.Seek(TimeSpan.FromSeconds(v * d.TotalSeconds));
            };
            c.Children.Add(seek.At(8, 104));

            double y = 124, bh = 22;
            c.Children.Add(RetroButton.Make(GlyphKind.Prev, null, "Previous (Z)", 32, bh, player.Previous).At(8, y));
            c.Children.Add(RetroButton.Make(GlyphKind.Play, null, "Play (X)", 32, bh, PlayButton).At(40, y));
            c.Children.Add(RetroButton.Make(GlyphKind.Pause, null, "Pause (C)", 32, bh, () => { if (player.State == PlayState.Paused) player.Resume(); else player.Pause(); }).At(72, y));
            c.Children.Add(RetroButton.Make(GlyphKind.Stop, null, "Stop (V)", 32, bh, player.Stop).At(104, y));
            c.Children.Add(RetroButton.Make(GlyphKind.Next, null, "Next (B)", 32, bh, player.Next).At(136, y));
            c.Children.Add(RetroButton.Make(GlyphKind.Eject, null, "Open files (L)", 28, bh, OpenFiles).At(174, y));

            btnShuffle = RetroButton.Make(GlyphKind.None, "SHUFFLE", "Shuffle (S)", 62, bh, ToggleShuffle, true);
            btnShuffle.IsChecked = s.Shuffle;
            c.Children.Add(btnShuffle.At(208, y));
            btnRepeat = RetroButton.Make(GlyphKind.None, "REPEAT", "Repeat: off / all / one (R)", 62, bh, CycleRepeat, true);
            c.Children.Add(btnRepeat.At(272, y));
            UpdateRepeatButton();

            btnEq = RetroButton.Make(GlyphKind.None, "EQ", "Equalizer & effects (E)", 38, bh, () => { s.ShowEq = !s.ShowEq; ApplyPanelVisibility(); }, true);
            btnPl = RetroButton.Make(GlyphKind.None, "PL", "Playlist (P)", 38, bh, () => { s.ShowPlaylist = !s.ShowPlaylist; ApplyPanelVisibility(); }, true);
            btnVis = RetroButton.Make(GlyphKind.None, "VIS", "Visualizer window (K)", 42, bh, ToggleVis, true);
            c.Children.Add(btnEq.At(338, y));
            c.Children.Add(btnPl.At(372 + 2, y));
            c.Children.Add(btnVis.At(410, y));
            btnEq.Width = 34; btnPl.Width = 34; btnVis.Width = 42;

            c.MouseLeftButtonDown += (o, e) => { if (e.OriginalSource is PanelFace) { try { DragMove(); } catch { } } };
            c.MouseRightButtonUp += (o, e) => { if (e.OriginalSource is PanelFace || e.OriginalSource is MarqueeDisplay) ShowMainMenu(); };
            return c;
        }

        static string BalanceText(double b)
        {
            if (Math.Abs(b) < 0.01) return "Balance: center";
            return "Balance: " + (int)(Math.Abs(b) * 100) + "% " + (b < 0 ? "left" : "right");
        }

        void PlayButton()
        {
            if (player.State == PlayState.Playing && player.Position.TotalSeconds > 0.5) { player.Seek(TimeSpan.Zero); return; }
            var sel = list?.SelectedItem as PlaylistItem;
            if (player.State == PlayState.Stopped && sel != null) { player.Play(sel); return; }
            player.PlayCurrentOrFirst();
        }

        void ToggleShuffle()
        {
            s.Shuffle = !s.Shuffle;
            btnShuffle.IsChecked = s.Shuffle;
            player.ShowStatus("Shuffle " + (s.Shuffle ? "on" : "off"), 1.5);
        }

        void CycleRepeat()
        {
            s.Repeat = (s.Repeat + 1) % 3;
            UpdateRepeatButton();
            player.ShowStatus(new[] { "Repeat off", "Repeat all", "Repeat one track" }[s.Repeat], 1.5);
        }

        void UpdateRepeatButton()
        {
            btnRepeat.IsChecked = s.Repeat != 0;
            btnRepeat.Text = s.Repeat == 2 ? "REPEAT 1" : "REPEAT";
        }

        void ApplyPanelVisibility()
        {
            eqPanel.Visibility = s.ShowEq ? Visibility.Visible : Visibility.Collapsed;
            plPanel.Visibility = s.ShowPlaylist ? Visibility.Visible : Visibility.Collapsed;
            btnEq.IsChecked = s.ShowEq;
            btnPl.IsChecked = s.ShowPlaylist;
        }

        // =====================================================================================
        // Equalizer / DSP panel
        // =====================================================================================
        FrameworkElement BuildEq()
        {
            double H = 176;
            var c = new Canvas { Width = W, Height = H, Background = Brushes.Transparent };
            c.Children.Add(new PanelFace { Width = W, Height = H });
            var tb = new TitleBar("EQUALIZER  ·  EFFECTS") { Width = W };
            tb.AddButton(GlyphKind.Close, "Hide equalizer", () => { s.ShowEq = false; ApplyPanelVisibility(); });
            c.Children.Add(tb.At(0, 0));

            eqOn = RetroButton.Make(GlyphKind.None, "ON", "Enable equalizer", 44, 18, () =>
            {
                s.EqEnabled = !s.EqEnabled; eqOn.IsChecked = s.EqEnabled; player.ApplyDsp(); curve.InvalidateVisual();
            }, true);
            eqOn.IsChecked = s.EqEnabled;
            c.Children.Add(eqOn.At(8, 24));
            var presetsBtn = RetroButton.Make(GlyphKind.None, "PRESETS", "Equalizer presets", 66, 18, null);
            presetsBtn.Click += () => ShowPresetMenu(presetsBtn);
            c.Children.Add(presetsBtn.At(56, 24));
            c.Children.Add(RetroButton.Make(GlyphKind.None, "RESET", "Flat EQ, reset effects", 52, 18, ResetEq).At(126, 24));
            curve = new EqCurve(s) { Width = W - 194, Height = 20 };
            c.Children.Add(curve.At(186, 23));

            double top = 52, sh = 76;
            preamp = new RetroSlider { Kind = SliderStyle.EqBand, Orientation = Orientation.Vertical, Width = 14, Height = sh, Value = 0.5 + s.Preamp / 24.0, DefaultValue = 0.5 };
            preamp.TipFormatter = v => "Preamp " + Db(v);
            preamp.ToolTip = "Preamp";
            preamp.ValueChanged += v => { s.Preamp = (float)((v - 0.5) * 24); player.ApplyDsp(); curve.InvalidateVisual(); player.ShowStatus("Preamp: " + Db(v), 1.2); };
            c.Children.Add(preamp.At(16, top));
            c.Children.Add(Ui.Label("PRE", 8).At(13, top + sh + 3));
            c.Children.Add(Ui.Label("+12", 8, Theme.DimText).At(38, top - 2));
            c.Children.Add(Ui.Label("0", 8, Theme.DimText).At(44, top + sh / 2 - 6));
            c.Children.Add(Ui.Label("-12", 8, Theme.DimText).At(40, top + sh - 11));

            for (int i = 0; i < 10; i++)
            {
                int k = i;
                double x = 70 + i * 38;
                var sl = new RetroSlider { Kind = SliderStyle.EqBand, Orientation = Orientation.Vertical, Width = 14, Height = sh, Value = 0.5 + s.EqBands[i] / 24.0, DefaultValue = 0.5 };
                sl.TipFormatter = v => EqPresets.Labels[k] + " Hz  " + Db(v);
                sl.ToolTip = EqPresets.Labels[k] + " Hz";
                sl.ValueChanged += v =>
                {
                    s.EqBands[k] = (float)((v - 0.5) * 24);
                    s.EqPresetName = "Custom";
                    player.ApplyDsp(); curve.InvalidateVisual();
                    player.ShowStatus("EQ " + EqPresets.Labels[k] + "Hz: " + Db(v), 1.2);
                };
                bands[i] = sl;
                c.Children.Add(sl.At(x, top));
                var lb = Ui.Label(EqPresets.Labels[i], 8);
                lb.Width = 30; lb.TextAlignment = TextAlignment.Center;
                c.Children.Add(lb.At(x - 8, top + sh + 3));
            }

            double y = 150;
            c.Children.Add(Ui.Label("WIDTH", 8).At(8, y + 2));
            width = new RetroSlider { Kind = SliderStyle.Plain, Width = 74, Height = 14, Value = s.StereoWidth / 2, DefaultValue = 0.5 };
            width.TipFormatter = v => "Stereo width " + (int)(v * 200) + "%";
            width.ToolTip = width.TipFormatter(width.Value);
            width.ValueChanged += v => { s.StereoWidth = v * 2; player.ApplyDsp(); player.ShowStatus("Stereo width: " + (int)(v * 200) + "%", 1.2); };
            c.Children.Add(width.At(42, y));

            c.Children.Add(Ui.Label("REVERB", 8).At(124, y + 2));
            reverb = new RetroSlider { Kind = SliderStyle.Plain, Width = 66, Height = 14, Value = s.Reverb, DefaultValue = 0 };
            reverb.TipFormatter = v => "Reverb " + (int)(v * 100) + "%";
            reverb.ToolTip = reverb.TipFormatter(reverb.Value);
            reverb.ValueChanged += v => { s.Reverb = v; player.ApplyDsp(); player.ShowStatus("Reverb: " + (int)(v * 100) + "%", 1.2); };
            c.Children.Add(reverb.At(164, y));

            c.Children.Add(Ui.Label("X-FADE", 8).At(238, y + 2));
            xfade = new RetroSlider { Kind = SliderStyle.Plain, Width = 66, Height = 14, Value = s.CrossfadeSeconds / 10, DefaultValue = 0 };
            xfade.TipFormatter = v => v < 0.01 ? "Crossfade off" : "Crossfade " + (v * 10).ToString("0.0") + " s";
            xfade.ToolTip = xfade.TipFormatter(xfade.Value);
            xfade.ValueChanged += v => { s.CrossfadeSeconds = Math.Round(v * 10, 1); player.ShowStatus(xfade.TipFormatter(v), 1.2); };
            c.Children.Add(xfade.At(278, y));

            vocal = RetroButton.Make(GlyphKind.None, "KARAOKE", "Vocal remover (removes centre-panned vocals)", 98, 18, () =>
            {
                s.VocalRemover = !s.VocalRemover; vocal.IsChecked = s.VocalRemover; player.ApplyDsp();
                player.ShowStatus("Karaoke (vocal remover) " + (s.VocalRemover ? "on" : "off"), 1.5);
            }, true);
            vocal.IsChecked = s.VocalRemover;
            c.Children.Add(vocal.At(354, y - 2));

            c.MouseLeftButtonDown += (o, e) => { if (e.OriginalSource is PanelFace) { try { DragMove(); } catch { } } };
            return c;
        }

        static string Db(double v)
        {
            double db = (v - 0.5) * 24;
            return (db >= 0 ? "+" : "") + db.ToString("0.0") + " dB";
        }

        void SyncEqSliders()
        {
            preamp.Value = 0.5 + s.Preamp / 24.0;
            for (int i = 0; i < 10; i++) bands[i].Value = 0.5 + s.EqBands[i] / 24.0;
            eqOn.IsChecked = s.EqEnabled;
            curve.InvalidateVisual();
        }

        void ApplyPreset(string name, float[] values, float pre)
        {
            for (int i = 0; i < 10; i++) s.EqBands[i] = values[i];
            s.Preamp = pre;
            s.EqPresetName = name;
            s.EqEnabled = true;
            SyncEqSliders();
            player.ApplyDsp();
            player.ShowStatus("EQ preset: " + name, 2);
        }

        void ResetEq()
        {
            ApplyPreset("Flat", new float[10], 0);
            s.StereoWidth = 1; width.Value = 0.5;
            s.Reverb = 0; reverb.Value = 0;
            s.VocalRemover = false; vocal.IsChecked = false;
            player.ApplyDsp();
        }

        void ShowPresetMenu(FrameworkElement anchor)
        {
            var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
            foreach (var kv in EqPresets.BuiltIn)
            {
                var p = kv;
                var mi = new MenuItem { Header = p.Key, IsCheckable = true, IsChecked = s.EqPresetName == p.Key };
                mi.Click += (o, e) => ApplyPreset(p.Key, p.Value, p.Key == "Flat" ? 0 : -2);
                menu.Items.Add(mi);
            }
            if (s.CustomPresets.Count > 0)
            {
                menu.Items.Add(new Separator());
                foreach (var cp in s.CustomPresets.ToList())
                {
                    var p = cp;
                    var mi = new MenuItem { Header = "★ " + p.Name, IsCheckable = true, IsChecked = s.EqPresetName == p.Name };
                    mi.Click += (o, e) => ApplyPreset(p.Name, p.Bands, p.Preamp);
                    menu.Items.Add(mi);
                }
            }
            menu.Items.Add(new Separator());
            var save = new MenuItem { Header = "Save current as preset…" };
            save.Click += (o, e) =>
            {
                var dlg = new InputDialog("Save preset", "Preset name:", s.EqPresetName == "Custom" ? "My preset" : s.EqPresetName) { Owner = this };
                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Value))
                {
                    s.CustomPresets.RemoveAll(x => x.Name == dlg.Value.Trim());
                    s.CustomPresets.Add(new EqPreset { Name = dlg.Value.Trim(), Preamp = s.Preamp, Bands = (float[])s.EqBands.Clone() });
                    s.EqPresetName = dlg.Value.Trim();
                    s.Save();
                }
            };
            menu.Items.Add(save);
            if (s.CustomPresets.Count > 0)
            {
                var del = new MenuItem { Header = "Delete custom preset" };
                foreach (var cp in s.CustomPresets.ToList())
                {
                    var p = cp;
                    var mi = new MenuItem { Header = p.Name };
                    mi.Click += (o, e) => { s.CustomPresets.Remove(p); s.Save(); };
                    del.Items.Add(mi);
                }
                menu.Items.Add(del);
            }
            menu.IsOpen = true;
        }

        // =====================================================================================
        // Playlist panel
        // =====================================================================================
        FrameworkElement BuildPlaylist()
        {
            var g = new Grid { Width = W, Height = Util.Clamp(s.PlaylistHeight, 120, 1400) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            var face = new PanelFace();
            Grid.SetRowSpan(face, 4);
            g.Children.Add(face);

            var tb = new TitleBar("PLAYLIST");
            tb.AddButton(GlyphKind.Close, "Hide playlist", () => { s.ShowPlaylist = false; ApplyPanelVisibility(); });
            g.Children.Add(tb);

            var sg = new Grid { Margin = new Thickness(6, 4, 6, 2) };
            search = DialogStyle.TextBox();
            search.FontSize = 11; search.Padding = new Thickness(3, 1, 3, 1);
            searchHint = new TextBlock { Text = "  Search playlist…  (Ctrl+F)", Foreground = Theme.DimText, FontSize = 11, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
            search.TextChanged += (o, e) =>
            {
                searchHint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                view?.Refresh();
            };
            search.KeyDown += (o, e) => { if (e.Key == Key.Escape) { search.Text = ""; list.Focus(); } else if (e.Key == Key.Enter && list.Items.Count > 0) player.Play(list.Items[0] as PlaylistItem); };
            sg.Children.Add(search);
            sg.Children.Add(searchHint);
            Grid.SetRow(sg, 1);
            g.Children.Add(sg);

            list = new ListBox
            {
                Margin = new Thickness(6, 2, 6, 2),
                Background = Theme.PlaylistBg,
                BorderBrush = Theme.BevelDark,
                Foreground = Theme.PlaylistText,
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI, Tahoma"),
                SelectionMode = SelectionMode.Extended,
                ItemsSource = player.Items,
                ItemTemplate = Xaml.Parse<DataTemplate>(Xaml.PlaylistItemTemplate),
                ItemContainerStyle = Xaml.Parse<Style>(Xaml.ListItemStyle),
                AllowDrop = true
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            VirtualizingPanel.SetIsVirtualizing(list, true);
            VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
            list.Resources.Add(typeof(ScrollBar), Xaml.Parse<Style>(Xaml.ScrollBarStyle));
            list.MouseDoubleClick += (o, e) =>
            {
                var item = ItemFromSource(e.OriginalSource as DependencyObject);
                if (item != null) player.Play(item);
            };
            list.KeyDown += (o, e) =>
            {
                if (e.Key == Key.Enter && list.SelectedItem is PlaylistItem it) { player.Play(it); e.Handled = true; }
                else if (e.Key == Key.Delete) { RemoveSelected(); e.Handled = true; }
            };
            list.ContextMenuOpening += (o, e) => { list.ContextMenu = BuildListMenu(); };
            list.ContextMenu = new ContextMenu();
            Grid.SetRow(list, 2);
            g.Children.Add(list);

            view = CollectionViewSource.GetDefaultView(player.Items);
            view.Filter = o =>
            {
                string q = search?.Text?.Trim();
                if (string.IsNullOrEmpty(q)) return true;
                var it = (PlaylistItem)o;
                return it.DisplayTitle.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                       it.Location.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
            };

            var bar = new Grid { Margin = new Thickness(6, 3, 6, 5) };
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            RetroButton add = null, rem = null, misc = null, lst = null;
            add = RetroButton.Make(GlyphKind.None, "+ ADD", "Add files, folders, links", 54, 20, () => ShowAddMenu(add));
            rem = RetroButton.Make(GlyphKind.None, "− REM", "Remove items", 54, 20, () => ShowRemoveMenu(rem));
            misc = RetroButton.Make(GlyphKind.None, "SORT", "Sort / shuffle the list", 46, 20, () => ShowSortMenu(misc));
            lst = RetroButton.Make(GlyphKind.None, "LIST", "Load / save playlists", 44, 20, () => ShowListMenu(lst));
            var yt = RetroButton.Make(GlyphKind.None, "▶ YT / SC", "Add a YouTube / SoundCloud link or search (Ctrl+U)", 76, 20, () => OpenUrlDialog());
            foreach (var b in new[] { add, rem, misc, lst, yt }) { b.Margin = new Thickness(0, 0, 3, 0); btns.Children.Add(b); }
            bar.Children.Add(btns);
            plInfo = new TextBlock { Foreground = Theme.LcdText, FontFamily = Theme.LcdFont, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            bar.Children.Add(plInfo);
            var grip = new ResizeGrip(this, g);
            grip.HorizontalAlignment = HorizontalAlignment.Right;
            grip.VerticalAlignment = VerticalAlignment.Bottom;
            grip.Margin = new Thickness(0, 0, -5, -4);
            bar.Children.Add(grip);
            Grid.SetRow(bar, 3);
            g.Children.Add(bar);
            UpdatePlInfo();
            return g;
        }

        sealed class ResizeGrip : FrameworkElement
        {
            readonly MainWindow owner; readonly FrameworkElement target;
            Point start; double startH; bool drag;
            public ResizeGrip(MainWindow o, FrameworkElement t) { owner = o; target = t; Width = 12; Height = 12; Cursor = Cursors.SizeNS; ToolTip = "Drag to resize the playlist"; }
            protected override void OnRender(DrawingContext dc)
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 12, 12));
                var p = Theme.P(Theme.BevelLight, 1);
                for (int i = 0; i < 3; i++) dc.DrawLine(p, new Point(11, 3 + i * 3), new Point(3 + i * 3, 11));
            }
            protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { drag = true; start = PointToScreen(e.GetPosition(this)); startH = target.Height; CaptureMouse(); e.Handled = true; }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (!drag) return;
                var now = PointToScreen(e.GetPosition(this));
                double dpi = 1;
                try { dpi = VisualTreeHelper.GetDpi(this).DpiScaleY; } catch { }
                double h = startH + (now.Y - start.Y) / dpi / owner.scale.ScaleY;
                target.Height = Util.Clamp(h, 120, 1400);
                owner.s.PlaylistHeight = target.Height;
            }
            protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { drag = false; ReleaseMouseCapture(); }
        }

        static PlaylistItem ItemFromSource(DependencyObject d)
        {
            while (d != null && !(d is ListBoxItem)) d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            return (d as ListBoxItem)?.DataContext as PlaylistItem;
        }

        List<PlaylistItem> Selected() => list.SelectedItems.Cast<PlaylistItem>().OrderBy(i => player.Items.IndexOf(i)).ToList();

        ContextMenu BuildListMenu()
        {
            var menu = new ContextMenu();
            var sel = Selected();
            var first = sel.FirstOrDefault();
            if (first != null)
            {
                menu.Items.Add(MI("Play", () => player.Play(first), "Enter"));
                if (first.IsOnline && !first.IsVideo)
                    menu.Items.Add(MI("Play as video", () => { first.PlayAsVideo = true; player.Play(first); }));
                if (first.PlayAsVideo)
                    menu.Items.Add(MI("Play as audio only", () => { first.PlayAsVideo = false; player.Play(first); }));
                menu.Items.Add(new Separator());
                menu.Items.Add(MI("Move up", () => Move(sel, -1), "Alt+Up"));
                menu.Items.Add(MI("Move down", () => Move(sel, 1), "Alt+Down"));
                menu.Items.Add(MI("Remove", RemoveSelected, "Del"));
                menu.Items.Add(MI("Crop (keep only selected)", () => player.Remove(player.Items.Except(sel).ToList())));
                menu.Items.Add(new Separator());
                if (first.IsOnline)
                    menu.Items.Add(MI("Copy link", () => { try { Clipboard.SetText(first.Location); } catch { } }));
                else
                    menu.Items.Add(MI("Open file location", () => { try { Process.Start("explorer.exe", "/select,\"" + first.Location + "\""); } catch { } }));
                if (first.IsOnline)
                    menu.Items.Add(MI("Open in browser", () => { try { Process.Start(first.Location); } catch { } }));
                menu.Items.Add(new Separator());
            }
            menu.Items.Add(MI("Add files…", OpenFiles, "L"));
            menu.Items.Add(MI("Add folder…", OpenFolder));
            menu.Items.Add(MI("Add YouTube / SoundCloud / URL…", () => OpenUrlDialog(), "Ctrl+U"));
            menu.Items.Add(MI("Clear playlist", ClearPlaylist));
            return menu;
        }

        void Move(List<PlaylistItem> sel, int dir)
        {
            if (sel.Count == 0) return;
            var items = player.Items;
            var ordered = dir < 0 ? sel : Enumerable.Reverse(sel).ToList();
            foreach (var it in ordered)
            {
                int i = items.IndexOf(it), j = i + dir;
                if (j < 0 || j >= items.Count || sel.Contains(items[j])) continue;
                items.Move(i, j);
            }
            list.SelectedItems.Clear();
            foreach (var it in sel) list.SelectedItems.Add(it);
        }

        static MenuItem MI(string header, Action a, string gesture = null)
        {
            var mi = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
            mi.Click += (o, e) => a();
            return mi;
        }

        void ShowAddMenu(FrameworkElement anchor)
        {
            var m = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Top };
            m.Items.Add(MI("Add files…", OpenFiles, "L"));
            m.Items.Add(MI("Add folder…", OpenFolder, "Shift+L"));
            m.Items.Add(MI("Add YouTube / SoundCloud / URL…", () => OpenUrlDialog(), "Ctrl+U"));
            m.Items.Add(MI("Search YouTube / SoundCloud…", () => OpenUrlDialog()));
            m.IsOpen = true;
        }

        void ShowRemoveMenu(FrameworkElement anchor)
        {
            var m = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Top };
            m.Items.Add(MI("Remove selected", RemoveSelected, "Del"));
            m.Items.Add(MI("Remove failed / missing", () => player.Remove(player.Items.Where(i => i.Failed || (!i.IsOnline && !File.Exists(i.Location))).ToList())));
            m.Items.Add(MI("Remove duplicates", () =>
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                player.Remove(player.Items.Where(i => !seen.Add(i.Location)).ToList());
            }));
            m.Items.Add(MI("Clear playlist", ClearPlaylist));
            m.IsOpen = true;
        }

        void ShowSortMenu(FrameworkElement anchor)
        {
            var m = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Top };
            m.Items.Add(MI("Sort by title", () => Reorder(player.Items.OrderBy(i => i.DisplayTitle, StringComparer.CurrentCultureIgnoreCase))));
            m.Items.Add(MI("Sort by file path / link", () => Reorder(player.Items.OrderBy(i => i.Location, StringComparer.CurrentCultureIgnoreCase))));
            m.Items.Add(MI("Sort by length", () => Reorder(player.Items.OrderBy(i => i.Duration))));
            m.Items.Add(MI("Reverse list", () => Reorder(player.Items.Reverse())));
            m.Items.Add(MI("Randomize list", () => { var r = new Random(); Reorder(player.Items.OrderBy(i => r.Next())); }));
            m.IsOpen = true;
        }

        void ShowListMenu(FrameworkElement anchor)
        {
            var m = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Top };
            m.Items.Add(MI("Open playlist…", () => LoadPlaylist(true)));
            m.Items.Add(MI("Append playlist…", () => LoadPlaylist(false)));
            m.Items.Add(MI("Save playlist…", SavePlaylist, "Ctrl+S"));
            m.IsOpen = true;
        }

        void Reorder(IEnumerable<PlaylistItem> order)
        {
            var l = order.ToList();
            for (int i = 0; i < l.Count; i++)
            {
                int cur = player.Items.IndexOf(l[i]);
                if (cur != i) player.Items.Move(cur, i);
            }
        }

        void RemoveSelected()
        {
            var sel = Selected();
            if (sel.Count == 0) return;
            int idx = player.Items.IndexOf(sel[0]);
            player.Remove(sel);
            if (player.Items.Count > 0) list.SelectedIndex = Math.Min(idx, player.Items.Count - 1);
        }

        void ClearPlaylist()
        {
            player.Stop();
            player.Items.Clear();
        }

        void UpdatePlInfo()
        {
            if (plInfo == null) return;
            var total = TimeSpan.FromSeconds(player.Items.Sum(i => i.Duration.TotalSeconds));
            int idx = player.Current != null ? player.Items.IndexOf(player.Current) + 1 : 0;
            plInfo.Text = (idx > 0 ? idx + "/" : "") + player.Items.Count + "  " + Util.FormatTime(total);
        }

        // =====================================================================================
        // Adding media
        // =====================================================================================
        const string AudioFilter = "*.mp3;*.wav;*.wma;*.aac;*.m4a;*.flac;*.ogg;*.oga;*.opus;*.aif;*.aiff;*.alac;*.mp2;*.ac3;*.amr;*.weba;*.mka";
        const string VideoFilter = "*.mp4;*.m4v;*.mkv;*.avi;*.wmv;*.mov;*.webm;*.3gp;*.3g2;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.asf;*.flv;*.vob";

        void OpenFiles()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "Add music / video",
                Filter = "All media|" + AudioFilter + ";" + VideoFilter + ";*.m3u;*.m3u8;*.pls|Audio|" + AudioFilter + "|Video|" + VideoFilter + "|Playlists|*.m3u;*.m3u8;*.pls|All files|*.*"
            };
            if (dlg.ShowDialog(this) == true) AddPaths(dlg.FileNames, true);
        }

        void OpenFolder()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Add a folder of music / videos (subfolders included)", ShowNewFolderButton = false })
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) AddPaths(new[] { dlg.SelectedPath }, true);
            }
        }

        public void AddPaths(IEnumerable<string> paths, bool playFirst)
        {
            var items = player.ExpandPaths(paths);
            if (items.Count == 0) { player.ShowStatus("No playable media found.", 3); return; }
            player.AddRange(items);
            player.ShowStatus("Added " + items.Count + " item" + (items.Count == 1 ? "" : "s"), 2);
            if (playFirst && (player.State == PlayState.Stopped || player.State == PlayState.Paused)) player.Play(items[0]);
        }

        public async void AddUrl(string url, bool play)
        {
            if (!OnlineSources.IsKnownSite(url)) { AddPaths(new[] { url }, play); return; }
            if (!await EnsureYtDlpInteractive()) return;
            player.ShowStatus("Reading link…", 60);
            try
            {
                var items = await YtDlp.ListAsync(url);
                player.AddRange(items);
                player.ShowStatus("Added " + items.Count + " online item" + (items.Count == 1 ? "" : "s"), 2);
                if (play) player.Play(items[0]);
            }
            catch (Exception ex) { player.ShowStatus("Error: " + ex.Message, 6); }
        }

        void OpenUrlDialog()
        {
            var dlg = new AddUrlDialog(EnsureYtDlpInteractive) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null && dlg.Result.Count > 0)
            {
                player.AddRange(dlg.Result);
                player.ShowStatus("Added " + dlg.Result.Count + " online item" + (dlg.Result.Count == 1 ? "" : "s"), 2);
                if (dlg.PlayNow) player.Play(dlg.Result[0]);
                list.ScrollIntoView(dlg.Result[0]);
            }
        }

        void LoadPlaylist(bool replace)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Playlists|*.m3u;*.m3u8;*.pls|All files|*.*", Title = "Open playlist" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var items = PlaylistIO.Load(dlg.FileName);
                if (replace) ClearPlaylist();
                player.AddRange(items);
                if (replace && items.Count > 0) player.Play(items[0]);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "RetroAmp", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        void SavePlaylist()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "M3U8 playlist|*.m3u8|M3U playlist|*.m3u", FileName = "playlist.m3u8", Title = "Save playlist" };
            if (dlg.ShowDialog(this) != true) return;
            try { PlaylistIO.Save(dlg.FileName, player.Items); player.ShowStatus("Playlist saved", 2); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "RetroAmp", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    bool wasEmpty = player.Items.Count == 0;
                    AddPaths(files, wasEmpty || player.State == PlayState.Stopped);
                }
                else if (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text))
                {
                    string text = (e.Data.GetData(DataFormats.UnicodeText) ?? e.Data.GetData(DataFormats.Text)) as string;
                    var urls = (text ?? "").Split(new[] { '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(Util.IsUrl).ToList();
                    foreach (var u in urls) AddUrl(u, player.State == PlayState.Stopped && u == urls[0]);
                }
            }
            catch (Exception ex) { AppPaths.Log("Drop failed: " + ex.Message); }
        }

        // =====================================================================================
        // yt-dlp
        // =====================================================================================
        Task<bool> EnsureYtDlpInteractive()
        {
            if (YtDlp.Available) return Task.FromResult(true);
            if (ytDlpInstall != null && !ytDlpInstall.IsCompleted) return ytDlpInstall;
            ytDlpInstall = InstallYtDlp(true);
            return ytDlpInstall;
        }

        async Task<bool> InstallYtDlp(bool ask)
        {
            if (ask)
            {
                var r = MessageBox.Show(this,
                    "To play YouTube and SoundCloud, RetroAmp uses the free open-source tool \"yt-dlp\" (about 18 MB).\n\n" +
                    "Download it now from github.com/yt-dlp?\n\nIt is stored in:\n" + AppPaths.ToolsDir,
                    "RetroAmp – online streaming", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return false;
            }
            try
            {
                var prog = new Progress<double>(p => player.ShowStatus("Downloading yt-dlp… " + (int)(p * 100) + "%", 30));
                await Tools.InstallYtDlpAsync(prog);
                player.ShowStatus("yt-dlp installed ✓", 3);
                return true;
            }
            catch (Exception ex)
            {
                player.ShowStatus("yt-dlp download failed", 5);
                MessageBox.Show(this, "Could not download yt-dlp:\n" + ex.Message + "\n\nYou can also put yt-dlp.exe manually into:\n" + AppPaths.ToolsDir,
                    "RetroAmp", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        async void InstallDeno()
        {
            var r = MessageBox.Show(this,
                "YouTube now requires a JavaScript runtime for yt-dlp to get every format. RetroAmp can download Deno (about 40 MB, open source) " +
                "into its tools folder. It is optional – most videos still play without it.\n\nDownload Deno now?",
                "RetroAmp – Deno", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            try
            {
                var prog = new Progress<double>(p => player.ShowStatus("Downloading Deno… " + (int)(p * 100) + "%", 30));
                await Tools.InstallDenoAsync(prog);
                player.ShowStatus("Deno installed ✓", 3);
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not install Deno:\n" + ex.Message, "RetroAmp", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        async void UpdateYtDlp()
        {
            if (!YtDlp.Available) { await InstallYtDlp(false); return; }
            player.ShowStatus("Updating yt-dlp…", 60);
            try
            {
                string outp = await YtDlp.UpdateAsync();
                player.ShowStatus("yt-dlp update finished", 3);
                MessageBox.Show(this, string.IsNullOrWhiteSpace(outp) ? "Done." : outp, "yt-dlp update", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                player.ShowStatus("", 0);
                if (MessageBox.Show(this, "Self-update failed (" + ex.Message + "). Re-download the latest yt-dlp?", "RetroAmp", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    await InstallYtDlp(false);
            }
        }

        // =====================================================================================
        // Menus
        // =====================================================================================
        void ShowMainMenu()
        {
            var m = new ContextMenu();
            m.Items.Add(MI("Add files…", OpenFiles, "L"));
            m.Items.Add(MI("Add folder…", OpenFolder, "Shift+L"));
            m.Items.Add(MI("Add YouTube / SoundCloud / URL…", () => OpenUrlDialog(), "Ctrl+U"));
            m.Items.Add(new Separator());
            m.Items.Add(MI("Open playlist…", () => LoadPlaylist(true)));
            m.Items.Add(MI("Save playlist…", SavePlaylist, "Ctrl+S"));
            m.Items.Add(new Separator());
            m.Items.Add(MI("Visualizer window", ToggleVis, "K"));
            m.Items.Add(MI("Video window", ShowVideo));

            var opt = new MenuItem { Header = "Options" };
            var top = new MenuItem { Header = "Always on top", IsCheckable = true, IsChecked = s.AlwaysOnTop, InputGestureText = "Ctrl+T" };
            top.Click += (o, e) => ToggleTopmost();
            opt.Items.Add(top);
            var size = new MenuItem { Header = "Size" };
            foreach (var z in new[] { 1.0, 1.25, 1.5, 1.75, 2.0 })
            {
                double zz = z;
                var mi = new MenuItem { Header = (int)(z * 100) + "%", IsCheckable = true, IsChecked = Math.Abs(s.Scale - z) < 0.01 };
                mi.Click += (o, e) => { s.Scale = zz; scale.ScaleX = scale.ScaleY = zz; };
                size.Items.Add(mi);
            }
            opt.Items.Add(size);
            var rem = new MenuItem { Header = "Show remaining time", IsCheckable = true, IsChecked = s.ShowRemaining };
            rem.Click += (o, e) => { s.ShowRemaining = !s.ShowRemaining; lcd.ShowRemaining = s.ShowRemaining; };
            opt.Items.Add(rem);
            var dev = new MenuItem { Header = "Audio output device" };
            var devs = AudioEngine.GetDevices();
            var dm = new MenuItem { Header = "Windows default", IsCheckable = true, IsChecked = s.AudioDevice < 0 };
            dm.Click += (o, e) => SetDevice(-1);
            dev.Items.Add(dm);
            for (int i = 0; i < devs.Count; i++)
            {
                int k = i;
                var mi = new MenuItem { Header = devs[i], IsCheckable = true, IsChecked = s.AudioDevice == i };
                mi.Click += (o, e) => SetDevice(k);
                dev.Items.Add(mi);
            }
            opt.Items.Add(dev);
            m.Items.Add(opt);

            var online = new MenuItem { Header = "Online (YouTube / SoundCloud)" };
            online.Items.Add(MI(YtDlp.Available ? "Update yt-dlp" : "Install yt-dlp", UpdateYtDlp));
            online.Items.Add(MI(Tools.DenoPath != null ? "Deno installed ✓ (reinstall)" : "Install Deno (better YouTube support)", InstallDeno));
            var ck = new MenuItem { Header = "YouTube: use sign-in cookies from browser" };
            foreach (var b in new[] { "", "firefox", "edge", "chrome", "brave", "opera", "vivaldi" })
            {
                string bb = b;
                var mi = new MenuItem { Header = b == "" ? "Off" : char.ToUpper(b[0]) + b.Substring(1), IsCheckable = true, IsChecked = (s.CookiesBrowser ?? "") == b };
                mi.Click += (o, e) =>
                {
                    s.CookiesBrowser = bb; YtDlp.CookiesBrowser = bb;
                    player.ShowStatus(bb == "" ? "Browser cookies off" : "Using " + bb + " cookies for YouTube (close the browser if it fails)", 4);
                };
                ck.Items.Add(mi);
            }
            ck.ToolTip = "Helps when YouTube says 'Sign in to confirm you're not a bot'. Firefox works best.";
            online.Items.Add(ck);
            online.Items.Add(new Separator());
            online.Items.Add(MI("Open cache folder", () => { try { Process.Start("explorer.exe", "\"" + AppPaths.CacheDir + "\""); } catch { } }));
            online.Items.Add(MI("Clear download cache (" + YtDlp.CacheSizeMb() + " MB)", () =>
            {
                string keep = player.Current?.CachedFile;
                foreach (var f in Directory.GetFiles(AppPaths.CacheDir))
                {
                    if (keep != null && string.Equals(f, keep, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(f); } catch { }
                }
                foreach (var it in player.Items) if (it.CachedFile != keep) it.CachedFile = null;
                player.ShowStatus("Cache cleared", 2);
            }));
            m.Items.Add(online);
            m.Items.Add(new Separator());
            m.Items.Add(MI("Keyboard shortcuts", ShowShortcuts, "F1"));
            m.Items.Add(MI("About RetroAmp", ShowAbout));
            m.Items.Add(new Separator());
            m.Items.Add(MI("Exit", Close, "Alt+F4"));
            m.PlacementTarget = this;
            m.IsOpen = true;
        }

        void SetDevice(int i)
        {
            s.AudioDevice = i;
            try { player.Audio.DeviceNumber = i; } catch (Exception ex) { player.ShowStatus("Device error: " + ex.Message, 5); }
        }

        void ToggleTopmost()
        {
            s.AlwaysOnTop = !s.AlwaysOnTop;
            Topmost = s.AlwaysOnTop;
            player.ShowStatus("Always on top " + (s.AlwaysOnTop ? "on" : "off"), 1.5);
        }

        void ShowShortcuts()
        {
            MessageBox.Show(this,
                "Z  previous        X  play          C  pause\n" +
                "V  stop            B  next          Space  play/pause\n" +
                "←/→  seek 5 s      ↑/↓  volume\n" +
                "S  shuffle         R  repeat\n" +
                "E  equalizer       P  playlist      K  visualizer\n" +
                "L  add files       Shift+L  add folder\n" +
                "Ctrl+U  add YouTube / SoundCloud link or search\n" +
                "Ctrl+F  search playlist     Ctrl+S  save playlist\n" +
                "Ctrl+T  always on top       Del  remove selected\n\n" +
                "Media keys on your keyboard work globally.\n" +
                "Visualizer: click/←/→ change style, double-click = full screen.",
                "RetroAmp – keyboard shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void ShowAbout()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show(this,
                "RetroAmp " + v.Major + "." + v.Minor + "." + v.Build + "\n" +
                "A lightweight retro-style music & video player.\n\n" +
                "• Local audio: MP3, FLAC, M4A/AAC, WMA, WAV, OGG/Opus*\n" +
                "• Video: MP4, MKV, AVI, WMV, MOV, WebM* (Windows codecs)\n" +
                "• YouTube & SoundCloud via yt-dlp\n" +
                "• 10-band EQ, presets, reverb, stereo width, karaoke, crossfade\n" +
                "• Visualizers: spectrum, scope, speakers & VU meters, radial, starfield\n\n" +
                "* some formats need the free Microsoft Store codec extensions.\n\n" +
                "Data folder: " + AppPaths.DataDir + (AppPaths.IsPortable ? "  (portable mode)" : "") + "\n\n" +
                "Audio engine: NAudio · Tags: TagLib# · Online: yt-dlp\nMIT License",
                "About RetroAmp", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =====================================================================================
        // Windows
        // =====================================================================================
        void ToggleVis()
        {
            if (visWin == null)
            {
                visWin = new VisualizerWindow(player, s);
                visWin.IsVisibleChanged += (o, e) => btnVis.IsChecked = visWin.IsVisible;
            }
            if (visWin.IsVisible) { visWin.Close(); return; }
            visWin.Show();
            visWin.Activate();
            s.ShowVisWindow = true;
        }

        void ShowVideo()
        {
            if (videoWin == null) videoWin = new VideoWindow(player);
            if (!videoWin.IsVisible) videoWin.Show();
            if (videoWin.WindowState == WindowState.Minimized) videoWin.WindowState = WindowState.Normal;
            videoWin.Activate();
        }

        void OnTrackChanged()
        {
            var cur = player.Current;
            Title = cur != null ? cur.DisplayTitle + " - RetroAmp" : "RetroAmp";
            if (cur != null && list != null && player.Items.Contains(cur)) list.ScrollIntoView(cur);
            UpdatePlInfo();
            if (cur != null && !cur.IsVideo && videoWin != null && videoWin.IsVisible && player.State != PlayState.Loading) { }
        }

        void OnStateChanged()
        {
            if (thumbPlay != null)
            {
                bool playing = player.State == PlayState.Playing;
                thumbPlay.ImageSource = playing ? imgPause : imgPlay;
                thumbPlay.Description = playing ? "Pause" : "Play";
            }
            if (TaskbarItemInfo != null)
                TaskbarItemInfo.ProgressState = player.State == PlayState.Loading ? TaskbarItemProgressState.Indeterminate :
                    player.State == PlayState.Paused ? TaskbarItemProgressState.Paused : TaskbarItemProgressState.None;
            // Hide the video window when an audio track takes over
            if (videoWin != null && videoWin.IsVisible && player.State == PlayState.Playing && !player.UsingMedia)
                videoWin.Hide();
        }

        int slowTick;
        void OnTimer()
        {
            player.Tick();
            var dur = player.Duration;
            bool active = player.State == PlayState.Playing || player.State == PlayState.Paused;
            seek.Enabled = active && dur > TimeSpan.Zero;
            if (!seek.IsDragging)
                seek.Value = active && dur.TotalSeconds > 0 ? player.Position.TotalSeconds / dur.TotalSeconds : 0;
            seek.InvalidateVisual();
            if (++slowTick % 10 == 0) UpdatePlInfo();
        }

        void SetupTaskbar()
        {
            try
            {
                imgPlay = GlyphImage(GlyphKind.Play);
                imgPause = GlyphImage(GlyphKind.Pause);
                var prev = new ThumbButtonInfo { ImageSource = GlyphImage(GlyphKind.Prev), Description = "Previous" };
                prev.Click += (o, e) => player.Previous();
                thumbPlay = new ThumbButtonInfo { ImageSource = imgPlay, Description = "Play" };
                thumbPlay.Click += (o, e) => player.TogglePlayPause();
                var next = new ThumbButtonInfo { ImageSource = GlyphImage(GlyphKind.Next), Description = "Next" };
                next.Click += (o, e) => player.Next();
                TaskbarItemInfo = new TaskbarItemInfo();
                TaskbarItemInfo.ThumbButtonInfos.Add(prev);
                TaskbarItemInfo.ThumbButtonInfos.Add(thumbPlay);
                TaskbarItemInfo.ThumbButtonInfos.Add(next);
            }
            catch (Exception ex) { AppPaths.Log("Taskbar: " + ex.Message); }
        }

        static ImageSource GlyphImage(GlyphKind g)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) RetroButton.DrawGlyph(dc, g, 16, 16, 40, Brushes.White);
            var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }

        // =====================================================================================
        // Keyboard + global media keys
        // =====================================================================================
        void OnKey(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (ctrl)
            {
                switch (key)
                {
                    case Key.U: OpenUrlDialog(); break;
                    case Key.O: OpenFiles(); break;
                    case Key.S: SavePlaylist(); break;
                    case Key.T: ToggleTopmost(); break;
                    case Key.F: if (!s.ShowPlaylist) { s.ShowPlaylist = true; ApplyPanelVisibility(); } search.Focus(); break;
                    case Key.V:
                        if (Keyboard.FocusedElement is TextBox) return;
                        try { if (Clipboard.ContainsText()) foreach (var u in Clipboard.GetText().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(Util.IsUrl)) AddUrl(u, false); } catch { }
                        break;
                    default: return;
                }
                e.Handled = true;
                return;
            }
            if (alt && list.IsKeyboardFocusWithin && (key == Key.Up || key == Key.Down))
            {
                Move(Selected(), key == Key.Up ? -1 : 1);
                e.Handled = true;
                return;
            }
            if (Keyboard.FocusedElement is TextBox) return;
            bool inList = list.IsKeyboardFocusWithin;
            switch (key)
            {
                case Key.Z: player.Previous(); break;
                case Key.X: PlayButton(); break;
                case Key.C: if (player.State == PlayState.Paused) player.Resume(); else player.Pause(); break;
                case Key.V: player.Stop(); break;
                case Key.B: player.Next(); break;
                case Key.Space: player.TogglePlayPause(); break;
                case Key.L: if (shift) OpenFolder(); else OpenFiles(); break;
                case Key.S: ToggleShuffle(); break;
                case Key.R: CycleRepeat(); break;
                case Key.E: s.ShowEq = !s.ShowEq; ApplyPanelVisibility(); break;
                case Key.P: s.ShowPlaylist = !s.ShowPlaylist; ApplyPanelVisibility(); break;
                case Key.K: ToggleVis(); break;
                case Key.F1: ShowShortcuts(); break;
                case Key.Left: if (inList) return; player.Seek(Max0(player.Position - TimeSpan.FromSeconds(5))); break;
                case Key.Right: if (inList) return; player.Seek(player.Position + TimeSpan.FromSeconds(5)); break;
                case Key.Up: if (inList) return; vol.Value = s.Volume = Math.Min(1, s.Volume + 0.05); player.ApplyVolume(); player.ShowStatus("Volume: " + (int)(s.Volume * 100) + "%", 1.2); break;
                case Key.Down: if (inList) return; vol.Value = s.Volume = Math.Max(0, s.Volume - 0.05); player.ApplyVolume(); player.ShowStatus("Volume: " + (int)(s.Volume * 100) + "%", 1.2); break;
                default: return;
            }
            e.Handled = true;
        }

        static TimeSpan Max0(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        const int WM_HOTKEY = 0x0312, WM_APPCOMMAND = 0x0319;
        static readonly uint[] MediaKeys = { 0xB3, 0xB2, 0xB0, 0xB1 }; // play/pause, stop, next, prev
        IntPtr hwnd;

        void HookWindow()
        {
            hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            for (int i = 0; i < MediaKeys.Length; i++)
            {
                try { RegisterHotKey(hwnd, 0x5100 + i, 0, MediaKeys[i]); } catch { }
            }
        }

        IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                switch (wParam.ToInt32() - 0x5100)
                {
                    case 0: player.TogglePlayPause(); handled = true; break;
                    case 1: player.Stop(); handled = true; break;
                    case 2: player.Next(); handled = true; break;
                    case 3: player.Previous(); handled = true; break;
                }
            }
            else if (msg == WM_APPCOMMAND)
            {
                int cmd = (int)((lParam.ToInt64() >> 16) & 0x0FFF);
                switch (cmd)
                {
                    case 14: player.TogglePlayPause(); handled = true; break;
                    case 46: player.Resume(); handled = true; break;     // APPCOMMAND_MEDIA_PLAY
                    case 47: player.Pause(); handled = true; break;      // APPCOMMAND_MEDIA_PAUSE
                    case 13: player.Stop(); handled = true; break;
                    case 11: player.Next(); handled = true; break;
                    case 12: player.Previous(); handled = true; break;
                }
                if (handled) return new IntPtr(1);
            }
            return IntPtr.Zero;
        }

        // =====================================================================================
        // Shutdown
        // =====================================================================================
        void OnClosing(object sender, CancelEventArgs e)
        {
            VisualizerWindow.AppClosing = true;
            try
            {
                for (int i = 0; i < MediaKeys.Length; i++) UnregisterHotKey(hwnd, 0x5100 + i);
                if (WindowState == WindowState.Normal) { s.WindowLeft = Left; s.WindowTop = Top; }
                s.LastIndex = player.Current != null ? player.Items.IndexOf(player.Current) : -1;
                s.ShowVisWindow = visWin != null && visWin.IsVisible;
                PlaylistIO.Save(AppPaths.LastPlaylistFile, player.Items);
                s.Save();
            }
            catch (Exception ex) { AppPaths.Log("Save on exit failed: " + ex.Message); }
            timer.Stop();
            try { visWin?.Close(); } catch { }
            try { videoWin?.Close(); } catch { }
            player.Dispose();
        }
    }
}
