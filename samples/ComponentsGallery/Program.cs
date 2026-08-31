using System;
using System.Windows.Forms;
using ComponentsGallery.UI;
using WinFormsXaml;

namespace ComponentsGallery
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            XamlRuntime.Register("UI.Components");
            new MainForm().Start();
        }
    }
}
