using System;
using System.ComponentModel.Plugin;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayPerfmon.Plugin
{
    public abstract partial class NotifyIconPlugin : IPlugin, IDisposable
    {
        protected virtual string BalloonTipText { get; } = string.Empty;

        protected virtual bool HasSettings { get; } = false;
        protected virtual void ShowSettings() { }
        protected virtual void ApplySettings() { }
        protected abstract void Clear(Graphics graphics);
        protected abstract void Draw(Graphics graphics);

        public NotifyIconPlugin(int interval) {
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
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        void Tick(object sender, EventArgs e) {
            using (var graphics = Graphics.FromImage(_image)) {
                Clear(graphics);
                Draw(graphics);
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
                if (HasSettings) {
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
                }
                _disposed = true;
            }
        }

        readonly NotifyIcon _notifyIcon = new();
        readonly Timer _timer = new();
        readonly Bitmap _image = new(16, 16);

        bool _disposed = false;

        [LibraryImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DestroyIcon(IntPtr handle);
    }
}
