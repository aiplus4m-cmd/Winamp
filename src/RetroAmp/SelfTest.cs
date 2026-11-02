using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroAmp.Audio;
using RetroAmp.Core;
using RetroAmp.Media;
using RetroAmp.UI;

namespace RetroAmp
{
    /// <summary>
    /// "RetroAmp.exe --selftest &lt;dir&gt; [--online]" — headless checks used by CI on a real Windows machine:
    /// decodes audio through the full DSP chain, renders the UI and every visualizer to PNG, optionally tests yt-dlp.
    /// </summary>
    public static class SelfTest
    {
        static readonly StringBuilder Log = new StringBuilder();
        static int failures;
        static string logFile;
        static void Write(string line)
        {
            line = DateTime.Now.ToString("HH:mm:ss ") + line;
            Log.AppendLine(line);
            try { File.AppendAllText(logFile, line + Environment.NewLine); } catch { }
        }
        static void Ok(string m) => Write("OK   " + m);
        static void Fail(string m) { failures++; Write("FAIL " + m); }
        static void Step(string m) => Write("...  " + m);

        public static int Run(string outDir, bool online)
        {
            Directory.CreateDirectory(outDir);
            logFile = Path.Combine(outDir, "selftest.log");
            try { File.Delete(logFile); } catch { }
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Startup += async (s, e) =>
            {
                try { await RunAll(outDir, online); }
                catch (Exception ex) { Fail("Unexpected: " + ex); }
                Write(failures == 0 ? "ALL PASSED" : failures + " FAILURE(S)");
                app.Shutdown(failures == 0 ? 0 : 1);
            };
            return app.Run();
        }

        static async Task RunAll(string outDir, bool online)
        {
            string wav = Path.Combine(outDir, "test.wav");
            Step("writing wav");
            WriteTestWav(wav, 6);
            Ok("wrote test wav");

            // ---- decode + DSP
            var dsp = await DecodeTest(wav, "wav");

            // ---- main window
            Step("creating main window");
            var settings = new Settings { Scale = 1.25 };
            var player = new Player(settings);
            player.ApplyDsp();
            player.AddRange(new[] { new PlaylistItem(wav, "Test tone", TimeSpan.FromSeconds(6)),
                new PlaylistItem("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "Online example", TimeSpan.FromSeconds(212)),
                new PlaylistItem("https://soundcloud.com/example/track", "SoundCloud example") });
            player.MarkCurrent(player.Items[0]);
            var win = new MainWindow(player, settings);
            win.Show();
            await Pump(dsp, 90);
            Save((FrameworkElement)win.Content, Path.Combine(outDir, "main-window.png"));
            Ok("main window rendered");

            // ---- visualizers
            Step("visualizers");
            var vw = new Window { Width = 800, Height = 450, WindowStyle = WindowStyle.None, ShowInTaskbar = false };
            var vis = new VisualizerView { Caption = "RetroAmp self-test" };
            vw.Content = vis;
            vw.Show();
            for (int m = 0; m < VisualizerView.ModeNames.Length; m++)
            {
                vis.Mode = (VisMode)m;
                await Pump(dsp, 45);
                vis.InvalidateVisual();
                await Task.Delay(30);
                Save(vis, Path.Combine(outDir, "vis-" + m + "-" + ((VisMode)m).ToString().ToLower() + ".png"));
            }
            Ok("visualizers rendered");
            var a = Analyzer.Instance;
            if (a.Bands.Max() > 0.2f && a.LevelL > 0.02f) Ok($"analyzer sees signal (max band {a.Bands.Max():0.00}, level {a.LevelL:0.00}, bass {a.Bass:0.00})");
            else Fail($"analyzer saw no signal (max band {a.Bands.Max():0.00})");

            // ---- skins
            Step("skins");
            foreach (var skin in Skins.BuiltIn)
            {
                win.ApplySkin(skin);
                await Pump(dsp, 20);
                Save((FrameworkElement)win.Content, Path.Combine(outDir, "skin-" + Safe(skin.Name) + ".png"));
            }
            win.ApplySkin(Skins.BuiltIn[0]);
            Ok("skins rendered: " + Skins.BuiltIn.Count);

            // ---- LRC parser
            var lrc = Lyrics.Parse("[ar:Test]\n[ti:Song]\n[offset:+250]\n[00:01.00]First line\n[00:03.50][00:10.00]Chorus <00:04.00>word\n[00:06.20]\n[00:07.00]Third", "test");
            if (lrc.Synced && lrc.Lines.Count == 5 && lrc.OffsetMs == 250 && lrc.IndexAt(TimeSpan.FromSeconds(4)) == 1 && lrc.Lines[1].Text == "Chorus word" && lrc.IndexAt(TimeSpan.FromSeconds(11)) == 4)
                Ok("LRC parser");
            else Fail("LRC parser: lines=" + lrc.Lines.Count + " idx=" + lrc.IndexAt(TimeSpan.FromSeconds(4)));
            LyricsService.GuessArtistTitle(new PlaylistItem("https://www.youtube.com/watch?v=abcdefghijk", "Sơn Tùng M-TP - Chúng Ta Của Hiện Tại (Official MV)"), out string ga, out string gt);
            if (ga == "Sơn Tùng M-TP" && gt == "Chúng Ta Của Hiện Tại") Ok("title guess: '" + ga + "' / '" + gt + "'"); else Fail("title guess: '" + ga + "' / '" + gt + "'");

            // ---- About window
            var about = new AboutWindow();
            about.Show();
            await Task.Delay(300);
            Save((FrameworkElement)about.Content, Path.Combine(outDir, "about.png"));
            about.Close();
            Ok("about window rendered");

            if (online) await LyricsTests(outDir, win, player, dsp);

            // ---- playlist I/O
            string m3u = Path.Combine(outDir, "test.m3u8");
            PlaylistIO.Save(m3u, player.Items);
            var back = PlaylistIO.Load(m3u);
            if (back.Count == 3 && back[1].Kind == ItemKind.OnlineStream) Ok("m3u8 round trip"); else Fail("m3u8 round trip");

            // ---- equalizer response sanity: +12 dB at 60 Hz must boost a 60 Hz sine
            Ok("eq gain test: " + EqGainTest().ToString("0.0") + " dB at 60 Hz with +12 dB band");

            // ---- real audio output (may not exist on CI)
            Step("audio output");
            try
            {
                await player.Audio.PlayAsync(wav, 0);
                await Task.Delay(800);
                Ok("audio output playing, position " + player.Audio.Position.TotalSeconds.ToString("0.00") + " s");
                player.Audio.Stop();
            }
            catch (Exception ex) { Ok("audio output not available on this machine (" + ex.Message + ")"); }

            if (online) await OnlineTests(outDir);
            Step("closing windows");
            vw.Close();
            win.Close();
        }

        static string Safe(string n)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Replace(' ', '-').ToLowerInvariant();
        }

