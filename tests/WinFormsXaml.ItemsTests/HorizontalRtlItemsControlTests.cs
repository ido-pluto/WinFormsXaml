using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinFormsXaml;
using RuntimeItemsControl = WinFormsXaml.XamlRuntime.ItemsControl;

namespace WinFormsXaml.ItemsTests
{
    internal static class HorizontalRtlItemsControlTests
    {
        private const int WmHScroll = 0x0114;
        private const int SbLineLeft = 0;
        private const int SbPageLeft = 2;
        private const int SbThumbPosition = 4;
        private const int SbLeft = 6;
        private const int SbRight = 7;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr window,
            int message,
            IntPtr wParam,
            IntPtr lParam);

        private sealed class Row
        {
            public readonly string Title;

            public Row(int index)
            {
                Title = "RTL row " + index;
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal XamlRuntime Runtime;
            internal RuntimeItemsControl Host;

            public void Dispose()
            {
                if (Runtime != null)
                    Runtime.Dispose();

                Runtime = null;
                Host = null;
            }
        }

        private sealed class NativeScrollProbe : RuntimeItemsControl
        {
            internal ScrollEventArgs RaiseNativeScroll(
                ScrollEventType type,
                int oldPhysical,
                int newPhysical)
            {
                ScrollEventArgs args = new ScrollEventArgs(
                    type,
                    oldPhysical,
                    newPhysical,
                    ScrollOrientation.HorizontalScroll);

                base.OnScroll(args);
                return args;
            }
        }

        private sealed class LayoutCountingItemsControl : RuntimeItemsControl
        {
            internal int LayoutCount;

            protected override void OnLayout(LayoutEventArgs e)
            {
                LayoutCount++;
                base.OnLayout(e);
            }
        }

        internal static void RunAll()
        {
            TestOrdinaryNativeLogicalMappingAndReachability();
            TestNativePhysicalEventsAndSmoothPinning();
            TestOrdinaryRangeAndFlowTransitionsPreserveLogicalOffset();
            TestDirectVirtualNativeLogicalMappingAndScrollToIndex();
            TestDirectNativeAndProgrammaticOriginChangesPublishViewport();
            TestDirectOriginObserverIsNativeBacked();
            TestStyledOrdinaryAndDirectUseLogicalValues();
            TestKeepScrollBarOnRightCoalescesHorizontalLayout();
            TestKeepScrollBarOnRightRollbackAndReentrantOwnership();
        }

        private static void
            TestOrdinaryNativeLogicalMappingAndReachability()
        {
            using (Fixture fixture = CreateFixture(
                false,
                false,
                true,
                24))
            {
                RuntimeItemsControl host = fixture.Host;
                int initialPhysical = GetPhysicalOffset(host);
                int maximum = DiscoverMaximum(host);

                AssertTrue(maximum > 0, "ordinary fixture is scrollable");
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "ordinary RTL initializes at logical item zero");
                AssertEqual(
                    maximum,
                    initialPhysical,
                    "ordinary RTL explicitly initializes native P to M");
                AssertEqual(
                    RightToLeft.No,
                    host.RightToLeft,
                    "horizontal scrolling host remains native LTR");

                Rectangle viewport = host.ItemsViewportRectangleForTest;
                Control first = GetRenderedControl(host, 0);
                AssertIntersects(
                    first.Bounds,
                    viewport,
                    "item zero is visible at logical start");
                AssertTrue(
                    first.Right <= viewport.Right,
                    "item zero is anchored inside the right viewport edge");

                int middle = maximum / 2;
                host.SetLogicalScrollOffset(middle);
                AssertEqual(
                    middle,
                    host.GetLogicalScrollOffset(),
                    "ordinary midpoint is logical");
                AssertEqual(
                    maximum - middle,
                    GetPhysicalOffset(host),
                    "ordinary midpoint publishes P=M-L");

                host.SetLogicalScrollOffset(maximum);
                Control last = GetRenderedControl(
                    host,
                    host.RenderedItems.Count - 1);
                viewport = host.ItemsViewportRectangleForTest;
                AssertEqual(
                    0,
                    GetPhysicalOffset(host),
                    "logical end publishes physical zero");
                AssertIntersects(
                    last.Bounds,
                    viewport,
                    "logical end exposes the last row without a blank tail");

                long builds = host.ItemTemplateBlueprintBuildCount;
                long disposals = host.ItemControlTreeDisposedCount;
                host.SetLogicalScrollOffset(maximum / 3);
                AssertEqual(
                    builds,
                    host.ItemTemplateBlueprintBuildCount,
                    "ordinary pure scrolling does not rebuild row trees");
                AssertEqual(
                    disposals,
                    host.ItemControlTreeDisposedCount,
                    "ordinary pure scrolling does not dispose row trees");
            }

