using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TrayPerfmon.Plugin.GpuUsage
{
    public partial class GpuUsage
    {
        protected override void ShowSettings() {
            List<GpuInfo> gpus;
            try {
                gpus = GpuInfo.GetGpuAdapters();
            } catch {
                gpus = [];
            }

            var instances = GetEngineInstances();
            if (gpus.Count == 0 && instances.Length == 0) {
                MessageBox.Show("No GPU Engine instances found.", "GpuUsage", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Fallback when DXGI list is empty but counters exist.
            if (gpus.Count == 0) {
                gpus = instances
                    .Select(ExtractAdapterKey)
                    .Where(k => k.Length > 0)
                    .Distinct()
                    .Select(k => new GpuInfo { InstanceName = k, Name = k })
                    .ToList();
            }

            using var dialog = new Form {
                Text = "GpuUsage - Select GPU Engine",
                ClientSize = new Size(360, 170),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var gpuLabel = new Label {
                Text = "GPU adapter:",
                Left = 10, Top = 12, Width = 340, Height = 20
            };
            var gpuCombo = new ComboBox {
                Left = 10, Top = 32, Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            gpuCombo.Items.AddRange(gpus.ToArray());
            gpuCombo.DisplayMember = "Name";
            var gpuIndex = string.IsNullOrEmpty(AdapterInstance)
                ? 0
                : gpus.FindIndex(g => g.InstanceName == AdapterInstance);
            gpuCombo.SelectedIndex = gpuIndex >= 0 ? gpuIndex : 0;

            var typeLabel = new Label {
                Text = "Engine type (Task Manager style):",
                Left = 10, Top = 65, Width = 340, Height = 20
            };
            var typeCombo = new ComboBox {
                Left = 10, Top = 85, Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            void ReloadTypes() {
                typeCombo.Items.Clear();
                if (gpuCombo.SelectedItem is not GpuInfo selected) return;
                var types = GetEngineTypes(instances, selected.InstanceName);
                if (types.Length == 0) {
                    types = ["3D"];
                }
                typeCombo.Items.AddRange(types);
                var idx = string.IsNullOrEmpty(EngineType)
                    ? 0
                    : System.Array.FindIndex(types, t => t.Equals(EngineType, System.StringComparison.OrdinalIgnoreCase));
                typeCombo.SelectedIndex = idx >= 0 ? idx : 0;
            }

            gpuCombo.SelectedIndexChanged += (_, _) => ReloadTypes();
            ReloadTypes();

            var okButton = new Button { Text = "OK", Left = 200, Top = 125, Width = 70, DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Cancel", Left = 280, Top = 125, Width = 70, DialogResult = DialogResult.Cancel };

            dialog.Controls.AddRange([gpuLabel, gpuCombo, typeLabel, typeCombo, okButton, cancelButton]);
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;

            if (dialog.ShowDialog() == DialogResult.OK
                && gpuCombo.SelectedItem is GpuInfo gpu
                && typeCombo.SelectedItem is string type) {
                if (gpu.InstanceName != AdapterInstance || type != EngineType) {
                    AdapterInstance = gpu.InstanceName;
                    EngineType = type;
                    Apply();
                }
            }
        }

        static string ExtractAdapterKey(string instance) {
            // pid_*_luid_0x..._0x..._phys_N_eng_...
            var luid = instance.IndexOf("luid_", System.StringComparison.Ordinal);
            if (luid < 0) return string.Empty;
            var eng = instance.IndexOf("_eng_", luid, System.StringComparison.Ordinal);
            if (eng < 0) return string.Empty;
            return instance[luid..eng];
        }
    }
}
