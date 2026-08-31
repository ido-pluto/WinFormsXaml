using System;
using System.Collections;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class ReactiveRenderedRecordIndexTests
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private sealed class ReactiveRow
        {
            public readonly string Id;
            public readonly PropertyBinding<string> Text;

            public ReactiveRow(int index)
            {
                Id = "reactive-index-" + index;
                Text = new PropertyBinding<string>("Row " + index);
            }
        }

        private sealed class PlainRow
        {
            public readonly string Id;
            private readonly string _text;
            public bool ThrowOnRead;

            public PlainRow(string id, string text)
            {
                Id = id;
                _text = text;
            }

            public string Text
            {
                get
                {
                    if (ThrowOnRead)
                    {
                        throw new InvalidOperationException(
                            "Rendered-record index rollback failure.");
                    }

                    return _text;
                }
            }
        }

        internal static void RunAll()
        {
            TestLargeReactiveBatchUsesOneIndexProbePerLookup();
            TestEqualControlReorderKeepsReferenceIdentity();
            TestCommonMixedReorderAvoidsLinearReferenceScans();
            TestFailedNormalRefreshRetainsCommittedIndex();
            TestDirectVirtualRangeAndRollbackKeepIndexCurrent();
            TestLightweightTransitionsClearAndRestoreIndex();
            TestDisposalClearsIndex();
        }