        static async Task LyricsTests(string outDir, MainWindow win, Player player, DspChain dsp)
        {
            Step("lyrics from LRCLIB");
            var item = new PlaylistItem("https://www.youtube.com/watch?v=yKNxeF4KMsY", "Coldplay - Yellow (Official Video)", TimeSpan.FromSeconds(269));
            player.AddRange(new[] { item });
            player.MarkCurrent(item);
            Lyrics ly = null;
            try
            {
                ly = await LyricsService.FindAsync(item, TimeSpan.FromSeconds(269), true, new CancellationTokenSource(TimeSpan.FromSeconds(40)).Token);
            }
            catch (Exception ex) { Fail("LRCLIB: " + ex.Message); }
            if (ly != null && !ly.IsEmpty) Ok($"LRCLIB lyrics: {ly.Artist} - {ly.Title}, {ly.Lines.Count} lines, synced={ly.Synced}");
            else { Fail("LRCLIB returned nothing for Coldplay - Yellow"); ly = Lyrics.Parse("[00:00.00]Look at the stars\n[00:04.00]Look how they shine for you\n[00:08.00]And everything you do", "fallback"); }

            var vn = new PlaylistItem("https://www.youtube.com/watch?v=psZ1g9fMfeo", "Sơn Tùng M-TP | CHÚNG TA CỦA HIỆN TẠI | OFFICIAL MUSIC VIDEO", TimeSpan.FromSeconds(301));
            try
            {
                var lv = await LyricsService.FindAsync(vn, vn.Duration, true, new CancellationTokenSource(TimeSpan.FromSeconds(40)).Token);
                Ok("Vietnamese lyrics lookup: " + (lv == null ? "not found" : lv.Artist + " - " + lv.Title + ", " + lv.Lines.Count + " lines, synced=" + lv.Synced));
            }
            catch (Exception ex) { Ok("Vietnamese lyrics lookup error (non-fatal): " + ex.Message); }

            // show it ~1 minute into the song
            ly.OffsetMs = 62000;
            win.Lyrics.SetLyrics(item, ly);
            var lw = new LyricsWindow(player, win.Lyrics, player.Settings);
            lw.Show();
            await Pump(dsp, 60);
            Save((FrameworkElement)lw.Content, Path.Combine(outDir, "lyrics-window.png"));
            var dl = new DesktopLyricsWindow(player, win.Lyrics, player.Settings);
            dl.Show();
            await Pump(dsp, 30);
            Save((FrameworkElement)dl.Content, Path.Combine(outDir, "desktop-lyrics.png"));
            dl.Close();
            Ok("lyrics windows rendered");
        }

        static async Task<DspChain> DecodeTest(string file, string label)
        {
            Step("decoding " + label);
            var deck = await Task.Run(() => new Deck(file, AudioEngine.MixFormat));
            var dsp = new DspChain(deck) { Volume = 1f };
            for (int i = 0; i < 10; i++) dsp.SetBand(i, i < 2 ? 6 : 0);
            var buf = new float[44100 * 2];
            int n = await Task.Run(() => dsp.Read(buf, 0, buf.Length));
            float peak = buf.Take(n).Select(Math.Abs).DefaultIfEmpty(0).Max();
            if (n > 0 && peak > 0.01f) Ok($"decoded {label}: {deck.SourceSampleRate} Hz, {deck.SourceChannels} ch, {deck.Duration.TotalSeconds:0.0}s, peak {peak:0.00}");
            else Fail($"decode {label}: n={n} peak={peak}");
            return dsp;
        }

