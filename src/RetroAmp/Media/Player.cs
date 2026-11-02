using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RetroAmp.Audio;
using RetroAmp.Core;

namespace RetroAmp.Media
{
    public enum PlayState { Stopped, Playing, Paused, Loading }
    public enum RepeatMode { Off = 0, All = 1, One = 2 }

    /// <summary>The playback brain: playlist navigation, backend selection, online resolving, crossfade.</summary>
    public sealed class Player : IDisposable
    {
        public readonly ObservableCollection<PlaylistItem> Items = new ObservableCollection<PlaylistItem>();
        public readonly AudioEngine Audio = new AudioEngine();
        public readonly MediaBackend Media = new MediaBackend();
        public readonly Settings Settings;

        public PlaylistItem Current { get; private set; }
        public PlayState State { get; private set; } = PlayState.Stopped;
        public bool UsingMedia { get; private set; }
        public int InfoKbps { get; private set; }
        public int InfoKhz { get; private set; }
        public int InfoChannels { get; private set; }

        public string StatusText { get; private set; }
        DateTime statusUntil;

        public event Action TrackChanged;
        public event Action StateChanged;
        public event Action VideoStarted;
        /// <summary>UI hook that makes sure yt-dlp exists (asks the user, downloads it). Returns false if unavailable.</summary>
        public Func<Task<bool>> EnsureYtDlp;

        readonly List<PlaylistItem> history = new List<PlaylistItem>();
        readonly Random rnd = new Random();
        CancellationTokenSource loadCts;
        int token;
        int consecutiveFailures;
        bool crossfadeStarted;

        public Player(Settings settings)
        {
            Settings = settings;
            Items.CollectionChanged += (s, e) => Renumber();
            Audio.TrackEnded += OnTrackEnded;
            Audio.OutputError += msg => ShowStatus("Audio device error: " + msg, 6);
            Media.Ended += () => { if (UsingMedia) OnTrackEnded(); };
            Media.Failed += msg => { if (UsingMedia) Fail(Current, msg); };
            Media.Opened += () =>
            {
                if (!UsingMedia) return;
                if (Current != null && Media.Duration > TimeSpan.Zero) Current.Duration = Media.Duration;
                if (Media.HasVideo) VideoStarted?.Invoke();
                StateChanged?.Invoke();
            };
            ApplyVolume();
            Audio.DeviceNumber = settings.AudioDevice;
        }

        void Renumber()
        {
            for (int i = 0; i < Items.Count; i++) Items[i].Index = i + 1;
        }

        public void ShowStatus(string text, double seconds = 4)
        {
            StatusText = text;
            statusUntil = DateTime.Now.AddSeconds(seconds);
        }

        public string ActiveStatus => StatusText != null && DateTime.Now < statusUntil ? StatusText : null;

        // ---------------------------------------------------------------- volume / dsp
        public void ApplyVolume()
        {
            Audio.Dsp.Volume = (float)Settings.Volume;
            Audio.Dsp.Balance = (float)Settings.Balance;
            Media.Volume = Settings.Volume * Settings.Volume;
            Media.Balance = Settings.Balance;
        }

        public void ApplyDsp()
        {
            var d = Audio.Dsp;
            d.EqEnabled = Settings.EqEnabled;
            d.AutoGain = Settings.EqAutoGain;
            d.PreampDb = Settings.Preamp;
            for (int i = 0; i < 10; i++) d.SetBand(i, Settings.EqBands[i]);
            d.StereoWidth = (float)Settings.StereoWidth;
            d.ReverbAmount = (float)Settings.Reverb;
            d.VocalRemover = Settings.VocalRemover;
        }

        // ---------------------------------------------------------------- position
        public TimeSpan Position => UsingMedia ? Media.Position : Audio.Position;
        public TimeSpan Duration
        {
            get
            {
                var d = UsingMedia ? Media.Duration : Audio.Duration;
                if (d <= TimeSpan.Zero && Current != null) d = Current.Duration;
                return d;
            }
        }

