using System;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayPerfmon.Plugin
{
    public abstract class NotifyIconPlugin : IPlugin, IDisposable
    {
        protected abstract Lazy<PerformanceCounter>[] Factories { get; }

        protected virtual string BalloonTipText { get; } = string.Empty;

        public NotifyIconPlugin(int count, int interval) {
            _count = count;
            _performanceCounter = new PerformanceCounter[_count];
            _value = new float[_count];
            _timer.Interval = interval;
            _timer.Tick += Tick;
        }

        public void Construct() {
            var factories = Factories;
            for (var i = 0; i < _count; ++i) {
                _performanceCounter[i] = factories[i].Value;
            }
            _notifyIcon.Text = GetType().Name;
            _notifyIcon.Visible = true;
            _notifyIcon.Click += Click;
            _timer.Start();
        }

        void Tick(object sender, EventArgs e) {
            for (var i = 0; i < _count; ++i) {
                _value[i] = _performanceCounter[i].NextValue();
            }
            using (var graphics = Graphics.FromImage(_image)) {
                Clear(graphics);
                Draw(graphics, _value);
            }
            var icon = Icon.FromHandle(_image.GetHicon());
            _notifyIcon.Icon = icon;
            DestroyIcon(icon.Handle);
        }

        void Click(object sender, EventArgs e) {
            if (!string.IsNullOrWhiteSpace(BalloonTipText)) {
                _notifyIcon.ShowBalloonTip(1000, GetType().Name, BalloonTipText, ToolTipIcon.Info);
            }
        }

        protected abstract void Clear(Graphics graphics);

        protected abstract void Draw(Graphics graphics, float[] value);

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    _notifyIcon?.Dispose();
                    _notifyIcon = null;
                    _timer?.Dispose();
                    _timer = null;
                    _image?.Dispose();
                    _image = null;
                }
                _disposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected readonly int _count;

        NotifyIcon _notifyIcon = new NotifyIcon();
        Timer _timer = new Timer();
        Bitmap _image = new Bitmap(16, 16);
        Rectangle _rectangle = new Rectangle(0, 0, 16, 16);
        bool _disposed = false;

        readonly PerformanceCounter[] _performanceCounter;
        readonly float[] _value;

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern bool DestroyIcon(IntPtr handle);
    }
}
