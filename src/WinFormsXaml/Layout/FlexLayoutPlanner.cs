using System;

namespace WinFormsXaml
{
    /// <summary>
    /// Describes one visible item in logical flex coordinates. The planner is
    /// deliberately independent of Control so repeated-item layouts can reuse
    /// the same wrapping and growth rules.
    /// </summary>
    internal struct FlexLayoutItemMetrics
    {
        internal int SourceIndex;
        internal int BasisMain;
        internal int CrossSize;
        internal int MainLeadingMargin;
        internal int MainTrailingMargin;
        internal int CrossLeadingMargin;
        internal int CrossTrailingMargin;
        internal float Grow;
        internal int MaxMain;

        internal FlexLayoutItemMetrics(
            int sourceIndex,
            int basisMain,
            int crossSize,
            int mainLeadingMargin,
            int mainTrailingMargin,
            int crossLeadingMargin,
            int crossTrailingMargin,
            float grow,
            int maxMain)
        {
            SourceIndex = sourceIndex;
            BasisMain = Math.Max(0, basisMain);
            CrossSize = Math.Max(0, crossSize);
            MainLeadingMargin = mainLeadingMargin;
            MainTrailingMargin = mainTrailingMargin;
            CrossLeadingMargin = crossLeadingMargin;
            CrossTrailingMargin = crossTrailingMargin;
            Grow =
                grow > 0.0f &&
                !Single.IsNaN(grow) &&
                !Single.IsInfinity(grow)
                    ? grow
                    : 0.0f;
            MaxMain = maxMain > 0 ? maxMain : Int32.MaxValue;
        }

        internal int OuterBasisMain
        {
            get
            {
                return FlexLayoutPlanner.SumExtent(
                    BasisMain,
                    MainLeadingMargin,
                    MainTrailingMargin);
            }
        }

        internal int OuterCrossSize
        {
            get
            {
                return FlexLayoutPlanner.SumExtent(
                    CrossSize,
                    CrossLeadingMargin,
                    CrossTrailingMargin);
            }
        }
    }

    /// <summary>One logical row or column in a flex plan.</summary>
    internal struct FlexLayoutLine
    {
        internal int ItemStart;
        internal int ItemCount;
        internal int NaturalMain;
        internal int AssignedMain;
        internal int CrossSize;
        internal int CrossOffset;
    }

    /// <summary>
    /// Immutable line membership plus per-pass main-axis assignments.
    /// </summary>
    internal sealed class FlexLayoutPlan
    {
        internal FlexLayoutItemMetrics[] Items;
        internal int ItemCount;
        internal FlexLayoutLine[] Lines;
        internal int LineCount;
        internal int[] AssignedMain;
        internal int PreferredMain;
        internal int PreferredCross;
    }

    /// <summary>
    /// Builds logical flex lines and distributes positive free space. It does
    /// not reverse source order; callers map logical offsets to LTR or RTL
    /// physical coordinates when arranging.
    /// </summary>
    internal static class FlexLayoutPlanner
    {
        // Retaining enough storage for the common large-list case avoids a
        // managed allocation on every layout without allowing a one-off huge
        // panel to pin equally huge scratch arrays for its lifetime.
        internal const int MaximumRetainedItemCapacity = 2048;

        internal static FlexLayoutPlan Create(
            FlexLayoutItemMetrics[] items,
            int itemCount,
            int availableMain,
            bool constrainMain,
            bool wrap,
            int gap)
        {
            return Create(
                items,
                itemCount,
                availableMain,
                constrainMain,
                wrap,
                gap,
                null);
        }

