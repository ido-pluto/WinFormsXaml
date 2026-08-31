using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    /// <summary>
    /// Guards the committed viewport origin when a far direct realization fails
    /// before it can replace the published controls.
    /// </summary>
    internal static class DirectVirtualizationOriginRollbackTests
    {
        private sealed class RollbackRow
        {
            public readonly string Id;
            private readonly string _text;
            public bool ThrowOnRead;

            public RollbackRow(int index)
            {
                Id = "rollback-" + index;
                _text = "Rollback " + index;
            }

            public string Text
            {
                get
                {
                    if (ThrowOnRead)
                    {
                        throw new InvalidOperationException(
                            "Direct viewport destination build failed.");
                    }

                    return _text;
                }
            }
        }

        internal static void RunAll()
        {
            TestFailedFarDestinationBuildRestoresPublishedOrigin();
        }

#if DIRECT_VIRTUAL_ORIGIN_ROLLBACK_STANDALONE
        [STAThread]
        private static int Main()
        {
            try
            {
                RunAll();
                Console.WriteLine(
                    "Direct virtualization origin rollback test passed.");
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
            TestFailedFarDestinationBuildRestoresPublishedOrigin()
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
                ArrayList rows = CreateRows(100);
                RollbackRow destination = (RollbackRow)rows[50];

                host.CreateControl();
                host.SetItems(rows);
                host.ScrollToIndex(10);
                Application.DoEvents();

                AssertTrue(
                    host.DirectVirtualActive &&
                    host.DirectVirtualViewport != null,
                    "the direct viewport is active before the failure");
                AssertViewportCovered(host, "committed origin");

                Point physicalOrigin = host.AutoScrollPosition;
                Point logicalOrigin = GetLogicalScroll(host);
                ArrayList committedRecords = host.RenderedItems;
                VirtualViewportModel committedModel =
                    host.DirectVirtualViewport;
                int firstVisible = committedModel.FindIndexAtOffset(
                    logicalOrigin.Y);
                Control committedVisibleControl = GetControlForIndex(
                    host,
                    firstVisible);

                AssertTrue(
                    logicalOrigin.Y > 0,
                    "the committed rollback origin is nonzero");
                AssertControlIntersectsViewport(
                    host,
                    committedVisibleControl,
                    "the committed control starts in the viewport");

                destination.ThrowOnRead = true;
                Exception surfaced = null;

                try
                {
                    host.ScrollToIndex(50);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(
                    surfaced != null,
                    "the far destination binding failure is surfaced");
                AssertSame(
                    committedModel,
                    host.DirectVirtualViewport,
                    "the failed build retains the committed model");
                AssertSame(
                    committedRecords,
                    host.RenderedItems,
                    "the failed build retains the committed record snapshot");
                AssertEqual(
                    physicalOrigin,
                    host.AutoScrollPosition,
                    "the failed build restores the native scroll origin");
                AssertEqual(
                    logicalOrigin,
                    GetLogicalScroll(host),
                    "the failed build restores the logical scroll origin");
                AssertControlIntersectsViewport(
                    host,
                    committedVisibleControl,
                    "the retained control intersects the restored viewport");
                AssertViewportCovered(host, "immediate rollback");

                Application.DoEvents();

                AssertSame(
                    committedRecords,
                    host.RenderedItems,
                    "message dispatch retains the committed records");
                AssertEqual(
                    physicalOrigin,
                    host.AutoScrollPosition,
                    "message dispatch preserves the native origin");
                AssertEqual(
                    logicalOrigin,
                    GetLogicalScroll(host),
                    "message dispatch preserves the logical origin");
                AssertControlIntersectsViewport(
                    host,
                    committedVisibleControl,
                    "the retained control remains in the viewport");
                AssertViewportCovered(host, "post-message-loop rollback");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static ArrayList CreateRows(int count)
        {
            ArrayList rows = new ArrayList(count);
            int index;

            for (index = 0; index < count; index++)
                rows.Add(new RollbackRow(index));

            return rows;
        }

        private static Point GetLogicalScroll(
            XamlRuntime.ItemsControl host)
        {
            Point native = host.AutoScrollPosition;
            return new Point(
                Math.Max(0, -native.X),
                Math.Max(0, -native.Y));
        }

        private static Control GetControlForIndex(
            XamlRuntime.ItemsControl host,
            int logicalIndex)
        {
            int index;

            for (index = 0; index < host.RenderedItems.Count; index++)
            {
                object record = host.RenderedItems[index];

                if ((int)GetField(record, "LogicalIndex") == logicalIndex)
                    return GetField(record, "Control") as Control;
            }

            throw new InvalidOperationException(
                "No realized control for logical index " + logicalIndex + ".");
        }

        private static void AssertControlIntersectsViewport(
            XamlRuntime.ItemsControl host,
            Control control,
            string message)
        {
            AssertTrue(
                control != null && !control.IsDisposed,
                message + " and is alive");
            AssertTrue(
                control.Bounds.IntersectsWith(host.ClientRectangle),
                message + " (bounds=" + control.Bounds +
                ", viewport=" + host.ClientRectangle + ")");
        }

        private static void AssertViewportCovered(
            XamlRuntime.ItemsControl host,
            string message)
        {
            VirtualViewportModel model = host.DirectVirtualViewport;

            AssertTrue(
                host.DirectVirtualActive && model != null,
                message + " keeps the direct viewport active");

            int viewport = host.ClientSize.Height;

            if (model.Count == 0 || viewport <= 0 || model.TotalExtent == 0L)
                return;

            int logicalScroll = GetLogicalScroll(host).Y;
            AssertTrue(
                (long)logicalScroll < model.TotalExtent,
                message + " keeps the origin inside content");

            long viewportEnd = Math.Min(
                model.TotalExtent,
                (long)logicalScroll + (long)viewport);
            int first = model.FindIndexAtOffset(logicalScroll);
            int last = model.FindIndexAtOffset(viewportEnd - 1L);

            AssertTrue(
                host.DirectVirtualRealizedStart <= first &&
                host.DirectVirtualRealizedEnd >= last,
                message + " retains every visible logical index");

            int expectedIndex = first;
            int coveredEnd = 0;
            int index;

            for (index = 0; index < host.RenderedItems.Count; index++)
            {
                object record = host.RenderedItems[index];
                int logicalIndex = (int)GetField(record, "LogicalIndex");

                if (logicalIndex < first || logicalIndex > last)
                    continue;

                AssertEqual(
                    expectedIndex,
                    logicalIndex,
                    message + " retains contiguous visible records");

                Control control = GetField(record, "Control") as Control;
                AssertTrue(
                    control != null && !control.IsDisposed,
                    message + " retains each visible control");

                int start = control.Bounds.Top;
                int end = control.Bounds.Bottom;
                AssertTrue(
                    end > 0 && start < viewport,
                    message + " places every visible control in the client");

                if (logicalIndex == first)
                {
                    AssertTrue(
                        start <= 0,
                        message + " has no leading blank space");
                }
                else
                {
                    AssertTrue(
                        start <= coveredEnd,
                        message + " has no blank space between controls");
                }

                coveredEnd = Math.Max(coveredEnd, end);
                expectedIndex++;
            }

            AssertEqual(
                last + 1,
                expectedIndex,
                message + " publishes every visible control");
            AssertTrue(
                coveredEnd >= Math.Min(
                    viewport,
                    ToInt32(model.TotalExtent - (long)logicalScroll)),
                message + " covers the trailing viewport edge");
        }

        private static int ToInt32(long value)
        {
            if (value <= 0L)
                return 0;
            if (value >= Int32.MaxValue)
                return Int32.MaxValue;

            return (int)value;
        }

        private static object GetField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            if (field == null)
                throw new InvalidOperationException("Missing field: " + name);

            return field.GetValue(instance);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message + ".");
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ": expected the same object reference.");
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
                    message + ": expected <" + expected +
                    ">, actual <" + actual + ">.");
            }
        }
    }
}
