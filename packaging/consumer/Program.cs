using System;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.PackageConsumer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            XamlRuntime ui =
                XamlRuntime.Load(
                    "<Form Name='MainForm' Text='Package consumer' />");

            try
            {
                Form form = ui.Form;
                form.Text = "Local package compiled successfully";
            }
            finally
            {
                ui.Dispose();
            }
        }
    }
}
