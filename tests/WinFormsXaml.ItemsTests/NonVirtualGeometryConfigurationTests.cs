using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    public delegate void GeometryMeasureCallback();

    public sealed class GeometryMeasureProbeLabel : Label
    {
        public static GeometryMeasureCallback NextMeasureCallback;
        public static int MeasureCount;

        public override Size GetPreferredSize(Size proposedSize)
        {
            MeasureCount++;
            GeometryMeasureCallback callback = NextMeasureCallback;
            NextMeasureCallback = null;

            if (callback != null)
                callback();

            return base.GetPreferredSize(proposedSize);
        }
    }

    internal static class NonVirtualGeometryConfigurationTests
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private sealed class GeometryRow : INotifyPropertyChanged
        {
            private readonly string _title;

            public readonly string Id;
            public readonly int Height;
            public int TitleReadCount;

            public GeometryRow(
                string id,
                string title,
                int height)
            {
                Id = id;
                _title = title;
                Height = height;
            }

            public string Title
            {
                get
                {
                    TitleReadCount++;
                    return _title;
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public void NotifyTitleChanged()
            {
                PropertyChangedEventHandler handler = PropertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs("Title"));
                }
            }
        }

        private sealed class GeometryState
        {
            public int FunctionCallCount;

            public string FormatTitle(string title)
            {
                FunctionCallCount++;
                return "formatted:" + title;
            }
        }

        private sealed class CountingEnumerable : IEnumerable
        {
            private readonly ArrayList _items;
            private readonly bool _throwAfterFirst;

            public int EnumerationCount;

            public CountingEnumerable(
                ArrayList items,
                bool throwAfterFirst)
            {
                _items = items;
                _throwAfterFirst = throwAfterFirst;
            }

            public IEnumerator GetEnumerator()
            {
                EnumerationCount++;

                if (_throwAfterFirst && EnumerationCount > 1)
                {
                    throw new InvalidOperationException(
                        "Stable geometry unexpectedly enumerated the source.");
                }

                return _items.GetEnumerator();
            }
        }

        internal static void RunAll()
        {
            TestStableGeometryChangesDoNotRefreshItems();
            TestGeometryFailureRollbackAndReentrantOwnership();
            TestAutoScrollFailureRestoresExtentAndOrigin();
            TestNonstableConfigurationsRetainReloadBehavior();
            TestCollapsedEmptyContentMeasuresOnceWithoutFallbackScan();
            TestReentrantCollapseUsesVisibilityFallbackScan();
            TestSettledLayoutSkipsRedundantFinalMeasurement();
            TestOriginMutationKeepsSecondRuntimeLayout();
            TestOrdinarySmallChangeIgnoresTallHeaderAndFixedItemSize();
            TestReentrantReplacementPublishesTheNewTree();
            TestSameListPublicationRevisionRetriesLayout();
            TestReentrantLayoutGeometryMutationRemeasuresRows();
            TestDirectViewportDisposalStopsLayoutPipeline();
            TestNativeGeometryMutationUsesANewMeasureEpoch();
            TestPureScrollDoesNotRemeasureRows();
            TestDisposedRootIsNotReused();
            TestMaximumSpacingSaturatesWithoutWrapping();
        }