        public void Seek(TimeSpan t)
        {
            if (State == PlayState.Stopped || State == PlayState.Loading) return;
            if (UsingMedia) Media.Position = t; else Audio.Seek(t);
            crossfadeStarted = false;
        }

        // ---------------------------------------------------------------- transport
        public void TogglePlayPause()
        {
            if (State == PlayState.Playing) Pause();
            else if (State == PlayState.Paused) Resume();
            else if (State == PlayState.Stopped) PlayCurrentOrFirst();
        }

        public void PlayCurrentOrFirst()
        {
            if (State == PlayState.Paused) { Resume(); return; }
            var it = Current != null && Items.Contains(Current) ? Current : Items.FirstOrDefault();
            if (it != null) Play(it);
        }

        public void Pause()
        {
            if (State != PlayState.Playing) return;
            if (UsingMedia) Media.Pause(); else Audio.Pause();
            SetState(PlayState.Paused);
        }

        public void Resume()
        {
            if (State != PlayState.Paused) return;
            if (UsingMedia) Media.Resume(); else Audio.Resume();
            SetState(PlayState.Playing);
        }

        public void Stop()
        {
            loadCts?.Cancel();
            token++;
            Audio.Stop();
            Media.Close();
            LoopbackTap.Stop();
            UsingMedia = false;
            SetState(PlayState.Stopped);
        }

        public void Next() => Advance(false);
        public void Previous()
        {
            if (State != PlayState.Stopped && Position.TotalSeconds > 4) { Seek(TimeSpan.Zero); return; }
            if (Items.Count == 0) return;
            PlaylistItem target;
            if (Settings.Shuffle && history.Count > 1)
            {
                history.RemoveAt(history.Count - 1);
                target = history[history.Count - 1];
                history.RemoveAt(history.Count - 1);
                if (!Items.Contains(target)) target = Items[0];
            }
            else
            {
                int i = Current == null ? 0 : Items.IndexOf(Current) - 1;
                if (i < 0) i = Items.Count - 1;
                target = Items[i];
            }
            Play(target);
        }

        void Advance(bool auto)
        {
            int idx = NextIndex(auto);
            if (idx < 0) { Stop(); return; }
            Play(Items[idx], auto);
        }

        int NextIndex(bool auto)
        {
            if (Items.Count == 0) return -1;
            int cur = Current == null ? -1 : Items.IndexOf(Current);
            if (Settings.Shuffle)
            {
                if (Items.Count == 1) return auto && (RepeatMode)Settings.Repeat == RepeatMode.Off ? -1 : 0;
                var recent = new HashSet<PlaylistItem>(history.Skip(Math.Max(0, history.Count - Math.Min(history.Count, Items.Count / 2))));
                var pool = Items.Where(x => x != Current && !recent.Contains(x) && !x.Failed).ToList();
                if (pool.Count == 0) pool = Items.Where(x => x != Current).ToList();
                return Items.IndexOf(pool[rnd.Next(pool.Count)]);
            }
            int next = cur + 1;
            if (next >= Items.Count)
            {
                if (auto && (RepeatMode)Settings.Repeat == RepeatMode.Off) return -1;
                next = 0;
            }
            return next;
        }

        void OnTrackEnded()
        {
            if (State == PlayState.Stopped) return;
            if ((RepeatMode)Settings.Repeat == RepeatMode.One && Current != null) { Play(Current, true); return; }
            Advance(true);
        }

        void SetState(PlayState s)
        {
            if (State == s) return;
            State = s;
            StateChanged?.Invoke();
        }

        public void MarkCurrent(PlaylistItem item) => SetCurrent(item);

        void SetCurrent(PlaylistItem item)
        {
            if (Current != null) Current.IsCurrent = false;
            Current = item;
            if (item != null) item.IsCurrent = true;
            if (item != null && (history.Count == 0 || history[history.Count - 1] != item))
            {
                history.Add(item);
                if (history.Count > 500) history.RemoveAt(0);
            }
            TrackChanged?.Invoke();
        }

