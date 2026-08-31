using System;
using System.Windows.Forms;
using PresetStudio.UI;

namespace PresetStudio
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
