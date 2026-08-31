using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    /// <summary>
    /// Exercises the real WinForms smooth-scroll timer while wheel requests
    /// are still arriving. These tests deliberately do not use
    /// ApplySmoothScrollFrameForTest: completing every transition before the
    /// next input cannot reproduce the coalesced path used by an application.
    /// </summary>
    internal static class ItemsControlStyledSmoothBurstTests
    {
        private sealed class ComplexRow
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Detail;
            public readonly string LinkText;
            public readonly string Url;
            public readonly bool Checked;

            public ComplexRow(int index)
            {
                Id = "natural-burst-" + index;
                Title = "Notification " + index;
                Detail = "Nested content for notification " + index;
                LinkText = "Open " + index;
                Url = "https://example.invalid/notifications/" + index;
                Checked = (index & 1) == 0;
            }
        }

        private sealed class FrameSample
        {
            public int ContentOffset;
            public int BarValue;
            public int Maximum;
            public int LargeChange;
            public Rectangle BarBounds;
            public Rectangle ViewportBounds;
            public Size ClientSize;
            public ScrollBarGeometry Geometry;
            public bool ActiveNativeChrome;
            public bool SecondaryNativeChrome;
            public int SecondaryOffset;
        }

        private sealed class FrameTrace : IDisposable
        {
            private readonly XamlRuntime.ItemsControl _host;
            private readonly ScrollBarControl _bar;
            private readonly EventHandler _valueChanged;
            private readonly ArrayList _samples;
            private int _naturalFrameCount;

            public FrameTrace(
                XamlRuntime.ItemsControl host,
                ScrollBarControl bar)
            {
                _host = host;
                _bar = bar;
                _samples = new ArrayList();
                _valueChanged = new EventHandler(BarValueChanged);
                _bar.ValueChanged += _valueChanged;
            }

            public ArrayList Samples
            {
                get { return _samples; }
            }

            public int NaturalFrameCount
            {
                get { return _naturalFrameCount; }
            }

            public void Capture()
            {
                FrameSample sample = new FrameSample();
                sample.ContentOffset =
                    _host.GetLogicalScrollOffset();
                sample.BarValue = _bar.Value;
                sample.Maximum =
                    _bar.EffectiveMaximumForTest;
                sample.LargeChange = _bar.LargeChange;
                sample.BarBounds = _bar.Bounds;
                sample.ViewportBounds =
                    _host.ItemsViewportRectangleForTest;
                sample.ClientSize = _host.ClientSize;
                sample.Geometry =
                    _bar.GetScrollBarGeometryForTest();
                sample.ActiveNativeChrome =
                    _host.ActiveNativeScrollStyleVisibleForTest;
                sample.SecondaryNativeChrome =
                    _host.SecondaryNativeScrollStyleVisibleForTest;
                sample.SecondaryOffset =
                    _host.AutoScrollPosition.X;
                _samples.Add(sample);
            }

            private void BarValueChanged(
                object sender,
                EventArgs e)
            {
                _naturalFrameCount++;
                Capture();
            }

            public void Dispose()
            {
                _bar.ValueChanged -= _valueChanged;
            }
        }

        internal static void RunAll()
        {
            if (SystemInformation.MouseWheelScrollLines == 0)
                return;

            Exception nonvirtualFailure = null;
            Exception virtualFailure = null;

            try
            {
                TestNaturalCoalescedWheelBurst(false);
            }
            catch (Exception ex)
            {
                nonvirtualFailure = ex;
            }

            try
            {
                TestNaturalCoalescedWheelBurst(true);
            }
            catch (Exception ex)
            {
                virtualFailure = ex;
            }

            if (nonvirtualFailure != null || virtualFailure != null)
            {
                string message =
                    "Natural styled smooth-scroll burst failures:";

                if (nonvirtualFailure != null)
                {
                    message += " nonvirtual: " +
                        nonvirtualFailure.Message + ";";
                }

                if (virtualFailure != null)
                {
                    message += " virtualized: " +
                        virtualFailure.Message + ";";
                }

                throw new InvalidOperationException(
                    message,
                    nonvirtualFailure != null
                        ? nonvirtualFailure
                        : virtualFailure);
            }
        }

        private static void TestNaturalCoalescedWheelBurst(
            bool virtualizing)
        {
            const int WheelInputCount = 12;
            const int InputSpacingMilliseconds = 20;
            string mode = virtualizing
                ? "virtualized"
                : "nonvirtual";
            XamlRuntime styledRuntime = XamlRuntime.Load(
                CreateMarkup(virtualizing, true));
            XamlRuntime nativeRuntime = XamlRuntime.Load(
                CreateMarkup(virtualizing, false));
            Form form = new Form();

            try
            {
                XamlRuntime.ItemsControl styled =
                    styledRuntime.GetItemsControl("Rows");
                XamlRuntime.ItemsControl native =
                    nativeRuntime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(40);

                form.ClientSize = new Size(900, 270);
                styled.Location = new Point(12, 12);
                native.Location = new Point(456, 12);
                form.Controls.Add(styled);
                form.Controls.Add(native);
                styled.SetItems(rows);
                native.SetItems(rows);
                form.Show();
                PumpForMilliseconds(30);

                ScrollBarControl bar =
                    styled.ThemedScrollBarForTest;

                AssertTrue(
                    bar != null && bar.Visible,
                    mode + " fixture exposes the styled scrollbar");
                AssertTrue(
                    native.ThemedScrollBarForTest == null &&
                    native.ActiveNativeScrollStyleVisibleForTest,
                    mode + " comparison fixture uses the native scrollbar");
                AssertTrue(
                    virtualizing == styled.DirectVirtualActive,
                    mode + " styled fixture uses the requested renderer");
                AssertTrue(
                    virtualizing == native.DirectVirtualActive,
                    mode + " native fixture uses the requested renderer");

                native.SmoothScroll = false;
                SendWheelInputs(native, WheelInputCount, false);
                int expectedFinalOffset =
                    native.GetLogicalScrollOffset();

                AssertTrue(
                    expectedFinalOffset > 0,
                    mode + " native wheel comparison produces displacement");

                styled.SetLogicalScrollOffset(0);
                styled.SmoothScroll = true;
                styled.SmoothScrollDuration = 180;
                Rectangle initialBarBounds = bar.Bounds;
                Rectangle initialViewport =
                    styled.ItemsViewportRectangleForTest;
                Size initialClientSize = styled.ClientSize;
                ScrollBarGeometry initialGeometry =
                    bar.GetScrollBarGeometryForTest();
                int initialMaximum =
                    bar.EffectiveMaximumForTest;
                int initialLargeChange = bar.LargeChange;
                long initialHideAttempts =
                    styled.ThemedNativeHideAttemptCountForTest;
                Exception frameFailure = null;

                using (FrameTrace trace = new FrameTrace(styled, bar))
                {
                    int i;
                    trace.Capture();

                    for (i = 0; i < WheelInputCount; i++)
                    {
                        InvokeHostMouseWheel(styled, -120);
                        trace.Capture();
                        PumpForMilliseconds(
                            InputSpacingMilliseconds);
                    }

                    PumpUntilSettled(styled, 2000);
                    trace.Capture();

                    AssertTrue(
                        trace.NaturalFrameCount >= 4,
                        mode + " burst is observed across multiple real timer frames");

                    try
                    {
                        AssertStableForwardFrames(
                            trace.Samples,
                            initialBarBounds,
                            initialViewport,
                            initialClientSize,
                            initialGeometry,
                            initialMaximum,
                            initialLargeChange,
                            mode);
                    }
                    catch (Exception ex)
                    {
                        frameFailure = ex;
                    }
                }

                Hashtable completionReported = new Hashtable();
                ArrayList completionFailures = new ArrayList();

                RecordConditionFailure(
                    completionReported,
                    completionFailures,
                    "animation-settled",
                    !styled.SmoothScrollAnimationActiveForTest,
                    mode + " natural animation settles before verification",
                    false,
                    styled.SmoothScrollAnimationActiveForTest);
                RecordEqualFailure(
                    completionReported,
                    completionFailures,
                    "final-content-offset",
                    expectedFinalOffset,
                    styled.GetLogicalScrollOffset(),
                    mode + " styled burst preserves native wheel displacement");
                RecordEqualFailure(
                    completionReported,
                    completionFailures,
                    "final-thumb-offset",
                    expectedFinalOffset,
                    bar.Value,
                    mode + " final thumb reaches the exact content offset");
                RecordConditionFailure(
                    completionReported,
                    completionFailures,
                    "bar-identity",
                    Object.ReferenceEquals(
                        bar,
                        styled.ThemedScrollBarForTest),
                    mode + " burst retains one styled scrollbar instance",
                    true,
                    Object.ReferenceEquals(
                        bar,
                        styled.ThemedScrollBarForTest));
                RecordEqualFailure(
                    completionReported,
                    completionFailures,
                    "native-hide-attempts",
                    initialHideAttempts,
                    styled.ThemedNativeHideAttemptCountForTest,
                    mode + " burst never toggles and re-hides native chrome");

                if (virtualizing)
                {
                    RecordConditionFailure(
                        completionReported,
                        completionFailures,
                        "virtual-realized-count",
                        styled.RenderedItems.Count < rows.Count,
                        "virtualized burst keeps the complex source virtualized",
                        "< " + rows.Count,
                        styled.RenderedItems.Count);
                }

                if (frameFailure != null ||
                    completionFailures.Count > 0)
                {
                    string failureMessage = frameFailure == null
                        ? String.Empty
                        : frameFailure.Message;
                    int failureIndex;

                    for (failureIndex = 0;
                         failureIndex < completionFailures.Count;
                         failureIndex++)
                    {
                        if (failureMessage.Length > 0)
                            failureMessage += " | ";

                        failureMessage +=
                            completionFailures[failureIndex];
                    }

                    throw new InvalidOperationException(
                        failureMessage,
                        frameFailure);
                }
            }
            finally
            {
                form.Dispose();
                styledRuntime.Dispose();
                nativeRuntime.Dispose();
            }
        }

        private static void AssertStableForwardFrames(
            ArrayList samples,
            Rectangle initialBarBounds,
            Rectangle initialViewport,
            Size initialClientSize,
            ScrollBarGeometry initialGeometry,
            int initialMaximum,
            int initialLargeChange,
            string mode)
        {
            int previousOffset = -1;
            int previousThumbStart = -1;
            Hashtable reported = new Hashtable();
            ArrayList failures = new ArrayList();
            int i;

            for (i = 0; i < samples.Count; i++)
            {
                FrameSample sample = (FrameSample)samples[i];

                RecordEqualFailure(
                    reported,
                    failures,
                    "content-thumb",
                    sample.ContentOffset,
                    sample.BarValue,
                    mode + " frame " + i +
                    " publishes content and thumb atomically");
                RecordConditionFailure(
                    reported,
                    failures,
                    "content-monotonic",
                    sample.ContentOffset >= previousOffset,
                    mode + " content never moves backward in a forward burst at frame " + i,
                    ">= " + previousOffset,
                    sample.ContentOffset);
                RecordConditionFailure(
                    reported,
                    failures,
                    "thumb-monotonic",
                    sample.Geometry.ThumbStart >= previousThumbStart,
                    mode + " painted thumb never jumps backward at frame " + i,
                    ">= " + previousThumbStart,
                    sample.Geometry.ThumbStart);
                RecordEqualFailure(
                    reported,
                    failures,
                    "bar-bounds",
                    initialBarBounds,
                    sample.BarBounds,
                    mode + " scrollbar strip remains fixed at frame " + i);
                RecordEqualFailure(
                    reported,
                    failures,
                    "viewport-bounds",
                    initialViewport,
                    sample.ViewportBounds,
                    mode + " item viewport remains fixed at frame " + i);
                RecordEqualFailure(
                    reported,
                    failures,
                    "client-size",
                    initialClientSize,
                    sample.ClientSize,
                    mode + " host client size remains fixed at frame " + i);
                RecordEqualFailure(
                    reported,
                    failures,
                    "first-button",
                    initialGeometry.FirstButton,
                    sample.Geometry.FirstButton,
                    mode + " leading arrow remains fixed at frame " + i);
                RecordEqualFailure(
                    reported,
                    failures,
                    "last-button",
                    initialGeometry.LastButton,
                    sample.Geometry.LastButton,
                    mode + " trailing arrow remains fixed at frame " + i);
                RecordEqualFailure(
                    reported,
                    failures,
                    "track",
                    initialGeometry.Track,
                    sample.Geometry.Track,
                    mode + " scrollbar track remains fixed at frame " + i);
                RecordEqualFailure(
                    reported,
                    failures,
                    "thumb-length",
                    initialGeometry.ThumbLength,
                    sample.Geometry.ThumbLength,
                    mode + " thumb length remains stable at frame " + i);
                RecordEqualFailure(
                    reported,
                    failures,
                    "maximum",
                    initialMaximum,
                    sample.Maximum,
                    mode + " scroll extent remains stable at frame " + i);
                RecordEqualFailure(
                    reported,
                    failures,
                    "large-change",
                    initialLargeChange,
                    sample.LargeChange,
                    mode + " viewport page remains stable at frame " + i);
                RecordConditionFailure(
                    reported,
                    failures,
                    "native-chrome",
                    !sample.ActiveNativeChrome &&
                    !sample.SecondaryNativeChrome,
                    mode + " frame " + i +
                    " never exposes native scrollbar chrome",
                    "active=false, secondary=false",
                    "active=" + sample.ActiveNativeChrome +
                    ", secondary=" + sample.SecondaryNativeChrome);
                RecordEqualFailure(
                    reported,
                    failures,
                    "secondary-offset",
                    0,
                    sample.SecondaryOffset,
                    mode + " frame " + i +
                    " never moves the secondary axis");

                previousOffset = sample.ContentOffset;
                previousThumbStart = sample.Geometry.ThumbStart;
            }

            if (failures.Count > 0)
            {
                string message = String.Empty;

                for (i = 0; i < failures.Count; i++)
                {
                    if (i > 0)
                        message += " | ";

                    message += failures[i];
                }

                throw new InvalidOperationException(message);
            }
        }

        private static void RecordEqualFailure(
            Hashtable reported,
            ArrayList failures,
            string key,
            object expected,
            object actual,
            string message)
        {
            if (Object.Equals(expected, actual) ||
                reported.ContainsKey(key))
            {
                return;
            }

            reported[key] = true;
            failures.Add(
                message + ". Expected: " + expected +
                "; actual: " + actual + ".");
        }

        private static void RecordConditionFailure(
            Hashtable reported,
            ArrayList failures,
            string key,
            bool condition,
            string message,
            object expected,
            object actual)
        {
            if (condition || reported.ContainsKey(key))
                return;

            reported[key] = true;
            failures.Add(
                message + ". Expected: " + expected +
                "; actual: " + actual + ".");
        }

        private static void SendWheelInputs(
            XamlRuntime.ItemsControl host,
            int count,
            bool pumpBetweenInputs)
        {
            int i;

            for (i = 0; i < count; i++)
            {
                InvokeHostMouseWheel(host, -120);

                if (pumpBetweenInputs)
                    PumpForMilliseconds(20);
            }
        }

        private static void InvokeHostMouseWheel(
            XamlRuntime.ItemsControl host,
            int delta)
        {
            MethodInfo method = typeof(XamlRuntime.ItemsControl).GetMethod(
                "OnMouseWheel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            HandledMouseEventArgs args = new HandledMouseEventArgs(
                MouseButtons.None,
                0,
                1,
                1,
                delta);

            AssertTrue(
                method != null,
                "ItemsControl wheel entry point is available to the regression");
            method.Invoke(host, new object[] { args });
        }

        private static void PumpUntilSettled(
            XamlRuntime.ItemsControl host,
            int timeoutMilliseconds)
        {
            Stopwatch elapsed = Stopwatch.StartNew();

            while (host.SmoothScrollAnimationActiveForTest &&
                   elapsed.ElapsedMilliseconds < timeoutMilliseconds)
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }

            Application.DoEvents();
            AssertTrue(
                !host.SmoothScrollAnimationActiveForTest,
                "natural smooth-scroll timer settles within the timeout");
        }

        private static void PumpForMilliseconds(int milliseconds)
        {
            Stopwatch elapsed = Stopwatch.StartNew();

            while (elapsed.ElapsedMilliseconds < milliseconds)
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }
        }

        private static ArrayList CreateRows(int count)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new ComplexRow(i));

            return rows;
        }

        private static string CreateMarkup(
            bool virtualizing,
            bool styled)
        {
            string style = styled
                ? "  <ItemsControl.VerticalScrollStyle>" +
                  "    <ScrollBarStyle Thickness='16' " +
                  "      TrackColor='#202124' ThumbColor='#80868B' />" +
                  "  </ItemsControl.VerticalScrollStyle>"
                : String.Empty;

            return
                "<ItemsControl Name='Rows' Width='420' Height='230' " +
                "AutoScroll='true' Virtualizing='" +
                (virtualizing ? "true" : "false") + "' " +
                "VirtualizationThreshold='1' FixedItemSize='72' " +
                "OverscanItems='3' VirtualizationCacheItems='12' " +
                "ProgressiveRendering='false' ItemKeyPath='Id' " +
                "Spacing='6' ScrollBarGap='4'>" +
                style +
                "  <ItemsControl.ItemTemplate>" +
                "    <Border Height='72' Padding='5' " +
                "      BorderBrush='#D1D5DB' BorderThickness='1'>" +
                "      <StackPanel Spacing='2'>" +
                "        <StackPanel Orientation='Horizontal' Spacing='5'>" +
                "          <Label Width='170' Text='{Binding Title}' />" +
                "          <CheckBox Width='65' Text='Enabled' " +
                "            Checked='{Binding Checked}' />" +
                "        </StackPanel>" +
                "        <Label Text='{Binding Detail}' />" +
                "        <HyperlinkLabel Text='{Binding LinkText}' " +
                "          NavigateUri='{Binding Url}' />" +
                "      </StackPanel>" +
                "    </Border>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
        }

        private static void AssertTrue(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ". Expected: " + expected +
                    "; actual: " + actual + ".");
            }
        }
    }
}
