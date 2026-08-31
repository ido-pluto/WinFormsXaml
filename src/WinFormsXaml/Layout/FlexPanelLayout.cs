using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private void LayoutFlexPanel(
            FlexPanel panel)
        {
            bool ownsScratch;
            FlexLayoutPlan reusablePlan =
                LeaseFlexLayoutScratch(
                    panel,
                    out ownsScratch);
            FlexLayoutPlan plan = reusablePlan;

            try
            {
                Rectangle inner =
                    GetInnerRectangle(panel);

                bool row =
                    panel.Direction ==
                        FlexDirection.Row;

                bool rtl =
                    IsRightToLeft(panel);

                plan =
                    CreateFlexLayoutPlan(
                        panel,
                        inner.Size,
                        true,
                        true,
                        reusablePlan);

                int availableMain =
                    row
                        ? inner.Width
                        : inner.Height;

                FlexLayoutPlanner.AllocateGrow(
                    plan,
                    availableMain,
                    panel.Gap);

                int lineIndex;

                for (lineIndex = 0;
                     lineIndex < plan.LineCount;
                     lineIndex++)
                {
                    ArrangeFlexLine(
                        panel,
                        inner,
                        plan,
                        lineIndex,
                        row,
                        rtl);
                }
            }
            finally
            {
                ReturnFlexLayoutScratch(
                    panel,
                    ownsScratch,
                    plan);
            }
        }

        private static FlexLayoutPlan LeaseFlexLayoutScratch(
            FlexPanel panel,
            out bool ownsScratch)
        {
            ownsScratch = false;

            if (panel == null || panel.LayoutScratchInUse)
                return null;

            panel.LayoutScratchInUse = true;
            ownsScratch = true;

            FlexLayoutPlan plan =
                panel.LayoutScratchPlan;

            panel.LayoutScratchPlan = null;
            return plan;
        }

        private static void ReturnFlexLayoutScratch(
            FlexPanel panel,
            bool ownsScratch,
            FlexLayoutPlan plan)
        {
            if (!ownsScratch || panel == null)
                return;

            if (panel.IsDisposed ||
                panel.Disposing ||
                panel.Runtime == null ||
                !CanRetainFlexLayoutPlan(plan))
            {
                panel.LayoutScratchPlan = null;
            }
            else
            {
                panel.LayoutScratchPlan = plan;
            }

            panel.LayoutScratchInUse = false;
        }

        private static bool CanRetainFlexLayoutPlan(
            FlexLayoutPlan plan)
        {
            if (plan == null)
                return false;

            int maximum =
                FlexLayoutPlanner.MaximumRetainedItemCapacity;

            return (plan.Items == null ||
                    plan.Items.Length <= maximum) &&
                   (plan.Lines == null ||
                    plan.Lines.Length <= maximum) &&
                   (plan.AssignedMain == null ||
                    plan.AssignedMain.Length <= maximum);
        }

        private FlexLayoutPlan CreateFlexLayoutPlan(
            FlexPanel panel,
            Size proposedInnerSize,
            bool constrainMain,
            bool clearCollapsedBounds,
            FlexLayoutPlan reusablePlan)
        {
            bool row =
                panel.Direction ==
                    FlexDirection.Row;

            bool rtl =
                IsRightToLeft(panel);

            int controlCount = panel.Controls.Count;
            FlexLayoutItemMetrics[] items;
#if !WINFORMSXAML_PACKAGE
            int arrayAllocationCount = 0;
            int[] previousAssigned = reusablePlan == null
                ? null
                : reusablePlan.AssignedMain;
            FlexLayoutLine[] previousLines = reusablePlan == null
                ? null
                : reusablePlan.Lines;
#endif

            if (reusablePlan != null &&
                reusablePlan.Items != null &&
                reusablePlan.Items.Length >= controlCount)
            {
                items = reusablePlan.Items;
            }
            else
            {
                items =
                    new FlexLayoutItemMetrics[
                        FlexLayoutPlanner.GetScratchCapacity(
                            controlCount)];
#if !WINFORMSXAML_PACKAGE
                arrayAllocationCount++;
#endif
            }

            int visibleCount = 0;
            int i;

            for (i = 0;
                 i < panel.Controls.Count;
                 i++)
            {
                Control child =
                    panel.Controls[i];

                ElementInfo info =
                    GetInfo(child);

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

                bool measurementFailed;

                Size desired =
                    GetDesiredSize(
                        child,
                        proposedInnerSize,
                        out measurementFailed);

                if (measurementFailed)
                {
                    // Keep the established transient-failure contract: the
                    // measure/arrange pass retries a failed preferred-size
                    // query, then the shared plan uses the recovered value.
                    desired =
                        GetDesiredSize(
                            child,
                            proposedInnerSize);
                }

                bool flexibleGrowChild =
                    info.FlexGrow > 0.0f &&
                    (row
                        ? !info.WidthExplicit
                        : !info.HeightExplicit);

                int basisMain =
                    flexibleGrowChild
                        ? 0
                        : GetFlexMainBasis(
                            child,
                            info,
                            row,
                            desired);

                basisMain =
                    row
                        ? ApplyWidthLimits(
                            child,
                            basisMain)
                        : ApplyHeightLimits(
                            child,
                            basisMain);

                int crossSize =
                    row
                        ? desired.Height
                        : desired.Width;

                int maximumMain =
                    row
                        ? child.MaximumSize.Width
                        : child.MaximumSize.Height;

                int mainLeadingMargin;
                int mainTrailingMargin;
                int crossLeadingMargin;
                int crossTrailingMargin;

                if (row)
                {
                    mainLeadingMargin =
                        rtl
                            ? margin.Right
                            : margin.Left;
                    mainTrailingMargin =
                        rtl
                            ? margin.Left
                            : margin.Right;
                    crossLeadingMargin = margin.Top;
                    crossTrailingMargin = margin.Bottom;
                }
                else
                {
                    mainLeadingMargin = margin.Top;
                    mainTrailingMargin = margin.Bottom;
                    crossLeadingMargin =
                        rtl
                            ? margin.Right
                            : margin.Left;
                    crossTrailingMargin =
                        rtl
                            ? margin.Left
                            : margin.Right;
                }

                items[visibleCount] =
                    new FlexLayoutItemMetrics(
                        i,
                        basisMain,
                        crossSize,
                        mainLeadingMargin,
                        mainTrailingMargin,
                        crossLeadingMargin,
                        crossTrailingMargin,
                        info.FlexGrow,
                        maximumMain);

                visibleCount++;
            }

            int availableMain =
                row
                    ? proposedInnerSize.Width
                    : proposedInnerSize.Height;

            FlexLayoutPlan result =
                FlexLayoutPlanner.Create(
                    items,
                    visibleCount,
                    availableMain,
                    constrainMain,
                    panel.Wrap,
                    panel.Gap,
                    reusablePlan);

#if !WINFORMSXAML_PACKAGE
            if (!Object.ReferenceEquals(
                    previousAssigned,
                    result.AssignedMain))
            {
                arrayAllocationCount++;
            }

            if (!Object.ReferenceEquals(
                    previousLines,
                    result.Lines))
            {
                arrayAllocationCount++;
            }

            panel.RecordLayoutStorageForTest(
                Object.ReferenceEquals(
                    reusablePlan,
                    result),
                arrayAllocationCount);
#endif

            return result;
        }

        private void ArrangeFlexLine(
            FlexPanel panel,
            Rectangle inner,
            FlexLayoutPlan plan,
            int lineIndex,
            bool row,
            bool rtl)
        {
            FlexLayoutLine line =
                plan.Lines[lineIndex];

            int availableMain =
                row
                    ? inner.Width
                    : inner.Height;

            int availableCross =
                row
                    ? inner.Height
                    : inner.Width;

            int lineCross =
                panel.Wrap
                    ? line.CrossSize
                    : availableCross;

            int lineCrossOffset =
                panel.Wrap
                    ? line.CrossOffset
                    : 0;

            int justifyFree =
                Math.Max(
                    0,
                    availableMain -
                    line.AssignedMain);

            int leading = 0;
            int between = panel.Gap;

            if (panel.JustifyContent ==
                FlexJustifyContent.Center)
            {
                leading = justifyFree / 2;
            }
            else if (panel.JustifyContent ==
                     FlexJustifyContent.End)
            {
                leading = justifyFree;
            }
            else if (panel.JustifyContent ==
                     FlexJustifyContent.SpaceBetween &&
                     line.ItemCount > 1)
            {
                between =
                    panel.Gap +
                    justifyFree /
                    (line.ItemCount - 1);
            }
            else if (panel.JustifyContent ==
                     FlexJustifyContent.SpaceAround)
            {
                int extra =
                    line.ItemCount > 0
                        ? justifyFree /
                          line.ItemCount
                        : 0;

                leading = extra / 2;
                between =
                    panel.Gap +
                    extra;
            }

            int logicalMain = leading;
            int end =
                line.ItemStart +
                line.ItemCount;

            int itemIndex;

            for (itemIndex = line.ItemStart;
                 itemIndex < end;
                 itemIndex++)
            {
                FlexLayoutItemMetrics item =
                    plan.Items[itemIndex];

                Control child =
                    panel.Controls[item.SourceIndex];

                ElementInfo info =
                    GetInfo(child);

                int mainSize =
                    plan.AssignedMain[itemIndex];

                int crossSize =
                    item.CrossSize;

                int crossPosition;

                if (panel.AlignItems ==
                    FlexAlignItems.Stretch &&
                    (row
                        ? !info.HeightExplicit
                        : !info.WidthExplicit))
                {
                    crossSize =
                        Math.Max(
                            0,
                            lineCross -
                            item.CrossLeadingMargin -
                            item.CrossTrailingMargin);

                    crossSize =
                        row
                            ? ApplyHeightLimits(
                                child,
                                crossSize)
                            : ApplyWidthLimits(
                                child,
                                crossSize);
                }

                int outerCross =
                    FlexLayoutPlanner.SumExtent(
                        crossSize,
                        item.CrossLeadingMargin,
                        item.CrossTrailingMargin);

                if (panel.AlignItems ==
                    FlexAlignItems.Center)
                {
                    crossPosition =
                        (lineCross - outerCross) / 2 +
                        item.CrossLeadingMargin;
                }
                else if (panel.AlignItems ==
                         FlexAlignItems.End)
                {
                    crossPosition =
                        lineCross -
                        item.CrossTrailingMargin -
                        crossSize;
                }
                else
                {
                    crossPosition =
                        item.CrossLeadingMargin;
                }

                int mainPosition =
                    logicalMain +
                    item.MainLeadingMargin;

                int x;
                int y;
                int width;
                int height;

                if (row)
                {
                    x =
                        rtl
                            ? inner.Right -
                              mainPosition -
                              mainSize
                            : inner.Left +
                              mainPosition;

                    y =
                        inner.Top +
                        lineCrossOffset +
                        crossPosition;

                    width = mainSize;
                    height = crossSize;
                }
                else
                {
                    int lineRight =
                        inner.Right -
                        lineCrossOffset;

                    x =
                        rtl
                            ? lineRight -
                              crossPosition -
                              crossSize
                            : inner.Left +
                              lineCrossOffset +
                              crossPosition;

                    y =
                        inner.Top +
                        mainPosition;

                    width = crossSize;
                    height = mainSize;
                }

                SetBoundsIfChanged(
                    child,
                    new Rectangle(
                        x,
                        y,
                        Math.Max(0, width),
                        Math.Max(0, height)));

                RecordFlexArrangedSize(
                    info,
                    Math.Max(0, width),
                    Math.Max(0, height));

                logicalMain =
                    FlexLayoutPlanner.AddClamped(
                        logicalMain,
                        FlexLayoutPlanner.SumExtent(
                            mainSize,
                            item.MainLeadingMargin,
                            item.MainTrailingMargin));

                if (itemIndex + 1 < end)
                {
                    logicalMain =
                        FlexLayoutPlanner.AddClamped(
                            logicalMain,
                            between);
                }
            }
        }

        private static int GetFlexMainBasis(
            Control child,
            ElementInfo info,
            bool row,
            Size desired)
        {
            if (row)
            {
                if (!info.WidthExplicit ||
                    info.FlexGrow <= 0.0f)
                {
                    return desired.Width;
                }

                FlexBasisState state =
                    GetOrCreateFlexBasisState(info);

                if (!state.WidthBasisKnown ||
                    !state.LastArrangedWidthKnown ||
                    child.Width !=
                        state.LastArrangedWidth)
                {
                    state.WidthBasis =
                        child.Width;
                    state.WidthBasisKnown = true;
                }

                return state.WidthBasis;
            }

            if (!info.HeightExplicit ||
                info.FlexGrow <= 0.0f)
            {
                return desired.Height;
            }

            FlexBasisState heightState =
                GetOrCreateFlexBasisState(info);

            if (!heightState.HeightBasisKnown ||
                !heightState.LastArrangedHeightKnown ||
                child.Height !=
                    heightState.LastArrangedHeight)
            {
                heightState.HeightBasis =
                    child.Height;
                heightState.HeightBasisKnown = true;
            }

            return heightState.HeightBasis;
        }

        private static FlexBasisState GetOrCreateFlexBasisState(
            ElementInfo info)
        {
            if (info.FlexBasis == null)
            {
                info.FlexBasis =
                    new FlexBasisState();
            }

            return info.FlexBasis;
        }

        private static void RecordFlexArrangedSize(
            ElementInfo info,
            int width,
            int height)
        {
            FlexBasisState state =
                info.FlexBasis;

            if (state == null)
                return;

            state.LastArrangedWidth = width;
            state.LastArrangedWidthKnown = true;
            state.LastArrangedHeight = height;
            state.LastArrangedHeightKnown = true;
        }

        private static void InvalidateFlexWidthBasis(
            ElementInfo info)
        {
            FlexBasisState state =
                info == null
                    ? null
                    : info.FlexBasis;

            if (state == null)
                return;

            state.WidthBasisKnown = false;
            state.LastArrangedWidthKnown = false;
        }

        private static void InvalidateFlexHeightBasis(
            ElementInfo info)
        {
            FlexBasisState state =
                info == null
                    ? null
                    : info.FlexBasis;

            if (state == null)
                return;

            state.HeightBasisKnown = false;
            state.LastArrangedHeightKnown = false;
        }

        private Size GetPreferredFlexPanelSize(
            FlexPanel panel,
            Size proposed)
        {
            bool ownsScratch;
            FlexLayoutPlan reusablePlan =
                LeaseFlexLayoutScratch(
                    panel,
                    out ownsScratch);
            FlexLayoutPlan plan = reusablePlan;

            try
            {
                bool row =
                    panel.Direction ==
                        FlexDirection.Row;

                int horizontalPadding =
                    panel.Padding.Left +
                    panel.Padding.Right;

                int verticalPadding =
                    panel.Padding.Top +
                    panel.Padding.Bottom;

                Size proposedInner =
                    new Size(
                        proposed.Width > 0
                            ? Math.Max(
                                0,
                                proposed.Width -
                                horizontalPadding)
                            : 0,
                        proposed.Height > 0
                            ? Math.Max(
                                0,
                                proposed.Height -
                                verticalPadding)
                            : 0);

                bool constrainMain =
                    row
                        ? proposed.Width > 0
                        : proposed.Height > 0;

                plan =
                    CreateFlexLayoutPlan(
                        panel,
                        proposedInner,
                        constrainMain,
                        false,
                        reusablePlan);

                int width =
                    row
                        ? plan.PreferredMain
                        : plan.PreferredCross;

                int height =
                    row
                        ? plan.PreferredCross
                        : plan.PreferredMain;

                width =
                    FlexLayoutPlanner.AddClamped(
                        width,
                        horizontalPadding);

                height =
                    FlexLayoutPlanner.AddClamped(
                        height,
                        verticalPadding);

                return ApplyMinimumSize(
                    panel,
                    new Size(
                        width,
                        height));
            }
            finally
            {
                ReturnFlexLayoutScratch(
                    panel,
                    ownsScratch,
                    plan);
            }
        }
    }
}
