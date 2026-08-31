using System;
using System.Windows.Forms;
using ItemsExplorer.UI;

namespace ItemsExplorer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            new MainForm().Start();
        }
    }
}
