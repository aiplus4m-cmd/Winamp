using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace RetroAmp.Audio
{
    /// <summary>Endless mixer: sums all active decks, outputs silence when empty.</summary>
    public sealed class Mixer : ISampleProvider
    {
        readonly List<Deck> decks = new List<Deck>();
        readonly object sync = new object();
        float[] temp = new float[0];
        public event Action<Deck> DeckFinished; // raised on the audio thread

        public Mixer(WaveFormat format) { WaveFormat = format; }
        public WaveFormat WaveFormat { get; }

        public void Add(Deck d) { lock (sync) decks.Add(d); }
        public void Remove(Deck d) { lock (sync) decks.Remove(d); }
        public List<Deck> Clear()
        {
            lock (sync) { var l = new List<Deck>(decks); decks.Clear(); return l; }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            List<Deck> finished = null;
            lock (sync)
            {
                if (temp.Length < count) temp = new float[count];
                for (int d = 0; d < decks.Count; d++)
                {
                    var deck = decks[d];
                    if (deck.Paused) continue;
                    int n = deck.Read(temp, 0, count);
                    for (int i = 0; i < n; i++) buffer[offset + i] += temp[i];
                    if (deck.Finished) (finished ?? (finished = new List<Deck>())).Add(deck);
                }
                if (finished != null) foreach (var f in finished) decks.Remove(f);
            }
            if (finished != null) foreach (var f in finished) DeckFinished?.Invoke(f);
            return count;
        }
    }
}
