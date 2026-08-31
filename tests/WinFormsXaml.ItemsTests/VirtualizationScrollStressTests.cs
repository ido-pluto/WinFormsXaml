using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    /// <summary>
    /// Exercises the direct viewport with immediate, alternating scroll input.
    /// Every assertion is made before yielding the message loop so a delayed
    /// repair cannot hide a blank range.
    /// </summary>
    internal static class VirtualizationScrollStressTests
    {
        private sealed class StressRow
        {
            public readonly string Id;
            public readonly int Height;
            public readonly string Title;
            public readonly string Detail;
            public readonly string LinkText;
            public readonly string Url;
            public readonly string Action;
            public readonly bool Checked;

            public StressRow(int index, int height)
            {
                Id = "stress-" + index;
                Height = height;
                Title = "Stress row " + index;
                Detail = "Complex nested content for row " + index;
                LinkText = "Open " + index;
                Url = "https://example.invalid/items/" + index;
                Action = "Action";
                Checked = (index & 1) == 0;
            }
        }

        internal static void RunAll()
        {
            TestVirtualizationIsOptInAtAndBeyondTheOldThreshold();
            TestNativePixelFramesUseTranslationFastPath();
            TestFixedRowsCoverRapidAlternatingScrolls();
            TestComplexVariableRowsCoverJumpsResizesAndReversals();
        }

        private static void TestNativePixelFramesUseTranslationFastPath()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='280' Height='101' " +
                "AutoScroll='true' ItemKeyPath='Id' Virtualizing='true' " +
                "VirtualizationThreshold='1' FixedItemSize='24' " +
                "OverscanItems='3' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Height='24' Orientation='Horizontal'>" +
                "      <Label Width='120' Text='{Binding Title}' />" +
                "      <Button Width='70' Text='{Binding Action}' />" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(240, new int[] { 24 });

                host.CreateControl();
                host.SetItems(rows);
                host.SetLogicalScrollOffset(1);

                int realizedStart = host.DirectVirtualRealizedStart;
                int realizedEnd = host.DirectVirtualRealizedEnd;
                int realizedCount = host.RealizedCount;
                long created = host.VirtualCreatedCount;
                long translated =
                    host.DirectVirtualTranslationFastPathCountForTest;

                host.SetLogicalScrollOffset(2);

                AssertEqual(
                    translated + 1L,
                    host.DirectVirtualTranslationFastPathCountForTest,
                    "same-range native pixel movement skips virtual relayout");
                AssertEqual(
                    realizedStart,
                    host.DirectVirtualRealizedStart,
                    "translation-only frame preserves realized start");
                AssertEqual(
                    realizedEnd,
                    host.DirectVirtualRealizedEnd,
                    "translation-only frame preserves realized end");
                AssertEqual(
                    realizedCount,
                    host.RealizedCount,
                    "translation-only frame preserves realized controls");
                AssertEqual(
                    created,
                    host.VirtualCreatedCount,
                    "translation-only frame creates no item controls");
                AssertViewportCovered(
                    host,
                    "translation-only native pixel frame");

                object firstRecord = host.RenderedItems[0];
                FieldInfo cacheField = firstRecord.GetType().GetField(
                    "MeasureCacheValid",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                AssertTrue(
                    cacheField != null,
                    "measurement-cache guard is available to the regression");
                cacheField.SetValue(firstRecord, false);
                translated =
                    host.DirectVirtualTranslationFastPathCountForTest;
                host.SetLogicalScrollOffset(3);

                AssertEqual(
                    translated,
                    host.DirectVirtualTranslationFastPathCountForTest,
                    "invalid measurement cache uses full virtual layout");
                AssertEqual(
                    true,
                    cacheField.GetValue(firstRecord),
                    "full virtual layout refreshes the invalid measurement");

                translated =
                    host.DirectVirtualTranslationFastPathCountForTest;
                host.HandleDirectVirtualViewportChanged();
                AssertEqual(
                    translated,
                    host.DirectVirtualTranslationFastPathCountForTest,
                    "same-origin layout is not mistaken for native translation");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestVirtualizationIsOptInAtAndBeyondTheOldThreshold()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='240' Height='100' " +
                "AutoScroll='true' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Height='20' Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(96, new int[] { 20 });

                host.CreateControl();
                host.SetItems(rows);

                AssertTrue(
                    !host.Virtualizing,
                    "omitted Virtualizing remains false");
                AssertTrue(
                    !host.IsVirtualizing,
                    "the former 32-item boundary does not activate a viewport");
                AssertEqual(
                    rows.Count,
                    host.RealizedCount,
                    "the ordinary renderer realizes the complete list by default");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestFixedRowsCoverRapidAlternatingScrolls()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='360' Height='137' " +
                "AutoScroll='true' ItemKeyPath='Id' Virtualizing='true' " +
                "VirtualizationThreshold='1' FixedItemSize='28' " +
                "OverscanItems='2' VirtualizationCacheItems='12' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Height='28' Orientation='Horizontal' Spacing='4'>" +
                "      <Label Width='112' Text='{Binding Title}' />" +
                "      <CheckBox Width='70' Text='Flag' Checked='{Binding Checked}' />" +
                "      <HyperlinkLabel Width='78' Text='{Binding LinkText}' " +
                "                      NavigateUri='{Binding Url}' />" +
                "      <Button Width='64' Height='22' Text='{Binding Action}' />" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(1000, new int[] { 28 });
                int[] speeds =
                    new int[] { 1, 3, 11, 28, 57, 211, 977, 4096 };
                int position = 0;
                int direction = 1;
                int i;

                host.CreateControl();
                host.SetItems(rows);
                AssertTrue(host.IsVirtualizing, "fixed stress viewport is active");

                for (i = 0; i < 160; i++)
                {
                    if (i != 0 && i % 9 == 0)
                        direction = -direction;

                    int maximum = GetMaximumLogicalScroll(host);
                    long candidate =
                        (long)position +
                        (long)direction * (long)speeds[i % speeds.Length];

                    if (candidate < 0L)
                    {
                        position = 0;
                        direction = 1;
                    }
                    else if (candidate > maximum)
                    {
                        position = maximum;
                        direction = -1;
                    }
                    else
                    {
                        position = (int)candidate;
                    }

                    SetScrollAndAssertCovered(
                        host,
                        position,
                        "fixed alternating scroll " + i);

                    if (i % 23 == 0)
                    {
                        Application.DoEvents();
                        AssertViewportCovered(
                            host,
                            "fixed alternating post-message-loop " + i);
                    }
                }

                int[] jumpDivisors = new int[] { 1, 8, 2, 16, 4, 32, 3, 64 };

                for (i = 0; i < jumpDivisors.Length; i++)
                {
                    int maximum = GetMaximumLogicalScroll(host);
                    int target = i % 2 == 0
                        ? maximum - maximum / jumpDivisors[i]
                        : maximum / jumpDivisors[i];

                    SetScrollAndAssertCovered(
                        host,
                        target,
                        "fixed thumb jump " + i);
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestComplexVariableRowsCoverJumpsResizesAndReversals()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='420' Height='143' " +
                "AutoScroll='true' ItemKeyPath='Id' Virtualizing='true' " +
                "VirtualizationThreshold='1' EstimatedItemSize='96' " +
                "OverscanItems='1' VirtualizationCacheItems='16' Spacing='0' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Height='{Binding Height}' Padding='4' Spacing='2'>" +
                "      <StackPanel Orientation='Horizontal' Spacing='5'>" +
                "        <Label Width='132' Text='{Binding Title}' />" +
                "        <CheckBox Width='72' Text='Flag' Checked='{Binding Checked}' />" +
                "        <Button Width='64' Height='22' Text='{Binding Action}' />" +
                "      </StackPanel>" +
                "      <Label AutoSize='true' Text='{Binding Detail}' />" +
                "      <HyperlinkLabel AutoSize='true' Text='{Binding LinkText}' " +
                "                      NavigateUri='{Binding Url}' />" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                int[] heights =
                    new int[] { 18, 176, 32, 224, 48, 12, 132, 64, 20 };
                ArrayList rows = CreateRows(420, heights);
                int i;

                host.CreateControl();
                host.SetItems(rows);
                AssertTrue(
                    host.IsVirtualizing,
                    "complex variable stress viewport is active");

                for (i = 0; i < 140; i++)
                {
                    VirtualViewportModel model = host.DirectVirtualViewport;
                    int forwardIndex = (i * 197 + 31) % rows.Count;
                    int index = (i & 1) == 0
                        ? forwardIndex
                        : rows.Count - 1 - forwardIndex;
                    long extent = model.GetExtent(index);
                    long target =
                        model.GetOffset(index) +
                        Math.Max(0L, (extent - 1L) / 2L);

                    if (i % 11 == 0)
                    {
                        host.Size = new Size(
                            300 + (i % 4) * 53,
                            73 + (i % 5) * 29);
                    }

                    SetScrollAndAssertCovered(
                        host,
                        ToInt32(target),
                        "complex variable reversal " + i);

                    if (i % 17 == 0)
                    {
                        Application.DoEvents();
                        AssertViewportCovered(
                            host,
                            "complex variable post-resize " + i);
                    }
                }

                SetScrollAndAssertCovered(
                    host,
                    GetMaximumLogicalScroll(host),
                    "complex variable final end clamp");
                SetScrollAndAssertCovered(
                    host,
                    0,
                    "complex variable final return to start");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static ArrayList CreateRows(int count, int[] heights)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new StressRow(i, heights[i % heights.Length]));

            return rows;
        }

        private static void SetScrollAndAssertCovered(
            XamlRuntime.ItemsControl host,
            int requestedOffset,
            string message)
        {
            int normalized = Math.Max(
                0,
                Math.Min(GetMaximumLogicalScroll(host), requestedOffset));
            bool previousSuppress =
                host.DirectVirtualSuppressScrollRefresh;
            host.DirectVirtualSuppressScrollRefresh = true;

            try
            {
                host.AutoScrollPosition = new Point(0, normalized);
            }
            finally
            {
                host.DirectVirtualSuppressScrollRefresh = previousSuppress;
            }

            AssertTrue(
                host.HandleDirectVirtualViewportChanged(),
                message + " is handled synchronously by the direct viewport");
            AssertViewportCovered(host, message);
        }

        private static int GetMaximumLogicalScroll(
            XamlRuntime.ItemsControl host)
        {
            VirtualViewportModel model = host.DirectVirtualViewport;
            int viewport = host.ClientSize.Height;
            long maximum = model == null
                ? 0L
                : Math.Max(0L, model.TotalExtent - (long)viewport);

            return ToInt32(maximum);
        }

        private static int ToInt32(long value)
        {
            if (value <= 0L)
                return 0;
            if (value >= Int32.MaxValue)
                return Int32.MaxValue;

            return (int)value;
        }

        private static void AssertViewportCovered(
            XamlRuntime.ItemsControl host,
            string message)
        {
            VirtualViewportModel model = host.DirectVirtualViewport;

            AssertTrue(
                host.DirectVirtualActive && model != null,
                message + " keeps a committed direct viewport");

            int viewport = host.ClientSize.Height;

            if (model.Count == 0 || viewport <= 0 || model.TotalExtent == 0L)
                return;

            int logicalScroll = Math.Max(0, -host.AutoScrollPosition.Y);
            AssertTrue(
                (long)logicalScroll < model.TotalExtent,
                message + " keeps its origin inside logical content");

            long viewportEnd = Math.Min(
                model.TotalExtent,
                (long)logicalScroll + (long)viewport);
            int first = model.FindIndexAtOffset(logicalScroll);
            int last = model.FindIndexAtOffset(viewportEnd - 1L);

            AssertTrue(
                host.DirectVirtualRealizedStart <= first &&
                host.DirectVirtualRealizedEnd >= last,
                message + " realizes every visible logical index");

            int expectedIndex = first;
            int coveredEnd = 0;
            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                object record = host.RenderedItems[i];
                int logicalIndex = (int)GetField(record, "LogicalIndex");

                if (logicalIndex < first || logicalIndex > last)
                    continue;

                AssertEqual(
                    expectedIndex,
                    logicalIndex,
                    message + " keeps visible rows contiguous");

                Control control = GetField(record, "Control") as Control;
                AssertTrue(
                    control != null && !control.IsDisposed,
                    message + " retains the visible control tree");

                int start = control.Bounds.Top;
                int end = control.Bounds.Bottom;
                AssertTrue(
                    end > 0 && start < viewport,
                    message + " places every visible row in the client");

                if (logicalIndex == first)
                {
                    AssertTrue(
                        start <= 0,
                        message + " has no blank leading space");
                }
                else
                {
                    AssertTrue(
                        start <= coveredEnd,
                        message + " has no blank space between rows " +
                        "(index=" + logicalIndex +
                        ", start=" + start +
                        ", previousEnd=" + coveredEnd +
                        ", scroll=" + logicalScroll +
                        ", realized=" +
                        host.DirectVirtualRealizedStart + ".." +
                        host.DirectVirtualRealizedEnd +
                        ", records=" + DescribeRenderedGeometry(host) + ")");
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

        private static string DescribeRenderedGeometry(
            XamlRuntime.ItemsControl host)
        {
            string result = String.Empty;
            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                object record = host.RenderedItems[i];
                Control control = GetField(record, "Control") as Control;

                if (result.Length > 0)
                    result += ";";

                result += GetField(record, "LogicalIndex") + ":" +
                    (control == null
                        ? "null"
                        : control.Bounds + "#" + control.GetHashCode());
            }

            return result;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message + ".");
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ": expected <" + expected +
                    ">, actual <" + actual + ">.");
            }
        }
    }
}
