using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using RetroAmp.Core;

namespace RetroAmp.UI
{
    /// <summary>
    /// A skin: a named set of colours. Every property can be overridden from a .ini file
    /// ("Key = #RRGGBB") placed in the Skins folder.
    /// </summary>
    public sealed class ThemeDef
    {
        public string Name = "Retro Night";
        public string Author = "RetroAmp";
        public string Window = "#14161D";
        public string Face1 = "#343849", Face2 = "#23262F", Face3 = "#1A1C23";
        public string BevelLight = "#5A6079", BevelDark = "#0B0C10";
        public string Title1 = "#3E4458", Title2 = "#2A2E3C", Title3 = "#20232D";
        public string TitleText = "#E9C46A", TitleStripe = "#5A4A2A";
        public string Label = "#9AA3BF", Dim = "#6B7390";
        public string Lcd1 = "#06100A", Lcd2 = "#030805", Lcd3 = "#020503";
        public string LcdText = "#4CFF6A", LcdAccent = "#FFB547";
        public string Button1 = "#4A5068", Button2 = "#363A4C", Button3 = "#2A2D3B";
        public string Glyph = "#D9DDEA", GlyphActive = "#FFB547";
        public string LedOn = "#7CFF4F";
        public string Groove = "#0A0B0F";
        public string Seek1 = "#B86E14", Seek2 = "#FFB547";
        public string Thumb1 = "#C9CEDD", Thumb2 = "#8F95AA", Thumb3 = "#6B7086";
        public string ListBg = "#000000", ListText = "#00E83A", ListCurrent = "#FFFFFF", ListSelect = "#1A2F80", ListFailed = "#A04040";

        public ThemeDef Clone() => (ThemeDef)MemberwiseClone();

        public static IEnumerable<FieldInfo> ColorFields =>
            typeof(ThemeDef).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType == typeof(string));

