using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>
    /// Describes visible and overscanned indices for one uniform item axis.
    /// Empty ranges use -1 for every index.
    /// </summary>
    internal struct VirtualItemRange
    {
        private readonly int _firstVisibleIndex;
        private readonly int _lastVisibleIndex;
        private readonly int _realizationStartIndex;
        private readonly int _realizationEndIndex;

        internal VirtualItemRange(
            int firstVisibleIndex,
            int lastVisibleIndex,
            int realizationStartIndex,
            int realizationEndIndex)
        {
            _firstVisibleIndex = firstVisibleIndex;
            _lastVisibleIndex = lastVisibleIndex;
            _realizationStartIndex = realizationStartIndex;
            _realizationEndIndex = realizationEndIndex;
        }

        internal static VirtualItemRange Empty
        {
            get { return new VirtualItemRange(-1, -1, -1, -1); }
        }

        internal bool IsEmpty
        {
            get { return _firstVisibleIndex < 0; }
        }

        internal int FirstVisibleIndex
        {
            get { return _firstVisibleIndex; }
        }

        internal int LastVisibleIndex
        {
            get { return _lastVisibleIndex; }
        }

        internal int RealizationStartIndex
        {
            get { return _realizationStartIndex; }
        }

        internal int RealizationEndIndex
        {
            get { return _realizationEndIndex; }
        }
    }

    /// <summary>
    /// Pure uniform-item viewport arithmetic. Item bounds and viewport bounds
    /// are treated as half-open intervals, so merely touching an edge does not
    /// make an item visible. <c>viewportSize</c> is the usable inner viewport
    /// after padding; <c>logicalScrollOffset</c> is already expressed in that
    /// inner content coordinate system. Padding belongs to layout and native
    /// scroll-extent bookkeeping, not to the item-index projection.
    /// </summary>
    internal static class VirtualRangeCalculator
    {
        /// <summary>
        /// Calculates the visible and requested realization ranges for uniform
        /// items on one scrolling axis.
        /// </summary>
        /// <param name="itemCount">The nonnegative logical item count.</param>
        /// <param name="itemSize">The positive item extent without spacing.</param>
        /// <param name="spacing">The nonnegative gap between adjacent items.</param>
        /// <param name="orientation">The axis from which metrics are selected.</param>
        /// <param name="padding">Host padding, validated here but already excluded from the supplied viewport.</param>
        /// <param name="viewportSize">The usable inner viewport after padding.</param>
        /// <param name="logicalScrollOffset">The nonnegative host scroll offset.</param>
        /// <param name="overscanBefore">Items requested before the visible range.</param>
        /// <param name="overscanAfter">Items requested after the visible range.</param>
        /// <returns>An empty range or bounded logical item indices.</returns>
        internal static VirtualItemRange Calculate(
            int itemCount,
            int itemSize,
            int spacing,
            Orientation orientation,
            Padding padding,
            Size viewportSize,
            Point logicalScrollOffset,
            int overscanBefore,
            int overscanAfter)
        {
            ValidateOrientation(orientation);

            if (itemCount < 0)
                throw new ArgumentOutOfRangeException("itemCount");
            if (overscanBefore < 0)
                throw new ArgumentOutOfRangeException("overscanBefore");
            if (overscanAfter < 0)
                throw new ArgumentOutOfRangeException("overscanAfter");

            if (itemCount == 0)
                return VirtualItemRange.Empty;

            if (itemSize <= 0)
                throw new ArgumentOutOfRangeException("itemSize");
            if (spacing < 0)
                throw new ArgumentOutOfRangeException("spacing");

            int leadingPadding = orientation == Orientation.Vertical
                ? padding.Top
                : padding.Left;
            int trailingPadding = orientation == Orientation.Vertical
                ? padding.Bottom
                : padding.Right;
            int viewport = orientation == Orientation.Vertical
                ? viewportSize.Height
                : viewportSize.Width;
            int scroll = orientation == Orientation.Vertical
                ? logicalScrollOffset.Y
                : logicalScrollOffset.X;

            if (leadingPadding < 0)
                throw new ArgumentOutOfRangeException("padding");
            if (trailingPadding < 0)
                throw new ArgumentOutOfRangeException("padding");
            if (viewport < 0)
                throw new ArgumentOutOfRangeException("viewportSize");
            if (scroll < 0)
                throw new ArgumentOutOfRangeException("logicalScrollOffset");

            if (viewport == 0)
                return VirtualItemRange.Empty;

            long stride = (long)itemSize + (long)spacing;
            long contentEnd =
                ((long)itemCount - 1L) * stride +
                (long)itemSize;

            // The caller supplied the usable viewport after padding. Child
            // offsets and AutoScrollPosition therefore share the same logical
            // origin; subtracting padding here would apply the inset twice and
            // keep a row visible after its half-open trailing edge had passed.
            long viewportStart = scroll;
            long viewportEnd = viewportStart + (long)viewport;

            if (viewportStart >= contentEnd)
            {
                return VirtualItemRange.Empty;
            }

            long clippedStart = viewportStart;
            long clippedEnd = Math.Min(viewportEnd, contentEnd);

            if (clippedEnd <= clippedStart)
                return VirtualItemRange.Empty;

            long relativeStart = clippedStart;
            long first = relativeStart / stride;
            long offsetInStride = relativeStart % stride;

            // If the viewport begins in inter-item spacing, the previous item
            // has already ended. Advance to the next possible item.
            if (offsetInStride >= itemSize)
                first++;

            if (first >= itemCount)
                return VirtualItemRange.Empty;

            long firstItemStart = first * stride;

            if (firstItemStart >= clippedEnd)
                return VirtualItemRange.Empty;

            long relativeLastPixel = clippedEnd - 1L;
            long last = relativeLastPixel / stride;

            if (last >= itemCount)
                last = itemCount - 1L;

            if (last < first)
                return VirtualItemRange.Empty;

            long realizationStart = first - (long)overscanBefore;
            long realizationEnd = last + (long)overscanAfter;

            if (realizationStart < 0)
                realizationStart = 0;
            if (realizationEnd >= itemCount)
                realizationEnd = itemCount - 1L;

            return new VirtualItemRange(
                (int)first,
                (int)last,
                (int)realizationStart,
                (int)realizationEnd);
        }

        /// <summary>
        /// Splits the existing two-sided overscan budget according to the last
        /// published scroll direction. Initial and stationary viewports retain
        /// the symmetric split. A moving viewport places as much of the same
        /// total budget as an Int32 can represent on the leading edge of travel.
        /// </summary>
        internal static void CalculateDirectionalOverscan(
            int overscanItems,
            bool hasPreviousOffset,
            int previousOffset,
            int currentOffset,
            out int overscanBefore,
            out int overscanAfter)
        {
            if (overscanItems < 0)
                throw new ArgumentOutOfRangeException("overscanItems");
            if (previousOffset < 0)
                throw new ArgumentOutOfRangeException("previousOffset");
            if (currentOffset < 0)
                throw new ArgumentOutOfRangeException("currentOffset");

            int direction = !hasPreviousOffset ||
                            currentOffset == previousOffset
                ? 0
                : currentOffset > previousOffset ? 1 : -1;

            CalculateOverscanForDirection(
                overscanItems,
                direction,
                out overscanBefore,
                out overscanAfter);
        }

        /// <summary>
        /// Splits the same bounded overscan budget for an already established
        /// travel direction. The direct viewport uses this overload to retain
        /// its last published direction across duplicate native scroll/layout
        /// notifications for one settled offset.
        /// </summary>
        internal static void CalculateOverscanForDirection(
            int overscanItems,
            int direction,
            out int overscanBefore,
            out int overscanAfter)
        {
            if (overscanItems < 0)
                throw new ArgumentOutOfRangeException("overscanItems");
            if (direction < -1 || direction > 1)
                throw new ArgumentOutOfRangeException("direction");

            overscanBefore = overscanItems;
            overscanAfter = overscanItems;

            if (direction == 0)
                return;

            long totalBudget = (long)overscanItems * 2L;
            int travelSide = (int)Math.Min(
                (long)Int32.MaxValue,
                totalBudget);
            int oppositeSide = (int)(totalBudget - travelSide);

            if (direction > 0)
            {
                overscanBefore = oppositeSide;
                overscanAfter = travelSide;
            }
            else
            {
                overscanBefore = travelSide;
                overscanAfter = oppositeSide;
            }
        }

        /// <summary>
        /// Expands a measured variable-size request in one bounded batch. The
        /// smallest positive measured row is the conservative maximum number of
        /// rows that can fit in the viewport; symmetric overscan is then added.
        /// Near the source end, unused forward capacity is moved before the
        /// measured row. This avoids one-row-at-a-time correction when the
        /// initial estimate was much larger than the actual controls.
        /// </summary>
        internal static VirtualItemRange ExpandMeasuredRealization(
            VirtualItemRange measured,
            int itemCount,
            int viewportAxis,
            int smallestPositiveMeasuredExtent,
            int overscan)
        {
            return ExpandMeasuredRealization(
                measured,
                itemCount,
                viewportAxis,
                smallestPositiveMeasuredExtent,
                overscan,
                overscan);
        }

        /// <summary>
        /// Expands a measured variable-size request using independently bounded
        /// before/after overscan. Near the logical end, unused forward visible
        /// capacity is shifted backward so a native end clamp cannot require one
        /// newly measured short row per correction pass.
        /// </summary>
        internal static VirtualItemRange ExpandMeasuredRealization(
            VirtualItemRange measured,
            int itemCount,
            int viewportAxis,
            int smallestPositiveMeasuredExtent,
            int overscanBefore,
            int overscanAfter)
        {
            if (itemCount < 0)
                throw new ArgumentOutOfRangeException("itemCount");
            if (viewportAxis < 0)
                throw new ArgumentOutOfRangeException("viewportAxis");
            if (smallestPositiveMeasuredExtent < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "smallestPositiveMeasuredExtent");
            }
            if (overscanBefore < 0)
                throw new ArgumentOutOfRangeException("overscanBefore");
            if (overscanAfter < 0)
                throw new ArgumentOutOfRangeException("overscanAfter");

            if (measured.IsEmpty ||
                itemCount == 0 ||
                viewportAxis == 0 ||
                smallestPositiveMeasuredExtent == 0)
            {
                return measured;
            }

            // The viewport can begin partway through its first row. Reserve
            // that partial row plus enough complete minimum-size rows for the
            // remaining pixels (for example 20px at offset 9 in 10px rows can
            // touch three rows, not two).
            long remainingAfterFirstPixel =
                (long)viewportAxis - 1L;
            long visibleCapacity = 1L +
                (remainingAfterFirstPixel +
                 (long)smallestPositiveMeasuredExtent - 1L) /
                (long)smallestPositiveMeasuredExtent;
            long visibleStart = measured.FirstVisibleIndex;
            long visibleEnd = visibleStart + visibleCapacity - 1L;

            // At the end of the source, newly measured short rows reduce the
            // native maximum scroll offset. Shift the unused visible capacity
            // backward before adding overscan so the settled viewport is already
            // represented by this one bounded request.
            if (visibleEnd >= itemCount)
            {
                long unavailableAfter =
                    visibleEnd - ((long)itemCount - 1L);
                visibleEnd = itemCount - 1L;
                visibleStart = Math.Max(
                    0L,
                    visibleStart - unavailableAfter);
            }

            long start = visibleStart - (long)overscanBefore;
            long end = visibleEnd + (long)overscanAfter;

            if (start < 0)
                start = 0;
            if (end >= itemCount)
                end = itemCount - 1L;

            start = Math.Min(
                start,
                measured.RealizationStartIndex);
            end = Math.Max(
                end,
                measured.RealizationEndIndex);

            return new VirtualItemRange(
                measured.FirstVisibleIndex,
                measured.LastVisibleIndex,
                (int)start,
                (int)end);
        }

        private static void ValidateOrientation(
            Orientation orientation)
        {
            if (orientation != Orientation.Vertical &&
                orientation != Orientation.Horizontal)
            {
                throw new InvalidEnumArgumentException(
                    "orientation",
                    (int)orientation,
                    typeof(Orientation));
            }
        }
    }
}