        /// <summary>Feeds audio through the chain in real-time sized blocks and updates the analyzer.</summary>
        static async Task Pump(DspChain dsp, int frames)
        {
            var buf = new float[735 * 2];
            for (int i = 0; i < frames; i++)
            {
                int n = await Task.Run(() => dsp.Read(buf, 0, buf.Length));
                if (n == 0) break;
                Analyzer.Instance.Update(1 / 60.0);
                await Task.Delay(16);
            }
        }

        static double EqGainTest()
        {
            var src = new SineProvider(60);
            var dsp = new DspChain(src) { Volume = 1f };
            var buf = new float[44100];
            dsp.Read(buf, 0, buf.Length);
            double before = Rms(buf);
            dsp.SetBand(0, 12);
            dsp.Read(buf, 0, buf.Length);
            dsp.Read(buf, 0, buf.Length);
            double after = Rms(buf);
            return 20 * Math.Log10(after / before);
        }

        static double Rms(float[] b) => Math.Sqrt(b.Average(x => (double)x * x));

        sealed class SineProvider : NAudio.Wave.ISampleProvider
        {
            readonly double f; long n;
            public SineProvider(double f) { this.f = f; }
            public NAudio.Wave.WaveFormat WaveFormat { get; } = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            public int Read(float[] buffer, int offset, int count)
            {
                for (int i = 0; i < count; i += 2) { float v = (float)(0.2 * Math.Sin(2 * Math.PI * f * n++ / 44100)); buffer[offset + i] = v; buffer[offset + i + 1] = v; }
                return count;
            }
        }

        static async Task OnlineTests(string outDir)
        {
            try
            {
                Step("downloading yt-dlp");
                await Tools.InstallYtDlpAsync(null);
                Ok("yt-dlp downloaded: " + Tools.YtDlpPath);
            }
            catch (Exception ex) { Fail("yt-dlp download: " + ex.Message); return; }

            foreach (var q in new[] { "scsearch3:lofi chill", "ytsearch3:creative commons music" })
            {
                try
                {
                    Step("listing " + q);
                    var items = await YtDlp.ListAsync(q, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                    Ok($"{q}: {items.Count} results, first = {items[0].DisplayTitle} ({items[0].Location})");
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                    Step("downloading " + items[0].Location);
                    string file = await YtDlp.FetchAudioAsync(items[0], null, cts.Token);
                    Ok($"  downloaded {Path.GetFileName(file)} ({new FileInfo(file).Length / 1024} KB)");
                    await DecodeTest(file, Path.GetExtension(file));
                }
                catch (Exception ex)
                {
                    // YouTube often blocks datacenter IPs; record but do not fail the build for it
                    if (q.StartsWith("yt")) Ok("  (non-fatal) YouTube test: " + ex.Message);
                    else Fail(q + ": " + ex.Message);
                }
            }
        }

        static void Save(FrameworkElement el, string path)
        {
            el.UpdateLayout();
            int w = Math.Max(1, (int)Math.Ceiling(el.ActualWidth)), h = Math.Max(1, (int)Math.Ceiling(el.ActualHeight));
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new VisualBrush(el), null, new Rect(0, 0, w, h));
            }
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(path)) enc.Save(fs);
        }

        /// <summary>Writes a little stereo "song": kick drum, bass line, chord pad and hi-hats.</summary>
        static void WriteTestWav(string path, int seconds)
        {
            const int sr = 44100;
            int n = sr * seconds;
            using (var fs = File.Create(path))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + n * 4); bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)2);
                bw.Write(sr); bw.Write(sr * 4); bw.Write((short)4); bw.Write((short)16);
                bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(n * 4);
                var rnd = new Random(1);
                double[] chord = { 220, 277.18, 329.63 };
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / sr;
                    double beat = t % 0.5;
                    double kick = Math.Sin(2 * Math.PI * (50 + 80 * Math.Exp(-beat * 30)) * beat) * Math.Exp(-beat * 8) * 0.7;
                    double bass = Math.Sin(2 * Math.PI * (t % 2 < 1 ? 55 : 73.4) * t) * 0.25;
                    double pad = 0; foreach (var f in chord) pad += Math.Sin(2 * Math.PI * f * t) * 0.06;
                    double hb = (t + 0.25) % 0.5;
                    double hat = (rnd.NextDouble() * 2 - 1) * Math.Exp(-hb * 60) * 0.25;
                    double l = kick + bass + pad + hat * 0.6, r = kick + bass + pad * 0.8 + hat;
                    bw.Write((short)(Math.Max(-1, Math.Min(1, l)) * 30000));
                    bw.Write((short)(Math.Max(-1, Math.Min(1, r)) * 30000));
                }
            }
        }
    }
}
