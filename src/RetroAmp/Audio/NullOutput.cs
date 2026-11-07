using System;
using System.Diagnostics;
using System.Threading;
using NAudio.Wave;

namespace RetroAmp.Audio
{
    /// <summary>
    /// An output "device" that pulls audio through the whole chain in real time and throws it away.
    /// Used when no sound device exists, and by the self-test to exercise playback and track changes.
    /// </summary>
    public sealed class NullOutput : IWavePlayer
    {
        IWaveProvider source;
        Thread thread;
        volatile bool stop;
        volatile PlaybackState state = PlaybackState.Stopped;
        readonly SynchronizationContext ctx = SynchronizationContext.Current;
        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public float Volume { get; set; } = 1f;
        public PlaybackState PlaybackState => state;
        public WaveFormat OutputWaveFormat => source?.WaveFormat;

        public void Init(IWaveProvider waveProvider) { source = waveProvider; }

        public void Play()
        {
            if (source == null) throw new InvalidOperationException("Not initialised");
            state = PlaybackState.Playing;
            if (thread != null) return;
            stop = false;
            thread = new Thread(Run) { IsBackground = true, Name = "NullOutput" };
            thread.Start();
        }

        void Run()
        {
            Exception error = null;
            try
            {
                var fmt = source.WaveFormat;
                int chunk = fmt.AverageBytesPerSecond / 50; // 20 ms
                chunk -= chunk % fmt.BlockAlign;
                var buf = new byte[chunk];
                var sw = Stopwatch.StartNew();
                long sent = 0;
                while (!stop)
                {
                    if (state != PlaybackState.Playing) { Thread.Sleep(10); sw.Restart(); sent = 0; continue; }
                    double aheadMs = sent * 1000.0 / fmt.AverageBytesPerSecond - sw.Elapsed.TotalMilliseconds;
                    if (aheadMs > 60) { Thread.Sleep(5); continue; }
                    source.Read(buf, 0, chunk);
                    sent += chunk;
                }
            }
            catch (Exception ex) { error = ex; }
            state = PlaybackState.Stopped;
            var h = PlaybackStopped;
            if (h != null)
            {
                if (ctx != null) ctx.Post(_ => h(this, new StoppedEventArgs(error)), null);
                else h(this, new StoppedEventArgs(error));
            }
        }

        public void Pause() { if (state == PlaybackState.Playing) state = PlaybackState.Paused; }

        public void Stop()
        {
            stop = true;
            var t = thread;
            thread = null;
            if (t != null && t != Thread.CurrentThread) t.Join(1000);
            state = PlaybackState.Stopped;
        }

        public void Dispose() => Stop();
    }
}
