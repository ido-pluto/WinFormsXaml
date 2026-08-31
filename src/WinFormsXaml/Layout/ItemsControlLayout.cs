using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private struct ItemsContentMeasurement
        {
            public Size ContentSize;
            public int VisibleCount;
            public bool Stable;

            public ItemsContentMeasurement(
                Size contentSize,
                int visibleCount)
            {
                ContentSize = contentSize;
                VisibleCount = visibleCount;
                Stable = true;
            }

            public ItemsContentMeasurement(
                Size contentSize,
                int visibleCount,
                bool stable)
            {
                ContentSize = contentSize;
                VisibleCount = visibleCount;
                Stable = stable;
            }
        }

        /// <summary>
        /// One retained, non-virtual wrapped layout pass. FlexLayoutPlan stores
        /// dense visible-item line membership; the parallel arrays preserve the
        /// already-measured native controls without re-reading bindings.
        /// </summary>
        internal sealed class WrappedItemsLayoutPlan
        {
            internal FlexLayoutPlan Flex;
            internal Control[] Controls;
            internal Size[] Desired;
            internal Padding[] Margins;
            internal int RetainedControlCount;
            internal bool RowFlow;
            internal int AvailableMain;
        }

        private static bool IsItemsLayoutSnapshotCurrent(
            ItemsControl host,
            ArrayList records,
            int recordCount,
            int generation,
            long publicationRevision,
            XamlRuntime runtime)
        {
            return host != null &&
                   !host.IsDisposed &&
                   !host.Disposing &&
                   runtime != null &&
                   !runtime.IsDisposed &&
                   Object.ReferenceEquals(host.Runtime, runtime) &&
                   host.RefreshGeneration == generation &&
                   Object.ReferenceEquals(host.RenderedItems, records) &&
                   (records == null ? 0 : records.Count) == recordCount &&
                   host.RenderedItemPublicationRevision ==
                       publicationRevision;
        }

        private int GetRenderedItemCount(
            ItemsControl host,
            ArrayList records)
        {
            if (records != null)
                return records.Count;

            return host.Controls.Count;
        }

        private Control GetRenderedItemControl(
            ItemsControl host,
            ArrayList records,
            int index)
        {
            if (records != null)
            {
                RenderedItemRecord record =
                    records[index] as RenderedItemRecord;

                return record == null
                    ? null
                    : record.Control;
            }

            return host.Controls[index];
        }

        private bool LayoutItemsControl(ItemsControl host)
        {
            if (host == null ||
                host.IsDisposed ||
                host.Disposing ||
                IsDisposed ||
                !Object.ReferenceEquals(host.Runtime, this))
            {
                return false;
            }

            if (host.LightweightActive)
            {
                HandleLightweightViewportChanged(host);
                return !host.IsDisposed &&
                       !host.Disposing &&
                       !IsDisposed &&
                       Object.ReferenceEquals(host.Runtime, this);
            }

            if (host.DirectVirtualActive)
            {
                host.HandleDirectVirtualViewportChanged();
                return !host.IsDisposed &&
                       !host.Disposing &&
                       !IsDisposed &&
                       Object.ReferenceEquals(host.Runtime, this);
            }

            ArrayList layoutRecords = host.RenderedItems;
            int layoutRecordCount = layoutRecords == null
                ? 0
                : layoutRecords.Count;
            int layoutGeneration = host.RefreshGeneration;
            long layoutPublicationRevision =
                host.RenderedItemPublicationRevision;
            XamlRuntime layoutRuntime = this;

            if (!IsItemsLayoutSnapshotCurrent(
                    host,
                    layoutRecords,
                    layoutRecordCount,
                    layoutGeneration,
                    layoutPublicationRevision,
                    layoutRuntime))
            {
                return false;
            }

            if (host.Wrap)
            {
                return LayoutWrappedItemsControl(
                    host,
                    layoutRecords,
                    layoutRecordCount,
                    layoutGeneration,
                    layoutPublicationRevision,
                    layoutRuntime);
            }

            Rectangle viewport = host.GetItemsViewportRectangle();
            ItemsContentMeasurement measured =
                new ItemsContentMeasurement(
                    Size.Empty,
                    0);
            bool contentMeasured = false;

            // Explicitly own the virtual extent instead of allowing native
            // ScrollableControl child-bound heuristics to invent a horizontal
            // range. This is particularly important for RTL text and AutoSize
            // labels, whose preferred width can be much wider than the viewport.
            if (host.AutoScroll)
            {
                int pass;

                for (pass = 0; pass < 2; pass++)
                {
                    long collapseRevision =
                        CaptureElementCollapseRevision();
                    ItemsContentMeasurement content =
                        MeasureItemsContentSize(
                            host,
                            viewport.Size,
                            layoutRecords,
                            layoutRecordCount,
                            layoutGeneration,
                            layoutPublicationRevision,
                            layoutRuntime);

                    if (!content.Stable ||
                        !IsItemsLayoutSnapshotCurrent(
                            host,
                            layoutRecords,
                            layoutRecordCount,
                            layoutGeneration,
                            layoutPublicationRevision,
                            layoutRuntime))
                    {
                        return false;
                    }

                    measured = content;
                    contentMeasured = true;

                    // IMPORTANT (.NET Framework / legacy WinForms):
                    // ScrollableControl's layout path contains an all-or-nothing check
                    // for AutoScrollMinSize.Width != 0 && Height != 0. Using (0, H) for
                    // a vertical list can therefore work initially, disappear when the
                    // window grows, and then fail to reappear when the window shrinks.
                    // Keep the non-scrolling axis at 1 pixel: non-zero for the native
                    // bookkeeping, but far too small to create a scrollbar on that axis.
                    bool hasRenderedItems =
                        ElementCollapseRevisionChanged(collapseRevision)
                            ? HasLayoutItems(
                                host,
                                layoutRecords)
                            : content.VisibleCount > 0;

                    Size virtualSize;

                    if (!hasRenderedItems)
                    {
                        virtualSize = Size.Empty;
                    }
                    else if (host.Orientation == Orientation.Vertical)
                    {
                        virtualSize = new Size(
                            1,
                            Math.Max(1, content.ContentSize.Height));
                    }
                    else
                    {
                        virtualSize = new Size(
                            Math.Max(1, content.ContentSize.Width),
                            1);
                    }

                    if (host.AutoScrollMinSize == virtualSize)
                        break;

                    host.AutoScrollMinSize = virtualSize;

                    if (!IsItemsLayoutSnapshotCurrent(
                            host,
                            layoutRecords,
                            layoutRecordCount,
                            layoutGeneration,
                            layoutPublicationRevision,
                            layoutRuntime))
                    {
                        return false;
                    }

                    viewport = host.GetItemsViewportRectangle();
                }
            }

            // ScrollableControl physically moves its children when the display
            // origin changes. Current AutoScrollPosition is therefore the virtual
            // origin to use whenever a layout pass occurs while already scrolled.
            if (!contentMeasured)
            {
                measured = MeasureItemsContentSize(
                    host,
                    viewport.Size,
                    layoutRecords,
                    layoutRecordCount,
                    layoutGeneration,
                    layoutPublicationRevision,
                    layoutRuntime);

                if (!measured.Stable ||
                    !IsItemsLayoutSnapshotCurrent(
                        host,
                        layoutRecords,
                        layoutRecordCount,
                        layoutGeneration,
                        layoutPublicationRevision,
                        layoutRuntime))
                {
                    return false;
                }

                contentMeasured = true;
            }

            Size measuredContent = measured.ContentSize;

            Point scroll = host.AutoScrollPosition;
            int originX = host.AutoScroll ? scroll.X : 0;
            int originY = host.AutoScroll ? scroll.Y : 0;

            // The native child-bound marker is the authoritative fallback for
            // grow -> hide scrollbar -> shrink -> show scrollbar transitions.
            // Unlike cached AutoScrollMinSize/displayRect state, child bounds are
            // re-evaluated by ScrollableControl on every native layout.
            host.UpdateScrollExtentMarker(
                measuredContent,
                new Point(originX, originY));

            if (!IsItemsLayoutSnapshotCurrent(
                    host,
                    layoutRecords,
                    layoutRecordCount,
                    layoutGeneration,
                    layoutPublicationRevision,
                    layoutRuntime))
            {
                return false;
            }

            // The viewport origin already contains the host padding and any
            // framework-owned strip reserved on the left/top edge. Add only
            // the native scroll display origin here; adding Padding again
            // would miss a left-side RTL scrollbar and double-count padding.
            int contentLeft =
                originX + viewport.X;

            int contentTop =
                originY + viewport.Y;

            int availableWidth = Math.Max(
                0,
                viewport.Width);

            int availableHeight = Math.Max(
                0,
                viewport.Height);

            Size measureSize =
                new Size(
                    availableWidth,
                    availableHeight);

            bool rtl = host.ContentRightToLeft;
            int measuredContentWidth = ClampNonnegativeLongToInt(
                Math.Max(
                    0L,
                    (long)measuredContent.Width -
                        (long)host.Padding.Left -
                        (long)host.Padding.Right));
            int rtlContentWidth = Math.Max(
                availableWidth,
                measuredContentWidth);

            long cursor =
                host.Orientation == Orientation.Vertical
                    ? contentTop
                    : (rtl
                        // RTL item zero is anchored to the full content's
                        // right edge, not the current viewport's right edge.
                        // With native P=M-L this keeps every row in positive
                        // content coordinates and makes the logical end
                        // reachable without a blank tail.
                        ? (long)ClampLongToInt(
                            (long)contentLeft +
                            (long)rtlContentWidth)
                        : contentLeft);

            int visibleIndex = 0;
            int count = GetRenderedItemCount(
                host,
                layoutRecords);
            int i;

            for (i = 0; i < count; i++)
            {
                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        layoutRecords,
                        layoutRecordCount,
                        layoutGeneration,
                        layoutPublicationRevision,
                        layoutRuntime))
                {
                    return false;
                }

                Control child =
                    GetRenderedItemControl(
                        host,
                        layoutRecords,
                        i);

                if (child == null || child.IsDisposed)
                    continue;

                ElementInfo info = GetInfo(child);

                if (info.Collapsed)
                {
                    SetBoundsIfChanged(child, Rectangle.Empty);

                    if (!IsItemsLayoutSnapshotCurrent(
                            host,
                            layoutRecords,
                            layoutRecordCount,
                            layoutGeneration,
                            layoutPublicationRevision,
                            layoutRuntime))
                    {
                        return false;
                    }

                    continue;
                }

                if (visibleIndex > 0)
                {
                    if (host.Orientation == Orientation.Vertical)
                    {
                        cursor = SaturatingAddSigned(
                            cursor,
                            host.Spacing);
                    }
                    else if (rtl)
                    {
                        cursor = SaturatingAddSigned(
                            cursor,
                            -(long)host.Spacing);
                    }
                    else
                    {
                        cursor = SaturatingAddSigned(
                            cursor,
                            host.Spacing);
                    }
                }

                Padding margin =
                    GetEffectiveMargin(
                        child,
                        info.Margin);

                RenderedItemRecord measureRecord =
                    layoutRecords != null && i < layoutRecords.Count
                        ? layoutRecords[i] as RenderedItemRecord
                        : null;

                Size childMeasureSize = new Size(
                    Math.Max(0, measureSize.Width - margin.Left - margin.Right),
                    Math.Max(0, measureSize.Height - margin.Top - margin.Bottom));

                Size desired =
                    GetCachedDesiredSize(
                        host,
                        measureRecord,
                        child,
                        childMeasureSize,
                        true);

                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        layoutRecords,
                        layoutRecordCount,
                        layoutGeneration,
                        layoutPublicationRevision,
                        layoutRuntime))
                {
                    return false;
                }

                if (host.Orientation == Orientation.Vertical)
                {
                    int totalHeight = ClampNonnegativeLongToInt(
                        SaturatingAddNonnegative(
                            desired.Height,
                            SaturatingAddNonnegative(
                                margin.Top,
                                margin.Bottom)));

                    Rectangle slot =
                        new Rectangle(
                            contentLeft,
                            ClampLongToInt(cursor),
                            availableWidth,
                            totalHeight);

                    LayoutControlInSlotWithDesired(
                        child,
                        slot,
                        true,
                        false,
                        desired);

                    cursor = SaturatingAddSigned(
                        cursor,
                        totalHeight);
                }
                else
                {
                    int totalWidth = ClampNonnegativeLongToInt(
                        SaturatingAddNonnegative(
                            desired.Width,
                            SaturatingAddNonnegative(
                                margin.Left,
                                margin.Right)));

                    Rectangle slot;

                    if (rtl)
                    {
                        slot = new Rectangle(
                            ClampLongToInt(
                                SaturatingAddSigned(
                                    cursor,
                                    -(long)totalWidth)),
                            contentTop,
                            totalWidth,
                            availableHeight);

                        cursor = SaturatingAddSigned(
                            cursor,
                            -(long)totalWidth);
                    }
                    else
                    {
                        slot = new Rectangle(
                            ClampLongToInt(cursor),
                            contentTop,
                            totalWidth,
                            availableHeight);

                        cursor = SaturatingAddSigned(
                            cursor,
                            totalWidth);
                    }

                    LayoutControlInSlotWithDesired(
                        child,
                        slot,
                        false,
                        true,
                        desired);
                }

                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        layoutRecords,
                        layoutRecordCount,
                        layoutGeneration,
                        layoutPublicationRevision,
                        layoutRuntime))
                {
                    return false;
                }

                visibleIndex++;
            }

            return true;
        }

        private bool LayoutWrappedItemsControl(
            ItemsControl host,
            ArrayList records,
            int recordCount,
            int generation,
            long publicationRevision,
            XamlRuntime runtime)
        {
            bool ownsScratch;
            WrappedItemsLayoutPlan reusablePlan =
                LeaseWrappedItemsLayoutScratch(
                    host,
                    out ownsScratch);
            WrappedItemsLayoutPlan wrapped = null;

            try
            {
                Rectangle viewport =
                    host.GetItemsViewportRectangle();
                int pass;

                for (pass = 0; pass < 2; pass++)
                {
                    wrapped = CreateWrappedItemsLayoutPlan(
                        host,
                        viewport.Size,
                        true,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime,
                        reusablePlan,
                        pass > 0);

                    if (wrapped == null ||
                        !IsItemsLayoutSnapshotCurrent(
                            host,
                            records,
                            recordCount,
                            generation,
                            publicationRevision,
                            runtime))
                    {
                        return false;
                    }

                    reusablePlan = wrapped;

                    if (!host.AutoScroll)
                        break;

                    Size virtualSize =
                        GetWrappedItemsVirtualSize(host, wrapped);

                    if (host.AutoScrollMinSize == virtualSize)
                        break;

                    host.AutoScrollMinSize = virtualSize;

                    if (!IsItemsLayoutSnapshotCurrent(
                            host,
                            records,
                            recordCount,
                            generation,
                            publicationRevision,
                            runtime))
                    {
                        return false;
                    }

                    Rectangle nextViewport =
                        host.GetItemsViewportRectangle();

                    if (nextViewport.Size == viewport.Size)
                    {
                        viewport = nextViewport;
                        break;
                    }

                    viewport = nextViewport;
                }

                if (wrapped == null)
                    return false;

                Point scroll = host.AutoScrollPosition;
                int originX = host.AutoScroll ? scroll.X : 0;
                int originY = host.AutoScroll ? scroll.Y : 0;
                Size contentSize =
                    GetWrappedItemsContentSize(
                        host,
                        wrapped,
                        viewport.Size);

                host.UpdateScrollExtentMarker(
                    contentSize,
                    new Point(originX, originY));

                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime))
                {
                    return false;
                }

                return ArrangeWrappedItemsLayout(
                    host,
                    wrapped,
                    viewport,
                    originX,
                    originY,
                    records,
                    recordCount,
                    generation,
                    publicationRevision,
                    runtime);
            }
            finally
            {
                ReturnWrappedItemsLayoutScratch(
                    host,
                    ownsScratch,
                    reusablePlan);
            }
        }

        private static WrappedItemsLayoutPlan
            LeaseWrappedItemsLayoutScratch(
                ItemsControl host,
                out bool ownsScratch)
        {
            ownsScratch = false;

            if (host == null || host.WrappedLayoutScratchInUse)
                return null;

            host.WrappedLayoutScratchInUse = true;
            ownsScratch = true;

            WrappedItemsLayoutPlan plan =
                host.WrappedLayoutScratchPlan;

            host.WrappedLayoutScratchPlan = null;
            return plan;
        }

        private static void ReturnWrappedItemsLayoutScratch(
            ItemsControl host,
            bool ownsScratch,
            WrappedItemsLayoutPlan plan)
        {
            if (!ownsScratch || host == null)
                return;

            if (host.IsDisposed ||
                host.Disposing ||
                host.Runtime == null ||
                !CanRetainWrappedItemsLayoutPlan(plan))
            {
                host.WrappedLayoutScratchPlan = null;
            }
            else
            {
                host.WrappedLayoutScratchPlan = plan;
            }

            host.WrappedLayoutScratchInUse = false;
        }

        private static bool CanRetainWrappedItemsLayoutPlan(
            WrappedItemsLayoutPlan plan)
        {
            if (plan == null)
                return false;

            int maximum =
                FlexLayoutPlanner.MaximumRetainedItemCapacity;

            return (plan.Controls == null ||
                    plan.Controls.Length <= maximum) &&
                   (plan.Desired == null ||
                    plan.Desired.Length <= maximum) &&
                   (plan.Margins == null ||
                    plan.Margins.Length <= maximum) &&
                   CanRetainFlexLayoutPlan(plan.Flex);
        }

        private WrappedItemsLayoutPlan CreateWrappedItemsLayoutPlan(
            ItemsControl host,
            Size available,
            bool clearCollapsedBounds,
            ArrayList records,
            int recordCount,
            int generation,
            long publicationRevision,
            XamlRuntime runtime,
            WrappedItemsLayoutPlan reusablePlan,
            bool secondPass)
        {
            if (!IsItemsLayoutSnapshotCurrent(
                    host,
                    records,
                    recordCount,
                    generation,
                    publicationRevision,
                    runtime))
            {
                return null;
            }

            int count = GetRenderedItemCount(host, records);
            WrappedItemsLayoutPlan result =
                reusablePlan == null
                    ? new WrappedItemsLayoutPlan()
                    : reusablePlan;
            FlexLayoutPlan previousFlex = result.Flex;
            int capacity =
                FlexLayoutPlanner.GetScratchCapacity(count);
            FlexLayoutItemMetrics[] metrics;
            Control[] controls;
            Size[] desiredSizes;
            Padding[] margins;
#if !WINFORMSXAML_PACKAGE
            int arrayAllocationCount = 0;
            int[] previousAssigned = previousFlex == null
                ? null
                : previousFlex.AssignedMain;
            FlexLayoutLine[] previousLines = previousFlex == null
                ? null
                : previousFlex.Lines;
#endif

            if (previousFlex != null &&
                previousFlex.Items != null &&
                previousFlex.Items.Length >= count)
            {
                metrics = previousFlex.Items;
            }
            else
            {
                metrics =
                    new FlexLayoutItemMetrics[capacity];
#if !WINFORMSXAML_PACKAGE
                arrayAllocationCount++;
#endif
            }

            if (result.Controls != null &&
                result.Controls.Length >= count)
            {
                controls = result.Controls;
            }
            else
            {
                controls = new Control[capacity];
#if !WINFORMSXAML_PACKAGE
                arrayAllocationCount++;
#endif
            }

            if (result.Desired != null &&
                result.Desired.Length >= count)
            {
                desiredSizes = result.Desired;
            }
            else
            {
                desiredSizes = new Size[capacity];
#if !WINFORMSXAML_PACKAGE
                arrayAllocationCount++;
#endif
            }

            if (result.Margins != null &&
                result.Margins.Length >= count)
            {
                margins = result.Margins;
            }
            else
            {
                margins = new Padding[capacity];
#if !WINFORMSXAML_PACKAGE
                arrayAllocationCount++;
#endif
            }

            result.Controls = controls;
            result.Desired = desiredSizes;
            result.Margins = margins;
            bool rowFlow =
                host.Orientation == Orientation.Vertical;
            bool rtl = host.ContentRightToLeft;
            int visible = 0;
            int sourceIndex;

            for (sourceIndex = 0; sourceIndex < count; sourceIndex++)
            {
                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime))
                {
                    return null;
                }

                Control child =
                    GetRenderedItemControl(
                        host,
                        records,
                        sourceIndex);

                if (child == null || child.IsDisposed)
                    continue;

                ElementInfo info = GetInfo(child);

                if (info.Collapsed)
                {
                    if (clearCollapsedBounds)
                    {
                        SetBoundsIfChanged(
                            child,
                            Rectangle.Empty);
                    }

                    continue;
                }

                Padding margin =
                    GetEffectiveMargin(
                        child,
                        info.Margin);
                Size proposed = new Size(
                    Math.Max(
                        0,
                        available.Width -
                        margin.Left -
                        margin.Right),
                    Math.Max(
                        0,
                        available.Height -
                        margin.Top -
                        margin.Bottom));
                RenderedItemRecord record =
                    records != null && sourceIndex < records.Count
                        ? records[sourceIndex] as RenderedItemRecord
                        : null;
                Size desired =
                    GetCachedDesiredSize(
                        host,
                        record,
                        child,
                        proposed,
                        true);

                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime))
                {
                    return null;
                }

                desired.Width =
                    ApplyWidthLimits(child, desired.Width);
                desired.Height =
                    ApplyHeightLimits(child, desired.Height);

                bool flexibleMain =
                    info.FlexGrow > 0.0f &&
                    (rowFlow
                        ? !info.WidthExplicit
                        : !info.HeightExplicit);
                int basisMain = flexibleMain
                    ? 0
                    : GetFlexMainBasis(
                        child,
                        info,
                        rowFlow,
                        desired);

                basisMain = rowFlow
                    ? ApplyWidthLimits(child, basisMain)
                    : ApplyHeightLimits(child, basisMain);

                int crossSize = rowFlow
                    ? desired.Height
                    : desired.Width;
                int maxMain = rowFlow
                    ? child.MaximumSize.Width
                    : child.MaximumSize.Height;

                int mainLeading;
                int mainTrailing;
                int crossLeading;
                int crossTrailing;

                if (rowFlow)
                {
                    mainLeading = rtl
                        ? margin.Right
                        : margin.Left;
                    mainTrailing = rtl
                        ? margin.Left
                        : margin.Right;
                    crossLeading = margin.Top;
                    crossTrailing = margin.Bottom;
                }
                else
                {
                    mainLeading = margin.Top;
                    mainTrailing = margin.Bottom;
                    crossLeading = rtl
                        ? margin.Right
                        : margin.Left;
                    crossTrailing = rtl
                        ? margin.Left
                        : margin.Right;
                }

                metrics[visible] =
                    new FlexLayoutItemMetrics(
                        sourceIndex,
                        basisMain,
                        crossSize,
                        mainLeading,
                        mainTrailing,
                        crossLeading,
                        crossTrailing,
                        info.FlexGrow,
                        maxMain);
                controls[visible] = child;
                desiredSizes[visible] = desired;
                margins[visible] = margin;
                visible++;
            }

            if (result.RetainedControlCount > visible)
            {
                int clearCount = Math.Min(
                    result.RetainedControlCount,
                    controls.Length) - visible;

                if (clearCount > 0)
                {
                    Array.Clear(
                        controls,
                        visible,
                        clearCount);
                }
            }

            result.RetainedControlCount = visible;

            int availableMain = Math.Max(
                0,
                rowFlow
                    ? available.Width
                    : available.Height);
            bool constrainMain = availableMain > 0;
            FlexLayoutPlan flex =
                FlexLayoutPlanner.Create(
                    metrics,
                    visible,
                    availableMain,
                    constrainMain,
                    true,
                    host.Spacing,
                    previousFlex);

            FlexLayoutPlanner.AllocateGrow(
                flex,
                availableMain,
                host.Spacing);

            result.Flex = flex;
            result.RowFlow = rowFlow;
            result.AvailableMain = availableMain;

