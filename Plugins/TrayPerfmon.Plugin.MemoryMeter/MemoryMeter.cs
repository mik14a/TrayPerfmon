using System;
using System.Collections.Generic;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.Humanize;
using Microsoft.VisualBasic.Devices;

namespace TrayPerfmon.Plugin.MemoryGrapth
{
    [Plugin("MemoryMeter", "Display memory usage")]
    public class MemoryMeter : NotifyIconPlugin
    {
        protected override Lazy<PerformanceCounter>[] Factories => _factories;

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

        public string Low { get; set; } = "Lime";

        public string Middle { get; set; } = "Yellow";

        public string High { get; set; } = "Red";

        protected float Committed { get; private set; }

        protected float Use { get; private set; }

        public MemoryMeter()
            : base(2, 1000 / FramesPerSecond) {
            var computerInfo = new ComputerInfo();
            _max = computerInfo.TotalPhysicalMemory;
            var converter = new ColorConverter();
            _range = new[] {
                KeyValuePair.Create(0.5f, new SolidBrush((Color)converter.ConvertFrom(Low)) as Brush),
                KeyValuePair.Create(0.75f, new SolidBrush((Color)converter.ConvertFrom(Middle)) as Brush),
                KeyValuePair.Create(1f, new SolidBrush((Color)converter.ConvertFrom(High)) as Brush)
            };
        }

        protected override void Clear(Graphics graphics) {
            graphics.Clear(Color.Transparent);
        }

        protected override void Draw(Graphics graphics, float[] value) {
            _available = (ulong)value[1];
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Committed = value[0] / 100f;
            Use = 1f - (float)_available / _max;
            var bounds = graphics.VisibleClipBounds;
            DrawMeter(graphics, Committed, bounds);
            DrawMeter(graphics, Use, RectangleF.Inflate(bounds, -bounds.Width / 5, -bounds.Height / 5));

            void DrawMeter(Graphics g, float v, RectangleF r) {
                var brush = _range.First(range => v <= range.Key).Value;
                g.FillEllipse(Brushes.Black, r);
                g.FillPie(brush, r.X, r.Y, r.Width, r.Height, 90f, 360f * v);
            }
        }

        ulong _available, _max;
        readonly KeyValuePair<float, Brush>[] _range;

        static MemoryMeter() {
            const string categoryName = "Memory";
            _factories = new[] {
                new Lazy<PerformanceCounter>(() => new PerformanceCounter(categoryName, "% Committed Bytes In Use", true)),
                new Lazy<PerformanceCounter>(() => new PerformanceCounter(categoryName, "Available Bytes", true))
            };
        }

        static readonly Lazy<PerformanceCounter>[] _factories;
    }
}
