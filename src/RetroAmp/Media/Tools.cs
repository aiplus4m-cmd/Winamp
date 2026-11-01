using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RetroAmp.Core;

namespace RetroAmp.Media
{
    /// <summary>Locates / downloads the helper tools: yt-dlp (required for online sources) and Deno (optional JS runtime that yt-dlp uses for YouTube).</summary>
    public static class Tools
    {
        public const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        public const string DenoUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

        static Tools()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; }
            catch { try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { } }
        }

        public static string YtDlpPath => Find("yt-dlp.exe");
        public static string DenoPath => Find("deno.exe");

        static string Find(string exe)
        {
            foreach (var dir in new[] { AppPaths.ToolsDir, AppPaths.AppDir, Path.Combine(AppPaths.AppDir, "tools") })
            {
                try { string p = Path.Combine(dir, exe); if (File.Exists(p)) return p; } catch { }
            }
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(';').Where(d => d.Length > 0))
            {
                try { string p = Path.Combine(dir.Trim('"'), exe); if (File.Exists(p)) return p; } catch { }
            }
            return null;
        }

        public static async Task DownloadFileAsync(string url, string dest, IProgress<double> progress, CancellationToken ct)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("RetroAmp/1.0");
                using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    resp.EnsureSuccessStatusCode();
                    long total = resp.Content.Headers.ContentLength ?? -1;
                    string tmp = dest + ".part";
                    using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fs = File.Create(tmp))
                    {
                        var buf = new byte[81920];
                        long done = 0; int n;
                        while ((n = await src.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await fs.WriteAsync(buf, 0, n, ct).ConfigureAwait(false);
                            done += n;
                            if (total > 0) progress?.Report(done / (double)total);
                        }
                    }
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(tmp, dest);
                }
            }
        }

        public static async Task<string> InstallYtDlpAsync(IProgress<double> progress, CancellationToken ct = default)
        {
            string dest = Path.Combine(AppPaths.ToolsDir, "yt-dlp.exe");
            await DownloadFileAsync(YtDlpUrl, dest, progress, ct);
            return dest;
        }

        public static async Task<string> InstallDenoAsync(IProgress<double> progress, CancellationToken ct = default)
        {
            string zip = Path.Combine(AppPaths.ToolsDir, "deno.zip");
            await DownloadFileAsync(DenoUrl, zip, progress, ct);
            string dest = Path.Combine(AppPaths.ToolsDir, "deno.exe");
            await Task.Run(() =>
            {
                using (var archive = ZipFile.OpenRead(zip))
                {
                    var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals("deno.exe", StringComparison.OrdinalIgnoreCase));
                    if (entry == null) throw new InvalidDataException("deno.exe not found in archive");
                    if (File.Exists(dest)) File.Delete(dest);
                    entry.ExtractToFile(dest);
                }
                File.Delete(zip);
            });
            return dest;
        }
    }
}
