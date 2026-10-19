using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace MorseApp.Controls
{
    /// <summary>
    /// Live "key scope": a scrolling logic-analyser style trace of the key line.
    /// Time flows right to left; the right edge is NOW. A raised bar means the key is down.
    /// Vertical gridlines are drawn every Morse unit (1 unit = 1200 / WPM ms), so a dit should be
    /// one cell wide and a dah three, which is exactly what a learner needs to see while adjusting speed.
    /// </summary>
    public sealed class KeyScope : FrameworkElement
    {
        private struct Segment
        {
            public double Start;   // ms on the Stopwatch clock
            public double End;     // NaN while the key is still down
            public int Kind;       // 0 = unclassified, 1 = dit, 2 = dah, 3 = invalid
        }

        private readonly List<Segment> _segments = new();
        private int _openIndex = -1;
        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        /// <summary>Length of one Morse unit in milliseconds.</summary>
        public double UnitMs { get; set; } = 80;

        /// <summary>How many units of history the trace shows.</summary>
        public double WindowUnits { get; set; } = 42;

        /// <summary>Classifies an in-progress press (duration in ms) as 1 dit, 2 dah, anything else invalid.</summary>
        public Func<double, int>? LiveClassifier { get; set; }

        public KeyScope()
        {
            SnapsToDevicePixels = true;
            Loaded += (_, _) => CompositionTarget.Rendering += OnFrame;
            Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
        }

        private static double NowMs() => Stopwatch.GetTimestamp() * TicksToMs;
        public static double ToMs(long timestamp) => timestamp * TicksToMs;

        /// <summary>Key went down at the given Stopwatch timestamp (default: now).</summary>
        public void Begin(long timestamp = 0)
        {
            double t = timestamp == 0 ? NowMs() : ToMs(timestamp);
            if (_openIndex >= 0) End(0, timestamp);
            _segments.Add(new Segment { Start = t, End = double.NaN, Kind = 0 });
            _openIndex = _segments.Count - 1;
        }

        /// <summary>Key came up; kind 1 = dit, 2 = dah, anything else is drawn as invalid.</summary>
        public void End(int kind, long timestamp = 0)
        {
            if (_openIndex < 0) return;
            double t = timestamp == 0 ? NowMs() : ToMs(timestamp);
            var s = _segments[_openIndex];
            s.End = t;
            s.Kind = kind is 1 or 2 ? kind : 3;
            _segments[_openIndex] = s;
            _openIndex = -1;
        }

        public bool IsDown => _openIndex >= 0;

        private void OnFrame(object? sender, EventArgs e)
        {
            if (!IsVisible) return;
            InvalidateVisual();
        }

        private Brush Res(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 20 || h < 20) return;

            var well = Res("Well", Brushes.Black);
            var line = Res("Line", Brushes.Gray);
            var muted = Res("Muted", Brushes.Gray);
            var signal = Res("Signal", Brushes.Orange);
            var ice = Res("Ice", Brushes.DodgerBlue);
            var alert = Res("Alert", Brushes.Red);
            var text = Res("Text", Brushes.White);

            double now = NowMs();
            double windowMs = Math.Max(500, UnitMs * WindowUnits);
            double pxPerMs = (w - 14) / windowMs;
            double xNow = w - 8;
            double X(double t) => xNow - (now - t) * pxPerMs;

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h), 12, 12));
            dc.DrawRoundedRectangle(well, new Pen(line, 1), new Rect(0.5, 0.5, w - 1, h - 1), 12, 12);

            double top = 22, bottom = h - 12;           // high / low rails of the trace
            double barH = bottom - top;

            // Unit gridlines, fixed to the clock so they scroll with the trace.
            var gridPen = new Pen(line, 1) { DashStyle = DashStyles.Dot };
            double firstUnit = Math.Ceiling((now - windowMs) / UnitMs) * UnitMs;
            for (double t = firstUnit; t <= now; t += UnitMs)
            {
                double x = Math.Round(X(t)) + 0.5;
                if (x < 2 || x > w - 2) continue;
                dc.DrawLine(gridPen, new Point(x, top - 6), new Point(x, bottom + 4));
            }

            // Baseline (key up level)
            dc.DrawLine(new Pen(muted, 1) { }, new Point(4, bottom + 0.5), new Point(w - 4, bottom + 0.5));

            // Pressed segments
            for (int i = _segments.Count - 1; i >= 0; i--)
            {
                var s = _segments[i];
                double end = double.IsNaN(s.End) ? now : s.End;
                if (end < now - windowMs - 50)
                {
                    _segments.RemoveRange(0, i + 1);
                    _openIndex = _openIndex >= 0 ? _openIndex - (i + 1) : -1;
                    break;
                }
            }
            foreach (var s in _segments)
            {
                bool open = double.IsNaN(s.End);
                double end = open ? now : s.End;
                double x0 = Math.Max(2, X(s.Start)), x1 = Math.Min(w - 2, X(end));
                if (x1 <= x0) continue;
                int kind = s.Kind;
                if (open) kind = LiveClassifier?.Invoke(end - s.Start) ?? 0;
                Brush fill = kind switch { 1 => signal, 2 => ice, 3 => alert, _ => text };
                var r = new Rect(x0, top, Math.Max(3, x1 - x0), barH);
                dc.DrawRoundedRectangle(fill, null, r, 5, 5);
                if (open)
                    dc.DrawRoundedRectangle(null, new Pen(text, 1.5), r, 5, 5);
            }

            // NOW marker
            dc.DrawLine(new Pen(text, 1.5) { DashStyle = DashStyles.Dash }, new Point(xNow + 4, 6), new Point(xNow + 4, h - 6));

            // Legend: ghost dit (1u) and dah (3u) so the target lengths are always visible
            double ppu = UnitMs * pxPerMs;
            double lx = 12, ly = 5, lh = 8;
            var ghost = new Pen(muted, 1);
            dc.DrawRoundedRectangle(null, ghost, new Rect(lx, ly, Math.Max(3, ppu), lh), 3, 3);
            DrawLabel(dc, "dit 1u", muted, lx + Math.Max(3, ppu) + 6, ly - 3);
            double dx = lx + Math.Max(3, ppu) + 56;
            dc.DrawRoundedRectangle(null, ghost, new Rect(dx, ly, Math.Max(6, ppu * 3), lh), 3, 3);
            DrawLabel(dc, "dah 3u", muted, dx + Math.Max(6, ppu * 3) + 6, ly - 3);

            dc.Pop();
        }

        private void DrawLabel(DrawingContext dc, string s, Brush brush, double x, double y)
        {
            var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas"), 10, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
