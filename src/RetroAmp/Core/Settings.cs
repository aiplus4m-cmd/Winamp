using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace RetroAmp.Core
{
    public class EqPreset
    {
        public string Name { get; set; }
        public float Preamp { get; set; }
        public float[] Bands { get; set; } = new float[10];
    }

    public class Settings
    {
        public double Volume { get; set; } = 0.8;
        public double Balance { get; set; } = 0.0;
        public bool Shuffle { get; set; }
        public int Repeat { get; set; } // 0 off, 1 all, 2 one

        public bool EqEnabled { get; set; } = true;
        public float Preamp { get; set; }
        public float[] EqBands { get; set; } = new float[10];
        public string EqPresetName { get; set; } = "Flat";
        public List<EqPreset> CustomPresets { get; set; } = new List<EqPreset>();

        public double StereoWidth { get; set; } = 1.0;   // 0..2
        public double Reverb { get; set; } = 0.0;        // 0..1
        public double CrossfadeSeconds { get; set; } = 0.0; // 0..10
        public bool VocalRemover { get; set; }

        public bool ShowEq { get; set; } = true;
        public bool ShowPlaylist { get; set; } = true;
        public double PlaylistHeight { get; set; } = 240;
        public double Scale { get; set; } = 1.25;
        public bool AlwaysOnTop { get; set; }
        public bool ShowRemaining { get; set; }
        public int MiniVisMode { get; set; } = 0;
        public int VisMode { get; set; } = 2;
        public bool ShowVisWindow { get; set; }
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public int AudioDevice { get; set; } = -1;
        public int LastIndex { get; set; } = -1;
        public long CacheLimitMb { get; set; } = 1024;
        public bool AskedForYtDlp { get; set; }
        public string CookiesBrowser { get; set; } = "";

        public static Settings Load()
        {
            try
            {
                if (File.Exists(AppPaths.SettingsFile))
                {
                    using (var fs = File.OpenRead(AppPaths.SettingsFile))
                    {
                        var s = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(fs);
                        if (s.EqBands == null || s.EqBands.Length != 10) s.EqBands = new float[10];
                        if (s.CustomPresets == null) s.CustomPresets = new List<EqPreset>();
                        return s;
                    }
                }
            }
            catch (Exception ex) { AppPaths.Log("Settings load failed: " + ex.Message); }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                string tmp = AppPaths.SettingsFile + ".tmp";
                using (var fs = File.Create(tmp))
                    new XmlSerializer(typeof(Settings)).Serialize(fs, this);
                if (File.Exists(AppPaths.SettingsFile)) File.Delete(AppPaths.SettingsFile);
                File.Move(tmp, AppPaths.SettingsFile);
            }
            catch (Exception ex) { AppPaths.Log("Settings save failed: " + ex.Message); }
        }
    }
}
