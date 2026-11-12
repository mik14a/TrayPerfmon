using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrayPerfmon.Plugin.CpuGrid
{
    public partial class CpuGrid
    {
        static readonly string[] GridModes = ["Physical", "Logical"];

        protected override void ShowSettings() {
            using var dialog = new Form {
                Text = "CpuGrid - Select Grid Mode",
                ClientSize = new Size(340, 130),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var label = new Label {
                Text = "One cell per:",
                Left = 10, Top = 12, Width = 320, Height = 20
            };

            var comboBox = new ComboBox {
                Left = 10, Top = 36, Width = 320,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBox.Items.AddRange(GridModes);
            var idx = Array.IndexOf(GridModes, GridMode);
            comboBox.SelectedIndex = idx >= 0 ? idx : 0;

            var hint = new Label {
                Left = 10, Top = 62, Width = 320, Height = 20,
                ForeColor = SystemColors.GrayText,
                Text = DescribeMode(comboBox.SelectedIndex)
            };
            comboBox.SelectedIndexChanged += (_, _) => hint.Text = DescribeMode(comboBox.SelectedIndex);

            var okButton = new Button { Text = "OK", Left = 180, Top = 95, Width = 70, DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Cancel", Left = 260, Top = 95, Width = 70, DialogResult = DialogResult.Cancel };

            dialog.Controls.AddRange([label, comboBox, hint, okButton, cancelButton]);
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;

            if (dialog.ShowDialog() == DialogResult.OK && 0 <= comboBox.SelectedIndex
                && GridModes[comboBox.SelectedIndex] != GridMode) {
                GridMode = GridModes[comboBox.SelectedIndex];
                Apply();
            }
        }

        /// <summary>Gray hint under the combo showing how many cells each mode produces.</summary>
        static string DescribeMode(int index) {
            var logical = Environment.ProcessorCount;
            if (index == 1) {
                return $"{logical} cells ({logical} logical CPUs)";
            }
            var physical = BuildPhysicalCells()?.Count ?? logical;
            return $"{physical} cells ({physical} physical cores)";
        }
    }
}
