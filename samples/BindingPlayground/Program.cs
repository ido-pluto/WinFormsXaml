using System;
using System.Windows.Forms;
using BindingPlayground.UI;

namespace BindingPlayground
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
