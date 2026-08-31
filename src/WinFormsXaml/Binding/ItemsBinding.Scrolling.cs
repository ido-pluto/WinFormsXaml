using System;
using System.Collections;
using System.Collections.Generic;

namespace WinFormsXaml
{
    internal sealed class ItemsBindingScrollRequest
    {
        internal readonly int Index;
        internal readonly object Item;
        internal readonly bool ResolveItem;
        internal readonly ItemScrollAlignment Alignment;
        internal readonly bool HasAnimationOverride;
        internal readonly bool Animate;

        internal ItemsBindingScrollRequest(
            int index,
            object item,
            bool resolveItem,
            ItemScrollAlignment alignment,
            bool hasAnimationOverride,
            bool animate)
        {
            Index = index;
            Item = item;
            ResolveItem = resolveItem;
            Alignment = alignment;
            HasAnimationOverride = hasAnimationOverride;
            Animate = animate;
        }
    }

    internal interface IItemsBindingScrollObserver
    {
        void OnItemsBindingScrollRequested(
            object source,
            ItemsBindingScrollRequest request);
    }

    internal interface IItemsBindingScrollSource
    {
        void AddScrollObserver(IItemsBindingScrollObserver observer);
        void RemoveScrollObserver(IItemsBindingScrollObserver observer);
        int ResolveScrollIndex(ItemsBindingScrollRequest request);
    }

    public sealed partial class ItemsBinding<T> :
        IItemsBindingScrollSource
    {
        private readonly object _scrollObserverSync = new object();
        private ArrayList _scrollObservers;

        /// <summary>
        /// Scrolls the first equal item into the nearest visible position in
        /// every ItemsControl currently observing this binding. Each host uses
        /// its SmoothScroll setting.
        /// </summary>
        public void ScrollIntoView(T item)
        {
            RequestItemScrollByItem(
                item,
                ItemScrollAlignment.Nearest,
                false,
                false);
        }

        /// <summary>
        /// Scrolls the first equal item to the requested alignment in every
        /// observing ItemsControl. Each host uses its SmoothScroll setting.
        /// </summary>
        public void ScrollIntoView(
            T item,
            ItemScrollAlignment alignment)
        {
            RequestItemScrollByItem(
                item,
                alignment,
                false,
                false);
        }

        /// <summary>
        /// Scrolls the first equal item in every observing ItemsControl and
        /// explicitly selects animated or immediate movement.
        /// </summary>
        public void ScrollIntoView(
            T item,
            ItemScrollAlignment alignment,
            bool animate)
        {
            RequestItemScrollByItem(
                item,
                alignment,
                true,
                animate);
        }

        /// <summary>
        /// Scrolls one exact logical occurrence into the nearest visible
        /// position in every observing ItemsControl. This name remains
        /// unambiguous when T itself is Int32.
        /// </summary>
        public void ScrollIndexIntoView(int index)
        {
            RequestItemScroll(
                ValidateScrollIndex(index),
                null,
                false,
                ItemScrollAlignment.Nearest,
                false,
                false);
        }

        /// <summary>
        /// Scrolls one exact logical occurrence to the requested alignment in
        /// every observing ItemsControl. Each host uses its SmoothScroll setting.
        /// </summary>
        public void ScrollIndexIntoView(
            int index,
            ItemScrollAlignment alignment)
        {
            RequestItemScroll(
                ValidateScrollIndex(index),
                null,
                false,
                alignment,
                false,
                false);
        }

        /// <summary>
        /// Scrolls one exact logical occurrence in every observing
        /// ItemsControl and explicitly selects animated or immediate movement.
        /// </summary>
        public void ScrollIndexIntoView(
            int index,
            ItemScrollAlignment alignment,
            bool animate)
        {
            RequestItemScroll(
                ValidateScrollIndex(index),
                null,
                false,
                alignment,
                true,
                animate);
        }

        private int FindFirstEqualIndex(T item, bool throwIfMissing)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            int i;

            for (i = 0; i < Count; i++)
            {
                if (comparer.Equals(this[i], item))
                    return i;
            }

            if (throwIfMissing)
            {
                throw new ArgumentException(
                    "The requested item is not present in this ItemsBinding.",
                    "item");
            }

            return -1;
        }

        private int ValidateScrollIndex(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException("index");

            return index;
        }

