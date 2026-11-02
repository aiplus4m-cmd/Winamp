using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RetroAmp.Core;

namespace RetroAmp.Media
{
    [DataContract]
    public sealed class LrcLibItem
    {
        [DataMember(Name = "id")] public long Id;
        [DataMember(Name = "trackName")] public string TrackName;
        [DataMember(Name = "artistName")] public string ArtistName;
        [DataMember(Name = "albumName")] public string AlbumName;
        [DataMember(Name = "duration")] public double? Duration;
        [DataMember(Name = "instrumental")] public bool? Instrumental;
        [DataMember(Name = "plainLyrics")] public string PlainLyrics;
        [DataMember(Name = "syncedLyrics")] public string SyncedLyrics;

        public bool HasSynced => !string.IsNullOrWhiteSpace(SyncedLyrics);
        public bool HasAny => HasSynced || !string.IsNullOrWhiteSpace(PlainLyrics);
        public override string ToString() =>
            (ArtistName ?? "?") + " - " + (TrackName ?? "?") + (Duration.HasValue ? "  (" + Util.FormatTime(TimeSpan.FromSeconds(Duration.Value)) + ")" : "") +
            (HasSynced ? "  ♪ synced" : (HasAny ? "  text only" : ""));
    }

    /// <summary>
    /// Finds lyrics: (1) per-song cache, (2) a .lrc file next to the audio file, (3) LRCLIB (free, open lyrics database).
    /// </summary>
    public static class LyricsService
    {
        const string Api = "https://lrclib.net/api/";
        static readonly HttpClient Http = CreateClient();
        public static string LyricsDir => Path.Combine(AppPaths.DataDir, "Lyrics");

        static HttpClient CreateClient()
        {
            try { System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12; } catch { }
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("RetroAmp/" + typeof(LyricsService).Assembly.GetName().Version.ToString(3) + " (https://topvl.net; NhamStudio)");
            return h;
        }

