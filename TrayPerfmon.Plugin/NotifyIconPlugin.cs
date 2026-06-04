using System;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayPerfmon.Plugin
{
    public abstract partial class NotifyIconPlugin : IPlugin, IDisposable
    {
        protected abstract Lazy<PerformanceCounter>[] Factories { get; }

        protected virtual string BalloonTipText { get; } = string.Empty;

        protected virtual void ShowSettings() { }
        protected virtual void ApplySettings() { }
        protected abstract void Clear(Graphics graphics);
        protected abstract void Draw(Graphics graphics, float[] value);

        public NotifyIconPlugin(int count, bool hasSettings, int interval) {
            _count = count;
            _hasSettings = hasSettings;
            _performanceCounter = new PerformanceCounter[_count];
            _value = new float[_count];
            _timer.Interval = interval;
            _timer.Tick += Tick;
        }

        public void Construct() {
            Apply();
            _notifyIcon.Text = GetType().Name;
            _notifyIcon.Visible = true;
            _notifyIcon.MouseClick += NotifyIconMouseClick;
            _timer.Start();
        }

        public void Apply() {
            ApplySettings();
            for (var i = 0; i < _count; ++i) {
                _performanceCounter[i]?.Dispose();
            }
            for (var i = 0; i < _count; ++i) {
                _performanceCounter[i] = Factories[i]?.Value;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        void Tick(object sender, EventArgs e) {
            for (var i = 0; i < _count; ++i) {
                _value[i] = _performanceCounter[i].NextValue();
            }
            using (var graphics = Graphics.FromImage(_image)) {
                Clear(graphics);
                Draw(graphics, _value);
            }
            var handle = _image.GetHicon();
            try {
                using var icon = Icon.FromHandle(handle);
                var previous = _notifyIcon.Icon;
                _notifyIcon.Icon = (Icon)icon.Clone();
                previous?.Dispose();
            } finally {
                DestroyIcon(handle);
            }
        }

        void NotifyIconMouseClick(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                if (_hasSettings) {
                    ShowSettings();
                }
            } else if (e.Button == MouseButtons.Right) {
                if (!string.IsNullOrWhiteSpace(BalloonTipText)) {
                    _notifyIcon.ShowBalloonTip(1000, GetType().Name, BalloonTipText, ToolTipIcon.Info);
                }
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    _notifyIcon?.Dispose();
                    _timer?.Dispose();
                    _image?.Dispose();
                    foreach (var performanceCounter in _performanceCounter) {
                        performanceCounter?.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        readonly NotifyIcon _notifyIcon = new();
        readonly Timer _timer = new();
        readonly Bitmap _image = new(16, 16);
        readonly PerformanceCounter[] _performanceCounter;
        readonly float[] _value;

        protected readonly int _count;
        protected readonly bool _hasSettings;

        bool _disposed = false;

        [LibraryImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DestroyIcon(IntPtr handle);
    }
}
