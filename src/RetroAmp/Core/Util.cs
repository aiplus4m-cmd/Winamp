using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RetroAmp.Core
{
    public static class Util
    {
        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        public static readonly HashSet<string> AudioExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".wma", ".aac", ".m4a", ".flac", ".ogg", ".oga", ".opus", ".aif", ".aiff", ".alac", ".mp2", ".ac3", ".amr", ".3ga", ".weba", ".mka" };

        public static readonly HashSet<string> VideoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".m4v", ".mkv", ".avi", ".wmv", ".mov", ".webm", ".3gp", ".3g2", ".ts", ".m2ts", ".mts", ".mpg", ".mpeg", ".asf", ".flv", ".vob" };

        public static readonly HashSet<string> PlaylistExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".m3u", ".m3u8", ".pls" };

        public static bool IsMediaFile(string path)
        {
            string ext = SafeExt(path);
            return AudioExts.Contains(ext) || VideoExts.Contains(ext);
        }

        public static string SafeExt(string path)
        {
            try
            {
                string p = path;
                int q = p.IndexOfAny(new[] { '?', '#' });
                if (q >= 0 && IsUrl(p)) p = p.Substring(0, q);
                return Path.GetExtension(p) ?? "";
            }
            catch { return ""; }
        }

        public static bool IsUrl(string s) =>
            s != null && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                          s.StartsWith("mms://", StringComparison.OrdinalIgnoreCase) ||
                          s.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase));

        public static string FormatTime(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t.TotalHours >= 1) return string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
        }

        public static IEnumerable<string> EnumerateMedia(string folder)
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories); }
            catch { yield break; }
            var list = new List<string>();
            try { list.AddRange(files.Where(IsMediaFile)); } catch { }
            list.Sort(StringComparer.CurrentCultureIgnoreCase);
            foreach (var f in list) yield return f;
        }

        public static double ParseDouble(string s, double def = 0)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return def;
        }
    }
}
