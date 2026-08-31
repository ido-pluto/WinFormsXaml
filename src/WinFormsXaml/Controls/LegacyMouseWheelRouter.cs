using System;
using System.Collections;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinFormsXaml
{
    internal sealed class LegacyMouseWheelRouter : IMessageFilter
    {
        private const string LegacyMouseWheelMessage = "MSWHEEL_ROLLMSG";

        [ThreadStatic]
        private static LegacyMouseWheelRouter _current;

        private readonly ArrayList _hosts;
        private readonly int _messageId;

        private LegacyMouseWheelRouter(int messageId)
        {
            _messageId = messageId;
            _hosts = new ArrayList();
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);

        internal static bool Register(XamlRuntime.ItemsControl host)
        {
            if (host == null)
                return false;

            LegacyMouseWheelRouter router = _current;

            if (router == null)
            {
                int messageId;

                try
                {
                    messageId = RegisterWindowMessage(
                        LegacyMouseWheelMessage);
                }
                catch
                {
                    return false;
                }

                if (messageId == 0)
                    return false;

                router = new LegacyMouseWheelRouter(messageId);

                try
                {
                    Application.AddMessageFilter(router);
                }
                catch
                {
                    return false;
                }

                _current = router;
            }

            router.RemoveHost(host);
            router._hosts.Add(new WeakReference(host));
            return true;
        }

        internal static void Unregister(XamlRuntime.ItemsControl host)
        {
            LegacyMouseWheelRouter router = _current;

            if (router == null)
                return;

            router.RemoveHost(host);
            router.PruneHosts();

            if (router._hosts.Count != 0)
                return;

            try
            {
                Application.RemoveMessageFilter(router);
            }
            catch
            {
            }

            _current = null;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != _messageId)
                return false;

            XamlRuntime.ItemsControl host = FindTargetHost();

            if (host == null)
                return false;

            int delta = DecodeWheelDelta(m.WParam);

            if (!host.ProcessLegacyMouseWheel(delta))
                return false;

            m.Result = new IntPtr(1);
            return true;
        }

        /// <summary>
        /// MSWHEEL_ROLLMSG stores its signed wheel delta directly in wParam.
        /// Taking the signed low 32 bits handles both the original Win9x
        /// 32-bit payload and a legacy sender running in a 64-bit process.
        /// </summary>
        internal static int DecodeWheelDelta(IntPtr value)
        {
            return unchecked((int)value.ToInt64());
        }

        private XamlRuntime.ItemsControl FindTargetHost()
        {
            PruneHosts();
            Point screen = Control.MousePosition;
            Control target = null;

            try
            {
                IntPtr window = WindowFromPoint(screen);

                if (window != IntPtr.Zero)
                    target = Control.FromChildHandle(window);
            }
            catch
            {
            }

            while (target != null)
            {
                XamlRuntime.ItemsControl direct =
                    target as XamlRuntime.ItemsControl;

                if (IsEligible(direct) && ContainsHost(direct))
                    return direct;

                target = target.Parent;
            }

            XamlRuntime.ItemsControl best = null;
            int bestDepth = -1;
            int i;

            for (i = 0; i < _hosts.Count; i++)
            {
                WeakReference reference = _hosts[i] as WeakReference;
                XamlRuntime.ItemsControl candidate = reference == null
                    ? null
                    : reference.Target as XamlRuntime.ItemsControl;

                if (!IsEligible(candidate))
                    continue;

                Rectangle bounds;

                try
                {
                    bounds = candidate.RectangleToScreen(
                        candidate.ClientRectangle);
                }
                catch
                {
                    continue;
                }

                if (!bounds.Contains(screen))
                    continue;

                int depth = GetParentDepth(candidate);

                if (depth > bestDepth)
                {
                    best = candidate;
                    bestDepth = depth;
                }
            }

            return best;
        }

        private bool ContainsHost(XamlRuntime.ItemsControl host)
        {
            int i;

            for (i = 0; i < _hosts.Count; i++)
            {
                WeakReference reference = _hosts[i] as WeakReference;

                if (reference != null &&
                    Object.ReferenceEquals(reference.Target, host))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveHost(XamlRuntime.ItemsControl host)
        {
            int i;

            for (i = _hosts.Count - 1; i >= 0; i--)
            {
                WeakReference reference = _hosts[i] as WeakReference;
                object target = reference == null
                    ? null
                    : reference.Target;

                if (target == null || Object.ReferenceEquals(target, host))
                    _hosts.RemoveAt(i);
            }
        }

        private void PruneHosts()
        {
            RemoveHost(null);
        }

        private static bool IsEligible(XamlRuntime.ItemsControl host)
        {
            return host != null &&
                !host.IsDisposed &&
                !host.Disposing &&
                host.IsHandleCreated &&
                host.Visible &&
                host.Enabled &&
                host.AutoScroll;
        }

        private static int GetParentDepth(Control control)
        {
            int depth = 0;

            while (control != null && depth < Int32.MaxValue)
            {
                depth++;
                control = control.Parent;
            }

            return depth;
        }
    }
}
