using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace RetroAmp.UI
{
    public static class Theme
    {
        public static SolidColorBrush B(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
        public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        public static readonly Brush WindowBg = B("#14161D");
        public static readonly Brush PanelFace = Grad("#343849", "#23262F", "#1A1C23");
        public static readonly Brush BevelLight = B("#5A6079");
        public static readonly Brush BevelDark = B("#0B0C10");
        public static readonly Brush TitleBar = Grad("#3E4458", "#2A2E3C", "#20232D");
        public static readonly Brush TitleBarActive = Grad("#4A3B22", "#3A2E1C", "#2A2216");
        public static readonly Brush TitleText = B("#E9C46A");
        public static readonly Brush TitleStripe = B("#5A4A2A");
        public static readonly Brush LabelText = B("#9AA3BF");
        public static readonly Brush DimText = B("#6B7390");

        public static readonly Brush LcdBg = Grad("#06100A", "#030805", "#020503");
        public static readonly Brush LcdBorder = B("#0E1A12");
        public static readonly Brush LcdOn = B("#4CFF6A");
        public static readonly Brush LcdOff = B("#0F2716");
        public static readonly Brush LcdText = B("#5CFF7A");
        public static readonly Brush LcdDim = B("#2A8A3E");
        public static readonly Brush LcdAmber = B("#FFB547");

        public static readonly Brush ButtonFace = Grad("#4A5068", "#363A4C", "#2A2D3B");
        public static readonly Brush ButtonFaceHover = Grad("#5A6180", "#43485E", "#33374A");
        public static readonly Brush ButtonFacePressed = Grad("#1E2029", "#272A36", "#2E3140");
        public static readonly Brush Glyph = B("#D9DDEA");
        public static readonly Brush GlyphActive = B("#FFB547");
        public static readonly Brush LedOn = B("#7CFF4F");
        public static readonly Brush LedOff = B("#1C2A1C");

        public static readonly Brush Groove = B("#0A0B0F");
        public static readonly Brush SeekFill = Horizontal("#B86E14", "#FFB547");
        public static readonly Brush ThumbFace = Grad("#C9CEDD", "#8F95AA", "#6B7086");

        public static readonly Brush PlaylistBg = B("#000000");
        public static readonly Brush PlaylistText = B("#00E83A");
        public static readonly Brush PlaylistCurrent = B("#FFFFFF");
        public static readonly Brush PlaylistFailed = B("#A04040");
        public static readonly Brush PlaylistSelect = B("#1A2F80");

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
    }
}