            using (Fixture fixture = CreateFixture(
                false,
                false,
                false,
                24))
            {
                AssertEqual(
                    RightToLeft.No,
                    fixture.Host.RightToLeft,
                    "KeepScrollBarOnRight=false still keeps horizontal host LTR");
                AssertRtlPhysicalMapping(
                    fixture.Host,
                    "ordinary KeepScrollBarOnRight=false");
            }
        }

        private static void TestNativePhysicalEventsAndSmoothPinning()
        {
            using (NativeScrollProbe host = new NativeScrollProbe())
            {
                host.Orientation = Orientation.Horizontal;
                host.ContentRightToLeft = true;
                host.Size = new Size(160, 70);
                host.AutoScrollMinSize = new Size(1000, 1);
                host.CreateControl();
                host.PerformLayout();

                int maximum = DiscoverMaximum(host);
                host.ScrollToStart();

                host.RaiseNativeScroll(
                    ScrollEventType.First,
                    maximum,
                    0);
                AssertEqual(
                    maximum,
                    host.GetLogicalScrollOffset(),
                    "physical First/SB_LEFT maps to logical end");

                host.RaiseNativeScroll(
                    ScrollEventType.Last,
                    0,
                    maximum);
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "physical Last/SB_RIGHT maps to logical start");

                int middlePhysical = maximum / 2;
                host.RaiseNativeScroll(
                    ScrollEventType.ThumbPosition,
                    maximum,
                    middlePhysical);
                AssertEqual(
                    maximum - middlePhysical,
                    host.GetLogicalScrollOffset(),
                    "native thumb P is converted to logical M-P");

                host.SmoothScroll = false;
                host.ScrollToStart();
                host.RaiseNativeScroll(
                    ScrollEventType.SmallDecrement,
                    maximum,
                    Math.Max(0, maximum - 1));
                int forward = host.GetLogicalScrollOffset();
                AssertTrue(
                    forward > 0,
                    "physical left/decrement advances logical RTL content");

                host.RaiseNativeScroll(
                    ScrollEventType.SmallIncrement,
                    maximum - forward,
                    maximum);
                AssertTrue(
                    host.GetLogicalScrollOffset() < forward,
                    "physical right/increment moves toward logical start");

                host.ScrollToStart();
                host.SmoothScroll = true;
                ScrollEventArgs smooth = host.RaiseNativeScroll(
                    ScrollEventType.SmallDecrement,
                    maximum,
                    Math.Max(0, maximum - 1));
                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest &&
                    host.SmoothScrollTargetOffsetForTest > 0,
                    "smooth native command targets logical forward motion");
                AssertEqual(
                    maximum,
                    smooth.NewValue,
                    "smooth native event pins NewValue back to physical P");
                host.StopSmoothScrollAnimation();

                host.SmoothScroll = false;
                host.LiveScroll = false;
                int committedLogical = Math.Max(1, maximum / 3);
                host.SetLogicalScrollOffset(committedLogical);
                int committedPhysical = GetPhysicalOffset(host);
                ScrollEventArgs deferred = host.RaiseNativeScroll(
                    ScrollEventType.ThumbTrack,
                    committedPhysical,
                    0);
                AssertEqual(
                    committedLogical,
                    host.GetLogicalScrollOffset(),
                    "LiveScroll=false ThumbTrack preserves committed logical L");
                AssertEqual(
                    committedPhysical,
                    deferred.NewValue,
                    "LiveScroll=false ThumbTrack pins actual physical P");

