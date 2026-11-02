using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NAudio.Wave;

namespace RetroAmp.Audio
{
    /// <summary>
    /// Master effects chain: preamp → 10-band EQ → karaoke → stereo width → reverb →
    /// (visualizer tap) → volume/balance → soft limiter.
    /// UI thread writes public fields; the audio thread reads them.
    /// </summary>
    public sealed class DspChain : ISampleProvider
    {
        readonly ISampleProvider source;
        readonly int sampleRate;
        readonly Biquad[] eqL = new Biquad[10], eqR = new Biquad[10];
        readonly float[] bandGains = new float[10];
        readonly bool[] active = new bool[10];
        readonly Reverb reverb;
        readonly Limiter limiter;
        volatile bool eqDirty = true;
        float autoGainDb;

        /// <summary>Lower the level automatically by the EQ's peak boost, so EQ never pushes into clipping.</summary>
        public volatile bool AutoGain = true;
        public float AutoGainDb => autoGainDb;
        /// <summary>Peak gain of preamp + EQ curve in dB (computed whenever the EQ changes).</summary>
        public float EqPeakDb { get; private set; }
        float eqHeadroomDb;

        public volatile bool EqEnabled = true;
        public float PreampDb;
        public volatile bool VocalRemover;
        public float StereoWidth = 1f;
        public float ReverbAmount;
        public float Volume = 0.8f;
        public float Balance;

        public DspChain(ISampleProvider source)
        {
            this.source = source;
            sampleRate = source.WaveFormat.SampleRate;
            for (int i = 0; i < 10; i++) { eqL[i] = new Biquad(); eqR[i] = new Biquad(); }
            reverb = new Reverb(sampleRate);
            limiter = new Limiter(sampleRate);
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        // ---- diagnostics (read by the UI)
        int audioThreadId;
        readonly Stopwatch clock = Stopwatch.StartNew();
        double lastCallMs = -1, bufferedMs;
        public int Glitches;                 // times the output asked for audio later than the buffered audio lasted
        public float PeakOut;                // highest output sample since last reset
        public float MinLimiterGain = 1f;    // deepest limiter gain since last reset
        public double MaxProcessMs;          // slowest DSP block
        public void ResetDiagnostics() { Glitches = 0; PeakOut = 0; MinLimiterGain = 1f; MaxProcessMs = 0; }

        public void SetBand(int band, float db)
        {
            bandGains[band] = db;
            eqDirty = true;
        }

        /// <summary>Q per band from the spacing to its neighbours (bands are not evenly spaced: 12k/14k/16k are very close).</summary>
        static double BandQ(int i)
        {
            var f = EqPresets.Frequencies;
            double lo = i > 0 ? f[i - 1] : f[i] / (f[i + 1] / f[i]);
            double hi = i < f.Length - 1 ? f[i + 1] : f[i] * (f[i] / f[i - 1]);
            double bwOct = Math.Log(hi / lo, 2) / 2 * 1.15;           // half the span to the neighbours, slightly wider
            bwOct = Math.Max(0.3, Math.Min(2.0, bwOct));
            return 1.0 / (2 * Math.Sinh(Math.Log(2) / 2 * bwOct));
        }

        double ResponseDb(double f, double[] gains)
        {
            double db = 0;
            var tmp = new Biquad();
            for (int i = 0; i < 10; i++)
            {
                if (gains[i] == 0) continue;
                tmp.SetPeaking(sampleRate, EqPresets.Frequencies[i], BandQ(i), gains[i]);
                db += tmp.MagnitudeDb(f, sampleRate);
            }
            return db;
        }

        void RecalcEq()
        {
            eqDirty = false;
            // Neighbouring peaking filters overlap and add up. Solve for internal filter gains so the
            // response at each band centre matches the slider (a "corrected" graphic EQ).
            var target = new double[10];
            for (int i = 0; i < 10; i++) target[i] = bandGains[i];
            var g = (double[])target.Clone();
            bool any = target.Any(x => x != 0);
            if (any)
            {
                for (int iter = 0; iter < 12; iter++)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        double f = Math.Min(EqPresets.Frequencies[i], sampleRate * 0.45);
                        double err = target[i] - ResponseDb(f, g);
                        g[i] = Math.Max(-24, Math.Min(24, g[i] + err * 0.6));
                    }
                }
            }
            for (int i = 0; i < 10; i++)
            {
                eqL[i].SetPeaking(sampleRate, EqPresets.Frequencies[i], BandQ(i), any ? g[i] : 0);
                eqR[i].SetPeaking(sampleRate, EqPresets.Frequencies[i], BandQ(i), any ? g[i] : 0);
                active[i] = any && Math.Abs(g[i]) > 0.01;
            }
            // peak of the combined response (20 Hz .. 20 kHz)
            double peak = 0, peakLow = 0;
            if (any)
            {
                for (int k = 0; k <= 240; k++)
                {
                    double f = 20 * Math.Pow(1000, k / 240.0);
                    if (f > sampleRate * 0.48) break;
                    double db = 0;
                    for (int i = 0; i < 10; i++) if (active[i]) db += eqL[i].MagnitudeDb(f, sampleRate);
                    if (db > peak) peak = db;
                    if (f <= 5000 && db > peakLow) peakLow = db;
                }
            }
            EqPeakDb = (float)peak;
            // Music carries most of its energy (and peaks) below ~5 kHz: boosts there are compensated fully,
            // boosts in the top octaves only partly — the limiter catches the rare treble peaks.
            eqHeadroomDb = (float)Math.Max(peakLow, peak * 0.6);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // The output device's thread calls us: make it a high-priority audio thread so UI work
            // (visualizers, lyrics, scrolling) can never starve it and cause crackles.
            int tid = Thread.CurrentThread.ManagedThreadId;
            if (tid != audioThreadId)
            {
                audioThreadId = tid;
                try { Thread.CurrentThread.Priority = ThreadPriority.Highest; } catch { }
            }
            double now = clock.Elapsed.TotalMilliseconds;
            if (lastCallMs >= 0)
            {
                bufferedMs -= now - lastCallMs;
                if (bufferedMs < -15) Glitches++;     // the device ran dry before we were asked again
                if (bufferedMs < 0) bufferedMs = 0;
            }
            lastCallMs = now;

