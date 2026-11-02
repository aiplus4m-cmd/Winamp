using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RetroAmp.Media
{
    public sealed class LyricLine
    {
        public TimeSpan Time;
        public string Text;
    }

    /// <summary>Lyrics of one song; synced (LRC timestamps) or plain text.</summary>
    public sealed class Lyrics
    {
        public List<LyricLine> Lines = new List<LyricLine>();
        public bool Synced;
        public string Source = "";
        public string Artist = "", Title = "";
        public int OffsetMs;
        public string CacheFile;

        static readonly Regex TimeTag = new Regex(@"\[(\d{1,3}):(\d{1,2}(?:[.:]\d{1,3})?)\]", RegexOptions.Compiled);
        static readonly Regex MetaTag = new Regex(@"^\[([a-zA-Z#]+):(.*)\]\s*$", RegexOptions.Compiled);
        static readonly Regex WordTag = new Regex(@"<\d{1,3}:\d{1,2}(?:[.:]\d{1,3})?>", RegexOptions.Compiled);

        public static Lyrics Parse(string text, string source)
        {
            var ly = new Lyrics { Source = source };
            if (string.IsNullOrWhiteSpace(text)) return ly;
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var raw in lines)
            {
                string line = raw.Trim().TrimStart('﻿');
                if (line.Length == 0) continue;
                var meta = MetaTag.Match(line);
                if (meta.Success && !TimeTag.IsMatch(line))
                {
                    string k = meta.Groups[1].Value.ToLowerInvariant(), v = meta.Groups[2].Value.Trim();
                    if (k == "offset" && int.TryParse(v.TrimStart('+'), out int off)) ly.OffsetMs = off;
                    else if (k == "ar") ly.Artist = v;
                    else if (k == "ti") ly.Title = v;
                    continue;
                }
                var ms = TimeTag.Matches(line);
                if (ms.Count == 0) { ly.Lines.Add(new LyricLine { Time = TimeSpan.MinValue, Text = WordTag.Replace(line, "").Trim() }); continue; }
                string lyric = WordTag.Replace(TimeTag.Replace(line, ""), "").Trim();
                foreach (Match m in ms)
                {
                    int min = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    double sec = double.Parse(m.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                    ly.Lines.Add(new LyricLine { Time = TimeSpan.FromSeconds(min * 60 + sec), Text = lyric });
                }
            }
            int timed = ly.Lines.Count(l => l.Time != TimeSpan.MinValue);
            ly.Synced = timed > 0 && timed >= ly.Lines.Count / 2;
            if (ly.Synced)
            {
                ly.Lines = ly.Lines.Where(l => l.Time != TimeSpan.MinValue).OrderBy(l => l.Time).ToList();
                // drop trailing empty lines, keep gaps as instrumental markers
                while (ly.Lines.Count > 0 && ly.Lines[ly.Lines.Count - 1].Text.Length == 0) ly.Lines.RemoveAt(ly.Lines.Count - 1);
            }
            else
            {
                foreach (var l in ly.Lines) l.Time = TimeSpan.Zero;
            }
            return ly;
        }

        public bool IsEmpty => Lines.Count == 0 || Lines.All(l => string.IsNullOrWhiteSpace(l.Text));

        /// <summary>Index of the line being sung at time t (-1 before the first line).</summary>
        public int IndexAt(TimeSpan t)
        {
            if (!Synced || Lines.Count == 0) return -1;
            int lo = 0, hi = Lines.Count - 1, ans = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (Lines[mid].Time <= t) { ans = mid; lo = mid + 1; } else hi = mid - 1;
            }
            return ans;
        }

        /// <summary>0..1 progress through line i.</summary>
        public double Progress(int i, TimeSpan t, TimeSpan duration)
        {
            if (i < 0 || i >= Lines.Count) return 0;
            var start = Lines[i].Time;
            var end = i + 1 < Lines.Count ? Lines[i + 1].Time : (duration > start ? duration : start + TimeSpan.FromSeconds(5));
            double len = (end - start).TotalSeconds;
            if (len <= 0) return 1;
            // most lines are sung in the first ~85% of the gap
            return Math.Max(0, Math.Min(1, (t - start).TotalSeconds / Math.Min(len, Math.Max(1.5, len * 0.85))));
        }

        public string ToLrc()
        {
            var sb = new StringBuilder();
            if (Artist.Length > 0) sb.Append("[ar:").Append(Artist).AppendLine("]");
            if (Title.Length > 0) sb.Append("[ti:").Append(Title).AppendLine("]");
            sb.Append("[re:RetroAmp / ").Append(Source).AppendLine("]");
            if (OffsetMs != 0) sb.Append("[offset:").Append(OffsetMs.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
            foreach (var l in Lines)
            {
                if (Synced) sb.AppendFormat(CultureInfo.InvariantCulture, "[{0:00}:{1:00.00}]", (int)l.Time.TotalMinutes, l.Time.TotalSeconds % 60);
                sb.AppendLine(l.Text);
            }
            return sb.ToString();
        }

        public string PlainText => string.Join(Environment.NewLine, Lines.Select(l => l.Text));
    }
}
