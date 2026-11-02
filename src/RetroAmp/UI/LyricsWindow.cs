using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetroAmp.Core;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    public sealed class LyricsWindow : Window
    {
        readonly Player player;
        readonly LyricsController lyrics;
        readonly Settings s;
        readonly LyricsView view;
        readonly TextBlock info;
        public Action ToggleDesktop;

        public LyricsWindow(Player player, LyricsController lyrics, Settings settings)
        {
            this.player = player; this.lyrics = lyrics; s = settings;
            Title = "RetroAmp Lyrics";
            Width = 460; Height = 520; MinWidth = 220; MinHeight = 160;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Background = Theme.WindowBg;
            UseLayoutRounding = true;

            var title = new TitleBar("LYRICS");
            title.AddButton(GlyphKind.Minimize, "Lyrics earlier (−0.5 s)", () => lyrics.AdjustOffset(-500));
            title.AddButton(GlyphKind.Menu, "Lyrics options", ShowMenu);
            title.AddButton(GlyphKind.Close, "Close", Close);
            view = new LyricsView(player, lyrics) { FontSize = s.LyricsFontSize };
            info = new TextBlock { FontSize = 10, Margin = new Thickness(6, 2, 18, 3), TextTrimming = TextTrimming.CharacterEllipsis };

            var dock = new DockPanel();
            DockPanel.SetDock(title, Dock.Top);
            DockPanel.SetDock(info, Dock.Bottom);
            dock.Children.Add(title);
            dock.Children.Add(info);
            dock.Children.Add(new Border { BorderBrush = Theme.BevelDark, BorderThickness = new Thickness(1), Child = view });
            Content = dock;
            ApplyTheme();

            view.MouseRightButtonUp += (o, e) => ShowMenu();
            lyrics.Changed += UpdateInfo;
            player.TrackChanged += UpdateInfo;
            KeyDown += (o, e) =>
            {
                if (e.Key == Key.Escape) Close();
                else if (e.Key == Key.OemPlus || e.Key == Key.Add) lyrics.AdjustOffset(500);
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) lyrics.AdjustOffset(-500);
                else if (e.Key == Key.Space) player.TogglePlayPause();
            };
            Closing += (o, e) =>
            {
                if (VisualizerWindow.AppClosing) return;
                e.Cancel = true; Hide(); s.ShowLyricsWindow = false;
            };
            IsVisibleChanged += (o, e) => { if (IsVisible) lyrics.EnsureLoaded(); };
            UpdateInfo();
        }

        public void ApplyTheme()
        {
            Background = Theme.WindowBg;
            info.Foreground = Theme.LabelText;
            info.Background = Theme.PanelFace;
            Theme.InvalidateTree(this);
        }

        void UpdateInfo()
        {
            var ly = lyrics.Current;
            if (ly == null) info.Text = player.Current == null ? "" : lyrics.Status;
            else info.Text = "Source: " + ly.Source + (ly.Synced ? " · synced" : " · text only") +
                             (ly.OffsetMs != 0 ? " · offset " + (ly.OffsetMs > 0 ? "+" : "") + (ly.OffsetMs / 1000.0).ToString("0.0") + " s" : "") +
                             "   (click a line to jump there)";
        }

        MenuItem MI(string h, Action a, bool? check = null)
        {
            var mi = new MenuItem { Header = h };
            if (check.HasValue) { mi.IsCheckable = true; mi.IsChecked = check.Value; }
            mi.Click += (o, e) => a();
            return mi;
        }

        void ShowMenu()
        {
            var m = new ContextMenu();
            m.Items.Add(MI("Search lyrics…", SearchDialog));
            m.Items.Add(MI("Search again automatically", () => { if (player.Current != null) _ = lyrics.Load(player.Current, true); }));
            m.Items.Add(MI("Load .lrc / .txt file…", LoadFile));
            if (lyrics.Current != null) m.Items.Add(MI("Copy lyrics", () => { try { Clipboard.SetText(lyrics.Current.PlainText); } catch { } }));
            m.Items.Add(new Separator());
            m.Items.Add(MI("Lyrics later (+0.5 s)", () => lyrics.AdjustOffset(500)));
            m.Items.Add(MI("Lyrics earlier (−0.5 s)", () => lyrics.AdjustOffset(-500)));
            m.Items.Add(MI("Reset offset", () => { if (lyrics.Current != null) lyrics.AdjustOffset(-lyrics.Current.OffsetMs); }));
            m.Items.Add(new Separator());
            m.Items.Add(MI("Bigger text", () => SetFont(s.LyricsFontSize + 2)));
            m.Items.Add(MI("Smaller text", () => SetFont(s.LyricsFontSize - 2)));
            m.Items.Add(MI("Desktop lyrics", () => ToggleDesktop?.Invoke(), s.DesktopLyrics));
            m.Items.Add(MI("Find lyrics automatically for every song", () => s.LyricsAuto = !s.LyricsAuto, s.LyricsAuto));
            m.PlacementTarget = this;
            m.IsOpen = true;
        }

        void SetFont(double size)
        {
            s.LyricsFontSize = Util.Clamp(size, 10, 48);
            view.FontSize = s.LyricsFontSize;
        }

        void LoadFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Lyrics|*.lrc;*.txt|All files|*.*", Title = "Load lyrics" };
            if (dlg.ShowDialog(this) == true)
            {
                try { lyrics.LoadFile(dlg.FileName); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "RetroAmp"); }
            }
        }

        public void SearchDialog()
        {
            var item = player.Current;
            if (item == null) return;
            var dlg = new LyricsSearchDialog(item) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Chosen != null)
                lyrics.SetLyrics(item, LyricsService.SaveFromLrcLib(item, dlg.Chosen));
        }
    }

    /// <summary>Manual LRCLIB search: edit artist/title, pick a result.</summary>
    public sealed class LyricsSearchDialog : Window
    {
        public LrcLibItem Chosen { get; private set; }
        readonly TextBox artist, title;
        readonly ListBox results;
        readonly TextBlock status;

        public LyricsSearchDialog(PlaylistItem item)
        {
            DialogStyle.Apply(this, "Search lyrics (LRCLIB)");
            LyricsService.GuessArtistTitle(item, out string a, out string t);
            var root = new StackPanel { Margin = new Thickness(14), Width = 480 };
            root.Children.Add(DialogStyle.Text("Artist:"));
            artist = DialogStyle.TextBox(a); root.Children.Add(artist);
            root.Children.Add(DialogStyle.Text("Song title:"));
            title = DialogStyle.TextBox(t); root.Children.Add(title);
            title.KeyDown += (o, e) => { if (e.Key == Key.Enter) Search(); };
            artist.KeyDown += (o, e) => { if (e.Key == Key.Enter) Search(); };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(DialogStyle.Button("SEARCH", 80, Search));
            root.Children.Add(row);
            results = new ListBox
            {
                Height = 220, Margin = new Thickness(0, 8, 0, 8), Background = Theme.PlaylistBg, Foreground = Theme.PlaylistText,
                BorderBrush = Theme.BevelDark, FontSize = 12, ItemContainerStyle = Xaml.Parse<Style>(Xaml.ListItemStyle)
            };
            results.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), Xaml.Parse<Style>(Xaml.ScrollBarStyle));
            results.MouseDoubleClick += (o, e) => Use();
            root.Children.Add(results);
            var row2 = new StackPanel { Orientation = Orientation.Horizontal };
            row2.Children.Add(DialogStyle.Button("USE SELECTED", 110, Use));
            row2.Children.Add(DialogStyle.Button("CANCEL", 70, () => DialogResult = false));
            root.Children.Add(row2);
            status = DialogStyle.Text("", 12, Theme.LcdAmber);
            root.Children.Add(status);
            Content = root;
            Loaded += (o, e) => Search();
        }

        async void Search()
        {
            status.Text = "Searching…";
            try
            {
                var list = await LyricsService.SearchAsync(artist.Text.Trim().Length > 0 ? artist.Text.Trim() : null, title.Text.Trim(), CancellationToken.None);
                if (list.Count == 0 && artist.Text.Trim().Length > 0)
                    list = await LyricsService.SearchAsync(null, (artist.Text + " " + title.Text).Trim(), CancellationToken.None);
                results.ItemsSource = list.Where(x => x.HasAny).OrderByDescending(x => x.HasSynced).ToList();
                status.Text = results.Items.Count == 0 ? "Nothing found. Try a shorter title or remove the artist." : results.Items.Count + " results — double-click to use.";
            }
            catch (Exception ex) { status.Text = "Error: " + ex.Message; }
        }

        void Use()
        {
            Chosen = results.SelectedItem as LrcLibItem;
            if (Chosen != null) DialogResult = true;
        }
    }
}