        internal static FlexLayoutPlan Create(
            FlexLayoutItemMetrics[] items,
            int itemCount,
            int availableMain,
            bool constrainMain,
            bool wrap,
            int gap,
            FlexLayoutPlan reusablePlan)
        {
            if (items == null)
                throw new ArgumentNullException("items");

            if (itemCount < 0 || itemCount > items.Length)
                throw new ArgumentOutOfRangeException("itemCount");

            availableMain = Math.Max(0, availableMain);
            gap = Math.Max(0, gap);

            FlexLayoutPlan plan =
                reusablePlan == null
                    ? new FlexLayoutPlan()
                    : reusablePlan;

            plan.Items = items;
            plan.ItemCount = itemCount;
            plan.LineCount = 0;
            plan.PreferredMain = 0;
            plan.PreferredCross = 0;

            if (plan.AssignedMain == null ||
                plan.AssignedMain.Length < itemCount)
            {
                plan.AssignedMain =
                    new int[GetScratchCapacity(itemCount)];
            }

            if (plan.Lines == null ||
                plan.Lines.Length < itemCount)
            {
                plan.Lines =
                    new FlexLayoutLine[
                        GetScratchCapacity(itemCount)];
            }

            if (itemCount == 0)
                return plan;

            int lineStart = 0;
            int lineCount = 0;
            int lineMain = 0;
            int lineCross = 0;
            int i;

            for (i = 0; i < itemCount; i++)
            {
                FlexLayoutItemMetrics item =
                    items[i];

                plan.AssignedMain[i] =
                    item.BasisMain;

                int outerMain =
                    item.OuterBasisMain;

                int nextMain =
                    lineCount == 0
                        ? outerMain
                        : AddClamped(
                            lineMain,
                            AddClamped(
                                gap,
                                outerMain));

                if (wrap &&
                    constrainMain &&
                    lineCount > 0 &&
                    nextMain > availableMain)
                {
                    AddLine(
                        plan,
                        lineStart,
                        lineCount,
                        lineMain,
                        lineCross,
                        gap);

                    lineStart = i;
                    lineCount = 0;
                    lineMain = 0;
                    lineCross = 0;
                    nextMain = outerMain;
                }

                lineMain = nextMain;
                lineCross =
                    Math.Max(
                        lineCross,
                        item.OuterCrossSize);
                lineCount++;
            }

            AddLine(
                plan,
                lineStart,
                lineCount,
                lineMain,
                lineCross,
                gap);

            return plan;
        }

        internal static int GetScratchCapacity(int required)
        {
            if (required <= 0)
                return 0;

            if (required > MaximumRetainedItemCapacity)
                return required;

            int capacity = 16;

            while (capacity < required)
                capacity *= 2;

            return capacity;
        }

        /// <summary>
        /// Applies FlexGrow to every line. Maximum main-axis sizes freeze a
        /// child and the unconsumed share is redistributed among the remaining
        /// growing children.
        /// </summary>
        internal static void AllocateGrow(
            FlexLayoutPlan plan,
            int availableMain,
            int gap)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            availableMain = Math.Max(0, availableMain);
            gap = Math.Max(0, gap);

            int lineIndex;

            for (lineIndex = 0;
                 lineIndex < plan.LineCount;
                 lineIndex++)
            {
                AllocateLineGrow(
                    plan,
                    lineIndex,
                    availableMain,
                    gap);
            }
        }

