using System;
using System.Windows.Forms;

namespace WinFormsXaml.InteractiveBenchmarks
{
    internal sealed class Program
    {
        [STAThread]
        private static int Main(string[] arguments)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool autoStart = false;
            bool smoothScroll = false;
            bool styledScrollBar = false;
            BenchmarkProfile profile = BenchmarkProfile.Controls;
            int i;

            for (i = 0; arguments != null && i < arguments.Length; i++)
            {
                string argument = arguments[i];

                if (String.Equals(
                        argument,
                        "--autorun",
                        StringComparison.OrdinalIgnoreCase))
                {
                    autoStart = true;
                }
                else if (String.Equals(
                             argument,
                             "--lightweight",
                             StringComparison.OrdinalIgnoreCase))
                {
                    profile = BenchmarkProfile.Lightweight;
                }
                else if (String.Equals(
                             argument,
                             "--recycling",
                             StringComparison.OrdinalIgnoreCase))
                {
                    profile = BenchmarkProfile.Recycling;
                }
                else if (String.Equals(
                             argument,
                             "--controls",
                             StringComparison.OrdinalIgnoreCase))
                {
                    profile = BenchmarkProfile.Controls;
                }
                else if (String.Equals(
                             argument,
                             "--nonvirtual",
                             StringComparison.OrdinalIgnoreCase))
                {
                    profile = BenchmarkProfile.NonVirtual;
                }
                else if (String.Equals(
                             argument,
                             "--smooth",
                             StringComparison.OrdinalIgnoreCase))
                {
                    smoothScroll = true;
                }
                else if (String.Equals(
                             argument,
                             "--styled",
                             StringComparison.OrdinalIgnoreCase))
                {
                    styledScrollBar = true;
                }
                else
                {
                    Console.Error.WriteLine(
                        "Unknown option: " + argument);
                    return 2;
                }
            }

            using (BenchmarkDashboard dashboard =
                new BenchmarkDashboard(
                    autoStart,
                    profile,
                    smoothScroll,
                    styledScrollBar))
            {
                Application.Run(dashboard);
                return dashboard.ExitCode;
            }
        }
    }
}
