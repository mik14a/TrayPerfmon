using System;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace TrayPerfmon.Plugin.SampleCs
{
    // Export plugin description.
    [Plugin("SamplePluginCs", "Sample Plugin: % Disk Read/Write Time")]
    public class SamplePlugin : NotifyIconPlugin
    {
        /// <summary>
        /// Construct sample 15 frame par second.
        /// </summary>
        public SamplePlugin() : base(1000 / 15) {
        }

        /// <summary>
        /// Apply settings.
        /// </summary>
        protected override void ApplySettings() {
            foreach (var counter in _performanceCounter) {
                counter.Dispose();
            }
            _performanceCounter = [
                new PerformanceCounter("PhysicalDisk", "% Disk Read Time", "_Total", true),
                new PerformanceCounter("PhysicalDisk", "% Disk Write Time", "_Total", true),
            ];
            _brushes = new Brush[] { Brushes.Cyan, Brushes.Magenta };
        }

        protected override void Clear(Graphics graphics) {
            graphics.Clear(Color.Transparent);
        }

        /// <summary>
        /// Override draw method.
        /// </summary>
        protected override void Draw(Graphics graphics) {
            var value = _performanceCounter.Select(counter => counter.NextValue()).ToArray();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = graphics.VisibleClipBounds;
            // Draw Read/Write % value
            const int row = 2, column = 4;
            const int count = row * column, sample = 100 / count;
            float x = bounds.X, y = bounds.Y + bounds.Height / 2f;
            float width = bounds.Width / column, height = bounds.Height / 2f / row;
            for (var rw = 0; rw < 2; ++rw) {
                var next = (int)(value[rw] + sample - .1f) / sample;
                for (var i = 0; i < count; ++i) {
                    var brush = i < next ? _brushes[rw] : new SolidBrush(Color.FromArgb(0xC0, 0x00, 0x00, 0x00));
                    var dx = i % column;
                    var dy = i / column;
                    graphics.FillRectangle(brush, x + width * dx, y * rw + height * dy, width, height);
                }
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                foreach (var counter in _performanceCounter) {
                    counter.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        PerformanceCounter[] _performanceCounter = [];

        /// <summary>Draw brush for disk access.</summary>
        Brush[] _brushes;
    }
}
