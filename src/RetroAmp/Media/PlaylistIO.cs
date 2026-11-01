using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RetroAmp.Core;

namespace RetroAmp.Media
{
    public static class PlaylistIO
    {
        public static List<PlaylistItem> Load(string file)
        {
            var list = new List<PlaylistItem>();
            string dir = Path.GetDirectoryName(Path.GetFullPath(file));
            string[] lines = File.ReadAllLines(file, Encoding.UTF8);
            if (Util.SafeExt(file).Equals(".pls", StringComparison.OrdinalIgnoreCase))
            {
                var files = new SortedDictionary<int, string>();
                var titles = new Dictionary<int, string>();
                foreach (var raw in lines)
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim(), val = line.Substring(eq + 1).Trim();
                    if (key.StartsWith("File", StringComparison.OrdinalIgnoreCase) && int.TryParse(key.Substring(4), out int fi)) files[fi] = val;
                    else if (key.StartsWith("Title", StringComparison.OrdinalIgnoreCase) && int.TryParse(key.Substring(5), out int ti)) titles[ti] = val;
                }
                foreach (var kv in files)
                {
                    titles.TryGetValue(kv.Key, out string t);
                    list.Add(new PlaylistItem(Resolve(dir, kv.Value), t));
                }
                return list;
            }

            string pendingTitle = null; TimeSpan? pendingDur = null; bool pendingVideo = false;
            foreach (var raw in lines)
            {
                string line = raw.Trim().TrimStart('﻿');
                if (line.Length == 0) continue;
                if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
                {
                    string rest = line.Substring(8);
                    int comma = rest.IndexOf(',');
                    if (comma >= 0)
                    {
                        double secs = Util.ParseDouble(rest.Substring(0, comma).Trim(), -1);
                        pendingDur = secs > 0 ? TimeSpan.FromSeconds(secs) : (TimeSpan?)null;
                        pendingTitle = rest.Substring(comma + 1).Trim();
                    }
                    continue;
                }
                if (line.Equals("#RETROAMP:VIDEO", StringComparison.OrdinalIgnoreCase)) { pendingVideo = true; continue; }
                if (line.StartsWith("#")) continue;
                var item = new PlaylistItem(Resolve(dir, line), string.IsNullOrEmpty(pendingTitle) ? null : pendingTitle, pendingDur)
                { PlayAsVideo = pendingVideo };
                list.Add(item);
                pendingTitle = null; pendingDur = null; pendingVideo = false;
            }
            return list;
        }

        static string Resolve(string dir, string entry)
        {
            if (Util.IsUrl(entry)) return entry;
            if (entry.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                try { return new Uri(entry).LocalPath; } catch { }
            }
            try { return Path.IsPathRooted(entry) ? entry : Path.GetFullPath(Path.Combine(dir, entry)); }
            catch { return entry; }
        }

        public static void Save(string file, IEnumerable<PlaylistItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");
            foreach (var it in items)
            {
                int secs = it.Duration > TimeSpan.Zero ? (int)it.Duration.TotalSeconds : -1;
                sb.Append("#EXTINF:").Append(secs.ToString(CultureInfo.InvariantCulture)).Append(',').AppendLine(it.DisplayTitle);
                if (it.PlayAsVideo && it.IsOnline) sb.AppendLine("#RETROAMP:VIDEO");
                sb.AppendLine(it.Location);
            }
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
