using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class ItemsControlSmoothScrollTests
    {
        private delegate void TestAction();

        private sealed class SmoothProbe : ItemsControl
        {
            public int ScrollCallbackCount;

            public ScrollEventArgs RaiseNativeScroll(
                ScrollEventType type,
                int oldValue,
                int newValue)
            {
                ScrollOrientation orientation =
                    Orientation == Orientation.Vertical
                        ? ScrollOrientation.VerticalScroll
                        : ScrollOrientation.HorizontalScroll;
                ScrollEventArgs args = new ScrollEventArgs(
                    type,
                    oldValue,
                    newValue,
                    orientation);
                OnScroll(args);
                return args;
            }

            public void DispatchOwnNativeScrollMessage(int command)
            {
                int messageId = Orientation == Orientation.Vertical
                    ? 0x0115
                    : 0x0114;
                Message message = Message.Create(
                    Handle,
                    messageId,
                    new IntPtr(command),
                    IntPtr.Zero);

                WndProc(ref message);
            }

            protected override void OnScroll(ScrollEventArgs e)
            {
                ScrollCallbackCount++;
                base.OnScroll(e);
            }

            public void DestroyNativeHandle()
            {
                DestroyHandle();
            }
        }

        private sealed class SmoothRow
        {
            public readonly string Id;
            public readonly string Detail;
            private readonly string _title;
            public int TitleReadCount;

            public SmoothRow(int index)
            {
                Id = "smooth-" + index;
                _title = "Smooth row " + index;
                Detail = "Nested detail " + index;
            }

            public string Title
            {
                get
                {
                    TitleReadCount++;
                    return _title;
                }
            }
        }

        internal static void RunAll()
        {
            TestDefaultsAndDurationValidation();
            TestLegacyNativeArrowEventsUseImmediateCommandPath();
            TestSmoothNativeMessagesAvoidTheImmediateMove();
            TestDeterministicFramesRetargetAndFinishExactly();
            TestHorizontalFramesUseTheConfiguredAxis();
            TestTickCountWrapAndLifecycle();
            TestNonVirtualFramesDoNotRebuildOrRebind();
            TestVirtualFramesPublishTheVisibleRange();
            TestComplexFourModeScrollMatrix();
        }

        private static void TestDefaultsAndDurationValidation()
        {
            using (ItemsControl host = new ItemsControl())
            {
                AssertTrue(
                    host.SmoothScroll,
                    "smooth scrolling is enabled by default");
                AssertEqual(
                    120,
                    host.SmoothScrollDuration,
                    "smooth scrolling defaults to 120 milliseconds");

                AssertThrowsArgumentOutOfRange(
                    delegate { host.SmoothScrollDuration = 0; },
                    "zero duration is rejected");
                AssertThrowsArgumentOutOfRange(
                    delegate { host.SmoothScrollDuration = -1; },
                    "negative duration is rejected");
                AssertEqual(
                    120,
                    host.SmoothScrollDuration,
                    "invalid duration leaves the previous value intact");

                host.SmoothScrollDuration = 75;
                AssertEqual(
                    75,
                    host.SmoothScrollDuration,
                    "positive duration is accepted");
            }
        }

        private static void
            TestLegacyNativeArrowEventsUseImmediateCommandPath()
        {
            using (SmoothProbe host = CreateScrollHost(
                Orientation.Vertical))
            {
                host.SmoothScroll = false;
                int raised = 0;
                host.Scroll += delegate { raised++; };

                host.RaiseNativeScroll(
                    ScrollEventType.SmallIncrement,
                    0,
                    1);
                int small = host.GetLogicalScrollOffset();
                AssertTrue(
                    small > 0,
                    "native SmallIncrement moves immediately when smooth is off");

                host.RaiseNativeScroll(
                    ScrollEventType.EndScroll,
                    small,
                    small);
                AssertEqual(
                    small,
                    host.GetLogicalScrollOffset(),
                    "native EndScroll does not overwrite the settled offset");

                host.RaiseNativeScroll(
                    ScrollEventType.SmallDecrement,
                    small,
                    Math.Max(0, small - 1));
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "native SmallDecrement follows the same clamped command path");

                host.RaiseNativeScroll(
                    ScrollEventType.LargeIncrement,
                    0,
                    1);
                AssertTrue(
                    host.GetLogicalScrollOffset() > small,
                    "native LargeIncrement applies an explicit page command");
                AssertEqual(
                    4,
                    raised,
                    "every native arrow/page/end event still raises Scroll once");
            }
        }

        private static void
            TestSmoothNativeMessagesAvoidTheImmediateMove()
        {
            using (SmoothProbe host = CreateScrollHost(
                Orientation.Vertical))
            {
                host.SmoothScroll = true;
                int callbacks = host.ScrollCallbackCount;
                int eventOld = -1;
                int eventNew = -1;
                host.Scroll += delegate(object sender, ScrollEventArgs e)
                {
                    eventOld = e.OldValue;
                    eventNew = e.NewValue;
                };

                host.DispatchOwnNativeScrollMessage(1);

                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "intercepted native line input does not move before its first frame");
                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest &&
                    host.SmoothScrollTargetOffsetForTest > 0,
                    "intercepted native line input starts one smooth transition");
                AssertEqual(
                    callbacks + 1,
                    host.ScrollCallbackCount,
                    "interception preserves the virtual OnScroll callback");
                AssertTrue(
                    eventNew > eventOld,
                    "Scroll subscribers receive the native proposed physical value");

                int lineTarget =
                    host.SmoothScrollTargetOffsetForTest;
                host.DispatchOwnNativeScrollMessage(3);

                AssertTrue(
                    host.SmoothScrollTargetOffsetForTest > lineTarget,
                    "native page input coalesces from the pending target");
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "coalesced native input still avoids a redundant immediate move");
            }

            using (SmoothProbe host = CreateScrollHost(
                Orientation.Horizontal))
            {
                host.ContentRightToLeft = true;
                host.PerformLayout();
                host.SetLogicalScrollOffset(0);
                host.SmoothScroll = true;

                // SB_LINELEFT is a physical decrement. In horizontal RTL it
                // advances the framework's nonnegative logical position.
                host.DispatchOwnNativeScrollMessage(0);

                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "RTL native line input also waits for its first frame");
                AssertTrue(
                    host.SmoothScrollTargetOffsetForTest > 0,
                    "RTL native command is translated to logical direction");
            }
        }

        private static void
            TestDeterministicFramesRetargetAndFinishExactly()
        {
            using (SmoothProbe host = CreateScrollHost(
                Orientation.Vertical))
            {
                host.SmoothScroll = true;
                host.SmoothScrollDuration = 120;

                AssertTrue(
                    host.ScrollBy(ScrollEventType.LargeIncrement),
                    "first page command schedules an animation");
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "scheduled animation does not jump synchronously");
                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "page command activates the shared timer state");

                int firstTarget =
                    host.SmoothScrollTargetOffsetForTest;
                object timer =
                    host.SmoothScrollTimerIdentityForTest;
                AssertTrue(
                    firstTarget > 0 && timer != null,
                    "page command publishes a positive target and timer");

                host.ScrollBy(ScrollEventType.SmallIncrement);
                int coalescedTarget =
                    host.SmoothScrollTargetOffsetForTest;
                AssertTrue(
                    coalescedTarget > firstTarget,
                    "repeated command accumulates from the pending target");
                AssertTrue(
                    Object.ReferenceEquals(
                        timer,
                        host.SmoothScrollTimerIdentityForTest),
                    "retargeting reuses one WinForms timer");

                AssertTrue(
                    host.ApplySmoothScrollFrameForTest(60),
                    "half-duration frame keeps the animation active");
                int halfway = host.GetLogicalScrollOffset();
                AssertTrue(
                    halfway > coalescedTarget / 2 &&
                    halfway <= coalescedTarget,
                    "cubic ease-out advances beyond linear halfway");

                AssertTrue(
                    !host.ApplySmoothScrollFrameForTest(120),
                    "duration frame completes the animation");
                AssertEqual(
                    coalescedTarget,
                    host.GetLogicalScrollOffset(),
                    "final frame publishes the exact target");

                host.ScrollBy(ScrollEventType.SmallIncrement);
                AssertTrue(
                    Object.ReferenceEquals(
                        timer,
                        host.SmoothScrollTimerIdentityForTest),
                    "a later transition reuses the stopped timer");

                host.SetLogicalScrollOffset(7);
                AssertEqual(
                    7,
                    host.GetLogicalScrollOffset(),
                    "the immediate primitive overrides a pending animation");
                AssertTrue(
                    !host.SmoothScrollAnimationActiveForTest,
                    "the immediate primitive cancels the pending target");

                host.ScrollBy(ScrollEventType.LargeIncrement);
                host.LiveScroll = false;
                host.RaiseNativeScroll(
                    ScrollEventType.ThumbTrack,
                    7,
                    240);
                AssertEqual(
                    7,
                    host.GetLogicalScrollOffset(),
                    "deferred native thumb tracking preserves the committed origin");
                AssertTrue(
                    !host.SmoothScrollAnimationActiveForTest,
                    "thumb tracking cancels interpolation");

                host.RaiseNativeScroll(
                    ScrollEventType.ThumbPosition,
                    7,
                    240);
                AssertEqual(
                    240,
                    host.GetLogicalScrollOffset(),
                    "native thumb release commits the deferred position");

                host.ScrollBy(ScrollEventType.LargeIncrement);
                host.RaiseNativeScroll(
                    ScrollEventType.ThumbPosition,
                    240,
                    320);
                AssertEqual(
                    320,
                    host.GetLogicalScrollOffset(),
                    "thumb release remains immediate");

                host.ScrollBy(ScrollEventType.Last);
                int last = host.GetLogicalScrollOffset();
                AssertTrue(
                    last > 320 &&
                    !host.SmoothScrollAnimationActiveForTest,
                    "Last remains an immediate exact command");
                host.ScrollBy(ScrollEventType.First);
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "First remains an immediate exact command");

                host.RaiseNativeScroll(
                    ScrollEventType.LargeIncrement,
                    0,
                    1);
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "native page command is pinned at its old position while animating");
                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "native page command enters smooth timer state");
                int nativeTarget =
                    host.SmoothScrollTargetOffsetForTest;
                host.ApplySmoothScrollFrameForTest(120);
                AssertEqual(
                    nativeTarget,
                    host.GetLogicalScrollOffset(),
                    "native page animation ends at its exact logical target");
            }
        }

        private static void TestHorizontalFramesUseTheConfiguredAxis()
        {
            using (SmoothProbe host = CreateScrollHost(
                Orientation.Horizontal))
            {
                host.SmoothScroll = true;
                host.ScrollBy(ScrollEventType.LargeIncrement);
                int target = host.SmoothScrollTargetOffsetForTest;
                host.ApplySmoothScrollFrameForTest(120);

                AssertEqual(
                    target,
                    host.GetLogicalScrollOffset(),
                    "horizontal animation reaches its logical target");
                AssertEqual(
                    0,
                    Math.Max(0, -host.AutoScrollPosition.Y),
                    "horizontal animation does not move the vertical axis");
            }
        }

        private static void TestTickCountWrapAndLifecycle()
        {
            AssertEqual(
                20,
                XamlRuntime.ItemsControl.
                    GetSmoothScrollElapsedMilliseconds(
                        Int32.MaxValue - 10,
                        Int32.MinValue + 9),
                "elapsed time remains monotonic across TickCount wrap");

            SmoothProbe host = CreateScrollHost(
                Orientation.Vertical);

            try
            {
                host.SmoothScroll = true;
                host.ScrollBy(ScrollEventType.LargeIncrement);
                object timer =
                    host.SmoothScrollTimerIdentityForTest;

                host.DestroyNativeHandle();
                AssertTrue(
                    !host.SmoothScrollAnimationActiveForTest,
                    "handle destruction stops the animation");
                AssertTrue(
                    Object.ReferenceEquals(
                        timer,
                        host.SmoothScrollTimerIdentityForTest),
                    "handle destruction retains the reusable timer");

                host.CreateControl();
                host.ScrollBy(ScrollEventType.LargeIncrement);
                AssertTrue(
                    Object.ReferenceEquals(
                        timer,
                        host.SmoothScrollTimerIdentityForTest),
                    "handle recreation reuses the same timer");
            }
            finally
            {
                host.Dispose();
            }

            AssertTrue(
                !host.SmoothScrollAnimationActiveForTest &&
                host.SmoothScrollTimerIdentityForTest == null,
                "disposal stops and releases the timer");
        }

        private static void
            TestNonVirtualFramesDoNotRebuildOrRebind()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='320' Height='96' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "ItemKeyPath='Id' Spacing='1'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Height='34' Orientation='Horizontal' " +
                "                Padding='2' Spacing='4'>" +
                "      <Label Width='100' Text='{Binding Title}' />" +
                "      <Panel Width='196' Height='26'>" +
                "        <Label Width='136' Text='{Binding Detail}' />" +
                "        <Button Left='140' Width='52' Height='22' " +
                "                Text='Open' />" +
                "      </Panel>" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(64);
                int i;

                host.CreateControl();
                host.SetItems(rows);
                host.SmoothScroll = true;

                Control[] controls = CaptureControls(host);
                int[] reads = CaptureTitleReads(rows);
                long blueprintBuilds =
                    host.ItemTemplateBlueprintBuildCount;
                long fallbackBuilds =
                    host.ItemTemplateFallbackBuildCount;
                long disposals =
                    host.ItemControlTreeDisposedCount;
                int completed = 0;
                host.RefreshCompleted += delegate { completed++; };

                for (i = 0; i < 48; i++)
                {
                    host.ScrollBy(ScrollEventType.LargeIncrement);
                    host.ScrollBy(ScrollEventType.SmallIncrement);
                    host.ApplySmoothScrollFrameForTest(30);
                    host.ApplySmoothScrollFrameForTest(60);
                    host.ApplySmoothScrollFrameForTest(120);

                    host.ScrollBy(ScrollEventType.SmallDecrement);
                    host.ApplySmoothScrollFrameForTest(60);
                    host.ApplySmoothScrollFrameForTest(120);
                }

                Control[] finalControls = CaptureControls(host);
                AssertEqual(
                    controls.Length,
                    finalControls.Length,
                    "smooth frames retain every nonvirtual record");

                for (i = 0; i < controls.Length; i++)
                {
                    AssertTrue(
                        Object.ReferenceEquals(
                            controls[i],
                            finalControls[i]),
                        "smooth frames retain Control identity at " + i);
                    AssertEqual(
                        reads[i],
                        ((SmoothRow)rows[i]).TitleReadCount,
                        "smooth frames do not reread Title at " + i);
                }

                AssertEqual(
                    blueprintBuilds,
                    host.ItemTemplateBlueprintBuildCount,
                    "smooth frames do not build blueprint item trees");
                AssertEqual(
                    fallbackBuilds,
                    host.ItemTemplateFallbackBuildCount,
                    "smooth frames do not build fallback item trees");
                AssertEqual(
                    disposals,
                    host.ItemControlTreeDisposedCount,
                    "smooth frames do not dispose item trees");
                AssertEqual(
                    0,
                    completed,
                    "smooth frames do not raise RefreshCompleted");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestVirtualFramesPublishTheVisibleRange()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='240' Height='100' " +
                "Virtualizing='true' VirtualizationThreshold='1' " +
                "FixedItemSize='24' OverscanItems='2' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='24' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.CreateControl();
                host.SetItems(CreateRows(200));
                host.SmoothScroll = true;

                int i;

                for (i = 0; i < 8; i++)
                    host.ScrollBy(ScrollEventType.LargeIncrement);

                int target = host.SmoothScrollTargetOffsetForTest;
                host.ApplySmoothScrollFrameForTest(60);
                AssertVirtualViewportCovered(
                    host,
                    "intermediate virtual smooth frame");
                host.ApplySmoothScrollFrameForTest(120);
                AssertEqual(
                    target,
                    host.GetLogicalScrollOffset(),
                    "virtual smooth animation reaches the exact target");
                AssertVirtualViewportCovered(
                    host,
                    "final virtual smooth frame");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestComplexFourModeScrollMatrix()
        {
            bool[] switches = new bool[] { false, true };
            int virtualIndex;
            int themedIndex;

            for (virtualIndex = 0;
                 virtualIndex < switches.Length;
                 virtualIndex++)
            {
                for (themedIndex = 0;
                     themedIndex < switches.Length;
                     themedIndex++)
                {
                    RunComplexScrollMatrixCase(
                        switches[virtualIndex],
                        switches[themedIndex]);
                }
            }
        }

        private static void RunComplexScrollMatrixCase(
            bool virtualizing,
            bool themed)
        {
            string mode =
                (virtualizing ? "virtual" : "nonvirtual") +
                "/" +
                (themed ? "framework" : "native");
            string style = themed
                ? "  <ItemsControl.VerticalScrollStyle>" +
                  "    <ScrollBarStyle Thickness='17' " +
                  "        TrackColor='#202124' ThumbColor='#80868B' />" +
                  "  </ItemsControl.VerticalScrollStyle>"
                : String.Empty;
            string markup =
                "<ItemsControl Name='Rows' Width='360' Height='118' " +
                "Virtualizing='" +
                (virtualizing ? "true" : "false") + "' " +
                "VirtualizationThreshold='1' FixedItemSize='42' " +
                "OverscanItems='3' ProgressiveRendering='false' " +
                "ItemKeyPath='Id' Spacing='1' ScrollBarGap='7'>" +
                style +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Height='42' Orientation='Horizontal' " +
                "                Padding='2' Spacing='4'>" +
                "      <Label Width='116' Text='{Binding Title}' />" +
                "      <Panel Width='180' Height='34'>" +
                "        <Label Width='112' Text='{Binding Detail}' />" +
                "        <CheckBox Left='116' Width='58' Text='On' />" +
                "      </Panel>" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(128);
                Control[] controls = null;
                int[] reads = null;
                int i;

                host.CreateControl();
                host.SetItems(rows);
                host.PerformLayout();
                host.SmoothScroll = true;

                ScrollBarControl bar = host.ThemedScrollBarForTest;
                AssertTrue(
                    themed == (bar != null && bar.Visible),
                    mode + " exposes only the selected scrollbar renderer");
                AssertTrue(
                    themed != host.ActiveNativeScrollStyleVisibleForTest,
                    mode + " never exposes both scrollbar renderers");
                AssertTrue(
                    !host.SecondaryNativeScrollStyleVisibleForTest,
                    mode + " starts without cross-axis native chrome");

                if (!virtualizing)
                {
                    controls = CaptureControls(host);
                    reads = CaptureTitleReads(rows);
                }
                else
                {
                    AssertVirtualViewportCovered(
                        host,
                        mode + " initial viewport");
                }

                Size initialExtent = host.AutoScrollMinSize;
                int initialMaximum =
                    host.GetMaximumLogicalScrollOffsetForTest;
                int initialNativeMaximum =
                    host.VerticalScroll.Maximum;
                int initialLargeChange =
                    host.VerticalScroll.LargeChange;
                Rectangle barBounds = bar == null
                    ? Rectangle.Empty
                    : bar.Bounds;
                long synchronizations =
                    host.ThemedScrollBarSynchronizationCountForTest;
                long secondaryHideAttempts =
                    host.SecondaryNativeHideAttemptCountForTest;
                int layoutPasses = 0;
                int barLayoutPasses = 0;
                int nullLayoutPasses = 0;
                int itemLayoutPasses = 0;

                host.Layout += delegate(object sender, LayoutEventArgs e)
                {
                    layoutPasses++;

                    if (e == null || e.AffectedControl == null)
                    {
                        nullLayoutPasses++;
                    }
                    else if (Object.ReferenceEquals(
                                 e.AffectedControl,
                                 bar))
                    {
                        barLayoutPasses++;
                    }
                    else
                    {
                        itemLayoutPasses++;
                    }
                };

                for (i = 0; i < 16; i++)
                {
                    int start = host.GetLogicalScrollOffset();
                    if (themed)
                    {
                        bar.ExecuteScrollCommand(
                            ScrollEventType.LargeIncrement);
                    }
                    else
                    {
                        AssertTrue(
                            host.ScrollBy(
                                ScrollEventType.LargeIncrement),
                            mode + " accepts rapid page input " + i);
                    }

                    AssertTrue(
                        host.SmoothScrollAnimationActiveForTest,
                        mode + " animates rapid page input " + i);
                    int firstTarget =
                        host.SmoothScrollTargetOffsetForTest;
                    AssertFourModeFrame(
                        host,
                        bar,
                        virtualizing,
                        mode,
                        start,
                        firstTarget,
                        30);

                    if (themed)
                    {
                        bar.ExecuteScrollCommand(
                            ScrollEventType.SmallIncrement);
                    }
                    else
                    {
                        AssertTrue(
                            host.ScrollBy(
                                ScrollEventType.SmallIncrement),
                            mode + " retargets active input " + i);
                    }
                    int target =
                        host.SmoothScrollTargetOffsetForTest;
                    AssertTrue(
                        target > firstTarget,
                        mode + " coalesces rapid input monotonically " + i);
                    AssertFourModeFrame(
                        host,
                        bar,
                        virtualizing,
                        mode,
                        host.GetLogicalScrollOffset(),
                        target,
                        60);
                    AssertFourModeFrame(
                        host,
                        bar,
                        virtualizing,
                        mode,
                        host.GetLogicalScrollOffset(),
                        target,
                        120);

                    start = host.GetLogicalScrollOffset();
                    if (themed)
                    {
                        bar.ExecuteScrollCommand(
                            ScrollEventType.SmallDecrement);
                    }
                    else
                    {
                        AssertTrue(
                            host.ScrollBy(
                                ScrollEventType.SmallDecrement),
                            mode + " accepts reverse input " + i);
                    }

                    AssertTrue(
                        host.SmoothScrollAnimationActiveForTest,
                        mode + " animates reverse input " + i);
                    target = host.SmoothScrollTargetOffsetForTest;
                    AssertFourModeFrame(
                        host,
                        bar,
                        virtualizing,
                        mode,
                        start,
                        target,
                        60);
                    AssertFourModeFrame(
                        host,
                        bar,
                        virtualizing,
                        mode,
                        host.GetLogicalScrollOffset(),
                        target,
                        120);
                }

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    int start = host.GetLogicalScrollOffset();
                    AssertTrue(
                        host.ProcessMouseWheelDelta(-120),
                        mode + " accepts focused wheel input");
                    int target =
                        host.SmoothScrollTargetOffsetForTest;
                    AssertFourModeFrame(
                        host,
                        bar,
                        virtualizing,
                        mode,
                        start,
                        target,
                        60);
                    AssertFourModeFrame(
                        host,
                        bar,
                        virtualizing,
                        mode,
                        host.GetLogicalScrollOffset(),
                        target,
                        120);
                }

                AssertEqual(
                    initialExtent,
                    host.AutoScrollMinSize,
                    mode + " keeps its content extent stable");
                AssertEqual(
                    initialMaximum,
                    host.GetMaximumLogicalScrollOffsetForTest,
                    mode + " keeps its logical range stable");
                AssertEqual(
                    initialNativeMaximum,
                    host.VerticalScroll.Maximum,
                    mode + " keeps its native range stable");
                AssertEqual(
                    initialLargeChange,
                    host.VerticalScroll.LargeChange,
                    mode + " keeps its native page size stable");
                AssertTrue(
                    !host.SecondaryNativeScrollStyleVisibleForTest,
                    mode + " never flashes cross-axis native chrome");
                AssertEqual(
                    secondaryHideAttempts,
                    host.SecondaryNativeHideAttemptCountForTest,
                    mode + " performs no redundant cross-axis user32 hide");

                if (themed)
                {
                    AssertEqual(
                        barBounds,
                        bar.Bounds,
                        mode + " keeps the framework strip anchored");
                    AssertTrue(
                        host.ThemedScrollBarSynchronizationCountForTest >
                            synchronizations,
                        mode + " publishes framework thumb frames");
                }
                else
                {
                    AssertEqual(
                        synchronizations,
                        host.ThemedScrollBarSynchronizationCountForTest,
                        mode + " skips framework synchronization entirely");
                }

                AssertEqual(
                    0,
                    layoutPasses,
                    mode + " smooth frames trigger no host layout pass " +
                    "[bar=" + barLayoutPasses +
                    ", item=" + itemLayoutPasses +
                    ", null=" + nullLayoutPasses + "]");

                if (!virtualizing)
                {
                    Control[] finalControls = CaptureControls(host);
                    AssertEqual(
                        controls.Length,
                        finalControls.Length,
                        mode + " retains every materialized row");

                    for (i = 0; i < controls.Length; i++)
                    {
                        AssertTrue(
                            Object.ReferenceEquals(
                                controls[i],
                                finalControls[i]),
                            mode + " retains row identity " + i);
                        AssertEqual(
                            reads[i],
                            ((SmoothRow)rows[i]).TitleReadCount,
                            mode + " does not rebind row " + i);
                    }
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void AssertFourModeFrame(
            XamlRuntime.ItemsControl host,
            ScrollBarControl bar,
            bool virtualizing,
            string mode,
            int previous,
            int target,
            int elapsedMilliseconds)
        {
            host.ApplySmoothScrollFrameForTest(elapsedMilliseconds);
            int current = host.GetLogicalScrollOffset();

            if (target >= previous)
            {
                AssertTrue(
                    current >= previous && current <= target,
                    mode + " forward frame is monotonic");
            }
            else
            {
                AssertTrue(
                    current <= previous && current >= target,
                    mode + " reverse frame is monotonic");
            }

            if (bar != null)
            {
                AssertEqual(
                    current,
                    bar.Value,
                    mode + " thumb and content publish one position");
                AssertTrue(
                    !host.ActiveNativeScrollStyleVisibleForTest,
                    mode + " never reveals native chrome during a frame");
            }

            AssertTrue(
                !host.SecondaryNativeScrollStyleVisibleForTest,
                mode + " never reveals cross-axis chrome during a frame");

            if (virtualizing)
            {
                AssertVirtualViewportCovered(
                    host,
                    mode + " frame at " + elapsedMilliseconds);
            }
        }

        private static SmoothProbe CreateScrollHost(
            Orientation orientation)
        {
            SmoothProbe host = new SmoothProbe();
            host.Orientation = orientation;
            host.Size = new Size(120, 80);
            host.AutoScrollMinSize = orientation == Orientation.Vertical
                ? new Size(120, 2000)
                : new Size(2000, 80);
            host.CreateControl();
            host.PerformLayout();
            return host;
        }

        private static ArrayList CreateRows(int count)
        {
            ArrayList rows = new ArrayList();
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new SmoothRow(i));

            return rows;
        }

        private static Control[] CaptureControls(
            XamlRuntime.ItemsControl host)
        {
            Control[] controls =
                new Control[host.RenderedItems.Count];
            int i;

            for (i = 0; i < controls.Length; i++)
            {
                object record = host.RenderedItems[i];
                FieldInfo field = record.GetType().GetField(
                    "Control",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                controls[i] = field == null
                    ? null
                    : field.GetValue(record) as Control;
                AssertTrue(
                    controls[i] != null &&
                    !controls[i].IsDisposed,
                    "rendered Control is alive at " + i);
            }

            return controls;
        }

        private static int[] CaptureTitleReads(ArrayList rows)
        {
            int[] reads = new int[rows.Count];
            int i;

            for (i = 0; i < rows.Count; i++)
                reads[i] = ((SmoothRow)rows[i]).TitleReadCount;

            return reads;
        }

        private static void AssertVirtualViewportCovered(
            XamlRuntime.ItemsControl host,
            string message)
        {
            VirtualViewportModel model = host.DirectVirtualViewport;
            AssertTrue(
                host.DirectVirtualActive && model != null,
                message + " keeps direct virtualization active");

            int offset = host.GetLogicalScrollOffset();
            int viewport = host.ClientSize.Height;
            int first = model.FindIndexAtOffset(offset);
            long firstContentEnd = model.GetOffset(first) +
                Math.Max(
                    0L,
                    model.GetExtent(first) -
                    (first < model.Count - 1
                        ? host.Spacing
                        : 0));

            // The viewport can begin in inter-item spacing. The model retains
            // that gap in the preceding stride, while the range calculator's
            // half-open item bounds correctly advances to the next row.
            if (first < model.Count - 1 &&
                offset >= firstContentEnd)
            {
                first++;
            }

            long endOffset = Math.Min(
                model.TotalExtent - 1L,
                (long)offset +
                (long)Math.Max(1, viewport) - 1L);
            int last = model.FindIndexAtOffset(endOffset);

            AssertTrue(
                host.DirectVirtualRealizedStart <= first &&
                host.DirectVirtualRealizedEnd >= last,
                message + " realizes the complete visible range " +
                "[offset=" + offset +
                ", viewport=" + viewport +
                ", visible=" + first + ".." + last +
                ", realized=" +
                    host.DirectVirtualRealizedStart + ".." +
                    host.DirectVirtualRealizedEnd +
                ", published=" +
                    host.DirectVirtualLastPublishedScrollAxis +
                ", physical=" +
                    Math.Max(0, -host.AutoScrollPosition.Y) + "]");
        }

        private static void AssertThrowsArgumentOutOfRange(
            TestAction action,
            string message)
        {
            try
            {
                action();
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Assertion failed: " + message + ".");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message + ".");
            }
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message +
                    ". Expected <" + expected +
                    ">, actual <" + actual + ">.");
            }
        }
    }
}
