using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public delegate void VirtualRealizationAuditCallback();

    public sealed class VirtualRealizationAuditRow
    {
        public string Id;
        public string Value;
        public Image Preview;
    }

    public sealed class VirtualRealizationAuditControl : Label
    {
        public static int DisposeCount;
        public static VirtualRealizationAuditCallback BuildCallback;

        private bool _disposed;
        private string _auditValue;

        public string AuditValue
        {
            get { return _auditValue; }
            set
            {
                if (String.Equals(
                        value,
                        "Throw",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Requested virtual build failure.");
                }

                _auditValue = value;
                Text = value;

                VirtualRealizationAuditCallback callback =
                    BuildCallback;

                if (callback != null)
                    callback();
            }
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

    /// <summary>
    /// Coverage for the synchronous direct-viewport realization manager.
    /// </summary>
    internal static class VirtualRealizationManagerTests
    {
        private sealed class Fixture : IDisposable
        {
            public XamlRuntime Runtime;
            public XamlRuntime.ItemsControl Host;
            public ArrayList Rows;
            public Bitmap ExternalImage;

            public void Dispose()
            {
                if (Runtime != null)
                    Runtime.Dispose();

                if (ExternalImage != null)
                    ExternalImage.Dispose();
            }
        }

        public static void Run()
        {
            TestDetachedCacheIsOnlyAReusableHint();
            TestValidationAndReusePolicyDrainCache();
            TestPublishedReuseReceivesCurrentGeneration();
            TestDirectCacheTrimAndClear();
            TestLeavingRecordsDisposeWithoutOwningApplicationImages();
            TestBuildFailureKeepsCommittedRange();
            TestEmptyAndSingleItemRanges();
            TestRangeCrossesThirtyTwoItemBoundary();
            TestReentrantBuildKeepsNewerRange();
            TestStyledCollapsedRootFallsBackBeforePublication();
        }

        private static void
            TestStyledCollapsedRootFallsBackBeforePublication()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='120' Height='80' " +
                "    AutoScroll='true' Virtualizing='true' " +
                "    VirtualizationThreshold='1' FixedItemSize='20' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.Resources>" +
                "    <Style TargetType='Panel'>" +
                "      <Setter Property='Visibility' Value='Collapsed' />" +
                "    </Style>" +
                "  </ItemsControl.Resources>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <VirtualRealizationAuditControl " +
                "          AuditValue='{Binding Value}' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                VirtualRealizationAuditRow row =
                    new VirtualRealizationAuditRow();

                row.Id = "collapsed";
                row.Value = "Collapsed by implicit style";
                rows.Add(row);
                SetField(host, "ItemValues", rows);
                SetField(host, "CommittedItemValues", rows);

                bool activated = InvokeDirectActivation(
                    runtime,
                    host);

                AssertEqual(
                    false,
                    activated,
                    "a style-collapsed root falls back to keyed rendering");
                AssertEqual(
                    false,
                    GetField(host, "DirectVirtualActive"),
                    "rejected direct activation restores its prior state");

                ArrayList rendered =
                    GetField(host, "RenderedItems") as ArrayList;

                AssertTrue(
                    rendered == null || rendered.Count == 0,
                    "the rejected direct candidate is never published");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestDetachedCacheIsOnlyAReusableHint()
        {
            VirtualRealizationAuditControl.DisposeCount = 0;

            using (Fixture fixture = CreateFixture(6, 2))
            {
                int generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    false,
                    generation);

                Control first = FindRootForRow(
                    fixture.Host,
                    fixture.Rows[0]);
                Control second = FindRootForRow(
                    fixture.Host,
                    fixture.Rows[1]);

                generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    2,
                    3,
                    false,
                    false,
                    generation);

                AssertEqual(
                    2,
                    fixture.Host.VirtualCacheCount,
                    "leaving controls enter the bounded detached cache");
                AssertEqual(
                    null,
                    first.Parent,
                    "cached first row is removed from native child layout");
                AssertEqual(
                    null,
                    second.Parent,
                    "cached second row is removed from native child layout");

                ((VirtualRealizationAuditRow)fixture.Rows[0]).Value =
                    "Row 0 refreshed";

                generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    false,
                    generation);

                AssertSame(
                    first,
                    FindRootForRow(fixture.Host, fixture.Rows[0]),
                    "same-key cached first row is reused as a hint");
                AssertEqual(
                    "Row 0 refreshed",
                    FindAuditControl(first).Text,
                    "cache reuse re-evaluates ordinary bindings");
                AssertSame(
                    second,
                    FindRootForRow(fixture.Host, fixture.Rows[1]),
                    "same-key cached second row is reused as a hint");
                AssertRenderedIndices(
                    fixture.Host,
                    new int[] { 0, 1 },
                    "cache reuse preserves sorted logical output");
            }
        }

        private static void
            TestValidationAndReusePolicyDrainCache()
        {
            VirtualRealizationAuditControl.DisposeCount = 0;

            using (Fixture fixture = CreateFixture(6, 2))
            {
                int generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    false,
                    generation);

                Control originalFirst = FindRootForRow(
                    fixture.Host,
                    fixture.Rows[0]);

                generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    2,
                    3,
                    false,
                    false,
                    generation);

                AssertEqual(
                    2,
                    fixture.Host.VirtualCacheCount,
                    "the precondition cache is populated");

                generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    true,
                    generation);

                AssertEqual(
                    0,
                    fixture.Host.VirtualCacheCount,
                    "validated realization drains stale cached trees");
                AssertTrue(
                    !Object.ReferenceEquals(
                        originalFirst,
                        FindRootForRow(fixture.Host, fixture.Rows[0])),
                    "validated realization does not borrow its old cache hint");

                fixture.Host.ReuseItems = false;
                generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    2,
                    3,
                    false,
                    false,
                    generation);

                AssertEqual(
                    0,
                    fixture.Host.VirtualCacheCount,
                    "ReuseItems=false never retains unusable cached trees");
                AssertEqual(
                    1,
                    fixture.ExternalImage.Width,
                    "cache policy cleanup preserves application image ownership");
            }
        }

        private static void
            TestPublishedReuseReceivesCurrentGeneration()
        {
            using (Fixture fixture = CreateFixture(2, 1))
            {
                int generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    false,
                    generation);

                object record = FindRecordForRow(
                    fixture.Host,
                    fixture.Rows[0]);
                Control control = GetField(record, "Control") as Control;
                generation = AdvanceGeneration(fixture.Host);

                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    false,
                    generation);

                object reused = FindRecordForRow(
                    fixture.Host,
                    fixture.Rows[0]);

                AssertSame(
                    control,
                    GetField(reused, "Control"),
                    "the unchanged current control is reused");
                AssertEqual(
                    generation,
                    GetField(reused, "RealizationGeneration"),
                    "publication stamps reuse with the committed generation");
            }
        }

        private static void
            TestDirectCacheTrimAndClear()
        {
            VirtualRealizationAuditControl.DisposeCount = 0;

            using (Fixture fixture = CreateFixture(6, 3))
            {
                SetField(fixture.Host, "DirectVirtualActive", true);
                int generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    2,
                    false,
                    false,
                    generation);

                generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    3,
                    5,
                    false,
                    false,
                    generation);

                SetField(fixture.Host, "_virtualizationCacheItems", 1);
                InvokeDirectCacheTrim(fixture.Runtime, fixture.Host);
                AssertEqual(
                    1,
                    fixture.Host.VirtualCacheCount,
                    "active direct cache trim honors the configured limit");

                InvokeDirectCacheClear(fixture.Runtime, fixture.Host);
                AssertEqual(
                    0,
                    fixture.Host.VirtualCacheCount,
                    "explicit direct cache clear drains an active host");
                AssertEqual(
                    1,
                    fixture.ExternalImage.Width,
                    "direct cache trim preserves external image ownership");
            }
        }

        private static void
            TestLeavingRecordsDisposeWithoutOwningApplicationImages()
        {
            VirtualRealizationAuditControl.DisposeCount = 0;

            using (Fixture fixture = CreateFixture(4, 0))
            {
                int generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    false,
                    generation);

                generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    2,
                    3,
                    false,
                    false,
                    generation);

                AssertEqual(
                    2,
                    VirtualRealizationAuditControl.DisposeCount,
                    "leaving uncached control trees are disposed");
                AssertEqual(
                    1,
                    fixture.ExternalImage.Width,
                    "disposing generated trees does not own application images");
                AssertRenderedIndices(
                    fixture.Host,
                    new int[] { 2, 3 },
                    "range shift publishes only the requested rows");
            }
        }

        private static void TestBuildFailureKeepsCommittedRange()
        {
            VirtualRealizationAuditControl.DisposeCount = 0;

            using (Fixture fixture = CreateFixture(4, 0))
            {
                int generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    false,
                    generation);

                Control first = FindRootForRow(
                    fixture.Host,
                    fixture.Rows[0]);
                ((VirtualRealizationAuditRow)fixture.Rows[2]).Value =
                    "Throw";
                generation = AdvanceGeneration(fixture.Host);
                bool threw = false;

                try
                {
                    InvokeManager(
                        fixture.Runtime,
                        fixture.Host,
                        2,
                        3,
                        false,
                        true,
                        generation);
                }
                catch (Exception)
                {
                    threw = true;
                }

                AssertTrue(threw, "a template build failure remains observable");
                AssertSame(
                    first,
                    FindRootForRow(fixture.Host, fixture.Rows[0]),
                    "build failure leaves the committed control range intact");
                AssertRenderedIndices(
                    fixture.Host,
                    new int[] { 0, 1 },
                    "build failure leaves committed range metadata intact");
                AssertTrue(
                    VirtualRealizationAuditControl.DisposeCount > 0,
                    "the failed staged control is disposed");
                AssertEqual(
                    1,
                    fixture.ExternalImage.Width,
                    "failure cleanup preserves external image ownership");
            }
        }

        private static void TestRangeCrossesThirtyTwoItemBoundary()
        {
            using (Fixture fixture = CreateFixture(33, 0))
            {
                int generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    30,
                    32,
                    false,
                    false,
                    generation);

                AssertRenderedIndices(
                    fixture.Host,
                    new int[] { 30, 31, 32 },
                    "the 32-item boundary is an ordinary bounded range");
                AssertEqual(
                    3,
                    fixture.Host.RealizedCount,
                    "only the three requested boundary rows are realized");
            }
        }

        private static void TestEmptyAndSingleItemRanges()
        {
            using (Fixture empty = CreateFixture(0, 0))
            {
                int generation = AdvanceGeneration(empty.Host);
                InvokeManager(
                    empty.Runtime,
                    empty.Host,
                    -1,
                    -1,
                    false,
                    false,
                    generation);

                AssertEqual(
                    0,
                    empty.Host.RealizedCount,
                    "an empty logical source publishes an empty range");
            }

            using (Fixture single = CreateFixture(1, 0))
            {
                int generation = AdvanceGeneration(single.Host);
                InvokeManager(
                    single.Runtime,
                    single.Host,
                    0,
                    0,
                    false,
                    false,
                    generation);
                AssertRenderedIndices(
                    single.Host,
                    new int[] { 0 },
                    "a one-item range realizes exactly its single index");

                generation = AdvanceGeneration(single.Host);
                InvokeManager(
                    single.Runtime,
                    single.Host,
                    -1,
                    -1,
                    false,
                    false,
                    generation);
                AssertEqual(
                    0,
                    single.Host.RealizedCount,
                    "the explicit empty range retires a single realized row");
            }
        }

        private static void TestReentrantBuildKeepsNewerRange()
        {
            VirtualRealizationAuditControl.DisposeCount = 0;

            using (Fixture fixture = CreateFixture(6, 0))
            {
                int generation = AdvanceGeneration(fixture.Host);
                InvokeManager(
                    fixture.Runtime,
                    fixture.Host,
                    0,
                    1,
                    false,
                    false,
                    generation);

                VirtualRealizationAuditControl.BuildCallback =
                    delegate
                    {
                        VirtualRealizationAuditControl.BuildCallback = null;
                        int nestedGeneration =
                            AdvanceGeneration(fixture.Host);
                        InvokeManager(
                            fixture.Runtime,
                            fixture.Host,
                            4,
                            5,
                            false,
                            false,
                            nestedGeneration);
                    };

                try
                {
                    generation = AdvanceGeneration(fixture.Host);
                    InvokeManager(
                        fixture.Runtime,
                        fixture.Host,
                        2,
                        3,
                        false,
                        true,
                        generation);
                }
                finally
                {
                    VirtualRealizationAuditControl.BuildCallback = null;
                }

                AssertRenderedIndices(
                    fixture.Host,
                    new int[] { 4, 5 },
                    "a reentrant build gives the newer generation final ownership");
                AssertTrue(
                    FindRootForRow(fixture.Host, fixture.Rows[2]) == null,
                    "the superseded staged row is not published");
            }
        }

        private static Fixture CreateFixture(
            int count,
            int cacheItems)
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <VirtualRealizationAuditControl " +
                "          AuditValue='{Binding Value}' />" +
                "      <PictureBox Source='{Binding Preview}' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            Fixture fixture = new Fixture();
            fixture.ExternalImage = new Bitmap(1, 1);
            fixture.Rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
            {
                VirtualRealizationAuditRow row =
                    new VirtualRealizationAuditRow();

                row.Id = "row-" + i.ToString();
                row.Value = "Row " + i.ToString();
                row.Preview = fixture.ExternalImage;
                fixture.Rows.Add(row);
            }

            fixture.Runtime = XamlRuntime.Load(markup);
            fixture.Host = fixture.Runtime.GetItemsControl("Rows");
            fixture.Host.VirtualizationCacheItems = cacheItems;
            SetField(fixture.Host, "ItemValues", fixture.Rows);
            SetField(fixture.Host, "CommittedItemValues", fixture.Rows);
            return fixture;
        }

        private static void InvokeManager(
            XamlRuntime runtime,
            XamlRuntime.ItemsControl host,
            int start,
            int end,
            bool forceRebuild,
            bool validateValues,
            int expectedGeneration)
        {
            MethodInfo manager = typeof(XamlRuntime).GetMethod(
                "ReconcileVirtualRangeSynchronously",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (manager == null)
            {
                throw new InvalidOperationException(
                    "The synchronous virtual realization manager was not found.");
            }

            try
            {
                manager.Invoke(
                    runtime,
                    new object[]
                    {
                        host,
                        start,
                        end,
                        forceRebuild,
                        validateValues,
                        expectedGeneration
                    });
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;

                throw;
            }
        }

        private static bool InvokeDirectActivation(
            XamlRuntime runtime,
            XamlRuntime.ItemsControl host)
        {
            MethodInfo activate = typeof(XamlRuntime).GetMethod(
                "ActivateDirectViewportVirtualization",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (activate == null)
            {
                throw new InvalidOperationException(
                    "The direct viewport activation method was not found.");
            }

            try
            {
                return (bool)activate.Invoke(
                    runtime,
                    new object[] { host, false, true });
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
            FieldInfo field = FindField(host.GetType(), "RefreshGeneration");
            int generation = (int)field.GetValue(host) + 1;
            field.SetValue(host, generation);
            SetField(host, "DirectVirtualGeneration", generation);
            SetField(host, "DirectVirtualActive", true);
            return generation;
        }

        private static void InvokeDirectCacheTrim(
            XamlRuntime runtime,
            XamlRuntime.ItemsControl host)
        {
            InvokeDirectCacheMethod(
                runtime,
                host,
                "TrimDirectVirtualizationCache");
        }

        private static void InvokeDirectCacheClear(
            XamlRuntime runtime,
            XamlRuntime.ItemsControl host)
        {
            InvokeDirectCacheMethod(
                runtime,
                host,
                "ClearDirectVirtualizationCache");
        }

        private static void InvokeDirectCacheMethod(
            XamlRuntime runtime,
            XamlRuntime.ItemsControl host,
            string methodName)
        {
            MethodInfo trim = typeof(XamlRuntime).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (trim == null)
            {
                throw new InvalidOperationException(
                    "The direct virtualization cache method was not found: " +
                    methodName + ".");
            }

            try
            {
                trim.Invoke(runtime, new object[] { host });
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;

                throw;
            }
        }

        private static object FindRecordForRow(
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
                    return record;
                }
            }

            return null;
        }

        private static Control FindRootForRow(
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

        private static VirtualRealizationAuditControl FindAuditControl(
            Control root)
        {
            if (root == null)
                return null;

            VirtualRealizationAuditControl audit =
                root as VirtualRealizationAuditControl;

            if (audit != null)
                return audit;

            int i;

            for (i = 0; i < root.Controls.Count; i++)
            {
                audit = FindAuditControl(root.Controls[i]);

                if (audit != null)
                    return audit;
            }

            return null;
        }

        private static void AssertRenderedIndices(
            XamlRuntime.ItemsControl host,
            int[] expected,
            string message)
        {
            ArrayList records = GetField(host, "RenderedItems") as ArrayList;

            if (records == null || records.Count != expected.Length)
                throw new InvalidOperationException(message + ".");

            int i;

            for (i = 0; i < expected.Length; i++)
            {
                object record = records[i];
                object actual = GetField(record, "LogicalIndex");

                if (!Object.Equals(expected[i], actual))
                    throw new InvalidOperationException(message + ".");
            }
        }

        private static object GetField(object target, string name)
        {
            if (target == null)
                return null;

            FieldInfo field = FindField(target.GetType(), name);
            return field.GetValue(target);
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
                "Expected field was not found: " + name + ".");
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
                throw new InvalidOperationException(message + ".");
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
                throw new InvalidOperationException(message + ".");
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message + ".");
        }
    }
}
