using System;
using System.Collections;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    public sealed class RecyclingAuditRow
    {
        public string Id;
        public readonly PropertyBinding<string> Text;

        public RecyclingAuditRow(string id, string text)
        {
            Id = id;
            Text = new PropertyBinding<string>(text);
        }
    }

    public sealed class RecyclingAuditControl : Panel,
        IRecyclableItemControl
    {
        public int ResetCount;
        public int DisposeCount;
        public bool DeclineNextReset;
        public bool ThrowOnNextReset;
        public ItemRecycleContext LastContext;

        private bool _disposed;

        public bool TryPrepareForRecycle(ItemRecycleContext context)
        {
            ResetCount++;
            LastContext = context;

            Control editor = Controls["Editor"];

            if (editor != null)
                editor.Text = "transient-reset";

            if (ThrowOnNextReset)
            {
                ThrowOnNextReset = false;
                throw new InvalidOperationException(
                    "recycling-reset-failure");
            }

            if (DeclineNextReset)
            {
                DeclineNextReset = false;
                return false;
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }

    internal static class CrossItemControlRecyclingTests
    {
        private sealed class Fixture : IDisposable
        {
            public XamlRuntime Runtime;
            public XamlRuntime.ItemsControl Host;
            public ArrayList Rows;

            public void Dispose()
            {
                if (Runtime != null)
                    Runtime.Dispose();
            }
        }

        public static void RunAll()
        {
            TestNativeVirtualCacheIsAllocatedLazily();
            TestDisabledModeKeepsIdentityCacheSemantics();
            TestExplicitModeNeverInfersArbitraryTreeSafety();
            TestExplicitModePatchesAndReactivatesSlots();
            TestDeclineDisposesCandidateAndBuildsFreshTree();
            TestResetExceptionIsVisibleAndRollsBackRange();
        }

        private static void TestNativeVirtualCacheIsAllocatedLazily()
        {
            using (XamlRuntime.ItemsControl unused =
                new XamlRuntime.ItemsControl())
            {
                AssertTrue(
                    GetField(unused, "DirectVirtualCacheRecords") == null,
                    "An unused ItemsControl does not allocate a native-row cache");
            }

            using (Fixture fixture = CreateFixture(false))
            {
                RealizeRange(fixture, 0, 0);

                AssertTrue(
                    GetField(
                        fixture.Host,
                        "DirectVirtualCacheRecords") is ArrayList,
                    "The controls viewport creates its cache on first realization");
            }
        }

        private static void TestExplicitModeNeverInfersArbitraryTreeSafety()
        {
            using (Fixture fixture = CreateNonParticipantFixture())
            {
                RealizeRange(fixture, 0, 0);
                Control first = FindControlForRow(
                    fixture.Host,
                    fixture.Rows[0]);
                RealizeRange(fixture, 1, 1);
                RealizeRange(fixture, 2, 2);
                Control third = FindControlForRow(
                    fixture.Host,
                    fixture.Rows[2]);

                AssertTrue(
                    first != null && third != null &&
                    !Object.ReferenceEquals(first, third),
                    "Explicit mode never infers arbitrary tree safety");
                AssertEqual(
                    0L,
                    fixture.Host.VirtualCrossItemRecycleCount,
                    "non-participating roots are not cross-item reuse");
                AssertEqual(
                    0L,
                    fixture.Host.VirtualCrossItemRecycleRejectedCount,
                    "non-participating roots are skipped, not reset rejects");
            }
        }

        private static void TestDisabledModeKeepsIdentityCacheSemantics()
        {
            using (Fixture fixture = CreateFixture(false))
            {
                RecyclingAuditControl first = RealizeFirstThenCache(
                    fixture);
                RealizeRange(fixture, 2, 2);

                RecyclingAuditControl third = FindRootForRow(
                    fixture.Host,
                    fixture.Rows[2]);

                AssertTrue(
                    third != null && !Object.ReferenceEquals(first, third),
                    "Disabled mode constructs a different-item row");
                AssertEqual(
                    0,
                    first.ResetCount,
                    "Disabled mode never invokes the opt-in contract");
                AssertEqual(
                    0L,
                    fixture.Host.VirtualCrossItemRecycleCount,
                    "Disabled mode reports no cross-item reuse");
            }
        }

        private static void TestExplicitModePatchesAndReactivatesSlots()
        {
            using (Fixture fixture = CreateFixture(true))
            {
                RecyclingAuditControl first = RealizeFirstThenCache(
                    fixture);
                RecyclingAuditRow oldRow =
                    (RecyclingAuditRow)fixture.Rows[0];
                RecyclingAuditRow newRow =
                    (RecyclingAuditRow)fixture.Rows[2];

                RealizeRange(fixture, 2, 2);

                RecyclingAuditControl recycled = FindRootForRow(
                    fixture.Host,
                    newRow);
                TextBox editor = recycled == null
                    ? null
                    : recycled.Controls["Editor"] as TextBox;

                AssertTrue(
                    Object.ReferenceEquals(first, recycled),
                    "Explicit mode publishes the accepted cached root");
                AssertEqual(
                    "Third",
                    editor == null ? null : editor.Text,
                    "compiled binding slots overwrite transient reset state");
                AssertEqual(
                    "First",
                    oldRow.Text.Value,
                    "reset runs after the old TwoWay subscription is detached");
                AssertTrue(
                    first.LastContext != null &&
                    Object.ReferenceEquals(
                        oldRow,
                        first.LastContext.OldItem) &&
                    Object.ReferenceEquals(
                        newRow,
                        first.LastContext.NewItem) &&
                    first.LastContext.OldIndex == 0 &&
                    first.LastContext.NewIndex == 2,
                    "immutable reset context identifies both logical rows");

                oldRow.Text.Value = "stale-old-change";
                AssertEqual(
                    "Third",
                    editor.Text,
                    "old item subscription remains detached");

                newRow.Text.Value = "live-new-change";
                AssertEqual(
                    "live-new-change",
                    editor.Text,
                    "new item subscription is active");

                editor.Text = "two-way-change";
                AssertEqual(
                    "two-way-change",
                    newRow.Text.Value,
                    "recycled TwoWay slot writes to the new source");
                AssertEqual(
                    1L,
                    fixture.Host.VirtualCrossItemRecycleCount,
                    "precise cross-item diagnostic increments on publish");
                AssertEqual(
                    1L,
                    fixture.Host.VirtualCacheReuseCount,
                    "general detached-cache diagnostic includes cross reuse");
            }
        }

        private static void
            TestDeclineDisposesCandidateAndBuildsFreshTree()
        {
            using (Fixture fixture = CreateFixture(true))
            {
                RecyclingAuditControl first = RealizeFirstThenCache(
                    fixture);
                first.DeclineNextReset = true;

                RealizeRange(fixture, 2, 2);

                RecyclingAuditControl third = FindRootForRow(
                    fixture.Host,
                    fixture.Rows[2]);

                AssertTrue(
                    third != null && !Object.ReferenceEquals(first, third),
                    "a clean decline falls back to fresh construction");
                AssertEqual(
                    1,
                    first.DisposeCount,
                    "a declined cached tree is disposed exactly once");
                AssertEqual(
                    1L,
                    fixture.Host.VirtualCrossItemRecycleRejectedCount,
                    "decline increments the rejection diagnostic");
                AssertEqual(
                    0L,
                    fixture.Host.VirtualCrossItemRecycleCount,
                    "declined tree is never counted as published reuse");
                AssertTrue(
                    !CacheContainsControl(fixture.Host, first),
                    "declined tree never returns to the cache");
            }
        }

        private static void TestResetExceptionIsVisibleAndRollsBackRange()
        {
            using (Fixture fixture = CreateFixture(true))
            {
                RecyclingAuditControl first = RealizeFirstThenCache(
                    fixture);
                RecyclingAuditControl committed = FindRootForRow(
                    fixture.Host,
                    fixture.Rows[1]);
                first.ThrowOnNextReset = true;
                bool failed = false;

                try
                {
                    RealizeRange(fixture, 2, 2);
                }
                catch (InvalidOperationException ex)
                {
                    failed = ex.Message.IndexOf(
                        "recycling-reset-failure",
                        StringComparison.Ordinal) >= 0;
                }

                AssertTrue(
                    failed,
                    "reset callback exception remains visible");
                AssertTrue(
                    Object.ReferenceEquals(
                        committed,
                        FindRootForRow(
                            fixture.Host,
                            fixture.Rows[1])),
                    "failed recycle keeps the committed range unchanged");
                AssertEqual(
                    1,
                    first.DisposeCount,
                    "failed reset candidate is disposal-only");
                AssertTrue(
                    !CacheContainsControl(fixture.Host, first),
                    "failed reset candidate never returns to the cache");
            }
        }

        private static Fixture CreateFixture(bool explicitRecycling)
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <RecyclingAuditControl Name='RowRoot'>" +
                "      <TextBox Name='Editor' " +
                "        Text='{Binding Text, Mode=TwoWay}' />" +
                "    </RecyclingAuditControl>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = new Fixture();
            fixture.Runtime = XamlRuntime.Load(markup);
            fixture.Host = fixture.Runtime.GetItemsControl("Rows");
            fixture.Host.VirtualizationCacheItems = 2;
            fixture.Host.ItemRecycling = explicitRecycling
                ? ItemRecyclingMode.Explicit
                : ItemRecyclingMode.Disabled;
            fixture.Rows = new ArrayList();
            fixture.Rows.Add(new RecyclingAuditRow("one", "First"));
            fixture.Rows.Add(new RecyclingAuditRow("two", "Second"));
            fixture.Rows.Add(new RecyclingAuditRow("three", "Third"));
            SetField(fixture.Host, "ItemValues", fixture.Rows);
            SetField(fixture.Host, "CommittedItemValues", fixture.Rows);
            return fixture;
        }

        private static Fixture CreateNonParticipantFixture()
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Name='RowRoot'>" +
                "      <TextBox Name='Editor' " +
                "        Text='{Binding Text, Mode=TwoWay}' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = new Fixture();
            fixture.Runtime = XamlRuntime.Load(markup);
            fixture.Host = fixture.Runtime.GetItemsControl("Rows");
            fixture.Host.VirtualizationCacheItems = 2;
            fixture.Host.ItemRecycling = ItemRecyclingMode.Explicit;
            fixture.Rows = new ArrayList();
            fixture.Rows.Add(new RecyclingAuditRow("one", "First"));
            fixture.Rows.Add(new RecyclingAuditRow("two", "Second"));
            fixture.Rows.Add(new RecyclingAuditRow("three", "Third"));
            SetField(fixture.Host, "ItemValues", fixture.Rows);
            SetField(fixture.Host, "CommittedItemValues", fixture.Rows);
            return fixture;
        }

        private static RecyclingAuditControl RealizeFirstThenCache(
            Fixture fixture)
        {
            RealizeRange(fixture, 0, 0);
            RecyclingAuditControl first = FindRootForRow(
                fixture.Host,
                fixture.Rows[0]);
            RealizeRange(fixture, 1, 1);

            AssertTrue(
                first != null && first.Parent == null,
                "first row enters the detached cache");
            return first;
        }

        private static void RealizeRange(
            Fixture fixture,
            int start,
            int end)
        {
            int generation = AdvanceGeneration(fixture.Host);
            MethodInfo manager = typeof(XamlRuntime).GetMethod(
                "ReconcileVirtualRangeSynchronously",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (manager == null)
            {
                throw new InvalidOperationException(
                    "The direct realization manager was not found.");
            }

            try
            {
                manager.Invoke(
                    fixture.Runtime,
                    new object[]
                    {
                        fixture.Host,
                        start,
                        end,
                        false,
                        false,
                        generation
                    });
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;

                throw;
            }
        }

        private static int AdvanceGeneration(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo refresh = FindField(
                host.GetType(),
                "RefreshGeneration");
            int generation = (int)refresh.GetValue(host) + 1;
            refresh.SetValue(host, generation);
            SetField(host, "DirectVirtualGeneration", generation);
            SetField(host, "DirectVirtualActive", true);
            return generation;
        }

        private static RecyclingAuditControl FindRootForRow(
            XamlRuntime.ItemsControl host,
            object row)
        {
            return FindControlForRow(host, row) as
                RecyclingAuditControl;
        }

        private static Control FindControlForRow(
            XamlRuntime.ItemsControl host,
            object row)
        {
            ArrayList records = GetField(
                host,
                "RenderedItems") as ArrayList;
            int i;

            for (i = 0; records != null && i < records.Count; i++)
            {
                object record = records[i];

                if (Object.ReferenceEquals(
                    GetField(record, "Item"),
                    row))
                {
                    return GetField(record, "Control") as Control;
                }
            }

            return null;
        }

        private static bool CacheContainsControl(
            XamlRuntime.ItemsControl host,
            Control control)
        {
            ArrayList records = GetField(
                host,
                "DirectVirtualCacheRecords") as ArrayList;
            int i;

            for (i = 0; records != null && i < records.Count; i++)
            {
                if (Object.ReferenceEquals(
                    GetField(records[i], "Control"),
                    control))
                {
                    return true;
                }
            }

            return false;
        }

        private static object GetField(object target, string name)
        {
            return FindField(target.GetType(), name).GetValue(target);
        }

        private static void SetField(
            object target,
            string name,
            object value)
        {
            FindField(target.GetType(), name).SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                if (field != null)
                    return field;

                type = type.BaseType;
            }

            throw new InvalidOperationException(
                "Field was not found: " + name + ".");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message + ".");
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
