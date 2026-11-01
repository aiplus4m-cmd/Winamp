using System;
using System.Diagnostics;

namespace RetroAmp.Audio
{
    /// <summary>
    /// Lock-protected ring buffer holding the most recent stereo samples heading to the speakers.
    /// Written by the audio thread (or the WASAPI loopback tap for video) and read by the visualizers.
    /// </summary>
    public static class VisBus
    {
        public const int Size = 8192;
        static readonly float[] L = new float[Size];
        static readonly float[] R = new float[Size];
        static int pos;
        static readonly object Lock = new object();
        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static long lastWriteMs = -100000;
        public static volatile int SampleRate = 44100;

        public static double MsSinceLastWrite => Clock.ElapsedMilliseconds - System.Threading.Interlocked.Read(ref lastWriteMs);

        public static void WriteInterleaved(float[] buffer, int offset, int count, int channels)
        {
            if (channels < 1) return;
            int frames = count / channels;
            lock (Lock)
            {
                for (int i = 0; i < frames; i++)
                {
                    int b = offset + i * channels;
                    float l = buffer[b];
                    float r = channels > 1 ? buffer[b + 1] : l;
                    L[pos] = l; R[pos] = r;
                    pos = (pos + 1) & (Size - 1);
                }
            }
            System.Threading.Interlocked.Exchange(ref lastWriteMs, Clock.ElapsedMilliseconds);
        }

        /// <summary>Copies the newest l.Length samples (oldest first).</summary>
        public static void Snapshot(float[] l, float[] r)
        {
            int n = Math.Min(l.Length, Size);
            lock (Lock)
            {
                int start = (pos - n + Size) & (Size - 1);
                for (int i = 0; i < n; i++)
                {
                    int k = (start + i) & (Size - 1);
                    l[i] = L[k]; r[i] = R[k];
                }
            }
        }
    }
}
