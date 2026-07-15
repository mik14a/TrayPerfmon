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
        const int FramesPerSecond = 15;
        const int Samples = FramesPerSecond / 2;

        // Dark-editor palette (Dracula accents — higher contrast than One Dark).
        public string Low { get; set; } = "#50fa7b";

        public string Middle { get; set; } = "#f1fa8c";

        public string High { get; set; } = "#ff5555";

        public CpuGraph()
            : base(1000 / FramesPerSecond) {
            var queue = Enumerable.Range(0, Environment.ProcessorCount).Select(_ => new Queue<float>());
            _value = new List<Queue<float>>(queue);
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
        }

        protected override void Clear(Graphics graphics) {
            // Per-plugin backdrop alpha (bar chart needs a plate; tune here only).
            graphics.Clear(Color.FromArgb(0x08, 0x00, 0x00, 0x00));
        }

        protected override void Draw(Graphics graphics) {
            var value = _performanceCounter.Select(counter => counter.NextValue()).ToArray();
            for (var i = 0; i < value.Length; ++i) {
                _value[i].Enqueue(value[i]);
                while (_value[i].Count > Samples) _value[i].Dequeue();
            }
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

        PerformanceCounter[] _performanceCounter = [];
        readonly List<Queue<float>> _value;
        KeyValuePair<int, Brush>[] _range = [];
    }
}
