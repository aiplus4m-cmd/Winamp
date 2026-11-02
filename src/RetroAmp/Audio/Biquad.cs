using System;

namespace RetroAmp.Audio
{
    /// <summary>RBJ cookbook peaking EQ biquad (Direct Form I).</summary>
    public sealed class Biquad
    {
        double b0 = 1, b1, b2, a1, a2;
        double x1, x2, y1, y2;

        public void SetPeaking(double sampleRate, double freq, double q, double gainDb)
        {
            freq = Math.Min(freq, sampleRate * 0.45);
            double A = Math.Pow(10, gainDb / 40);
            double w0 = 2 * Math.PI * freq / sampleRate;
            double alpha = Math.Sin(w0) / (2 * q);
            double cos = Math.Cos(w0);
            double a0 = 1 + alpha / A;
            b0 = (1 + alpha * A) / a0;
            b1 = -2 * cos / a0;
            b2 = (1 - alpha * A) / a0;
            a1 = -2 * cos / a0;
            a2 = (1 - alpha / A) / a0;
        }

        /// <summary>Magnitude response in dB at a frequency.</summary>
        public double MagnitudeDb(double freq, double sampleRate)
        {
            double w = 2 * Math.PI * freq / sampleRate;
            double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
            double nr = b0 + b1 * c1 + b2 * c2, ni = -(b1 * s1 + b2 * s2);
            double dr = 1 + a1 * c1 + a2 * c2, di = -(a1 * s1 + a2 * s2);
            double mag2 = (nr * nr + ni * ni) / Math.Max(1e-20, dr * dr + di * di);
            return 10 * Math.Log10(Math.Max(1e-20, mag2));
        }

        public float Process(float x)
        {
            double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            if (Math.Abs(y1) < 1e-15) y1 = 0; // denormal guard
            return (float)y;
        }

        public void Reset() { x1 = x2 = y1 = y2 = 0; }
    }
}
