using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RetroAmp.UI
{
    /// <summary>Striped retro title bar that drags its window; hosts small buttons on the right.</summary>
    public sealed class TitleBar : Grid
    {
        readonly string title;
        readonly StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 3, 0) };
        public event Action DoubleClicked;

        public TitleBar(string title, double height = 18)
        {
            this.title = title;
            Height = height;
            Children.Add(new TitleBarBackground(title));
            Children.Add(buttons);
            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { DoubleClicked?.Invoke(); return; }
                var w = Window.GetWindow(this);
                try { w?.DragMove(); } catch { }
            };
        }

        public RetroButton AddButton(GlyphKind g, string tip, Action onClick)
        {
            var b = new RetroButton { Glyph = g, Width = 15, Height = 13, ToolTip = tip, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };
            b.Click += onClick;
            buttons.Children.Add(b);
            return b;
        }

        sealed class TitleBarBackground : FrameworkElement
        {
            readonly string title;
            public TitleBarBackground(string t) { title = t; }
            protected override void OnRender(DrawingContext dc)
            {
                double w = ActualWidth, h = ActualHeight;
                Theme.Bevel(dc, new Rect(0, 0, w, h), Theme.TitleBar);
                var ft = Theme.Text(this, title, 10, Theme.TitleText, true);
                double tx = (w - ft.Width) / 2;
                var stripe = Theme.P(Theme.TitleStripe, 1);
                var stripe2 = Theme.P(Theme.BevelDark, 1);
                for (double y = 5; y < h - 4; y += 3)
                {
                    dc.DrawLine(stripe, new Point(6, y), new Point(tx - 8, y));
                    dc.DrawLine(stripe2, new Point(6, y + 1), new Point(tx - 8, y + 1));
                    dc.DrawLine(stripe, new Point(tx + ft.Width + 8, y), new Point(w - 60, y));
                    dc.DrawLine(stripe2, new Point(tx + ft.Width + 8, y + 1), new Point(w - 60, y + 1));
                }
                dc.DrawText(ft, new Point(tx, (h - ft.Height) / 2));
            }
        }
    }

    public static class Ui
    {
        public static TextBlock Label(string text, double size = 9, Brush brush = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = FontWeights.Bold,
                Foreground = brush ?? Theme.LabelText,
                FontFamily = Theme.UiFont,
                IsHitTestVisible = false
            };
        }

        public static T At<T>(this T el, double x, double y) where T : UIElement
        {
            Canvas.SetLeft(el, x);
            Canvas.SetTop(el, y);
            return el;
        }
    }

    /// <summary>Panel background with bevel.</summary>
    public sealed class PanelFace : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc) =>
            Theme.Bevel(dc, new Rect(0, 0, ActualWidth, ActualHeight), Theme.PanelFace);
    }
}