                host.RaiseNativeScroll(
                    ScrollEventType.ThumbPosition,
                    committedPhysical,
                    0);
                AssertEqual(
                    maximum,
                    host.GetLogicalScrollOffset(),
                    "ThumbPosition commits deferred physical thumb P");
            }
        }

        private static void
            TestOrdinaryRangeAndFlowTransitionsPreserveLogicalOffset()
        {
            using (Fixture fixture = CreateFixture(
                false,
                false,
                true,
                26))
            {
                RuntimeItemsControl host = fixture.Host;
                host.SmoothScroll = false;
                int maximum = DiscoverMaximum(host);
                int preserved = Math.Min(47, maximum / 2);
                host.SetLogicalScrollOffset(preserved);

                host.Width += 24;
                host.PerformLayout();
                AssertEqual(
                    preserved,
                    host.GetLogicalScrollOffset(),
                    "resize preserves nonzero logical L");

                host.SetItems(CreateRows(34));
                host.PerformLayout();
                AssertEqual(
                    preserved,
                    host.GetLogicalScrollOffset(),
                    "append/range growth preserves nonzero logical L");
                AssertPhysicalMatchesLogical(host, "append");

                host.ContentRightToLeft = false;
                host.PerformLayout();
                AssertEqual(
                    preserved,
                    host.GetLogicalScrollOffset(),
                    "RTL to LTR preserves framework logical offset");
                AssertEqual(
                    preserved,
                    GetPhysicalOffset(host),
                    "LTR uses physical P=L");

                host.ContentRightToLeft = true;
                host.PerformLayout();
                AssertEqual(
                    preserved,
                    host.GetLogicalScrollOffset(),
                    "LTR to RTL restores the same logical offset");
                AssertPhysicalMatchesLogical(host, "RTL restoration");

                host.Orientation = Orientation.Vertical;
                AssertEqual(
                    preserved,
                    host.GetLogicalScrollOffset(),
                    "horizontal to vertical transition preserves L");
                host.Orientation = Orientation.Horizontal;
                AssertEqual(
                    preserved,
                    host.GetLogicalScrollOffset(),
                    "vertical to horizontal transition preserves L");

                host.SetLogicalScrollOffset(Int32.MaxValue);
                int oldEnd = host.GetLogicalScrollOffset();
                host.SetItems(CreateRows(4));
                host.PerformLayout();
                int newEnd = host.GetLogicalScrollOffset();
                AssertEqual(
                    GetEffectiveHorizontalMaximum(host),
                    newEnd,
                    "range shrink clamps exactly to the new logical end");
                AssertTrue(newEnd <= oldEnd, "new end does not exceed old end");
                AssertEqual(
                    0,
                    GetPhysicalOffset(host),
                    "clamped RTL logical end publishes physical zero");
                AssertIntersects(
                    GetRenderedControl(host, host.RenderedItems.Count - 1).Bounds,
                    host.ItemsViewportRectangleForTest,
                    "range shrink keeps the last ordinary row visible");
                AssertPhysicalMatchesLogical(host, "shrink clamp");
            }
        }

        private static void
            TestDirectVirtualNativeLogicalMappingAndScrollToIndex()
        {
            using (Fixture fixture = CreateFixture(
                true,
                false,
                true,
                120))
            {
                RuntimeItemsControl host = fixture.Host;
                AssertTrue(
                    host.DirectVirtualActive,
                    "direct horizontal fixture activates Controls virtualization");
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "direct RTL begins at logical zero");
                AssertEqual(
                    0,
                    host.DirectVirtualRealizedStart,
                    "direct RTL initially realizes item zero");
                AssertRtlPhysicalMapping(host, "direct native");

                host.ScrollToIndex(70);
                AssertTrue(
                    host.DirectVirtualRealizedStart <= 70 &&
                    host.DirectVirtualRealizedEnd >= 70,
                    "direct ScrollToIndex consumes the logical host API");
                AssertPhysicalMatchesLogical(
                    host,
                    "direct ScrollToIndex");

                host.SetLogicalScrollOffset(Int32.MaxValue);
                AssertEqual(
                    host.Count - 1,
                    host.DirectVirtualRealizedEnd,
                    "direct logical end realizes the last item");
                Control last = GetRenderedControlByLogicalIndex(
                    host,
                    host.Count - 1);
                AssertIntersects(
                    last.Bounds,
                    host.ItemsViewportRectangleForTest,
                    "direct logical end has no blank viewport tail");

                host.ScrollToStart();
                host.SmoothScroll = true;
                host.ScrollBy(ScrollEventType.SmallIncrement);
                host.ApplySmoothScrollFrameForTest(60);
                AssertDirectViewportPublished(
                    host,
                    "direct smooth intermediate frame");
                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration);
                AssertTrue(
                    !host.SmoothScrollAnimationActiveForTest,
                    "direct smooth animation reaches its final frame");
                AssertDirectViewportPublished(
                    host,
                    "direct smooth final frame");
            }
        }

        private static void TestDirectOriginObserverIsNativeBacked()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='70' " +
                "Orientation='Horizontal' AutoScroll='true' " +
                "Virtualizing='true' VirtualizationThreshold='1' " +
                "FixedItemSize='36' OverscanItems='2' " +
                "ProgressiveRendering='false' Padding='4' Spacing='3'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='36' Height='32' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                RuntimeItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.ContentRightToLeft = true;
                host.SetItems(CreateRows(120));
                AssertTrue(
                    host.DirectVirtualActive,
                    "fixture activates direct mode");

                // WinForms may create the parent HWND eagerly while adding the
                // first realized child, or defer it until CreateControl. The
                // observer contract begins once the parent handle exists and
                // must be identical for both native lifecycle paths.
                host.CreateControl();
                AssertTrue(
                    host.IsHandleCreated,
                    "fixture creates the ItemsControl HWND");
                Control marker = GetScrollExtentMarker(host);
                AssertTrue(
                    marker != null &&
                    marker.IsHandleCreated &&
                    !marker.Visible &&
                    marker.Bounds == Rectangle.Empty,
                    "hidden zero-size origin observer owns a native child HWND");

                int maximum = GetEffectiveHorizontalMaximum(host);
                AssertTrue(maximum > 4, "pre-handle fixture has a native range");
                int firstPhysical = (maximum * 2) / 3;
                host.AutoScrollPosition = new Point(firstPhysical, 0);
                AssertDirectViewportPublished(
                    host,
                    "pre-handle AutoScrollPosition jump");

                maximum = GetEffectiveHorizontalMaximum(host);
                int secondPhysical = Math.Max(1, maximum / 3);
                host.HorizontalScroll.Value = secondPhysical;
                AssertDirectViewportPublished(
                    host,
                    "pre-handle HorizontalScroll.Value jump");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestStyledOrdinaryAndDirectUseLogicalValues()
        {
            TestStyledFixture(false, "styled ordinary");
            TestStyledFixture(true, "styled direct");
        }

        private static void
            TestDirectNativeAndProgrammaticOriginChangesPublishViewport()
        {
            using (Fixture fixture = CreateFixture(
                true,
                false,
                true,
                120))
            {
                RuntimeItemsControl host = fixture.Host;
                host.SmoothScroll = false;
                int maximum = DiscoverMaximum(host);

                SendNativeHorizontalScroll(host, SbPageLeft, 0);
                AssertTrue(
                    host.GetLogicalScrollOffset() > 0,
                    "native physical page-left advances direct logical RTL");
                AssertDirectViewportPublished(
                    host,
                    "native page-left");

                SendNativeHorizontalScroll(host, SbLeft, 0);
                maximum = GetEffectiveHorizontalMaximum(host);
                AssertEqual(
                    maximum,
                    host.GetLogicalScrollOffset(),
                    "native physical left reaches direct logical end");
                AssertEqual(
                    host.Count - 1,
                    host.DirectVirtualRealizedEnd,
                    "native physical left realizes the final direct row");
                AssertDirectViewportPublished(host, "native physical left");

                SendNativeHorizontalScroll(host, SbRight, 0);
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "native physical right returns direct logical start");
                AssertDirectViewportPublished(host, "native physical right");

                maximum = GetEffectiveHorizontalMaximum(host);
                int thumbPhysical = maximum / 2;
                SendNativeHorizontalScroll(
                    host,
                    SbThumbPosition,
                    thumbPhysical);
                AssertEqual(
                    GetEffectiveHorizontalMaximum(host) -
                        GetPhysicalOffset(host),
                    host.GetLogicalScrollOffset(),
                    "native thumb premove publishes current logical M-P");
                AssertDirectViewportPublished(host, "native thumb position");

                SendNativeHorizontalScroll(host, SbLineLeft, 0);
                AssertDirectViewportPublished(host, "repeated native line-left");
            }

            TestDirectProgrammaticOrigins(false, "native direct");
            TestDirectProgrammaticOrigins(true, "styled direct");
        }

        private static void TestDirectProgrammaticOrigins(
            bool styled,
            string name)
        {
            using (Fixture fixture = CreateFixture(
                true,
                styled,
                true,
                120))
            {
                RuntimeItemsControl host = fixture.Host;
                int maximum = DiscoverMaximum(host);
                int firstLogical = Math.Max(1, maximum / 3);
                int firstPhysical = maximum - firstLogical;

                host.AutoScrollPosition =
                    new Point(firstPhysical, 0);
                AssertEqual(
                    GetEffectiveHorizontalMaximum(host) -
                        GetPhysicalOffset(host),
                    host.GetLogicalScrollOffset(),
                    name + " inherited AutoScrollPosition maps current P to L");
                AssertDirectViewportPublished(
                    host,
                    name + " inherited AutoScrollPosition");

                maximum = GetEffectiveHorizontalMaximum(host);
                int secondLogical = Math.Max(
                    firstLogical + 1,
                    (maximum * 2) / 3);
                secondLogical = Math.Min(maximum, secondLogical);
                int secondPhysical = maximum - secondLogical;
                host.HorizontalScroll.Value = secondPhysical;
                AssertEqual(
                    GetEffectiveHorizontalMaximum(host) -
                        GetPhysicalOffset(host),
                    host.GetLogicalScrollOffset(),
                    name + " inherited HorizontalScroll.Value maps current P to L");
                AssertDirectViewportPublished(
                    host,
                    name + " inherited HorizontalScroll.Value");
            }
        }

        private static void TestStyledFixture(
            bool direct,
            string name)
        {
            using (Fixture fixture = CreateFixture(
                direct,
                true,
                false,
                direct ? 120 : 28))
            {
                RuntimeItemsControl host = fixture.Host;
                host.SmoothScroll = false;
                ScrollBarControl bar = host.ThemedScrollBarForTest;
                AssertTrue(
                    bar != null && bar.Visible,
                    name + " exposes the framework bar");
                AssertRtlPhysicalMapping(host, name);

                int maximum = DiscoverMaximum(host);
                host.ScrollToStart();
                bar.ExecuteScrollCommand(
                    ScrollEventType.SmallIncrement);
                AssertTrue(
                    host.GetLogicalScrollOffset() > 0,
                    name + " custom arrow increments logical L");
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    bar.Value,
                    name + " bar Value remains logical");
                AssertTrue(
                    GetPhysicalOffset(host) < maximum,
                    name + " logical forward motion lowers physical P");

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    host.ScrollToStart();
                    host.ProcessMouseWheelDelta(-120);
                    int hostWheel = host.GetLogicalScrollOffset();
                    host.ScrollToStart();
                    InvokeMouseWheel(bar, -120);
                    AssertEqual(
                        hostWheel,
                        host.GetLogicalScrollOffset(),
                        name + " host and custom wheel share logical direction");
                }

                host.ScrollToStart();
                host.SmoothScroll = true;
                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);
                int firstTarget =
                    host.SmoothScrollTargetOffsetForTest;
                host.ApplySmoothScrollFrameForTest(30);
                int firstFrame = host.GetLogicalScrollOffset();
                AssertTrue(
                    firstFrame > 0 && firstFrame < firstTarget,
                    name + " styled RTL publishes an intermediate frame");
                AssertEqual(
                    firstFrame,
                    bar.Value,
                    name + " styled RTL thumb follows the intermediate frame");

                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);
                AssertTrue(
                    host.SmoothScrollTargetOffsetForTest > firstTarget,
                    name + " styled RTL retarget accumulates from the pending target");
                host.ApplySmoothScrollFrameForTest(30);
                AssertTrue(
                    host.GetLogicalScrollOffset() > firstFrame,
                    name + " styled RTL retarget remains logically forward");
                AssertPhysicalMatchesLogical(
                    host,
                    name + " styled RTL retarget frame");
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    bar.Value,
                    name + " styled RTL retarget keeps thumb synchronized");
                AssertTrue(
                    !host.ActiveNativeScrollStyleVisibleForTest,
                    name + " styled RTL retarget never exposes native chrome");

                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration);
            }
        }

        private static void
            TestKeepScrollBarOnRightCoalescesHorizontalLayout()
        {
            XamlRuntime runtime = XamlRuntime.Load("<Panel />");

            try
            {
                using (LayoutCountingItemsControl host =
                    new LayoutCountingItemsControl())
                {
                    host.Runtime = runtime;
                    host.Orientation = Orientation.Horizontal;
                    host.ContentRightToLeft = true;
                    host.HorizontalScrollStyle = new ScrollBarStyle();
                    host.Size = new Size(180, 70);
                    host.AutoScrollMinSize = new Size(900, 1);
                    host.CreateControl();
                    host.PerformLayout();

                    int before = host.LayoutCount;
                    host.KeepScrollBarOnRight = false;
                    AssertEqual(
                        before + 1,
                        host.LayoutCount,
                        "KeepScrollBarOnRight change issues one layout response");
                    AssertEqual(
                        RightToLeft.No,
                        host.RightToLeft,
                        "coalesced horizontal transition retains native LTR host");
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestKeepScrollBarOnRightRollbackAndReentrantOwnership()
        {
            using (Fixture fixture = CreateFixture(
                false,
                true,
                true,
                28))
            {
                RuntimeItemsControl host = fixture.Host;
                host.SetLogicalScrollOffset(31);
                int oldLogical = host.GetLogicalScrollOffset();
                Size oldExtent = host.AutoScrollMinSize;
                Point oldOrigin = host.AutoScrollPosition;
                bool throwLayout = true;
                LayoutEventHandler throwing = delegate
                {
                    if (!throwLayout)
                        return;

                    throwLayout = false;
                    throw new InvalidOperationException("keep rollback");
                };
                host.Layout += throwing;
                bool threw = false;

                try
                {
                    host.KeepScrollBarOnRight = false;
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }

                host.Layout -= throwing;
                AssertTrue(threw, "throwing KeepScrollBarOnRight layout propagates");
                AssertTrue(
                    host.KeepScrollBarOnRight,
                    "failed KeepScrollBarOnRight restores the old property");
                AssertEqual(
                    RightToLeft.No,
                    host.RightToLeft,
                    "failed KeepScrollBarOnRight restores native direction");
                AssertTrue(
                    host.ContentRightToLeft,
                    "failed KeepScrollBarOnRight restores content direction");
                AssertEqual(oldLogical, host.GetLogicalScrollOffset(),
                    "failed KeepScrollBarOnRight restores logical L");
                AssertEqual(oldExtent, host.AutoScrollMinSize,
                    "failed KeepScrollBarOnRight restores extent");
                AssertEqual(oldOrigin, host.AutoScrollPosition,
                    "failed KeepScrollBarOnRight restores physical origin");

                bool reenter = true;
                LayoutEventHandler reentrant = delegate
                {
                    if (!reenter)
                        return;

                    reenter = false;
                    host.KeepScrollBarOnRight = true;
                    host.SetLogicalScrollOffset(47);
                    throw new InvalidOperationException("outer keep failure");
                };
                host.Layout += reentrant;
                threw = false;

                try
                {
                    host.KeepScrollBarOnRight = false;
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }

                host.Layout -= reentrant;
                AssertTrue(threw, "reentrant outer Keep failure propagates");
                AssertTrue(
                    host.KeepScrollBarOnRight,
                    "inner Keep mutation owns final property state");
                AssertEqual(
                    47,
                    host.GetLogicalScrollOffset(),
                    "outer rollback does not overwrite inner logical state");
            }
        }

        private static Fixture CreateFixture(
            bool direct,
            bool styled,
            bool keepScrollBarOnRight,
            int count)
        {
            string style = styled
                ? "  <ItemsControl.HorizontalScrollStyle>" +
                  "    <ScrollBarStyle Thickness='14' />" +
                  "  </ItemsControl.HorizontalScrollStyle>"
                : String.Empty;
            string markup =
                "<ItemsControl Name='Rows' Width='180' Height='70' " +
                "Orientation='Horizontal' AutoScroll='true' " +
                "Virtualizing='" + (direct ? "true" : "false") + "' " +
                "VirtualizationThreshold='1' FixedItemSize='36' " +
                "OverscanItems='2' ProgressiveRendering='false' " +
                "Padding='4' Spacing='3'>" +
                style +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='36' Height='32' " +
                "        Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            Fixture fixture = new Fixture();
            fixture.Runtime = XamlRuntime.Load(markup);
            fixture.Host = fixture.Runtime.GetItemsControl("Rows");
            fixture.Host.CreateControl();
            fixture.Host.ContentRightToLeft = true;

            if (!keepScrollBarOnRight)
                fixture.Host.KeepScrollBarOnRight = false;

            fixture.Host.SetItems(CreateRows(count));
            fixture.Host.PerformLayout();
            Application.DoEvents();
            return fixture;
        }

        private static ArrayList CreateRows(int count)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new Row(i));

            return rows;
        }

        private static int DiscoverMaximum(RuntimeItemsControl host)
        {
            int pass;

            // Direct variable measurement can refine M while the requested
            // end range is realized. Converge through the public logical API;
            // ordinary fixed geometry exits on the first pass.
            for (pass = 0; pass < 8; pass++)
            {
                host.SetLogicalScrollOffset(Int32.MaxValue);
                host.PerformLayout();

                if (host.GetLogicalScrollOffset() ==
                    GetEffectiveHorizontalMaximum(host))
                {
                    break;
                }
            }

            host.ScrollToStart();
            host.PerformLayout();
            return GetEffectiveHorizontalMaximum(host);
        }

        private static int GetPhysicalOffset(RuntimeItemsControl host)
        {
            int value = host.AutoScrollPosition.X;

            if (value >= 0)
                return 0;

            return value == Int32.MinValue
                ? Int32.MaxValue
                : -value;
        }

        private static void AssertRtlPhysicalMapping(
            RuntimeItemsControl host,
            string name)
        {
            int maximum = DiscoverMaximum(host);
            int logical = host.GetLogicalScrollOffset();
            int physical = GetPhysicalOffset(host);
            AssertTrue(maximum > 0, name + " has a nonzero range");
            AssertEqual(
                0,
                logical,
                name + " starts at L0 (M=" + maximum +
                ", P=" + physical +
                ", published=" +
                host.DirectVirtualLastPublishedScrollAxis +
                ", range=" + host.DirectVirtualRealizedStart +
                ".." + host.DirectVirtualRealizedEnd + ")");
            AssertEqual(maximum, physical, name + " L0 uses P=M");

            int middle = Math.Max(1, maximum / 3);
            host.SetLogicalScrollOffset(middle);
            AssertPhysicalMatchesLogical(host, name + " midpoint");
        }

        private static void AssertPhysicalMatchesLogical(
            RuntimeItemsControl host,
            string name)
        {
            int logical = host.GetLogicalScrollOffset();
            int physical = GetPhysicalOffset(host);
            int maximum = GetEffectiveHorizontalMaximum(host);
            AssertEqual(
                maximum - logical,
                physical,
                name + " preserves P=M-L");
        }

        private static int GetEffectiveHorizontalMaximum(
            RuntimeItemsControl host)
        {
            long maximum =
                (long)host.HorizontalScroll.Maximum -
                (long)Math.Max(
                    0,
                    host.HorizontalScroll.LargeChange) +
                1L;

            if (maximum <= 0L)
                return 0;

            return maximum >= Int32.MaxValue
                ? Int32.MaxValue
                : (int)maximum;
        }

        private static Control GetRenderedControl(
            RuntimeItemsControl host,
            int recordIndex)
        {
            object record = host.RenderedItems[recordIndex];
            FieldInfo controlField = record.GetType().GetField(
                "Control",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Control control = controlField == null
                ? null
                : controlField.GetValue(record) as Control;

            AssertTrue(
                control != null && !control.IsDisposed,
                "rendered record exposes a live control");
            return control;
        }

        private static Control GetScrollExtentMarker(
            RuntimeItemsControl host)
        {
            FieldInfo field = typeof(RuntimeItemsControl).GetField(
                "_scrollExtentMarker",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

            return field == null
                ? null
                : field.GetValue(host) as Control;
        }

        private static Control GetRenderedControlByLogicalIndex(
            RuntimeItemsControl host,
            int logicalIndex)
        {
            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                object record = host.RenderedItems[i];
                FieldInfo indexField = record.GetType().GetField(
                    "LogicalIndex",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                if (indexField != null &&
                    (int)indexField.GetValue(record) == logicalIndex)
                {
                    return GetRenderedControl(host, i);
                }
            }

            throw new InvalidOperationException(
                "Assertion failed: logical row " + logicalIndex +
                " is not realized.");
        }

        private static void InvokeMouseWheel(
            ScrollBarControl bar,
            int delta)
        {
            MethodInfo method = typeof(ScrollBarControl).GetMethod(
                "OnMouseWheel",
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            AssertTrue(method != null, "scrollbar exposes OnMouseWheel");
            method.Invoke(
                bar,
                new object[]
                {
                    new HandledMouseEventArgs(
                        MouseButtons.None,
                        0,
                        0,
                        0,
                        delta)
                });
        }

        private static void SendNativeHorizontalScroll(
            RuntimeItemsControl host,
            int command,
            int physicalPosition)
        {
            int word = (physicalPosition & 0xffff) << 16;
            word |= command & 0xffff;
            SendMessage(
                host.Handle,
                WmHScroll,
                new IntPtr(word),
                IntPtr.Zero);
            Application.DoEvents();
        }

        private static void AssertDirectViewportPublished(
            RuntimeItemsControl host,
            string message)
        {
            int logical = host.GetLogicalScrollOffset();
            int expectedIndex = Math.Min(
                host.Count - 1,
                logical / Math.Max(
                    1,
                    host.FixedItemSize + host.Spacing));

            AssertTrue(
                host.DirectVirtualHasPublishedScrollAxis &&
                host.DirectVirtualLastPublishedScrollAxis == logical,
                message + " publishes the current logical axis");
            AssertTrue(
                host.DirectVirtualRealizedStart <= expectedIndex &&
                host.DirectVirtualRealizedEnd >= expectedIndex,
                message + " realizes the visible logical row");

            Rectangle viewport = host.ItemsViewportRectangleForTest;
            bool intersects = false;
            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                Control control = GetRenderedControl(host, i);

                if (control.Bounds.Width > 0 &&
                    control.Bounds.Height > 0 &&
                    control.Bounds.Right > viewport.Left &&
                    control.Bounds.Left < viewport.Right &&
                    control.Bounds.Bottom > viewport.Top &&
                    control.Bounds.Top < viewport.Bottom)
                {
                    intersects = true;
                    break;
                }
            }

            AssertTrue(
                intersects,
                message + " leaves no blank direct viewport");
            AssertPhysicalMatchesLogical(host, message);
        }

        private static void AssertIntersects(
            Rectangle bounds,
            Rectangle viewport,
            string message)
        {
            AssertTrue(
                bounds.Width > 0 &&
                bounds.Height > 0 &&
                bounds.Right > viewport.Left &&
                bounds.Left < viewport.Right &&
                bounds.Bottom > viewport.Top &&
                bounds.Top < viewport.Bottom,
                message);
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
                    ". Expected " + expected +
                    ", actual " + actual + ".");
            }
        }
    }
}
