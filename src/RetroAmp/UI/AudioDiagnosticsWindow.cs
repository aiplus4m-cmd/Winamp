using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RetroAmp.Media;

namespace RetroAmp.UI
{
    /// <summary>Live view of the audio path: helps tell crackles from clipping from loud speakers.</summary>
    public sealed class AudioDiagnosticsWindow : Window
    {
        readonly Player player;
        readonly TextBlock text;
        readonly DispatcherTimer timer;

        public AudioDiagnosticsWindow(Player p)
        {
            player = p;
            DialogStyle.Apply(this, "Audio diagnostics");
            var root = new StackPanel { Margin = new Thickness(14), Width = 470 };
            text = new TextBlock { FontFamily = Theme.LcdFont, FontSize = 12, Foreground = Theme.LcdText, Background = Theme.LcdBg, Padding = new Thickness(10), TextWrapping = TextWrapping.Wrap };
            root.Children.Add(text);
            root.Children.Add(DialogStyle.Text(
                "Glitches > 0 while music plays = the audio thread was late (crackles) → try closing the visualizer / lyrics, or Compatibility mode.\n" +
                "Limiter < 0 dB = the song + EQ was too loud and was gently turned down (no distortion).\n" +
                "If both stay at 0 and you still hear distortion, the sound is clean when it leaves RetroAmp: it is the speaker or a Windows sound enhancement.", 11));
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            row.Children.Add(DialogStyle.Button("RESET", 70, () => p.Audio.Dsp.ResetDiagnostics()));
            row.Children.Add(DialogStyle.Button("COPY", 70, () => { try { Clipboard.SetText(text.Text); } catch { } }));
            row.Children.Add(DialogStyle.Button("CLOSE", 70, Close));
            root.Children.Add(row);
            Content = root;
            ShowInTaskbar = true;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (o, e) => Refresh();
            timer.Start();
            Closed += (o, e) => timer.Stop();
            p.Audio.Dsp.ResetDiagnostics();
            Refresh();
        }

        static string Db(double lin) => lin <= 1e-6 ? "-inf" : (20 * Math.Log10(lin)).ToString("0.0");

        void Refresh()
        {
            var a = player.Audio;
            var d = a.Dsp;
            var deck = a.Current;
            text.Text =
                "Output     : " + a.OutputDescription + "\n" +
                "Source     : " + (player.UsingMedia ? "Windows Media (video / fallback)" : deck == null ? "-" : deck.SourceSampleRate + " Hz, " + deck.SourceChannels + " ch") + "\n" +
                "Volume     : " + (int)(player.Settings.Volume * 100) + "%  (= " + Db(player.Settings.Volume * player.Settings.Volume) + " dB)\n" +
                "EQ / AUTO  : " + (d.EqEnabled ? "on" : "off") + ", auto gain " + d.AutoGainDb.ToString("0.0") + " dB\n" +
                "Peak out   : " + Db(d.PeakOut) + " dBFS\n" +
                "Limiter    : " + Db(d.MinLimiterGain) + " dB (deepest)\n" +
                "Glitches   : " + d.Glitches + "\n" +
                "DSP time   : " + d.MaxProcessMs.ToString("0.0") + " ms (slowest block)";
        }
    }
}
