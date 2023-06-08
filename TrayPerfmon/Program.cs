using System;
using System.Windows.Forms;

namespace TrayPerfmon
{
    internal class Program
    {
        [STAThread]
        private static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ApplicationContext());
        }
    }
}
