using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RetroAmp.UI
{
    public enum GlyphKind { None, Prev, Play, Pause, Stop, Next, Eject, Close, Minimize, Menu, Shade }

    /// <summary>Custom-drawn bevelled button with optional glyph, label, toggle LED.</summary>
    public class RetroButton : FrameworkElement
    {
        bool pressed, hover;
        bool isChecked;
        string text;
        public GlyphKind Glyph { get; set; }
        public bool IsToggle { get; set; }
        public double FontSize { get; set; } = 9;
        public bool Flat { get; set; }
        public event Action Click;

        public string Text { get => text; set { text = value; InvalidateVisual(); } }
        public bool IsChecked { get => isChecked; set { isChecked = value; InvalidateVisual(); } }

        public RetroButton()
        {
            Cursor = Cursors.Hand;
            Focusable = false;
            SnapsToDevicePixels = true;
        }

        public static RetroButton Make(GlyphKind g, string text, string tip, double w, double h, Action onClick, bool toggle = false)
        {
            var b = new RetroButton { Glyph = g, Text = text, Width = w, Height = h, IsToggle = toggle };
            if (tip != null) b.ToolTip = tip;
            if (onClick != null) b.Click += onClick;
            return b;
        }

        protected override void OnMouseEnter(MouseEventArgs e) { hover = true; InvalidateVisual(); }
        protected override void OnMouseLeave(MouseEventArgs e) { hover = false; InvalidateVisual(); }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            pressed = true; CaptureMouse(); InvalidateVisual(); e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!pressed) return;
            pressed = false; ReleaseMouseCapture(); InvalidateVisual();
            var p = e.GetPosition(this);
            if (p.X >= 0 && p.Y >= 0 && p.X <= ActualWidth && p.Y <= ActualHeight) Click?.Invoke();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e) { pressed = false; InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            var r = new Rect(0, 0, ActualWidth, ActualHeight);
            if (r.Width < 2 || r.Height < 2) return;
            bool down = pressed || (IsToggle && isChecked && Glyph != GlyphKind.None && text == null);
            Brush face = pressed ? Theme.ButtonFacePressed : (hover ? Theme.ButtonFaceHover : Theme.ButtonFace);
            if (Flat && !hover && !pressed) face = Brushes.Transparent;
            Theme.Bevel(dc, r, face, down);

            Brush glyphBrush = (IsToggle && isChecked) ? Theme.GlyphActive : Theme.Glyph;
            double ox = pressed ? 1 : 0;
            double cx = r.Width / 2 + ox, cy = r.Height / 2 + ox;

            if (IsToggle && text != null)
            {
                // LED on the left
                var led = new Rect(4 + ox, cy - 2, 5, 4);
                dc.DrawRectangle(isChecked ? Theme.LedOn : Theme.LedOff, null, led);
                var ft = Theme.Text(this, text, FontSize, isChecked ? Theme.GlyphActive : Theme.Glyph, true);
                dc.DrawText(ft, new Point(Math.Max(11, (r.Width - ft.Width) / 2 + 4) + ox, cy - ft.Height / 2));
                return;
            }
            if (Glyph != GlyphKind.None) DrawGlyph(dc, Glyph, cx, cy, Math.Min(r.Width, r.Height), glyphBrush);
            if (text != null)
            {
                var ft = Theme.Text(this, text, FontSize, glyphBrush, true);
                dc.DrawText(ft, new Point((r.Width - ft.Width) / 2 + ox, cy - ft.Height / 2));
            }
        }

        public static void DrawGlyph(DrawingContext dc, GlyphKind g, double cx, double cy, double size, Brush brush)
        {
            double s = size * 0.28;
            switch (g)
            {
                case GlyphKind.Play:
                    Poly(dc, brush, new Point(cx - s * 0.7, cy - s), new Point(cx + s, cy), new Point(cx - s * 0.7, cy + s));
                    break;
                case GlyphKind.Pause:
                    dc.DrawRectangle(brush, null, new Rect(cx - s * 0.85, cy - s, s * 0.6, s * 2));
                    dc.DrawRectangle(brush, null, new Rect(cx + s * 0.25, cy - s, s * 0.6, s * 2));
                    break;
                case GlyphKind.Stop:
                    dc.DrawRectangle(brush, null, new Rect(cx - s * 0.85, cy - s * 0.85, s * 1.7, s * 1.7));
                    break;
                case GlyphKind.Prev:
                    dc.DrawRectangle(brush, null, new Rect(cx - s * 1.1, cy - s, s * 0.35, s * 2));
                    Poly(dc, brush, new Point(cx + s * 0.1, cy - s), new Point(cx - s * 0.75, cy), new Point(cx + s * 0.1, cy + s));
                    Poly(dc, brush, new Point(cx + s * 1.0, cy - s), new Point(cx + s * 0.15, cy), new Point(cx + s * 1.0, cy + s));
                    break;
                case GlyphKind.Next:
                    dc.DrawRectangle(brush, null, new Rect(cx + s * 0.75, cy - s, s * 0.35, s * 2));
                    Poly(dc, brush, new Point(cx - s * 0.1, cy - s), new Point(cx + s * 0.75, cy), new Point(cx - s * 0.1, cy + s));
                    Poly(dc, brush, new Point(cx - s * 1.0, cy - s), new Point(cx - s * 0.15, cy), new Point(cx - s * 1.0, cy + s));
                    break;
                case GlyphKind.Eject:
                    Poly(dc, brush, new Point(cx - s, cy + s * 0.25), new Point(cx, cy - s), new Point(cx + s, cy + s * 0.25));
                    dc.DrawRectangle(brush, null, new Rect(cx - s, cy + s * 0.55, s * 2, s * 0.45));
                    break;
                case GlyphKind.Close:
                    {
                        var pen = new Pen(brush, Math.Max(1.2, size * 0.12));
                        dc.DrawLine(pen, new Point(cx - s, cy - s), new Point(cx + s, cy + s));
                        dc.DrawLine(pen, new Point(cx - s, cy + s), new Point(cx + s, cy - s));
                        break;
                    }
                case GlyphKind.Minimize:
                    dc.DrawRectangle(brush, null, new Rect(cx - s, cy + s * 0.4, s * 2, Math.Max(1.5, s * 0.45)));
                    break;
                case GlyphKind.Shade:
                    dc.DrawRectangle(null, new Pen(brush, 1.2), new Rect(cx - s, cy - s * 0.8, s * 2, s * 1.6));
                    dc.DrawRectangle(brush, null, new Rect(cx - s, cy - s * 0.8, s * 2, s * 0.5));
                    break;
                case GlyphKind.Menu:
                    for (int i = -1; i <= 1; i++)
                        dc.DrawRectangle(brush, null, new Rect(cx - s, cy + i * s * 0.7 - s * 0.15, s * 2, Math.Max(1.2, s * 0.3)));
                    break;
            }
        }

        static void Poly(DrawingContext dc, Brush b, params Point[] pts)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(pts[0], true, true);
                for (int i = 1; i < pts.Length; i++) c.LineTo(pts[i], true, false);
            }
            g.Freeze();
            dc.DrawGeometry(b, null, g);
        }
    }
}