        /// <summary>Plays an item. auto = triggered by end-of-track (allows crossfade).</summary>
        public async void Play(PlaylistItem item, bool auto = false)
        {
            if (item == null) return;
            loadCts?.Cancel();
            loadCts = new CancellationTokenSource();
            var ct = loadCts.Token;
            int my = ++token;
            bool Wanted() => my == token;

            bool canCrossfade = auto && !UsingMedia && Audio.HasTrack && State == PlayState.Playing && Settings.CrossfadeSeconds > 0.05;
            double xf = canCrossfade ? Settings.CrossfadeSeconds : 0;
            crossfadeStarted = false;
            SetCurrent(item);
            if (xf <= 0) SetState(PlayState.Loading); else StateChanged?.Invoke();
            InfoKbps = 0; InfoKhz = 0; InfoChannels = 0;

            try
            {
                if (item.IsVideo)
                {
                    Uri uri;
                    if (item.Kind == ItemKind.OnlineStream)
                    {
                        if (!await RequireYtDlp()) throw new Exception("yt-dlp is required for online videos.");
                        ShowStatus("Getting video stream...", 30);
                        uri = new Uri(await YtDlp.GetVideoUrlAsync(item.Location, ct));
                    }
                    else uri = ToUri(item.Location);
                    if (!Wanted()) return;
                    StartMedia(uri);
                }
                else
                {
                    string src = item.Location;
                    if (item.Kind == ItemKind.OnlineStream)
                    {
                        if (!await RequireYtDlp()) throw new Exception("yt-dlp is required for YouTube / SoundCloud.");
                        if (!Wanted()) return;
                        ShowStatus("Connecting...", 60);
                        var prog = new Progress<double>(p => { if (Wanted()) ShowStatus("Buffering " + (int)(p * 100) + "%", 30); });
                        src = await YtDlp.FetchAudioAsync(item, prog, ct);
                        if (!Wanted()) return;
                        ShowStatus("", 0);
                    }
                    else if (item.Kind == ItemKind.LocalAudio && !File.Exists(src))
                        throw new FileNotFoundException("File not found: " + src);

                    bool ok = false;
                    Exception audioErr = null;
                    try
                    {
                        if (item.Kind == ItemKind.DirectUrl) ShowStatus("Connecting...", 20);
                        ok = await Audio.PlayAsync(src, xf, Wanted);
                        if (!ok) return;
                    }
                    catch (Exception ex) { audioErr = ex; }

                    if (!Wanted()) return;
                    if (ok)
                    {
                        Media.Close();
                        LoopbackTap.Stop();
                        UsingMedia = false;
                        var deck = Audio.Current;
                        if (deck != null)
                        {
                            InfoKhz = (int)Math.Round(deck.SourceSampleRate / 1000.0);
                            InfoChannels = deck.SourceChannels;
                            if (item.Duration <= TimeSpan.Zero && deck.Duration > TimeSpan.Zero) item.Duration = deck.Duration;
                        }
                        InfoKbps = item.Bitrate > 0 ? item.Bitrate : EstimateKbps(src, Audio.Duration);
                        ShowStatus("", 0);
                    }
                    else
                    {
                        AppPaths.Log("Audio engine could not open '" + src + "': " + audioErr?.Message + " — falling back to Windows Media.");
                        if (item.Kind == ItemKind.DirectUrl && YtDlp.Available)
                        {
                            try
                            {
                                string f = await YtDlp.FetchAudioAsync(item, null, ct);
                                if (!Wanted()) return;
                                if (await Audio.PlayAsync(f, 0, Wanted)) { UsingMedia = false; Media.Close(); LoopbackTap.Stop(); goto started; }
                            }
                            catch (OperationCanceledException) { throw; }
                            catch { }
                            if (!Wanted()) return;
                        }
                        StartMedia(ToUri(src));
                    }
                }
            started:
                consecutiveFailures = 0;
                item.Failed = false;
                SetState(PlayState.Playing);
                StateChanged?.Invoke();
                Prefetch();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!Wanted()) return;
                Fail(item, ex.Message);
            }
        }

