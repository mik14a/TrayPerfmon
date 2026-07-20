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
        /// <summary>The PerformanceCounter instance name for the GPU Adapter Memory category (e.g. "luid_0x..._phys_0").</summary>
        public string InstanceName { get; set; }
        // Dark-editor palette (Dracula accents — higher contrast than One Dark).
        public string Low { get; set; } = "#50fa7b";
        public string Middle { get; set; } = "#f1fa8c";
        public string High { get; set; } = "#ff5555";
        public string Fill { get; set; } = "#08000000";

        protected override string BalloonTipText {
            get {
                var total = _gpu.VideoMemory;
                var percent = total > 0 ? (Value * 100f / total) : 0f;
                var usedText = ((long)Value).ToString(Multiple.Binary);
                var totalText = ((long)total).ToString(Multiple.Binary);
                return $"{_gpu.Name}: {percent:0.0}% ({usedText} / {totalText})";
            }
        }
        protected float Value { get; private set; }
        protected override bool HasSettings => true;

        const int FramesPerSecond = 15;

        public GpuMemory()
            : base(1000 / FramesPerSecond) {
            _history = new Queue<float>();
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
            _fill = (Color)converter.ConvertFrom(Fill);
        }

        protected override void Clear(Graphics graphics) {
            graphics.Clear(_fill);
        }

        protected override void Draw(Graphics graphics) {
            var value = _performanceCounter.Select(counter => counter.NextValue()).ToArray();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var size = IconSize;
            Value = value[0];
            var percent = Math.Clamp(Value * 100f / _gpu.VideoMemory, 0f, 100f);
            _history.Enqueue(percent);
            while (size < _history.Count) {
                _history.Dequeue();
            }
            var x = 0;
            foreach (var framePercent in _history) {
                var height = size * framePercent / 100f;
                var y = size - height;
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

        readonly Queue<float> _history;

        GpuInfo _gpu;
        PerformanceCounter[] _performanceCounter = [];
        KeyValuePair<int, Brush>[] _range = [];
        Color _fill;
    }
}
