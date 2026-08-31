using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class ItemsControlScrollIntoViewTests
    {
        private sealed class Row
        {
            public readonly string Id;
            public readonly string Title;
            public readonly bool Show;
            public readonly int Height;

            public Row(int index, bool show)
                : this(index, show, 20)
            {
            }

            public Row(int index, bool show, int height)
            {
                Id = "row-" + index;
                Title = "Row " + index;
                Show = show;
                Height = height;
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal XamlRuntime Runtime;
            internal XamlRuntime.ItemsControl Host;

            public void Dispose()
            {
                if (Runtime != null)
                    Runtime.Dispose();

                Runtime = null;
                Host = null;
            }
        }

        internal static void RunAll()
        {
            TestTargetCalculationCoversEveryAlignment();
            TestNormalRendererAlignmentsAndLogicalConditionGap();
            TestNormalHorizontalRtlAlignment();
            TestDirectHorizontalRtlAlignment();
            TestVariableDirectAlignmentCorrectsMeasuredExtent();
            TestAnimatedVariableDirectAlignmentRetargetsAfterMeasurement();
            TestLightweightAlignment();
            TestAnimationOverrideWinsOverHostDefault();
            TestProgressiveRefreshDefersOnlyTheNewestRequest();
            TestOwnerThreadMarshal();
            TestItemsBindingScrollWaitsForCommittedChanges();
            TestItemsBindingBroadcastAndDetach();
        }

        private static void TestTargetCalculationCoversEveryAlignment()
        {
            AssertEqual(
                200,
                XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                    200L,
                    40L,
                    100,
                    100,
                    ItemScrollAlignment.Start),
                "Start uses the item leading edge");
            AssertEqual(
                170,
                XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                    200L,
                    40L,
                    100,
                    100,
                    ItemScrollAlignment.Center),
                "Center balances the free viewport space");
            AssertEqual(
                140,
                XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                    200L,
                    40L,
                    100,
                    100,
                    ItemScrollAlignment.End),
                "End uses the item trailing edge");
            AssertEqual(
                140,
                XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                    200L,
                    40L,
                    100,
                    100,
                    ItemScrollAlignment.Nearest),
                "Nearest chooses the smaller movement");
            AssertEqual(
                100,
                XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                    120L,
                    20L,
                    100,
                    100,
                    ItemScrollAlignment.Nearest),
                "Nearest preserves an already visible item");
            AssertEqual(
                100,
                XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                    80L,
                    180L,
                    100,
                    100,
                    ItemScrollAlignment.Nearest),
                "Nearest preserves a viewport already contained by a large item");
        }

        private static void
            TestNormalRendererAlignmentsAndLogicalConditionGap()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "Spacing='4'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Margin='0' " +
                "Condition='{Binding Show}' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = CreateFixture(markup, CreateRows(20, 3));

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                int itemExtent = 20;
                int stride = 24;
                int index = 10;
                // One earlier root is collapsed, so the tenth logical item is
                // the ninth visual slot. This catches compact rendered-record
                // indexing accidentally selecting the next logical item.
                int logicalStart = (index - 1) * stride;
                int viewport = host.ItemsViewportRectangleForTest.Height;

                host.ScrollIntoView(
                    index,
                    ItemScrollAlignment.Start,
                    false);
                AssertEqual(
                    logicalStart,
                    host.GetLogicalScrollOffset(),
                    "normal Start resolves the exact logical record after a root-condition gap");

                host.ScrollIntoView(
                    index,
                    ItemScrollAlignment.Center,
                    false);
                AssertEqual(
                    XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                        logicalStart,
                        itemExtent,
                        logicalStart,
                        viewport,
                        ItemScrollAlignment.Center),
                    host.GetLogicalScrollOffset(),
                    "normal Center uses the actual visible item bounds");

                host.ScrollIntoView(
                    index,
                    ItemScrollAlignment.End,
                    false);
                AssertEqual(
                    XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                        logicalStart,
                        itemExtent,
                        host.GetLogicalScrollOffset(),
                        viewport,
                        ItemScrollAlignment.End),
                    host.GetLogicalScrollOffset(),
                    "normal End aligns the trailing edge");

                host.ScrollToStart();
                host.ScrollIntoView(1);
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "default Nearest does not move an already visible item");

                host.ScrollToIndex(index);
                AssertEqual(
                    logicalStart,
                    host.GetLogicalScrollOffset(),
                    "ScrollToIndex remains an immediate leading-edge compatibility API");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void TestDirectHorizontalRtlAlignment()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='70' " +
                "Orientation='Horizontal' Virtualizing='true' " +
                "VirtualizationThreshold='1' FixedItemSize='36' " +
                "OverscanItems='2' ProgressiveRendering='false' " +
                "Spacing='3'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='36' Height='32' Margin='0' " +
                "Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = CreateFixture(markup, CreateRows(80, -1));

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                host.ContentRightToLeft = true;
                host.PerformLayout();

                int index = 30;
                long start = (long)index * 39L;
                int viewport = host.ItemsViewportRectangleForTest.Width;
                int expected =
                    XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                        start,
                        36L,
                        0,
                        viewport,
                        ItemScrollAlignment.Center);

                host.ScrollIntoView(
                    index,
                    ItemScrollAlignment.Center,
                    false);

                AssertTrue(
                    host.DirectVirtualActive,
                    "horizontal RTL fixture uses Controls virtualization");
                AssertEqual(
                    expected,
                    host.GetLogicalScrollOffset(),
                    "direct horizontal RTL Center remains a logical-axis operation");
                AssertTrue(
                    host.DirectVirtualRealizedStart <= index &&
                    host.DirectVirtualRealizedEnd >= index,
                    "direct immediate scrolling realizes the requested item before returning");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void TestNormalHorizontalRtlAlignment()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='70' " +
                "Orientation='Horizontal' Virtualizing='false' " +
                "ProgressiveRendering='false' Spacing='3'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='36' Height='32' Margin='0' " +
                "Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = CreateFixture(markup, CreateRows(40, -1));

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                host.ContentRightToLeft = true;
                host.PerformLayout();

                int index = 12;
                long start = (long)index * 39L;
                int expected =
                    XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                        start,
                        36L,
                        0,
                        host.ItemsViewportRectangleForTest.Width,
                        ItemScrollAlignment.End);

                host.ScrollIntoView(
                    index,
                    ItemScrollAlignment.End,
                    false);

                AssertEqual(
                    expected,
                    host.GetLogicalScrollOffset(),
                    "ordinary horizontal RTL End uses logical rather than native coordinates");
                AssertRtlPhysicalMapping(host);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void
            TestVariableDirectAlignmentCorrectsMeasuredExtent()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='true' VirtualizationThreshold='1' " +
                "EstimatedItemSize='20' OverscanItems='1' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Height='{Binding Height}' Margin='0' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            ArrayList rows = new ArrayList();
            int targetIndex = 30;
            int i;

            for (i = 0; i < 80; i++)
            {
                rows.Add(
                    new Row(
                        i,
                        true,
                        i == targetIndex ? 120 : 20));
            }

            Fixture fixture = CreateFixture(markup, rows);

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                host.ScrollIntoView(
                    targetIndex,
                    ItemScrollAlignment.Center,
                    false);

                AssertTrue(
                    host.DirectVirtualActive,
                    "variable-size alignment fixture uses Controls virtualization");
                AssertEqual(
                    615,
                    host.GetLogicalScrollOffset(),
                    "Center is recomputed after the target's actual extent is measured");

                Control target = FindRenderedControl(
                    host,
                    targetIndex);
                Rectangle viewport =
                    host.ItemsViewportRectangleForTest;

                AssertTrue(
                    Math.Abs(
                        (target.Top + target.Bottom) -
                        (viewport.Top + viewport.Bottom)) <= 1,
                    "the measured variable-height item is physically centered");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void
            TestAnimatedVariableDirectAlignmentRetargetsAfterMeasurement()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='true' VirtualizationThreshold='1' " +
                "EstimatedItemSize='20' OverscanItems='1' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Height='{Binding Height}' Margin='0' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            ArrayList rows = new ArrayList();
            int targetIndex = 30;
            int i;

            for (i = 0; i < 80; i++)
            {
                rows.Add(
                    new Row(
                        i,
                        true,
                        i == targetIndex ? 120 : 20));
            }

            Fixture fixture = CreateFixture(markup, rows);

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                host.ScrollIntoView(
                    targetIndex,
                    ItemScrollAlignment.Center,
                    true);

                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "variable-size item animation starts before realization");
                AssertEqual(
                    565,
                    host.SmoothScrollTargetOffsetForTest,
                    "the initial animated target uses the declared estimate");

                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration - 1);

                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "measurement retargets instead of completing the stale transition");
                AssertEqual(
                    615,
                    host.SmoothScrollTargetOffsetForTest,
                    "measurement replaces the estimated animated target");

                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration);

                AssertEqual(
                    615,
                    host.GetLogicalScrollOffset(),
                    "the retargeted transition reaches measured Center alignment");

                Control target = FindRenderedControl(
                    host,
                    targetIndex);
                Rectangle viewport =
                    host.ItemsViewportRectangleForTest;

                AssertTrue(
                    Math.Abs(
                        (target.Top + target.Bottom) -
                        (viewport.Top + viewport.Bottom)) <= 1,
                    "the animated variable-height item finishes physically centered");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void AssertRtlPhysicalMapping(
            XamlRuntime.ItemsControl host)
        {
            int physical = host.AutoScrollPosition.X >= 0
                ? 0
                : -host.AutoScrollPosition.X;
            int maximum = host.HorizontalScroll.Maximum -
                Math.Max(0, host.HorizontalScroll.LargeChange) + 1;

            AssertEqual(
                Math.Max(0, maximum - host.GetLogicalScrollOffset()),
                physical,
                "horizontal RTL publishes P=M-L after item alignment");
        }

        private static void TestLightweightAlignment()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='200' Height='100' " +
                "Orientation='Vertical' Virtualizing='true' " +
                "VirtualizationMode='Lightweight' FixedItemSize='24' " +
                "ProgressiveRendering='false' Spacing='2'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='24' Margin='0' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = CreateFixture(markup, CreateRows(100, -1));

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                int index = 40;
                long start = (long)index * 26L;
                int viewport = host.ItemsViewportRectangleForTest.Height;
                int expected =
                    XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                        start,
                        24L,
                        0,
                        viewport,
                        ItemScrollAlignment.End);

                host.ScrollIntoView(
                    index,
                    ItemScrollAlignment.End,
                    false);

                AssertTrue(
                    host.LightweightActive,
                    "lightweight fixture activates the painted backend");
                AssertEqual(
                    expected,
                    host.GetLogicalScrollOffset(),
                    "Lightweight End uses the shared logical alignment calculation");
                AssertTrue(
                    host.LightweightRealizedStart <= index &&
                    host.LightweightRealizedEnd >= index,
                    "Lightweight immediate scrolling publishes a range containing the item");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void TestAnimationOverrideWinsOverHostDefault()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "Spacing='4'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Margin='0' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = CreateFixture(markup, CreateRows(30, -1));

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                host.SmoothScroll = false;
                host.ScrollIntoView(
                    15,
                    ItemScrollAlignment.Start,
                    true);

                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "explicit animate=true starts animation while SmoothScroll is false");
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "forced animation does not jump synchronously");

                int target = host.SmoothScrollTargetOffsetForTest;
                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration);
                AssertEqual(
                    target,
                    host.GetLogicalScrollOffset(),
                    "forced animation reaches the exact requested alignment");

                host.ScrollToStart();
                host.SmoothScroll = true;
                host.ScrollIntoView(
                    14,
                    ItemScrollAlignment.Center);
                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "the overload without animate follows SmoothScroll=true");
                host.StopSmoothScrollAnimation();

                host.ScrollToStart();
                host.ScrollIntoView(
                    12,
                    ItemScrollAlignment.Center,
                    false);
                AssertTrue(
                    !host.SmoothScrollAnimationActiveForTest &&
                    host.GetLogicalScrollOffset() > 0,
                    "explicit animate=false stays immediate while SmoothScroll is true");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void TestItemsBindingBroadcastAndDetach()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Margin='0' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            ItemsBinding<Row> shared = new ItemsBinding<Row>();
            int i;

            for (i = 0; i < 30; i++)
                shared.Add(new Row(i, true));

            Fixture first = CreateFixture(markup, shared);
            Fixture second = CreateFixture(markup, shared);

            try
            {
                shared.ScrollIndexIntoView(
                    18,
                    ItemScrollAlignment.Start,
                    false);

                AssertEqual(
                    360,
                    first.Host.GetLogicalScrollOffset(),
                    "ItemsBinding scroll requests reach the first observing host");
                AssertEqual(
                    360,
                    second.Host.GetLogicalScrollOffset(),
                    "one shared ItemsBinding intentionally broadcasts to every host");

                Row repeated = shared[2];
                shared.Insert(10, repeated);
                shared.ScrollIntoView(
                    repeated,
                    ItemScrollAlignment.Start,
                    false);
                Application.DoEvents();
                AssertEqual(
                    40,
                    first.Host.GetLogicalScrollOffset(),
                    "item lookup deterministically chooses the first equal occurrence");

                ItemsBinding<Row> replacement =
                    new ItemsBinding<Row>();

                for (i = 0; i < 30; i++)
                    replacement.Add(new Row(100 + i, true));

                second.Host.SetItems(replacement);
                second.Host.ScrollToStart();
                shared.ScrollIndexIntoView(
                    20,
                    ItemScrollAlignment.Start,
                    false);

                AssertTrue(
                    first.Host.GetLogicalScrollOffset() > 0,
                    "the remaining shared-binding host still receives requests");
                AssertEqual(
                    0,
                    second.Host.GetLogicalScrollOffset(),
                    "a host detaches when its ItemsSource is replaced");

                first.Dispose();
                shared.ScrollIndexIntoView(
                    5,
                    ItemScrollAlignment.Start,
                    false);
                AssertEqual(
                    0,
                    second.Host.GetLogicalScrollOffset(),
                    "disposing a host removes its weak binding observer safely");
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }
        }

        private static void
            TestItemsBindingScrollWaitsForCommittedChanges()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Margin='0' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            ItemsBinding<Row> rows = new ItemsBinding<Row>();
            int i;

            for (i = 0; i < 30; i++)
                rows.Add(new Row(i, true));

            Fixture fixture = CreateFixture(markup, rows);

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                Row appended = new Row(1000, true);
                rows.Add(appended);
                rows.ScrollIntoView(
                    appended,
                    ItemScrollAlignment.Start,
                    false);

                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "Add then ScrollIntoView waits for the queued host patch");

                Application.DoEvents();

                AssertTrue(
                    host.GetLogicalScrollOffset() > 0,
                    "a newly appended item is shown after its patch commits");
                AssertTrue(
                    host.RenderedItems.Count == rows.Count,
                    "the append is committed before its scroll request runs");

                host.ScrollToStart();
                Row movedBeforeCommit = new Row(1001, true);
                rows.Insert(15, movedBeforeCommit);
                rows.ScrollIntoView(
                    movedBeforeCommit,
                    ItemScrollAlignment.Start,
                    false);
                rows.Insert(0, new Row(1002, true));

                Application.DoEvents();

                AssertEqual(
                    16 * 20,
                    host.GetLogicalScrollOffset(),
                    "the item overload re-resolves its current occurrence after a coalesced move");

                host.ScrollToStart();
                Row removedBeforeCommit = new Row(1003, true);
                rows.Insert(20, removedBeforeCommit);
                rows.ScrollIntoView(
                    removedBeforeCommit,
                    ItemScrollAlignment.End,
                    false);
                rows.Remove(removedBeforeCommit);

                Application.DoEvents();

                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "a removed deferred item is ignored instead of targeting its stale index");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void
            TestProgressiveRefreshDefersOnlyTheNewestRequest()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "Spacing='4'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Margin='0' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = CreateFixture(markup, CreateRows(8, -1));

            try
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                ArrayList firstReplacement = CreateRows(24, -1);
                host.ProgressiveRendering = true;
                host.ProgressiveBatchSize = 1;
                host.ProgressiveInterval = 60000;
                host.SetItems(firstReplacement);

                AssertTrue(
                    host.IsRefreshing,
                    "progressive replacement remains pending");

                host.ScrollIntoView(
                    6,
                    ItemScrollAlignment.Start,
                    false);
                host.ScrollIntoView(
                    18,
                    ItemScrollAlignment.Center,
                    false);

                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "a request does not target partially published rows");

                CompleteProgressiveRefresh(host);

                int expected =
                    XamlRuntime.ItemsControl.CalculateItemScrollTarget(
                        18L * 24L,
                        20L,
                        0,
                        host.ItemsViewportRectangleForTest.Height,
                        ItemScrollAlignment.Center);

                AssertEqual(
                    expected,
                    host.GetLogicalScrollOffset(),
                    "the newest deferred request runs after commit");

                host.ScrollToStart();
                ArrayList staleSource = CreateRows(26, -1);
                host.SetItems(staleSource);
                host.ScrollIntoView(
                    22,
                    ItemScrollAlignment.End,
                    false);

                ArrayList currentSource = CreateRows(12, -1);
                host.SetItems(currentSource);
                CompleteProgressiveRefresh(host);

                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "source replacement discards a stale deferred request");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void TestOwnerThreadMarshal()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Margin='0' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = CreateFixture(markup, CreateRows(30, -1));

            try
            {
                Exception workerError = null;
                int drainCount =
                    fixture.Host.ItemScrollDispatchDrainCountForTest;
                Thread worker = new Thread(
                    delegate()
                    {
                        try
                        {
                            int i;

                            for (i = 0; i < 50; i++)
                            {
                                fixture.Host.ScrollIntoView(
                                    i % 30,
                                    ItemScrollAlignment.Start,
                                    false);
                            }
                        }
                        catch (Exception ex)
                        {
                            workerError = ex;
                        }
                    });

                worker.Start();
                worker.Join();

                AssertEqual(
                    drainCount,
                    fixture.Host.ItemScrollDispatchDrainCountForTest,
                    "worker bursts remain in one pending UI dispatch");

                Application.DoEvents();

                AssertTrue(
                    workerError == null,
                    "post-handle worker calls marshal without throwing");
                AssertEqual(
                    19 * 20,
                    fixture.Host.GetLogicalScrollOffset(),
                    "the newest queued item scroll runs on the owner thread");
                AssertEqual(
                    drainCount + 1,
                    fixture.Host.ItemScrollDispatchDrainCountForTest,
                    "a worker burst drains through one UI callback");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture(
            string markup,
            IEnumerable rows)
        {
            Fixture fixture = new Fixture();
            fixture.Runtime = XamlRuntime.Load(markup);
            fixture.Host = fixture.Runtime.GetItemsControl("Rows");
            fixture.Host.CreateControl();
            fixture.Host.SetItems(rows);
            fixture.Host.PerformLayout();
            Application.DoEvents();
            return fixture;
        }

        private static void CompleteProgressiveRefresh(
            XamlRuntime.ItemsControl host)
        {
            int safety = 0;

            while (GetPendingRefresh(host) != null && safety < 100)
            {
                object state = GetPendingRefresh(host);
                FieldInfo timerField = state.GetType().GetField(
                    "Timer",
                    BindingFlags.Instance | BindingFlags.Public);

                AssertTrue(
                    timerField != null,
                    "progressive timer field is available");

                System.Windows.Forms.Timer timer =
                    timerField.GetValue(state) as System.Windows.Forms.Timer;
                AssertTrue(timer != null, "progressive timer is available");

                MethodInfo tickMethod =
                    typeof(System.Windows.Forms.Timer).GetMethod(
                    "OnTick",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                AssertTrue(
                    tickMethod != null,
                    "progressive timer tick is available");

                tickMethod.Invoke(timer, new object[] { EventArgs.Empty });
                safety++;
            }

            AssertTrue(
                GetPendingRefresh(host) == null,
                "progressive refresh completes within the bounded test loop");
        }

        private static object GetPendingRefresh(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo field = typeof(XamlRuntime.ItemsControl).GetField(
                "PendingRefresh",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(field != null, "pending refresh field is available");
            return field.GetValue(host);
        }

        private static Control FindRenderedControl(
            XamlRuntime.ItemsControl host,
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
                FieldInfo controlField = record.GetType().GetField(
                    "Control",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                if (indexField != null &&
                    controlField != null &&
                    (int)indexField.GetValue(record) == logicalIndex)
                {
                    Control control =
                        controlField.GetValue(record) as Control;

                    AssertTrue(
                        control != null && !control.IsDisposed,
                        "requested rendered control is alive");
                    return control;
                }
            }

            throw new InvalidOperationException(
                "The requested logical item is not realized.");
        }

        private static ArrayList CreateRows(
            int count,
            int hiddenIndex)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new Row(i, i != hiddenIndex));

            return rows;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(
            int expected,
            int actual,
            string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    message + ": expected " + expected +
                    ", actual " + actual + ".");
            }
        }
    }
}
