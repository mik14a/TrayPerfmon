using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TrayPerfmon.Plugin.GpuUsage
{
    public partial class GpuUsage
    {
        static string[] GetEngineTypes(IEnumerable<string> instances, string adapterInstance) {
            return string.IsNullOrEmpty(adapterInstance)
                ? []
                : [.. instances
                .Where(i => i.Contains(adapterInstance, StringComparison.Ordinal))
                .Select(ExtractEngineType)
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)];
        }

        static string[] GetEngineInstances() {
            try {
                return new PerformanceCounterCategory("GPU Engine").GetInstanceNames();
            } catch {
                // Category missing / PDH failure.
                return [];
            }
        }

        static string ExtractEngineType(string instance) {
            var i = instance.LastIndexOf(EngTypeMarker, StringComparison.Ordinal);
            // GPU Engine names are ..._engtype_TYPE; skip garbage if marker absent.
            return i < 0 ? string.Empty : instance[(i + EngTypeMarker.Length)..];
        }

        static string PreferEngineType(string[] types) {
            if (types.Length == 0) return "3D";
            var preferred = new[] { "3D", "Compute 0", "Copy" };
            foreach (var p in preferred) {
                foreach (var t in types) {
                    if (t.Equals(p, StringComparison.OrdinalIgnoreCase)) {
                        return t;
                    }
                }
            }
            return types[0];
        }
    }
}
