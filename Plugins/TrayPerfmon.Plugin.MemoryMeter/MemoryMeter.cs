using System;
using System.Collections.Generic;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.Humanize;

using Microsoft.VisualBasic.Devices;

namespace TrayPerfmon.Plugin.MemoryMeter
{
    [Plugin("MemoryMeter", "Display memory usage")]
    public class MemoryMeter : NotifyIconPlugin
    {
        protected override string BalloonTipText {
            get {
                var committed = (Committed * 100).ToString("0.00");
                var use = (Use * 100).ToString("0.00");
                var available = ((long)_available).ToString(Multiple.Binary);
                var max = ((long)_max).ToString(Multiple.Binary);
                return $"{committed}% Committed / {use}% Use ({available} / {max})";
            }
        }

        const int FramesPerSecond = 15;

        // Dark-editor palette (Dracula accents — higher contrast than One Dark).
        public string Low { get; set; } = "#50fa7b";
        public string Middle { get; set; } = "#f1fa8c";
        public string High { get; set; } = "#ff5555";

        protected float Committed { get; private set; }

        protected float Use { get; private set; }

        public MemoryMeter()
            : base(1000 / FramesPerSecond) {
            _max = new ComputerInfo().TotalPhysicalMemory;
        }

        protected override void ApplySettings() {
            const string categoryName = "Memory";
            foreach (var counter in _performanceCounter) {
                counter.Dispose();
            }
            _performanceCounter = [
                new PerformanceCounter(categoryName, "% Committed Bytes In Use", true),
                new PerformanceCounter(categoryName, "Available Bytes", true)
            ];
            foreach (var range in _range) {
                range.Value.Dispose();
            }
            var converter = new ColorConverter();
            _range = [
                KeyValuePair.Create(0.5f, new SolidBrush((Color)converter.ConvertFrom(Low)) as Brush),
                KeyValuePair.Create(0.75f, new SolidBrush((Color)converter.ConvertFrom(Middle)) as Brush),
                KeyValuePair.Create(1f, new SolidBrush((Color)converter.ConvertFrom(High)) as Brush)
            ];
        }

        protected override void Clear(Graphics graphics) {
            // Corners stay fully transparent; discs paint their own (semi-transparent) body.
            graphics.Clear(Color.Transparent);
        }

        protected override void Draw(Graphics graphics) {
            var value = _performanceCounter.Select(counter => counter.NextValue()).ToArray();
            _available = (ulong)value[1];  // Available Bytes
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Committed = Math.Clamp(value[0] / 100f, 0f, 1f);
            Use = Math.Clamp(1f - (float)_available / _max, 0f, 1f);
            var size = IconSize;
            var bounds = new RectangleF(0, 0, size, size);
            DrawMeter(graphics, Committed, bounds);
            DrawMeter(graphics, Use, RectangleF.Inflate(bounds, -bounds.Width / 5, -bounds.Height / 5));

            void DrawMeter(Graphics g, float v, RectangleF r) {
                var brush = _range.First(range => v <= range.Key).Value;
                // Unused arc: semi-transparent disc (circle-only icon is fine; no square plate).
                g.FillEllipse(_discBrush, r);
                g.FillPie(brush, r.X, r.Y, r.Width, r.Height, 90f, 360f * v);
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
                _discBrush.Dispose();
            }
            base.Dispose(disposing);
        }

        PerformanceCounter[] _performanceCounter = [];
        ulong _available;
        readonly ulong _max;
        KeyValuePair<float, Brush>[] _range = [];
        /// <summary>Unused portion of each meter disc (alpha tuned only for MemoryMeter).</summary>
        readonly SolidBrush _discBrush = new(Color.FromArgb(0x08, 0x00, 0x00, 0x00));
    }
}
