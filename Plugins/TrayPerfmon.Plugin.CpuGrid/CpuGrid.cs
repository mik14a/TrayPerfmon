using System;
using System.Collections.Generic;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace TrayPerfmon.Plugin.CpuGrid
{
    /// <summary>
    /// Draws per-CPU usage as a heatmap grid (one cell per core/logical CPU),
    /// which stays readable on high core count CPUs where per-core gauges collapse.
    /// </summary>
    [Plugin("CpuGrid", "Draw CPU usage as heatmap grid")]
    public class CpuGrid : NotifyIconPlugin
    {
        // Dark-editor palette (Dracula accents — higher contrast than One Dark).
        public string Low { get; set; } = "#50fa7b";
        public string Middle { get; set; } = "#f1fa8c";
        public string High { get; set; } = "#ff5555";
        public string Fill { get; set; } = "#08000000";

        /// <summary>"Physical" = one cell per core, "Logical" = one cell per logical CPU.</summary>
        public string GridMode { get; set; } = "Physical";

        protected override string BalloonTipText => $"CPU Usage: {Value:0.0}% (max {Max:0}% #{MaxIndex})";
        protected float Value { get; private set; }
        protected float Max { get; private set; }
        protected int MaxIndex { get; private set; }

        const int Interval = 250;
        // EMA weight of the newest sample; grid does not scroll, so smoothing beats averaging.
        const float Alpha = 0.3f;
        const int PaletteSteps = 20;

        public CpuGrid()
            : base(Interval) {
        }

        protected override void ApplySettings() {
            var converter = new ColorConverter();
            var low = (Color)converter.ConvertFrom(Low);
            var middle = (Color)converter.ConvertFrom(Middle);
            var high = (Color)converter.ConvertFrom(High);
            _fill = (Color)converter.ConvertFrom(Fill);

            // Cells stay visible at 0%: floor is Low darkened to 30%,
            // fading into the heat ramp as usage rises.
            var dim = Lerp(Color.Black, low, 0.3f);
            foreach (var brush in _brushes) {
                brush.Dispose();
            }
            _brushes = Enumerable.Range(0, PaletteSteps + 1)
                .Select(step => {
                    var t = (float)step / PaletteSteps;
                    var heat = t <= 0.5f ? Lerp(low, middle, t * 2) : Lerp(middle, high, (t - 0.5f) * 2);
                    return new SolidBrush(Lerp(dim, heat, Math.Min(1f, t * 4)));
                })
                .ToArray();

            _cells = string.Equals(GridMode, "Logical", StringComparison.OrdinalIgnoreCase)
                ? BuildLogicalCells()
                : BuildPhysicalCells() ?? BuildLogicalCells();
            // Counter instances only exist for the current processor group.
            _cells = _cells
                .Select(cell => cell.Where(index => index >= 0 && index < Environment.ProcessorCount).ToList())
                .Where(cell => cell.Count > 0)
                .ToList();
            foreach (var cell in _performanceCounter) {
                foreach (var counter in cell) {
                    counter.Dispose();
                }
            }
            _performanceCounter = _cells
                .Select(cell => cell
                    .Select(index => new PerformanceCounter("Processor", "% Processor Time", index.ToString(), true))
                    .ToArray())
                .ToArray();
            _smoothed = new float[_cells.Count];
            _seeded = false;
        }

        protected override void Clear(Graphics graphics) {
            graphics.Clear(_fill);
        }

        protected override void Draw(Graphics graphics) {
            var value = new float[_cells.Count];
            for (var i = 0; i < _cells.Count; ++i) {
                var counters = _performanceCounter[i];
                var sum = counters.Sum(counter => counter.NextValue());
                var sample = sum / counters.Length;
                value[i] = _seeded ? _smoothed[i] + Alpha * (sample - _smoothed[i]) : sample;
                _smoothed[i] = value[i];
            }
            _seeded = true;
            Value = value.Average();
            MaxIndex = 0;
            Max = value[0];
            for (var i = 1; i < value.Length; ++i) {
                if (value[i] > Max) {
                    Max = value[i];
                    MaxIndex = i;
                }
            }

            var size = IconSize;
            var count = value.Length;
            var columns = (int)Math.Ceiling(Math.Sqrt(count));
            var rows = (int)Math.Ceiling((double)count / columns);
            var cell = Math.Max(1, size / columns);
            // Gaps only help once a cell is wide enough to still show one lit pixel.
            var gap = cell >= 3 ? 1 : 0;
            var offsetY = (size - rows * cell) / 2;
            for (var i = 0; i < count; ++i) {
                var x = i % columns * cell;
                var y = offsetY + i / columns * cell;
                if (x + cell > size || y + cell > size) {
                    continue;
                }
                var step = (int)Math.Round(Math.Clamp(value[i], 0f, 100f) / 100f * PaletteSteps);
                graphics.FillRectangle(_brushes[step], x, y, cell - gap, cell - gap);
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                foreach (var cell in _performanceCounter) {
                    foreach (var counter in cell) {
                        counter.Dispose();
                    }
                }
                foreach (var brush in _brushes) {
                    brush.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        static List<List<int>> BuildLogicalCells() {
            return Enumerable.Range(0, Environment.ProcessorCount)
                .Select(index => new List<int> { index })
                .ToList();
        }

        /// <summary>
        /// Group logical CPUs into physical cores via GetLogicalProcessorInformationEx.
        /// Returns null when the topology is unavailable (caller falls back to logical).
        /// </summary>
        static List<List<int>> BuildPhysicalCells() {
            try {
                var length = 0u;
                _ = GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
                if (length == 0) {
                    return null;
                }
                var buffer = Marshal.AllocHGlobal((int)length);
                try {
                    if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length)) {
                        return null;
                    }
                    var cells = new List<(int Efficiency, int First, List<int> Cell)>();
                    var covered = new bool[Environment.ProcessorCount];
                    uint offset = 0;
                    while (offset + 8 <= length) {
                        // LOGICAL_PROCESSOR_RELATIONSHIP_INFO: int32 Relationship | int32 Size | union fields.
                        // Size 24 (Windows 10): GROUP_AFFINITY { mask @8, group @16 }.
                        // Size 48 (Windows 11): trailing GROUP_AFFINITY { mask @32, group @40 },
                        // efficiency class byte @8; the legacy mask slot is zero there.
                        var recordSize = Marshal.ReadInt32(buffer, (int)offset + 4);
                        if (recordSize < 24) {
                            break;
                        }
                        long mask;
                        int group;
                        int efficiency;
                        if (recordSize >= 48) {
                            mask = Marshal.ReadInt64(buffer, (int)offset + 32);
                            group = (ushort)Marshal.ReadInt16(buffer, (int)offset + 40);
                            efficiency = Marshal.ReadByte(buffer, (int)offset + 8);
                        } else {
                            mask = Marshal.ReadInt64(buffer, (int)offset + 8);
                            group = (ushort)Marshal.ReadInt16(buffer, (int)offset + 16);
                            efficiency = 0;
                        }
                        var cell = new List<int>();
                        for (var bit = 0; bit < 64; ++bit) {
                            if ((mask & (1L << bit)) == 0) {
                                continue;
                            }
                            var index = group * 64 + bit;
                            if (index < covered.Length) {
                                covered[index] = true;
                            }
                            cell.Add(index);
                        }
                        if (cell.Count > 0) {
                            cells.Add((efficiency, cell[0], cell));
                        }
                        offset += (uint)recordSize;
                    }
                    // Logical CPUs missing from the topology become their own cell.
                    for (var index = 0; index < covered.Length; ++index) {
                        if (!covered[index]) {
                            cells.Add((0, index, new List<int> { index }));
                        }
                    }
                    // Hybrid CPUs: performance cores first, then by logical order.
                    cells.Sort((a, b) => a.Efficiency != b.Efficiency
                        ? b.Efficiency.CompareTo(a.Efficiency)
                        : a.First.CompareTo(b.First));
                    return cells.Count > 0 ? cells.Select(cell => cell.Cell).ToList() : null;
                } finally {
                    Marshal.FreeHGlobal(buffer);
                }
            } catch (Exception) {
                return null;
            }
        }

        static Color Lerp(Color from, Color to, float t) {
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }

        const int RelationProcessorCore = 0;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetLogicalProcessorInformationEx(int relationship, IntPtr buffer, ref uint returnLength);

        List<List<int>> _cells = [];
        PerformanceCounter[][] _performanceCounter = [];
        SolidBrush[] _brushes = [];
        float[] _smoothed = [];
        bool _seeded = false;
        Color _fill;
    }
}
