using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TrayPerfmon.Plugin.GpuMemory
{
    public partial class GpuMemory
    {
        protected override void ShowSettings() {
            List<GpuInfo> gpus;
            try {
                gpus = GpuInfo.GetGpuAdapters();
            } catch {
                gpus = [];
            }

            if (gpus.Count == 0) {
                MessageBox.Show("No suitable GPU adapters with dedicated memory found.", "GpuMemory", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new Form {
                Text = "GpuMemory - Select GPU",
                ClientSize = new Size(320, 130),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var label = new Label {
                Text = "GPU adapter to monitor (Dedicated VRAM usage):",
                Left = 10, Top = 10, Width = 300, Height = 30
            };

            var comboBox = new ComboBox {
                Left = 10,
                Top = 45,
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBox.Items.AddRange(gpus.ToArray());
            comboBox.DisplayMember = "Name";
            var index = string.IsNullOrEmpty(InstanceName) ? 0 : gpus.FindIndex(gpu => gpu.InstanceName == InstanceName);
            comboBox.SelectedIndex = index >= 0 ? index : 0;

            var okButton = new Button { Text = "OK", Left = 160, Top = 85, Width = 70, DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Cancel", Left = 240, Top = 85, Width = 70, DialogResult = DialogResult.Cancel };

            dialog.Controls.AddRange(new Control[] { label, comboBox, okButton, cancelButton });
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;

            if (dialog.ShowDialog() == DialogResult.OK && comboBox.SelectedIndex >= 0) {
                var selectedGpu = gpus[comboBox.SelectedIndex];
                if (selectedGpu.InstanceName != InstanceName) {
                    InstanceName = selectedGpu.InstanceName;
                    Apply();
                }
            }
        }
    }
}
