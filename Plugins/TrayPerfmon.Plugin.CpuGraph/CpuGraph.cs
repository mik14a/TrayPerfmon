using System;
using System.Collections.Generic;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace TrayPerfmon.Plugin.CpuGraph
{
    [Plugin("CpuGraph", "Draw % processor time")]
    public class CpuGraph : NotifyIconPlugin
    {
        // Dark-editor palette (Dracula accents — higher contrast than One Dark).
        public string Low { get; set; } = "#50fa7b";
        public string Middle { get; set; } = "#f1fa8c";
        public string High { get; set; } = "#ff5555";
        public string Fill { get; set; } = "#08000000";

        protected override string BalloonTipText => $"CPU Usage: {Value:0.0}%";
        protected float Value { get; private set; }

        const int FramesPerSecond = 15;
        const int Samples = FramesPerSecond / 2;

        public CpuGraph()
            : base(1000 / FramesPerSecond) {
            var queue = Enumerable.Range(0, Environment.ProcessorCount).Select(_ => new Queue<float>());
            _value = [.. queue];
        }

        protected override void ApplySettings() {
            const string categoryName = "Processor";
            const string counterName = "% Processor Time";
            foreach (var counter in _performanceCounter) {
                counter.Dispose();
            }
            _performanceCounter = Enumerable.Range(0, Environment.ProcessorCount)
                .Select(index => new PerformanceCounter(categoryName, counterName, index.ToString(), true))
                .ToArray();
            foreach (var range in _range) {
                range.Value.Dispose();
            }
            var converter = new ColorConverter();
            _range = [
                new(50, new SolidBrush((Color)converter.ConvertFrom(Low))),
                new(75, new SolidBrush((Color)converter.ConvertFrom(Middle))),
                new(100, new SolidBrush((Color)converter.ConvertFrom(High)))
            ];
            _fill = (Color)converter.ConvertFrom(Fill);
        }

        protected override void Clear(Graphics graphics) {
            graphics.Clear(_fill);
        }

        protected override void Draw(Graphics graphics) {
            var value = _performanceCounter.Select(counter => counter.NextValue()).ToArray();
            for (var i = 0; i < value.Length; ++i) {
                _value[i].Enqueue(value[i]);
                while (_value[i].Count > Samples) _value[i].Dequeue();
            }
            Value = _value.Select(queue => queue.Average()).Average();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var size = IconSize;
            var width = (float)size / _value.Count;
            for (var i = 0; i < _value.Count; ++i) {
                var x = (float)size * i / _value.Count;
                var average = _value[i].Average();
                var height = size * average / 100f;
                var y = size - height;
                var brush = _range.First(range => average <= range.Key).Value;
                graphics.FillRectangle(brush, x, y, width, height);
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

        readonly List<Queue<float>> _value;

        PerformanceCounter[] _performanceCounter = [];
        KeyValuePair<int, Brush>[] _range = [];
        Color _fill;
    }
}
