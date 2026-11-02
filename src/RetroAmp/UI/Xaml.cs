using System.Windows.Markup;

namespace RetroAmp.UI
{
    /// <summary>Runtime XAML parsing for templates/styles (keeps the build free of XAML compilation).</summary>
    public static class Xaml
    {
        const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

        public static T Parse<T>(string xaml)
        {
            int i = xaml.IndexOf(' ');
            int j = xaml.IndexOf('>');
            int at = (i > 0 && i < j) ? i : j;
            if (xaml[at - 1] == '/') at--;
            string withNs = xaml.Substring(0, at) + " " + Ns + xaml.Substring(at);
            return (T)XamlReader.Parse(withNs);
        }

        public static string ScrollBarStyle => Themed(@"<Style TargetType=""ScrollBar"">
  <Setter Property=""Width"" Value=""11""/>
  <Setter Property=""MinWidth"" Value=""11""/>
  <Setter Property=""Template""><Setter.Value>
    <ControlTemplate TargetType=""ScrollBar"">
      <Border Background=""#0B0D12"" BorderBrush=""#1A1D26"" BorderThickness=""1,0,0,0"">
        <Track x:Name=""PART_Track"" IsDirectionReversed=""True"" Orientation=""Vertical"">
          <Track.DecreaseRepeatButton><RepeatButton Command=""ScrollBar.PageUpCommand"" Opacity=""0"" Focusable=""False""/></Track.DecreaseRepeatButton>
          <Track.IncreaseRepeatButton><RepeatButton Command=""ScrollBar.PageDownCommand"" Opacity=""0"" Focusable=""False""/></Track.IncreaseRepeatButton>
          <Track.Thumb><Thumb Focusable=""False""><Thumb.Template><ControlTemplate TargetType=""Thumb"">
            <Border Background=""#3C4460"" BorderBrush=""#5A6079"" BorderThickness=""1"" CornerRadius=""2"" Margin=""2,1,2,1""/>
          </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
        </Track>
      </Border>
    </ControlTemplate>
  </Setter.Value></Setter>
</Style>");

        public static string ListItemStyle => Themed(@"<Style TargetType=""ListBoxItem"">
  <Setter Property=""HorizontalContentAlignment"" Value=""Stretch""/>
  <Setter Property=""Padding"" Value=""3,0,3,0""/>
  <Setter Property=""Template""><Setter.Value>
    <ControlTemplate TargetType=""ListBoxItem"">
      <Border x:Name=""Bd"" Background=""Transparent"" Padding=""{TemplateBinding Padding}"">
        <ContentPresenter HorizontalAlignment=""Stretch""/>
      </Border>
      <ControlTemplate.Triggers>
        <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""Bd"" Property=""Background"" Value=""#101828""/></Trigger>
        <Trigger Property=""IsSelected"" Value=""True""><Setter TargetName=""Bd"" Property=""Background"" Value=""#1A2F80""/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate>
  </Setter.Value></Setter>
</Style>");

        static string Themed(string x)
        {
            var t = Theme.Current;
            var bg = Theme.C(t.ListBg);
            return x.Replace("#0B0D12", Theme.Hex(Theme.Mix(bg, Theme.C(t.Face2), 0.35)))
                    .Replace("#1A1D26", Theme.Hex(Theme.Mix(bg, Theme.C(t.Face2), 0.6)))
                    .Replace("#3C4460", t.Button2)
                    .Replace("#5A6079", t.BevelLight)
                    .Replace("#101828", Theme.Hex(Theme.Mix(bg, Theme.C(t.ListSelect), 0.45)))
                    .Replace("#1A2F80", t.ListSelect);
        }

        public const string PlaylistItemTemplate = @"<DataTemplate>
  <Grid>
    <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
    <TextBlock Text=""{Binding Display}"" Foreground=""{Binding ForeBrush}"" TextTrimming=""CharacterEllipsis""/>
    <TextBlock Grid.Column=""1"" Text=""{Binding DurationText}"" Foreground=""{Binding ForeBrush}"" Margin=""8,0,0,0""/>
  </Grid>
</DataTemplate>";

        public static string ResultItemTemplate => @"<DataTemplate>
  <Grid>
    <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
    <TextBlock Text=""{Binding DisplayTitle}"" Foreground=""" + Theme.Current.ListText + @""" TextTrimming=""CharacterEllipsis""/>
    <TextBlock Grid.Column=""1"" Text=""{Binding DurationText}"" Foreground=""" + Theme.Current.ListText + @""" Margin=""8,0,0,0""/>
  </Grid>
</DataTemplate>";
    }
}
