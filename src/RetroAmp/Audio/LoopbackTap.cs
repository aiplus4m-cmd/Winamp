using System;
using NAudio.Wave;

namespace RetroAmp.Audio
{
    /// <summary>
    /// Captures what Windows is playing (WASAPI loopback) so visualizers still dance while a video
    /// (decoded by Windows Media Foundation) is playing.
    /// </summary>
    public static class LoopbackTap
    {
        static WasapiLoopbackCapture capture;
        static float[] conv = new float[0];

        public static void Start()
        {
            if (capture != null) return;
            try
            {
                capture = new WasapiLoopbackCapture();
                var fmt = capture.WaveFormat;
                VisBus.SampleRate = fmt.SampleRate;
                capture.DataAvailable += (s, e) =>
                {
                    int ch = fmt.Channels;
                    int bps = fmt.BitsPerSample / 8;
                    if (bps <= 0 || ch <= 0) return;
                    int samples = e.BytesRecorded / bps;
                    if (conv.Length < samples) conv = new float[samples];
                    if (bps == 4)
                        Buffer.BlockCopy(e.Buffer, 0, conv, 0, samples * 4);
                    else if (bps == 2)
                        for (int i = 0; i < samples; i++) conv[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
                    else return;
                    VisBus.WriteInterleaved(conv, 0, samples - samples % ch, ch);
                };
                capture.StartRecording();
            }
            catch (Exception ex)
            {
                Core.AppPaths.Log("Loopback failed: " + ex.Message);
                Stop();
            }
        }

        public static void Stop()
        {
            var c = capture;
            capture = null;
            if (c == null) return;
            try { c.StopRecording(); } catch { }
            try { c.Dispose(); } catch { }
        }
    }
}
