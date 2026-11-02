using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroAmp.Core;

namespace RetroAmp.UI
{
    public sealed class AboutWindow : Window
    {
        public const string Developer = "NhảmStudio";
        public const string Website = "https://topvl.net";

        public AboutWindow()
        {
            DialogStyle.Apply(this, "About RetroAmp");
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            var root = new StackPanel { Width = 440 };

            var logoCard = new Border { Background = Brushes.White, Padding = new Thickness(20, 16, 20, 12) };
            var logo = LoadImage("RetroAmp.nhamstudio.png");
            if (logo != null) logoCard.Child = new Image { Source = logo, Height = 150, Stretch = Stretch.Uniform };
            root.Children.Add(logoCard);

            var body = new StackPanel { Margin = new Thickness(18, 12, 18, 14) };
            body.Children.Add(new TextBlock { Text = "RetroAmp " + v.Major + "." + v.Minor + "." + v.Build, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Theme.TitleText });
            body.Children.Add(DialogStyle.Text("Trình nghe nhạc & xem phim phong cách cổ điển — nhẹ, có YouTube / SoundCloud, EQ, visualizer và lời bài hát chạy theo nhạc.", 12));

            var grid = new Grid { Margin = new Thickness(0, 8, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            AddRow(grid, 0, "Dev:", new TextBlock { Text = Developer, FontWeight = FontWeights.Bold, Foreground = Theme.LabelText, FontSize = 13 });
            var link = new Hyperlink(new Run(Website)) { NavigateUri = new Uri(Website), Foreground = Theme.GlyphActive };
            link.RequestNavigate += (o, e) => { try { Process.Start(new ProcessStartInfo(Website) { UseShellExecute = true }); } catch { } e.Handled = true; };
            AddRow(grid, 1, "Web:", new TextBlock(link) { FontSize = 13 });
            body.Children.Add(grid);

            body.Children.Add(DialogStyle.Text(
                "Âm thanh: NAudio · Tag: TagLib# · Online: yt-dlp · Lời bài hát: LRCLIB\n" +
                "Thư mục dữ liệu: " + AppPaths.DataDir + (AppPaths.IsPortable ? "  (portable)" : "") + "\nMIT License", 10.5, Theme.DimText));
            var ok = DialogStyle.Button("OK", 80, Close);
            ok.HorizontalAlignment = HorizontalAlignment.Right;
            ok.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(ok);
            root.Children.Add(body);
            Content = root;
            KeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Enter) Close(); };
        }

        static void AddRow(Grid g, int row, string label, UIElement value)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Foreground = Theme.DimText, FontSize = 13, Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(l, row); g.Children.Add(l);
            Grid.SetRow(value, row); Grid.SetColumn(value, 1); g.Children.Add(value);
        }

        public static BitmapImage LoadImage(string resource)
        {
            try
            {
                var st = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
                if (st == null) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = st;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
    }
}