        private static void AllocateLineGrow(
            FlexLayoutPlan plan,
            int lineIndex,
            int availableMain,
            int gap)
        {
            FlexLayoutLine line =
                plan.Lines[lineIndex];

            int end =
                line.ItemStart +
                line.ItemCount;

            int i;

            for (i = line.ItemStart; i < end; i++)
            {
                plan.AssignedMain[i] =
                    plan.Items[i].BasisMain;
            }

            int remaining =
                Math.Max(
                    0,
                    availableMain -
                    line.NaturalMain);

            while (remaining > 0)
            {
                double totalGrow = 0.0;

                for (i = line.ItemStart; i < end; i++)
                {
                    FlexLayoutItemMetrics item =
                        plan.Items[i];

                    if (item.Grow > 0.0f &&
                        GetRemainingCapacity(
                            item,
                            plan.AssignedMain[i]) > 0)
                    {
                        totalGrow += item.Grow;
                    }
                }

                if (totalGrow <= 0.0)
                    break;

                int cappedIndex = -1;

                for (i = line.ItemStart; i < end; i++)
                {
                    FlexLayoutItemMetrics item =
                        plan.Items[i];

                    if (item.Grow <= 0.0f)
                        continue;

                    int capacity =
                        GetRemainingCapacity(
                            item,
                            plan.AssignedMain[i]);

                    if (capacity <= 0)
                        continue;

                    double idealShare =
                        ((double)remaining *
                         (double)item.Grow) /
                        totalGrow;

                    if (capacity <= idealShare)
                    {
                        cappedIndex = i;
                        break;
                    }
                }

                if (cappedIndex >= 0)
                {
                    int capacity =
                        GetRemainingCapacity(
                            plan.Items[cappedIndex],
                            plan.AssignedMain[cappedIndex]);

                    plan.AssignedMain[cappedIndex] +=
                        capacity;

                    remaining -= capacity;
                    continue;
                }

                int roundSpace = remaining;
                int assigned = 0;

                for (i = line.ItemStart; i < end; i++)
                {
                    FlexLayoutItemMetrics item =
                        plan.Items[i];

                    if (item.Grow <= 0.0f ||
                        GetRemainingCapacity(
                            item,
                            plan.AssignedMain[i]) <= 0)
                    {
                        continue;
                    }

                    int share =
                        (int)Math.Floor(
                            ((double)roundSpace *
                             (double)item.Grow) /
                            totalGrow);

                    int capacity =
                        GetRemainingCapacity(
                            item,
                            plan.AssignedMain[i]);

                    share =
                        Math.Min(
                            share,
                            capacity);

                    plan.AssignedMain[i] += share;
                    assigned += share;
                }

                remaining -= assigned;

                // Integer division can leave fewer than one pixel per active
                // child. Preserve source order and hand out those pixels once.
                for (i = line.ItemStart;
                     i < end && remaining > 0;
                     i++)
                {
                    FlexLayoutItemMetrics item =
                        plan.Items[i];

                    if (item.Grow <= 0.0f ||
                        GetRemainingCapacity(
                            item,
                            plan.AssignedMain[i]) <= 0)
                    {
                        continue;
                    }

                    plan.AssignedMain[i]++;
                    remaining--;
                }

                break;
            }

            line.AssignedMain =
                CalculateAssignedMain(
                    plan,
                    line,
                    gap);

            plan.Lines[lineIndex] = line;
        }

        private static int GetRemainingCapacity(
            FlexLayoutItemMetrics item,
            int assignedMain)
        {
            if (item.MaxMain == Int32.MaxValue)
                return Int32.MaxValue;

            return Math.Max(
                0,
                item.MaxMain -
                assignedMain);
        }

        private static int CalculateAssignedMain(
            FlexLayoutPlan plan,
            FlexLayoutLine line,
            int gap)
        {
            int main = 0;
            int end =
                line.ItemStart +
                line.ItemCount;

            int i;

            for (i = line.ItemStart; i < end; i++)
            {
                FlexLayoutItemMetrics item =
                    plan.Items[i];

                if (i > line.ItemStart)
                    main = AddClamped(main, gap);

                main =
                    AddClamped(
                        main,
                        SumExtent(
                            plan.AssignedMain[i],
                            item.MainLeadingMargin,
                            item.MainTrailingMargin));
            }

            return main;
        }

        private static void AddLine(
            FlexLayoutPlan plan,
            int itemStart,
            int itemCount,
            int naturalMain,
            int crossSize,
            int gap)
        {
            if (itemCount <= 0)
                return;

            FlexLayoutLine line =
                new FlexLayoutLine();

            line.ItemStart = itemStart;
            line.ItemCount = itemCount;
            line.NaturalMain = naturalMain;
            line.AssignedMain = naturalMain;
            line.CrossSize = crossSize;
            line.CrossOffset = plan.PreferredCross;

            if (plan.LineCount > 0)
            {
                line.CrossOffset =
                    AddClamped(
                        line.CrossOffset,
                        gap);

                plan.PreferredCross =
                    AddClamped(
                        plan.PreferredCross,
                        gap);
            }

            plan.Lines[plan.LineCount] = line;
            plan.LineCount++;
            plan.PreferredMain =
                Math.Max(
                    plan.PreferredMain,
                    naturalMain);
            plan.PreferredCross =
                AddClamped(
                    plan.PreferredCross,
                    crossSize);
        }

        internal static int AddClamped(
            int first,
            int second)
        {
            long result =
                (long)first +
                (long)second;

            if (result > Int32.MaxValue)
                return Int32.MaxValue;

            if (result < 0)
                return 0;

            return (int)result;
        }

        internal static int SumExtent(
            int size,
            int leading,
            int trailing)
        {
            long result =
                (long)size +
                (long)leading +
                (long)trailing;

            if (result > Int32.MaxValue)
                return Int32.MaxValue;

            if (result < 0)
                return 0;

            return (int)result;
        }
    }
}
