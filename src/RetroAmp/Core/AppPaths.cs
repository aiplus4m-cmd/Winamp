using System;
using System.IO;

namespace RetroAmp.Core
{
    /// <summary>
    /// Resolves where RetroAmp keeps its data. If a file named "portable.dat" sits next to the
    /// executable (the portable ZIP ships with one), everything is stored in ".\Data";
    /// otherwise the per-user AppData folders are used.
    /// </summary>
    public static class AppPaths
    {
        public static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly bool IsPortable;
        public static readonly string DataDir;
        public static readonly string CacheDir;
        public static readonly string ToolsDir;

        static AppPaths()
        {
            IsPortable = File.Exists(Path.Combine(AppDir, "portable.dat"));
            string data = null, local = null;
            if (IsPortable)
            {
                data = Path.Combine(AppDir, "Data");
                if (!TryCreate(data)) data = null;
                else local = data;
            }
            if (data == null)
            {
                data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RetroAmp");
                local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroAmp");
                TryCreate(data);
                TryCreate(local);
            }
            DataDir = data;
            CacheDir = Path.Combine(local, "Cache");
            ToolsDir = Path.Combine(local, "Tools");
            TryCreate(CacheDir);
            TryCreate(ToolsDir);
        }

        public static string SettingsFile => Path.Combine(DataDir, "settings.xml");
        public static string LastPlaylistFile => Path.Combine(DataDir, "last.m3u8");
        public static string LogFile => Path.Combine(DataDir, "retroamp.log");

        static bool TryCreate(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static void Log(string msg)
        {
            try { File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine); }
            catch { }
        }
    }
}
