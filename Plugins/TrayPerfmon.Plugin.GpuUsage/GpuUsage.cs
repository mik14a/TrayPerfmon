using System;
using System.Collections.Generic;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace TrayPerfmon.Plugin.GpuUsage
{
    [Plugin("GpuUsage", "GPU engine utilization by engtype (sum of instances)")]
    public partial class GpuUsage : NotifyIconPlugin
    {
        /// <summary>Adapter key matching GPU Engine instances (e.g. luid_0x..._phys_0).</summary>
        public string AdapterInstance { get; set; }
        /// <summary>Engine type suffix (e.g. 3D, Copy, Compute 0) — Task Manager style.</summary>
        public string EngineType { get; set; }
        // Dark-editor palette (Dracula accents — higher contrast than One Dark).
        public string Low { get; set; } = "#50fa7b";
        public string Middle { get; set; } = "#f1fa8c";
        public string High { get; set; } = "#ff5555";
        public string Fill { get; set; } = "#08000000";

        protected override string BalloonTipText => $"{GpuName} [{EngineType}]: {Percent:0.0}%";
        protected string GpuName { get; private set; } = "GPU";
        protected float Percent { get; private set; }
        protected override bool HasSettings => true;

        const int FramesPerSecond = 15;
        const int RefreshInstanceMs = 1000;
        const float EmaAlpha = 0.3f; // EMA weight on the new sample (higher = snappier).
        const string EngTypeMarker = "_engtype_";

        public GpuUsage()
            : base(1000 / FramesPerSecond) {
            _history = new Queue<float>();
        }

        protected override void ApplySettings() {
            var adapters = GpuInfo.GetGpuAdapters();
            // No DXGI adapters is possible (driver/VM). Counters-only fallback leaves AdapterInstance as-is.
            if (adapters.Count > 0) {
                var gpu = string.IsNullOrEmpty(AdapterInstance)
                    ? adapters.First()
                    : adapters.FirstOrDefault(g => g.InstanceName == AdapterInstance) ?? adapters.First();
                AdapterInstance = gpu.InstanceName;
                GpuName = gpu.Name;
            } else {
                GpuName = string.IsNullOrEmpty(AdapterInstance) ? "GPU" : AdapterInstance;
            }

            var instances = GetEngineInstances();
            var types = GetEngineTypes(instances, AdapterInstance);
            // First run, stale saved type, or adapter fallback to a GPU without this engtype.
            if (string.IsNullOrEmpty(EngineType)
                || !types.Contains(EngineType, StringComparer.OrdinalIgnoreCase)) {
                EngineType = PreferEngineType(types);
            }

            ClearCounters();
            _lastRefresh = DateTime.MinValue;
            SyncCounters(instances);

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
            RefreshCountersIfNeeded();

            // Instances die with their process — NextValue can throw.
            var sum = 0f;
            var dead = new List<string>();
            foreach (var pair in _counters) {
                try {
                    sum += pair.Value.NextValue();
                } catch {
                    dead.Add(pair.Key);
                }
            }
            foreach (var key in dead) {
                _counters[key].Dispose();
                _counters.Remove(key);
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var size = IconSize;
            var sample = Math.Clamp(sum, 0f, 100f);
            Percent = EmaAlpha * sample + (1f - EmaAlpha) * Percent;
            _history.Enqueue(Percent);
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
                ClearCounters();
                foreach (var range in _range) {
                    range.Value.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        void RefreshCountersIfNeeded() {
            var now = DateTime.UtcNow;
            if ((now - _lastRefresh).TotalMilliseconds < RefreshInstanceMs) {
                return;
            }
            _lastRefresh = now;
            SyncCounters(GetEngineInstances());
        }

        void SyncCounters(string[] instances) {
            // ApplySettings always leaves EngineType non-empty; AdapterInstance may be empty if no GPU at all.
            var matched = string.IsNullOrEmpty(AdapterInstance)
                ? []
                : MatchInstances(instances, AdapterInstance, EngineType);
            var wanted = new HashSet<string>(matched, StringComparer.Ordinal);

            var remove = _counters.Keys.Where(key => !wanted.Contains(key)).ToArray();
            foreach (var key in remove) {
                _counters[key].Dispose();
                _counters.Remove(key);
            }

            foreach (var instance in matched) {
                if (_counters.ContainsKey(instance)) continue;
                try {
                    // Race: instance can vanish between GetInstanceNames and ctor.
                    _counters[instance] = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true);
                } catch {
                }
            }

            static string[] MatchInstances(IEnumerable<string> instances, string adapterInstance, string engineType) {
                var suffix = EngTypeMarker + engineType;
                return [.. instances.Where(i =>
                i.Contains(adapterInstance, StringComparison.Ordinal) &&
                i.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))];
            }
        }

        void ClearCounters() {
            foreach (var counter in _counters.Values) {
                counter.Dispose();
            }
            _counters.Clear();
        }

        readonly Dictionary<string, PerformanceCounter> _counters = new(StringComparer.Ordinal);
        readonly Queue<float> _history;

        DateTime _lastRefresh;
        KeyValuePair<int, Brush>[] _range = [];
        Color _fill;
    }
}
