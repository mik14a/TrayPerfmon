using System;
using System.Collections.Generic;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.Humanize;

namespace TrayPerfmon.Plugin.GpuMemory
{
    [Plugin("GpuMemory", "GPU adapter memory (VRAM) usage - works for AMD, NVIDIA, Intel etc.")]
    public partial class GpuMemory : NotifyIconPlugin
    {
        protected override string BalloonTipText {
            get {
                var used = _value;
                var total = _gpu.VideoMemory;
                var percent = total > 0 ? (used * 100.0 / total) : 0;
                var usedText = ((long)used).ToString(Multiple.Binary);
                var totalText = ((long)total).ToString(Multiple.Binary);
                return $"{_gpu.Name}: {percent:0.0}% ({usedText} / {totalText})";
            }
        }

        const int FramesPerSecond = 15;
        const int Samples = 16;

        public string InstanceName { get; set; }
        public string Low { get; set; } = "Lime";
        public string Middle { get; set; } = "Yellow";
        public string High { get; set; } = "Red";

        protected override bool HasSettings => true;

        public GpuMemory()
            : base(1000 / FramesPerSecond) {
            _history = new Queue<float>();
            for (var i = 0; i < Samples; ++i) {
                _history.Enqueue(0f);
            }
        }

        protected override void ApplySettings() {
            var adapters = GpuInfo.GetGpuAdapters();
            _gpu = InstanceName == null ? adapters.First() : adapters.FirstOrDefault(g => g.InstanceName == InstanceName) ?? adapters.First();
            InstanceName = _gpu.InstanceName;
            foreach (var counter in _performanceCounter) {
                counter.Dispose();
            }
            _performanceCounter = [
                new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", _gpu.InstanceName, true)
            ];
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
            graphics.Clear(Color.Black);
        }

        protected override void Draw(Graphics graphics) {
            var value = _performanceCounter.Select(counter => counter.NextValue()).ToArray();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            _value = value[0];
            var percent = Math.Clamp(_value * 100f / _gpu.VideoMemory, 0f, 100f);
            _history.Enqueue(percent);
            while (Samples < _history.Count) {
                _history.Dequeue();
            }
            var x = 0;
            foreach (var framePercent in _history) {
                var height = 16f * framePercent / 100f;
                var y = 16f - height;
                var brush = _range.First(range => framePercent <= range.Key).Value;
                graphics.FillRectangle(brush, x, y, 1, height);
                ++x;
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
        GpuInfo _gpu;
        float _value;
        readonly Queue<float> _history;
        KeyValuePair<int, Brush>[] _range = [];
    }
}
