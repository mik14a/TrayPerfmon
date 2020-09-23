using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using TrayPerfmon.Plugin;

namespace TrayPerfmon.Plugin.SampleCs
{
    // Export using Managed Extensibility Framework.
    [Export(typeof(INotifyIconPlugin))]
    [ExportMetadata("Description", "Sample Plugin: % Disk Read/Write Time")]
    public class SamplePlugin : NotifyIconPlugin
    {
        /// <summary>
        /// Override factories for create performance counter.
        /// </summary>
        protected override Lazy<PerformanceCounter>[] Factories => _factories;

        /// <summary>
        /// Construct with 2 counter and sample 15 frame par second.
        /// </summary>
        public SamplePlugin() : base(2, 1000 / 15) { }

        /// <summary>
        /// Override clear method.
        /// </summary>
        /// <param name="graphics"></param>
        protected override void Clear(Graphics graphics) {
            //Clear icon image
            graphics.Clear(Color.Transparent);
        }

        /// <summary>
        /// Override draw method.
        /// </summary>
        protected override void Draw(Graphics graphics, float[] value) {
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
                    var brush = i < next ? _brushes[rw] : Brushes.Black;
                    var dx = i % column;
                    var dy = i / column;
                    graphics.FillRectangle(brush, x + width * dx, y * rw + height * dy, width, height);
                }
            }
        }

        /// <summary>
        /// Construct a class.
        /// </summary>
        static SamplePlugin() {
            // Open 'Server Explorer' window and find your own performance counter in local machine
            _factories = new Lazy<PerformanceCounter>[] {
                new Lazy<PerformanceCounter>(() => new PerformanceCounter("PhysicalDisk", "% Disk Read Time", "_Total", true)),
                new Lazy<PerformanceCounter>(() => new PerformanceCounter("PhysicalDisk", "% Disk Write Time", "_Total", true)),
            };
            // Create Read/Write color brush
            _brushes = new Brush[] { Brushes.Cyan, Brushes.Magenta };
        }

        /// <summary>Static performance counter factories.</summary>
        static readonly Lazy<PerformanceCounter>[] _factories;
        /// <summary>Draw brush for disk access.</summary>
        static readonly Brush[] _brushes;
    }
}
