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
        protected override Lazy<PerformanceCounter>[] Factories => _factories;

        const int FramesPerSecond = 15;
        const int Samples = FramesPerSecond / 2;

        public string Low { get; set; } = "Lime";

        public string Middle { get; set; } = "Yellow";

        public string High { get; set; } = "Red";

        public CpuGraph()
            : base(Environment.ProcessorCount, 1000 / FramesPerSecond) {
            var queue = Enumerable.Range(0, Environment.ProcessorCount).Select(_ => new Queue<float>());
            _value = new List<Queue<float>>(queue);
            var converter = new ColorConverter();
            _range = new KeyValuePair<int, Brush>[] {
                new KeyValuePair<int, Brush>(50, new SolidBrush((Color)converter.ConvertFrom(Low))),
                new KeyValuePair<int, Brush>(75, new SolidBrush((Color)converter.ConvertFrom(Middle))),
                new KeyValuePair<int, Brush>(100, new SolidBrush((Color)converter.ConvertFrom(High)))
            };
        }

        protected override void Clear(Graphics graphics) {
            graphics.Clear(Color.Black);
        }

        protected override void Draw(Graphics graphics, float[] value) {
            for (var i = 0; i < value.Length; ++i) {
                _value[i].Enqueue(value[i]);
                while (_value[i].Count > Samples) _value[i].Dequeue();
            }
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var width = 16f / _value.Count;
            for (var i = 0; i < _value.Count; ++i) {
                var x = 16f * i / _value.Count;
                var average = _value[i].Average();
                var height = 16f * average / 100f;
                var y = 16f - height;
                var brush = _range.First(range => average <= range.Key).Value;
                graphics.FillRectangle(brush, x, y, width, height);
            }
        }

        readonly List<Queue<float>> _value;
        readonly KeyValuePair<int, Brush>[] _range;

        static CpuGraph() {
            _factories = Enumerable.Range(0, Environment.ProcessorCount).Select(CreateFactory).ToArray();

            Lazy<PerformanceCounter> CreateFactory(int index) {
                const string categoryName = "Processor";
                const string counterName = "% Processor Time";
                var instanceName = index.ToString();
                return new Lazy<PerformanceCounter>(() => new PerformanceCounter(categoryName, counterName, instanceName, true));
            }
        }

        static readonly Lazy<PerformanceCounter>[] _factories;
    }
}
