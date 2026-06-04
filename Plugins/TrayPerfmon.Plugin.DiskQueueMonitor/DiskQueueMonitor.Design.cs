using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TrayPerfmon.Plugin.DiskQueueMonitor
{
    public partial class DiskQueueMonitor
    {
        protected override void ShowSettings() {
            string[] instances;
            try {
                var counterCategory = new PerformanceCounterCategory("PhysicalDisk");
                var instanceNames = counterCategory.GetInstanceNames();
                instances = [.. instanceNames
                    .OrderBy(n => n == "_Total" ? 0 : 1)
                    .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)];
            } catch {
                instances = ["_Total"];
            }

            using var dialog = new Form {
                Text = "DiskQueueMonitor - Select Disk",
                ClientSize = new Size(320, 130),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var label = new Label {
                Text = "Physical disk instance to monitor (Read + Write queue):",
                Left = 10, Top = 10, Width = 300, Height = 30
            };

            var comboBox = new ComboBox {
                Left = 10,
                Top = 45,
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBox.Items.AddRange(instances);

            var idx = Array.IndexOf(instances, DiskInstance);
            comboBox.SelectedIndex = idx >= 0 ? idx : 0;

            var okButton = new Button { Text = "OK", Left = 160, Top = 85, Width = 70, DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Cancel", Left = 240, Top = 85, Width = 70, DialogResult = DialogResult.Cancel };

            dialog.Controls.AddRange([label, comboBox, okButton, cancelButton]);
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;

            if (dialog.ShowDialog() == DialogResult.OK && comboBox.SelectedItem is string selected) {
                if (selected != DiskInstance) {
                    DiskInstance = selected;
                    Apply();
                }
            }
        }
    }
}