        private static void ValidateScrollAlignment(
            ItemScrollAlignment alignment)
        {
            if (alignment < ItemScrollAlignment.Nearest ||
                alignment > ItemScrollAlignment.End)
            {
                throw new ArgumentOutOfRangeException("alignment");
            }
        }

        private void RequestItemScrollByItem(
            T item,
            ItemScrollAlignment alignment,
            bool hasAnimationOverride,
            bool animate)
        {
            int index = FindFirstEqualIndex(item, true);

            RequestItemScroll(
                index,
                item,
                true,
                alignment,
                hasAnimationOverride,
                animate);
        }

        private void RequestItemScroll(
            int index,
            object item,
            bool resolveItem,
            ItemScrollAlignment alignment,
            bool hasAnimationOverride,
            bool animate)
        {
            ValidateScrollAlignment(alignment);

            ItemsBindingScrollRequest request =
                new ItemsBindingScrollRequest(
                    index,
                    item,
                    resolveItem,
                    alignment,
                    hasAnimationOverride,
                    animate);
            IItemsBindingScrollObserver[] observers =
                SnapshotScrollObservers();
            Exception firstError = null;
            int i;

            for (i = 0; i < observers.Length; i++)
            {
                try
                {
                    observers[i].OnItemsBindingScrollRequested(
                        this,
                        request);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (firstError != null)
                throw firstError;
        }

        int IItemsBindingScrollSource.ResolveScrollIndex(
            ItemsBindingScrollRequest request)
        {
            if (request == null)
                return -1;

            if (!request.ResolveItem)
            {
                return request.Index >= 0 && request.Index < Count
                    ? request.Index
                    : -1;
            }

            T item;

            try
            {
                item = (T)request.Item;
            }
            catch (InvalidCastException)
            {
                return -1;
            }

            return FindFirstEqualIndex(item, false);
        }

        private IItemsBindingScrollObserver[] SnapshotScrollObservers()
        {
            lock (_scrollObserverSync)
            {
                if (_scrollObservers == null ||
                    _scrollObservers.Count == 0)
                {
                    return new IItemsBindingScrollObserver[0];
                }

                ArrayList live = new ArrayList(_scrollObservers.Count);
                int i;

                for (i = _scrollObservers.Count - 1; i >= 0; i--)
                {
                    WeakReference reference =
                        _scrollObservers[i] as WeakReference;
                    IItemsBindingScrollObserver observer = reference == null
                        ? null
                        : reference.Target as IItemsBindingScrollObserver;

                    if (observer == null)
                        _scrollObservers.RemoveAt(i);
                    else
                        live.Add(observer);
                }

                IItemsBindingScrollObserver[] result =
                    new IItemsBindingScrollObserver[live.Count];

                for (i = 0; i < live.Count; i++)
                {
                    result[i] =
                        (IItemsBindingScrollObserver)live[i];
                }

                return result;
            }
        }

        void IItemsBindingScrollSource.AddScrollObserver(
            IItemsBindingScrollObserver observer)
        {
            if (observer == null)
                throw new ArgumentNullException("observer");

            lock (_scrollObserverSync)
            {
                if (_scrollObservers == null)
                    _scrollObservers = new ArrayList();

                int i;

                for (i = _scrollObservers.Count - 1; i >= 0; i--)
                {
                    WeakReference reference =
                        _scrollObservers[i] as WeakReference;
                    object target = reference == null
                        ? null
                        : reference.Target;

                    if (target == null)
                    {
                        _scrollObservers.RemoveAt(i);
                    }
                    else if (Object.ReferenceEquals(target, observer))
                    {
                        return;
                    }
                }

                _scrollObservers.Add(new WeakReference(observer));
            }
        }

        void IItemsBindingScrollSource.RemoveScrollObserver(
            IItemsBindingScrollObserver observer)
        {
            if (observer == null)
                return;

            lock (_scrollObserverSync)
            {
                if (_scrollObservers == null)
                    return;

                int i;

                for (i = _scrollObservers.Count - 1; i >= 0; i--)
                {
                    WeakReference reference =
                        _scrollObservers[i] as WeakReference;
                    object target = reference == null
                        ? null
                        : reference.Target;

                    if (target == null ||
                        Object.ReferenceEquals(target, observer))
                    {
                        _scrollObservers.RemoveAt(i);
                    }
                }
            }
        }
    }
}
