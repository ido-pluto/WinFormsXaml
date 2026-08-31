using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace WinFormsXaml
{
    /// <summary>
    /// An observable .NET 2.0-compatible item list for ItemsControl.ItemsSource.
    /// </summary>
    public sealed partial class ItemsBinding<T> : BindingList<T>
    {
        private static readonly bool CanPlanAgainstLiveList =
            !typeof(T).IsValueType;
        private long _replaceRequestVersion;

        /// <summary>Creates an empty observable item list.</summary>
        public ItemsBinding()
        {
        }

        /// <summary>
        /// Creates an observable list from a snapshot of the supplied items.
        /// </summary>
        public ItemsBinding(IList<T> items)
            : base(SnapshotItems(items))
        {
        }

        /// <summary>
        /// Re-evaluates every item in each ItemsControl that observes this
        /// binding. Compatible keyed controls are still reused and patched.
        /// </summary>
        public void ReloadItems()
        {
            ResetBindings();
        }

        /// <summary>
        /// Publishes a precise reload for one occurrence at its current logical
        /// list index, allowing observing ItemsControls to patch only that row
        /// when their template permits it.
        /// </summary>
        public void ReloadItem(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException("index");

            ResetItem(index);
        }

        /// <summary>
        /// Adds a sequence while publishing at most one reset notification.
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException("items");

            List<T> snapshot = new List<T>(items);

            if (snapshot.Count == 0)
                return;

            bool previousRaiseListChangedEvents = RaiseListChangedEvents;
            bool changed = false;
            RaiseListChangedEvents = false;

            try
            {
                int i;

                for (i = 0; i < snapshot.Count; i++)
                {
                    Add(snapshot[i]);
                    changed = true;
                }
            }
            finally
            {
                RaiseListChangedEvents = previousRaiseListChangedEvents;

                if (changed && previousRaiseListChangedEvents)
                    ResetBindings();
            }
        }

        /// <summary>
        /// Reconciles the list with a snapshot and publishes only its planned
        /// structural changes. Large, unrelated changes use one reset.
        /// </summary>
        public void Replace(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException("items");

            if (Object.ReferenceEquals(items, this))
                return;

            long previousRequestVersion = _replaceRequestVersion;
            long requestVersion = BeginReplaceRequest();
            List<T> replacement;

            try
            {
                replacement = new List<T>(items);
            }
            catch
            {
                RestoreFailedReplaceRequest(
                    requestVersion,
                    previousRequestVersion);
                throw;
            }

            // Enumerating a caller-owned source can execute user code. If that
            // code starts a newer replacement, it owns the final state and this
            // older request must not resume with a stale snapshot.
            if (!IsCurrentReplaceRequest(requestVersion))
                return;

            IList<T> current = CanPlanAgainstLiveList
                ? (IList<T>)this
                : SnapshotCurrentItems();
            List<ItemsBindingDiff<T>.Operation> operations;

            // Reference items are compared only by ReferenceEquals and
            // RuntimeHelpers.GetHashCode, so TryPlan cannot invoke user code
            // while reading this list. Avoid its otherwise unconditional O(n)
            // snapshot. Value equality can execute custom struct code; retain
            // the isolated snapshot for that reentrant edge case.
            bool planned;

            try
            {
                planned = ItemsBindingDiff<T>.TryPlan(
                    current,
                    replacement,
                    out operations);
            }
            catch
            {
                RestoreFailedReplaceRequest(
                    requestVersion,
                    previousRequestVersion);
                throw;
            }

            // Value-type equality and hashing can also execute user code.
            if (!IsCurrentReplaceRequest(requestVersion))
                return;

            if (!planned)
            {
                ReplaceWithReset(replacement, requestVersion);
                return;
            }

            ApplyOperations(operations, requestVersion);
        }

        private long BeginReplaceRequest()
        {
            unchecked
            {
                _replaceRequestVersion++;
            }

            return _replaceRequestVersion;
        }

        private bool IsCurrentReplaceRequest(long requestVersion)
        {
            return _replaceRequestVersion == requestVersion;
        }

        private void RestoreFailedReplaceRequest(
            long requestVersion,
            long previousRequestVersion)
        {
            // A failed request has not committed any list edit. Restore the
            // enclosing request only when no still-newer replacement ran while
            // source enumeration or equality comparison executed user code.
            if (IsCurrentReplaceRequest(requestVersion))
                _replaceRequestVersion = previousRequestVersion;
        }

        private List<T> SnapshotCurrentItems()
        {
            List<T> snapshot = new List<T>(Count);
            int i;

            for (i = 0; i < Count; i++)
                snapshot.Add(this[i]);

            return snapshot;
        }

        private void ApplyOperations(
            IList<ItemsBindingDiff<T>.Operation> operations,
            long requestVersion)
        {
            int i;

            for (i = 0; i < operations.Count; i++)
            {
                // Each operation can synchronously raise ListChanged. A handler
                // may call Replace again; never apply the remaining indices from
                // the now-obsolete plan over that newer result.
                if (!IsCurrentReplaceRequest(requestVersion))
                    return;

                ItemsBindingDiff<T>.Operation operation = operations[i];

                switch (operation.Type)
                {
                    case ItemsBindingDiff<T>.OperationType.Insert:
                        Insert(operation.Index, operation.Value);
                        break;

                    case ItemsBindingDiff<T>.OperationType.Remove:
                        RemoveAt(operation.Index);
                        break;

                    case ItemsBindingDiff<T>.OperationType.Replace:
                        this[operation.Index] = operation.Value;
                        break;

                    case ItemsBindingDiff<T>.OperationType.Move:
                        ApplyMove(operation.OldIndex, operation.Index);
                        break;
                }
            }
        }

        private void ApplyMove(int oldIndex, int newIndex)
        {
            T item = this[oldIndex];
            bool previousRaiseListChangedEvents = RaiseListChangedEvents;
            bool completed = false;
            RaiseListChangedEvents = false;

            try
            {
                // BindingList still maintains PropertyChanged subscriptions
                // while publication is disabled.
                RemoveAt(oldIndex);
                Insert(newIndex, item);
                completed = true;
            }
            finally
            {
                RaiseListChangedEvents = previousRaiseListChangedEvents;
            }

            if (completed && previousRaiseListChangedEvents)
            {
                OnListChanged(
                    new ListChangedEventArgs(
                        ListChangedType.ItemMoved,
                        newIndex,
                        oldIndex));
            }
        }

        private void ReplaceWithReset(
            IList<T> replacement,
            long requestVersion)
        {
            if (!IsCurrentReplaceRequest(requestVersion))
                return;

            bool previousRaiseListChangedEvents = RaiseListChangedEvents;
            bool changed = false;
            RaiseListChangedEvents = false;

            try
            {
                Clear();
                changed = true;
                int i;

                for (i = 0; i < replacement.Count; i++)
                {
                    if (!IsCurrentReplaceRequest(requestVersion))
                        return;

                    Add(replacement[i]);
                }
            }
            finally
            {
                RaiseListChangedEvents = previousRaiseListChangedEvents;

                if (changed && previousRaiseListChangedEvents)
                    ResetBindings();
            }
        }

        private static IList<T> SnapshotItems(IList<T> items)
        {
            if (items == null)
                throw new ArgumentNullException("items");

            // BindingList(IList<T>) keeps and mutates the supplied list. An
            // ItemsBinding owns its observable state, so accepting a caller's
            // list must not make later Add/Remove/Replace operations mutate
            // that caller-owned collection behind its back.
            return new List<T>(items);
        }
    }
}
