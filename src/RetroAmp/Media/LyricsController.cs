using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RetroAmp.Core;

namespace RetroAmp.Media
{
    /// <summary>Keeps lyrics for the current track and maps the play position onto lines.</summary>
    public sealed class LyricsController
    {
        readonly Player player;
        CancellationTokenSource cts;
        PlaylistItem loadedFor;
        public Lyrics Current { get; private set; }
        public string Status { get; private set; } = "";
        public bool Busy { get; private set; }
        public event Action Changed;
        /// <summary>Set by the UI: true when some lyrics view is visible (enables auto-loading).</summary>
        public Func<bool> Wanted = () => false;

        public LyricsController(Player p)
        {
            player = p;
            p.TrackChanged += () => { if (player.Current != loadedFor) { Current = null; Status = ""; Changed?.Invoke(); } MaybeLoad(); };
            p.StateChanged += MaybeLoad;
        }

        void MaybeLoad()
        {
            var it = player.Current;
            if (it == null || it == loadedFor) return;
            if (player.State != PlayState.Playing) return; // duration is known once playing
            if (!player.Settings.LyricsAuto && !Wanted()) return;
            _ = Load(it, false);
        }

        public void EnsureLoaded()
        {
            var it = player.Current;
            if (it != null && it != loadedFor) _ = Load(it, false);
        }

        public async Task Load(PlaylistItem item, bool force)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            var ct = cts.Token;
            loadedFor = item;
            Current = null; Busy = true; Status = "Searching lyrics…"; Changed?.Invoke();
            try
            {
                var dur = player.Current == item ? player.Duration : item.Duration;
                var ly = await LyricsService.FindAsync(item, dur, force, ct);
                if (ct.IsCancellationRequested || loadedFor != item) return;
                Current = ly != null && !ly.IsEmpty ? ly : null;
                Status = Current == null ? "No lyrics found" : (Current.Synced ? "" : "Lyrics (not synced)");
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { Status = "Lyrics error: " + ex.Message; AppPaths.Log("Lyrics: " + ex); }
            finally { if (loadedFor == item) Busy = false; }
            Changed?.Invoke();
        }

        public void SetLyrics(PlaylistItem item, Lyrics ly)
        {
            cts?.Cancel();
            loadedFor = item;
            Current = ly; Busy = false;
            Status = ly == null ? "No lyrics found" : "";
            Changed?.Invoke();
        }

        /// <summary>Audible position adjusted for output latency and the per-song offset.</summary>
        public TimeSpan Position
        {
            get
            {
                var t = player.Position;
                if (!player.UsingMedia) t -= TimeSpan.FromMilliseconds(170);
                if (Current != null) t += TimeSpan.FromMilliseconds(Current.OffsetMs);
                return t;
            }
        }

        public void AdjustOffset(int ms)
        {
            if (Current == null || player.Current == null) return;
            Current.OffsetMs += ms;
            LyricsService.Save(player.Current, Current);
            player.ShowStatus("Lyrics offset " + (Current.OffsetMs >= 0 ? "+" : "") + (Current.OffsetMs / 1000.0).ToString("0.0") + " s", 1.5);
            Changed?.Invoke();
        }

        public void LoadFile(string path)
        {
            if (player.Current == null) return;
            var ly = Lyrics.Parse(File.ReadAllText(path), "file");
            LyricsService.Save(player.Current, ly);
            SetLyrics(player.Current, ly);
        }
    }
}
