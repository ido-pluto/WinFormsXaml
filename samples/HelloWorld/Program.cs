using System;
using System.Windows.Forms;
using HelloWorld.UI;
using WinFormsXaml;

namespace HelloWorld
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // With no fragment, every embedded XML resource is inspected and
            // only Component-root documents are registered.
            XamlRuntime.Register();

            new MainForm().Start();
        }
    }
}
