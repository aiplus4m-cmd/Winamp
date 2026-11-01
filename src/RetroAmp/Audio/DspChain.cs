using System;
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
        readonly Reverb reverb;
        volatile bool eqDirty = true;

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
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        public void SetBand(int band, float db)
        {
            bandGains[band] = db;
            eqDirty = true;
        }

        void RecalcEq()
        {
            eqDirty = false;
            for (int i = 0; i < 10; i++)
            {
                double q = 1.25;
                eqL[i].SetPeaking(sampleRate, EqPresets.Frequencies[i], q, bandGains[i]);
                eqR[i].SetPeaking(sampleRate, EqPresets.Frequencies[i], q, bandGains[i]);
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int n = source.Read(buffer, offset, count);
            if (eqDirty) RecalcEq();

            bool eq = EqEnabled;
            float pre = eq ? (float)Math.Pow(10, PreampDb / 20.0) : 1f;
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
                        if (bandGains[b] == 0) continue;
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
                buffer[i] = Limit(buffer[i] * gl);
                buffer[i + 1] = Limit(buffer[i + 1] * gr);
            }
            return n;
        }

        static float Limit(float x)
        {
            float a = Math.Abs(x);
            if (a <= 0.85f) return x;
            float over = (a - 0.85f) / 0.15f;
            float y = 0.85f + 0.15f * (float)Math.Tanh(over);
            return x < 0 ? -y : y;
        }

        public void ResetState()
        {
            for (int i = 0; i < 10; i++) { eqL[i].Reset(); eqR[i].Reset(); }
            reverb.Clear();
        }
    }
}