        public static string CacheKey(PlaylistItem item)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes((item.Location ?? "").ToLowerInvariant()));
                return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
            }
        }

        static string CachePath(PlaylistItem item, string ext) => Path.Combine(LyricsDir, CacheKey(item) + ext);

        /// <summary>Artist/title guess used for searching.</summary>
        public static void GuessArtistTitle(PlaylistItem item, out string artist, out string title)
        {
            artist = item.Artist?.Trim() ?? "";
            title = item.Title?.Trim() ?? "";
            if (title.Length == 0)
            {
                title = Util.IsUrl(item.Location) ? "" : SafeName(item.Location);
            }
            title = Clean(title);
            if (artist.Length == 0 || (item.IsOnline && title.Contains(" - ")))
            {
                var parts = Regex.Split(title, @"\s+[-–—|]\s+");
                if (parts.Length >= 2) { artist = parts[0].Trim(); title = string.Join(" - ", parts.Skip(1)).Trim(); }
            }
            artist = Clean(artist);
            title = Regex.Replace(title, @"\s+(ft\.?|feat\.?|featuring|x)\s+.*$", "", RegexOptions.IgnoreCase).Trim();
        }

        static string SafeName(string path) { try { return Path.GetFileNameWithoutExtension(path); } catch { return path; } }

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, @"[\(\[【「][^\)\]】」]*[\)\]】」]", " ");          // (Official MV) [Lyrics] 【...】
            s = Regex.Replace(s, @"^\s*\d{1,3}\s*[\.\-_)]\s*", "");                  // "01 - " track numbers
            s = Regex.Replace(s, @"\b(official|music video|lyrics?|lyric video|audio|mv|m/v|hd|4k|visualizer|karaoke)\b", " ", RegexOptions.IgnoreCase);
            s = s.Replace("_", " ").Replace("|", " - ");
            s = Regex.Replace(s, @"\s{2,}", " ").Trim(' ', '-', '–', '|', '.');
            return s;
        }

        /// <summary>Returns lyrics or null. Never throws for "not found".</summary>
        public static async Task<Lyrics> FindAsync(PlaylistItem item, TimeSpan duration, bool force, CancellationToken ct)
        {
            Directory.CreateDirectory(LyricsDir);
            string cacheLrc = CachePath(item, ".lrc"), cacheTxt = CachePath(item, ".txt"), none = CachePath(item, ".none");
            if (!force)
            {
                if (File.Exists(cacheLrc)) return Load(cacheLrc, "cache");
                if (File.Exists(cacheTxt)) return Load(cacheTxt, "cache");
            }
            // .lrc beside the file
            if (!Util.IsUrl(item.Location))
            {
                try
                {
                    string local = Path.ChangeExtension(item.Location, ".lrc");
                    if (File.Exists(local)) { var l = Load(local, "local .lrc"); if (!l.IsEmpty) return l; }
                }
                catch { }
            }
            if (!force && File.Exists(none) && DateTime.UtcNow - File.GetLastWriteTimeUtc(none) < TimeSpan.FromDays(3)) return null;

            GuessArtistTitle(item, out string artist, out string title);
            if (title.Length == 0) return null;
            if (duration <= TimeSpan.Zero) duration = item.Duration;

            var best = await LookupAsync(artist, title, duration, ct);
            if (best == null || !best.HasAny)
            {
                try { File.WriteAllText(none, artist + " - " + title); } catch { }
                return null;
            }
            return SaveFromLrcLib(item, best);
        }

        public static Lyrics SaveFromLrcLib(PlaylistItem item, LrcLibItem best)
        {
            var ly = Lyrics.Parse(best.HasSynced ? best.SyncedLyrics : best.PlainLyrics, "LRCLIB");
            ly.Artist = best.ArtistName ?? ""; ly.Title = best.TrackName ?? "";
            Save(item, ly);
            return ly;
        }

        public static void Save(PlaylistItem item, Lyrics ly)
        {
            try
            {
                Directory.CreateDirectory(LyricsDir);
                string path = CachePath(item, ly.Synced ? ".lrc" : ".txt");
                foreach (var ext in new[] { ".lrc", ".txt", ".none" })
                {
                    string p = CachePath(item, ext);
                    if (p != path && File.Exists(p)) File.Delete(p);
                }
                File.WriteAllText(path, ly.ToLrc(), new UTF8Encoding(false));
                ly.CacheFile = path;
            }
            catch (Exception ex) { AppPaths.Log("Lyrics save: " + ex.Message); }
        }

        static Lyrics Load(string path, string source)
        {
            var ly = Lyrics.Parse(File.ReadAllText(path, Encoding.UTF8), source);
            if (source == "cache") ly.CacheFile = path;
            var re = Regex.Match(File.ReadAllText(path), @"\[re:RetroAmp / ([^\]]*)\]");
            if (re.Success) ly.Source = re.Groups[1].Value;
            return ly;
        }

        static async Task<LrcLibItem> LookupAsync(string artist, string title, TimeSpan duration, CancellationToken ct)
        {
            var candidates = new List<LrcLibItem>();
            if (artist.Length > 0)
            {
                string url = Api + "get?artist_name=" + Uri.EscapeDataString(artist) + "&track_name=" + Uri.EscapeDataString(title);
                if (duration > TimeSpan.Zero) url += "&duration=" + (int)Math.Round(duration.TotalSeconds);
                var one = await GetJson<LrcLibItem>(url, ct);
                if (one != null && one.HasSynced) return one;
                if (one != null) candidates.Add(one);
                candidates.AddRange(await SearchAsync(artist, title, ct));
            }
            if (!candidates.Any(c => c.HasSynced))
                candidates.AddRange(await SearchAsync(null, (artist + " " + title).Trim(), ct));
            if (!candidates.Any(c => c.HasAny) && artist.Length > 0)
                candidates.AddRange(await SearchAsync(null, title, ct));
            return Pick(candidates, artist, title, duration);
        }

        public static async Task<List<LrcLibItem>> SearchAsync(string artist, string titleOrQuery, CancellationToken ct)
        {
            string url = artist == null
                ? Api + "search?q=" + Uri.EscapeDataString(titleOrQuery)
                : Api + "search?artist_name=" + Uri.EscapeDataString(artist) + "&track_name=" + Uri.EscapeDataString(titleOrQuery);
            var arr = await GetJson<LrcLibItem[]>(url, ct);
            return arr?.Where(a => a != null).ToList() ?? new List<LrcLibItem>();
        }

        static LrcLibItem Pick(List<LrcLibItem> c, string artist, string title, TimeSpan duration)
        {
            LrcLibItem best = null; double bestScore = double.MinValue;
            foreach (var x in c.Where(i => i.HasAny))
            {
                double s = x.HasSynced ? 3 : 0;
                if (duration > TimeSpan.Zero && x.Duration.HasValue)
                {
                    double d = Math.Abs(x.Duration.Value - duration.TotalSeconds);
                    s += d <= 2 ? 4 : d <= 6 ? 2 : d <= 15 ? 0 : -6;
                }
                if (Similar(x.TrackName, title)) s += 2;
                if (artist.Length > 0 && Similar(x.ArtistName, artist)) s += 1.5;
                if (s > bestScore) { bestScore = s; best = x; }
            }
            return bestScore >= 1 ? best : null;
        }

        static bool Similar(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string na = Norm(a), nb = Norm(b);
            return na.Length > 0 && nb.Length > 0 && (na.Contains(nb) || nb.Contains(na));
        }

        static string Norm(string s)
        {
            var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in d)
            {
                if (ch == 'đ') { sb.Append('d'); continue; }
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            }
            return sb.ToString();
        }

        static async Task<T> GetJson<T>(string url, CancellationToken ct) where T : class
        {
            try
            {
                using (var resp = await Http.GetAsync(url, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode) return null;
                    var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    using (var ms = new MemoryStream(bytes))
                        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(ms);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { AppPaths.Log("LRCLIB: " + ex.Message); return null; }
        }
    }
}
