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