            int n = source.Read(buffer, offset, count);
            if (eqDirty) RecalcEq();

            bool eq = EqEnabled;
            float preDb = eq ? PreampDb : 0f;
            // headroom: whatever the EQ adds at its loudest frequency (plus preamp boosts) is taken off again
            float headroom = 0f;
            if (AutoGain)
            {
                float peak = (eq ? eqHeadroomDb : 0f) + preDb;
                if (peak > 0) headroom = -peak;
                if (StereoWidth > 1f) headroom -= (float)(20 * Math.Log10(1 + (StereoWidth - 1) * 0.5));
                if (ReverbAmount > 0.001f) headroom -= ReverbAmount * 3f;
            }
            autoGainDb = headroom;
            float pre = (float)Math.Pow(10, (preDb + headroom) / 20.0);
            bool vocal = VocalRemover;
            float width = StereoWidth;
            float rev = ReverbAmount;

            for (int i = offset; i < offset + n - 1; i += 2)
            {
                float l = buffer[i] * pre, r = buffer[i + 1] * pre;
                if (eq)
                {
                    for (int b = 0; b < 10; b++)
                    {
                        if (!active[b]) continue;
                        l = eqL[b].Process(l);
                        r = eqR[b].Process(r);
                    }
                }
                if (vocal)
                {
                    float side = (l - r) * 0.5f;
                    l = side; r = side;
                }
                if (width != 1f)
                {
                    float mid = (l + r) * 0.5f, side = (l - r) * 0.5f * width;
                    l = mid + side; r = mid - side;
                }
                if (rev > 0.001f) reverb.Process(ref l, ref r, rev);
                buffer[i] = l; buffer[i + 1] = r;
            }

            VisBus.SampleRate = sampleRate;
            VisBus.WriteInterleaved(buffer, offset, n, 2);

            float vol = Volume * Volume; // perceptual taper
            float gl = vol * Math.Min(1f, 1f - Balance);
            float gr = vol * Math.Min(1f, 1f + Balance);
            for (int i = offset; i < offset + n - 1; i += 2)
            {
                float l = buffer[i] * gl, r = buffer[i + 1] * gr;
                limiter.Process(ref l, ref r);
                buffer[i] = l; buffer[i + 1] = r;
                float a = Math.Max(Math.Abs(l), Math.Abs(r));
                if (a > PeakOut) PeakOut = a;
            }
            if (limiter.GainReduction < MinLimiterGain) MinLimiterGain = limiter.GainReduction;
            double took = clock.Elapsed.TotalMilliseconds - now;
            if (took > MaxProcessMs) MaxProcessMs = took;
            bufferedMs += n / 2.0 / sampleRate * 1000.0;
            return n;
        }

        /// <summary>Current limiter gain (1 = not limiting).</summary>
        public float LimiterGain => limiter.GainReduction;

        public void ResetState()
        {
            for (int i = 0; i < 10; i++) { eqL[i].Reset(); eqR[i].Reset(); }
            reverb.Clear();
            limiter.Reset();
        }
    }
}
