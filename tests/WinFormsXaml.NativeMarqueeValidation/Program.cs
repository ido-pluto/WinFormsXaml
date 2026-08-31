using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.NativeMarqueeValidation
{
    internal static class Program
    {
        private const int FailureExitCode = 1;
        private const int SkipExitCode = 2;
        private const int TerminalStateUnclaimed = 0;
        private const int TerminalStatePass = 1;
        private const int TerminalStateFail = 2;
        private const int TerminalStateSkip = 3;
        private const int WindowStyleIndex = -16;
        private const int NativeMarqueeStyle = 0x00000008;
        private const int SetMarqueeMessage = 0x040A;
        private static readonly object _terminalSync = new object();
        private static int _terminalState = TerminalStateUnclaimed;

        [STAThread]
        private static int Main()
        {
            OperatingSystem operatingSystem = Environment.OSVersion;

            if (operatingSystem.Platform != PlatformID.Win32NT)
            {
                return Skip(
                    "a direct Windows NT-family host is required; Mono and " +
                    "other non-Windows hosts are not native marquee evidence");
            }

            if (operatingSystem.Version.Major < 5 ||
                (operatingSystem.Version.Major == 5 &&
                 operatingSystem.Version.Minor == 0))
            {
                return Skip(
                    "this Windows version does not provide the supported " +
                    "version 6 Common Controls marquee path");
            }

            try
            {
                // This must remain the first operation that can initialize
                // WinForms rendering. No Control may be constructed above it.
                Application.EnableVisualStyles();

                if (!Application.RenderWithVisualStyles)
                {
                    return Skip(
                        "Application.EnableVisualStyles did not activate client " +
                        "visual styles on this Windows session");
                }

                ValidateNativeMarquee();
                if (TryReportTerminal(
                    TerminalStatePass,
                    false,
                    "WINFORMSXAML_NATIVE_MARQUEE: PASS - visual styles were " +
                    "enabled before control creation and " +
                    "CompatibleProgressBar retained the native marquee HWND " +
                    "path"))
                {
                    return 0;
                }

                return FailureExitCode;
            }
            catch (Exception ex)
            {
                TryReportTerminal(
                    TerminalStateFail,
                    true,
                    "WINFORMSXAML_NATIVE_MARQUEE: FAIL - " +
                    ex.GetType().FullName +
                    ": " +
                    ex.Message);
                return FailureExitCode;
            }
        }

        private static void ValidateNativeMarquee()
        {
            Exception validationFailure = null;
            bool validationCompleted = false;

            using (Form form = new Form())
            using (CompatibleProgressBar progress =
                new CompatibleProgressBar())
            using (System.Windows.Forms.Timer probeTimer =
                new System.Windows.Forms.Timer())
            {
                ProgressBar nativeProgress = progress;
                form.Text = "WinFormsXaml native marquee validation";
                form.ShowInTaskbar = false;
                form.Width = 200;
                form.Height = 80;
                progress.PreferMarqueeFallback = false;
                nativeProgress.Style = ProgressBarStyle.Marquee;
                nativeProgress.MarqueeAnimationSpeed = 37;
                progress.Width = 160;
                progress.Height = 24;
                progress.Left = 12;
                progress.Top = 12;
                form.Controls.Add(progress);
                probeTimer.Interval = 100;
                probeTimer.Tick +=
                    delegate
                    {
                        probeTimer.Stop();

                        try
                        {
                            ValidateNativeMarqueeHandle(progress);
                            validationCompleted = true;
                        }
                        catch (Exception ex)
                        {
                            validationFailure = ex;
                        }
                        finally
                        {
                            form.Close();
                        }
                    };
                form.Shown +=
                    delegate
                    {
                        probeTimer.Start();
                    };

                System.Threading.Timer watchdog =
                    new System.Threading.Timer(
                        delegate(object state)
                        {
                            if (TryReportTerminal(
                                TerminalStateFail,
                                true,
                                "WINFORMSXAML_NATIVE_MARQUEE: FAIL - the " +
                                "shown Form validation did not complete " +
                                "within 10 seconds"))
                            {
                                Environment.Exit(FailureExitCode);
                            }
                        },
                        null,
                        10000,
                        System.Threading.Timeout.Infinite);

                try
                {
                    Application.Run(form);
                }
                finally
                {
                    watchdog.Dispose();
                }
            }

            if (validationFailure != null)
                throw validationFailure;

            Assert(
                validationCompleted,
                "the shown Form closed before native marquee validation ran");
        }

        private static void ValidateNativeMarqueeHandle(
            CompatibleProgressBar progress)
        {
            ProgressBar nativeProgress = progress;
            IntPtr handle = progress.Handle;

            Assert(
                handle != IntPtr.Zero,
                "the validation progress bar did not create a native handle");
            Assert(
                progress.Visible,
                "the validation progress bar was not visible on the shown Form");

            int nativeStyle = GetWindowLong(
                handle,
                WindowStyleIndex);

            Assert(
                (nativeStyle & NativeMarqueeStyle) ==
                    NativeMarqueeStyle,
                "the created progress HWND does not contain PBS_MARQUEE");
            Assert(
                nativeProgress.Style == ProgressBarStyle.Marquee,
                "the public Style property no longer reports Marquee");
            Assert(
                nativeProgress.MarqueeAnimationSpeed == 37,
                "the public MarqueeAnimationSpeed value was not retained");
            Assert(
                ReadBooleanField(
                    progress,
                    "_rendererSelectionInitialized"),
                "CompatibleProgressBar did not snapshot a renderer for the " +
                "created handle");
            Assert(
                !ReadBooleanField(
                    progress,
                    "_useLegacyRendererForHandle"),
                "CompatibleProgressBar selected the legacy renderer on a " +
                "visual-styles-capable handle");
            Assert(
                !ReadBooleanField(
                    progress,
                    "_legacyMarqueeActive"),
                "the legacy marquee animation became active on the native path");
            Assert(
                ReadIntPtrField(progress, "_maskHandle") ==
                    IntPtr.Zero,
                "the fallback mask HWND was created on the native path");

            IntPtr messageResult = SendMessage(
                handle,
                SetMarqueeMessage,
                new IntPtr(1),
                new IntPtr(37));

            Assert(
                messageResult != IntPtr.Zero,
                "the native progress HWND rejected PBM_SETMARQUEE");
        }

        private static bool ReadBooleanField(
            CompatibleProgressBar progress,
            string fieldName)
        {
            return Convert.ToBoolean(
                ReadField(progress, fieldName),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static IntPtr ReadIntPtrField(
            CompatibleProgressBar progress,
            string fieldName)
        {
            return (IntPtr)ReadField(progress, fieldName);
        }

        private static object ReadField(
            CompatibleProgressBar progress,
            string fieldName)
        {
            FieldInfo field = typeof(CompatibleProgressBar).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                throw new InvalidOperationException(
                    "CompatibleProgressBar no longer exposes the expected " +
                    "validation field '" +
                    fieldName +
                    "'.");
            }

            return field.GetValue(progress);
        }

        private static int Skip(string reason)
        {
            TryReportTerminal(
                TerminalStateSkip,
                false,
                "WINFORMSXAML_NATIVE_MARQUEE: SKIP - " + reason);
            return SkipExitCode;
        }

        private static bool TryReportTerminal(
            int terminalState,
            bool writeToError,
            string message)
        {
            lock (_terminalSync)
            {
                if (_terminalState != TerminalStateUnclaimed)
                    return false;

                _terminalState = terminalState;

                if (writeToError)
                    Console.Error.WriteLine(message);
                else
                    Console.WriteLine(message);

                return true;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(
            IntPtr window,
            int index);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr window,
            int message,
            IntPtr wParam,
            IntPtr lParam);
    }
}
