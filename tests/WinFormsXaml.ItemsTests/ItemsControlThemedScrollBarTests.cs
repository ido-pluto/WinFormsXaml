using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class ItemsControlThemedScrollBarTests
    {
        private const int WindowLongStyle = -16;
        private const int WindowStyleVerticalScroll = 0x00200000;
        private const int WindowLeftButtonDown = 0x0201;
        private const int WindowLeftButtonUp = 0x0202;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(
            IntPtr window,
            int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(
            IntPtr window,
            int index,
            int value);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr window,
            int message,
            IntPtr wordParameter,
            IntPtr longParameter);

        private sealed class Row
        {
            public readonly string Id;
            public readonly string Title;

            public Row(int index)
            {
                Id = "styled-scroll-" + index;
                Title = "Styled row " + index;
            }
        }

        private sealed class ComplexRow
        {
            private readonly string _title;
            private readonly string _detail;

            public readonly string Id;
            public readonly bool Checked;
            public int TitleReadCount;
            public int DetailReadCount;

            public ComplexRow(int index)
            {
                Id = "complex-styled-scroll-" + index;
                Checked = (index & 1) == 0;
                _title = "Complex styled row " + index;
                _detail = "Nested detail for row " + index;
            }

            public string Title
            {
                get
                {
                    TitleReadCount++;
                    return _title;
                }
            }

            public string Detail
            {
                get
                {
                    DetailReadCount++;
                    return _detail;
                }
            }
        }

        private sealed class VariableStyledRow
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Detail;
            public readonly string Url;
            public readonly int Height;

            public VariableStyledRow(int index)
            {
                Id = "variable-styled-scroll-" + index;
                Title = "Notification " + index;
                Detail = "A complex notification row with nested content " +
                    index;
                Url = "https://example.invalid/" + index;
                Height = 70;
            }
        }

        private sealed class CountingItemsControl : ItemsControl
        {
            internal int LayoutPassCount;

            protected override void OnLayout(LayoutEventArgs e)
            {
                LayoutPassCount++;
                base.OnLayout(e);
            }
        }

        private sealed class OptionalStyleState
        {
            public readonly PropertyBinding<object> Style;

            public OptionalStyleState(object value)
            {
                Style = new PropertyBinding<object>(value);
            }
        }

        internal static void RunAll()
        {
            TestNullDefaultPreservesNativeScrollbar();
            TestEmptyFalseAndUnsetStylesPreserveNativeScrollbar();
            TestStyleTransitionsPreservePositionAndExclusiveChrome();
            TestStyleTransitionsPreserveFocusedContentIdentity();
            TestXmlStyleAndNonVirtualViewport();
            TestCommandsWheelThumbAndSmoothSynchronization();
            TestThemedRangeIgnoresNativeRangeState();
            TestComplexFastSmoothFramesStayPure();
            TestDirectAndLightweightSynchronization();
            TestVariableVirtualThumbStaysStableDuringBurst();
            TestShownNonVirtualWheelKeepsSingleChrome();
            TestScrollBarGapSupportsNativeAndStyledChrome();
            TestPaddingExactFitUsesManagedVisibility();
            TestResizeRangeAndExternalPosition();
            TestRightToLeftEdgesAndOrientationSelection();
            TestSharedStyleReplacementAndDisposal();
            TestZeroSizeSentinelAxisInvariant();
            TestSecondaryAxisInvariant();
        }

        private static void
            TestEmptyFalseAndUnsetStylesPreserveNativeScrollbar()
        {
            AssertInactiveXmlStyle(
                "VerticalScrollStyle=''",
                "empty XML style");
            AssertInactiveXmlStyle(
                "VerticalScrollStyle='false'",
                "false XML style");
            AssertInactiveXmlPropertyElementStyle();

            const string unsetMarkup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Default'>" +
                "    <Preset Name='Default'>" +
                "      <Set Key='Other' Value='unused' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' Width='180' Height='90' " +
                "      Virtualizing='false' ProgressiveRendering='false' " +
                "      VerticalScrollStyle='{Preset Theme.ScrollStyle}'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label Height='28' Text='{Binding Title}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";
            XamlRuntime unsetRuntime = XamlRuntime.Load(unsetMarkup);

            try
            {
                XamlRuntime.ItemsControl unsetHost =
                    unsetRuntime.GetItemsControl("Rows");
                unsetHost.CreateControl();
                unsetHost.SetItems(CreateRows(20));
                unsetHost.PerformLayout();
                AssertNativeOnly(unsetHost, "unset preset style");
            }
            finally
            {
                unsetRuntime.Dispose();
            }

            OptionalStyleState state =
                new OptionalStyleState(false);
            const string bindingMarkup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "    Virtualizing='false' ProgressiveRendering='false' " +
                "    VerticalScrollStyle='{Binding Style}'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='28' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime bindingRuntime =
                XamlRuntime.Load(bindingMarkup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    bindingRuntime.GetItemsControl("Rows");
                host.CreateControl();
                host.SetItems(CreateRows(20));
                host.PerformLayout();
                AssertNativeOnly(host, "false bound style");

                state.Style.Value = CreateStyle(17);
                Application.DoEvents();
                host.PerformLayout();
                AssertCustomOnly(host, "effective bound style");

                state.Style.Value = String.Empty;
                Application.DoEvents();
                host.PerformLayout();
                AssertNativeOnly(host, "empty bound style");
            }
            finally
            {
                bindingRuntime.Dispose();
            }
        }

        private static void
            TestStyleTransitionsPreservePositionAndExclusiveChrome()
        {
            using (ItemsControl host = CreateManualScrollHost(
                Orientation.Vertical,
                null))
            {
                host.SetLogicalScrollOffset(437);
                int expected = host.GetLogicalScrollOffset();
                AssertTrue(expected > 0, "native host reaches a nonzero offset");
                AssertNativeOnly(host, "before style activation");

                host.VerticalScrollStyle = CreateStyle(17);
                host.PerformLayout();
                AssertEqual(
                    expected,
                    host.GetLogicalScrollOffset(),
                    "style activation preserves logical position");
                AssertCustomOnly(host, "after style activation");

                host.VerticalScrollStyle = null;
                host.PerformLayout();
                AssertEqual(
                    expected,
                    host.GetLogicalScrollOffset(),
                    "style removal preserves logical position");
                AssertNativeOnly(host, "after style removal");
            }
        }

        private static void
            TestStyleTransitionsPreserveFocusedContentIdentity()
        {
            Form form = new Form();
            ItemsControl host = new ItemsControl();
            TextBox editor = new TextBox();

            try
            {
                form.ClientSize = new Size(320, 180);
                host.Bounds = new Rectangle(0, 0, 240, 110);
                host.AutoScroll = true;
                host.AutoScrollMinSize = new Size(1, 800);
                editor.Bounds = new Rectangle(8, 360, 140, 24);
                editor.Text = "Retained editor";
                host.Controls.Add(editor);
                form.Controls.Add(host);
                form.Show();
                editor.Focus();
                Application.DoEvents();

                IntPtr editorHandle = editor.Handle;
                int contentCount = CountApplicationContentControls(host);

                host.VerticalScrollStyle = CreateStyle(17);
                host.PerformLayout();
                Application.DoEvents();

                AssertTrue(
                    editor.ContainsFocus && editor.Handle == editorHandle,
                    "native-to-custom transition preserves focused content identity");
                AssertEqual(
                    contentCount,
                    CountApplicationContentControls(host),
                    "custom transition never inserts chrome into content");

                host.VerticalScrollStyle = null;
                host.PerformLayout();
                Application.DoEvents();

                AssertTrue(
                    editor.ContainsFocus && editor.Handle == editorHandle,
                    "custom-to-native transition preserves focused content identity");
                AssertEqual(
                    contentCount,
                    CountApplicationContentControls(host),
                    "native transition leaves content membership unchanged");
            }
            finally
            {
                form.Dispose();
                host.Dispose();
            }
        }

        private static void TestNullDefaultPreservesNativeScrollbar()
        {
            using (ItemsControl host = CreateManualScrollHost(
                Orientation.Vertical,
                null))
            {
                AssertTrue(
                    host.VerticalScrollStyle == null &&
                    host.HorizontalScrollStyle == null,
                    "themed appearances default to null");
                AssertTrue(
                    host.ThemedScrollBarForTest == null,
                    "the null default does not create infrastructure");
                AssertTrue(
                    host.VerticalScroll.Visible,
                    "the ordinary managed vertical range remains visible");
                AssertTrue(
                    host.ActiveNativeScrollStyleVisibleForTest,
                    "the null default leaves native WS_VSCROLL intact");
            }

            using (CountingItemsControl nativeHost =
                new CountingItemsControl())
            {
                nativeHost.Size = new Size(180, 90);
                nativeHost.CreateControl();
                nativeHost.PerformLayout();
                nativeHost.LayoutPassCount = 0;
                nativeHost.Orientation = Orientation.Horizontal;
                AssertEqual(
                    1,
                    nativeHost.LayoutPassCount,
                    "a null-style orientation change has no themed relayout pass");
            }
        }

        private static void TestXmlStyleAndNonVirtualViewport()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='260' Height='104' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "Padding='3' Spacing='1' ScrollBarGap='6'>" +
                "  <ItemsControl.VerticalScrollStyle>" +
                "    <ScrollBarStyle TrackColor='#202124' " +
                "        ThumbColor='#80868B' Thickness='18' />" +
                "  </ItemsControl.VerticalScrollStyle>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='28' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SmoothScroll = false;
                host.CreateControl();
                host.SetItems(CreateRows(32));
                host.PerformLayout();

                ScrollBarControl bar = host.ThemedScrollBarForTest;
                AssertTrue(
                    host.VerticalScrollStyle != null,
                    "nested XML assigns VerticalScrollStyle");
                AssertTrue(
                    bar is VerticalScrollBar && bar.Visible,
                    "vertical XML style creates one visible framework bar");
                AssertEqual(
                    Color.FromArgb(0x20, 0x21, 0x24),
                    bar.Style.TrackColor,
                    "nested XML style reaches the active bar");
                AssertEqual(18, bar.Width, "XML thickness owns the strip");
                AssertTrue(
                    !host.ActiveNativeScrollStyleVisibleForTest,
                    "the managed native range remains active without native chrome");
                AssertTrue(
                    host.VerticalScroll.Visible,
                    "native managed vertical state remains authoritative");

                Rectangle viewport =
                    host.ItemsViewportRectangleForTest;
                AssertEqual(
                    Math.Max(
                        0,
                        host.ClientSize.Width -
                        host.Padding.Left -
                        host.Padding.Right -
                        bar.Width -
                        host.ScrollBarGap),
                    viewport.Width,
                    "the bar and gap consume one cross-axis viewport strip");

                AssertInfrastructureTailOrder(host, bar);
                AssertRenderedRowsAvoidBar(host, viewport);

                host.ContentRightToLeft = true;
                host.KeepScrollBarOnRight = false;
                host.PerformLayout();
                viewport = host.ItemsViewportRectangleForTest;
                AssertEqual(
                    0,
                    bar.Left,
                    "ordinary RTL places the framework bar on the left");
                AssertEqual(
                    host.Padding.Left +
                        bar.Width +
                        host.ScrollBarGap,
                    viewport.Left,
                    "RTL keeps the configured gap between bar and items");
                AssertRenderedRowsAvoidBar(host, viewport);

                long disposed = host.ItemControlTreeDisposedCount;
                long blueprints = host.ItemTemplateBlueprintBuildCount;
                Control firstRow = GetRenderedControl(host, 0);
                host.ScrollBarGap = 11;
                viewport = host.ItemsViewportRectangleForTest;
                AssertEqual(
                    host.Padding.Left + bar.Width + 11,
                    viewport.Left,
                    "a live gap change republishes RTL viewport geometry");
                AssertTrue(
                    Object.ReferenceEquals(
                        firstRow,
                        GetRenderedControl(host, 0)),
                    "a live gap change preserves item control identity");
                AssertEqual(
                    disposed,
                    host.ItemControlTreeDisposedCount,
                    "a live gap change disposes no item tree");
                AssertEqual(
                    blueprints,
                    host.ItemTemplateBlueprintBuildCount,
                    "a live gap change rebuilds no template blueprint");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestCommandsWheelThumbAndSmoothSynchronization()
        {
            using (ItemsControl host = CreateManualScrollHost(
                Orientation.Vertical,
                new ScrollBarStyle()))
            {
                host.SmoothScroll = false;
                ScrollBarControl bar = host.ThemedScrollBarForTest;
                int initial = host.GetLogicalScrollOffset();
                int valueChangedCount = 0;
                EventHandler valueChanged =
                    delegate { valueChangedCount++; };
                Size originalExtent = host.AutoScrollMinSize;
                int originalNativeMaximum =
                    host.VerticalScroll.Maximum;
                long creationHideAttempts =
                    host.ThemedNativeHideAttemptCountForTest;

                bar.ValueChanged += valueChanged;

                AssertEqual(
                    host.GetMaximumLogicalScrollOffsetForTest,
                    bar.EffectiveMaximumForTest,
                    "styled arrows and wheel share one logical range");

                ScrollBarGeometry hostedGeometry =
                    bar.GetScrollBarGeometryForTest();
                Point hostedArrow = Center(
                    hostedGeometry.LastButton);
                AssertTrue(
                    bar.Parent == null &&
                    CountFrameworkScrollBars(host) == 0,
                    "the hosted arrow strip stays outside scrollable content");
                InvokeMouseButton(bar, "OnMouseDown", hostedArrow);
                InvokeMouseButton(bar, "OnMouseUp", hostedArrow);
                AssertTrue(
                    host.GetLogicalScrollOffset() > initial,
                    "a hosted arrow hit scrolls ItemsControl");
                host.SetLogicalScrollOffset(initial);
                long inputHideAttempts =
                    host.ThemedNativeHideAttemptCountForTest;
                AssertTrue(
                    inputHideAttempts <= creationHideAttempts + 1L,
                    "an unparented handle performs at most one initial native " +
                    "scrollbar convergence before logical input begins");
                valueChangedCount = 0;

                bar.ExecuteScrollCommand(
                    ScrollEventType.SmallIncrement);
                AssertTrue(
                    host.GetLogicalScrollOffset() > initial,
                    "framework arrow command scrolls ItemsControl");
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    bar.Value,
                    "arrow command synchronizes bar and content");
                AssertEqual(
                    1,
                    valueChangedCount,
                    "one arrow input publishes one ValueChanged event");
                AssertTrue(
                    !host.ActiveNativeScrollStyleVisibleForTest,
                    "logical input keeps native chrome masked");
                AssertEqual(
                    inputHideAttempts,
                    host.ThemedNativeHideAttemptCountForTest,
                    "logical commands do not round-trip native scrollbar chrome");

                int repeated;

                for (repeated = 0; repeated < 24; repeated++)
                {
                    bar.ExecuteScrollCommand(
                        ScrollEventType.SmallIncrement);
                    bar.ExecuteScrollCommand(
                        ScrollEventType.SmallDecrement);
                }

                AssertEqual(
                    originalExtent,
                    host.AutoScrollMinSize,
                    "viewport infrastructure never inflates the scroll extent");
                AssertEqual(
                    originalNativeMaximum,
                    host.VerticalScroll.Maximum,
                    "repeated scrolling preserves the managed native range");

                int beforePage = host.GetLogicalScrollOffset();
                valueChangedCount = 0;
                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);
                AssertTrue(
                    host.GetLogicalScrollOffset() > beforePage,
                    "framework page command scrolls ItemsControl");
                AssertEqual(
                    1,
                    valueChangedCount,
                    "one page input publishes one ValueChanged event");

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    int beforeWheel = host.GetLogicalScrollOffset();
                    int perNotch =
                        SystemInformation.MouseWheelScrollLines == -1
                            ? bar.LargeChange
                            : bar.SmallChange *
                                SystemInformation.MouseWheelScrollLines;
                    int expectedWheel = Math.Min(
                        bar.EffectiveMaximumForTest,
                        beforeWheel + (perNotch * 3));
                    valueChangedCount = 0;
                    InvokeMouseWheel(bar, -360);
                    AssertEqual(
                        expectedWheel,
                        host.GetLogicalScrollOffset(),
                        "aggregated wheel notches preserve their full logical delta");
                    AssertEqual(
                        1,
                        valueChangedCount,
                        "one aggregated wheel input publishes one ValueChanged event");
                }

                host.LiveScroll = false;
                int contentBeforeTrack = host.GetLogicalScrollOffset();
                int thumbTarget = Math.Min(
                    bar.EffectiveMaximumForTest,
                    contentBeforeTrack + 137);
                valueChangedCount = 0;
                InvokeBarInputValue(
                    bar,
                    thumbTarget,
                    ScrollEventType.ThumbTrack);
                AssertEqual(
                    contentBeforeTrack,
                    host.GetLogicalScrollOffset(),
                    "LiveScroll=false keeps content fixed during ThumbTrack");
                AssertEqual(
                    thumbTarget,
                    bar.Value,
                    "LiveScroll=false still moves the owner-painted thumb");
                AssertEqual(
                    1,
                    valueChangedCount,
                    "deferred thumb movement publishes one ValueChanged event");

                valueChangedCount = 0;
                InvokeBarInputValue(
                    bar,
                    thumbTarget,
                    ScrollEventType.ThumbPosition);
                AssertEqual(
                    thumbTarget,
                    host.GetLogicalScrollOffset(),
                    "ThumbPosition commits deferred thumb tracking");
                AssertEqual(
                    0,
                    valueChangedCount,
                    "committing an unchanged visual thumb does not republish ValueChanged");

                host.LiveScroll = true;
                int liveTarget = Math.Min(
                    bar.EffectiveMaximumForTest,
                    thumbTarget + 71);
                valueChangedCount = 0;
                InvokeBarInputValue(
                    bar,
                    liveTarget,
                    ScrollEventType.ThumbTrack);
                AssertEqual(
                    liveTarget,
                    host.GetLogicalScrollOffset(),
                    "LiveScroll=true follows ThumbTrack immediately");
                AssertEqual(
                    1,
                    valueChangedCount,
                    "live thumb input publishes one ValueChanged event");

                host.SetLogicalScrollOffset(0);
                host.SmoothScroll = true;
                valueChangedCount = 0;
                long nativeHideAttempts =
                    host.ThemedNativeHideAttemptCountForTest;
                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);
                int target = host.SmoothScrollTargetOffsetForTest;
                AssertTrue(
                    target > 0 &&
                    host.SmoothScrollAnimationActiveForTest,
                    "framework page command starts smooth scrolling");
                AssertEqual(
                    0,
                    bar.Value,
                    "smooth command pins the bar to the current frame");
                AssertEqual(
                    0,
                    valueChangedCount,
                    "starting a smooth command does not publish a phantom value");
                AssertEqual(
                    nativeHideAttempts,
                    host.ThemedNativeHideAttemptCountForTest,
                    "starting a smooth command does not re-hide before a frame is published");

                long synchronizationCount =
                    host.ThemedScrollBarSynchronizationCountForTest;
                host.ApplySmoothScrollFrameForTest(60);
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    bar.Value,
                    "intermediate smooth frame synchronizes the thumb");
                AssertEqual(
                    synchronizationCount + 1L,
                    host.ThemedScrollBarSynchronizationCountForTest,
                    "one smooth frame performs one final scrollbar synchronization");
                AssertEqual(
                    nativeHideAttempts,
                    host.ThemedNativeHideAttemptCountForTest,
                    "one smooth frame performs no native re-hide");
                AssertEqual(
                    1,
                    valueChangedCount,
                    "one smooth frame publishes one actual ValueChanged event");
                valueChangedCount = 0;
                synchronizationCount =
                    host.ThemedScrollBarSynchronizationCountForTest;
                host.ApplySmoothScrollFrameForTest(120);
                AssertEqual(target, bar.Value, "final smooth frame is exact");
                AssertEqual(
                    synchronizationCount + 1L,
                    host.ThemedScrollBarSynchronizationCountForTest,
                    "the final smooth frame performs one scrollbar synchronization");
                AssertEqual(
                    nativeHideAttempts,
                    host.ThemedNativeHideAttemptCountForTest,
                    "smooth frames never round-trip native chrome");
                AssertEqual(
                    1,
                    valueChangedCount,
                    "the final smooth frame publishes one ValueChanged event");
                AssertTrue(
                    !host.ActiveNativeScrollStyleVisibleForTest,
                    "native chrome stays hidden after repeated smooth frames");

                bar.ValueChanged -= valueChanged;
            }
        }

        private static void TestDirectAndLightweightSynchronization()
        {
            const string directMarkup =
                "<ItemsControl Name='Rows' Width='240' Height='96' " +
                "Virtualizing='true' VirtualizationThreshold='1' " +
                "FixedItemSize='24' OverscanItems='2' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.VerticalScrollStyle>" +
                "    <ScrollBarStyle Thickness='15' />" +
                "  </ItemsControl.VerticalScrollStyle>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='24' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime directRuntime = XamlRuntime.Load(directMarkup);

            try
            {
                XamlRuntime.ItemsControl host =
                    directRuntime.GetItemsControl("Rows");
                host.CreateControl();
                host.SetItems(CreateRows(300));
                AssertTrue(
                    host.DirectVirtualActive,
                    "direct Controls virtualization is active");

                host.ContentRightToLeft = true;
                host.KeepScrollBarOnRight = false;
                host.PerformLayout();
                AssertEqual(
                    0,
                    host.ThemedScrollBarForTest.Left,
                    "direct RTL places the framework bar on the left");
                AssertRenderedRowsAvoidBar(
                    host,
                    host.ItemsViewportRectangleForTest);
                AssertThemedVirtualScroll(host, "direct");
            }
            finally
            {
                directRuntime.Dispose();
            }

            const string lightweightMarkup =
                "<ItemsControl Name='Rows' Width='240' Height='96' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='24' ProgressiveRendering='false'>" +
                "  <ItemsControl.VerticalScrollStyle>" +
                "    <ScrollBarStyle Thickness='14' />" +
                "  </ItemsControl.VerticalScrollStyle>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime lightweightRuntime =
                XamlRuntime.Load(lightweightMarkup);

            try
            {
                XamlRuntime.ItemsControl host =
                    lightweightRuntime.GetItemsControl("Rows");
                host.CreateControl();
                host.SetItems(CreateRows(300));
                AssertTrue(
                    host.LightweightActive,
                    "Lightweight virtualization is active");
                AssertThemedVirtualScroll(host, "lightweight");
            }
            finally
            {
                lightweightRuntime.Dispose();
            }
        }

        private static void
            TestVariableVirtualThumbStaysStableDuringBurst()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='420' Height='143' " +
                "AutoScroll='true' Virtualizing='true' " +
                "VirtualizationThreshold='1' EstimatedItemSize='96' " +
                "OverscanItems='1' VirtualizationCacheItems='8' " +
                "ItemKeyPath='Id' Spacing='8' " +
                "ProgressiveRendering='false' SmoothScroll='true' " +
                "ScrollBarGap='2'>" +
                "  <ItemsControl.VerticalScrollStyle>" +
                "    <ScrollBarStyle Thickness='16' " +
                "        TrackColor='#EEEEEE' ThumbColor='#999999' />" +
                "  </ItemsControl.VerticalScrollStyle>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Border Height='{Binding Height}' Padding='6' " +
                "            BorderBrush='#D1D5DB' BorderThickness='1'>" +
                "      <StackPanel Spacing='2'>" +
                "        <Label Text='{Binding Title}' />" +
                "        <Label Text='{Binding Detail}' />" +
                "        <HyperlinkLabel Text='Open' " +
                "            NavigateUri='{Binding Url}' />" +
                "      </StackPanel>" +
                "    </Border>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ScrollBarControl bar;
                int previousValue = -1;
                int previousThumbStart = -1;
                int i;

                host.CreateControl();
                host.SetItems(CreateVariableStyledRows(40));
                host.PerformLayout();
                bar = host.ThemedScrollBarForTest;

                AssertTrue(
                    host.DirectVirtualActive,
                    "variable styled fixture activates direct virtualization");

                for (i = 0; i < 36; i++)
                {
                    bool forward = i < 18;
                    host.ProcessMouseWheelDelta(
                        forward ? -120 : 120);
                    host.ApplySmoothScrollFrameForTest(
                        host.SmoothScrollDuration);

                    ScrollBarGeometry geometry =
                        bar.GetScrollBarGeometryForTest();

                    AssertEqual(
                        host.GetMaximumLogicalScrollOffsetForTest,
                        bar.EffectiveMaximumForTest,
                        "virtual input publishes the current framework range directly");
                    AssertEqual(
                        Math.Max(
                            host.Font.Height,
                            host.ItemsViewportRectangleForTest.Height),
                        bar.LargeChange,
                        "virtual input publishes the current framework viewport directly");
                    AssertEqual(
                        host.GetLogicalScrollOffset(),
                        bar.Value,
                        "virtual input publishes content and thumb as one frame");
                    AssertTrue(
                        forward
                            ? bar.Value >= previousValue
                            : bar.Value <= previousValue,
                        "the published value follows rapid direction changes monotonically");
                    AssertTrue(
                        forward
                            ? geometry.ThumbStart >= previousThumbStart
                            : geometry.ThumbStart <= previousThumbStart,
                        "the painted thumb follows rapid direction changes monotonically");
                    AssertTrue(
                        !host.SecondaryNativeScrollStyleVisibleForTest,
                        "vertical virtual input never exposes native horizontal chrome");
                    AssertEqual(
                        0,
                        host.AutoScrollPosition.X,
                        "vertical virtual input never moves the horizontal origin");
                    AssertTrue(
                        !host.ActiveNativeScrollStyleVisibleForTest,
                        "virtual input never restores native vertical chrome");

                    previousValue = bar.Value;
                    previousThumbStart = geometry.ThumbStart;
                }

                AssertTrue(
                    host.RenderedItems.Count < 40,
                    "the final correction does not realize the entire complex source");
                AssertEqual(
                    host.GetMaximumLogicalScrollOffsetForTest,
                    bar.EffectiveMaximumForTest,
                    "the final thumb retains the framework logical range");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestThemedRangeIgnoresNativeRangeState()
        {
            using (ItemsControl host = CreateManualScrollHost(
                Orientation.Vertical,
                CreateStyle(16)))
            {
                int expected = Math.Max(
                    0,
                    host.AutoScrollMinSize.Height -
                    host.ClientSize.Height);

                // ScrollableControl may mutate these values while native
                // chrome is converging or being hidden. They are deliberately
                // made incompatible with the framework extent so this test
                // fails if the custom thumb ever reads them as its range.
                host.VerticalScroll.Maximum = 7000;
                host.VerticalScroll.LargeChange = 3;

                AssertEqual(
                    expected,
                    host.GetMaximumLogicalScrollOffsetForTest,
                    "styled range has one framework-owned extent authority");
                AssertEqual(
                    expected,
                    host.ThemedScrollBarForTest.EffectiveMaximumForTest,
                    "native range convergence cannot move the styled thumb denominator");
            }
        }

        private static void TestShownNonVirtualWheelKeepsSingleChrome()
        {
            const string markup =
                "<ItemsControl Name='Rows' Dock='Fill' " +
                "AutoScroll='true' Virtualizing='false' Spacing='8' " +
                "ProgressiveRendering='false' SmoothScroll='true' " +
                "ScrollBarGap='2'>" +
                "  <ItemsControl.VerticalScrollStyle>" +
                "    <ScrollBarStyle Thickness='16' " +
                "        TrackColor='#EEEEEE' ThumbColor='#999999' />" +
                "  </ItemsControl.VerticalScrollStyle>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Border Height='{Binding Height}' Padding='6' " +
                "            BorderBrush='#D1D5DB' BorderThickness='1'>" +
                "      <StackPanel Spacing='2'>" +
                "        <Label Text='{Binding Title}' />" +
                "        <Label Text='{Binding Detail}' />" +
                "        <HyperlinkLabel Text='Open' " +
                "            NavigateUri='{Binding Url}' />" +
                "      </StackPanel>" +
                "    </Border>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);
            Form form = new Form();

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                form.ClientSize = new Size(620, 320);
                form.RightToLeft = RightToLeft.Yes;
                form.Controls.Add(host);
                host.ContentRightToLeft = true;
                host.KeepScrollBarOnRight = true;
                host.SetItems(CreateVariableStyledRows(40));
                form.Show();
                Application.DoEvents();

                ScrollBarControl bar = host.ThemedScrollBarForTest;
                Rectangle bounds = bar.Bounds;
                ScrollBarGeometry fixedGeometry =
                    bar.GetScrollBarGeometryForTest();
                int clientWidth = host.ClientSize.Width;
                int locationChanges = 0;
                int heldMaximum = -1;
                int heldLargeChange = -1;
                int heldThumbLength = -1;
                int previousValue = bar.Value;
                int previousThumbStart =
                    bar.GetScrollBarGeometryForTest().ThumbStart;
                int i;

                bar.LocationChanged += delegate { locationChanges++; };

                AssertTrue(
                    bar.Parent == null,
                    "styled chrome has no managed content parent");
                AssertTrue(
                    bar.IsHandleCreated &&
                    GetParent(bar.Handle) == form.Handle &&
                    GetParent(bar.Handle) != host.Handle,
                    "styled chrome HWND is a viewport sibling, not scrollable content");

                Point nativeArrow = Center(fixedGeometry.LastButton);
                int packedArrowPoint =
                    (nativeArrow.X & 0xffff) |
                    ((nativeArrow.Y & 0xffff) << 16);
                int beforeNativeArrow = host.GetLogicalScrollOffset();
                SendMessage(
                    bar.Handle,
                    WindowLeftButtonDown,
                    new IntPtr(1),
                    new IntPtr(packedArrowPoint));
                SendMessage(
                    bar.Handle,
                    WindowLeftButtonUp,
                    IntPtr.Zero,
                    new IntPtr(packedArrowPoint));
                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration);
                AssertTrue(
                    host.GetLogicalScrollOffset() > beforeNativeArrow,
                    "the detached native arrow still routes input to ItemsControl");
                host.SetLogicalScrollOffset(0);

                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);
                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "nonvirtual styled fixture starts a smooth frame");
                int nativeStyle = GetWindowLong(
                    host.Handle,
                    WindowLongStyle);
                SetWindowLong(
                    host.Handle,
                    WindowLongStyle,
                    nativeStyle | WindowStyleVerticalScroll);
                bool nativeRestoreWasAccepted =
                    host.ActiveNativeScrollStyleVisibleForTest;

                if (!nativeRestoreWasAccepted)
                {
                    AssertTrue(
                        (GetWindowLong(host.Handle, WindowLongStyle) &
                            WindowStyleVerticalScroll) == 0,
                        "styled host rejects native scroll chrome at the window-style boundary");
                }

                host.ApplySmoothScrollFrameForTest(60);
                AssertTrue(
                    !host.ActiveNativeScrollStyleVisibleForTest,
                    nativeRestoreWasAccepted
                        ? "nonvirtual styled frame removes restored native chrome before paint"
                        : "nonvirtual styled frame preserves the rejected native chrome state");
                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration);
                previousValue = bar.Value;
                previousThumbStart =
                    bar.GetScrollBarGeometryForTest().ThumbStart;

                for (i = 0; i < 32; i++)
                {
                    InvokeHostMouseWheel(
                        host,
                        i < 16 ? -120 : 120);
                    host.ApplySmoothScrollFrameForTest(60);
                    host.ApplySmoothScrollFrameForTest(
                        host.SmoothScrollDuration);
                    Application.DoEvents();

                    ScrollBarGeometry geometry =
                        bar.GetScrollBarGeometryForTest();

                    if (heldMaximum < 0)
                    {
                        heldMaximum = bar.EffectiveMaximumForTest;
                        heldLargeChange = bar.LargeChange;
                        heldThumbLength = geometry.ThumbLength;
                    }

                    AssertTrue(
                        Object.ReferenceEquals(
                            bar,
                            host.ThemedScrollBarForTest),
                        "nonvirtual wheel keeps one framework scrollbar instance");
                    AssertEqual(
                        bounds,
                        bar.Bounds,
                        "nonvirtual wheel keeps viewport chrome anchored");
                    AssertEqual(
                        clientWidth,
                        host.ClientSize.Width,
                        "nonvirtual wheel does not oscillate client width");
                    AssertTrue(
                        !host.SecondaryNativeScrollStyleVisibleForTest &&
                        host.AutoScrollPosition.X == 0,
                        "nonvirtual vertical wheel never exposes native horizontal chrome");
                    AssertTrue(
                        !host.ActiveNativeScrollStyleVisibleForTest,
                        "nonvirtual wheel never replaces custom vertical chrome");
                    AssertEqual(
                        heldMaximum,
                        bar.EffectiveMaximumForTest,
                        "nonvirtual wheel keeps one range denominator during the burst");
                    AssertEqual(
                        heldLargeChange,
                        bar.LargeChange,
                        "nonvirtual wheel keeps one viewport page during the burst");
                    AssertEqual(
                        heldThumbLength,
                        geometry.ThumbLength,
                        "nonvirtual wheel keeps a stable painted thumb size");
                    AssertEqual(
                        fixedGeometry.FirstButton,
                        geometry.FirstButton,
                        "the leading arrow is immutable viewport chrome");
                    AssertEqual(
                        fixedGeometry.LastButton,
                        geometry.LastButton,
                        "the trailing arrow is immutable viewport chrome");
                    AssertEqual(
                        fixedGeometry.Track,
                        geometry.Track,
                        "track endpoints never participate in scrolling");
                    AssertTrue(
                        i < 16
                            ? bar.Value >= previousValue
                            : bar.Value <= previousValue,
                        "nonvirtual thumb value follows the requested direction");
                    AssertTrue(
                        i < 16
                            ? geometry.ThumbStart >= previousThumbStart
                            : geometry.ThumbStart <= previousThumbStart,
                        "nonvirtual painted thumb never jumps against the requested direction");

                    previousValue = bar.Value;
                    previousThumbStart = geometry.ThumbStart;
                }

                AssertEqual(
                    0,
                    locationChanges,
                    "content-origin changes never translate the hosted scrollbar");
            }
            finally
            {
                form.Dispose();
                runtime.Dispose();
            }
        }

        private static void TestComplexFastSmoothFramesStayPure()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='360' Height='118' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "ItemKeyPath='Id' Spacing='1' ScrollBarGap='9'>" +
                "  <ItemsControl.VerticalScrollStyle>" +
                "    <ScrollBarStyle Thickness='17' " +
                "        TrackColor='#202124' ThumbColor='#80868B' />" +
                "  </ItemsControl.VerticalScrollStyle>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Height='42' Orientation='Horizontal' " +
                "                Padding='2' Spacing='4'>" +
                "      <Label Width='116' Text='{Binding Title}' />" +
                "      <Panel Width='180' Height='34'>" +
                "        <Label Width='112' Text='{Binding Detail}' />" +
                "        <CheckBox Left='116' Width='58' Text='On' " +
                "                  Checked='{Binding Checked}' />" +
                "      </Panel>" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateComplexRows(192);
                int i;

                host.CreateControl();
                host.SetItems(rows);
                host.PerformLayout();
                host.SmoothScroll = true;

                ScrollBarControl bar = host.ThemedScrollBarForTest;
                Rectangle barBounds = bar.Bounds;
                Control[] controls = CaptureRenderedControls(host);
                int[] reads = CaptureComplexReads(rows);
                long blueprints = host.ItemTemplateBlueprintBuildCount;
                long fallbackBuilds =
                    host.ItemTemplateFallbackBuildCount;
                long disposals = host.ItemControlTreeDisposedCount;
                long hideAttempts =
                    host.ThemedNativeHideAttemptCountForTest;
                int hostLayouts = 0;

                host.Layout += delegate { hostLayouts++; };
                host.ResetItemsLayoutScanDiagnosticsForTest();

                for (i = 0; i < 24; i++)
                {
                    bar.ExecuteScrollCommand(
                        (i & 1) == 0
                            ? ScrollEventType.LargeIncrement
                            : ScrollEventType.LargeDecrement);

                    AssertPureSmoothFrame(
                        host,
                        bar,
                        barBounds,
                        hideAttempts,
                        30);
                    AssertPureSmoothFrame(
                        host,
                        bar,
                        barBounds,
                        hideAttempts,
                        60);
                    AssertPureSmoothFrame(
                        host,
                        bar,
                        barBounds,
                        hideAttempts,
                        90);
                    AssertPureSmoothFrame(
                        host,
                        bar,
                        barBounds,
                        hideAttempts,
                        120);
                }

                AssertEqual(
                    0,
                    hostLayouts,
                    "complex smooth frames trigger no parent layout pass");
                AssertEqual(
                    0L,
                    host.ItemsMeasureRecordProbeCountForTest,
                    "complex smooth frames perform no row measurement scan");
                AssertEqual(
                    0L,
                    host.ItemsVisibilityFallbackProbeCountForTest,
                    "complex smooth frames perform no visibility fallback scan");
                AssertEqual(
                    blueprints,
                    host.ItemTemplateBlueprintBuildCount,
                    "complex smooth frames rebuild no blueprint");
                AssertEqual(
                    fallbackBuilds,
                    host.ItemTemplateFallbackBuildCount,
                    "complex smooth frames run no fallback build");
                AssertEqual(
                    disposals,
                    host.ItemControlTreeDisposedCount,
                    "complex smooth frames dispose no item tree");
                AssertControlIdentity(
                    controls,
                    CaptureRenderedControls(host),
                    "complex smooth frame");
                AssertComplexReadsUnchanged(rows, reads);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void AssertPureSmoothFrame(
            XamlRuntime.ItemsControl host,
            ScrollBarControl bar,
            Rectangle barBounds,
            long hideAttempts,
            int elapsedMilliseconds)
        {
            host.ApplySmoothScrollFrameForTest(elapsedMilliseconds);
            AssertEqual(
                host.GetLogicalScrollOffset(),
                bar.Value,
                "complex smooth frame keeps thumb and content together");
            AssertEqual(
                barBounds,
                bar.Bounds,
                "complex smooth frame keeps the overlay strip anchored");
            AssertTrue(
                !host.ActiveNativeScrollStyleVisibleForTest,
                "complex smooth frame never exposes native chrome");
            AssertEqual(
                hideAttempts,
                host.ThemedNativeHideAttemptCountForTest,
                "complex smooth frame performs no native hide call");
        }

        private static void AssertThemedVirtualScroll(
            XamlRuntime.ItemsControl host,
            string mode)
        {
            host.SmoothScroll = false;
            host.PerformLayout();
            ScrollBarControl bar = host.ThemedScrollBarForTest;
            AssertTrue(
                bar != null && bar.Visible,
                mode + " mode exposes the framework bar");
            AssertInfrastructureTailOrder(host, bar);

            bar.ExecuteScrollCommand(
                ScrollEventType.LargeIncrement);
            AssertTrue(
                host.GetLogicalScrollOffset() > 0,
                mode + " page command moves the logical viewport");
            AssertEqual(
                host.GetLogicalScrollOffset(),
                bar.Value,
                mode + " bar follows the settled viewport");

            host.SetLogicalScrollOffset(Int32.MaxValue);
            AssertEqual(
                host.GetLogicalScrollOffset(),
                bar.Value,
                mode + " direct position clamps and synchronizes");
            AssertTrue(
                !host.ActiveNativeScrollStyleVisibleForTest,
                mode + " keeps native chrome hidden");

            host.SetLogicalScrollOffset(0);
            host.SmoothScroll = true;
            long hideAttempts =
                host.ThemedNativeHideAttemptCountForTest;
            bar.ExecuteScrollCommand(
                ScrollEventType.LargeIncrement);
            int firstTarget =
                host.SmoothScrollTargetOffsetForTest;
            host.ApplySmoothScrollFrameForTest(30);
            int firstFrame = host.GetLogicalScrollOffset();
            AssertTrue(
                firstFrame > 0 && firstFrame < firstTarget,
                mode + " styled virtual scroll publishes an intermediate frame");
            AssertEqual(
                firstFrame,
                bar.Value,
                mode + " styled virtual thumb follows the intermediate frame");

            bar.ExecuteScrollCommand(
                ScrollEventType.LargeIncrement);
            int retarget = host.SmoothScrollTargetOffsetForTest;
            AssertTrue(
                retarget > firstTarget,
                mode + " repeated styled input retargets the active transition");
            host.ApplySmoothScrollFrameForTest(30);
            AssertTrue(
                host.GetLogicalScrollOffset() > firstFrame,
                mode + " retargeted styled virtual frame remains monotonic");
            AssertEqual(
                host.GetLogicalScrollOffset(),
                bar.Value,
                mode + " retargeted styled virtual thumb remains synchronized");
            AssertEqual(
                host.GetLogicalScrollOffset(),
                bar.Value,
                mode + " styled virtual retarget stays synchronized after chrome reconciliation");
            AssertTrue(
                host.ThemedNativeHideAttemptCountForTest >= hideAttempts,
                mode + " styled virtual chrome reconciliation is monotonic");
            AssertTrue(
                !host.ActiveNativeScrollStyleVisibleForTest,
                mode + " styled virtual retarget never exposes native chrome");

            host.ApplySmoothScrollFrameForTest(
                host.SmoothScrollDuration);
        }

        private static void TestResizeRangeAndExternalPosition()
        {
            using (ItemsControl host = CreateManualScrollHost(
                Orientation.Vertical,
                CreateStyle(17)))
            {
                ScrollBarControl bar = host.ThemedScrollBarForTest;
                AssertEqual(
                    host.GetMaximumLogicalScrollOffsetForTest,
                    bar.EffectiveMaximumForTest,
                    "bar maximum mirrors the framework logical range");
                AssertEqual(
                    Math.Max(
                        host.Font.Height,
                        host.ItemsViewportRectangleForTest.Height),
                    bar.LargeChange,
                    "bar page size comes from the framework viewport");

                host.SetLogicalScrollOffset(Int32.MaxValue);
                AssertEqual(
                    bar.EffectiveMaximumForTest,
                    host.GetLogicalScrollOffset(),
                    "end clamping uses the managed effective maximum");

                host.AutoScrollPosition = new Point(0, 211);
                Application.DoEvents();
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    bar.Value,
                    "external AutoScrollPosition synchronizes the bar");
                AssertEqual(
                    0,
                    bar.Top,
                    "external scrolling keeps the bar fixed in the viewport");
                AssertTrue(
                    !host.ActiveNativeScrollStyleVisibleForTest,
                    "external AutoScrollPosition reconciliation re-hides native chrome");

                host.Height = 170;
                host.PerformLayout();
                AssertEqual(
                    Math.Max(
                        host.Font.Height,
                        host.ItemsViewportRectangleForTest.Height),
                    bar.LargeChange,
                    "resize recomputes the page size");

                host.Height = 1400;
                host.PerformLayout();
                AssertTrue(
                    !bar.Visible,
                    "bar hides when the viewport contains the extent");
                AssertEqual(
                    host.ClientSize.Width -
                    host.Padding.Left -
                    host.Padding.Right,
                    host.ItemsViewportRectangleForTest.Width,
                    "a hidden bar does not reserve a strip");

                host.Height = 80;
                host.PerformLayout();
                AssertTrue(
                    bar.Visible,
                    "bar returns when the viewport shrinks");
            }
        }

        private static void TestPaddingExactFitUsesManagedVisibility()
        {
            using (ItemsControl host = new ItemsControl())
            {
                host.Size = new Size(180, 90);
                host.Padding = new Padding(9);
                host.VerticalScrollStyle = CreateStyle(16);
                host.AutoScrollMinSize = new Size(
                    1,
                    host.ClientSize.Height);
                host.CreateControl();
                host.PerformLayout();

                ScrollBarControl bar = host.ThemedScrollBarForTest;
                AssertTrue(
                    !host.VerticalScroll.Visible && !bar.Visible,
                    "padding does not invent scrolling at native exact fit");
                AssertEqual(
                    host.ClientSize.Width -
                        host.Padding.Left -
                        host.Padding.Right,
                    host.ItemsViewportRectangleForTest.Width,
                    "an exact-fit hidden bar reserves no strip");

                host.AutoScrollMinSize = new Size(
                    1,
                    host.ClientSize.Height + 1);
                host.PerformLayout();
                AssertTrue(
                    host.VerticalScroll.Visible && bar.Visible,
                    "one pixel beyond managed exact fit shows both range and bar");
            }
        }

        private static void
            TestScrollBarGapSupportsNativeAndStyledChrome()
        {
            using (ItemsControl native = new ItemsControl())
            {
                native.Size = new Size(180, 90);
                native.Padding = new Padding(3);
                native.AutoScrollMinSize = new Size(1, 1200);
                native.CreateControl();
                native.PerformLayout();

                Rectangle baseline =
                    native.ItemsViewportRectangleForTest;
                native.ScrollBarGap = 9;
                Rectangle gapped =
                    native.ItemsViewportRectangleForTest;

                AssertTrue(
                    native.VerticalScroll.Visible &&
                    native.ActiveNativeScrollStyleVisibleForTest &&
                    native.ThemedScrollBarForTest == null,
                    "native gap retains only native vertical chrome");
                AssertEqual(
                    Math.Max(0, baseline.Width - 9),
                    gapped.Width,
                    "native vertical gap reserves host viewport space");
                AssertEqual(
                    baseline.Left,
                    gapped.Left,
                    "right-side native gap does not shift the viewport origin");

                bool rejected = false;

                try
                {
                    native.ScrollBarGap = -1;
                }
                catch (ArgumentOutOfRangeException)
                {
                    rejected = true;
                }

                AssertTrue(rejected, "negative native scrollbar gap is rejected");
                AssertEqual(
                    9,
                    native.ScrollBarGap,
                    "invalid native gap preserves the previous value");
            }

            using (ItemsControl horizontal = new ItemsControl())
            {
                horizontal.Orientation = Orientation.Horizontal;
                horizontal.Size = new Size(180, 90);
                horizontal.Padding = new Padding(3);
                horizontal.AutoScrollMinSize = new Size(1200, 1);
                horizontal.ScrollBarGap = 7;
                horizontal.CreateControl();
                horizontal.PerformLayout();

                Rectangle withoutGap = horizontal.ClientRectangle;
                withoutGap.X += horizontal.Padding.Left;
                withoutGap.Y += horizontal.Padding.Top;
                withoutGap.Width = Math.Max(
                    0,
                    withoutGap.Width -
                    horizontal.Padding.Left -
                    horizontal.Padding.Right);
                withoutGap.Height = Math.Max(
                    0,
                    withoutGap.Height -
                    horizontal.Padding.Top -
                    horizontal.Padding.Bottom);

                AssertTrue(
                    horizontal.HorizontalScroll.Visible &&
                    horizontal.ActiveNativeScrollStyleVisibleForTest,
                    "native horizontal gap retains native chrome");
                AssertEqual(
                    Math.Max(0, withoutGap.Height - 7),
                    horizontal.ItemsViewportRectangleForTest.Height,
                    "native horizontal gap reserves host viewport space");
            }

            using (ItemsControl styled = CreateManualScrollHost(
                Orientation.Vertical,
                CreateStyle(16)))
            {
                Rectangle baseline =
                    styled.ItemsViewportRectangleForTest;
                styled.ScrollBarGap = 5;

                AssertEqual(
                    Math.Max(0, baseline.Width - 5),
                    styled.ItemsViewportRectangleForTest.Width,
                    "the same host property reserves styled scrollbar space");
            }
        }

        private static void
            TestRightToLeftEdgesAndOrientationSelection()
        {
            using (ItemsControl vertical = CreateManualScrollHost(
                Orientation.Vertical,
                CreateStyle(19)))
            {
                vertical.ScrollBarGap = 7;
                ScrollBarControl bar = vertical.ThemedScrollBarForTest;
                AssertEqual(
                    vertical.ClientSize.Width - bar.Width,
                    bar.Left,
                    "KeepScrollBarOnRight defaults to the right edge");

                vertical.ContentRightToLeft = true;
                vertical.KeepScrollBarOnRight = false;
                vertical.PerformLayout();
                AssertEqual(0, bar.Left, "RTL can place the bar on the left");
                AssertEqual(
                    bar.Width +
                        vertical.ScrollBarGap +
                        vertical.Padding.Left,
                    vertical.ItemsViewportRectangleForTest.X,
                    "left scrollbar strip shifts the item viewport");

                vertical.KeepScrollBarOnRight = true;
                vertical.PerformLayout();
                AssertEqual(
                    vertical.ClientSize.Width - bar.Width,
                    bar.Left,
                    "KeepScrollBarOnRight overrides RTL placement");
            }

            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "ProgressiveRendering='false' Virtualizing='false' " +
                "SmoothScroll='false'>" +
                "  <ItemsControl.VerticalScrollStyle>" +
                "    <ScrollBarStyle Thickness='13' />" +
                "  </ItemsControl.VerticalScrollStyle>" +
                "  <ItemsControl.HorizontalScrollStyle>" +
                "    <ScrollBarStyle Thickness='15' />" +
                "  </ItemsControl.HorizontalScrollStyle>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='80' Height='24' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.CreateControl();
                host.SetItems(CreateRows(40));
                ScrollBarControl verticalBar =
                    host.ThemedScrollBarForTest;
                AssertTrue(
                    verticalBar is VerticalScrollBar &&
                    host.VerticalScroll.Visible,
                    "vertical orientation chooses only the vertical style");

                host.Orientation = Orientation.Horizontal;
                host.ContentRightToLeft = true;
                host.RightToLeft = RightToLeft.Yes;
                host.PerformLayout();
                ScrollBarControl horizontalBar =
                    host.ThemedScrollBarForTest;
                AssertTrue(
                    horizontalBar is HorizontalScrollBar &&
                    host.HorizontalScroll.Visible &&
                    !Object.ReferenceEquals(verticalBar, horizontalBar),
                    "rendered orientation transition publishes the new axis before activation");
                AssertTrue(
                    verticalBar.IsDisposed,
                    "inactive-axis infrastructure is disposed");
                AssertEqual(
                    RightToLeft.Yes,
                    horizontalBar.RightToLeft,
                    "horizontal framework bar receives logical RTL");
                AssertEqual(
                    0,
                    CountFrameworkScrollBars(host),
                    "the framework axis is detached from scrollable content");

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    host.SetLogicalScrollOffset(0);
                    int beforeWheel = host.GetLogicalScrollOffset();
                    InvokeMouseWheel(horizontalBar, -120);
                    AssertTrue(
                        host.GetLogicalScrollOffset() > beforeWheel,
                        "RTL horizontal themed wheel preserves logical forward direction");
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestSharedStyleReplacementAndDisposal()
        {
            ScrollBarStyle shared = CreateStyle(17);
            ItemsControl first = CreateManualScrollHost(
                Orientation.Vertical,
                shared);
            ItemsControl second = CreateManualScrollHost(
                Orientation.Vertical,
                shared);

            try
            {
                shared.Thickness = 23;
                AssertEqual(
                    23,
                    first.ThemedScrollBarForTest.Width,
                    "shared style updates the first host geometry");
                AssertEqual(
                    23,
                    second.ThemedScrollBarForTest.Width,
                    "shared style updates the second host geometry");

                ScrollBarStyle replacement = CreateStyle(19);
                first.VerticalScrollStyle = replacement;
                ScrollBarControl replacementBar =
                    first.ThemedScrollBarForTest;
                shared.Thickness = 25;
                AssertEqual(
                    19,
                    replacementBar.Width,
                    "replacement detaches the old style from one host");
                AssertEqual(
                    25,
                    second.ThemedScrollBarForTest.Width,
                    "the shared style remains attached to the other host");

                first.Dispose();
                shared.Thickness = 27;
                AssertEqual(
                    27,
                    second.ThemedScrollBarForTest.Width,
                    "disposed hosts leave no stale shared-style callback");

                second.VerticalScrollStyle = null;
                second.PerformLayout();
                AssertTrue(
                    second.ThemedScrollBarForTest == null,
                    "assigning null restores native infrastructure");
                AssertTrue(
                    second.ActiveNativeScrollStyleVisibleForTest,
                    "assigning null restores native WS_VSCROLL");
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }

            ScrollBarStyle paintStyle = CreateStyle(17);

            using (CountingItemsControl paintHost =
                CreateCountingManualScrollHost(paintStyle))
            {
                int layoutCount = paintHost.LayoutPassCount;
                paintStyle.TrackColor = Color.Navy;
                paintStyle.MinimumThumbLength =
                    paintStyle.MinimumThumbLength + 1;
                AssertEqual(
                    layoutCount,
                    paintHost.LayoutPassCount,
                    "paint-only shared style changes do not relayout item rows");

                paintStyle.Thickness = 21;
                AssertTrue(
                    paintHost.LayoutPassCount > layoutCount,
                    "thickness changes still relayout the reserved strip");
                AssertEqual(
                    21,
                    paintHost.ThemedScrollBarForTest.Width,
                    "thickness relayout publishes the new geometry");
            }
        }

        private static void TestSecondaryAxisInvariant()
        {
            ItemsControl host = new ItemsControl();
            bool rejected = false;

            try
            {
                host.Size = new Size(120, 80);
                host.AutoScrollMinSize = new Size(900, 900);
                host.CreateControl();
                host.PerformLayout();

                try
                {
                    host.VerticalScrollStyle = new ScrollBarStyle();
                }
                catch (InvalidOperationException ex)
                {
                    rejected =
                        ex.Message.IndexOf("secondary scrollbar") >= 0;
                }

                AssertTrue(
                    rejected,
                    "two-dimensional native ranges are rejected explicitly");
                AssertTrue(
                    host.VerticalScrollStyle == null,
                    "a rejected activation preserves the native default");
            }
            finally
            {
                host.Dispose();
            }
        }

        private static void TestZeroSizeSentinelAxisInvariant()
        {
            using (ItemsControl host = new ItemsControl())
            {
                host.Size = Size.Empty;
                host.AutoScrollMinSize = new Size(1, 1200);
                host.VerticalScrollStyle = CreateStyle(16);
                host.CreateControl();
                host.PerformLayout();

                AssertEqual(
                    0,
                    CountFrameworkScrollBars(host),
                    "a zero-size sentinel keeps chrome outside content");

                host.Size = new Size(180, 90);
                host.PerformLayout();
                Application.DoEvents();
                host.PerformLayout();

                AssertTrue(
                    !host.ActiveNativeScrollStyleVisibleForTest &&
                    !host.SecondaryNativeScrollStyleVisibleForTest,
                    "positive resize exposes no competing native chrome");
                AssertTrue(
                    host.ThemedScrollBarForTest is VerticalScrollBar &&
                    host.ThemedScrollBarForTest.Visible,
                    "positive resize exposes the one configured framework axis");
                AssertEqual(
                    0,
                    CountFrameworkScrollBars(host),
                    "zero-size convergence keeps chrome outside content");
            }
        }

        private static ItemsControl CreateManualScrollHost(
            Orientation orientation,
            ScrollBarStyle style)
        {
            ItemsControl host = new ItemsControl();
            host.Orientation = orientation;
            host.Size = new Size(180, 90);
            host.Padding = new Padding(2);

            if (orientation == Orientation.Vertical)
            {
                host.AutoScrollMinSize = new Size(1, 1200);
                host.VerticalScrollStyle = style;
            }
            else
            {
                host.AutoScrollMinSize = new Size(1200, 1);
                host.HorizontalScrollStyle = style;
            }

            host.CreateControl();
            host.PerformLayout();
            return host;
        }

        private static void AssertInactiveXmlStyle(
            string styleAttribute,
            string message)
        {
            string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "    Virtualizing='false' ProgressiveRendering='false' " +
                styleAttribute + ">" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='28' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.CreateControl();
                host.SetItems(CreateRows(20));
                host.PerformLayout();
                AssertNativeOnly(host, message);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void AssertInactiveXmlPropertyElementStyle()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "    Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.VerticalScrollStyle />" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='28' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.CreateControl();
                host.SetItems(CreateRows(20));
                host.PerformLayout();
                AssertNativeOnly(host, "empty XML property-element style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void AssertNativeOnly(
            XamlRuntime.ItemsControl host,
            string message)
        {
            AssertTrue(
                host.VerticalScrollStyle == null,
                message + " leaves the style unset");
            AssertTrue(
                host.ThemedScrollBarForTest == null,
                message + " creates no custom scrollbar");
            AssertTrue(
                host.VerticalScroll.Visible,
                message + " retains the native managed range");
            AssertTrue(
                host.ActiveNativeScrollStyleVisibleForTest,
                message + " retains native scrollbar chrome");
        }

        private static void AssertCustomOnly(
            XamlRuntime.ItemsControl host,
            string message)
        {
            AssertTrue(
                host.VerticalScrollStyle != null,
                message + " retains the effective style");
            AssertTrue(
                host.ThemedScrollBarForTest != null &&
                host.ThemedScrollBarForTest.Visible,
                message + " exposes one custom scrollbar");
            AssertTrue(
                !host.ActiveNativeScrollStyleVisibleForTest,
                message + " hides native scrollbar chrome");
            AssertEqual(
                0,
                CountFrameworkScrollBars(host),
                message + " keeps custom chrome outside scrollable content");
        }

        private static CountingItemsControl
            CreateCountingManualScrollHost(ScrollBarStyle style)
        {
            CountingItemsControl host = new CountingItemsControl();
            host.Orientation = Orientation.Vertical;
            host.Size = new Size(180, 90);
            host.Padding = new Padding(2);
            host.AutoScrollMinSize = new Size(1, 1200);
            host.VerticalScrollStyle = style;
            host.CreateControl();
            host.PerformLayout();
            return host;
        }

        private static ScrollBarStyle CreateStyle(int thickness)
        {
            ScrollBarStyle style = new ScrollBarStyle();
            style.Thickness = thickness;
            return style;
        }

        private static ArrayList CreateRows(int count)
        {
            ArrayList rows = new ArrayList();
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new Row(i));

            return rows;
        }

        private static ArrayList CreateComplexRows(int count)
        {
            ArrayList rows = new ArrayList();
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new ComplexRow(i));

            return rows;
        }

        private static ArrayList CreateVariableStyledRows(int count)
        {
            ArrayList rows = new ArrayList();
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new VariableStyledRow(i));

            return rows;
        }

        private static Control[] CaptureRenderedControls(
            XamlRuntime.ItemsControl host)
        {
            Control[] controls =
                new Control[host.RenderedItems.Count];
            int i;

            for (i = 0; i < controls.Length; i++)
                controls[i] = GetRenderedControl(host, i);

            return controls;
        }

        private static int[] CaptureComplexReads(ArrayList rows)
        {
            int[] reads = new int[rows.Count * 2];
            int i;

            for (i = 0; i < rows.Count; i++)
            {
                ComplexRow row = (ComplexRow)rows[i];
                reads[i * 2] = row.TitleReadCount;
                reads[i * 2 + 1] = row.DetailReadCount;
            }

            return reads;
        }

        private static void AssertComplexReadsUnchanged(
            ArrayList rows,
            int[] reads)
        {
            int i;

            for (i = 0; i < rows.Count; i++)
            {
                ComplexRow row = (ComplexRow)rows[i];
                AssertEqual(
                    reads[i * 2],
                    row.TitleReadCount,
                    "smooth frames do not reread title at " + i);
                AssertEqual(
                    reads[i * 2 + 1],
                    row.DetailReadCount,
                    "smooth frames do not reread detail at " + i);
            }
        }

        private static void AssertControlIdentity(
            Control[] expected,
            Control[] actual,
            string message)
        {
            AssertEqual(
                expected.Length,
                actual.Length,
                message + " control count");

            int i;

            for (i = 0; i < expected.Length; i++)
            {
                AssertTrue(
                    Object.ReferenceEquals(expected[i], actual[i]),
                    message + " preserves control " + i);
            }
        }

        private static void InvokeMouseWheel(
            ScrollBarControl bar,
            int delta)
        {
            MethodInfo method = typeof(ScrollBarControl).GetMethod(
                "OnMouseWheel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            HandledMouseEventArgs args = new HandledMouseEventArgs(
                MouseButtons.None,
                0,
                1,
                1,
                delta);
            method.Invoke(bar, new object[] { args });
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
            method.Invoke(host, new object[] { args });
        }

        private static void InvokeMouseButton(
            ScrollBarControl bar,
            string methodName,
            Point point)
        {
            MethodInfo method = typeof(ScrollBarControl).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(
                bar,
                new object[]
                {
                    new MouseEventArgs(
                        MouseButtons.Left,
                        1,
                        point.X,
                        point.Y,
                        0)
                });
        }

        private static Point Center(Rectangle bounds)
        {
            return new Point(
                bounds.Left + Math.Max(0, bounds.Width / 2),
                bounds.Top + Math.Max(0, bounds.Height / 2));
        }

        private static void InvokeBarInputValue(
            ScrollBarControl bar,
            int value,
            ScrollEventType type)
        {
            MethodInfo method = typeof(ScrollBarControl).GetMethod(
                "SetValueFromInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(
                bar,
                new object[] { (long)value, type });
        }

        private static void AssertInfrastructureTailOrder(
            XamlRuntime.ItemsControl host,
            ScrollBarControl bar)
        {
            int rows = host.RenderedItems == null
                ? 0
                : host.RenderedItems.Count;
            int i;

            for (i = 0; i < rows; i++)
            {
                object record = host.RenderedItems[i];
                FieldInfo field = record.GetType().GetField(
                    "Control",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                Control row = field.GetValue(record) as Control;
                AssertTrue(
                    row != null &&
                    row.Parent == host &&
                    host.Controls.Contains(row),
                    "rendered row remains content at index " + i);
            }

            AssertTrue(
                host.Controls.Count >= rows + 1,
                "the extent marker remains an infrastructure tail control");
            AssertTrue(
                bar.Parent == null &&
                CountFrameworkScrollBars(host) == 0,
                "framework chrome is not a scroll-translated child");
            AssertTrue(
                !Object.ReferenceEquals(
                    host.Controls[host.Controls.Count - 1],
                    bar),
                "only the extent marker remains in the content collection");
        }

        private static void AssertRenderedRowsAvoidBar(
            XamlRuntime.ItemsControl host,
            Rectangle viewport)
        {
            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                object record = host.RenderedItems[i];
                FieldInfo field = record.GetType().GetField(
                    "Control",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                Control row = field.GetValue(record) as Control;

                if (row == null || !row.Visible)
                    continue;

                AssertTrue(
                    row.Left >= viewport.Left &&
                    row.Right <= viewport.Right,
                    "row bounds stay outside the custom bar strip at " + i);
            }
        }

        private static Control GetRenderedControl(
            XamlRuntime.ItemsControl host,
            int index)
        {
            object record = host.RenderedItems[index];
            FieldInfo field = record.GetType().GetField(
                "Control",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            return field.GetValue(record) as Control;
        }

        private static int CountFrameworkScrollBars(
            Control host)
        {
            int count = 0;
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                if (host.Controls[i] is ScrollBarControl)
                    count++;
            }

            return count;
        }

        private static int CountApplicationContentControls(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo markerField =
                typeof(XamlRuntime.ItemsControl).GetField(
                    "_scrollExtentMarker",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
            Control marker = markerField == null
                ? null
                : markerField.GetValue(host) as Control;
            int count = 0;
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                Control child = host.Controls[i];

                if (!Object.ReferenceEquals(child, marker) &&
                    !(child is ScrollBarControl))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value)
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
                    message + ": expected " + expected +
                    ", actual " + actual + ".");
            }
        }
    }
}
