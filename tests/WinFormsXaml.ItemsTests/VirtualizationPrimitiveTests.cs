using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml.ItemsTests
{
    /// <summary>
    /// Pure direct-virtualization primitive coverage.
    /// </summary>
    internal static class VirtualizationPrimitiveTests
    {
        private delegate void TestOperation();

        private sealed class TrackingEnumerable : IEnumerable, IDisposable
        {
            private readonly IList _items;
            private readonly Exception _moveNextFailure;
            private readonly Exception _disposeFailure;

            public int EnumerationCount;
            public int EnumeratorDisposeCount;
            public int SourceDisposeCount;

            public TrackingEnumerable(
                IList items,
                Exception moveNextFailure,
                Exception disposeFailure)
            {
                _items = items;
                _moveNextFailure = moveNextFailure;
                _disposeFailure = disposeFailure;
            }

            public IEnumerator GetEnumerator()
            {
                EnumerationCount++;
                return new TrackingEnumerator(this);
            }

            public void Dispose()
            {
                SourceDisposeCount++;
            }

            private sealed class TrackingEnumerator : IEnumerator, IDisposable
            {
                private readonly TrackingEnumerable _owner;
                private int _index;

                public TrackingEnumerator(TrackingEnumerable owner)
                {
                    _owner = owner;
                    _index = -1;
                }

                public object Current
                {
                    get
                    {
                        if (_index < 0 || _index >= _owner._items.Count)
                            throw new InvalidOperationException();

                        return _owner._items[_index];
                    }
                }

                public bool MoveNext()
                {
                    if (_owner._moveNextFailure != null)
                        throw _owner._moveNextFailure;

                    _index++;
                    return _index < _owner._items.Count;
                }

                public void Reset()
                {
                    _index = -1;
                }

                public void Dispose()
                {
                    _owner.EnumeratorDisposeCount++;

                    if (_owner._disposeFailure != null)
                        throw _owner._disposeFailure;
                }
            }
        }

        internal static void RunAll()
        {
            TestIndexedSourceRemainsLive();
            TestBindingListIsExposedWithoutSnapshot();
            TestEnumerableIsSnapshottedOnce();
            TestEnumeratorDisposalAndFailurePropagation();
            TestAdapterBoundsAndEmptySource();
            TestEmptyRange();
            TestVerticalRangeAndAsymmetricOverscan();
            TestDirectionalOverscanPreservesConfiguredBudget();
            TestLeadingPaddingDoesNotReduceInnerViewport();
            TestInnerViewportScrollDoesNotApplyPaddingTwice();
            TestHorizontalRangeUsesHorizontalMetrics();
            TestExactItemBoundaries();
            TestSpacingAndPaddingDoNotCreateVisibleItems();
            TestMeasuredRangeExpansionIsBatchedAndBounded();
            TestHugeRangesDoNotOverflow();
            TestInvalidRangeArguments();
        }

        private static void TestIndexedSourceRemainsLive()
        {
            ArrayList items = new ArrayList();
            items.Add("first");

            VirtualItemSourceAdapter adapter =
                VirtualItemSourceAdapter.Create(items);

            AssertTrue(!adapter.IsSnapshot, "IList remains live");
            AssertEqual(1, adapter.Count, "initial live count");
            AssertEqual("first", adapter.GetItem(0), "initial live item");

            items[0] = "changed";
            items.Add("second");

            AssertEqual(2, adapter.Count, "updated live count");
            AssertEqual("changed", adapter.GetItem(0), "updated live item");
            AssertEqual("second", adapter.GetItem(1), "appended live item");
        }

        private static void TestBindingListIsExposedWithoutSnapshot()
        {
            BindingList<string> items = new BindingList<string>();
            items.Add("row");

            VirtualItemSourceAdapter adapter =
                VirtualItemSourceAdapter.Create(items);

            AssertTrue(!adapter.IsSnapshot, "IBindingList remains live");
            AssertTrue(
                Object.ReferenceEquals(items, adapter.BindingList),
                "binding list identity is retained");
            AssertEqual("row", adapter.GetItem(0), "binding list item");
        }

        private static void TestEnumerableIsSnapshottedOnce()
        {
            ArrayList items = new ArrayList();
            items.Add("one");
            items.Add("two");

            TrackingEnumerable source = new TrackingEnumerable(
                items,
                null,
                null);

            VirtualItemSourceAdapter adapter =
                VirtualItemSourceAdapter.Create(source);

            AssertTrue(adapter.IsSnapshot, "enumerable uses snapshot");
            AssertEqual(1, source.EnumerationCount, "one enumeration");
            AssertEqual(1, source.EnumeratorDisposeCount, "enumerator disposed");
            AssertEqual(0, source.SourceDisposeCount, "source is not disposed");

            items[0] = "changed";
            items.Add("three");

            AssertEqual(2, adapter.Count, "snapshot count is stable");
            AssertEqual("one", adapter.GetItem(0), "snapshot value is stable");
            AssertEqual(1, source.EnumerationCount, "reads do not re-enumerate");
        }

        private static void TestEnumeratorDisposalAndFailurePropagation()
        {
            InvalidOperationException enumerationFailure =
                new InvalidOperationException("enumeration");
            InvalidOperationException cleanupFailure =
                new InvalidOperationException("cleanup");

            TrackingEnumerable failed = new TrackingEnumerable(
                new ArrayList(),
                enumerationFailure,
                cleanupFailure);
            Exception observed = null;

            try
            {
                VirtualItemSourceAdapter.Create(failed);
            }
            catch (Exception ex)
            {
                observed = ex;
            }

            AssertTrue(
                Object.ReferenceEquals(enumerationFailure, observed),
                "enumeration failure wins over cleanup failure");
            AssertEqual(1, failed.EnumeratorDisposeCount, "failed enumerator disposed");
            AssertEqual(0, failed.SourceDisposeCount, "failed source is not disposed");

            TrackingEnumerable cleanupFailed = new TrackingEnumerable(
                new ArrayList(),
                null,
                cleanupFailure);
            observed = null;

            try
            {
                VirtualItemSourceAdapter.Create(cleanupFailed);
            }
            catch (Exception ex)
            {
                observed = ex;
            }

            AssertTrue(
                Object.ReferenceEquals(cleanupFailure, observed),
                "cleanup failure after successful enumeration is propagated");
            AssertEqual(
                1,
                cleanupFailed.EnumeratorDisposeCount,
                "successful enumerator cleanup attempted once");
        }

        private static void TestAdapterBoundsAndEmptySource()
        {
            VirtualItemSourceAdapter empty =
                VirtualItemSourceAdapter.Create(null);

            AssertTrue(empty.IsSnapshot, "null source is an empty snapshot");
            AssertEqual(0, empty.Count, "null source count");
            AssertArgumentOutOfRange(
                delegate { empty.GetItem(0); },
                "empty upper bound");

            ArrayList item = new ArrayList();
            item.Add("value");
            VirtualItemSourceAdapter adapter =
                VirtualItemSourceAdapter.Create(item);

            AssertArgumentOutOfRange(
                delegate { adapter.GetItem(-1); },
                "negative item index");
            AssertArgumentOutOfRange(
                delegate { adapter.GetItem(1); },
                "item count upper bound");
        }

        private static void TestEmptyRange()
        {
            VirtualItemRange range = VirtualRangeCalculator.Calculate(
                0,
                0,
                0,
                Orientation.Vertical,
                Padding.Empty,
                Size.Empty,
                Point.Empty,
                0,
                0);

            AssertTrue(range.IsEmpty, "zero item count is empty");
            AssertEqual(-1, range.FirstVisibleIndex, "empty first index");
            AssertEqual(-1, range.LastVisibleIndex, "empty last index");
        }

        private static void TestVerticalRangeAndAsymmetricOverscan()
        {
            VirtualItemRange range = VirtualRangeCalculator.Calculate(
                10,
                20,
                5,
                Orientation.Vertical,
                new Padding(0, 10, 0, 7),
                new Size(200, 40),
                new Point(0, 10),
                1,
                2);

            AssertRange(range, 0, 1, 0, 3, "vertical range");
        }

        private static void
            TestDirectionalOverscanPreservesConfiguredBudget()
        {
            int before;
            int after;

            VirtualRangeCalculator.CalculateDirectionalOverscan(
                3,
                false,
                0,
                100,
                out before,
                out after);
            AssertEqual(3, before, "initial overscan before");
            AssertEqual(3, after, "initial overscan after");

            VirtualRangeCalculator.CalculateDirectionalOverscan(
                3,
                true,
                100,
                120,
                out before,
                out after);
            AssertEqual(0, before, "forward overscan before");
            AssertEqual(6, after, "forward overscan after");

            VirtualRangeCalculator.CalculateDirectionalOverscan(
                3,
                true,
                120,
                100,
                out before,
                out after);
            AssertEqual(6, before, "backward overscan before");
            AssertEqual(0, after, "backward overscan after");

            VirtualRangeCalculator.CalculateDirectionalOverscan(
                3,
                true,
                100,
                100,
                out before,
                out after);
            AssertEqual(3, before, "stationary overscan before");
            AssertEqual(3, after, "stationary overscan after");

            int largeBudget = Int32.MaxValue - 10;
            VirtualRangeCalculator.CalculateDirectionalOverscan(
                largeBudget,
                true,
                0,
                Int32.MaxValue,
                out before,
                out after);
            AssertTrue(
                (long)before + (long)after ==
                    (long)largeBudget * 2L,
                "large directional overscan preserves its Int64 budget");
            AssertEqual(
                Int32.MaxValue,
                after,
                "large forward overscan saturates only the travel side");
        }

        private static void TestLeadingPaddingDoesNotReduceInnerViewport()
        {
            VirtualItemRange range = VirtualRangeCalculator.Calculate(
                3,
                10,
                0,
                Orientation.Vertical,
                new Padding(0, 10, 0, 0),
                new Size(10, 20),
                Point.Empty,
                0,
                0);

            AssertRange(
                range,
                0,
                1,
                0,
                1,
                "leading padding is outside the supplied inner viewport");
        }

        private static void TestInnerViewportScrollDoesNotApplyPaddingTwice()
        {
            VirtualItemRange vertical = VirtualRangeCalculator.Calculate(
                4,
                20,
                0,
                Orientation.Vertical,
                new Padding(0, 10, 0, 7),
                new Size(10, 20),
                new Point(0, 20),
                0,
                0);

            AssertRange(
                vertical,
                1,
                1,
                1,
                1,
                "inner vertical scroll is not reduced by top padding");

            VirtualItemRange horizontal = VirtualRangeCalculator.Calculate(
                4,
                20,
                0,
                Orientation.Horizontal,
                new Padding(12, 0, 9, 0),
                new Size(20, 10),
                new Point(20, 0),
                0,
                0);

            AssertRange(
                horizontal,
                1,
                1,
                1,
                1,
                "inner horizontal scroll is not reduced by left padding");
        }

        private static void TestHorizontalRangeUsesHorizontalMetrics()
        {
            VirtualItemRange range = VirtualRangeCalculator.Calculate(
                5,
                10,
                2,
                Orientation.Horizontal,
                new Padding(5, 100, 9, 100),
                new Size(30, 1),
                new Point(17, 1000),
                2,
                1);

            AssertRange(range, 1, 3, 0, 4, "horizontal range");
        }

        private static void TestExactItemBoundaries()
        {
            VirtualItemRange first = VirtualRangeCalculator.Calculate(
                4,
                20,
                0,
                Orientation.Vertical,
                Padding.Empty,
                new Size(10, 20),
                Point.Empty,
                0,
                0);

            AssertRange(first, 0, 0, 0, 0, "viewport end is exclusive");

            VirtualItemRange second = VirtualRangeCalculator.Calculate(
                4,
                20,
                0,
                Orientation.Vertical,
                Padding.Empty,
                new Size(10, 20),
                new Point(0, 20),
                0,
                0);

            AssertRange(second, 1, 1, 1, 1, "item end is exclusive");
        }

        private static void TestSpacingAndPaddingDoNotCreateVisibleItems()
        {
            VirtualItemRange spacingOnly = VirtualRangeCalculator.Calculate(
                3,
                10,
                5,
                Orientation.Vertical,
                Padding.Empty,
                new Size(10, 5),
                new Point(0, 10),
                3,
                3);

            AssertTrue(spacingOnly.IsEmpty, "spacing-only viewport is empty");

            VirtualItemRange reachesNext = VirtualRangeCalculator.Calculate(
                3,
                10,
                5,
                Orientation.Vertical,
                Padding.Empty,
                new Size(10, 6),
                new Point(0, 10),
                0,
                0);

            AssertRange(reachesNext, 1, 1, 1, 1, "next item edge is visible");

            VirtualItemRange trailingOnly = VirtualRangeCalculator.Calculate(
                2,
                10,
                0,
                Orientation.Vertical,
                new Padding(0, 5, 0, 10),
                new Size(10, 5),
                new Point(0, 25),
                1,
                1);

            AssertTrue(trailingOnly.IsEmpty, "trailing padding is not an item");
        }

        private static void TestMeasuredRangeExpansionIsBatchedAndBounded()
        {
            VirtualItemRange measured = new VirtualItemRange(
                10,
                11,
                8,
                13);
            VirtualItemRange expanded =
                VirtualRangeCalculator.ExpandMeasuredRealization(
                    measured,
                    1000,
                    100,
                    1,
                    2);

            AssertRange(
                expanded,
                10,
                11,
                8,
                111,
                "one-pixel rows expand in one viewport-bounded batch");

            VirtualItemRange partialFirst =
                VirtualRangeCalculator.ExpandMeasuredRealization(
                    new VirtualItemRange(10, 11, 10, 11),
                    100,
                    20,
                    10,
                    0);

            AssertRange(
                partialFirst,
                10,
                11,
                10,
                12,
                "a partial first row reserves one additional visible slot");

            VirtualItemRange clamped =
                VirtualRangeCalculator.ExpandMeasuredRealization(
                    new VirtualItemRange(98, 99, 97, 99),
                    100,
                    100,
                    1,
                    3);

            AssertRange(
                clamped,
                98,
                99,
                0,
                99,
                "end correction shifts unused visible capacity backward");

            VirtualItemRange asymmetricEnd =
                VirtualRangeCalculator.ExpandMeasuredRealization(
                    new VirtualItemRange(18, 19, 18, 19),
                    20,
                    5,
                    1,
                    1,
                    3);

            AssertRange(
                asymmetricEnd,
                18,
                19,
                14,
                19,
                "end correction shifts capacity before directional overscan");

            VirtualItemRange unchanged =
                VirtualRangeCalculator.ExpandMeasuredRealization(
                    measured,
                    1000,
                    100,
                    0,
                    2);

            AssertRange(
                unchanged,
                10,
                11,
                8,
                13,
                "no positive measurement keeps the complete current range");
        }

        private static void TestHugeRangesDoNotOverflow()
        {
            VirtualItemRange last = VirtualRangeCalculator.Calculate(
                Int32.MaxValue,
                1,
                0,
                Orientation.Horizontal,
                Padding.Empty,
                new Size(Int32.MaxValue, 1),
                new Point(Int32.MaxValue - 1, 0),
                Int32.MaxValue,
                Int32.MaxValue);

            AssertRange(
                last,
                Int32.MaxValue - 1,
                Int32.MaxValue - 1,
                0,
                Int32.MaxValue - 1,
                "huge count and overscan");

            VirtualItemRange hugeStride = VirtualRangeCalculator.Calculate(
                Int32.MaxValue,
                1,
                Int32.MaxValue - 1,
                Orientation.Horizontal,
                Padding.Empty,
                new Size(Int32.MaxValue, 1),
                new Point(Int32.MaxValue, 0),
                0,
                0);

            AssertRange(hugeStride, 1, 1, 1, 1, "huge uniform stride");
        }

        private static void TestInvalidRangeArguments()
        {
            AssertArgumentOutOfRange(
                delegate
                {
                    VirtualRangeCalculator.Calculate(
                        -1, 1, 0, Orientation.Vertical, Padding.Empty,
                        new Size(1, 1), Point.Empty, 0, 0);
                },
                "negative item count");

            AssertArgumentOutOfRange(
                delegate
                {
                    VirtualRangeCalculator.Calculate(
                        1, 0, 0, Orientation.Vertical, Padding.Empty,
                        new Size(1, 1), Point.Empty, 0, 0);
                },
                "zero item size");

            AssertArgumentOutOfRange(
                delegate
                {
                    VirtualRangeCalculator.Calculate(
                        1, 1, -1, Orientation.Vertical, Padding.Empty,
                        new Size(1, 1), Point.Empty, 0, 0);
                },
                "negative spacing");

            AssertArgumentOutOfRange(
                delegate
                {
                    VirtualRangeCalculator.Calculate(
                        1, 1, 0, Orientation.Vertical, Padding.Empty,
                        new Size(1, 1), Point.Empty, -1, 0);
                },
                "negative overscan");
        }

        private static void AssertRange(
            VirtualItemRange range,
            int first,
            int last,
            int start,
            int end,
            string message)
        {
            if (range.IsEmpty ||
                range.FirstVisibleIndex != first ||
                range.LastVisibleIndex != last ||
                range.RealizationStartIndex != start ||
                range.RealizationEndIndex != end)
            {
                throw new InvalidOperationException(
                    message + ": expected " +
                    first.ToString() + ".." + last.ToString() +
                    " (realize " + start.ToString() + ".." + end.ToString() +
                    "), actual " + range.FirstVisibleIndex.ToString() + ".." +
                    range.LastVisibleIndex.ToString() + " (realize " +
                    range.RealizationStartIndex.ToString() + ".." +
                    range.RealizationEndIndex.ToString() + ").");
            }
        }

        private static void AssertArgumentOutOfRange(
            TestOperation operation,
            string message)
        {
            bool threw = false;

            try
            {
                operation();
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            if (!threw)
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
                    message + ": expected " + Convert.ToString(expected) +
                    ", actual " + Convert.ToString(actual) + ".");
            }
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
        }
    }
}
