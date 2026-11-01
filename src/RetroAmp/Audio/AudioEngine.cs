using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using RetroAmp.Core;

namespace RetroAmp.Audio
{
    /// <summary>
    /// Persistent output device + mixer + DSP. Tracks are opened as decks; crossfades are done by
    /// fading the outgoing deck while the new one fades in.
    /// </summary>
    public sealed class AudioEngine : IDisposable
    {
        public static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        readonly SynchronizationContext ui;
        readonly Mixer mixer;
        public readonly DspChain Dsp;
        WaveOutEvent output;
        Deck current;
        int deviceNumber = -1;

        public event Action TrackEnded;
        public event Action<string> OutputError;

        public AudioEngine()
        {
            ui = SynchronizationContext.Current ?? new SynchronizationContext();
            mixer = new Mixer(MixFormat);
            mixer.DeckFinished += OnDeckFinished;
            Dsp = new DspChain(mixer);
        }

        public bool HasTrack => current != null;
        public bool IsPaused { get; private set; }
        public Deck Current => current;
        public TimeSpan Position => current?.Position ?? TimeSpan.Zero;
        public TimeSpan Duration => current?.Duration ?? TimeSpan.Zero;

        public static List<string> GetDevices()
        {
            var list = new List<string>();
            try
            {
                int n = waveOutGetNumDevs();
                for (int i = 0; i < n; i++)
                {
                    var caps = new WaveOutCapsW();
                    if (waveOutGetDevCapsW((IntPtr)i, ref caps, Marshal.SizeOf(typeof(WaveOutCapsW))) == 0) list.Add(caps.szPname);
                    else list.Add("Device " + (i + 1));
                }
            }
            catch { }
            return list;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WaveOutCapsW
        {
            public ushort wMid, wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
            public uint dwFormats;
            public ushort wChannels, wReserved1;
            public uint dwSupport;
        }
        [DllImport("winmm.dll")] static extern int waveOutGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern int waveOutGetDevCapsW(IntPtr id, ref WaveOutCapsW caps, int size);

        public int DeviceNumber
        {
            get => deviceNumber;
            set
            {
                if (value == deviceNumber) return;
                deviceNumber = value;
                if (output != null)
                {
                    bool wasPlaying = output.PlaybackState == PlaybackState.Playing;
                    DisposeOutput();
                    EnsureOutput();
                    if (wasPlaying) output.Play();
                }
            }
        }

        void EnsureOutput()
        {
            if (output != null) return;
            output = new WaveOutEvent { DeviceNumber = deviceNumber, DesiredLatency = 140, NumberOfBuffers = 3 };
            output.PlaybackStopped += (s, e) =>
            {
                if (e.Exception != null) OutputError?.Invoke(e.Exception.Message);
            };
            output.Init(Dsp);
        }

        void DisposeOutput()
        {
            if (output == null) return;
            try { output.Stop(); } catch { }
            try { output.Dispose(); } catch { }
            output = null;
        }

        /// <summary>Opens a file/URL on a worker thread and starts it. Throws if it can't be decoded.</summary>
        public async Task<bool> PlayAsync(string path, double crossfadeSeconds, Func<bool> stillWanted = null)
        {
            var deck = await Task.Run(() => new Deck(path, MixFormat));
            if (stillWanted != null && !stillWanted()) { DisposeLater(deck); return false; }
            EnsureOutput();
            var old = current;
            bool fade = crossfadeSeconds > 0.05 && old != null && !old.Finished && !IsPaused;
            if (old != null)
            {
                if (fade) old.FadeOutAndRemove(crossfadeSeconds);
                else { mixer.Remove(old); DisposeLater(old); }
            }
            if (fade) deck.FadeIn(crossfadeSeconds * 0.8);
            else Dsp.ResetState();
            current = deck;
            mixer.Add(deck);
            IsPaused = false;
            if (output.PlaybackState != PlaybackState.Playing) output.Play();
            return true;
        }

        void OnDeckFinished(Deck deck)
        {
            ui.Post(_ =>
            {
                bool wasCurrent = deck == current;
                if (wasCurrent) current = null;
                DisposeLater(deck);
                if (wasCurrent) TrackEnded?.Invoke();
            }, null);
        }

        static void DisposeLater(Deck d) => Task.Run(() => d.Dispose());

        public void Pause()
        {
            if (output == null || current == null) return;
            output.Pause();
            IsPaused = true;
        }

        public void Resume()
        {
            if (output == null || current == null) return;
            output.Play();
            IsPaused = false;
        }

        public void Stop()
        {
            foreach (var d in mixer.Clear()) DisposeLater(d);
            current = null;
            IsPaused = false;
            if (output != null) { try { output.Stop(); } catch { } }
            Dsp.ResetState();
        }

        public void Seek(TimeSpan t) => current?.Seek(t);

        public void Dispose()
        {
            Stop();
            DisposeOutput();
        }
    }
}
