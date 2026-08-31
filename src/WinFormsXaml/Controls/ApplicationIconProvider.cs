using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    internal static class ApplicationIconProvider
    {
        private static readonly object SyncRoot = new object();
        private static Icon _applicationIcon;
        private static bool _initialized;

        public static Icon GetApplicationIcon()
        {
            lock (SyncRoot)
            {
                if (!_initialized)
                {
                    _initialized = true;

                    try
                    {
                        string executablePath = Application.ExecutablePath;

                        if (!String.IsNullOrEmpty(executablePath))
                        {
                            _applicationIcon =
                                Icon.ExtractAssociatedIcon(executablePath);
                        }
                    }
                    catch
                    {
                        _applicationIcon = null;
                    }
                }

                if (_applicationIcon == null)
                    _applicationIcon = SystemIcons.Application;

                try
                {
                    // Forms receive independent instances so disposing one
                    // runtime cannot invalidate another form's icon.
                    return (Icon)_applicationIcon.Clone();
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