#if NONVIRTUAL_GEOMETRY_STANDALONE
        [STAThread]
        private static int Main()
        {
            try
            {
                RunAll();
                Console.WriteLine(
                    "Nonvirtual geometry configuration tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
#endif

        private static void TestStableGeometryChangesDoNotRefreshItems()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='190' Height='96' " +
                "ItemKeyPath='Id' Virtualizing='false' " +
                "ProgressiveRendering='false' AutoScroll='true' " +
                "Orientation='Vertical' Spacing='2'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Width='40' Height='20' " +
                "      Text='{Binding Title}' " +
                "      Tag='{Function FormatTitle(Title)}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            GeometryState state = new GeometryState();
            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(8, 20);
                CountingEnumerable source =
                    new CountingEnumerable(rows, true);
                host.SetItems(source);
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);
                int titleReads = CountTitleReads(rows);
                int functionCalls = state.FunctionCallCount;
                long blueprintBuilds =
                    host.ItemTemplateBlueprintBuildCount;
                long fallbackBuilds =
                    host.ItemTemplateFallbackBuildCount;
                long disposals = host.ItemControlTreeDisposedCount;
                int subscriptions =
                    host.ActiveItemBindingSubscriptionCount;
                int completed = 0;
                int failed = 0;
                host.RefreshCompleted += delegate { completed++; };
                host.RefreshFailed += delegate { failed++; };

                AssertVerticalGap(host, 2, "initial vertical spacing");

                host.Spacing = 7;
                AssertVerticalGap(host, 7, "layout-only vertical spacing");

                host.Orientation = Orientation.Horizontal;
                AssertTrue(
                    controls[0].Left < controls[1].Left,
                    "LTR horizontal rows advance to the right");
                AssertEqual(
                    7,
                    controls[1].Left - controls[0].Right,
                    "horizontal spacing is applied without a refresh");

                host.AutoScroll = false;
                AssertTrue(!host.AutoScroll, "AutoScroll disables");
                host.AutoScroll = true;
                AssertTrue(host.AutoScroll, "AutoScroll re-enables");

                host.KeepScrollBarOnRight = false;
                host.RightToLeft = RightToLeft.Yes;
                host.PerformLayout();
                AssertTrue(
                    controls[0].Left > controls[1].Left,
                    "RTL horizontal rows advance to the left");

                AssertEqual(
                    1,
                    source.EnumerationCount,
                    "stable geometry enumerates the source only for initial render");
                AssertEqual(
                    titleReads,
                    CountTitleReads(rows),
                    "stable geometry does not reevaluate item bindings");
                AssertEqual(
                    functionCalls,
                    state.FunctionCallCount,
                    "stable geometry does not reevaluate Functions");
                AssertEqual(
                    blueprintBuilds,
                    host.ItemTemplateBlueprintBuildCount,
                    "stable geometry does not rebuild blueprint controls");
                AssertEqual(
                    fallbackBuilds,
                    host.ItemTemplateFallbackBuildCount,
                    "stable geometry does not use fallback builds");
                AssertEqual(
                    disposals,
                    host.ItemControlTreeDisposedCount,
                    "stable geometry does not dispose item controls");
                AssertEqual(
                    subscriptions,
                    host.ActiveItemBindingSubscriptionCount,
                    "stable geometry preserves binding subscriptions");
                AssertEqual(0, completed, "no refresh completion event");
                AssertEqual(0, failed, "no refresh failure event");
                AssertSameControls(
                    controls,
                    GetRenderedControls(host),
                    "stable geometry retains every item control");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestGeometryFailureRollbackAndReentrantOwnership()
        {
            XamlRuntime runtime = LoadSimpleGeometryHost(4, 20, false);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.Spacing = 3;
                bool throwLayout = false;
                int nestedSpacing = -1;
                LayoutEventHandler handler = delegate
                {
                    if (!throwLayout)
                        return;

                    throwLayout = false;

                    if (nestedSpacing >= 0)
                    {
                        int value = nestedSpacing;
                        nestedSpacing = -1;
                        host.Spacing = value;
                    }

                    throw new InvalidOperationException(
                        "Injected geometry layout failure.");
                };
                host.Layout += handler;

                throwLayout = true;
                AssertThrows(
                    delegate { host.Spacing = 9; },
                    "simple layout failure is surfaced");
                AssertEqual(3, host.Spacing, "failed spacing rolls back");
                AssertVerticalGap(host, 3, "rollback re-lays out old spacing");

                nestedSpacing = 13;
                throwLayout = true;
                AssertThrows(
                    delegate { host.Spacing = 7; },
                    "reentrant outer layout failure is surfaced");
                AssertEqual(
                    13,
                    host.Spacing,
                    "newer reentrant mutation is not overwritten by outer rollback");
                host.PerformLayout();
                AssertVerticalGap(
                    host,
                    13,
                    "newer reentrant spacing owns final geometry");

                host.Layout -= handler;
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestAutoScrollFailureRestoresExtentAndOrigin()
        {
            XamlRuntime runtime = LoadSimpleGeometryHost(20, 24, false);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetLogicalScrollOffset(80);
                Size previousExtent = host.AutoScrollMinSize;
                int previousOffset = host.GetLogicalScrollOffset();
                bool throwLayout = false;
                LayoutEventHandler handler = delegate
                {
                    if (!throwLayout)
                        return;

                    throwLayout = false;
                    throw new InvalidOperationException(
                        "Injected AutoScroll layout failure.");
                };
                host.Layout += handler;
                throwLayout = true;

                AssertThrows(
                    delegate { host.AutoScroll = false; },
                    "AutoScroll layout failure is surfaced");
                AssertTrue(host.AutoScroll, "AutoScroll property rolls back");
                AssertEqual(
                    previousExtent,
                    host.AutoScrollMinSize,
                    "AutoScroll extent rolls back");
                AssertEqual(
                    previousOffset,
                    host.GetLogicalScrollOffset(),
                    "AutoScroll logical origin rolls back");
                host.Layout -= handler;
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestNonstableConfigurationsRetainReloadBehavior()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='true' VirtualizationThreshold='1000' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                CountingEnumerable configuredSource =
                    new CountingEnumerable(CreateRows(4, 20), false);
                host.SetItems(configuredSource);
                AssertTrue(
                    !host.DirectVirtualActive,
                    "configured Controls virtualization remains below threshold");
                int beforeConfigured =
                    configuredSource.EnumerationCount;
                host.Spacing = 5;
                AssertTrue(
                    configuredSource.EnumerationCount > beforeConfigured,
                    "configured Virtualizing retains configuration reload behavior");
            }
            finally
            {
                DisposeRuntime(runtime);
            }

            XamlRuntime progressiveRuntime =
                LoadSimpleGeometryHost(1, 20, false);

            try
            {
                XamlRuntime.ItemsControl host =
                    progressiveRuntime.GetItemsControl("Rows");
                host.ProgressiveRendering = true;
                host.ProgressiveBatchSize = 1;
                host.ProgressiveInterval = 60000;
                EnsureHandle(progressiveRuntime.RootControl);
                CountingEnumerable progressiveSource =
                    new CountingEnumerable(CreateRows(5, 20), false);
                host.SetItems(progressiveSource);
                AssertTrue(
                    GetPendingRefresh(host) != null,
                    "progressive source has pending publication");
                int beforePending =
                    progressiveSource.EnumerationCount;
                host.Spacing = 6;
                AssertTrue(
                    progressiveSource.EnumerationCount > beforePending,
                    "pending progressive geometry retains restart/reload behavior");
                AssertTrue(
                    GetPendingRefresh(host) != null,
                    "configuration reload republishes progressive work");
            }
            finally
            {
                DisposeRuntime(progressiveRuntime);
            }
        }

        private static void
            TestCollapsedEmptyContentMeasuresOnceWithoutFallbackScan()
        {
            const int count = 37;
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Padding='0' Virtualizing='false' " +
                "ProgressiveRendering='false' AutoScroll='true'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Visibility='Collapsed' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(count, 20));
                host.AutoScrollMinSize = Size.Empty;
                host.ResetItemsLayoutScanDiagnosticsForTest();

                InvokeRuntimeItemsLayout(runtime, host);

                AssertEqual(
                    (long)count,
                    host.ItemsMeasureRecordProbeCountForTest,
                    "valid empty measurement is not repeated as a sentinel");
                AssertEqual(
                    0L,
                    host.ItemsVisibilityFallbackProbeCountForTest,
                    "stable collapsed state avoids HasLayoutItems scan");
                AssertEqual(
                    Size.Empty,
                    host.AutoScrollMinSize,
                    "all-collapsed rows publish an empty extent");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReentrantCollapseUsesVisibilityFallbackScan()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='true'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <GeometryMeasureProbeLabel Height='20' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(2, 20));
                Control first = GetRenderedControls(host)[0];
                ClearRenderedMeasureCaches(host);
                GeometryMeasureProbeLabel.NextMeasureCallback =
                    delegate
                    {
                        SetElementCollapsed(runtime, first, true);
                    };
                host.ResetItemsLayoutScanDiagnosticsForTest();

                InvokeRuntimeItemsLayout(runtime, host);

                AssertTrue(
                    host.ItemsVisibilityFallbackProbeCountForTest > 0L,
                    "reentrant collapsed-state change retains verification scan");
            }
            finally
            {
                GeometryMeasureProbeLabel.NextMeasureCallback = null;
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestSettledLayoutSkipsRedundantFinalMeasurement()
        {
            const int count = 9;
            XamlRuntime runtime = LoadSimpleGeometryHost(count, 20, true);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.Height = 400;
                host.PerformLayout();
                Rectangle[] bounds = CaptureBounds(host);
                Size extent = host.AutoScrollMinSize;
                Point origin = host.AutoScrollPosition;
                host.ResetItemsLayoutScanDiagnosticsForTest();

                host.PerformLayout();

                AssertEqual(
                    (long)count,
                    host.ItemsMeasureRecordProbeCountForTest,
                    "settled OnLayout skips redundant final runtime measurement");
                AssertEqual(
                    0L,
                    host.ItemsVisibilityFallbackProbeCountForTest,
                    "settled layout has no visibility fallback scan");
                AssertEqual(extent, host.AutoScrollMinSize, "extent is stable");
                AssertEqual(origin, host.AutoScrollPosition, "origin is stable");
                AssertBoundsEqual(bounds, CaptureBounds(host));
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestOriginMutationKeepsSecondRuntimeLayout()
        {
            const int count = 14;
            XamlRuntime runtime = LoadSimpleGeometryHost(count, 28, false);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetLogicalScrollOffset(Int32.MaxValue);
                int oldEnd = host.GetLogicalScrollOffset();
                AssertTrue(oldEnd > 1, "test starts at the old logical end");
                int layoutEventCount = 0;
                bool mutateOrigin = true;
                LayoutEventHandler handler = delegate
                {
                    if (!mutateOrigin)
                        return;

                    layoutEventCount++;

                    if (layoutEventCount == 2)
                    {
                        mutateOrigin = false;
                        host.AutoScrollPosition = new Point(
                            0,
                            Math.Max(0, oldEnd - 1));
                    }
                };
                host.Layout += handler;
                host.ResetItemsLayoutScanDiagnosticsForTest();

                host.PerformLayout();

                host.Layout -= handler;
                AssertTrue(
                    host.ItemsMeasureRecordProbeCountForTest >=
                        (long)count * 2L,
                    "base-layout origin clamp keeps a second runtime measurement");
                AssertEqual(
                    oldEnd - 1,
                    host.GetLogicalScrollOffset(),
                    "second pass preserves the newer clamped origin");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestOrdinarySmallChangeIgnoresTallHeaderAndFixedItemSize()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='90' " +
                "Virtualizing='true' VirtualizationThreshold='1000' " +
                "FixedItemSize='500' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='{Binding Height}' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                rows.Add(new GeometryRow("header", "Header", 300));
                rows.Add(new GeometryRow("row", "Row", 20));
                host.SetItems(rows);
                host.PerformLayout();

                int smallChange = InvokeSmallScrollChange(host);
                AssertEqual(
                    Math.Max(1, host.Font == null ? 1 : host.Font.Height),
                    smallChange,
                    "ordinary line scrolling remains native-like despite tall header and FixedItemSize");
                AssertTrue(
                    !host.DirectVirtualActive,
                    "configured virtualization remains ordinary below threshold");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReentrantReplacementPublishesTheNewTree()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <GeometryMeasureProbeLabel Height='20' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(7, 20));
                GeometryMeasureProbeLabel.NextMeasureCallback =
                    delegate
                    {
                        host.SetItems(CreateRows(2, 20));
                    };

                host.PerformLayout();

                AssertEqual(
                    2,
                    host.RenderedItems.Count,
                    "shorter reentrant replacement is the published tree");
                AssertTrue(
                    !GetRenderedControls(host)[0].IsDisposed,
                    "shorter replacement leaves live arranged controls");

                host.SetItems(CreateRows(6, 20));
                GeometryMeasureProbeLabel.NextMeasureCallback =
                    delegate
                    {
                        host.SetItems(CreateRows(6, 20));
                    };
                host.PerformLayout();
                AssertEqual(
                    6,
                    host.RenderedItems.Count,
                    "equal-count reentrant replacement is not mixed with old records");

                Control moving = GetRenderedControls(host)[1];
                bool replaceOnBounds = true;
                EventHandler moved = delegate
                {
                    if (!replaceOnBounds)
                        return;

                    replaceOnBounds = false;
                    host.SetItems(CreateRows(3, 20));
                };
                moving.LocationChanged += moved;
                host.Spacing = host.Spacing + 5;
                moving.LocationChanged -= moved;
                AssertEqual(
                    3,
                    host.RenderedItems.Count,
                    "SetBounds reentrancy arranges the replacement tree");
            }
            finally
            {
                GeometryMeasureProbeLabel.NextMeasureCallback = null;
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestSameListPublicationRevisionRetriesLayout()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='120' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <GeometryMeasureProbeLabel Height='20' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(3, 20));
                host.PerformLayout();
                ArrayList published = host.RenderedItems;
                object prototype = published[0];
                object appendedRecord = Activator.CreateInstance(
                    prototype.GetType(),
                    true);
                GeometryMeasureProbeLabel appended =
                    new GeometryMeasureProbeLabel();
                appended.Height = 20;
                appended.Text = "same-list append";
                SetRecordField(appendedRecord, "Control", appended);
                SetRecordField(appendedRecord, "IntendedVisible", true);
                SetRecordField(appendedRecord, "LogicalIndex", 3);
                ClearRenderedMeasureCaches(host);
                GeometryMeasureProbeLabel.NextMeasureCallback =
                    delegate
                    {
                        host.AppendPublishedRenderedItemRecord(
                            published,
                            appendedRecord);
                        host.Controls.Add(appended);
                    };

                host.PerformLayout();

                AssertTrue(
                    Object.ReferenceEquals(published, host.RenderedItems),
                    "progressive append keeps the published list identity");
                AssertEqual(
                    4,
                    host.RenderedItems.Count,
                    "same-list publication appends exactly one record");
                AssertTrue(
                    appended.Parent == host &&
                    appended.Bounds != Rectangle.Empty &&
                    appended.Top >= GetRenderedControls(host)[2].Bottom,
                    "same-list appended row is arranged by the retry pass");
            }
            finally
            {
                GeometryMeasureProbeLabel.NextMeasureCallback = null;
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestReentrantLayoutGeometryMutationRemeasuresRows()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='130' Height='70' " +
                "Orientation='Horizontal' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <GeometryMeasureProbeLabel AutoSize='true' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(3, 20));
                host.PerformLayout();
                Label first = GetRenderedControls(host)[0] as Label;
                int oldWidth = host.AutoScrollMinSize.Width;
                int layoutEvents = 0;
                bool mutated = false;
                LayoutEventHandler handler = delegate
                {
                    layoutEvents++;

                    if (layoutEvents == 2)
                    {
                        mutated = true;
                        first.Text =
                            "A reentrant Layout mutation that must be remeasured";
                        first.AutoSize = false;
                        first.Width = 300;
                        InvokeNestedItemsLayout(host, first, "Text");
                        GeometryMeasureProbeLabel.MeasureCount = 0;
                    }
                };
                host.Layout += handler;
                GeometryMeasureProbeLabel.MeasureCount = 0;

                host.PerformLayout();

                host.Layout -= handler;
                AssertTrue(mutated, "test reached the runtime-owned base layout");
                AssertTrue(
                    GeometryMeasureProbeLabel.MeasureCount >= 3,
                    "reentrant geometry dirtiness retries all ordinary rows");
                AssertTrue(
                    host.AutoScrollMinSize.Width > oldWidth,
                    "reentrant Text mutation updates the published extent " +
                    "(old=" + oldWidth + ", new=" +
                    host.AutoScrollMinSize.Width + ")");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestDirectViewportDisposalStopsLayoutPipeline()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='150' Height='70' " +
                "Orientation='Vertical' Virtualizing='true' " +
                "VirtualizationThreshold='1' FixedItemSize='0' " +
                "EstimatedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <GeometryMeasureProbeLabel AutoSize='true' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);
            bool disposedInCallback = false;

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(20, 20));
                host.PerformLayout();
                AssertTrue(
                    host.DirectVirtualActive,
                    "disposal fixture activates direct virtualization");
                ClearRenderedMeasureCaches(host);
                GeometryMeasureProbeLabel.NextMeasureCallback =
                    delegate
                    {
                        disposedInCallback = true;
                        runtime.Dispose();
                    };

                host.PerformLayout();

                AssertTrue(
                    disposedInCallback && runtime.IsDisposed,
                    "direct viewport callback can dispose without stale reconciliation");
            }
            finally
            {
                GeometryMeasureProbeLabel.NextMeasureCallback = null;
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestNativeGeometryMutationUsesANewMeasureEpoch()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='130' Height='70' " +
                "Orientation='Horizontal' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <GeometryMeasureProbeLabel AutoSize='true' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);
            Font enlarged = null;

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(1, 20));
                host.PerformLayout();
                Label label = GetRenderedControls(host)[0] as Label;
                int originalWidth = host.AutoScrollMinSize.Width;
                GeometryMeasureProbeLabel.MeasureCount = 0;
                label.Text =
                    "A much longer native label value that must be remeasured";
                enlarged = new Font(
                    label.Font.FontFamily,
                    label.Font.Size + 4.0f);
                label.Font = enlarged;
                label.AutoSize = true;
                host.PerformLayout();

                AssertTrue(
                    GeometryMeasureProbeLabel.MeasureCount > 0,
                    "a later native layout uses a new measurement epoch");
                AssertTrue(
                    host.AutoScrollMinSize.Width > originalWidth,
                    "native Text/Font/AutoSize mutation updates the extent");
            }
            finally
            {
                DisposeRuntime(runtime);

                if (enlarged != null)
                    enlarged.Dispose();
            }
        }

        private static void TestPureScrollDoesNotRemeasureRows()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='130' Height='70' " +
                "Orientation='Horizontal' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <GeometryMeasureProbeLabel Width='30' Height='20' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(30, 20));
                host.PerformLayout();
                GeometryMeasureProbeLabel.MeasureCount = 0;
                host.SetLogicalScrollOffset(20);
                host.SetLogicalScrollOffset(40);
                host.SmoothScroll = true;
                host.ScrollBy(ScrollEventType.SmallIncrement);
                host.ApplySmoothScrollFrameForTest(60);
                host.StopSmoothScrollAnimation();

                AssertEqual(
                    0,
                    GeometryMeasureProbeLabel.MeasureCount,
                    "immediate and smooth pure scrolling do not remeasure rows");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDisposedRootIsNotReused()
        {
            XamlRuntime runtime = LoadSimpleGeometryHost(4, 20, false);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                Control disposed = GetRenderedControls(host)[0];
                disposed.Dispose();
                host.ForceReloadItems();
                Control replacement = GetRenderedControls(host)[0];

                AssertTrue(
                    !replacement.IsDisposed &&
                    !Object.ReferenceEquals(disposed, replacement),
                    "a disposed old root is rebuilt instead of reused");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestMaximumSpacingSaturatesWithoutWrapping()
        {
            XamlRuntime runtime = LoadSimpleGeometryHost(3, 20, false);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.Orientation = Orientation.Horizontal;
                host.Spacing = Int32.MaxValue;
                host.PerformLayout();

                AssertEqual(
                    Int32.MaxValue,
                    host.AutoScrollMinSize.Width,
                    "maximum spacing saturates the measured extent");
                AssertTrue(
                    host.AutoScrollMinSize.Width >= 0,
                    "maximum spacing never wraps the range negative");

                Control[] controls = GetRenderedControls(host);
                int previousX = Int32.MinValue;
                int i;

                for (i = 0; i < controls.Length; i++)
                {
                    Control control = controls[i];

                    AssertTrue(
                        control != null && control.Left >= previousX,
                        "maximum spacing keeps LTR row coordinates ordered");
                    previousX = control.Left;
                }
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static XamlRuntime LoadSimpleGeometryHost(
            int count,
            int height,
            bool fitViewport)
        {
            string markup =
                "<ItemsControl Name='Rows' Width='190' Height='" +
                (fitViewport ? "400" : "96") +
                "' ItemKeyPath='Id' Virtualizing='false' " +
                "ProgressiveRendering='false' AutoScroll='true' " +
                "Orientation='Vertical' Spacing='2'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='{Binding Height}' " +
                "      Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);
            XamlRuntime.ItemsControl host =
                runtime.GetItemsControl("Rows");
            host.SetItems(CreateRows(count, height));
            host.CreateControl();
            host.PerformLayout();
            return runtime;
        }

        private static ArrayList CreateRows(int count, int height)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
            {
                rows.Add(
                    new GeometryRow(
                        "geometry-" + i,
                        "Geometry " + i,
                        height));
            }

            return rows;
        }

        private static int CountTitleReads(ArrayList rows)
        {
            int count = 0;
            int i;

            for (i = 0; i < rows.Count; i++)
                count += ((GeometryRow)rows[i]).TitleReadCount;

            return count;
        }

        private static Control[] GetRenderedControls(
            XamlRuntime.ItemsControl host)
        {
            ArrayList records = host.RenderedItems;
            Control[] controls = new Control[records.Count];
            int i;

            for (i = 0; i < records.Count; i++)
            {
                FieldInfo field = records[i].GetType().GetField(
                    "Control",
                    InstanceMembers);
                controls[i] = field.GetValue(records[i]) as Control;
            }

            return controls;
        }

        private static void AssertSameControls(
            Control[] expected,
            Control[] actual,
            string message)
        {
            AssertEqual(expected.Length, actual.Length, message + " count");
            int i;

            for (i = 0; i < expected.Length; i++)
            {
                AssertTrue(
                    Object.ReferenceEquals(expected[i], actual[i]),
                    message + " at " + i);
            }
        }

        private static void AssertVerticalGap(
            XamlRuntime.ItemsControl host,
            int expected,
            string message)
        {
            Control[] controls = GetRenderedControls(host);
            AssertTrue(controls.Length >= 2, message + " has two rows");
            AssertEqual(
                expected,
                controls[1].Top - controls[0].Bottom,
                message);
        }

        private static Rectangle[] CaptureBounds(
            XamlRuntime.ItemsControl host)
        {
            Control[] controls = GetRenderedControls(host);
            Rectangle[] result = new Rectangle[controls.Length];
            int i;

            for (i = 0; i < controls.Length; i++)
                result[i] = controls[i].Bounds;

            return result;
        }

        private static void AssertBoundsEqual(
            Rectangle[] expected,
            Rectangle[] actual)
        {
            AssertEqual(expected.Length, actual.Length, "bounds count");
            int i;

            for (i = 0; i < expected.Length; i++)
            {
                AssertEqual(
                    expected[i],
                    actual[i],
                    "stable bounds at " + i);
            }
        }

        private static void ClearRenderedMeasureCaches(
            XamlRuntime.ItemsControl host)
        {
            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                object record = host.RenderedItems[i];
                FieldInfo field = record.GetType().GetField(
                    "MeasureCacheValid",
                    InstanceMembers);
                field.SetValue(record, false);
            }
        }

        private static void SetRecordField(
            object record,
            string name,
            object value)
        {
            FieldInfo field = record.GetType().GetField(
                name,
                InstanceMembers);

            AssertTrue(field != null, "rendered record field " + name);
            field.SetValue(record, value);
        }

        private static void SetElementCollapsed(
            XamlRuntime runtime,
            Control control,
            bool collapsed)
        {
            MethodInfo getInfo = typeof(XamlRuntime).GetMethod(
                "GetInfo",
                InstanceMembers);
            object info = getInfo.Invoke(
                runtime,
                new object[] { control });
            MethodInfo setVisibility = typeof(XamlRuntime).GetMethod(
                "SetElementVisibilityState",
                InstanceMembers);
            setVisibility.Invoke(
                runtime,
                new object[] { info, false, collapsed });
        }

        private static void InvokeRuntimeItemsLayout(
            XamlRuntime runtime,
            XamlRuntime.ItemsControl host)
        {
            MethodInfo method = typeof(XamlRuntime).GetMethod(
                "LayoutItemsControl",
                InstanceMembers);
            method.Invoke(runtime, new object[] { host });
        }

        private static void InvokeNestedItemsLayout(
            XamlRuntime.ItemsControl host,
            Control affectedControl,
            string affectedProperty)
        {
            MethodInfo method = typeof(XamlRuntime.ItemsControl).GetMethod(
                "OnLayout",
                InstanceMembers);
            method.Invoke(
                host,
                new object[]
                {
                    new LayoutEventArgs(
                        affectedControl,
                        affectedProperty)
                });
        }

        private static int InvokeSmallScrollChange(
            XamlRuntime.ItemsControl host)
        {
            MethodInfo method = typeof(XamlRuntime.ItemsControl).GetMethod(
                "GetSmallScrollChange",
                InstanceMembers);
            return (int)method.Invoke(host, null);
        }

        private static object GetPendingRefresh(
            XamlRuntime.ItemsControl host)
        {
            return host.PendingRefresh;
        }

        private static void EnsureHandle(Control control)
        {
            AssertTrue(control != null, "runtime root exists");

            if (!control.IsHandleCreated)
                control.CreateControl();

            AssertTrue(control.IsHandleCreated, "runtime root handle exists");
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

        private static void AssertThrows(
            MethodInvoker action,
            string message)
        {
            bool threw = false;

            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            AssertTrue(threw, message);
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
