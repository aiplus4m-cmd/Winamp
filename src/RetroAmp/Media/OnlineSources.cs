using System;
using System.Text.RegularExpressions;

namespace RetroAmp.Media
{
    public static class OnlineSources
    {
        static readonly string[] Hosts =
        {
            "youtube.com", "youtu.be", "music.youtube.com", "youtube-nocookie.com",
            "soundcloud.com", "snd.sc", "on.soundcloud.com",
            "bandcamp.com", "mixcloud.com", "vimeo.com", "dailymotion.com", "nhaccuatui.com", "zingmp3.vn", "tiktok.com", "facebook.com"
        };

        public static string Host(string url)
        {
            try { return new Uri(url).Host.ToLowerInvariant(); } catch { return ""; }
        }

        public static bool IsKnownSite(string url)
        {
            string h = Host(url);
            foreach (var k in Hosts) if (h == k || h.EndsWith("." + k)) return true;
            return false;
        }

        public static bool IsYouTube(string url) { string h = Host(url); return h.Contains("youtube") || h == "youtu.be"; }
        public static bool IsSoundCloud(string url) { string h = Host(url); return h.Contains("soundcloud") || h == "snd.sc"; }

        public static string ShortName(string url)
        {
            if (IsYouTube(url)) return "YT";
            if (IsSoundCloud(url)) return "SC";
            string h = Host(url);
            if (h.Contains("bandcamp")) return "BC";
            if (h.Contains("mixcloud")) return "MC";
            return "WEB";
        }

        public static string YouTubeId(string url)
        {
            var m = Regex.Match(url ?? "", @"(?:v=|youtu\.be/|shorts/|embed/)([A-Za-z0-9_-]{11})");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