#if !WINFORMSXAML_PACKAGE
            if (!Object.ReferenceEquals(
                    previousAssigned,
                    flex.AssignedMain))
            {
                arrayAllocationCount++;
            }

            if (!Object.ReferenceEquals(
                    previousLines,
                    flex.Lines))
            {
                arrayAllocationCount++;
            }

            host.RecordWrappedLayoutStorageForTest(
                Object.ReferenceEquals(
                    reusablePlan,
                    result),
                arrayAllocationCount,
                secondPass &&
                Object.ReferenceEquals(
                    reusablePlan,
                    result));
#endif

            return result;
        }

#if !WINFORMSXAML_PACKAGE
        internal WrappedItemsLayoutPlan
            CreateWrappedItemsLayoutPlanForTest(
                ItemsControl host,
                Size available,
                WrappedItemsLayoutPlan reusablePlan,
                bool secondPass)
        {
            ArrayList records = host.RenderedItems;

            return CreateWrappedItemsLayoutPlan(
                host,
                available,
                true,
                records,
                records == null ? 0 : records.Count,
                host.RefreshGeneration,
                host.RenderedItemPublicationRevision,
                this,
                reusablePlan,
                secondPass);
        }
#endif

        private static Size GetWrappedItemsVirtualSize(
            ItemsControl host,
            WrappedItemsLayoutPlan wrapped)
        {
            if (wrapped.Flex.ItemCount == 0)
                return Size.Empty;

            if (wrapped.RowFlow)
            {
                int height = FlexLayoutPlanner.SumExtent(
                    wrapped.Flex.PreferredCross,
                    host.Padding.Top,
                    host.Padding.Bottom);

                return new Size(1, Math.Max(1, height));
            }

            int width = FlexLayoutPlanner.SumExtent(
                wrapped.Flex.PreferredCross,
                host.Padding.Left,
                host.Padding.Right);

            return new Size(Math.Max(1, width), 1);
        }

        private static Size GetWrappedItemsContentSize(
            ItemsControl host,
            WrappedItemsLayoutPlan wrapped,
            Size viewport)
        {
            if (wrapped.Flex.ItemCount == 0)
                return Size.Empty;

            if (wrapped.RowFlow)
            {
                return new Size(
                    Math.Max(1, viewport.Width),
                    Math.Max(
                        1,
                        FlexLayoutPlanner.SumExtent(
                            wrapped.Flex.PreferredCross,
                            host.Padding.Top,
                            host.Padding.Bottom)));
            }

            return new Size(
                Math.Max(
                    1,
                    FlexLayoutPlanner.SumExtent(
                        wrapped.Flex.PreferredCross,
                        host.Padding.Left,
                        host.Padding.Right)),
                Math.Max(1, viewport.Height));
        }

        private bool ArrangeWrappedItemsLayout(
            ItemsControl host,
            WrappedItemsLayoutPlan wrapped,
            Rectangle viewport,
            int originX,
            int originY,
            ArrayList records,
            int recordCount,
            int generation,
            long publicationRevision,
            XamlRuntime runtime)
        {
            bool rtl = host.ContentRightToLeft;
            int lineIndex;

            for (lineIndex = 0;
                 lineIndex < wrapped.Flex.LineCount;
                 lineIndex++)
            {
                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime))
                {
                    return false;
                }

                FlexLayoutLine line =
                    wrapped.Flex.Lines[lineIndex];
                Rectangle lineRectangle;

                if (wrapped.RowFlow)
                {
                    lineRectangle = new Rectangle(
                        viewport.Left,
                        ClampLongToInt(
                            (long)originY +
                            (long)viewport.Top +
                            (long)line.CrossOffset),
                        viewport.Width,
                        line.CrossSize);
                }
                else
                {
                    int x;

                    if (rtl)
                    {
                        int contentCross = Math.Max(
                            viewport.Width,
                            wrapped.Flex.PreferredCross);
                        long right =
                            (long)originX +
                            (long)viewport.Left +
                            (long)contentCross -
                            (long)line.CrossOffset;

                        x = ClampLongToInt(
                            right -
                            (long)line.CrossSize);
                    }
                    else
                    {
                        x = ClampLongToInt(
                            (long)originX +
                            (long)viewport.Left +
                            (long)line.CrossOffset);
                    }

                    lineRectangle = new Rectangle(
                        x,
                        viewport.Top,
                        line.CrossSize,
                        viewport.Height);
                }

                if (!ArrangeWrappedItemsLine(
                        host,
                        wrapped,
                        line,
                        lineRectangle,
                        rtl,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ArrangeWrappedItemsLine(
            ItemsControl host,
            WrappedItemsLayoutPlan wrapped,
            FlexLayoutLine line,
            Rectangle lineRectangle,
            bool rtl,
            ArrayList records,
            int recordCount,
            int generation,
            long publicationRevision,
            XamlRuntime runtime)
        {
            int free = Math.Max(
                0,
                wrapped.AvailableMain -
                line.AssignedMain);
            int leading = 0;
            int between = host.Spacing;

            if (host.JustifyContent == FlexJustifyContent.Center)
            {
                leading = free / 2;
            }
            else if (host.JustifyContent == FlexJustifyContent.End)
            {
                leading = free;
            }
            else if (host.JustifyContent ==
                     FlexJustifyContent.SpaceBetween &&
                     line.ItemCount > 1)
            {
                between = FlexLayoutPlanner.AddClamped(
                    host.Spacing,
                    free / (line.ItemCount - 1));
            }
            else if (host.JustifyContent ==
                     FlexJustifyContent.SpaceAround)
            {
                int extra = line.ItemCount > 0
                    ? free / line.ItemCount
                    : 0;

                leading = extra / 2;
                between = FlexLayoutPlanner.AddClamped(
                    host.Spacing,
                    extra);
            }

            int logicalCursor = leading;
            int end = line.ItemStart + line.ItemCount;
            int itemIndex;

            for (itemIndex = line.ItemStart;
                 itemIndex < end;
                 itemIndex++)
            {
                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime))
                {
                    return false;
                }

                Control child = wrapped.Controls[itemIndex];

                if (child == null || child.IsDisposed)
                    return false;

                ElementInfo info = GetInfo(child);
                FlexLayoutItemMetrics metrics =
                    wrapped.Flex.Items[itemIndex];
                Padding margin = wrapped.Margins[itemIndex];
                Size desired = wrapped.Desired[itemIndex];
                int assignedMain =
                    wrapped.Flex.AssignedMain[itemIndex];
                int width = wrapped.RowFlow
                    ? assignedMain
                    : desired.Width;
                int height = wrapped.RowFlow
                    ? desired.Height
                    : assignedMain;
                int x;
                int y;

                if (wrapped.RowFlow)
                {
                    width = ApplyWidthLimits(child, width);

                    if (host.AlignItems == FlexAlignItems.Stretch &&
                        !info.HeightExplicit)
                    {
                        height = Math.Max(
                            0,
                            line.CrossSize -
                            margin.Top -
                            margin.Bottom);
                    }

                    height = ApplyHeightLimits(child, height);

                    if (rtl)
                    {
                        long right =
                            (long)lineRectangle.Right -
                            (long)logicalCursor -
                            (long)metrics.MainLeadingMargin;
                        x = ClampLongToInt(right - width);
                    }
                    else
                    {
                        x = ClampLongToInt(
                            (long)lineRectangle.Left +
                            (long)logicalCursor +
                            (long)metrics.MainLeadingMargin);
                    }

                    y = GetWrappedCrossPosition(
                        lineRectangle.Top,
                        line.CrossSize,
                        height,
                        margin.Top,
                        margin.Bottom,
                        host.AlignItems,
                        false);
                }
                else
                {
                    height = ApplyHeightLimits(child, height);

                    if (host.AlignItems == FlexAlignItems.Stretch &&
                        !info.WidthExplicit)
                    {
                        width = Math.Max(
                            0,
                            line.CrossSize -
                            margin.Left -
                            margin.Right);
                    }

                    width = ApplyWidthLimits(child, width);
                    y = ClampLongToInt(
                        (long)lineRectangle.Top +
                        (long)logicalCursor +
                        (long)metrics.MainLeadingMargin);

                    x = GetWrappedCrossPosition(
                        lineRectangle.Left,
                        line.CrossSize,
                        width,
                        rtl ? margin.Right : margin.Left,
                        rtl ? margin.Left : margin.Right,
                        host.AlignItems,
                        rtl);
                }

                SetBoundsIfChanged(
                    child,
                    new Rectangle(
                        x,
                        y,
                        Math.Max(0, width),
                        Math.Max(0, height)));

                // Remember the arranged size separately from the item's
                // declared flex basis. Otherwise an explicit Width/Height on
                // a growing item can use the previous arranged size as the
                // next pass's basis and grow again after every re-layout.
                RecordFlexArrangedSize(
                    info,
                    Math.Max(0, width),
                    Math.Max(0, height));

                logicalCursor = FlexLayoutPlanner.AddClamped(
                    logicalCursor,
                    FlexLayoutPlanner.SumExtent(
                        assignedMain,
                        metrics.MainLeadingMargin,
                        metrics.MainTrailingMargin));

                if (itemIndex + 1 < end)
                {
                    logicalCursor = FlexLayoutPlanner.AddClamped(
                        logicalCursor,
                        between);
                }
            }

            return IsItemsLayoutSnapshotCurrent(
                host,
                records,
                recordCount,
                generation,
                publicationRevision,
                runtime);
        }

        private static int GetWrappedCrossPosition(
            int physicalStart,
            int availableCross,
            int size,
            int logicalLeadingMargin,
            int logicalTrailingMargin,
            FlexAlignItems alignment,
            bool inverted)
        {
            int outer = FlexLayoutPlanner.SumExtent(
                size,
                logicalLeadingMargin,
                logicalTrailingMargin);
            int logicalOffset;

            if (alignment == FlexAlignItems.Center)
            {
                logicalOffset = Math.Max(
                    0,
                    (availableCross - outer) / 2);
            }
            else if (alignment == FlexAlignItems.End)
            {
                logicalOffset = Math.Max(
                    0,
                    availableCross - outer);
            }
            else
            {
                logicalOffset = 0;
            }

            if (inverted)
            {
                long right =
                    (long)physicalStart +
                    (long)availableCross -
                    (long)logicalOffset -
                    (long)logicalLeadingMargin;

                return ClampLongToInt(right - size);
            }

            return ClampLongToInt(
                (long)physicalStart +
                (long)logicalOffset +
                (long)logicalLeadingMargin);
        }

        private Size GetCachedDesiredSize(
            ItemsControl host,
            RenderedItemRecord record,
            Control control,
            Size proposed,
            bool epochScoped)
        {
            if (record != null &&
                record.MeasureCacheValid &&
                (!epochScoped ||
                 record.MeasureCacheEpoch == host.ItemsMeasureEpoch) &&
                record.MeasureProposedWidth == proposed.Width &&
                record.MeasureProposedHeight == proposed.Height)
            {
                return record.MeasureCachedSize;
            }

            Size desired = GetDesiredSize(control, proposed);

            if (record != null)
            {
                record.MeasureCacheValid = true;
                record.MeasureCacheEpoch = epochScoped
                    ? host.ItemsMeasureEpoch
                    : 0L;
                record.MeasureProposedWidth = proposed.Width;
                record.MeasureProposedHeight = proposed.Height;
                record.MeasureCachedSize = desired;
            }

            return desired;
        }

        private static bool HaveSameControlOrder(
            ArrayList oldRecords,
            ArrayList newRecords)
        {
            if (Object.ReferenceEquals(oldRecords, newRecords))
                return true;

            if (oldRecords == null || newRecords == null ||
                oldRecords.Count != newRecords.Count)
            {
                return false;
            }

            int i;

            for (i = 0; i < oldRecords.Count; i++)
            {
                RenderedItemRecord oldRecord =
                    oldRecords[i] as RenderedItemRecord;

                RenderedItemRecord newRecord =
                    newRecords[i] as RenderedItemRecord;

                if (oldRecord == null || newRecord == null ||
                    !Object.ReferenceEquals(oldRecord.Control, newRecord.Control))
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateDirectVirtualRealizedRangeFromRecords(
            ItemsControl host,
            ArrayList records)
        {
            int start = -1;
            int end = -1;
            int i;

            if (records != null)
            {
                for (i = 0; i < records.Count; i++)
                {
                    RenderedItemRecord record =
                        records[i] as RenderedItemRecord;

                    if (record == null || record.Control == null ||
                        record.LogicalIndex < 0 || record.Control.IsDisposed)
                    {
                        continue;
                    }

                    if (start < 0 || record.LogicalIndex < start)
                        start = record.LogicalIndex;

                    if (end < 0 || record.LogicalIndex > end)
                        end = record.LogicalIndex;
                }
            }

            host.DirectVirtualRealizedStart = start;
            host.DirectVirtualRealizedEnd = end;
        }

        private ItemsContentMeasurement MeasureItemsContentSize(
            ItemsControl host,
            Size proposed,
            ArrayList records,
            int recordCount,
            int generation,
            long publicationRevision,
            XamlRuntime runtime)
        {
            if (!IsItemsLayoutSnapshotCurrent(
                    host,
                    records,
                    recordCount,
                    generation,
                    publicationRevision,
                    runtime))
            {
                return new ItemsContentMeasurement(
                    Size.Empty,
                    0,
                    false);
            }

            long width = 0L;
            long height = 0L;
            int visibleCount = 0;
            int count = GetRenderedItemCount(host, records);
            int i;

            // A vertical scrolling ItemsControl must measure text/containers at
            // the actual viewport width. Preferred widths are not allowed to create
            // a horizontal scroll range unless Orientation=Horizontal.
            Size itemProposed = proposed;

            for (i = 0; i < count; i++)
            {
                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime))
                {
                    return new ItemsContentMeasurement(
                        Size.Empty,
                        0,
                        false);
                }

#if !WINFORMSXAML_PACKAGE
                if (host.ItemsLayoutScanDiagnosticsEnabled)
                    host.RecordItemsMeasureRecordProbe();
#endif

                Control child = GetRenderedItemControl(
                    host,
                    records,
                    i);

                if (child == null || child.IsDisposed)
                    continue;

                ElementInfo info = GetInfo(child);

                if (info.Collapsed)
                    continue;

                RenderedItemRecord record =
                    records != null && i < records.Count
                        ? records[i] as RenderedItemRecord
                        : null;

                Padding margin =
                    GetEffectiveMargin(
                        child,
                        info.Margin);

                Size childProposed = new Size(
                    Math.Max(0, itemProposed.Width - margin.Left - margin.Right),
                    Math.Max(0, itemProposed.Height - margin.Top - margin.Bottom));

                Size desired =
                    GetCachedDesiredSize(
                        host,
                        record,
                        child,
                        childProposed,
                        true);

                if (!IsItemsLayoutSnapshotCurrent(
                        host,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        runtime))
                {
                    return new ItemsContentMeasurement(
                        Size.Empty,
                        0,
                        false);
                }

                long childWidth = SaturatingAddNonnegative(
                    desired.Width,
                    SaturatingAddNonnegative(
                        margin.Left,
                        margin.Right));

                long childHeight = SaturatingAddNonnegative(
                    desired.Height,
                    SaturatingAddNonnegative(
                        margin.Top,
                        margin.Bottom));

                if (host.Orientation == Orientation.Vertical)
                {
                    width = Math.Max(
                        width,
                        Math.Min(
                            childWidth,
                            (long)Math.Max(0, proposed.Width)));

                    height = SaturatingAddNonnegative(
                        height,
                        childHeight);
                }
                else
                {
                    width = SaturatingAddNonnegative(
                        width,
                        childWidth);
                    height = Math.Max(height, childHeight);
                }

                visibleCount++;
            }

            if (visibleCount > 1)
            {
                long spacing = SaturatingMultiplyNonnegative(
                    host.Spacing,
                    visibleCount - 1);

                if (host.Orientation == Orientation.Vertical)
                    height = SaturatingAddNonnegative(height, spacing);
                else
                    width = SaturatingAddNonnegative(width, spacing);
            }

            width = SaturatingAddNonnegative(
                width,
                SaturatingAddNonnegative(
                    host.Padding.Left,
                    host.Padding.Right));
            height = SaturatingAddNonnegative(
                height,
                SaturatingAddNonnegative(
                    host.Padding.Top,
                    host.Padding.Bottom));

            return new ItemsContentMeasurement(
                new Size(
                    ClampNonnegativeLongToInt(width),
                    ClampNonnegativeLongToInt(height)),
                visibleCount);
        }

        private bool HasLayoutItems(
            ItemsControl host,
            ArrayList records)
        {
            int count = GetRenderedItemCount(host, records);
            int i;

            for (i = 0; i < count; i++)
            {
#if !WINFORMSXAML_PACKAGE
                if (host.ItemsLayoutScanDiagnosticsEnabled)
                    host.RecordItemsVisibilityFallbackProbe();
#endif

                Control child = GetRenderedItemControl(
                    host,
                    records,
                    i);

                if (child != null &&
                    !child.IsDisposed &&
                    !GetInfo(child).Collapsed)
                    return true;
            }

            return false;
        }

        private static long SaturatingMultiplyNonnegative(
            long left,
            long right)
        {
            if (left <= 0L || right <= 0L)
                return 0L;

            if (left > Int64.MaxValue / right)
                return Int64.MaxValue;

            return left * right;
        }

        private ItemsContentMeasurement MeasureCurrentItemsContentSize(
            ItemsControl host,
            Size proposed)
        {
            int pass;

            for (pass = 0; pass < 2; pass++)
            {
                ArrayList records = host.RenderedItems;
                int recordCount = records == null
                    ? 0
                    : records.Count;
                int generation = host.RefreshGeneration;
                long publicationRevision =
                    host.RenderedItemPublicationRevision;
                ItemsContentMeasurement measured =
                    MeasureItemsContentSize(
                        host,
                        proposed,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        this);

                if (measured.Stable)
                    return measured;
            }

            return new ItemsContentMeasurement(
                Size.Empty,
                0,
                false);
        }

        private Size GetPreferredItemsControlSize(
            ItemsControl host,
            Size proposed)
        {
            host.AdvanceItemsMeasureEpoch();

            if (host.LightweightActive)
            {
                long count = host.Count;
                long contentHeight =
                    (count * (long)host.FixedItemSize) +
                    (Math.Max(0L, count - 1L) * host.Spacing) +
                    host.Padding.Top +
                    host.Padding.Bottom;
                int height = contentHeight > Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)Math.Max(0L, contentHeight);
                int width = proposed.Width > 0
                    ? proposed.Width
                    : Math.Max(host.Width, host.MinimumSize.Width);

                if (host.AutoScroll && proposed.Height > 0)
                    height = Math.Min(height, proposed.Height);

                return ApplyMinimumSize(
                    host,
                    new Size(Math.Max(0, width), Math.Max(0, height)));
            }

            if (host.DirectVirtualActive &&
                host.DirectVirtualViewport != null)
            {
                VirtualViewportModel viewport =
                    host.DirectVirtualViewport;
                long contentMain = viewport.TotalExtent;

                // Uniform models retain one synthetic trailing gap so every
                // index remains constant-stride. It is not visual content and
                // must not inflate the preferred size.
                if (viewport.Uniform &&
                    viewport.Count > 0 &&
                    host.Spacing > 0)
                {
                    contentMain = Math.Max(
                        0L,
                        contentMain - (long)host.Spacing);
                }

                long mainPadding =
                    host.Orientation == Orientation.Vertical
                        ? SaturatingAddNonnegative(
                            host.Padding.Top,
                            host.Padding.Bottom)
                        : SaturatingAddNonnegative(
                            host.Padding.Left,
                            host.Padding.Right);
                int totalMain = ClampNonnegativeLongToInt(
                    SaturatingAddNonnegative(
                        contentMain,
                        mainPadding));
                int horizontalPadding =
                    ClampNonnegativeLongToInt(
                        SaturatingAddNonnegative(
                            host.Padding.Left,
                            host.Padding.Right));
                int verticalPadding =
                    ClampNonnegativeLongToInt(
                        SaturatingAddNonnegative(
                            host.Padding.Top,
                            host.Padding.Bottom));
                int width = proposed.Width > 0
                    ? proposed.Width
                    : Math.Max(host.Width, horizontalPadding);
                int height = proposed.Height > 0
                    ? proposed.Height
                    : Math.Max(host.Height, verticalPadding);

                if (host.Orientation == Orientation.Vertical)
                {
                    if (height <= 0)
                        height = totalMain;
                    else
                        height = Math.Min(height, totalMain);
                }
                else
                {
                    if (width <= 0)
                        width = totalMain;
                    else
                        width = Math.Min(width, totalMain);
                }

                return ApplyMinimumSize(
                    host,
                    new Size(
                        Math.Max(0, width),
                        Math.Max(0, height)));
            }

            if (host.Wrap)
            {
                return GetPreferredWrappedItemsControlSize(
                    host,
                    proposed);
            }

            // When AutoScroll is active and a parent (for example FlexPanel with
            // FlexGrow) gives us a finite viewport, do not report the entire list
            // height as our preferred height. Doing so fights the parent layout and
            // can manufacture phantom scroll ranges.
            if (host.AutoScroll)
            {
                Size preferred =
                    MeasureCurrentItemsContentSize(
                        host,
                        proposed).ContentSize;

                if (host.Orientation == Orientation.Vertical &&
                    proposed.Height > 0)
                {
                    preferred.Height = Math.Min(
                        preferred.Height,
                        proposed.Height);
                }

                if (host.Orientation == Orientation.Horizontal &&
                    proposed.Width > 0)
                {
                    preferred.Width = Math.Min(
                        preferred.Width,
                        proposed.Width);
                }

                return ApplyMinimumSize(
                    host,
                    preferred);
            }

            return ApplyMinimumSize(
                host,
                MeasureCurrentItemsContentSize(
                    host,
                    proposed).ContentSize);
        }

        private Size GetPreferredWrappedItemsControlSize(
            ItemsControl host,
            Size proposed)
        {
            bool ownsScratch;
            WrappedItemsLayoutPlan reusablePlan =
                LeaseWrappedItemsLayoutScratch(
                    host,
                    out ownsScratch);
            WrappedItemsLayoutPlan wrapped = null;

            try
            {
                int horizontalPadding =
                    FlexLayoutPlanner.AddClamped(
                        host.Padding.Left,
                        host.Padding.Right);
                int verticalPadding =
                    FlexLayoutPlanner.AddClamped(
                        host.Padding.Top,
                        host.Padding.Bottom);
                Size available = new Size(
                    Math.Max(
                        0,
                        proposed.Width - horizontalPadding),
                    Math.Max(
                        0,
                        proposed.Height - verticalPadding));
                int pass;

                for (pass = 0; pass < 2; pass++)
                {
                    ArrayList records = host.RenderedItems;
                    int recordCount = records == null
                        ? 0
                        : records.Count;
                    int generation = host.RefreshGeneration;
                    long publicationRevision =
                        host.RenderedItemPublicationRevision;

                    wrapped = CreateWrappedItemsLayoutPlan(
                        host,
                        available,
                        false,
                        records,
                        recordCount,
                        generation,
                        publicationRevision,
                        this,
                        reusablePlan,
                        pass > 0);

                    if (wrapped != null)
                    {
                        reusablePlan = wrapped;
                        break;
                    }
                }

                if (wrapped == null)
                {
                    return ApplyMinimumSize(
                        host,
                        new Size(
                            horizontalPadding,
                            verticalPadding));
                }

                int width;
                int height;

                if (wrapped.RowFlow)
                {
                    int innerWidth = available.Width > 0
                        ? available.Width
                        : wrapped.Flex.PreferredMain;

                    width = FlexLayoutPlanner.AddClamped(
                        innerWidth,
                        horizontalPadding);
                    height = FlexLayoutPlanner.AddClamped(
                        wrapped.Flex.PreferredCross,
                        verticalPadding);

                    if (host.AutoScroll && proposed.Height > 0)
                        height = Math.Min(height, proposed.Height);
                }
                else
                {
                    int innerHeight = available.Height > 0
                        ? available.Height
                        : wrapped.Flex.PreferredMain;

                    width = FlexLayoutPlanner.AddClamped(
                        wrapped.Flex.PreferredCross,
                        horizontalPadding);
                    height = FlexLayoutPlanner.AddClamped(
                        innerHeight,
                        verticalPadding);

                    if (host.AutoScroll && proposed.Width > 0)
                        width = Math.Min(width, proposed.Width);
                }

                return ApplyMinimumSize(
                    host,
                    new Size(
                        Math.Max(0, width),
                        Math.Max(0, height)));
            }
            finally
            {
                ReturnWrappedItemsLayoutScratch(
                    host,
                    ownsScratch,
                    reusablePlan);
            }
        }
    }
}
