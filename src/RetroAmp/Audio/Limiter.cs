using System;

namespace RetroAmp.Audio
{
    /// <summary>
    /// Stereo-linked look-ahead peak limiter. Transparent below the ceiling; above it the gain is
    /// lowered smoothly *before* the peak arrives (5 ms look-ahead) and recovers slowly, so loud
    /// masters and EQ boosts are tamed without the crackle of a waveshaper/clipper.
    /// </summary>
    public sealed class Limiter
    {
        readonly int look;
        readonly float[] dl, dr, need;
        int pos;
        float env = 1f;
        readonly float attack, release;
        public float Ceiling = 0.98f; // -0.18 dBFS
        public float GainReduction => env;

        public Limiter(int sampleRate)
        {
            look = Math.Max(16, (int)(sampleRate * 0.005));
            dl = new float[look]; dr = new float[look]; need = new float[look];
            for (int i = 0; i < look; i++) need[i] = 1f;
            attack = (float)(1 - Math.Exp(-1.0 / (look / 5.0)));
            release = (float)(1 - Math.Exp(-1.0 / (sampleRate * 0.18)));
        }

        public void Process(ref float l, ref float r)
        {
            if (float.IsNaN(l) || float.IsInfinity(l)) l = 0;
            if (float.IsNaN(r) || float.IsInfinity(r)) r = 0;
            float peak = Math.Max(Math.Abs(l), Math.Abs(r));
            float g = peak > Ceiling ? Ceiling / peak : 1f;

            // write the incoming sample, read the one leaving the delay line
            float ol = dl[pos], or = dr[pos];
            dl[pos] = l; dr[pos] = r; need[pos] = g;
            pos++; if (pos >= look) pos = 0;

            // smallest gain needed by anything inside the look-ahead window
            float target = 1f;
            for (int i = 0; i < look; i++) if (need[i] < target) target = need[i];

            env += (target - env) * (target < env ? attack : release);

            l = Clamp(ol * env);
            r = Clamp(or * env);
        }

        float Clamp(float x) => x > Ceiling ? Ceiling : (x < -Ceiling ? -Ceiling : x);

        public void Reset()
        {
            Array.Clear(dl, 0, look); Array.Clear(dr, 0, look);
            for (int i = 0; i < look; i++) need[i] = 1f;
            env = 1f; pos = 0;
        }
    }
}
