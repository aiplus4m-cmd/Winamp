using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetroAmp.Core;

namespace RetroAmp.UI
{
    public enum SliderStyle { Seek, Volume, Balance, EqBand, Plain }

    /// <summary>Custom-drawn horizontal/vertical slider (value 0..1).</summary>
    public class RetroSlider : FrameworkElement
    {
        double value;
        bool dragging;
        public SliderStyle Kind { get; set; }
        public Orientation Orientation { get; set; } = Orientation.Horizontal;
        public double DefaultValue { get; set; } = 0.5;
        public double WheelStep { get; set; } = 0.03;
        public bool IsDragging => dragging;
        public bool Enabled { get; set; } = true;
        public Func<double, string> TipFormatter { get; set; }

        /// <summary>(value, fromUser)</summary>
        public event Action<double> ValueChanged;
        public event Action<double> DragCompleted;

        public RetroSlider()
        {
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
            Focusable = false;
        }

        public double Value
        {
            get => value;
            set { double v = Util.Clamp(value, 0, 1); if (Math.Abs(v - this.value) > 1e-6) { this.value = v; InvalidateVisual(); } }
        }

        bool Vertical => Orientation == Orientation.Vertical;
        double ThumbLen => Vertical ? 7 : (Kind == SliderStyle.Seek ? 22 : 12);

        void SetFromUser(double v)
        {
            v = Util.Clamp(v, 0, 1);
            if (Kind == SliderStyle.Balance || Kind == SliderStyle.EqBand)
                if (Math.Abs(v - 0.5) < 0.025) v = 0.5; // snap to centre
            value = v;
            InvalidateVisual();
            ValueChanged?.Invoke(v);
            if (TipFormatter != null) ToolTip = TipFormatter(v);
        }

        double FromPoint(Point p)
        {
            double t = ThumbLen;
            if (Vertical) return 1 - (p.Y - t / 2) / Math.Max(1, ActualHeight - t);
            return (p.X - t / 2) / Math.Max(1, ActualWidth - t);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!Enabled) return;
            if (e.ClickCount == 2 && Kind != SliderStyle.Seek)
            {
                SetFromUser(DefaultValue);
                DragCompleted?.Invoke(value);
                e.Handled = true;
                return;
            }
            dragging = true;
            CaptureMouse();
            SetFromUser(FromPoint(e.GetPosition(this)));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromUser(FromPoint(e.GetPosition(this)));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            ReleaseMouseCapture();
            DragCompleted?.Invoke(value);
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            if (dragging) { dragging = false; DragCompleted?.Invoke(value); }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (!Enabled) return;
            SetFromUser(value + Math.Sign(e.Delta) * WheelStep);
            DragCompleted?.Invoke(value);
            e.Handled = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 2 || h < 2) return;
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // hit-testing
            double t = ThumbLen;

            if (!Vertical)
            {
                double gh = Kind == SliderStyle.Seek ? Math.Min(6, h - 4) : Math.Min(5, h - 4);
                var groove = new Rect(0, (h - gh) / 2, w, gh);
                Theme.Bevel(dc, groove, Theme.Groove, true);
                double tx = value * (w - t);
                Brush fill = null; Rect fr = Rect.Empty;
                switch (Kind)
                {
                    case SliderStyle.Seek:
                        if (Enabled) { fill = Theme.SeekFill; fr = new Rect(1, groove.Top + 1, Math.Max(0, tx + t / 2 - 1), gh - 2); }
                        break;
                    case SliderStyle.Volume:
                    case SliderStyle.Plain:
                        fill = new SolidColorBrush(Kind == SliderStyle.Volume ? Theme.SpectrumColor(value) : Theme.C("#5C8DFF"));
                        fr = new Rect(1, groove.Top + 1, Math.Max(0, tx + t / 2 - 1), gh - 2);
                        break;
                    case SliderStyle.Balance:
                        {
                            double c = w / 2, x = tx + t / 2;
                            fill = new SolidColorBrush(Theme.SpectrumColor(Math.Abs(value - 0.5) * 2));
                            fr = new Rect(Math.Min(c, x), groove.Top + 1, Math.Abs(x - c), gh - 2);
                            dc.DrawRectangle(Theme.DimText, null, new Rect(c - 0.5, groove.Top - 1, 1, gh + 2));
                            break;
                        }
                }
                if (fill != null && !fr.IsEmpty && fr.Width > 0) dc.DrawRectangle(fill, null, fr);
                if (Enabled || Kind != SliderStyle.Seek)
                {
                    double th = Math.Min(h, Kind == SliderStyle.Seek ? 10 : 12);
                    var thumb = new Rect(tx, (h - th) / 2, t, th);
                    Theme.Bevel(dc, thumb, dragging ? Theme.ButtonFaceHover : Theme.ThumbFace);
                    dc.DrawRectangle(Theme.BevelDark, null, new Rect(thumb.Left + t / 2 - 0.5, thumb.Top + 2, 1, th - 4));
                }
            }
            else
            {
                double gw = Math.Min(5, w - 4);
                var groove = new Rect((w - gw) / 2, 0, gw, h);
                Theme.Bevel(dc, groove, Theme.Groove, true);
                double ty = (1 - value) * (h - t);
                double c = h / 2, y = ty + t / 2;
                if (Kind == SliderStyle.EqBand)
                {
                    var col = Theme.SpectrumColor(1 - (y / h));
                    dc.DrawRectangle(new SolidColorBrush(col), null, new Rect(groove.Left + 1, Math.Min(c, y), gw - 2, Math.Abs(y - c)));
                    dc.DrawRectangle(Theme.DimText, null, new Rect(groove.Left - 2, c - 0.5, gw + 4, 1));
                }
                var thumb = new Rect(0, ty, w, t);
                Theme.Bevel(dc, thumb, dragging ? Theme.ButtonFaceHover : Theme.ThumbFace);
                dc.DrawRectangle(Theme.BevelDark, null, new Rect(2, thumb.Top + t / 2 - 0.5, w - 4, 1));
            }
        }
    }
}
