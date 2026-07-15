using System;
using System.ComponentModel.Plugin;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

        /// <summary>Current tray icon edge length in device pixels (typically 16 at 96 DPI).</summary>
        protected int IconSize => _image?.Width ?? BaseIconSize;

        public NotifyIconPlugin(int interval) {
            _timer.Interval = interval;
            _timer.Tick += Tick;
        }

        public void Construct() {
            EnsureImage();
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
            EnsureImage();
            using (var graphics = Graphics.FromImage(_image)) {
                Clear(graphics);
                Draw(graphics);
            }
            // Bitmap.GetHicon() drops per-pixel alpha (treats as opaque / color-key).
            // CreateIconIndirect + 32bpp keeps ARGB so semi-transparent Clear() works.
            var handle = CreateIconHandle(_image);
            try {
                using var icon = Icon.FromHandle(handle);
                var previous = _notifyIcon.Icon;
                _notifyIcon.Icon = (Icon)icon.Clone();
                previous?.Dispose();
            } finally {
                DestroyIcon(handle);
            }
        }

        /// <summary>
        /// Build an HICON that preserves the source bitmap's alpha channel.
        /// Shell uses premultiplied 32bpp color bitmaps for translucent tray icons.
        /// </summary>
        static IntPtr CreateIconHandle(Bitmap source) {
            // Premultiply: Windows icon compositor expects PArgb for partial alpha.
            using var premultiplied = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(premultiplied)) {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(source, 0, 0);
            }

            var hbmColor = IntPtr.Zero;
            var hbmMask = IntPtr.Zero;
            try {
                hbmColor = premultiplied.GetHbitmap();
                // Mask is required by CreateIconIndirect; for 32bpp+alpha the color plane wins.
                using var mask = new Bitmap(source.Width, source.Height, PixelFormat.Format1bppIndexed);
                hbmMask = mask.GetHbitmap();

                var info = new IconInfo {
                    fIcon = 1,
                    xHotspot = 0,
                    yHotspot = 0,
                    hbmMask = hbmMask,
                    hbmColor = hbmColor,
                };
                var handle = CreateIconIndirect(ref info);
                return handle != IntPtr.Zero ? handle : source.GetHicon();
            } finally {
                if (hbmColor != IntPtr.Zero) {
                    DeleteObject(hbmColor);
                }
                if (hbmMask != IntPtr.Zero) {
                    DeleteObject(hbmMask);
                }
            }
        }

        void EnsureImage() {
            var size = GetSmallIconPixelSize();
            if (_image is not null && _image.Width == size && _image.Height == size) {
                return;
            }
            _image?.Dispose();
            _image = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        }

        /// <summary>
        /// Shell small-icon size for the primary monitor (physical pixels).
        /// Uses monitor DPI so the bitmap stays sharp under high-DPI scaling.
        /// </summary>
        static int GetSmallIconPixelSize() {
            try {
                var dpi = GetPrimaryMonitorDpi();
                var size = GetSystemMetricsForDpi(SmCxsmicon, dpi);
                return size > 0 ? size : Math.Max(BaseIconSize, (int)Math.Round(BaseIconSize * (dpi / 96.0)));
            } catch {
                return BaseIconSize;
            }
        }

        static uint GetPrimaryMonitorDpi() {
            var monitor = MonitorFromPoint(new NativePoint(0, 0), MonitorDefaultToPrimary);
            return monitor != IntPtr.Zero
                   && GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0
                   && dpiX > 0
                ? dpiX
                : 96;
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

        const int BaseIconSize = 16;
        const int SmCxsmicon = 49;
        const int MdtEffectiveDpi = 0;
        const uint MonitorDefaultToPrimary = 1;

        readonly NotifyIcon _notifyIcon = new();
        readonly Timer _timer = new();
        Bitmap _image;

        bool _disposed = false;

        [StructLayout(LayoutKind.Sequential)]
        struct NativePoint
        {
            public int X;
            public int Y;
            public NativePoint(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IconInfo
        {
            public int fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [LibraryImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DestroyIcon(IntPtr handle);

        [LibraryImport("User32.dll", SetLastError = true)]
        private static partial IntPtr CreateIconIndirect(ref IconInfo piconinfo);

        [LibraryImport("Gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DeleteObject(IntPtr ho);

        [LibraryImport("User32.dll")]
        private static partial IntPtr MonitorFromPoint(NativePoint pt, uint dwFlags);

        [LibraryImport("User32.dll")]
        private static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);

        [LibraryImport("Shcore.dll")]
        private static partial int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    }
}