        void Fail(PlaylistItem item, string message)
        {
            AppPaths.Log("Playback failed for " + item?.Location + ": " + message);
            if (item != null) item.Failed = true;
            ShowStatus("Error: " + message, 8);
            consecutiveFailures++;
            Audio.Stop();
            Media.Close();
            UsingMedia = false;
            SetState(PlayState.Stopped);
            if (consecutiveFailures < Math.Min(Items.Count, 5)) Advance(true);
            else consecutiveFailures = 0;
        }

        void StartMedia(Uri uri)
        {
            Audio.Stop();
            UsingMedia = true;
            Media.Open(uri);
            ApplyVolume();
            LoopbackTap.Start();
            if (Current != null && Current.IsVideo) VideoStarted?.Invoke();
        }

        static Uri ToUri(string loc) => Util.IsUrl(loc) ? new Uri(loc) : new Uri(Path.GetFullPath(loc));

        static int EstimateKbps(string path, TimeSpan dur)
        {
            try
            {
                if (Util.IsUrl(path) || dur.TotalSeconds < 1) return 0;
                return (int)(new FileInfo(path).Length * 8 / dur.TotalSeconds / 1000);
            }
            catch { return 0; }
        }

        async Task<bool> RequireYtDlp()
        {
            if (YtDlp.Available) return true;
            if (EnsureYtDlp == null) return false;
            return await EnsureYtDlp();
        }

        void Prefetch()
        {
            if (Settings.Shuffle || Current == null) return;
            int i = Items.IndexOf(Current) + 1;
            if (i <= 0 || i >= Items.Count) return;
            var next = Items[i];
            if (next.Kind != ItemKind.OnlineStream || next.IsVideo || !YtDlp.Available) return;
            if (next.CachedFile != null && File.Exists(next.CachedFile)) return;
            string keep = Current.CachedFile;
            Task.Run(async () =>
            {
                try { await YtDlp.FetchAudioAsync(next, null, CancellationToken.None); } catch { }
                YtDlp.TrimCache(Settings.CacheLimitMb, keep);
            });
        }

        /// <summary>Called periodically by the UI (≈10 Hz). Handles crossfade timing.</summary>
        public void Tick()
        {
            if (crossfadeStarted || UsingMedia || State != PlayState.Playing) return;
            double xf = Settings.CrossfadeSeconds;
            if (xf < 0.05 || (RepeatMode)Settings.Repeat == RepeatMode.One) return;
            var dur = Audio.Duration;
            if (dur.TotalSeconds < xf * 3) return;
            if ((dur - Audio.Position).TotalSeconds > xf) return;
            int idx = NextIndex(true);
            if (idx < 0) return;
            var next = Items[idx];
            bool ready = next.Kind == ItemKind.LocalAudio || (next.CachedFile != null && File.Exists(next.CachedFile));
            if (!ready || next.IsVideo) return;
            crossfadeStarted = true;
            Play(next, true);
        }

        // ---------------------------------------------------------------- playlist editing
        public void AddRange(IEnumerable<PlaylistItem> items)
        {
            foreach (var it in items)
            {
                Items.Add(it);
                TagLoader.Enqueue(it);
            }
        }

        public List<PlaylistItem> ExpandPaths(IEnumerable<string> paths)
        {
            var list = new List<PlaylistItem>();
            foreach (var p in paths)
            {
                try
                {
                    if (Directory.Exists(p)) list.AddRange(Util.EnumerateMedia(p).Select(f => new PlaylistItem(f)));
                    else if (Util.PlaylistExts.Contains(Util.SafeExt(p)) && File.Exists(p)) list.AddRange(PlaylistIO.Load(p));
                    else if (Util.IsUrl(p) || File.Exists(p)) list.Add(new PlaylistItem(p));
                }
                catch (Exception ex) { AppPaths.Log("Add failed: " + ex.Message); }
            }
            return list;
        }

        public void Remove(IEnumerable<PlaylistItem> items)
        {
            foreach (var it in items.ToList())
            {
                if (it == Current && State != PlayState.Stopped) continue;
                Items.Remove(it);
            }
        }

        public void Dispose()
        {
            loadCts?.Cancel();
            LoopbackTap.Stop();
            Media.Close();
            Audio.Dispose();
        }
    }
}
