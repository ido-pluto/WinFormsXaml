using System;
using System.Collections;
using System.ComponentModel;

namespace WinFormsXaml
{
    /// <summary>
    /// Provides count-and-index reads over an item source. Indexed sources stay
    /// live; a non-indexed enumerable is materialized exactly once.
    /// </summary>
    internal sealed class VirtualItemSourceAdapter
    {
        private readonly IList _items;
        private readonly IBindingList _bindingList;
        private readonly bool _isSnapshot;

        private VirtualItemSourceAdapter(
            IList items,
            IBindingList bindingList,
            bool isSnapshot)
        {
            _items = items;
            _bindingList = bindingList;
            _isSnapshot = isSnapshot;
        }

        /// <summary>
        /// Creates an indexed view over <paramref name="source"/>. A null source
        /// is represented by an empty snapshot.
        /// </summary>
        internal static VirtualItemSourceAdapter Create(
            IEnumerable source)
        {
            if (source == null)
            {
                return new VirtualItemSourceAdapter(
                    new ArrayList(0),
                    null,
                    true);
            }

            IList indexed = source as IList;

            if (indexed != null)
            {
                return new VirtualItemSourceAdapter(
                    indexed,
                    indexed as IBindingList,
                    false);
            }

            return new VirtualItemSourceAdapter(
                Snapshot(source),
                null,
                true);
        }

        /// <summary>
        /// Gets the current count. Live indexed sources are queried on every
        /// access; snapshots retain the count captured during creation.
        /// </summary>
        internal int Count
        {
            get { return _items.Count; }
        }

        /// <summary>
        /// Gets whether this adapter owns an internal snapshot rather than
        /// reading a caller-owned indexed collection directly.
        /// </summary>
        internal bool IsSnapshot
        {
            get { return _isSnapshot; }
        }

        /// <summary>
        /// Gets the live binding list, when the indexed source implements
        /// IBindingList. The adapter never subscribes to or disposes it.
        /// </summary>
        internal IBindingList BindingList
        {
            get { return _bindingList; }
        }

        /// <summary>Gets one item after applying consistent bounds checks.</summary>
        internal object GetItem(int index)
        {
            int count = _items.Count;

            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException("index");

            return _items[index];
        }

        private static ArrayList Snapshot(IEnumerable source)
        {
            ArrayList snapshot = new ArrayList();
            IEnumerator enumerator = source.GetEnumerator();

            if (enumerator == null)
            {
                throw new InvalidOperationException(
                    "The item source returned a null enumerator.");
            }

            IDisposable disposable = enumerator as IDisposable;

            try
            {
                while (enumerator.MoveNext())
                    snapshot.Add(enumerator.Current);
            }
            catch
            {
                // Preserve the enumeration failure, including its original
                // stack, if a hostile enumerator also fails during cleanup.
                if (disposable != null)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                    }
                }

                throw;
            }

            // A disposal failure after successful enumeration is part of that
            // enumeration operation and must remain observable to the caller.
            if (disposable != null)
                disposable.Dispose();

            return snapshot;
        }
    }
}
