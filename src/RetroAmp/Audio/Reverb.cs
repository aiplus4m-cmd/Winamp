namespace RetroAmp.Audio
{
    /// <summary>Small Freeverb-style stereo reverb (4 combs + 2 allpasses per channel).</summary>
    public sealed class Reverb
    {
        sealed class Comb
        {
            readonly float[] buf; int idx; float store;
            public Comb(int size) { buf = new float[size]; }
            public float Process(float input, float feedback, float damp)
            {
                float output = buf[idx];
                store = output * (1 - damp) + store * damp;
                buf[idx] = input + store * feedback;
                if (++idx >= buf.Length) idx = 0;
                return output;
            }
            public void Clear() { System.Array.Clear(buf, 0, buf.Length); store = 0; }
        }
        sealed class AllPass
        {
            readonly float[] buf; int idx;
            public AllPass(int size) { buf = new float[size]; }
            public float Process(float input)
            {
                float b = buf[idx];
                float output = -input + b;
                buf[idx] = input + b * 0.5f;
                if (++idx >= buf.Length) idx = 0;
                return output;
            }
            public void Clear() { System.Array.Clear(buf, 0, buf.Length); }
        }

        readonly Comb[] combL, combR;
        readonly AllPass[] apL, apR;
        public float Room = 0.82f, Damp = 0.25f;

        public Reverb(int sampleRate)
        {
            double s = sampleRate / 44100.0;
            int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491 };
            int[] aps = { 556, 441 };
            combL = new Comb[combs.Length]; combR = new Comb[combs.Length];
            for (int i = 0; i < combs.Length; i++)
            {
                combL[i] = new Comb((int)(combs[i] * s));
                combR[i] = new Comb((int)((combs[i] + 23) * s));
            }
            apL = new AllPass[aps.Length]; apR = new AllPass[aps.Length];
            for (int i = 0; i < aps.Length; i++)
            {
                apL[i] = new AllPass((int)(aps[i] * s));
                apR[i] = new AllPass((int)((aps[i] + 23) * s));
            }
        }

        public void Process(ref float l, ref float r, float wet)
        {
            float input = (l + r) * 0.015f;
            float ol = 0, or = 0;
            for (int i = 0; i < combL.Length; i++)
            {
                ol += combL[i].Process(input, Room, Damp);
                or += combR[i].Process(input, Room, Damp);
            }
            for (int i = 0; i < apL.Length; i++) { ol = apL[i].Process(ol); or = apR[i].Process(or); }
            l += ol * wet * 1.3f;
            r += or * wet * 1.3f;
        }

        public void Clear()
        {
            foreach (var c in combL) c.Clear();
            foreach (var c in combR) c.Clear();
            foreach (var a in apL) a.Clear();
            foreach (var a in apR) a.Clear();
        }
    }
}
