using System;
using System.Collections;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class NonVirtualItemsOptimizationTests
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private sealed class OptimizationRow
        {
            public string Id;
            public string Text;

            public OptimizationRow(string id, string text)
            {
                Id = id;
                Text = text;
            }
        }

        public static void RunAll()
        {
            TestInitialCommitRunsOneItemLayout();
            TestTailAppendRunsOneItemLayout();
            TestAutoScrollingTailAppendKeepsRangeAndOrigin();
            TestFastImmediateAndSmoothScrollRetainNativeTree();
            TestLargeInitialCommitAndTailAppendStayOrdered();
            TestEqualControlsRetainReferenceOwnership();
            TestScrollExtentMarkerIsLazy();
            TestProgressiveInitialBatchRunsBeforeTimer();
            TestProgressiveTimerContinuesWithReusableBudgetClock();
        }

        private static void TestScrollExtentMarkerIsLazy()
        {
            XamlRuntime runtime = LoadLayoutProbeHost();

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                FieldInfo markerField =
                    typeof(XamlRuntime.ItemsControl).GetField(
                        "_scrollExtentMarker",
                        InstanceMembers);

                AssertTrue(markerField != null, "extent marker field exists");
                AssertEqual(
                    null,
                    markerField.GetValue(host),
                    "empty ItemsControl allocates no extent marker");

                host.SetItems(CreateRows(2, "marker"));

                AssertEqual(
                    null,
                    markerField.GetValue(host),
                    "a non-scrolling host never allocates an extent marker");

                host.AutoScroll = true;
                host.PerformLayout();

                Control marker = markerField.GetValue(host) as Control;
                AssertTrue(
                    marker != null &&
                    Object.ReferenceEquals(marker.Parent, host),
                    "the first nonempty scrolling layout creates the extent marker");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestInitialCommitRunsOneItemLayout()
        {
            const int count = 48;
            XamlRuntime runtime = LoadLayoutProbeHost();

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.ResetItemsLayoutScanDiagnosticsForTest();

                host.SetItems(CreateRows(count, "initial-layout"));

                AssertEqual(
                    (long)count,
                    host.ItemsMeasureRecordProbeCountForTest,
                    "initial commit measures the published rows once");
                AssertEqual(
                    count,
                    host.RealizedCount,
                    "initial layout commit realizes every row");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestTailAppendRunsOneItemLayout()
        {
            const int initialCount = 48;
            XamlRuntime runtime = LoadLayoutProbeHost();

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(
                    initialCount,
                    "append-layout");
                host.SetItems(rows);
                host.ResetItemsLayoutScanDiagnosticsForTest();

                rows.Add(
                    new OptimizationRow(
                        "append-layout-tail",
                        "Tail"));
                host.SetItems(rows);

                AssertEqual(
                    (long)(initialCount + 1),
                    host.ItemsMeasureRecordProbeCountForTest,
                    "append-only commit measures the published rows once");
                AssertEqual(
                    initialCount + 1,
                    host.RealizedCount,
                    "append-only layout commit realizes the new tail");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static XamlRuntime LoadLayoutProbeHost()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='false' Width='320' Height='240'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='120' Height='20' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            return XamlRuntime.Load(markup);
        }

        private static void TestAutoScrollingTailAppendKeepsRangeAndOrigin()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='true' Width='240' Height='100'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='160' Height='20' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(20, "range");
                host.SetItems(rows);
                EnsureHandle(runtime.RootControl);
                host.PerformLayout();
                host.SetLogicalScrollOffset(80);

                int previousOffset = host.GetLogicalScrollOffset();
                int previousExtent = host.AutoScrollMinSize.Height;
                Control first = host.Controls[0];
                rows.Add(new OptimizationRow("range-tail", "Tail"));

                host.SetItems(rows);

                AssertTrue(
                    host.AutoScrollMinSize.Height > previousExtent,
                    "append-only scrolling commit grows the native range");
                AssertEqual(
                    previousOffset,
                    host.GetLogicalScrollOffset(),
                    "append-only scrolling commit preserves the logical origin");
                AssertSame(
                    first,
                    host.Controls[0],
                    "append-only scrolling commit retains existing controls");
                AssertSame(
                    rows[rows.Count - 1],
                    host.Controls[rows.Count - 1].Tag,
                    "append-only scrolling commit arranges the new tail");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestFastImmediateAndSmoothScrollRetainNativeTree()
        {
            const int count = 96;
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='true' Width='240' Height='100'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='160' Height='20' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(count, "scroll"));
                EnsureHandle(runtime.RootControl);
                host.PerformLayout();

                Control[] controls = new Control[count];
                int i;

                for (i = 0; i < count; i++)
                    controls[i] = host.Controls[i];

                long blueprintBuilds =
                    host.ItemTemplateBlueprintBuildCount;
                long fallbackBuilds =
                    host.ItemTemplateFallbackBuildCount;
                long disposedTrees =
                    host.ItemControlTreeDisposedCount;
                int subscriptions =
                    host.ActiveItemBindingSubscriptionCount;
                host.ResetItemsLayoutScanDiagnosticsForTest();

                for (i = 0; i < 160; i++)
                {
                    int requested = (i % 4) < 2
                        ? i * 17
                        : Int32.MaxValue - (i * 17);
                    host.SetLogicalScrollOffset(requested);
                }

                host.ScrollToStart();
                host.SmoothScroll = true;

                for (i = 0; i < 40; i++)
                {
                    host.ScrollBy(
                        (i % 2) == 0
                            ? ScrollEventType.LargeIncrement
                            : ScrollEventType.SmallIncrement);
                    host.ApplySmoothScrollFrameForTest(30);
                    host.ApplySmoothScrollFrameForTest(60);
                    host.ApplySmoothScrollFrameForTest(120);
                }

                host.StopSmoothScrollAnimation();

                AssertEqual(
                    0L,
                    host.ItemsMeasureRecordProbeCountForTest,
                    "fast immediate and smooth scrolling skips item measurement");
                AssertEqual(
                    blueprintBuilds,
                    host.ItemTemplateBlueprintBuildCount,
                    "scrolling does not construct blueprint rows");
                AssertEqual(
                    fallbackBuilds,
                    host.ItemTemplateFallbackBuildCount,
                    "scrolling does not construct fallback rows");
                AssertEqual(
                    disposedTrees,
                    host.ItemControlTreeDisposedCount,
                    "scrolling does not retire native rows");
                AssertEqual(
                    subscriptions,
                    host.ActiveItemBindingSubscriptionCount,
                    "scrolling retains item binding subscriptions");

                for (i = 0; i < count; i++)
                {
                    AssertSame(
                        controls[i],
                        host.Controls[i],
                        "scrolling retains native control at index " + i);
                }
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestLargeInitialCommitAndTailAppendStayOrdered()
        {
            const int initialCount = 1000;
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='false' Width='320' Height='240'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(initialCount, "initial");

                host.SetItems(rows);

                AssertEqual(
                    initialCount,
                    host.RealizedCount,
                    "large initial realized count");
                AssertEqual(
                    (long)initialCount,
                    host.ItemTemplateBlueprintBuildCount,
                    "eligible rows use the compiled blueprint");
                AssertEqual(
                    0L,
                    host.ItemTemplateFallbackBuildCount,
                    "eligible rows avoid the fallback renderer");
                AssertHostControlOrder(host, rows);
                AssertExtentMarkerIsAbsent(host);

                Control[] originalControls =
                    new Control[initialCount];
                int i;

                for (i = 0; i < initialCount; i++)
                    originalControls[i] = host.Controls[i];

                OptimizationRow tail = new OptimizationRow(
                    "tail",
                    "Tail");
                rows.Add(tail);
                host.SetItems(rows);

                AssertEqual(
                    initialCount + 1,
                    host.RealizedCount,
                    "tail append realized count");
                AssertEqual(
                    (long)(initialCount + 1),
                    host.ItemTemplateBlueprintBuildCount,
                    "tail append builds only its new row");
                AssertEqual(
                    0L,
                    host.ItemTemplateFallbackBuildCount,
                    "tail append remains on the blueprint path");

                for (i = 0; i < initialCount; i++)
                {
                    AssertSame(
                        originalControls[i],
                        host.Controls[i],
                        "tail append retains control at index " + i);
                }

                AssertHostControlOrder(host, rows);
                AssertExtentMarkerIsAbsent(host);
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestEqualControlsRetainReferenceOwnership()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <EqualLifecycleControl />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                OptimizationRow first =
                    new OptimizationRow("first", "First");
                OptimizationRow second =
                    new OptimizationRow("second", "Second");
                OptimizationRow third =
                    new OptimizationRow("third", "Third");
                ArrayList original = new ArrayList();
                original.Add(first);
                original.Add(second);
                original.Add(third);
                host.SetItems(original);

                EqualLifecycleControl firstControl =
                    FindEqualControl(host, first);
                EqualLifecycleControl secondControl =
                    FindEqualControl(host, second);
                EqualLifecycleControl thirdControl =
                    FindEqualControl(host, third);

                AssertNotSame(
                    firstControl,
                    secondControl,
                    "equal controls start as distinct references");
                AssertNotSame(
                    secondControl,
                    thirdControl,
                    "all equal controls start as distinct references");

                ArrayList reordered = new ArrayList();
                reordered.Add(third);
                reordered.Add(first);
                reordered.Add(second);
                host.SetItems(reordered);

                AssertSame(
                    firstControl,
                    FindEqualControl(host, first),
                    "first equal control retains its item ownership");
                AssertSame(
                    secondControl,
                    FindEqualControl(host, second),
                    "second equal control retains its item ownership");
                AssertSame(
                    thirdControl,
                    FindEqualControl(host, third),
                    "third equal control retains its item ownership");
                AssertRenderedRecordOrder(
                    host,
                    reordered,
                    new Control[]
                    {
                        thirdControl,
                        firstControl,
                        secondControl
                    });
                AssertExtentMarkerIsAbsent(host);
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestProgressiveInitialBatchRunsBeforeTimer()
        {
            XamlRuntime runtime = LoadProgressiveHost();

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                EnsureHandle(runtime.RootControl);
                host.ProgressiveRendering = true;
                host.ProgressiveBatchSize = 1;
                host.ProgressiveInterval = 60000;

                ArrayList rows = CreateRows(3, "progressive");
                host.SetItems(rows);

                AssertEqual(
                    1L,
                    host.ItemTemplateBlueprintBuildCount,
                    "the first bounded batch runs before SetItems returns");
                AssertEqual(
                    0,
                    GetRenderedItems(host).Count,
                    "the immediate incomplete batch stays detached");
                object state = GetPendingRefresh(host);
                AssertTrue(
                    state != null,
                    "the remaining work stays in a pending refresh");
                AssertTrue(
                    GetRefreshStateField(state, "Timer") is Timer,
                    "the continuation timer starts after the immediate batch");
                AssertTrue(
                    GetRefreshStateField(state, "ProgressiveBudget") != null,
                    "the progressive refresh owns one reusable budget clock");
                AssertEqual(
                    1L,
                    host.ProgressiveBatchCount,
                    "the immediate work is recorded as a progressive batch");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestProgressiveTimerContinuesWithReusableBudgetClock()
        {
            XamlRuntime runtime = LoadProgressiveHost();

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                EnsureHandle(runtime.RootControl);
                host.ProgressiveRendering = true;
                host.ProgressiveBatchSize = 1;
                host.ProgressiveInterval = 60000;

                ArrayList rows = CreateRows(3, "continuation");
                host.SetItems(rows);

                object state = GetPendingRefresh(host);
                object timer = GetRefreshStateField(state, "Timer");
                object budget = GetRefreshStateField(
                    state,
                    "ProgressiveBudget");

                AdvanceProgressiveTimer(host);

                AssertSame(
                    state,
                    GetPendingRefresh(host),
                    "the first timer continuation retains the refresh state");
                AssertSame(
                    timer,
                    GetRefreshStateField(state, "Timer"),
                    "the timer continuation reuses the same timer");
                AssertSame(
                    budget,
                    GetRefreshStateField(state, "ProgressiveBudget"),
                    "the timer continuation reuses the same budget clock");
                AssertEqual(
                    2L,
                    host.ItemTemplateBlueprintBuildCount,
                    "the timer continues with the next bounded batch");
                AssertEqual(
                    0,
                    GetRenderedItems(host).Count,
                    "the incomplete continuation remains atomic");

                AdvanceProgressiveTimer(host);

                ArrayList committed = GetRenderedItems(host);
                AssertEqual(
                    3,
                    committed.Count,
                    "the completed progressive refresh commits every row");
                AssertEqual(
                    3L,
                    host.ItemTemplateBlueprintBuildCount,
                    "progressive rows use the compiled blueprint");
                AssertEqual(
                    0L,
                    host.ItemTemplateFallbackBuildCount,
                    "progressive rows avoid fallback construction");
                AssertHostControlOrder(host, rows);
                AssertExtentMarkerIsAbsent(host);
                AssertTrue(
                    GetPendingRefresh(host) == null,
                    "progressive refresh is complete after the final batch");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static XamlRuntime LoadProgressiveHost()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' AutoScroll='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            return XamlRuntime.Load(markup);
        }

        private static ArrayList CreateRows(int count, string prefix)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
            {
                rows.Add(
                    new OptimizationRow(
                        prefix + "-" + i,
                        prefix + " " + i));
            }

            return rows;
        }

        private static void AssertHostControlOrder(
            XamlRuntime.ItemsControl host,
            ArrayList rows)
        {
            AssertTrue(
                host.Controls.Count >= rows.Count,
                "host contains every item control");

            int i;

            for (i = 0; i < rows.Count; i++)
            {
                AssertSame(
                    rows[i],
                    host.Controls[i].Tag,
                    "host control order at index " + i);
            }

            AssertRenderedRecordOrder(host, rows, null);
        }

        private static void AssertRenderedRecordOrder(
            XamlRuntime.ItemsControl host,
            ArrayList rows,
            Control[] expectedControls)
        {
            ArrayList records = GetRenderedItems(host);
            AssertEqual(
                rows.Count,
                records.Count,
                "rendered record count");

            int i;

            for (i = 0; i < rows.Count; i++)
            {
                object record = records[i];
                AssertSame(
                    rows[i],
                    GetRecordField(record, "Item"),
                    "rendered item order at index " + i);

                if (expectedControls != null)
                {
                    AssertSame(
                        expectedControls[i],
                        GetRenderedRecordControl(record),
                        "rendered control order at index " + i);
                }
            }
        }

        private static void AssertExtentMarkerIsAbsent(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo markerField =
                typeof(XamlRuntime.ItemsControl).GetField(
                    "_scrollExtentMarker",
                    InstanceMembers);

            AssertTrue(markerField != null, "extent marker field is available");
            AssertEqual(
                null,
                markerField.GetValue(host),
                "a non-scrolling retained tree allocates no extent marker");
        }

        private static void AssertExtentMarkerIsLast(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo markerField =
                typeof(XamlRuntime.ItemsControl).GetField(
                    "_scrollExtentMarker",
                    InstanceMembers);

            AssertTrue(markerField != null, "extent marker field is available");
            Control marker = markerField.GetValue(host) as Control;
            AssertTrue(marker != null, "extent marker exists");
            AssertSame(
                marker,
                host.Controls[host.Controls.Count - 1],
                "extent marker remains behind every item control");
        }

        private static EqualLifecycleControl FindEqualControl(
            XamlRuntime.ItemsControl host,
            object item)
        {
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                EqualLifecycleControl control =
                    host.Controls[i] as EqualLifecycleControl;

                if (control != null &&
                    Object.ReferenceEquals(control.Tag, item))
                {
                    return control;
                }
            }

            throw new InvalidOperationException(
                "No equal custom control was found for the requested item.");
        }

        private static ArrayList GetRenderedItems(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo field = typeof(XamlRuntime.ItemsControl).GetField(
                "RenderedItems",
                InstanceMembers);

            AssertTrue(field != null, "rendered items field is available");
            ArrayList records = field.GetValue(host) as ArrayList;
            AssertTrue(records != null, "rendered items are available");
            return records;
        }

        private static Control GetRenderedRecordControl(object record)
        {
            return GetRecordField(record, "Control") as Control;
        }

        private static object GetRecordField(
            object record,
            string name)
        {
            AssertTrue(record != null, "rendered record exists");
            FieldInfo field = record.GetType().GetField(
                name,
                InstanceMembers);

            AssertTrue(field != null, "rendered record field: " + name);
            return field.GetValue(record);
        }

        private static void AdvanceProgressiveTimer(
            XamlRuntime.ItemsControl host)
        {
            object state = GetPendingRefresh(host);
            AssertTrue(state != null, "pending progressive refresh exists");
            FieldInfo timerField = state.GetType().GetField(
                "Timer",
                InstanceMembers);
            AssertTrue(timerField != null, "progressive timer field exists");

            Timer timer = timerField.GetValue(state) as Timer;
            AssertTrue(timer != null, "progressive timer exists");
            MethodInfo tickMethod = typeof(Timer).GetMethod(
                "OnTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(tickMethod != null, "progressive timer tick exists");
            tickMethod.Invoke(timer, new object[] { EventArgs.Empty });
        }

        private static object GetPendingRefresh(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo field = typeof(XamlRuntime.ItemsControl).GetField(
                "PendingRefresh",
                InstanceMembers);

            AssertTrue(field != null, "pending refresh field is available");
            return field.GetValue(host);
        }

        private static object GetRefreshStateField(
            object state,
            string name)
        {
            AssertTrue(state != null, "pending refresh state is available");
            FieldInfo field = state.GetType().GetField(
                name,
                InstanceMembers);

            AssertTrue(field != null, "refresh state field is available: " + name);
            return field.GetValue(state);
        }

        private static void EnsureHandle(Control control)
        {
            AssertTrue(control != null, "runtime root exists");

            if (!control.IsHandleCreated)
                control.CreateControl();

            AssertTrue(control.IsHandleCreated, "runtime root handle is created");
        }

        private static void DisposeRuntime(XamlRuntime runtime)
        {
            if (runtime == null)
                return;

            Control root = runtime.RootControl;

            if (root != null && !root.IsDisposed)
                root.Dispose();

            runtime.Dispose();
        }

        private static void AssertTrue(bool condition, string message)
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
                    message +
                    ": expected '" +
                    expected +
                    "', actual '" +
                    actual +
                    "'.");
            }
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ": expected the same instance.");
            }
        }

        private static void AssertNotSame(
            object first,
            object second,
            string message)
        {
            if (Object.ReferenceEquals(first, second))
            {
                throw new InvalidOperationException(
                    message + ": expected distinct instances.");
            }
        }
    }
}
