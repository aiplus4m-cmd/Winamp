using System;
using RetroAmp.Core;

namespace RetroAmp.Audio
{
    /// <summary>
    /// Turns the samples in <see cref="VisBus"/> into data the visualizers draw:
    /// log-spaced spectrum bands with falling peaks, waveforms, VU levels, bass energy and beats.
    /// Updated once per rendered frame by <see cref="UI.FrameTicker"/>.
    /// </summary>
    public sealed class Analyzer
    {
        public static readonly Analyzer Instance = new Analyzer();

        public const int N = 2048;
        public const int BandCount = 64;
        public const int WaveLength = 1024;

        readonly float[] l = new float[N], r = new float[N];
        readonly float[] re = new float[N], im = new float[N];
        readonly float[] window = new float[N];
        readonly float[] mag = new float[N / 2];

        public readonly float[] Bands = new float[BandCount];
        public readonly float[] Peaks = new float[BandCount];
        readonly float[] peakHold = new float[BandCount];
        public readonly float[] WaveL = new float[WaveLength];
        public readonly float[] WaveR = new float[WaveLength];

        public float LevelL, LevelR;      // smoothed RMS-ish 0..1
        public float VuL, VuR;            // ballistic needle position 0..1
        public float Bass, Mid, Treble;   // 0..1 smoothed
        public float BassRaw;
        public float BeatPulse;           // 1 on beat, decays to 0
        public bool Beat;
        public double Time;               // seconds, running clock
        public bool Silent = true;

        readonly float[] bassHistory = new float[43];
        int bassHistPos;
        double lastBeatTime;

        Analyzer()
        {
            for (int i = 0; i < N; i++) window[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1)));
        }

        public void Update(double dt)
        {
            if (dt <= 0 || dt > 0.25) dt = 1.0 / 60;
            Time += dt;
            bool stale = VisBus.MsSinceLastWrite > 200;
            Silent = stale;
            if (stale)
            {
                Array.Clear(l, 0, N); Array.Clear(r, 0, N);
            }
            else VisBus.Snapshot(l, r);

            // waveforms (most recent samples)
            Array.Copy(l, N - WaveLength, WaveL, 0, WaveLength);
            Array.Copy(r, N - WaveLength, WaveR, 0, WaveLength);

            // levels
            double sl = 0, sr = 0;
            for (int i = N - 1024; i < N; i++) { sl += l[i] * l[i]; sr += r[i] * r[i]; }
            float rl = (float)Math.Sqrt(sl / 1024) * 1.9f, rr = (float)Math.Sqrt(sr / 1024) * 1.9f;
            LevelL = Smooth(LevelL, Util.Clamp(rl, 0, 1), 0.5f, 0.12f);
            LevelR = Smooth(LevelR, Util.Clamp(rr, 0, 1), 0.5f, 0.12f);
            VuL = Ballistic(VuL, Util.Clamp(rl * 1.3f, 0, 1.08f), dt);
            VuR = Ballistic(VuR, Util.Clamp(rr * 1.3f, 0, 1.08f), dt);

            // spectrum
            for (int i = 0; i < N; i++) { re[i] = (l[i] + r[i]) * 0.5f * window[i]; im[i] = 0; }
            Fft.Transform(re, im);
            for (int i = 0; i < N / 2; i++) mag[i] = (float)Math.Sqrt(re[i] * re[i] + im[i] * im[i]) * (4f / N);

            double sampleRate = VisBus.SampleRate;
            double binHz = sampleRate / N;
            const double fMin = 35, fMax = 16000;
            float decay = (float)(dt * 1.6);
            for (int b = 0; b < BandCount; b++)
            {
                double f0 = fMin * Math.Pow(fMax / fMin, (double)b / BandCount);
                double f1 = fMin * Math.Pow(fMax / fMin, (double)(b + 1) / BandCount);
                int i0 = Math.Max(1, (int)(f0 / binHz));
                int i1 = Math.Max(i0, Math.Min(N / 2 - 1, (int)(f1 / binHz)));
                float m = 0;
                for (int i = i0; i <= i1; i++) if (mag[i] > m) m = mag[i];
                // slight tilt so highs are visible like classic analyzers
                m *= (float)(1.0 + b / (double)BandCount * 1.5);
                double db = 20 * Math.Log10(m + 1e-9);
                float v = (float)Util.Clamp((db + 62) / 58.0, 0, 1);
                if (v > Bands[b]) Bands[b] = Bands[b] + (v - Bands[b]) * 0.7f;
                else Bands[b] = Math.Max(v, Bands[b] - decay);

                if (Bands[b] >= Peaks[b]) { Peaks[b] = Bands[b]; peakHold[b] = 0.35f; }
                else if (peakHold[b] > 0) peakHold[b] -= (float)dt;
                else Peaks[b] = Math.Max(0, Peaks[b] - (float)(dt * 0.9));
            }

            float bass = Avg(0, 12), mid = Avg(16, 44), treble = Avg(44, 64);
            BassRaw = bass;
            Bass = Smooth(Bass, bass, 0.6f, 0.15f);
            Mid = Smooth(Mid, mid, 0.5f, 0.12f);
            Treble = Smooth(Treble, treble, 0.6f, 0.15f);

            // beat detection on bass energy
            float avg = 0;
            for (int i = 0; i < bassHistory.Length; i++) avg += bassHistory[i];
            avg /= bassHistory.Length;
            Beat = false;
            if (!stale && bass > avg * 1.18f && bass > 0.32f && Time - lastBeatTime > 0.22)
            {
                Beat = true; lastBeatTime = Time; BeatPulse = 1f;
            }
            bassHistory[bassHistPos] = bass;
            bassHistPos = (bassHistPos + 1) % bassHistory.Length;
            BeatPulse = Math.Max(0, BeatPulse - (float)(dt * 3.5));
        }

        float Avg(int a, int b)
        {
            float s = 0;
            for (int i = a; i < b; i++) s += Bands[i];
            return s / (b - a);
        }

        static float Smooth(float cur, float target, float attack, float release) =>
            target > cur ? cur + (target - cur) * attack : cur + (target - cur) * release;

        static float Ballistic(float cur, float target, double dt)
        {
            float k = (float)(1 - Math.Exp(-dt / 0.06));
            return cur + (target - cur) * k;
        }
    }
}
