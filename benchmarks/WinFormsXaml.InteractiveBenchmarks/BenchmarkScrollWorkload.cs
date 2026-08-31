using System;
using System.Drawing;
using System.Runtime.InteropServices;
using WinFormsXaml;

namespace WinFormsXaml.InteractiveBenchmarks
{
    internal sealed class BenchmarkScrollWorkload
    {
        private const int SmoothScrollDurationMilliseconds = 120;
        private readonly BenchmarkProfile _profile;
        private readonly int _rowCount;
        private readonly bool _smoothScroll;
        private readonly bool _styledScrollBar;

        public BenchmarkScrollWorkload(
            BenchmarkProfile profile,
            int rowCount,
            bool smoothScroll,
            bool styledScrollBar)
        {
            if (rowCount <= 0)
                throw new ArgumentOutOfRangeException("rowCount");

            _profile = profile;
            _rowCount = rowCount;
            _smoothScroll = smoothScroll;
            _styledScrollBar = styledScrollBar;
        }

        public bool UsesSmoothScroll
        {
            get { return _smoothScroll; }
        }

        public int SmoothSettleDuration
        {
            get
            {
                return _smoothScroll
                    ? SmoothScrollDurationMilliseconds
                    : 0;
            }
        }

        public string Description
        {
            get
            {
                string renderer = _styledScrollBar
                    ? "framework-owned styled scrollbar"
                    : "native scrollbar";

                if (_smoothScroll)
                {
                    return
                        "native WM_MOUSEWHEEL messages with 120 ms " +
                        "coalesced smooth scrolling and " + renderer;
                }

                if (_profile != BenchmarkProfile.NonVirtual)
                {
                    return "mixed direct small offsets and ScrollToIndex jumps";
                }

                return
                    "native WM_MOUSEWHEEL messages and immediate " +
                    "ScrollToIndex jumps with " + renderer;
            }
        }

        public void Configure(XamlRuntime.ItemsControl items)
        {
            if (items == null)
                throw new ArgumentNullException("items");

            items.SmoothScroll = _smoothScroll;

            if (_styledScrollBar)
            {
                ScrollBarStyle style = new ScrollBarStyle();
                style.TrackColor = Color.FromArgb(32, 33, 36);
                style.ThumbColor = Color.FromArgb(128, 134, 139);
                style.ThumbHoverColor = Color.FromArgb(154, 160, 166);
                style.Thickness = 16;
                items.ScrollBarGap = 6;
                items.VerticalScrollStyle = style;
            }

            if (_smoothScroll)
            {
                items.SmoothScrollDuration =
                    SmoothScrollDurationMilliseconds;
            }
        }

        public void Apply(
            XamlRuntime.ItemsControl items,
            int operation)
        {
            if (items == null)
                throw new ArgumentNullException("items");

            if (_smoothScroll)
            {
                ApplySmoothWheel(items, operation);
                return;
            }

            if (_profile == BenchmarkProfile.NonVirtual)
            {
                ApplyNonVirtual(items, operation);
                return;
            }

            ApplyExistingVirtualizedWorkload(items, operation);
        }

        private void ApplyNonVirtual(
            XamlRuntime.ItemsControl items,
            int operation)
        {
            if (operation % 10 == 0)
            {
                int index =
                    ((operation * 53) + 31) % _rowCount;
                items.ScrollToIndex(index);
                return;
            }

            int direction = (operation / 30) % 2 == 0
                ? -1
                : 1;
            NativeMouseWheel.Send(items, direction * 120);
        }

        private static void ApplySmoothWheel(
            XamlRuntime.ItemsControl items,
            int operation)
        {
            int direction = (operation / 30) % 2 == 0
                ? -1
                : 1;
            NativeMouseWheel.Send(items, direction * 120);
        }

        private void ApplyExistingVirtualizedWorkload(
            XamlRuntime.ItemsControl items,
            int operation)
        {
            if (operation % 8 == 0)
            {
                int index = (operation * 7919) % _rowCount;
                items.ScrollToIndex(index);
                return;
            }

            Point native = items.AutoScrollPosition;
            int current = Math.Max(0, -native.Y);
            int direction = (operation / 24) % 2 == 0 ? 1 : -1;
            int requested = current + direction * 3 * 76;
            int maximum = Math.Max(
                0,
                items.AutoScrollMinSize.Height -
                items.ClientSize.Height);
            requested = Math.Max(0, Math.Min(maximum, requested));
            items.AutoScrollPosition = new Point(0, requested);
        }
    }

    internal static class NativeMouseWheel
    {
        private const int WmMouseWheel = 0x020A;

        public static void Send(
            XamlRuntime.ItemsControl items,
            int delta)
        {
            if (!items.IsHandleCreated)
                items.CreateControl();

            Point clientCenter = new Point(
                Math.Max(0, items.ClientSize.Width / 2),
                Math.Max(0, items.ClientSize.Height / 2));
            Point screenCenter = items.PointToScreen(clientCenter);
            int coordinates =
                (screenCenter.X & 0xFFFF) |
                ((screenCenter.Y & 0xFFFF) << 16);
            int wheel = unchecked(delta << 16);

            SendMessage(
                items.Handle,
                WmMouseWheel,
                new IntPtr(wheel),
                new IntPtr(coordinates));
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr window,
            int message,
            IntPtr wParam,
            IntPtr lParam);
    }
}
