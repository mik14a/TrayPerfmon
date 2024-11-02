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
    public class DiskQueueMonitor : NotifyIconPlugin
    {
        protected override Lazy<PerformanceCounter>[] Factories => _factories;

        protected override string BalloonTipText {
            get {
                var average = _queue.Select(q => q.Average()).ToArray();
                return $"Avg. Disk Queue Length Read [{average[0]}]/ Write [{average[1]}]";
            }
        }

        const int FramesPerSecond = 15;
        const int Samples = FramesPerSecond * 2;

        DiskQueueMonitor()
            : base(2, 1000 / FramesPerSecond) {
            _queue = new Queue<float>[2];
            for (var rw = 0; rw < 2; ++rw) {
                _queue[rw] = new Queue<float>();
                for (var i = 0; i < Samples; ++i) {
                    _queue[rw].Enqueue(0f);
                }
            }
        }

        protected override void Clear(Graphics graphics) {
            graphics.Clear(Color.Black);
        }

        protected override void Draw(Graphics graphics, float[] value) {
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

                var pen = 3f < average ? Pens.Red : 1f < average ? Pens.Yellow : Pens.Lime;
                var points = Enumerable.Range(start, count).Reverse().Zip(disp, (x, y) => new PointF(x, center + y * delta)).ToArray();
                graphics.DrawLines(pen, points);
            }
        }

        readonly Queue<float>[] _queue;

        static DiskQueueMonitor() {
            _factories = new Lazy<PerformanceCounter>[] {
                new(() => new PerformanceCounter("PhysicalDisk", "Avg. Disk Read Queue Length", "_Total", true)),
                new(() => new PerformanceCounter("PhysicalDisk", "Avg. Disk Write Queue Length", "_Total", true)),
            };
        }

        static readonly Lazy<PerformanceCounter>[] _factories;
    }
}
