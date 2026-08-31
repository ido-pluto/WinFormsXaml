using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class ItemsControlWrapTests
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private sealed class WrapRow
        {
            public readonly string Id;
            public readonly int Width;
            public readonly int Height;

            public WrapRow(
                string id,
                int width,
                int height)
            {
                Id = id;
                Width = width;
                Height = height;
            }
        }

        internal static void RunAll()
        {
            TestVerticalRowsResizeWithoutRebuilding();
            TestHorizontalColumnsAndRtlProgression();
            TestJustificationAndCrossAlignment();
            TestFlexGrowBasisIsStableAcrossRepeatedLayout();
            TestVerticalWrappedScrollIntoViewStress();
            TestAnimatedVerticalWrapRetargetsAfterResize();
            TestHorizontalWrappedScrollIntoViewRtlAlignment();
            TestWrappedLayoutReusesStorageAtScale();
            TestWrappedVirtualizationIsRejectedInEitherOrder();
        }

        private static void TestVerticalRowsResizeWithoutRebuilding()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='150' Height='100' " +
                "Orientation='Vertical' Wrap='true' Spacing='5' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='{Binding Width}' " +
                "           Height='{Binding Height}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(5, 40, 20));
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);
                long disposed = host.ItemControlTreeDisposedCount;

                AssertEqual(
                    controls[0].Top,
                    controls[2].Top,
                    "three 40-pixel items fit the initial row");
                AssertEqual(
                    5,
                    controls[1].Left - controls[0].Right,
                    "vertical-row item gap");
                AssertEqual(
                    5,
                    controls[3].Top - controls[0].Bottom,
                    "vertical-row line gap");

                host.Width = 95;
                host.PerformLayout();

                AssertEqual(
                    controls[0].Top,
                    controls[1].Top,
                    "two items fit after a narrower reflow");
                AssertTrue(
                    controls[2].Top > controls[0].Bottom,
                    "the third item moves to the next row");
                AssertSameControls(
                    controls,
                    GetRenderedControls(host),
                    "resize reflow retains every native item control");
                AssertEqual(
                    disposed,
                    host.ItemControlTreeDisposedCount,
                    "resize reflow disposes no item trees");
                AssertTrue(
                    host.AutoScrollMinSize.Height >=
                        controls[4].Bottom,
                    "vertical wrapped extent contains the final row");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestHorizontalColumnsAndRtlProgression()
        {
            const string ltrMarkup =
                "<ItemsControl Name='Rows' Width='100' Height='75' " +
                "Orientation='Horizontal' Wrap='true' Spacing='5' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='30' Height='20' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime ltrRuntime = XamlRuntime.Load(ltrMarkup);

            try
            {
                XamlRuntime.ItemsControl host =
                    ltrRuntime.GetItemsControl("Rows");
                host.SetItems(CreateRows(11, 30, 20));
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);

                AssertEqual(
                    controls[0].Left,
                    controls[1].Left,
                    "items flow down the first column");
                AssertEqual(
                    5,
                    controls[1].Top - controls[0].Bottom,
                    "horizontal-column item gap");
                AssertEqual(
                    5,
                    controls[2].Left - controls[0].Right,
                    "horizontal-column line gap");
                AssertTrue(
                    host.AutoScrollMinSize.Width > host.ClientSize.Width,
                    "horizontal wrap publishes a horizontal extent");
            }
            finally
            {
                ltrRuntime.Dispose();
            }

            const string rtlMarkup =
                "<ItemsControl Name='Rows' Width='100' Height='75' " +
                "FlowDirection='RightToLeft' Orientation='Horizontal' " +
                "Wrap='true' Spacing='5' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='30' Height='20' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime rtlRuntime = XamlRuntime.Load(rtlMarkup);

            try
            {
                XamlRuntime.ItemsControl host =
                    rtlRuntime.GetItemsControl("Rows");
                host.SetItems(CreateRows(11, 30, 20));
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);

                AssertTrue(
                    controls[0].Left > controls[2].Left,
                    "RTL horizontal columns progress from right to left");
                AssertEqual(
                    controls[0].Left,
                    controls[1].Left,
                    "RTL does not reverse top-to-bottom flow within a column");
            }
            finally
            {
                rtlRuntime.Dispose();
            }
        }

        private static void TestJustificationAndCrossAlignment()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='120' Height='70' " +
                "Orientation='Vertical' Wrap='true' Spacing='4' " +
                "JustifyContent='SpaceBetween' AlignItems='End' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='{Binding Width}' " +
                "           Height='{Binding Height}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                rows.Add(new WrapRow("tall", 20, 30));
                rows.Add(new WrapRow("short", 20, 10));
                host.SetItems(rows);
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);
                Rectangle viewport = host.ClientRectangle;

                AssertEqual(
                    controls[0].Bottom,
                    controls[1].Bottom,
                    "AlignItems=End aligns the item bottoms");
                AssertEqual(
                    viewport.Left,
                    controls[0].Left,
                    "SpaceBetween leaves the first item at logical start");
                AssertEqual(
                    viewport.Right,
                    controls[1].Right,
                    "SpaceBetween leaves the final item at logical end");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestFlexGrowBasisIsStableAcrossRepeatedLayout()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='170' Height='70' " +
                "Orientation='Vertical' Wrap='true' Spacing='5' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='{Binding Width}' " +
                "           Height='{Binding Height}' FlexGrow='1' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(3, 20, 20));
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);
                Rectangle[] initial = GetBounds(controls);
                long disposed = host.ItemControlTreeDisposedCount;
                int pass;

                AssertTrue(
                    initial[0].Width > 20 &&
                    initial[1].Width > 20 &&
                    initial[2].Width > 20,
                    "FlexGrow distributes the first row's free width");

                for (pass = 0; pass < 30; pass++)
                {
                    host.Height = pass % 2 == 0 ? 71 : 70;
                    host.PerformLayout();

                    AssertBoundsEqual(
                        initial,
                        GetBounds(controls),
                        "repeated layout does not feed arranged FlexGrow widths back into the basis");
                }

                host.Width = 140;
                host.PerformLayout();
                Rectangle[] narrow = GetBounds(controls);

                AssertTrue(
                    narrow[0].Width < initial[0].Width,
                    "FlexGrow responds to a narrower viewport");

                for (pass = 0; pass < 20; pass++)
                {
                    host.PerformLayout();
                    AssertBoundsEqual(
                        narrow,
                        GetBounds(controls),
                        "the narrower FlexGrow result remains stable");
                }

                host.Width = 170;
                host.Height = 70;
                host.PerformLayout();

                AssertBoundsEqual(
                    initial,
                    GetBounds(controls),
                    "restoring the viewport restores the declared FlexGrow basis");
                AssertSameControls(
                    controls,
                    GetRenderedControls(host),
                    "FlexGrow re-layout retains native item controls");
                AssertEqual(
                    disposed,
                    host.ItemControlTreeDisposedCount,
                    "FlexGrow re-layout disposes no item trees");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestVerticalWrappedScrollIntoViewStress()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='110' Height='75' " +
                "Orientation='Vertical' Wrap='true' Spacing='4' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='30' Height='20' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(60, 30, 20));
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);
                long disposed = host.ItemControlTreeDisposedCount;
                int verticalExtent = host.AutoScrollMinSize.Height;
                int round;

                AssertTrue(
                    verticalExtent >
                        host.ItemsViewportRectangleForTest.Height,
                    "vertical wrapping publishes a scrollable vertical extent");
                AssertEqual(
                    1,
                    host.AutoScrollMinSize.Width,
                    "vertical wrapping does not invent a horizontal extent");

                for (round = 0; round < 8; round++)
                {
                    ScrollAndAssertVerticalAlignment(
                        host,
                        controls,
                        45,
                        ItemScrollAlignment.Start,
                        "fast backward/forward Start alignment");
                    ScrollAndAssertVerticalAlignment(
                        host,
                        controls,
                        6,
                        ItemScrollAlignment.Start,
                        "fast return to an early wrapped row");
                    ScrollAndAssertVerticalAlignment(
                        host,
                        controls,
                        30,
                        ItemScrollAlignment.Center,
                        "wrapped-row Center alignment");
                    ScrollAndAssertVerticalAlignment(
                        host,
                        controls,
                        12,
                        ItemScrollAlignment.End,
                        "wrapped-row End alignment");
                    ScrollAndAssertVerticalAlignment(
                        host,
                        controls,
                        57,
                        ItemScrollAlignment.End,
                        "final wrapped row reaches the viewport end");

                    host.ScrollIntoView(
                        0,
                        ItemScrollAlignment.Nearest,
                        false);
                    host.PerformLayout();
                    Application.DoEvents();

                    AssertEqual(
                        0,
                        host.GetLogicalScrollOffset(),
                        "Nearest returns immediately to the first row");
                    AssertSameControls(
                        controls,
                        GetRenderedControls(host),
                        "rapid wrapped-row scrolling retains every native item control");
                    AssertEqual(
                        verticalExtent,
                        host.AutoScrollMinSize.Height,
                        "rapid wrapped-row scrolling keeps the virtual extent stable");
                }

                AssertEqual(
                    disposed,
                    host.ItemControlTreeDisposedCount,
                    "rapid wrapped-row scrolling disposes no item trees");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestAnimatedVerticalWrapRetargetsAfterResize()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='110' Height='75' " +
                "Orientation='Vertical' Wrap='true' Spacing='4' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='30' Height='20' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(60, 30, 20));
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);
                long disposed = host.ItemControlTreeDisposedCount;
                int targetIndex = 30;

                host.ScrollIntoView(
                    targetIndex,
                    ItemScrollAlignment.Center,
                    true);

                int oldTarget =
                    host.SmoothScrollTargetOffsetForTest;

                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "wrapped item animation starts before resize");

                host.Width = 75;
                host.PerformLayout();
                Application.DoEvents();

                int resizedTarget =
                    host.SmoothScrollTargetOffsetForTest;

                AssertTrue(
                    resizedTarget != oldTarget,
                    "wrap reflow replaces the stale animated pixel target");

                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration);
                host.PerformLayout();

                Rectangle viewport =
                    host.ItemsViewportRectangleForTest;
                Rectangle targetBounds =
                    controls[targetIndex].Bounds;

                AssertTrue(
                    Math.Abs(
                        (targetBounds.Top + targetBounds.Bottom) -
                        (viewport.Top + viewport.Bottom)) <= 1,
                    "resized wrap animation finishes at the new Center alignment");
                AssertSameControls(
                    controls,
                    GetRenderedControls(host),
                    "animated wrap resize retains every item control");
                AssertEqual(
                    disposed,
                    host.ItemControlTreeDisposedCount,
                    "animated wrap resize disposes no item trees");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestHorizontalWrappedScrollIntoViewRtlAlignment()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='100' Height='80' " +
                "Orientation='Horizontal' FlowDirection='RightToLeft' " +
                "Wrap='true' Spacing='5' AlignItems='End' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='{Binding Width}' " +
                "           Height='{Binding Height}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 36; i++)
                {
                    rows.Add(
                        new WrapRow(
                            "row-" + i,
                            i % 2 == 0 ? 30 : 10,
                            20));
                }

                host.SetItems(rows);
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);
                long disposed = host.ItemControlTreeDisposedCount;
                int horizontalExtent = host.AutoScrollMinSize.Width;
                int round;

                AssertTrue(
                    horizontalExtent >
                        host.ItemsViewportRectangleForTest.Width,
                    "horizontal wrapping publishes a scrollable horizontal extent");
                AssertEqual(
                    1,
                    host.AutoScrollMinSize.Height,
                    "horizontal wrapping does not invent a vertical extent");
                AssertEqual(
                    controls[0].Left,
                    controls[1].Left,
                    "RTL AlignItems=End aligns a narrow item to the logical cross end");

                for (round = 0; round < 8; round++)
                {
                    ScrollAndAssertHorizontalRtlAlignment(
                        host,
                        controls,
                        20,
                        ItemScrollAlignment.Start,
                        "RTL wrapped-column Start alignment");
                    ScrollAndAssertHorizontalRtlAlignment(
                        host,
                        controls,
                        6,
                        ItemScrollAlignment.End,
                        "RTL wrapped-column End alignment");
                    ScrollAndAssertHorizontalRtlAlignment(
                        host,
                        controls,
                        14,
                        ItemScrollAlignment.Center,
                        "RTL wrapped-column Center alignment");
                    ScrollAndAssertHorizontalRtlAlignment(
                        host,
                        controls,
                        34,
                        ItemScrollAlignment.End,
                        "RTL final wrapped column reaches the viewport end");

                    host.ScrollIntoView(
                        0,
                        ItemScrollAlignment.Nearest,
                        false);
                    host.PerformLayout();
                    Application.DoEvents();

                    AssertEqual(
                        0,
                        host.GetLogicalScrollOffset(),
                        "RTL Nearest returns to the first logical column");
                    AssertSameControls(
                        controls,
                        GetRenderedControls(host),
                        "rapid RTL column scrolling retains every native item control");
                    AssertEqual(
                        horizontalExtent,
                        host.AutoScrollMinSize.Width,
                        "rapid RTL column scrolling keeps the virtual extent stable");
                }

                AssertEqual(
                    disposed,
                    host.ItemControlTreeDisposedCount,
                    "rapid RTL column scrolling disposes no item trees");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        internal static void TestWrappedLayoutReusesStorageAtScale()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='320' Height='120' " +
                "Orientation='Vertical' FlowDirection='RightToLeft' " +
                "Wrap='true' Spacing='2' AutoScroll='true' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Margin='0' Width='12' Height='8' " +
                "FlexGrow='1' MaxWidth='24' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.SetItems(CreateRows(1000, 12, 8));
                host.CreateControl();
                host.PerformLayout();

                Control[] controls = GetRenderedControls(host);
                XamlRuntime.WrappedItemsLayoutPlan first =
                    runtime.CreateWrappedItemsLayoutPlanForTest(
                        host,
                        new Size(320, 120),
                        null,
                        false);

                host.ResetWrappedLayoutStorageDiagnosticsForTest();

                XamlRuntime.WrappedItemsLayoutPlan second =
                    runtime.CreateWrappedItemsLayoutPlanForTest(
                        host,
                        new Size(303, 120),
                        first,
                        true);

                AssertTrue(
                    Object.ReferenceEquals(first, second),
                    "the AutoScroll retry reuses the first pass plan identity");
                AssertEqual(
                    0L,
                    host.WrappedLayoutPlanAllocationCountForTest,
                    "the second viewport pass allocates no plan");
                AssertEqual(
                    0L,
                    host.WrappedLayoutArrayAllocationCountForTest,
                    "the second viewport pass allocates no metrics, control, " +
                    "size, margin, line, or assignment array");
                AssertEqual(
                    1L,
                    host.WrappedLayoutSecondPassReuseCountForTest,
                    "the precise second-pass reuse is observed once");

                host.WrappedLayoutScratchPlan = second;
                host.ResetWrappedLayoutStorageDiagnosticsForTest();
                int pass;

                for (pass = 0; pass < 40; pass++)
                {
                    host.Width = pass % 2 == 0 ? 321 : 320;
                    host.PerformLayout();

                    AssertTrue(
                        Object.ReferenceEquals(
                            second,
                            host.WrappedLayoutScratchIdentityForTest),
                        "wrapped stress layout retains its scratch identity");
                }

                host.Width = 320;
                host.PerformLayout();

                AssertEqual(
                    0L,
                    host.WrappedLayoutPlanAllocationCountForTest,
                    "1000-row warm wrap stress allocates no new plan");
                AssertEqual(
                    0L,
                    host.WrappedLayoutArrayAllocationCountForTest,
                    "1000-row warm wrap stress allocates no layout arrays");
                AssertTrue(
                    host.WrappedLayoutScratchReuseCountForTest >= 40L,
                    "1000-row wrap stress reuses its bounded storage");
                AssertTrue(
                    Object.ReferenceEquals(
                        controls[0],
                        GetRenderedControls(host)[0]) &&
                    Object.ReferenceEquals(
                        controls[controls.Length - 1],
                        GetRenderedControls(host)[controls.Length - 1]),
                    "wrapped storage reuse retains native item identities");
                AssertTrue(
                    controls[0].Right > controls[1].Right,
                    "wrapped storage reuse preserves RTL logical order");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void ScrollAndAssertVerticalAlignment(
            XamlRuntime.ItemsControl host,
            Control[] controls,
            int index,
            ItemScrollAlignment alignment,
            string message)
        {
            host.ScrollIntoView(index, alignment, false);
            host.PerformLayout();
            Application.DoEvents();

            Rectangle viewport = host.ItemsViewportRectangleForTest;
            Rectangle bounds = controls[index].Bounds;

            AssertIntersectsViewport(
                viewport,
                bounds,
                message + " keeps the requested item visible");

            if (alignment == ItemScrollAlignment.Start)
            {
                AssertEqual(
                    viewport.Top,
                    bounds.Top,
                    message);
            }
            else if (alignment == ItemScrollAlignment.Center)
            {
                AssertTrue(
                    Math.Abs(
                        (bounds.Top + bounds.Bottom) -
                        (viewport.Top + viewport.Bottom)) <= 1,
                    message + " centers the item");
            }
            else if (alignment == ItemScrollAlignment.End)
            {
                AssertEqual(
                    viewport.Bottom,
                    bounds.Bottom,
                    message);
            }
        }

        private static void ScrollAndAssertHorizontalRtlAlignment(
            XamlRuntime.ItemsControl host,
            Control[] controls,
            int index,
            ItemScrollAlignment alignment,
            string message)
        {
            host.ScrollIntoView(index, alignment, false);
            host.PerformLayout();
            Application.DoEvents();

            Rectangle viewport = host.ItemsViewportRectangleForTest;
            Rectangle bounds = controls[index].Bounds;

            AssertIntersectsViewport(
                viewport,
                bounds,
                message + " keeps the requested item visible");

            if (alignment == ItemScrollAlignment.Start)
            {
                AssertEqual(
                    viewport.Right,
                    bounds.Right,
                    message);
            }
            else if (alignment == ItemScrollAlignment.Center)
            {
                AssertTrue(
                    Math.Abs(
                        (bounds.Left + bounds.Right) -
                        (viewport.Left + viewport.Right)) <= 1,
                    message + " centers the item");
            }
            else if (alignment == ItemScrollAlignment.End)
            {
                AssertEqual(
                    viewport.Left,
                    bounds.Left,
                    message);
            }
        }

        private static void AssertIntersectsViewport(
            Rectangle viewport,
            Rectangle bounds,
            string message)
        {
            AssertTrue(
                Rectangle.Intersect(viewport, bounds).Width > 0 &&
                Rectangle.Intersect(viewport, bounds).Height > 0,
                message);
        }

        private static void
            TestWrappedVirtualizationIsRejectedInEitherOrder()
        {
            using (ItemsControl wrapFirst = new ItemsControl())
            {
                wrapFirst.Wrap = true;

                AssertThrowsInvalidOperation(
                    delegate { wrapFirst.Virtualizing = true; },
                    "Wrap then Virtualizing is rejected");
                AssertTrue(
                    !wrapFirst.Virtualizing,
                    "rejected Virtualizing assignment rolls back");
            }

            using (ItemsControl virtualFirst = new ItemsControl())
            {
                virtualFirst.Virtualizing = true;

                AssertThrowsInvalidOperation(
                    delegate { virtualFirst.Wrap = true; },
                    "Virtualizing then Wrap is rejected");
                AssertTrue(
                    !virtualFirst.Wrap,
                    "rejected Wrap assignment rolls back");
            }

            try
            {
                XamlRuntime.Load(
                    "<ItemsControl Wrap='true' Virtualizing='true'>" +
                    "  <ItemsControl.ItemTemplate>" +
                    "    <Label Text='{Binding .}' />" +
                    "  </ItemsControl.ItemTemplate>" +
                    "</ItemsControl>");
            }
            catch (Exception ex)
            {
                AssertTrue(
                    ex.ToString().IndexOf(
                        "Wrapped virtualization",
                        StringComparison.Ordinal) >= 0 ||
                    ex.ToString().IndexOf(
                        "cannot be combined",
                        StringComparison.Ordinal) >= 0,
                    "markup reports the unsupported combination clearly");
                return;
            }

            throw new InvalidOperationException(
                "Wrap plus Virtualizing markup should fail.");
        }

        private static ArrayList CreateRows(
            int count,
            int width,
            int height)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
            {
                rows.Add(
                    new WrapRow(
                        "row-" + i,
                        width,
                        height));
            }

            return rows;
        }

        private static Control[] GetRenderedControls(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo renderedField =
                typeof(XamlRuntime.ItemsControl).GetField(
                    "RenderedItems",
                    InstanceMembers);
            IList records = (IList)renderedField.GetValue(host);
            Control[] controls = new Control[records.Count];
            int i;

            for (i = 0; i < records.Count; i++)
            {
                object record = records[i];
                FieldInfo controlField = record.GetType().GetField(
                    "Control",
                    InstanceMembers);

                controls[i] = (Control)controlField.GetValue(record);
            }

            return controls;
        }

        private static Rectangle[] GetBounds(Control[] controls)
        {
            Rectangle[] bounds = new Rectangle[controls.Length];
            int i;

            for (i = 0; i < controls.Length; i++)
                bounds[i] = controls[i].Bounds;

            return bounds;
        }

        private static void AssertBoundsEqual(
            Rectangle[] expected,
            Rectangle[] actual,
            string message)
        {
            AssertEqual(expected.Length, actual.Length, message + " count");
            int i;

            for (i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    throw new InvalidOperationException(
                        message + " at index " + i +
                        ": expected " + expected[i] +
                        ", actual " + actual[i] + ".");
                }
            }
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
                if (!Object.ReferenceEquals(expected[i], actual[i]))
                {
                    throw new InvalidOperationException(
                        message + " at index " + i + ".");
                }
            }
        }

        private static void AssertThrowsInvalidOperation(
            MethodInvoker action,
            string message)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new InvalidOperationException(
                message + ": expected InvalidOperationException.");
        }

        private static void AssertTrue(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message + ".");
        }

        private static void AssertEqual(
            long expected,
            long actual,
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
