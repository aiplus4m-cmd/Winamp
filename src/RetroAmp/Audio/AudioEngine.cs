using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using RetroAmp.Core;

namespace RetroAmp.Audio
{
    public enum OutputMode { Wasapi = 0, WaveOut = 1, Null = 2 }

    public sealed class AudioDeviceInfo
    {
        public string Id;
        public string Name;
        public override string ToString() => Name;
    }

    /// <summary>
    /// Persistent output device + mixer + DSP. Tracks are opened as decks; crossfades are done by
    /// fading the outgoing deck while the new one fades in.
    /// Output goes through WASAPI shared mode at the device's own sample rate (like browsers and
    /// modern players), with legacy WaveOut as a fallback / compatibility mode.
    /// </summary>
    public sealed class AudioEngine : IDisposable
    {
        /// <summary>Internal mixing format = the default device's native rate, so Windows never has to resample.</summary>
        public static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(DetectDeviceRate(), 2);

        readonly SynchronizationContext ui;
        readonly Mixer mixer;
        public readonly DspChain Dsp;
        IWavePlayer output;
        Deck current;
        OutputMode mode = OutputMode.Wasapi;
        string deviceId = "";
        int waveOutDevice = -1;
        bool reopening;

        public event Action TrackEnded;
        public event Action<string> OutputError;

        public string OutputDescription { get; private set; } = "(not started)";
        /// <summary>Approximate delay between decoding and hearing, for lyrics/visual sync.</summary>
        public int LatencyMs { get; private set; } = 180;

        public AudioEngine()
        {
            // Events (track ended, device lost) must reach the UI thread. When the engine is created before
            // the WPF dispatcher runs, SynchronizationContext.Current is still null and a plain context would
            // post to the thread pool — touching the UI from there crashed the app at the end of every track.
            ui = SynchronizationContext.Current;
            if (ui == null || ui.GetType() == typeof(SynchronizationContext))
                ui = new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher);
            mixer = new Mixer(MixFormat);
            mixer.DeckFinished += OnDeckFinished;
            Dsp = new DspChain(mixer);
        }

        static int DetectDeviceRate()
        {
            try
            {
                using (var en = new MMDeviceEnumerator())
                using (var dev = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    int r = dev.AudioClient.MixFormat.SampleRate;
                    if (r >= 32000 && r <= 192000) return r;
                }
            }
            catch { }
            return 44100;
        }

        public bool HasTrack => current != null;
        public bool IsPaused { get; private set; }
        public Deck Current => current;
        public TimeSpan Position => current?.Position ?? TimeSpan.Zero;
        public TimeSpan Duration => current?.Duration ?? TimeSpan.Zero;

        public static List<AudioDeviceInfo> GetDevices()
        {
            var list = new List<AudioDeviceInfo>();
            try
            {
                using (var en = new MMDeviceEnumerator())
                    foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                        list.Add(new AudioDeviceInfo { Id = d.ID, Name = d.FriendlyName });
            }
            catch { }
            return list;
        }

        /// <summary>Selects output: WASAPI device id ("" = Windows default) or legacy WaveOut.</summary>
        public void Configure(OutputMode m, string wasapiDeviceId, int waveOutIndex)
        {
            mode = m; deviceId = wasapiDeviceId ?? ""; waveOutDevice = waveOutIndex;
            if (output != null)
            {
                bool wasPlaying = output.PlaybackState == PlaybackState.Playing;
                DisposeOutput();
                EnsureOutput();
                if (wasPlaying) output.Play();
            }
        }

        void EnsureOutput()
        {
            if (output != null) return;
            var provider = new SampleToWaveProvider(Dsp);
            if (mode == OutputMode.Wasapi)
            {
                MMDevice dev = null;
                try
                {
                    using (var en = new MMDeviceEnumerator())
                    {
                        if (!string.IsNullOrEmpty(deviceId)) { try { dev = en.GetDevice(deviceId); } catch { dev = null; } }
                        if (dev == null || dev.State != DeviceState.Active) dev = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    }
                    var w = new WasapiOut(dev, AudioClientShareMode.Shared, true, 200);
                    w.Init(provider);
                    HookStopped(w);
                    output = w;
                    var mf = dev.AudioClient.MixFormat;
                    OutputDescription = "WASAPI shared · " + dev.FriendlyName + " · device " + mf.SampleRate + " Hz / " + mf.Channels + " ch · RetroAmp mixes at " + MixFormat.SampleRate + " Hz";
                    LatencyMs = 200;
                    return;
                }
                catch (Exception ex)
                {
                    AppPaths.Log("WASAPI output failed, falling back to WaveOut: " + ex.Message);
                }
            }
            if (mode != OutputMode.Null)
            {
                try
                {
                    var wo = new WaveOutEvent { DeviceNumber = waveOutDevice, DesiredLatency = 300, NumberOfBuffers = 4 };
                    wo.Init(provider);
                    HookStopped(wo);
                    output = wo;
                    OutputDescription = "WaveOut (compatibility) · device #" + waveOutDevice + " · " + MixFormat.SampleRate + " Hz";
                    LatencyMs = 260;
                    return;
                }
                catch (Exception ex)
                {
                    AppPaths.Log("WaveOut failed: " + ex.Message);
                    if (mode == OutputMode.WaveOut) OutputError?.Invoke("No working audio device (" + ex.Message + ")");
                }
            }
            // no sound device at all: keep the player working silently instead of failing every track
            var nul = new NullOutput();
            nul.Init(provider);
            HookStopped(nul);
            output = nul;
            OutputDescription = mode == OutputMode.Null ? "Silent test output" : "No audio device found – playing silently";
            LatencyMs = 40;
        }

        void HookStopped(IWavePlayer p)
        {
            p.PlaybackStopped += (s, e) =>
            {
                if (e.Exception == null || reopening) return;
                AppPaths.Log("Output stopped: " + e.Exception.Message);
                // e.g. headphones unplugged / default device changed: reopen on the current default device
                ui.Post(_ =>
                {
                    if (output != p) return;
                    reopening = true;
                    try
                    {
                        DisposeOutput();
                        EnsureOutput();
                        if (current != null && !IsPaused) output.Play();
                    }
                    catch (Exception ex) { OutputError?.Invoke(ex.Message); }
                    finally { reopening = false; }
                }, null);
            };
        }

        void DisposeOutput()
        {
            if (output == null) return;
            var o = output;
            output = null;
            try { o.Stop(); } catch { }
            try { o.Dispose(); } catch { }
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

        // ---- legacy WaveOut device names (compatibility mode)
        public static List<string> GetWaveOutDevices()
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
    }
}
