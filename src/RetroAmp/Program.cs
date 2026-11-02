using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using RetroAmp.Core;
using RetroAmp.Media;
using RetroAmp.UI;

namespace RetroAmp
{
    public static class Program
    {
        const string MutexName = "RetroAmp.SingleInstance.v1";
        const string PipeName = "RetroAmp.Pipe.v1";
        static MainWindow mainWindow;

        [STAThread]
        public static int Main(string[] args)
        {
            int st = Array.IndexOf(args, "--selftest");
            if (st >= 0)
            {
                string dir = st + 1 < args.Length && !args[st + 1].StartsWith("--") ? args[st + 1] : "selftest-out";
                return SelfTest.Run(Path.GetFullPath(dir), args.Contains("--online"));
            }
            using (var mutex = new Mutex(true, MutexName, out bool first))
            {
                if (!first)
                {
                    if (SendToRunningInstance(args)) return 0;
                }

                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.DispatcherUnhandledException += (s, e) =>
                {
                    AppPaths.Log("Unhandled: " + e.Exception);
                    e.Handled = true;
                    try { MessageBox.Show(e.Exception.Message, "RetroAmp", MessageBoxButton.OK, MessageBoxImage.Warning); } catch { }
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => AppPaths.Log("Fatal: " + e.ExceptionObject);
                System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => { AppPaths.Log("Task: " + e.Exception); e.SetObserved(); };

                var settings = Settings.Load();
                YtDlp.CookiesBrowser = settings.CookiesBrowser ?? "";
                try { Theme.Apply(Skins.Find(settings.Skin)); } catch (Exception ex) { AppPaths.Log("Skin: " + ex.Message); }
                TagLoader.Init(app.Dispatcher);
                var player = new Player(settings);
                player.ApplyDsp();

                // restore last session's playlist
                try
                {
                    if (File.Exists(AppPaths.LastPlaylistFile))
                    {
                        player.AddRange(PlaylistIO.Load(AppPaths.LastPlaylistFile));
                        if (settings.LastIndex >= 0 && settings.LastIndex < player.Items.Count)
                            player.MarkCurrent(player.Items[settings.LastIndex]);
                    }
                }
                catch (Exception ex) { AppPaths.Log("Restore playlist failed: " + ex.Message); }

                mainWindow = new MainWindow(player, settings);
                var files = args.Where(a => !a.StartsWith("/") && !a.StartsWith("--")).ToArray();
                if (files.Length > 0)
                    mainWindow.Loaded += (s, e) => OpenArgs(files);
                if (first) StartPipeServer(app.Dispatcher);
                return app.Run(mainWindow);
            }
        }

        static void OpenArgs(string[] files)
        {
            var urls = files.Where(Util.IsUrl).ToList();
            var paths = files.Where(f => !Util.IsUrl(f)).ToList();
            if (paths.Count > 0) mainWindow.AddPaths(paths, true);
            foreach (var u in urls) mainWindow.AddUrl(u, paths.Count == 0 && u == urls[0]);
            if (mainWindow.WindowState == WindowState.Minimized) mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }

        static bool SendToRunningInstance(string[] args)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(1500);
                    var data = Encoding.UTF8.GetBytes(string.Join("\n", args.Select(a => File.Exists(a) || Directory.Exists(a) ? Path.GetFullPath(a) : a)) + "\n\u0004");
                    client.Write(data, 0, data.Length);
                    client.Flush();
                }
                return true;
            }
            catch { return false; }
        }

        static void StartPipeServer(Dispatcher dispatcher)
        {
            var t = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1))
                        {
                            server.WaitForConnection();
                            using (var reader = new StreamReader(server, Encoding.UTF8))
                            {
                                string all = reader.ReadToEnd();
                                var files = all.Split(new[] { '\n', '\u0004' }, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                                dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (files.Length > 0) OpenArgs(files);
                                    else
                                    {
                                        if (mainWindow.WindowState == WindowState.Minimized) mainWindow.WindowState = WindowState.Normal;
                                        mainWindow.Activate();
                                    }
                                }));
                            }
                        }
                    }
                    catch (Exception ex) { AppPaths.Log("Pipe: " + ex.Message); Thread.Sleep(500); }
                }
            }) { IsBackground = true, Name = "SingleInstancePipe" };
            t.Start();
        }
    }
}
