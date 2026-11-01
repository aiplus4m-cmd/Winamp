using System;
using System.Windows.Media;

namespace RetroAmp.Media
{
    /// <summary>Windows Media Foundation playback through WPF's MediaPlayer — used for video and as a fallback for audio.</summary>
    public sealed class MediaBackend
    {
        public readonly MediaPlayer Player = new MediaPlayer();
        public event Action Opened;
        public event Action Ended;
        public event Action<string> Failed;
        public bool IsOpen { get; private set; }
        public bool IsPaused { get; private set; }

        public MediaBackend()
        {
            Player.MediaOpened += (s, e) => Opened?.Invoke();
            Player.MediaEnded += (s, e) => Ended?.Invoke();
            Player.MediaFailed += (s, e) => Failed?.Invoke(e.ErrorException?.Message ?? "Media failed");
            Player.ScrubbingEnabled = true;
        }

        public void Open(Uri uri)
        {
            Player.Open(uri);
            Player.Play();
            IsOpen = true; IsPaused = false;
        }

        public void Pause() { if (IsOpen) { Player.Pause(); IsPaused = true; } }
        public void Resume() { if (IsOpen) { Player.Play(); IsPaused = false; } }

        public void Close()
        {
            if (!IsOpen) return;
            try { Player.Stop(); Player.Close(); } catch { }
            IsOpen = false; IsPaused = false;
        }

        public bool HasVideo => IsOpen && Player.HasVideo;
        public TimeSpan Position
        {
            get => IsOpen ? Player.Position : TimeSpan.Zero;
            set { if (IsOpen) Player.Position = value; }
        }
        public TimeSpan Duration => IsOpen && Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan : TimeSpan.Zero;
        public double Volume { set => Player.Volume = value; }
        public double Balance { set => Player.Balance = value; }
    }
}
