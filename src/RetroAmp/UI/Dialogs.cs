using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetroAmp.Core;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    public static class DialogStyle
    {
        public static TextBox TextBox(string text = "") => new TextBox
        {
            Text = text,
            Background = Theme.B("#07090D"),
            Foreground = Theme.B("#B8FFC4"),
            CaretBrush = Theme.B("#B8FFC4"),
            BorderBrush = Theme.B("#3A4258"),
            SelectionBrush = Theme.B("#2A4FBF"),
            Padding = new Thickness(4, 3, 4, 3),
            FontSize = 13
        };

        public static TextBlock Text(string t, double size = 12, Brush b = null) => new TextBlock
        {
            Text = t, FontSize = size, Foreground = b ?? Theme.B("#C9CEDD"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4)
        };

        public static RetroButton Button(string text, double w, Action click) =>
            new RetroButton { Text = text, Width = w, Height = 24, FontSize = 11, Margin = new Thickness(0, 0, 6, 0) }.With(click);

        static RetroButton With(this RetroButton b, Action click) { if (click != null) b.Click += click; return b; }

        public static void Apply(Window w, string title)
        {
            w.Title = title;
            w.Background = Theme.B("#1A1C24");
            w.Foreground = Theme.B("#C9CEDD");
            w.FontFamily = Theme.UiFont;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            w.ResizeMode = ResizeMode.NoResize;
            w.SizeToContent = SizeToContent.WidthAndHeight;
            w.UseLayoutRounding = true;
        }
    }

    /// <summary>Add a YouTube / SoundCloud / stream link, or search YouTube / SoundCloud.</summary>
    public sealed class AddUrlDialog : Window
    {
        public List<PlaylistItem> Result { get; private set; }
        public bool PlayNow { get; private set; }

        readonly Func<Task<bool>> ensureYtDlp;
        readonly TextBox url, query;
        readonly CheckBox asVideo;
        readonly ListBox results;
        readonly TextBlock status;
        readonly RetroButton ytBtn, scBtn;
        bool busy;

        public AddUrlDialog(Func<Task<bool>> ensureYtDlp)
        {
            this.ensureYtDlp = ensureYtDlp;
            DialogStyle.Apply(this, "Add link · YouTube · SoundCloud");
            var root = new StackPanel { Margin = new Thickness(14), Width = 540 };

            root.Children.Add(DialogStyle.Text("Paste a link — YouTube video / playlist / channel, SoundCloud track / playlist / artist, or any internet radio / media URL:"));
            url = DialogStyle.TextBox();
            try { if (Clipboard.ContainsText()) { var c = Clipboard.GetText().Trim(); if (Util.IsUrl(c)) url.Text = c; } } catch { }
            url.KeyDown += (s, e) => { if (e.Key == Key.Enter) AddLink(true); };
            root.Children.Add(url);
            asVideo = new CheckBox { Content = "Play as video (YouTube, Vimeo… opens the video window)", Foreground = Theme.B("#C9CEDD"), Margin = new Thickness(0, 6, 0, 6) };
            root.Children.Add(asVideo);
            var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 10) };
            row1.Children.Add(DialogStyle.Button("ADD & PLAY", 100, () => AddLink(true)));
            row1.Children.Add(DialogStyle.Button("ADD", 70, () => AddLink(false)));
            root.Children.Add(row1);

            root.Children.Add(new Border { Height = 1, Background = Theme.B("#3A4258"), Margin = new Thickness(0, 4, 0, 8) });
            root.Children.Add(DialogStyle.Text("Or search:"));
            var row2 = new Grid();
            row2.ColumnDefinitions.Add(new ColumnDefinition());
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            query = DialogStyle.TextBox();
            query.KeyDown += (s, e) => { if (e.Key == Key.Enter) Search(); };
            row2.Children.Add(query);
            var srcPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
            ytBtn = new RetroButton { Text = "YouTube", IsToggle = true, IsChecked = true, Width = 86, Height = 24, FontSize = 11, Margin = new Thickness(0, 0, 4, 0) };
            scBtn = new RetroButton { Text = "SoundCloud", IsToggle = true, Width = 96, Height = 24, FontSize = 11, Margin = new Thickness(0, 0, 4, 0) };
            ytBtn.Click += () => { ytBtn.IsChecked = true; scBtn.IsChecked = false; };
            scBtn.Click += () => { scBtn.IsChecked = true; ytBtn.IsChecked = false; };
            srcPanel.Children.Add(ytBtn); srcPanel.Children.Add(scBtn);
            srcPanel.Children.Add(new RetroButton { Text = "SEARCH", Width = 70, Height = 24, FontSize = 11 }.Also(b => b.Click += Search));
            Grid.SetColumn(srcPanel, 1);
            row2.Children.Add(srcPanel);
            root.Children.Add(row2);

            results = new ListBox
            {
                Height = 230,
                Margin = new Thickness(0, 8, 0, 8),
                Background = Brushes.Black,
                BorderBrush = Theme.B("#3A4258"),
                SelectionMode = SelectionMode.Extended,
                FontSize = 12,
                ItemTemplate = Xaml.Parse<DataTemplate>(Xaml.ResultItemTemplate),
                ItemContainerStyle = Xaml.Parse<Style>(Xaml.ListItemStyle)
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(results, ScrollBarVisibility.Disabled);
            results.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), Xaml.Parse<Style>(Xaml.ScrollBarStyle));
            results.MouseDoubleClick += (s, e) => TakeResults(true);
            root.Children.Add(results);

            var row3 = new StackPanel { Orientation = Orientation.Horizontal };
            row3.Children.Add(DialogStyle.Button("PLAY SELECTED", 120, () => TakeResults(true)));
            row3.Children.Add(DialogStyle.Button("ADD SELECTED", 110, () => TakeResults(false)));
            row3.Children.Add(DialogStyle.Button("ADD ALL", 80, () => { results.SelectAll(); TakeResults(false); }));
            row3.Children.Add(DialogStyle.Button("CLOSE", 70, Close));
            root.Children.Add(row3);

            status = DialogStyle.Text("", 12, Theme.LcdAmber);
            root.Children.Add(status);
            Content = root;
            Loaded += (s, e) => { url.Focus(); url.SelectAll(); };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        async void AddLink(bool play)
        {
            if (busy) return;
            string link = url.Text.Trim();
            if (!Util.IsUrl(link)) { status.Text = "Please paste a valid http(s) link."; return; }
            if (!OnlineSources.IsKnownSite(link) && !asVideo.IsChecked.GetValueOrDefault())
            {
                Result = new List<PlaylistItem> { new PlaylistItem(link) };
                PlayNow = play; DialogResult = true; return;
            }
            busy = true;
            try
            {
                if (!await ensureYtDlp()) { status.Text = "yt-dlp is required for this link."; return; }
                status.Text = "Reading link… (big playlists can take a little while)";
                var items = await YtDlp.ListAsync(link);
                bool video = asVideo.IsChecked.GetValueOrDefault();
                foreach (var it in items) it.PlayAsVideo = video;
                Result = items; PlayNow = play; DialogResult = true;
            }
            catch (Exception ex) { status.Text = "Error: " + ex.Message; }
            finally { busy = false; }
        }

        async void Search()
        {
            if (busy) return;
            string q = query.Text.Trim();
            if (q.Length == 0) return;
            busy = true;
            try
            {
                if (!await ensureYtDlp()) { status.Text = "yt-dlp is required for searching."; return; }
                status.Text = "Searching…";
                string prefix = scBtn.IsChecked ? "scsearch25:" : "ytsearch25:";
                var items = await YtDlp.ListAsync(prefix + q);
                results.ItemsSource = items;
                status.Text = items.Count + " results. Double-click to play, or select several and add.";
            }
            catch (Exception ex) { status.Text = "Error: " + ex.Message; }
            finally { busy = false; }
        }

        void TakeResults(bool play)
        {
            var sel = results.SelectedItems.Cast<PlaylistItem>().ToList();
            if (sel.Count == 0) { status.Text = "Select one or more results first."; return; }
            var src = results.ItemsSource as List<PlaylistItem>;
            if (src != null) sel = sel.OrderBy(i => src.IndexOf(i)).ToList();
            bool video = asVideo.IsChecked.GetValueOrDefault();
            Result = sel.Select(i => new PlaylistItem(i.Location, i.Title, i.Duration > TimeSpan.Zero ? i.Duration : (TimeSpan?)null) { Artist = i.Artist, PlayAsVideo = video }).ToList();
            PlayNow = play;
            DialogResult = true;
        }
    }

    public sealed class InputDialog : Window
    {
        readonly TextBox box;
        public string Value => box.Text;

        public InputDialog(string title, string prompt, string initial = "")
        {
            DialogStyle.Apply(this, title);
            var root = new StackPanel { Margin = new Thickness(14), Width = 320 };
            root.Children.Add(DialogStyle.Text(prompt));
            box = DialogStyle.TextBox(initial);
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) DialogResult = true; if (e.Key == Key.Escape) DialogResult = false; };
            root.Children.Add(box);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            row.Children.Add(DialogStyle.Button("OK", 70, () => DialogResult = true));
            row.Children.Add(DialogStyle.Button("CANCEL", 70, () => DialogResult = false));
            root.Children.Add(row);
            Content = root;
            Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
        }
    }

    public static class Ext
    {
        public static T Also<T>(this T obj, Action<T> a) { a(obj); return obj; }
    }
}
