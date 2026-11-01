using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RetroAmp.Core;

namespace RetroAmp.Media
{
    /// <summary>Thin async wrapper around yt-dlp.exe for listing playlists/searches and fetching audio.</summary>
    public static class YtDlp
    {
        const string Sep = "|~|";
        static readonly SemaphoreSlim DownloadGate = new SemaphoreSlim(2);

        public static bool Available => Tools.YtDlpPath != null;

        sealed class Result { public int Exit; public List<string> Out = new List<string>(); public string LastError = ""; }

        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";

        static Task<Result> RunAsync(string args, Action<string> onStdErr, CancellationToken ct)
        {
            string exe = Tools.YtDlpPath ?? throw new InvalidOperationException("yt-dlp is not installed.");
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = AppPaths.CacheDir
            };
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            string path = psi.EnvironmentVariables["PATH"] ?? "";
            psi.EnvironmentVariables["PATH"] = AppPaths.ToolsDir + ";" + path;

            var res = new Result();
            var tcs = new TaskCompletionSource<Result>();
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            int pending = 2;
            void Done()
            {
                if (Interlocked.Decrement(ref pending) == 0)
                {
                    try { p.WaitForExit(); res.Exit = p.ExitCode; } catch { res.Exit = -1; }
                    p.Dispose();
                    tcs.TrySetResult(res);
                }
            }
            p.OutputDataReceived += (s, e) => { if (e.Data == null) Done(); else lock (res.Out) res.Out.Add(e.Data); };
            p.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) { Done(); return; }
                if (e.Data.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)) res.LastError = e.Data;
                onStdErr?.Invoke(e.Data);
            };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (ct.CanBeCanceled)
                ct.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } tcs.TrySetCanceled(); });
            return tcs.Task;
        }

        /// <summary>Lists the entries of a URL (single video, playlist, set, channel) or a search ("ytsearch20:..." / "scsearch20:...").</summary>
        public static async Task<List<PlaylistItem>> ListAsync(string urlOrSearch, CancellationToken ct = default)
        {
            string tmpl = "%(webpage_url,url)s" + Sep + "%(duration)s" + Sep + "%(title)s" + Sep + "%(uploader,channel,artist)s";
            string args = "--flat-playlist --ignore-errors --no-warnings --print " + Q(tmpl) + " " + Q(urlOrSearch);
            var res = await RunAsync(args, null, ct);
            var items = new List<PlaylistItem>();
            foreach (var line in res.Out)
            {
                var parts = line.Split(new[] { Sep }, StringSplitOptions.None);
                if (parts.Length < 3 || !Util.IsUrl(parts[0])) continue;
                double secs = Util.ParseDouble(parts[1], 0);
                string title = parts[2] == "NA" ? null : parts[2];
                var it = new PlaylistItem(parts[0], title, secs > 0 ? TimeSpan.FromSeconds(secs) : (TimeSpan?)null);
                if (parts.Length > 3 && parts[3] != "NA" && !string.IsNullOrWhiteSpace(parts[3]) && title != null && !title.Contains(" - "))
                    it.Artist = parts[3];
                items.Add(it);
            }
            if (items.Count == 0)
                throw new Exception(string.IsNullOrEmpty(res.LastError) ? "Nothing found for this link." : res.LastError);
            return items;
        }

        static readonly Regex Percent = new Regex(@"(\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);

        /// <summary>Downloads the best audio of an online item into the cache and returns the local path.</summary>
        public static async Task<string> FetchAudioAsync(PlaylistItem item, IProgress<double> progress, CancellationToken ct)
        {
            if (item.CachedFile != null && File.Exists(item.CachedFile)) return item.CachedFile;
            string ytId = OnlineSources.YouTubeId(item.Location);
            if (ytId != null)
            {
                var hit = SafeFiles("Youtube-" + ytId + ".*").FirstOrDefault(f => !f.EndsWith(".part"));
                if (hit != null) { item.CachedFile = hit; Touch(hit); return hit; }
            }

            await DownloadGate.WaitAsync(ct);
            try
            {
                string fmt = "bestaudio[ext=m4a]/bestaudio[ext=mp3]/bestaudio[acodec^=mp4a]/bestaudio[ext=webm]/bestaudio/best";
                string outTmpl = Path.Combine(AppPaths.CacheDir, "%(extractor_key)s-%(id)s.%(ext)s");
                string print = "after_move:%(filepath)s" + Sep + "%(title)s" + Sep + "%(duration)s" + Sep + "%(uploader,artist)s";
                string args = "-f " + Q(fmt) + " --no-playlist --no-mtime --newline --no-warnings --match-filters \"!is_live\" " +
                              "-o " + Q(outTmpl) + " --no-simulate --print " + Q(print) + " " + Q(item.Location);
                var res = await RunAsync(args, line =>
                {
                    var m = Percent.Match(line);
                    if (m.Success && line.Contains("[download]")) progress?.Report(Util.ParseDouble(m.Groups[1].Value) / 100.0);
                }, ct);

                foreach (var line in res.Out)
                {
                    var parts = line.Split(new[] { Sep }, StringSplitOptions.None);
                    if (parts.Length >= 1 && File.Exists(parts[0]))
                    {
                        item.CachedFile = parts[0];
                        if (parts.Length > 1 && parts[1] != "NA" && string.IsNullOrWhiteSpace(item.Title)) item.Title = parts[1];
                        if (parts.Length > 2 && item.Duration <= TimeSpan.Zero)
                        {
                            double d = Util.ParseDouble(parts[2], 0);
                            if (d > 0) item.Duration = TimeSpan.FromSeconds(d);
                        }
                        return parts[0];
                    }
                }
                throw new Exception(string.IsNullOrEmpty(res.LastError) ? "Could not download this track (live stream or unavailable?)." : res.LastError);
            }
            finally { DownloadGate.Release(); }
        }

        /// <summary>Gets a direct, progressive (audio+video) stream URL that Windows can play.</summary>
        public static async Task<string> GetVideoUrlAsync(string url, CancellationToken ct)
        {
            string fmt = "best[ext=mp4][vcodec!=none][acodec!=none][height<=1080]/18/best[vcodec!=none][acodec!=none]";
            var res = await RunAsync("-f " + Q(fmt) + " --no-playlist --no-warnings -g " + Q(url), null, ct);
            var u = res.Out.FirstOrDefault(Util.IsUrl);
            if (u == null) throw new Exception(string.IsNullOrEmpty(res.LastError) ? "No playable video format found." : res.LastError);
            return u;
        }

        public static async Task<string> UpdateAsync()
        {
            var res = await RunAsync("-U", null, CancellationToken.None);
            return string.Join("\n", res.Out.Concat(new[] { res.LastError }).Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        static IEnumerable<string> SafeFiles(string pattern)
        {
            try { return Directory.GetFiles(AppPaths.CacheDir, pattern); } catch { return new string[0]; }
        }

        static void Touch(string f) { try { File.SetLastAccessTimeUtc(f, DateTime.UtcNow); File.SetLastWriteTimeUtc(f, DateTime.UtcNow); } catch { } }

        /// <summary>Deletes the oldest cached downloads above the size limit.</summary>
        public static void TrimCache(long limitMb, string keep = null)
        {
            try
            {
                var files = new DirectoryInfo(AppPaths.CacheDir).GetFiles().OrderBy(f => f.LastWriteTimeUtc).ToList();
                long total = files.Sum(f => f.Length);
                long limit = Math.Max(50, limitMb) * 1024L * 1024L;
                foreach (var f in files)
                {
                    if (total <= limit) break;
                    if (keep != null && string.Equals(f.FullName, keep, StringComparison.OrdinalIgnoreCase)) continue;
                    try { long len = f.Length; f.Delete(); total -= len; } catch { }
                }
            }
            catch { }
        }

        public static long CacheSizeMb()
        {
            try { return new DirectoryInfo(AppPaths.CacheDir).GetFiles().Sum(f => f.Length) / (1024 * 1024); } catch { return 0; }
        }
    }
}
