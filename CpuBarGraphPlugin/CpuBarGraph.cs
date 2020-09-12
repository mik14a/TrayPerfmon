using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using TrayPerfmon.Plugin;

namespace CpuBarGraphPlugin
{
    [Export(typeof(INotifyIconPlugin))]
    [ExportMetadata("Description", "Cpu bar graph")]
    class CpuBarGraph : NotifyIconPlugin
    {
        protected override Lazy<PerformanceCounter>[] Factories => _factories;

        CpuBarGraph()
            : base(Environment.ProcessorCount, 1000 / 15) {
            _height = new int[Environment.ProcessorCount];
        }

        protected override void Clear(Graphics graphics, Rectangle rectangle) {
            using (var brush = new SolidBrush(Color.Black)) {
                graphics.FillRectangle(brush, rectangle);
            }
        }

        protected override void Draw(Graphics graphics, float[] next) {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var width = 16f / next.Length;
            for (var i = 0; i < next.Length; ++i) {
                var brush = _range.First(range => next[i] <= range.Key).Value;
                var x = 16f * i / next.Length;
                var value = 16 * next[i] / 100;
                var delta = _height[i] < value ? 1 : value < _height[i] ? -1 : 0;
                var height = _height[i] + delta;
                var y = 16 - height;
                graphics.FillRectangle(brush, x, y, width, height);
                _height[i] = height;
            }
        }

        readonly int[] _height;

        static CpuBarGraph() {
            _factories = Enumerable
                .Range(0, Environment.ProcessorCount)
                .Select(index => new Lazy<PerformanceCounter>(() => CreateProcessorTime(index)))
                .ToArray();

            _range = new KeyValuePair<int, Brush>[] {
                new KeyValuePair<int, Brush>(50, new SolidBrush(Color.Lime)),
                new KeyValuePair<int, Brush>(75, new SolidBrush(Color.Yellow)),
                new KeyValuePair<int, Brush>(100, new SolidBrush(Color.Red))
            };
        }

        static PerformanceCounter CreateProcessorTime(int index) {
            return new PerformanceCounter("Processor", "% Processor Time", index.ToString(), true);
        }

        static readonly Lazy<PerformanceCounter>[] _factories;
        static readonly KeyValuePair<int, Brush>[] _range;
    }
}