#if REACTIVE_RENDERED_RECORD_INDEX_STANDALONE
        [STAThread]
        private static int Main()
        {
            try
            {
                RunAll();
                Console.WriteLine(
                    "Reactive rendered-record index tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
#endif

        private static void
            TestLargeReactiveBatchUsesOneIndexProbePerLookup()
        {
            const int rowCount = 512;
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <IndexedEqualPanel>" +
                "      <Label Text='{Binding Text}' />" +
                "    </IndexedEqualPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList(rowCount);
                int index;

                for (index = 0; index < rowCount; index++)
                    rows.Add(new ReactiveRow(index));

                EnsureHandle(runtime.RootControl);
                host.SetItems(rows);
                DrainReactiveCallbacks(runtime.RootControl);
                AssertIndexMatchesPublishedRecords(host, rowCount);

                SetBooleanField(
                    host,
                    "_renderedItemRecordIndexDiagnosticsEnabled",
                    true);
                SetInt64Field(
                    host,
                    "_renderedItemRecordIndexLookupCount",
                    0L);
                SetInt64Field(
                    host,
                    "_renderedItemRecordIndexProbeCount",
                    0L);

                for (index = 0; index < rowCount; index++)
                {
                    ReactiveRow row = rows[index] as ReactiveRow;
                    row.Text.Value = "Updated " + index;
                }

                DrainReactiveCallbacks(runtime.RootControl);

                long lookups = GetInt64Field(
                    host,
                    "_renderedItemRecordIndexLookupCount");
                long probes = GetInt64Field(
                    host,
                    "_renderedItemRecordIndexProbeCount");

                AssertTrue(
                    lookups >= rowCount,
                    "every changed reactive slot resolves its item root");
                AssertTrue(
                    lookups <= rowCount * 4L,
                    "reactive lookup count stays proportional to slot count");
                AssertEqual(
                    lookups,
                    probes,
                    "each reactive root lookup performs one indexed probe");

                AssertEqual(
                    "Updated 0",
                    GetLabelForItem(host, rows[0]).Text,
                    "first reactive row updates");
                AssertEqual(
                    "Updated 256",
                    GetLabelForItem(host, rows[256]).Text,
                    "middle reactive row updates");
                AssertEqual(
                    "Updated 511",
                    GetLabelForItem(host, rows[511]).Text,
                    "last reactive row updates");
                AssertIndexMatchesPublishedRecords(host, rowCount);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestEqualControlReorderKeepsReferenceIdentity()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <IndexedEqualPanel>" +
                "      <Label Text='{Binding Text}' />" +
                "    </IndexedEqualPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreatePlainRows(6, "equal");
                host.SetItems(rows);

                Hashtable firstIndex = GetRecordIndex(host);
                Control[] controls = new Control[rows.Count];
                int index;

                for (index = 0; index < rows.Count; index++)
                {
                    object record = host.RenderedItems[index];
                    controls[index] = GetRecordControl(record);
                    AssertSame(
                        record,
                        firstIndex[controls[index]],
                        "equal control maps to its own record at " + index);
                }

                ArrayList reordered = new ArrayList();
                reordered.Add(rows[5]);
                reordered.Add(rows[2]);
                reordered.Add(rows[0]);
                reordered.Add(rows[4]);
                reordered.Add(rows[1]);
                reordered.Add(rows[3]);
                host.SetItems(reordered);

                Hashtable reorderedIndex = GetRecordIndex(host);
                AssertNotSame(
                    firstIndex,
                    reorderedIndex,
                    "reorder publishes a replacement lookup snapshot");
                AssertIndexMatchesPublishedRecords(host, reordered.Count);

                for (index = 0; index < controls.Length; index++)
                {
                    object indexedRecord = reorderedIndex[controls[index]];
                    AssertTrue(
                        indexedRecord != null,
                        "equal control remains independently indexed at " +
                        index);
                    AssertSame(
                        rows[index],
                        GetRecordField(indexedRecord, "Item"),
                        "equal control retains its original item ownership");
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestCommonMixedReorderAvoidsLinearReferenceScans()
        {
            const int rowCount = 128;
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreatePlainRows(rowCount, "ordered");
                host.SetItems(rows);

                SetInt64Field(
                    host,
                    "_itemControlReferenceScanProbeCount",
                    0L);

                ArrayList reordered = new ArrayList(rowCount + 1);
                reordered.Add(new PlainRow("inserted", "Inserted"));
                int index;

                for (index = rowCount - 1; index >= 0; index--)
                    reordered.Add(rows[index]);

                host.SetItems(reordered);

                AssertEqual(
                    0L,
                    GetInt64Field(
                        host,
                        "_itemControlReferenceScanProbeCount"),
                    "ordinary mixed reorder accepts native reference indices " +
                    "without a linear fallback");
                AssertIndexMatchesPublishedRecords(
                    host,
                    reordered.Count);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestFailedNormalRefreshRetainsCommittedIndex()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "AutoScroll='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows =
                    CreatePlainRows(12, "committed");
                host.SetItems(committedRows);

                ArrayList committedRecords = host.RenderedItems;
                Hashtable committedIndex = GetRecordIndex(host);
                ArrayList replacement =
                    CreatePlainRows(12, "replacement");
                ((PlainRow)replacement[0]).ThrowOnRead = true;
                Exception surfaced = null;

                try
                {
                    host.SetItems(replacement);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(
                    surfaced != null,
                    "failed normal refresh surfaces its binding error");
                AssertSame(
                    committedRecords,
                    host.RenderedItems,
                    "failed normal refresh retains the record snapshot");
                AssertSame(
                    committedIndex,
                    GetRecordIndex(host),
                    "failed normal refresh retains the lookup snapshot");
                AssertIndexMatchesPublishedRecords(
                    host,
                    committedRows.Count);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestDirectVirtualRangeAndRollbackKeepIndexCurrent()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='220' Height='100' " +
                "AutoScroll='true' ItemKeyPath='Id' Virtualizing='true' " +
                "VirtualizationThreshold='1' FixedItemSize='20' " +
                "OverscanItems='0' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreatePlainRows(100, "virtual");
                host.CreateControl();
                host.SetItems(rows);

                AssertTrue(
                    host.DirectVirtualActive,
                    "direct virtualization is active");
                AssertIndexMatchesPublishedRecords(
                    host,
                    host.RenderedItems.Count);

                Hashtable firstIndex = GetRecordIndex(host);
                host.ScrollToIndex(50);
                Application.DoEvents();

                AssertNotSame(
                    firstIndex,
                    GetRecordIndex(host),
                    "a new direct range publishes a replacement lookup");
                AssertIndexMatchesPublishedRecords(
                    host,
                    host.RenderedItems.Count);

                ArrayList committedRecords = host.RenderedItems;
                Hashtable committedIndex = GetRecordIndex(host);
                ((PlainRow)rows[80]).ThrowOnRead = true;
                Exception surfaced = null;

                try
                {
                    host.ScrollToIndex(80);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(
                    surfaced != null,
                    "failed direct destination surfaces its binding error");
                AssertSame(
                    committedRecords,
                    host.RenderedItems,
                    "failed direct destination retains the record snapshot");
                AssertSame(
                    committedIndex,
                    GetRecordIndex(host),
                    "failed direct destination retains the lookup snapshot");
                AssertIndexMatchesPublishedRecords(
                    host,
                    host.RenderedItems.Count);

                ((PlainRow)rows[80]).ThrowOnRead = false;
                host.Virtualizing = false;
                AssertTrue(
                    !host.DirectVirtualActive,
                    "normal renderer owns the post-virtual transition");
                AssertIndexMatchesPublishedRecords(host, rows.Count);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestLightweightTransitionsClearAndRestoreIndex()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='220' Height='100' " +
                "AutoScroll='true' ItemKeyPath='Id' Virtualizing='true' " +
                "VirtualizationMode='Lightweight' " +
                "VirtualizationThreshold='100' FixedItemSize='20' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreatePlainRows(20, "lightweight");
                host.CreateControl();
                host.SetItems(rows);

                AssertTrue(
                    host.LightweightActive,
                    "lightweight renderer is active");
                AssertEqual(
                    0,
                    host.RenderedItems.Count,
                    "lightweight renderer publishes no native records");
                AssertEqual(
                    0,
                    GetRecordIndex(host).Count,
                    "lightweight renderer clears the native lookup");

                host.VirtualizationMode =
                    ItemsControlVirtualizationMode.Controls;
                AssertTrue(
                    !host.LightweightActive,
                    "controls renderer owns the transition from lightweight");
                AssertIndexMatchesPublishedRecords(host, rows.Count);

                host.VirtualizationMode =
                    ItemsControlVirtualizationMode.Lightweight;
                AssertTrue(
                    host.LightweightActive,
                    "lightweight renderer can be reactivated");
                AssertEqual(
                    0,
                    host.RenderedItems.Count,
                    "reactivated lightweight renderer has no native records");
                AssertEqual(
                    0,
                    GetRecordIndex(host).Count,
                    "reactivated lightweight renderer clears the lookup");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestDisposalClearsIndex()
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "ProgressiveRendering='false' AutoScroll='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);
            XamlRuntime.ItemsControl host =
                runtime.GetItemsControl("Rows");
            host.SetItems(CreatePlainRows(3, "dispose"));
            AssertIndexMatchesPublishedRecords(host, 3);

            try
            {
                host.Dispose();

                AssertTrue(
                    GetFieldValue(host, "RenderedItems") == null,
                    "ItemsControl disposal clears rendered records");
                AssertTrue(
                    GetFieldValue(
                        host,
                        "_renderedItemRecordsByControl") == null,
                    "ItemsControl disposal clears the rendered-record lookup");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static ArrayList CreatePlainRows(
            int count,
            string prefix)
        {
            ArrayList rows = new ArrayList(count);
            int index;

            for (index = 0; index < count; index++)
            {
                rows.Add(
                    new PlainRow(
                        prefix + "-" + index,
                        prefix + " " + index));
            }

            return rows;
        }

        private static void AssertIndexMatchesPublishedRecords(
            XamlRuntime.ItemsControl host,
            int expectedCount)
        {
            ArrayList records = host.RenderedItems;
            Hashtable index = GetRecordIndex(host);

            AssertTrue(records != null, "rendered records are available");
            AssertEqual(
                expectedCount,
                records.Count,
                "published rendered-record count");
            AssertEqual(
                records.Count,
                index.Count,
                "lookup contains one entry per rendered record");

            int recordIndex;
            int previousLogicalIndex = -1;

            for (recordIndex = 0;
                 recordIndex < records.Count;
                 recordIndex++)
            {
                object record = records[recordIndex];
                Control control = GetRecordControl(record);
                int logicalIndex =
                    (int)GetRecordField(record, "LogicalIndex");

                AssertTrue(
                    control != null,
                    "published record owns a control at " + recordIndex);
                AssertSame(
                    record,
                    index[control],
                    "control resolves to its published record at " +
                    recordIndex);
                AssertTrue(
                    logicalIndex > previousLogicalIndex,
                    "logical indices remain strictly ordered");
                previousLogicalIndex = logicalIndex;
            }
        }

        private static Label GetLabelForItem(
            XamlRuntime.ItemsControl host,
            object item)
        {
            int index;

            for (index = 0; index < host.Controls.Count; index++)
            {
                Control root = host.Controls[index];

                if (!Object.ReferenceEquals(root.Tag, item))
                    continue;

                Label direct = root as Label;

                if (direct != null)
                    return direct;

                if (root.Controls.Count > 0)
                    return root.Controls[0] as Label;
            }

            throw new InvalidOperationException(
                "No rendered label was found for the requested item.");
        }

        private static Hashtable GetRecordIndex(
            XamlRuntime.ItemsControl host)
        {
            Hashtable index = GetFieldValue(
                host,
                "_renderedItemRecordsByControl") as Hashtable;
            AssertTrue(index != null, "rendered-record lookup is available");
            return index;
        }

        private static Control GetRecordControl(object record)
        {
            return GetRecordField(record, "Control") as Control;
        }

        private static object GetRecordField(
            object record,
            string fieldName)
        {
            AssertTrue(record != null, "rendered record exists");
            FieldInfo field = record.GetType().GetField(
                fieldName,
                InstanceMembers);
            AssertTrue(field != null, "rendered record field " + fieldName);
            return field.GetValue(record);
        }

        private static object GetFieldValue(
            object owner,
            string fieldName)
        {
            FieldInfo field = FindInstanceField(
                owner.GetType(),
                fieldName);
            AssertTrue(field != null, "host field " + fieldName);
            return field.GetValue(owner);
        }

        private static long GetInt64Field(
            object owner,
            string fieldName)
        {
            return (long)GetFieldValue(owner, fieldName);
        }

        private static void SetInt64Field(
            object owner,
            string fieldName,
            long value)
        {
            FieldInfo field = FindInstanceField(
                owner.GetType(),
                fieldName);
            AssertTrue(field != null, "host field " + fieldName);
            field.SetValue(owner, value);
        }

        private static void SetBooleanField(
            object owner,
            string fieldName,
            bool value)
        {
            FieldInfo field = FindInstanceField(
                owner.GetType(),
                fieldName);
            AssertTrue(field != null, "host field " + fieldName);
            field.SetValue(owner, value);
        }

        private static FieldInfo FindInstanceField(
            Type type,
            string fieldName)
        {
            Type current = type;

            while (current != null)
            {
                FieldInfo field = current.GetField(
                    fieldName,
                    InstanceMembers |
                    BindingFlags.DeclaredOnly);

                if (field != null)
                    return field;

                current = current.BaseType;
            }

            return null;
        }

        private static void EnsureHandle(Control root)
        {
            AssertTrue(root != null, "runtime root exists");

            if (!root.IsHandleCreated)
                root.CreateControl();

            AssertTrue(root.IsHandleCreated, "runtime root handle exists");
        }

        private static void DrainReactiveCallbacks(Control root)
        {
            int round;

            for (round = 0; round < 6; round++)
            {
                bool reached = false;

                root.BeginInvoke(
                    new MethodInvoker(
                        delegate
                        {
                            reached = true;
                        }));

                int iterations = 0;

                while (!reached && iterations < 1024)
                {
                    Application.DoEvents();
                    iterations++;
                }

                AssertTrue(reached, "reactive dispatch sentinel reached");
            }
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
                    message + ". Expected " + expected +
                    ", actual " + actual + ".");
            }
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
                throw new InvalidOperationException(message);
        }

        private static void AssertNotSame(
            object first,
            object second,
            string message)
        {
            if (Object.ReferenceEquals(first, second))
                throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Deliberately makes distinct native roots value-equal so the lookup and
    /// z-order guards prove they use reference identity.
    /// </summary>
    public sealed class IndexedEqualPanel : Panel
    {
        public override bool Equals(object value)
        {
            return value is IndexedEqualPanel;
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }
}