        public string ToIni()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; RetroAmp skin. Colours are #RRGGBB or #AARRGGBB. Save as <name>.ini in the Skins folder.");
            sb.AppendLine("[Skin]");
            foreach (var f in ColorFields) sb.AppendLine(f.Name + " = " + f.GetValue(this));
            return sb.ToString();
        }

        public static ThemeDef FromIni(string path)
        {
            var t = new ThemeDef { Name = Path.GetFileNameWithoutExtension(path), Author = "" };
            var fields = ColorFields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") && !line.Contains("=") || line.StartsWith("[")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                if (!fields.TryGetValue(k, out var f)) continue;
                if (f.Name != "Name" && f.Name != "Author")
                {
                    try { ColorConverter.ConvertFromString(v); } catch { continue; }
                }
                f.SetValue(t, v);
            }
            return t;
        }
    }

    public static class Theme
    {
        public static ThemeDef Current { get; private set; } = new ThemeDef();
        public static event Action Changed;

        public static SolidColorBrush B(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
        public static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        public static string Hex(Color c) => string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);

        public static Brush WindowBg, PanelFace, BevelLight, BevelDark, TitleBar, TitleText, TitleStripe, LabelText, DimText;
        public static Brush LcdBg, LcdOn, LcdOff, LcdText, LcdDim, LcdAmber;
        public static Brush ButtonFace, ButtonFaceHover, ButtonFacePressed, Glyph, GlyphActive, LedOn, LedOff;
        public static Brush Groove, SeekFill, ThumbFace;
        public static Brush PlaylistBg, PlaylistText, PlaylistCurrent, PlaylistFailed, PlaylistSelect, PlaylistHover;
        public static Color LcdBgColor, LcdTextColor, AccentColor;

        static Theme() { Apply(new ThemeDef()); }

        public static void Apply(ThemeDef t)
        {
            Current = t;
            WindowBg = B(t.Window);
            PanelFace = Grad(t.Face1, t.Face2, t.Face3);
            BevelLight = B(t.BevelLight);
            BevelDark = B(t.BevelDark);
            TitleBar = Grad(t.Title1, t.Title2, t.Title3);
            TitleText = B(t.TitleText);
            TitleStripe = B(t.TitleStripe);
            LabelText = B(t.Label);
            DimText = B(t.Dim);

            LcdBgColor = C(t.Lcd2);
            LcdTextColor = C(t.LcdText);
            AccentColor = C(t.LcdAccent);
            LcdBg = Grad(t.Lcd1, t.Lcd2, t.Lcd3);
            LcdOn = B(t.LcdText);
            LcdText = B(t.LcdText);
            LcdOff = B(Mix(LcdBgColor, LcdTextColor, 0.13));
            LcdDim = B(Mix(LcdBgColor, LcdTextColor, 0.5));
            LcdAmber = B(t.LcdAccent);

            ButtonFace = Grad(t.Button1, t.Button2, t.Button3);
            ButtonFaceHover = Grad(Hex(Lighten(C(t.Button1), 0.12)), Hex(Lighten(C(t.Button2), 0.12)), Hex(Lighten(C(t.Button3), 0.12)));
            ButtonFacePressed = Grad(Hex(Lighten(C(t.Button3), -0.25)), Hex(Lighten(C(t.Button2), -0.2)), Hex(Lighten(C(t.Button1), -0.2)));
            Glyph = B(t.Glyph);
            GlyphActive = B(t.GlyphActive);
            LedOn = B(t.LedOn);
            LedOff = B(Mix(C(t.Button3), C(t.LedOn), 0.12));
            Groove = B(t.Groove);
            SeekFill = Horizontal(t.Seek1, t.Seek2);
            ThumbFace = Grad(t.Thumb1, t.Thumb2, t.Thumb3);

            PlaylistBg = B(t.ListBg);
            PlaylistText = B(t.ListText);
            PlaylistCurrent = B(t.ListCurrent);
            PlaylistFailed = B(t.ListFailed);
            PlaylistSelect = B(t.ListSelect);
            PlaylistHover = B(Mix(C(t.ListBg), C(t.ListSelect), 0.45));
            Changed?.Invoke();
        }

        public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

        public static Color Lighten(Color c, double amount) =>
            amount >= 0 ? Mix(c, Colors.White, amount) : Mix(c, Colors.Black, -amount);

        public static readonly FontFamily UiFont = new FontFamily("Segoe UI");
        public static readonly FontFamily LcdFont = new FontFamily("Consolas, Lucida Console, Courier New");
        static readonly Dictionary<string, Typeface> faces = new Dictionary<string, Typeface>();

        static Brush Grad(string a, string b, string c)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            g.GradientStops.Add(new GradientStop(C(a), 0));
            g.GradientStops.Add(new GradientStop(C(b), 0.55));
            g.GradientStops.Add(new GradientStop(C(c), 1));
            g.Freeze();
            return g;
        }

        public static Brush Horizontal(string a, string b)
        {
            var g = new LinearGradientBrush(C(a), C(b), 0);
            g.Freeze();
            return g;
        }

        public static Pen P(Brush b, double w)
        {
            var p = new Pen(b, w);
            p.Freeze();
            return p;
        }

        public static FormattedText Text(Visual v, string s, double size, Brush brush, bool bold = false, FontFamily font = null)
        {
            font = font ?? UiFont;
            string key = font.Source + (bold ? "B" : "");
            if (!faces.TryGetValue(key, out var tf))
                faces[key] = tf = new Typeface(font, FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            double ppd = 1.0;
            try { ppd = VisualTreeHelper.GetDpi(v).PixelsPerDip; } catch { }
            return new FormattedText(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, size, brush, ppd);
        }

        public static Color Hsv(double h, double s, double v, byte a = 255)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
            double r = 0, g = 0, bl = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; bl = x; }
            else if (h < 240) { g = x; bl = c; }
            else if (h < 300) { r = x; bl = c; }
            else { r = c; bl = x; }
            return Color.FromArgb(a, (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((bl + m) * 255));
        }

        /// <summary>Classic analyzer color: green at the bottom, through yellow, to red at the top.</summary>
        public static Color SpectrumColor(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            if (t < 0.5) return Color.FromRgb((byte)(40 + t * 2 * 215), 230, 40);
            return Color.FromRgb(255, (byte)(230 - (t - 0.5) * 2 * 200), 30);
        }

        /// <summary>Draws a raised (or sunken) bevelled rectangle.</summary>
        public static void Bevel(DrawingContext dc, Rect r, Brush face, bool sunken = false)
        {
            dc.DrawRectangle(face, null, r);
            var light = P(sunken ? BevelDark : BevelLight, 1);
            var dark = P(sunken ? BevelLight : BevelDark, 1);
            dc.DrawLine(light, new Point(r.Left + 0.5, r.Top + 0.5), new Point(r.Right - 0.5, r.Top + 0.5));
            dc.DrawLine(light, new Point(r.Left + 0.5, r.Top + 0.5), new Point(r.Left + 0.5, r.Bottom - 0.5));
            dc.DrawLine(dark, new Point(r.Left + 0.5, r.Bottom - 0.5), new Point(r.Right - 0.5, r.Bottom - 0.5));
            dc.DrawLine(dark, new Point(r.Right - 0.5, r.Top + 0.5), new Point(r.Right - 0.5, r.Bottom - 0.5));
        }

        /// <summary>Re-renders every custom-drawn element below a visual.</summary>
        public static void InvalidateTree(DependencyObject d)
        {
            if (d == null) return;
            (d as UIElement)?.InvalidateVisual();
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++) InvalidateTree(VisualTreeHelper.GetChild(d, i));
        }
    }

    /// <summary>Built-in skins and user skins (*.ini in the Skins folders).</summary>
    public static class Skins
    {
        public static readonly List<ThemeDef> BuiltIn = new List<ThemeDef>
        {
            new ThemeDef(),
            new ThemeDef
            {
                Name = "Classic 2000", Window = "#101018",
                Face1 = "#4A4B63", Face2 = "#38394C", Face3 = "#2B2C3B", BevelLight = "#7D7F9C", BevelDark = "#0E0E16",
                Title1 = "#5C5E7C", Title2 = "#45475F", Title3 = "#35364A", TitleText = "#F2D27A", TitleStripe = "#8C7A3E",
                Label = "#C4C6DA", Dim = "#8A8CA6",
                Lcd1 = "#000000", Lcd2 = "#000000", Lcd3 = "#000000", LcdText = "#00E000", LcdAccent = "#E8E800",
                Button1 = "#D3D4E2", Button2 = "#AEB0C6", Button3 = "#8E90A8", Glyph = "#20212E", GlyphActive = "#C24A00", LedOn = "#00E000",
                Groove = "#121219", Seek1 = "#6E7090", Seek2 = "#C9CBDD", Thumb1 = "#E6E7F0", Thumb2 = "#B5B7CA", Thumb3 = "#8E90A8",
                ListBg = "#000000", ListText = "#00E000", ListCurrent = "#FFFFFF", ListSelect = "#0000C6", ListFailed = "#A04040"
            },
            new ThemeDef
            {
                Name = "Silver Audio", Window = "#9AA3AF",
                Face1 = "#F1F3F6", Face2 = "#D5DAE1", Face3 = "#BCC3CD", BevelLight = "#FFFFFF", BevelDark = "#6E7784",
                Title1 = "#4C82BF", Title2 = "#2F5F96", Title3 = "#244C7A", TitleText = "#FFFFFF", TitleStripe = "#5F92CC",
                Label = "#2A3340", Dim = "#5D6775",
                Lcd1 = "#0D3358", Lcd2 = "#0A2A4A", Lcd3 = "#071E36", LcdText = "#86E8FF", LcdAccent = "#FFD36E",
                Button1 = "#FFFFFF", Button2 = "#E3E7ED", Button3 = "#C5CCD6", Glyph = "#25303E", GlyphActive = "#1E6FD0", LedOn = "#2FA8FF",
                Groove = "#8E97A4", Seek1 = "#2B78D0", Seek2 = "#7FC2FF", Thumb1 = "#FFFFFF", Thumb2 = "#D9DEE5", Thumb3 = "#A9B2BF",
                ListBg = "#0B1A2A", ListText = "#9FD8FF", ListCurrent = "#FFFFFF", ListSelect = "#2A5DA0", ListFailed = "#E07070"
            },
            new ThemeDef
            {
                Name = "Amber Hi-Fi", Window = "#0E0B08",
                Face1 = "#3A2F25", Face2 = "#261E17", Face3 = "#1A140F", BevelLight = "#6B5845", BevelDark = "#070504",
                Title1 = "#4A3B2C", Title2 = "#33281E", Title3 = "#251C15", TitleText = "#FFB347", TitleStripe = "#6A4A22",
                Label = "#C9AE8C", Dim = "#8A7357",
                Lcd1 = "#140B02", Lcd2 = "#0C0601", Lcd3 = "#060300", LcdText = "#FFB000", LcdAccent = "#FF5E3A",
                Button1 = "#5A4936", Button2 = "#413426", Button3 = "#30261B", Glyph = "#F3DFC2", GlyphActive = "#FFB000", LedOn = "#FFB000",
                Groove = "#0A0705", Seek1 = "#A35A00", Seek2 = "#FFC24A", Thumb1 = "#E8D3B5", Thumb2 = "#B89C78", Thumb3 = "#8A7357",
                ListBg = "#080502", ListText = "#FFB000", ListCurrent = "#FFFFFF", ListSelect = "#6A3A00", ListFailed = "#A04040"
            },
            new ThemeDef
            {
                Name = "Sakura", Window = "#D9A8BC",
                Face1 = "#FFF2F7", Face2 = "#F7D9E5", Face3 = "#EEC3D4", BevelLight = "#FFFFFF", BevelDark = "#B07A90",
                Title1 = "#F48FB8", Title2 = "#E46E9E", Title3 = "#D55A8C", TitleText = "#FFFFFF", TitleStripe = "#F7A9C8",
                Label = "#7A3352", Dim = "#A76A85",
                Lcd1 = "#3A1426", Lcd2 = "#2B0E1C", Lcd3 = "#1F0914", LcdText = "#FF9CCB", LcdAccent = "#FFE08A",
                Button1 = "#FFFFFF", Button2 = "#FBE4EE", Button3 = "#F0C8D8", Glyph = "#7A2B4E", GlyphActive = "#E2427F", LedOn = "#FF5C9E",
                Groove = "#C995AB", Seek1 = "#E2427F", Seek2 = "#FF9CCB", Thumb1 = "#FFFFFF", Thumb2 = "#F4D2E0", Thumb3 = "#D7A3B8",
                ListBg = "#2B0E1C", ListText = "#FFB3D6", ListCurrent = "#FFFFFF", ListSelect = "#8E2A5A", ListFailed = "#C06060"
            },
            new ThemeDef
            {
                Name = "NhảmStudio", Author = "NhảmStudio", Window = "#0E2F52",
                Face1 = "#2C7BC4", Face2 = "#2068AB", Face3 = "#185389", BevelLight = "#6FB0EA", BevelDark = "#0A2440",
                Title1 = "#F7A04A", Title2 = "#F28C28", Title3 = "#D9741A", TitleText = "#FFFFFF", TitleStripe = "#FFC07A",
                Label = "#E6F1FC", Dim = "#A9CBEB",
                Lcd1 = "#0C2238", Lcd2 = "#081A2C", Lcd3 = "#051220", LcdText = "#FF9A3C", LcdAccent = "#7CC4FF",
                Button1 = "#3A8AD3", Button2 = "#2A74B8", Button3 = "#205E98", Glyph = "#FFFFFF", GlyphActive = "#FFB066", LedOn = "#FF9A3C",
                Groove = "#0A2440", Seek1 = "#E8771A", Seek2 = "#FFB066", Thumb1 = "#FFFFFF", Thumb2 = "#D7E7F7", Thumb3 = "#9CC2E6",
                ListBg = "#061626", ListText = "#FFB066", ListCurrent = "#FFFFFF", ListSelect = "#2272B9", ListFailed = "#E07070"
            },
            new ThemeDef
            {
                Name = "Matrix", Window = "#000000",
                Face1 = "#0E1A10", Face2 = "#08110A", Face3 = "#050B06", BevelLight = "#1F4A26", BevelDark = "#000000",
                Title1 = "#12301A", Title2 = "#0B2011", Title3 = "#06160B", TitleText = "#5CFF7A", TitleStripe = "#1E5A2A",
                Label = "#4FD86A", Dim = "#2F8A40",
                Lcd1 = "#000000", Lcd2 = "#000000", Lcd3 = "#000000", LcdText = "#39FF5A", LcdAccent = "#C8FF3A",
                Button1 = "#163A1E", Button2 = "#0F2A15", Button3 = "#0A1E0F", Glyph = "#7CFF92", GlyphActive = "#D4FF4A", LedOn = "#39FF5A",
                Groove = "#000000", Seek1 = "#1A7A2C", Seek2 = "#5CFF7A", Thumb1 = "#5CFF7A", Thumb2 = "#2FA846", Thumb3 = "#1A6A2A",
                ListBg = "#000000", ListText = "#39FF5A", ListCurrent = "#D4FF4A", ListSelect = "#0F4A1C", ListFailed = "#A04040"
            },
        };

        public static IEnumerable<string> Folders => new[] { Path.Combine(AppPaths.DataDir, "Skins"), Path.Combine(AppPaths.AppDir, "Skins") };
        public static string UserFolder => Path.Combine(AppPaths.DataDir, "Skins");

        public static List<ThemeDef> All()
        {
            var list = new List<ThemeDef>(BuiltIn);
            foreach (var dir in Folders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir, "*.ini").OrderBy(x => x))
                    {
                        try
                        {
                            var t = ThemeDef.FromIni(f);
                            if (!list.Any(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase))) list.Add(t);
                        }
                        catch (Exception ex) { AppPaths.Log("Skin " + f + ": " + ex.Message); }
                    }
                }
                catch { }
            }
            return list;
        }

        public static ThemeDef Find(string name) =>
            All().FirstOrDefault(t => t.Name.Equals(name ?? "", StringComparison.OrdinalIgnoreCase)) ?? BuiltIn[0];
    }
}
