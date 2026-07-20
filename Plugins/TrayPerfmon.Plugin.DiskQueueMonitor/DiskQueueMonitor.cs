using System;
using System.Collections.Generic;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace TrayPerfmon.Plugin.DiskQueueMonitor
{
    [Plugin("DiskQueueMonitor", "Disk queue monitor")]
    public partial class DiskQueueMonitor : NotifyIconPlugin
    {
        /// <summary>The PerformanceCounter instance name for the PhysicalDisk category (e.g. "_Total" or "0 C:").</summary>
        public string DiskInstance { get; set; } = "_Total";
        // Dark-editor palette (Dracula accents — higher contrast than One Dark).
        public string Low { get; set; } = "#50fa7b";
        public string Middle { get; set; } = "#f1fa8c";
        public string High { get; set; } = "#ff5555";
        public string Fill { get; set; } = "#08000000";

        protected override string BalloonTipText {
            get {
                var average = _queue.Select(q => q.Average()).ToArray();
                return $"[{DiskInstance}] Disk Queue Read / Write: {average[0]:0.00} / {average[1]:0.00}";
            }
        }
        protected override bool HasSettings => true;

        const int FramesPerSecond = 15;
        const int Samples = FramesPerSecond * 2;

        DiskQueueMonitor()
            : base(1000 / FramesPerSecond) {
            _queue = new Queue<float>[2];
            for (var rw = 0; rw < 2; ++rw) {
                _queue[rw] = new Queue<float>();
                for (var i = 0; i < Samples; ++i) {
                    _queue[rw].Enqueue(0f);
                }
            }
        }

        protected override void ApplySettings() {
            foreach (var counter in _performanceCounter) {
                counter.Dispose();
            }
            var instance = DiskInstance ?? "_Total";
            _performanceCounter = [
                new PerformanceCounter("PhysicalDisk", "Avg. Disk Read Queue Length", instance, true),
                new PerformanceCounter("PhysicalDisk", "Avg. Disk Write Queue Length", instance, true),
            ];
            foreach (var range in _range) {
                range.Value.Dispose();
            }
            var converter = new ColorConverter();
            _range = [
                new(0f, new Pen((Color)converter.ConvertFrom(Low))),
                new(1f, new Pen((Color)converter.ConvertFrom(Middle))),
                new(3f, new Pen((Color)converter.ConvertFrom(High)))
            ];
            _fill = (Color)converter.ConvertFrom(Fill);
        }

        protected override void Clear(Graphics graphics) {
            graphics.Clear(_fill);
        }

        protected override void Draw(Graphics graphics) {
            var value = _performanceCounter.Select(counter => counter.NextValue()).ToArray();
            for (var rw = 0; rw < 2; ++rw) {
                _queue[rw].Enqueue(value[rw]);
                while (Samples < _queue[rw].Count) {
                    _ = _queue[rw].Dequeue();
                }
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = graphics.VisibleClipBounds;
            var start = (int)bounds.X;
            var count = (int)bounds.Width;
            for (var rw = 0; rw < 2; ++rw) {
                var average = _queue[rw].Average();
                var limit = Math.Max(1f, average);
                var center = bounds.Height / 2f;
                var delta = rw == 0 ? -center : center;
                var draw = _queue[rw].Reverse().Take(count).Select(q => q / limit);
                var disp = draw.Select(q => float.IsNaN(q) ? 0f : Math.Min(1f, q));
                if (1f < limit) {
                    var one = center + 1f / limit * delta;
                    graphics.DrawLine(Pens.Gray, start, one, start + count, one);
                }

                var pen = _range.Last(range => range.Key <= average).Value;
                var points = Enumerable.Range(start, count).Reverse().Zip(disp, (x, y) => new PointF(x, center + y * delta)).ToArray();
                graphics.DrawLines(pen, points);
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                foreach (var counter in _performanceCounter) {
                    counter.Dispose();
                }
                foreach (var range in _range) {
                    range.Value.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        readonly Queue<float>[] _queue;

        PerformanceCounter[] _performanceCounter = [];
        KeyValuePair<float, Pen>[] _range = [];
        Color _fill;
    }
}
