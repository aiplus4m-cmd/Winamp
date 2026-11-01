using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace RetroAmp.Audio
{
    /// <summary>One playing track: decoder → stereo → resampled to the mixer rate, with fade envelope.</summary>
    public sealed class Deck : ISampleProvider, IDisposable
    {
        readonly MediaFoundationReader reader;
        readonly ISampleProvider chain;
        readonly object sync = new object();
        float gain = 1f, fadeFrom = 1f, fadeTo = 1f;
        long fadeTotal, fadeDone;
        bool removeAfterFade;

        public string Path { get; }
        public volatile bool Finished;
        public bool Paused;
        public TimeSpan Duration { get; }
        public int SourceSampleRate { get; }
        public int SourceChannels { get; }

        public Deck(string path, WaveFormat target)
        {
            Path = path;
            reader = new MediaFoundationReader(path, new MediaFoundationReader.MediaFoundationReaderSettings
            {
                RequestFloatOutput = true,
                RepositionInRead = true
            });
            Duration = reader.TotalTime;
            SourceSampleRate = reader.WaveFormat.SampleRate;
            SourceChannels = reader.WaveFormat.Channels;

            ISampleProvider sp = reader.ToSampleProvider();
            if (sp.WaveFormat.Channels == 1) sp = new MonoToStereoSampleProvider(sp);
            else if (sp.WaveFormat.Channels > 2) sp = new DownmixProvider(sp);
            if (sp.WaveFormat.SampleRate != target.SampleRate) sp = new WdlResamplingSampleProvider(sp, target.SampleRate);
            chain = sp;
            WaveFormat = target;
        }

        public WaveFormat WaveFormat { get; }

        public TimeSpan Position
        {
            get { try { return reader.CurrentTime; } catch { return TimeSpan.Zero; } }
        }

        public void Seek(TimeSpan t)
        {
            lock (sync)
            {
                if (t < TimeSpan.Zero) t = TimeSpan.Zero;
                if (Duration > TimeSpan.Zero && t > Duration) t = Duration - TimeSpan.FromMilliseconds(200);
                reader.CurrentTime = t;
                Finished = false;
            }
        }

        public void FadeIn(double seconds)
        {
            lock (sync) StartFade(0f, 1f, seconds, false);
        }

        public void FadeOutAndRemove(double seconds)
        {
            lock (sync) StartFade(gain, 0f, seconds, true);
        }

        void StartFade(float from, float to, double seconds, bool remove)
        {
            fadeFrom = from; fadeTo = to; gain = from;
            fadeTotal = Math.Max(1, (long)(seconds * WaveFormat.SampleRate));
            fadeDone = 0;
            removeAfterFade = remove;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            lock (sync)
            {
                if (Finished) return 0;
                int n;
                try { n = chain.Read(buffer, offset, count); }
                catch (Exception ex) { Core.AppPaths.Log("Decode error: " + ex.Message); n = 0; }
                if (n <= 0) { Finished = true; return 0; }

                for (int i = offset; i < offset + n - 1; i += 2)
                {
                    if (fadeDone < fadeTotal)
                    {
                        fadeDone++;
                        gain = fadeFrom + (fadeTo - fadeFrom) * (fadeDone / (float)fadeTotal);
                    }
                    else if (removeAfterFade) { Finished = true; }
                    buffer[i] *= gain; buffer[i + 1] *= gain;
                }
                return n;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                try { reader.Dispose(); } catch { }
            }
        }
    }

    /// <summary>Simple N-channel → stereo downmix (front L/R + center + surrounds).</summary>
    sealed class DownmixProvider : ISampleProvider
    {
        readonly ISampleProvider src;
        readonly int ch;
        float[] buf = new float[0];
        public DownmixProvider(ISampleProvider src)
        {
            this.src = src; ch = src.WaveFormat.Channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(src.WaveFormat.SampleRate, 2);
        }
        public WaveFormat WaveFormat { get; }
        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / 2;
            int need = frames * ch;
            if (buf.Length < need) buf = new float[need];
            int got = src.Read(buf, 0, need);
            int gf = got / ch;
            for (int f = 0; f < gf; f++)
            {
                int b = f * ch;
                float l = buf[b], r = buf[b + 1];
                if (ch >= 3) { float c = buf[b + 2] * 0.707f; l += c; r += c; }
                if (ch >= 6) { l += buf[b + 4] * 0.5f; r += buf[b + 5] * 0.5f; }
                else if (ch >= 5) { l += buf[b + 3] * 0.5f; r += buf[b + 4] * 0.5f; }
                buffer[offset + f * 2] = l * 0.6f;
                buffer[offset + f * 2 + 1] = r * 0.6f;
            }
            return gf * 2;
        }
    }
}
