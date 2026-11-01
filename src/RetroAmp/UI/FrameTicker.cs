using System;
using System.Diagnostics;
using System.Windows.Media;
using RetroAmp.Audio;

namespace RetroAmp.UI
{
    /// <summary>Single per-frame tick (CompositionTarget.Rendering) that updates the analyzer, then all subscribers.</summary>
    public static class FrameTicker
    {
        static event Action tick;
        static bool hooked;
        static TimeSpan lastRender = TimeSpan.MinValue;
        static readonly Stopwatch sw = Stopwatch.StartNew();
        static double lastTime;
        public static double Dt { get; private set; } = 1 / 60.0;

        public static event Action Tick
        {
            add { tick += value; Hook(); }
            remove { tick -= value; }
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            CompositionTarget.Rendering += (s, e) =>
            {
                var re = e as RenderingEventArgs;
                if (re != null)
                {
                    if (re.RenderingTime == lastRender) return;
                    lastRender = re.RenderingTime;
                }
                double now = sw.Elapsed.TotalSeconds;
                Dt = now - lastTime;
                lastTime = now;
                if (Dt < 1 / 75.0 && Dt > 0) { lastTime -= Dt; return; } // cap ~70 fps
                Analyzer.Instance.Update(Dt);
                tick?.Invoke();
            };
        }
    }
}
