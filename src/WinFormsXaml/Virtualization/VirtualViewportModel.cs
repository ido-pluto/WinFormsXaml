using System;
using System.Collections;

namespace WinFormsXaml
{
    /// <summary>
    /// Stores item extents for viewport offset/index translation. Uniform mode
    /// retains only a count and fixed extent; general mode uses a long Fenwick
    /// tree for logarithmic updates and lookups.
    /// </summary>
    internal sealed class VirtualViewportModel
    {
        private readonly int _count;
        private readonly bool _uniform;
        private readonly long _uniformExtent;
        private readonly long[] _extents;
        private readonly long[] _tree;
        private long _totalExtent;
        private Hashtable _itemVersions;

        private sealed class ItemVersionSnapshot
        {
            public object Value;
            public bool HasValue;
        }

        /// <summary>Creates an allocation-constant fixed-extent model.</summary>
        internal VirtualViewportModel(int count, long itemExtent)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");

            if (itemExtent < 0)
                throw new ArgumentOutOfRangeException("itemExtent");

            if (count != 0 &&
                itemExtent > Int64.MaxValue / (long)count)
            {
                throw new OverflowException(
                    "The uniform viewport extent exceeds Int64.MaxValue.");
            }

            _count = count;
            _uniform = true;
            _uniformExtent = itemExtent;
            _totalExtent = itemExtent * (long)count;
        }

        /// <summary>
        /// Creates a mutable general model from a defensive copy of extents.
        /// Zero extents remain addressable by index but occupy no viewport space.
        /// </summary>
        internal VirtualViewportModel(long[] extents)
        {
            if (extents == null)
                throw new ArgumentNullException("extents");

            if (extents.Length == Int32.MaxValue)
            {
                throw new OverflowException(
                    "The general viewport index is too large.");
            }

            long total = 0;
            int i;

            for (i = 0; i < extents.Length; i++)
            {
                long extent = extents[i];

                if (extent < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        "extents",
                        "Item extents cannot be negative.");
                }

                if (extent > Int64.MaxValue - total)
                {
                    throw new OverflowException(
                        "The viewport extent total exceeds Int64.MaxValue.");
                }

                total += extent;
            }

            _count = extents.Length;
            _uniform = false;
            _extents = new long[_count];
            _tree = new long[_count + 1];
            _totalExtent = total;

            Array.Copy(extents, _extents, _count);

