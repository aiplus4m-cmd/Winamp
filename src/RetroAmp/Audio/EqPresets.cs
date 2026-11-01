using System.Collections.Generic;

namespace RetroAmp.Audio
{
    public static class EqPresets
    {
        public static readonly double[] Frequencies = { 60, 170, 310, 600, 1000, 3000, 6000, 12000, 14000, 16000 };
        public static readonly string[] Labels = { "60", "170", "310", "600", "1K", "3K", "6K", "12K", "14K", "16K" };
        public const float MaxDb = 12f;

        // Values in dB, tuned by ear in the spirit of the classic 10-band presets.
        public static readonly List<KeyValuePair<string, float[]>> BuiltIn = new List<KeyValuePair<string, float[]>>
        {
            P("Flat",            0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            P("Classical",       0, 0, 0, 0, 0, 0, -7, -7, -7, -9),
            P("Club",            0, 0, 8, 5.5f, 5.5f, 5.5f, 3, 0, 0, 0),
            P("Dance",           9.5f, 7, 2.5f, 0, 0, -5.5f, -7, -7, 0, 0),
            P("Full Bass",       9, 9, 9, 5.5f, 1.5f, -4, -8, -10, -11, -11),
            P("Full Bass & Treble", 7, 5.5f, 0, -7, -4.5f, 1.5f, 8, 11, 12, 12),
            P("Full Treble",     -9, -9, -9, -4, 2.5f, 11, 12, 12, 12, 12),
            P("Headphones",      4.5f, 11, 5.5f, -3.5f, -2.5f, 1.5f, 4.5f, 9.5f, 12, 12),
            P("Large Hall",      10, 10, 5.5f, 5.5f, 0, -4.5f, -4.5f, -4.5f, 0, 0),
            P("Live",            -4.5f, 0, 4, 5.5f, 5.5f, 5.5f, 4, 2.5f, 2.5f, 2.5f),
            P("Party",           7, 7, 0, 0, 0, 0, 0, 0, 7, 7),
            P("Pop",             -1.5f, 4.5f, 7, 8, 5.5f, 0, -2.5f, -2.5f, -1.5f, -1.5f),
            P("Reggae",          0, 0, 0, -5.5f, 0, 6.5f, 6.5f, 0, 0, 0),
            P("Rock",            8, 4.5f, -5.5f, -8, -3, 4, 8.5f, 11, 11, 11),
            P("Ska",             -2.5f, -4.5f, -4, 0, 4, 5.5f, 8.5f, 9.5f, 11, 9.5f),
            P("Soft",            4.5f, 1.5f, 0, -2.5f, 0, 4, 8, 9.5f, 11, 12),
            P("Soft Rock",       4, 4, 2.5f, 0, -4, -5.5f, -3, 0, 2.5f, 8.5f),
            P("Techno",          8, 5.5f, 0, -5.5f, -4.5f, 0, 8, 9.5f, 9.5f, 8.5f),
            P("Vocal",           -3, -3, -1, 2, 5, 5.5f, 4, 1, 0, -1.5f),
            P("Bass Boost",      7, 6, 4, 1.5f, 0, 0, 0, 0, 0, 0),
            P("Bolero (Vietnamese)", 3, 2, 1, 0, 2, 3.5f, 2, 0, -1, -2),
        };

        static KeyValuePair<string, float[]> P(string name, params float[] v) => new KeyValuePair<string, float[]>(name, v);
    }
}