            // Linear Fenwick construction. Every node is a subrange of the
            // already-validated total, so no intermediate addition can overflow.
            for (i = 1; i <= _count; i++)
            {
                _tree[i] += _extents[i - 1];

                long parent = (long)i + (long)(i & -i);

                if (parent <= _count)
                    _tree[(int)parent] += _tree[i];
            }
        }

        /// <summary>
        /// Creates a mutable general model without requiring a temporary source
        /// array. Every item except the last uses itemExtent; the last uses
        /// trailingExtent so callers can omit trailing inter-item spacing.
        /// </summary>
        internal VirtualViewportModel(
            int count,
            long itemExtent,
            long trailingExtent)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");

            if (itemExtent < 0)
                throw new ArgumentOutOfRangeException("itemExtent");

            if (trailingExtent < 0)
                throw new ArgumentOutOfRangeException("trailingExtent");

            long repeatedTotal = 0;

            if (count > 1)
            {
                if (itemExtent > Int64.MaxValue / (long)(count - 1))
                {
                    throw new OverflowException(
                        "The viewport extent total exceeds Int64.MaxValue.");
                }

                repeatedTotal = itemExtent * (long)(count - 1);
            }

            long total = repeatedTotal;

            if (count != 0)
            {
                if (trailingExtent > Int64.MaxValue - total)
                {
                    throw new OverflowException(
                        "The viewport extent total exceeds Int64.MaxValue.");
                }

                total += trailingExtent;
            }

            if (count == Int32.MaxValue)
            {
                throw new OverflowException(
                    "The general viewport index is too large.");
            }

            _count = count;
            _uniform = false;
            _extents = new long[count];
            _tree = new long[count + 1];
            _totalExtent = total;

            int i;

            for (i = 0; i < count; i++)
            {
                _extents[i] = i + 1 == count
                    ? trailingExtent
                    : itemExtent;
            }

            for (i = 1; i <= count; i++)
            {
                _tree[i] += _extents[i - 1];

                long parent = (long)i + (long)(i & -i);

                if (parent <= count)
                    _tree[(int)parent] += _tree[i];
            }
        }

        /// <summary>Gets the number of indexed items, including zero extents.</summary>
        internal int Count
        {
            get { return _count; }
        }

        /// <summary>Gets the checked sum of all included extents.</summary>
        internal long TotalExtent
        {
            get { return _totalExtent; }
        }

        /// <summary>Gets whether this model uses the fixed-extent fast path.</summary>
        internal bool Uniform
        {
            get { return _uniform; }
        }

        /// <summary>
        /// Enables the optional sparse per-item version snapshot. Geometry-only
        /// models keep it null, and versioned models retain entries only for
        /// indices actually inspected by realization.
        /// </summary>
        internal void InitializeItemVersionSnapshot()
        {
            _itemVersions = new Hashtable();
        }

        /// <summary>Stores one version value captured during source refresh.</summary>
        internal void SetItemVersion(
            int index,
            object value,
            bool hasValue)
        {
            ValidateItemIndex(index);

            if (_itemVersions == null)
            {
                throw new InvalidOperationException(
                    "The item version snapshot has not been initialized.");
            }

            ItemVersionSnapshot snapshot = new ItemVersionSnapshot();
            snapshot.Value = value;
            snapshot.HasValue = hasValue;
            _itemVersions[index] = snapshot;
        }

        /// <summary>
        /// Reads one captured version without invoking application code. False
        /// means this model has no version snapshot at all.
        /// </summary>
        internal bool TryGetItemVersion(
            int index,
            out object value,
            out bool hasValue)
        {
            ValidateItemIndex(index);

            if (_itemVersions == null)
            {
                value = null;
                hasValue = false;
                return false;
            }

            ItemVersionSnapshot snapshot =
                _itemVersions[index] as ItemVersionSnapshot;

            if (snapshot == null)
            {
                value = null;
                hasValue = false;
                return false;
            }

            value = snapshot.Value;
            hasValue = snapshot.HasValue;
            return true;
        }

        /// <summary>Gets one item's nonnegative extent.</summary>
        internal long GetExtent(int index)
        {
            ValidateItemIndex(index);

            return _uniform
                ? _uniformExtent
                : _extents[index];
        }

        /// <summary>
        /// Gets the prefix extent before index. Count is accepted and returns
        /// TotalExtent, which makes end-boundary calculations allocation-free.
        /// </summary>
        internal long GetOffset(int index)
        {
            if (index < 0 || index > _count)
                throw new ArgumentOutOfRangeException("index");

            if (_uniform)
                return (long)index * _uniformExtent;

            long sum = 0;
            int treeIndex = index;

            while (treeIndex > 0)
            {
                sum += _tree[treeIndex];
                treeIndex -= treeIndex & -treeIndex;
            }

            return sum;
        }

        /// <summary>
        /// Finds the included item whose half-open extent contains offset.
        /// Zero-extent items are skipped. Empty/all-zero models return -1 for
        /// offset zero; every other out-of-content offset is rejected.
        /// </summary>
        internal int FindIndexAtOffset(long offset)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (_totalExtent == 0)
            {
                if (offset == 0)
                    return -1;

                throw new ArgumentOutOfRangeException("offset");
            }

            if (offset >= _totalExtent)
                throw new ArgumentOutOfRangeException("offset");

            if (_uniform)
                return (int)(offset / _uniformExtent);

            // Fenwick lower-bound: retain the largest prefix whose extent is
            // <= offset. The resulting item is the first prefix strictly after
            // offset, naturally advancing across any zero-extent entries.
            int index = 0;
            long prefix = 0;
            int step = HighestPowerOfTwoAtMost(_count);

            while (step != 0)
            {
                long candidateLong = (long)index + (long)step;

                if (candidateLong <= _count)
                {
                    int candidate = (int)candidateLong;
                    long nodeExtent = _tree[candidate];

                    // Accepted Fenwick nodes are disjoint. Subtraction avoids
                    // even a theoretical overflowing prefix + node expression.
                    if (nodeExtent <= offset - prefix)
                    {
                        index = candidate;
                        prefix += nodeExtent;
                    }
                }

                step >>= 1;
            }

            return index;
        }

        /// <summary>Updates one extent in a general model.</summary>
        internal void SetExtent(int index, long extent)
        {
            ValidateItemIndex(index);

            if (extent < 0)
                throw new ArgumentOutOfRangeException("extent");

            if (_uniform)
            {
                throw new InvalidOperationException(
                    "A uniform viewport model has a fixed item extent.");
            }

            long previous = _extents[index];

            if (previous == extent)
                return;

            long retainedTotal = _totalExtent - previous;

            if (extent > Int64.MaxValue - retainedTotal)
            {
                throw new OverflowException(
                    "The viewport extent total exceeds Int64.MaxValue.");
            }

            long difference = extent - previous;
            long replacementTotal = retainedTotal + extent;
            int treeIndex = index + 1;

            // Total validation above makes every affected Fenwick node update
            // safe. Publish the point value only after validation succeeds.
            _extents[index] = extent;

            while (treeIndex <= _count)
            {
                _tree[treeIndex] += difference;

                int increment = treeIndex & -treeIndex;
                long next = (long)treeIndex + (long)increment;

                if (next > _count)
                    break;

                treeIndex = (int)next;
            }

            _totalExtent = replacementTotal;
        }

        private void ValidateItemIndex(int index)
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException("index");
        }

        private static int HighestPowerOfTwoAtMost(int value)
        {
            int result = 1;

            while (result <= value / 2)
                result <<= 1;

            return result;
        }
    }
}
