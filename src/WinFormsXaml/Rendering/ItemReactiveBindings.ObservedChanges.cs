using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private void OnRenderBindingSlotObservableChanged(
            object owner,
            long revision)
        {
            RenderBindingSlot slot = owner as RenderBindingSlot;

            if (slot == null ||
                slot.ObservableRegistration == null ||
                !IsObservableBindingCurrent(
                    slot.ObservableRegistration,
                    slot,
                    revision))
            {
                return;
            }

            ItemsControl host = slot.Host;

            if (host == null ||
                host.IsDisposed ||
                host.Disposing ||
                _dynamicFeaturesDisposed)
            {
                return;
            }

            BindingExpressionPlan directPlan;
            BindingPathResult pathResult;

            if (TryResolveRenderBindingSlotPathResult(
                    slot,
                    slot.DataContext,
                    out directPlan,
                    out pathResult))
            {
                slot.DirectPlan = directPlan;
                SetRenderBindingSlotSubscription(
                    slot,
                    host,
                    slot.DataContext,
                    pathResult,
                    true,
                    true);
            }
            else
            {
                slot.ReactiveDirty = true;
            }

            if (CanPatchReactiveItemSlot(host, slot))
                QueueReactiveItemPatch(host, slot);
            else
                QueueReactiveItemReload(host);
        }

        private static void OnObservableTargetValueCommitted(
            object owner,
            object committedTargetValue)
        {
            RenderBindingSlot slot = owner as RenderBindingSlot;

            if (slot != null)
            {
                CommitRenderBindingSlotValue(
                    slot,
                    committedTargetValue);
            }
        }

        private void DiscardPendingReactiveItemUpdate(
            ItemsControl host)
        {
            ReactiveItemUpdateBatch pending =
                DetachPendingReactiveItemUpdate(host);

            if (pending != null)
            {
                pending.Slots.Clear();
                pending.SlotSet.Clear();
            }
        }

        private ReactiveItemUpdateBatch DetachPendingReactiveItemUpdate(
            ItemsControl host)
        {
            if (host == null)
                return null;

            lock (_reactiveItemUpdateSync)
            {
                ReactiveItemUpdateBatch pending =
                    _pendingReactiveItemUpdates[host] as
                        ReactiveItemUpdateBatch;

                _pendingReactiveItemUpdates.Remove(host);
                return pending;
            }
        }

        private bool TryApplyObservedItemListChanges(
            ItemsControl host,
            IBindingList source,
            ArrayList changes,
            ArrayList changedIndices)
        {
            if (host == null ||
                source == null ||
                changes == null ||
                changes.Count == 0 ||
                host.IsDisposed ||
                host.Disposing ||
                host.PendingRefresh != null ||
                !Object.ReferenceEquals(host.ItemSource, source) ||
                !Object.ReferenceEquals(host.CommittedItemSource, source) ||
                host.ItemValues == null ||
                !Object.ReferenceEquals(
                    host.ItemValues,
                    host.CommittedItemValues))
            {
                return false;
            }

            bool itemChangesOnly = true;
            int i;

            for (i = 0; i < changes.Count; i++)
            {
                ItemsControl.ObservedItemListChange change =
                    changes[i] as
                        ItemsControl.ObservedItemListChange;

                if (change == null)
                    return false;

                if (change.Type != ListChangedType.ItemChanged)
                    itemChangesOnly = false;
            }

            if (itemChangesOnly &&
                TryApplyObservedItemChanges(
                    host,
                    source,
                    changes,
                    changedIndices))
            {
                return true;
            }

            ArrayList previousValues = host.ItemValues;
            ArrayList nextValues = CloneArrayList(previousValues);

            for (i = 0; i < changes.Count; i++)
            {
                ItemsControl.ObservedItemListChange change =
                    (ItemsControl.ObservedItemListChange)changes[i];

                switch (change.Type)
                {
                    case ListChangedType.ItemAdded:
                        if (change.NewIndex < 0 ||
                            change.NewIndex > nextValues.Count)
                        {
                            return false;
                        }

                        nextValues.Insert(
                            change.NewIndex,
                            change.Item);
                        break;

                    case ListChangedType.ItemDeleted:
                        if (change.NewIndex < 0 ||
                            change.NewIndex >= nextValues.Count)
                        {
                            return false;
                        }

                        nextValues.RemoveAt(change.NewIndex);
                        break;

                    case ListChangedType.ItemMoved:
                        if (change.OldIndex < 0 ||
                            change.OldIndex >= nextValues.Count ||
                            change.NewIndex < 0 ||
                            change.NewIndex >= nextValues.Count ||
                            !AreObservedItemValuesSame(
                                nextValues[change.OldIndex],
                                change.Item))
                        {
                            return false;
                        }

                        object movedItem =
                            nextValues[change.OldIndex];
                        nextValues.RemoveAt(change.OldIndex);
                        nextValues.Insert(
                            change.NewIndex,
                            movedItem);
                        break;

                    case ListChangedType.ItemChanged:
                        if (change.NewIndex < 0 ||
                            change.NewIndex >= nextValues.Count)
                        {
                            return false;
                        }

                        // ItemChanged can be either an in-place notification
                        // or an object/value replacement. Publish the captured
                        // value in both cases so boxed value types do not keep
                        // an older, merely Equals-equivalent payload.
                        nextValues[change.NewIndex] =
                            change.Item;
                        break;

                    default:
                        return false;
                }
            }

            try
            {
                if (source.Count != nextValues.Count)
                    return false;

                for (i = 0; i < nextValues.Count; i++)
                {
                    object finalItem = source[i];

                    if (!AreObservedItemValuesSame(
                            nextValues[i],
                            finalItem))
                    {
                        return false;
                    }

                    if (!Object.ReferenceEquals(
                            nextValues[i],
                            finalItem))
                    {
                        // Equality identifies a value-type occurrence; retain
                        // the source's exact final box for later binding reads.
                        nextValues[i] = finalItem;
                    }
                }
            }
            catch
            {
                return false;
            }

            // Structural, replacement, and mixed batches use the existing
            // transactional renderer. Supplying the exact event-derived
            // snapshot avoids re-enumerating the source; the normal keyed
            // planner remains the authority for controls, subscriptions,
            // conditions, viewport eligibility, rollback, and progressive work.
            DiscardPendingReactiveItemUpdate(host);
            host.ItemValues = nextValues;

            try
            {
                BeginItemsRefresh(host, false);

                if (Object.ReferenceEquals(
                        host.ItemValues,
                        nextValues) &&
                    host.PendingRefresh == null &&
                    !Object.ReferenceEquals(
                        host.CommittedItemValues,
                        nextValues))
                {
                    // Cancellation/rollback can decline a new transition when
                    // reentrant cleanup owns the host. Do not leave an
                    // uncommitted snapshot published as if it were complete.
                    host.ItemValues = previousValues;
                    return false;
                }
            }
            catch
            {
                if (Object.ReferenceEquals(host.ItemValues, nextValues) &&
                    host.PendingRefresh == null)
                {
                    host.ItemValues = previousValues;
                }

                throw;
            }

            return true;
        }

        private static bool AreObservedItemValuesSame(
            object left,
            object right)
        {
            if (Object.ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            Type type = left.GetType();

            return type.IsValueType &&
                   type == right.GetType() &&
                   Object.Equals(left, right);
        }

        /// <summary>
        /// Applies coalesced IBindingList ItemChanged notifications without
        /// re-enumerating and re-planning every sibling. Exact structural event
        /// batches can avoid source enumeration, but replacement objects,
        /// structural root conditions, and rebuild-only slots continue through
        /// the normal transactional renderer and viewport eligibility check.
        /// </summary>
        private static bool CanPatchObservedItemRecord(
            RenderedItemRecord record)
        {
            if (record == null ||
                record.Control == null ||
                record.Control.IsDisposed ||
                record.BindingSlots == null)
            {
                return false;
            }

            int i;

            for (i = 0; i < record.BindingSlots.Count; i++)
            {
                RenderBindingSlot slot =
                    record.BindingSlots[i] as RenderBindingSlot;

                if (slot == null)
                    continue;

                if (slot.Kind == RenderBindingSlotKind.RebuildOnChange ||
                    (slot.Target == null &&
                     slot.Kind == RenderBindingSlotKind.Condition) ||
                    (slot.Kind == RenderBindingSlotKind.Condition &&
                     Object.ReferenceEquals(
                         slot.Target,
                         record.Control)) ||
                    (slot.Target != null && slot.Target.IsDisposed))
                {
                    return false;
                }
            }

            return true;
        }

        private static RenderedItemRecord
            FindRenderedItemRecordAtLogicalIndex(
                ItemsControl host,
                int index)
        {
            if (host == null ||
                host.RenderedItems == null ||
                index < 0)
            {
                return null;
            }

            if (!host.DirectVirtualActive &&
                index < host.RenderedItems.Count)
            {
                RenderedItemRecord directCandidate =
                    host.RenderedItems[index] as RenderedItemRecord;

                if (directCandidate != null &&
                    directCandidate.LogicalIndex == index)
                {
                    return directCandidate;
                }
            }

            // Both renderers publish records in logical-index order. Direct
            // ranges start at an arbitrary logical index; normal ranges can
            // contain gaps when a root Condition suppresses a visual.
            int low = 0;
            int high = host.RenderedItems.Count - 1;

            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                RenderedItemRecord record =
                    host.RenderedItems[middle] as RenderedItemRecord;

                if (record == null)
                    return null;

                if (record.LogicalIndex == index)
                    return record;

                if (record.LogicalIndex < index)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return null;
        }

        private static int GetRenderedItemLogicalIndex(
            ItemsControl host,
            RenderedItemRecord record)
        {
            if (host == null || record == null ||
                record.Control == null ||
                host.RenderedItems == null ||
                !Object.ReferenceEquals(record.Owner, host) ||
                !Object.ReferenceEquals(
                    host.FindRenderedItemRecordByRoot(record.Control),
                    record))
            {
                return -1;
            }

            return record.LogicalIndex;
        }

        private bool TryCoverObservedItemChangesWithReactiveBatch(
            ItemsControl host,
            IBindingList source,
            ArrayList changes)
        {
            ReactiveItemUpdateBatch batch;
            Hashtable pendingSlots;

            lock (_reactiveItemUpdateSync)
            {
                batch =
                    _pendingReactiveItemUpdates[host] as
                        ReactiveItemUpdateBatch;

                if (batch == null ||
                    batch.ReloadRequired ||
                    batch.Slots.Count == 0)
                {
                    return false;
                }

                pendingSlots = new Hashtable(batch.SlotSet);
            }

            ArrayList propertyChanges =
                BuildExactObservedItemPropertyChanges(
                    source,
                    changes);

            if (propertyChanges == null ||
                propertyChanges.Count == 0)
            {
                return false;
            }

            int i;

            try
            {
                if (source.Count != host.ItemValues.Count)
                    return false;

                for (i = 0; i < propertyChanges.Count; i++)
                {
                    ItemsControl.ObservedItemListChange change =
                        propertyChanges[i] as
                            ItemsControl.ObservedItemListChange;

                    if (change == null ||
                        change.NewIndex < 0 ||
                        change.NewIndex >= source.Count ||
                        String.IsNullOrEmpty(change.PropertyName) ||
                        DoesObservedPropertyAffectItemIdentity(
                            host,
                            change.PropertyName))
                    {
                        return false;
                    }

                    object currentItem = source[change.NewIndex];

                    if (!Object.ReferenceEquals(
                            currentItem,
                            change.Item) ||
                        !Object.ReferenceEquals(
                            host.ItemValues[change.NewIndex],
                            change.Item))
                    {
                        return false;
                    }

                    RenderedItemRecord record =
                        FindRenderedItemRecordAtLogicalIndex(
                            host,
                            change.NewIndex);

                    if (record == null ||
                        record.Control == null ||
                        record.Control.IsDisposed ||
                        record.BindingSlots == null ||
                        !Object.ReferenceEquals(
                            record.Item,
                            change.Item))
                    {
                        // A virtualized occurrence outside the realized range has no
                        // precise slot transaction to own its update.
                        return false;
                    }

                    bool affectedSlotFound = false;
                    int slotIndex;

                    for (slotIndex = 0;
                         slotIndex < record.BindingSlots.Count;
                         slotIndex++)
                    {
                        RenderBindingSlot slot =
                            record.BindingSlots[slotIndex] as
                                RenderBindingSlot;
                        bool structuralSlot =
                            slot != null &&
                            (slot.Kind ==
                                RenderBindingSlotKind.Condition ||
                             slot.Kind ==
                                RenderBindingSlotKind.RebuildOnChange);

                        if (structuralSlot)
                        {
                            // Conditions and rebuild-only values can depend on the
                            // complete item through Function(.) or component logic.
                            // The row planner remains authoritative even when their
                            // current observable dependency set looks unrelated.
                            return false;
                        }

                        bool observesProperty =
                            DoesRenderBindingSlotObserveItemProperty(
                                slot,
                                change.Item,
                                change.PropertyName);

                        if (slot != null &&
                            ExpressionContainsFunctionCall(
                                slot.Expression) &&
                            (!observesProperty ||
                             !pendingSlots.ContainsKey(slot)))
                        {
                            // A Function can intentionally read external state or the
                            // whole item. Suppress the broad pass only when its explicit
                            // changed-property dependency queued this exact slot.
                            return false;
                        }

                        if (!observesProperty)
                        {
                            continue;
                        }

                        affectedSlotFound = true;

                        if (!CanPatchReactiveItemSlot(host, slot) ||
                            !pendingSlots.ContainsKey(slot))
                        {
                            return false;
                        }
                    }

                    // Do not turn an unrelated ItemChanged event into a no-op. The
                    // ordinary row planner remains responsible for Function values and
                    // other dependencies that cannot be proven by a realized slot.
                    if (!affectedSlotFound)
                        return false;
                }
            }
            catch
            {
                return false;
            }

            lock (_reactiveItemUpdateSync)
            {
                ReactiveItemUpdateBatch current =
                    _pendingReactiveItemUpdates[host] as
                        ReactiveItemUpdateBatch;

                if (!Object.ReferenceEquals(current, batch) ||
                    current.ReloadRequired)
                {
                    return false;
                }

                current.RaiseRefreshCompleted = true;
                return true;
            }
        }

        private static ArrayList BuildExactObservedItemPropertyChanges(
            IBindingList source,
            ArrayList changes)
        {
            if (source == null ||
                changes == null ||
                changes.Count == 0)
            {
                return null;
            }

            ArrayList exactChanges = new ArrayList(changes.Count);
            Hashtable propertiesByIndex = new Hashtable();
            Hashtable propertiesByItem = new Hashtable(
                _runtimeObjectReferenceComparer);
            int i;

            for (i = 0; i < changes.Count; i++)
            {
                ItemsControl.ObservedItemListChange change =
                    changes[i] as ItemsControl.ObservedItemListChange;

                if (change == null ||
                    change.Type != ListChangedType.ItemChanged ||
                    change.NewIndex < 0 ||
                    change.Item == null ||
                    String.IsNullOrEmpty(change.PropertyName))
                {
                    return null;
                }

                AddExactObservedItemPropertyChange(
                    exactChanges,
                    propertiesByIndex,
                    change,
                    change.NewIndex);

                Hashtable itemProperties =
                    propertiesByItem[change.Item] as Hashtable;

                if (itemProperties == null)
                {
                    itemProperties = new Hashtable(
                        StringComparer.OrdinalIgnoreCase);
                    propertiesByItem[change.Item] = itemProperties;
                }

                if (!itemProperties.ContainsKey(change.PropertyName))
                {
                    itemProperties[change.PropertyName] = change;
                }
            }

            // A single notifying object can occupy more than one logical row.
            // Require precise coverage for every occurrence, including an
            // unrealized virtual occurrence, before suppressing the broad plan.
            try
            {
                for (i = 0; i < source.Count; i++)
                {
                    object item = source[i];
                    Hashtable itemProperties = item == null
                        ? null
                        : propertiesByItem[item] as Hashtable;

                    if (itemProperties == null)
                        continue;

                    IDictionaryEnumerator propertyEnumerator =
                        itemProperties.GetEnumerator();

                    while (propertyEnumerator.MoveNext())
                    {
                        ItemsControl.ObservedItemListChange change =
                            propertyEnumerator.Value as
                                ItemsControl.ObservedItemListChange;

                        AddExactObservedItemPropertyChange(
                            exactChanges,
                            propertiesByIndex,
                            change,
                            i);
                    }
                }
            }
            catch
            {
                return null;
            }

            return exactChanges;
        }

        private static void AddExactObservedItemPropertyChange(
            ArrayList exactChanges,
            Hashtable propertiesByIndex,
            ItemsControl.ObservedItemListChange sourceChange,
            int index)
        {
            object indexKey = index;
            Hashtable properties =
                propertiesByIndex[indexKey] as Hashtable;

            if (properties == null)
            {
                properties = new Hashtable(
                    StringComparer.OrdinalIgnoreCase);
                propertiesByIndex[indexKey] = properties;
            }

            if (properties.ContainsKey(sourceChange.PropertyName))
                return;

            properties[sourceChange.PropertyName] = true;

            ItemsControl.ObservedItemListChange exact =
                new ItemsControl.ObservedItemListChange();
            exact.Type = ListChangedType.ItemChanged;
            exact.NewIndex = index;
            exact.OldIndex = index;
            exact.Item = sourceChange.Item;
            exact.PropertyName = sourceChange.PropertyName;
            exactChanges.Add(exact);
        }

        private static bool DoesObservedPropertyAffectItemIdentity(
            ItemsControl host,
            string propertyName)
        {
            if (host == null || String.IsNullOrEmpty(propertyName))
                return true;

            if (DoesObservedPropertyMatchPathRoot(
                    propertyName,
                    host.ItemVersionPath))
            {
                return true;
            }

            if (!String.IsNullOrEmpty(host.ItemKeyPath))
            {
                return DoesObservedPropertyMatchPathRoot(
                    propertyName,
                    host.ItemKeyPath);
            }

            int i;

            for (i = 0; i < CommonItemKeyPaths.Length; i++)
            {
                if (EqualsIgnoreCase(
                        propertyName,
                        CommonItemKeyPaths[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DoesObservedPropertyMatchPathRoot(
            string propertyName,
            string path)
        {
            if (String.IsNullOrEmpty(propertyName) ||
                String.IsNullOrEmpty(path))
            {
                return false;
            }

            string[] parts = GetCachedBindingPathParts(path);

            return parts.Length > 0 &&
                   EqualsIgnoreCase(propertyName, parts[0]);
        }

        private static bool DoesRenderBindingSlotObserveItemProperty(
            RenderBindingSlot slot,
            object item,
            string propertyName)
        {
            if (slot == null ||
                slot.PathResult == null ||
                String.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            int i;

            for (i = 0;
                 i < slot.PathResult.Dependencies.Count;
                 i++)
            {
                BindingPathDependency dependency =
                    slot.PathResult.Dependencies[i] as
                        BindingPathDependency;

                if (dependency != null &&
                    Object.ReferenceEquals(
                        dependency.Source,
                        item) &&
                    EqualsIgnoreCase(
                        dependency.NotifyMemberName,
                        propertyName))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryApplyObservedItemChanges(
            ItemsControl host,
            IBindingList source,
            ArrayList changes,
            ArrayList changedIndices)
        {
            if (host == null ||
                source == null ||
                changedIndices == null ||
                changedIndices.Count == 0 ||
                host.IsDisposed ||
                host.Disposing ||
                host.PendingRefresh != null ||
                !host.ReuseItems ||
                !Object.ReferenceEquals(host.ItemSource, source) ||
                !Object.ReferenceEquals(host.CommittedItemSource, source) ||
                host.ItemValues == null ||
                !Object.ReferenceEquals(
                    host.ItemValues,
                    host.CommittedItemValues) ||
                host.RenderedItems == null ||
                (!host.DirectVirtualActive &&
                 host.RenderedItems.Count != host.ItemValues.Count))
            {
                return false;
            }

            if (TryCoverObservedItemChangesWithReactiveBatch(
                    host,
                    source,
                    changes))
            {
                return true;
            }

            ReactiveItemUpdateBatch reactiveBatch =
                DetachPendingReactiveItemUpdate(host);

            if (reactiveBatch != null)
            {
                if (reactiveBatch.ReloadRequired)
                {
                    reactiveBatch.Slots.Clear();
                    reactiveBatch.SlotSet.Clear();
                    return false;
                }

                Hashtable indexSet = new Hashtable();
                int pendingIndex;

                for (pendingIndex = 0;
                     pendingIndex < changedIndices.Count;
                     pendingIndex++)
                {
                    indexSet[changedIndices[pendingIndex]] = true;
                }

                for (pendingIndex = 0;
                     pendingIndex < reactiveBatch.Slots.Count;
                     pendingIndex++)
                {
                    RenderBindingSlot slot =
                        reactiveBatch.Slots[pendingIndex] as
                            RenderBindingSlot;
                    RenderedItemRecord record = null;
                    int recordIndex = -1;

                    if (slot != null && slot.Target != null)
                    {
                        Control itemRoot = slot.Target;

                        while (itemRoot != null &&
                               itemRoot.Parent != host)
                        {
                            itemRoot = itemRoot.Parent;
                        }

                        int knownIndex;

                        for (knownIndex = 0;
                             knownIndex < changedIndices.Count;
                             knownIndex++)
                        {
                            int candidateIndex =
                                (int)changedIndices[knownIndex];
                            RenderedItemRecord candidate =
                                FindRenderedItemRecordAtLogicalIndex(
                                    host,
                                    candidateIndex);

                            if (candidate != null &&
                                Object.ReferenceEquals(
                                    candidate.Control,
                                    itemRoot))
                            {
                                record = candidate;
                                recordIndex = candidateIndex;
                                break;
                            }
                        }

                        if (recordIndex < 0)
                        {
                            // Duplicate references can produce one BindingList index
                            // notification but one reactive slot per realized occurrence.
                            // Pay the linear lookup only for those additional records.
                            record = FindRenderedItemRecordForTarget(
                                host,
                                slot.Target);
                            recordIndex =
                                GetRenderedItemLogicalIndex(
                                    host,
                                    record);
                        }
                    }

                    if (recordIndex >= 0 &&
                        !indexSet.ContainsKey(recordIndex))
                    {
                        indexSet[recordIndex] = true;
                        changedIndices.Add(recordIndex);
                    }
                }

                reactiveBatch.Slots.Clear();
                reactiveBatch.SlotSet.Clear();
            }

            changedIndices.Sort();

            int transitionGeneration = host.RefreshGeneration;
            ArrayList prepared = new ArrayList(changedIndices.Count);
            int i;

            try
            {
                if (source.Count != host.ItemValues.Count)
                    return false;

                // Validate the complete coalesced batch before invoking any binding or
                // Function expression. Once planning starts, every record is known to
                // be locally patchable, so a later record cannot force duplicate
                // whole-source evaluation of an earlier one.
                for (i = 0; i < changedIndices.Count; i++)
                {
                    if (!OwnsItemsTransition(
                            host,
                            transitionGeneration))
                    {
                        return true;
                    }

                    int index = (int)changedIndices[i];

                    if (index < 0 ||
                        index >= source.Count ||
                        index >= host.ItemValues.Count)
                    {
                        return false;
                    }

                    RenderedItemRecord record =
                        FindRenderedItemRecordAtLogicalIndex(
                            host,
                            index);
                    object committedItem = host.ItemValues[index];
                    object currentItem = source[index];

                    // A same-instance ItemChanged is the common BindingList<T>
                    // property-notification path. Replacements need DataContext and
                    // ownership changes, which the full refresh already handles.
                    // A direct-viewport row outside the realized range has no
                    // record to patch; returning false lets the caller publish
                    // its source snapshot through BeginItemsRefresh.
                    if (record == null ||
                        !CanPatchObservedItemRecord(record) ||
                        !Object.ReferenceEquals(record.Item, committedItem) ||
                        !Object.ReferenceEquals(currentItem, committedItem))
                    {
                        return false;
                    }

                    ObservedItemPatchPlan observedPlan =
                        new ObservedItemPatchPlan();
                    observedPlan.Index = index;
                    observedPlan.Record = record;
                    observedPlan.Item = currentItem;
                    prepared.Add(observedPlan);
                }

                for (i = 0; i < prepared.Count; i++)
                {
                    ObservedItemPatchPlan observedPlan =
                        prepared[i] as ObservedItemPatchPlan;

                    if (observedPlan == null)
                        continue;

                    observedPlan.Key = GetStableItemKey(
                        host,
                        observedPlan.Item,
                        observedPlan.Index);

                    if (!OwnsItemsTransition(
                            host,
                            transitionGeneration))
                    {
                        return true;
                    }

                    observedPlan.VersionValue =
                        GetItemVersionValue(
                            host,
                            observedPlan.Item,
                            out observedPlan.HasVersionValue);

                    if (!OwnsItemsTransition(
                            host,
                            transitionGeneration))
                    {
                        return true;
                    }

                    bool versionUnchanged =
                        observedPlan.HasVersionValue &&
                        observedPlan.Record.HasVersionValue &&
                        AreFunctionResultsEquivalent(
                            observedPlan.Record.VersionValue,
                            observedPlan.VersionValue);

                    bool normalDataKnownUnchanged =
                        versionUnchanged;

                    observedPlan.Patch = CreateItemPatchPlan(
                        host,
                        observedPlan.Record,
                        observedPlan.Item,
                        normalDataKnownUnchanged);

                    if (!OwnsItemsTransition(
                            host,
                            transitionGeneration))
                    {
                        return true;
                    }

                    if (observedPlan.Patch.RequiresRebuild)
                        return false;
                }

                if (source.Count != host.ItemValues.Count)
                    return false;

                for (i = 0; i < prepared.Count; i++)
                {
                    ObservedItemPatchPlan observedPlan =
                        prepared[i] as ObservedItemPatchPlan;

                    if (observedPlan == null)
                        continue;

                    if (observedPlan.Index < 0 ||
                        observedPlan.Index >= source.Count ||
                        !Object.ReferenceEquals(
                            source[observedPlan.Index],
                            observedPlan.Item) ||
                        !Object.ReferenceEquals(
                            host.ItemValues[observedPlan.Index],
                            observedPlan.Item))
                    {
                        return false;
                    }
                }

                if (!OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (!OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    return true;
                }

                throw ReportReactiveItemUpdateFailure(
                    host,
                    ex);
            }

            bool affectsLayout = false;
            int appliedPlanCount = 0;

            try
            {
                for (i = 0; i < prepared.Count; i++)
                {
                    ObservedItemPatchPlan observedPlan =
                        prepared[i] as ObservedItemPatchPlan;

                    if (observedPlan == null ||
                        observedPlan.Patch == null)
                    {
                        continue;
                    }

                    appliedPlanCount = i + 1;

                    if (!ApplyItemPatchPlan(
                            null,
                            observedPlan.Patch,
                            host,
                            transitionGeneration))
                    {
                        return true;
                    }

                    if (observedPlan.Patch.AffectsLayout)
                    {
                        affectsLayout = true;
                        observedPlan.Record.MeasureCacheValid = false;
                    }

                    SetReactiveItemPatchDirtyState(
                        observedPlan.Patch,
                        false);
                }

                if (affectsLayout &&
                    OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    host.PerformLayout();
                }
            }
            catch (Exception ex)
            {
                if (!OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    return true;
                }

                ArrayList appliedPlans = new ArrayList(prepared.Count);

                for (i = 0; i < prepared.Count; i++)
                {
                    ObservedItemPatchPlan observedPlan =
                        prepared[i] as ObservedItemPatchPlan;

                    appliedPlans.Add(
                        observedPlan == null
                            ? null
                            : observedPlan.Patch);
                }

                Exception rollbackError =
                    RollbackReactiveItemPatchPlans(
                        host,
                        transitionGeneration,
                        appliedPlans,
                        appliedPlanCount,
                        affectsLayout);

                if (!OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    return true;
                }

                SetReactiveItemPatchDirtyState(
                    appliedPlans,
                    true);
                ex = IncludeItemsRollbackError(
                    ex,
                    rollbackError);

                throw ReportReactiveItemUpdateFailure(
                    host,
                    ex);
            }

            if (!OwnsItemsTransition(
                    host,
                    transitionGeneration))
            {
                return true;
            }

            for (i = 0; i < prepared.Count; i++)
            {
                ObservedItemPatchPlan observedPlan =
                    prepared[i] as ObservedItemPatchPlan;

                if (observedPlan == null ||
                    observedPlan.Record == null)
                {
                    continue;
                }

                observedPlan.Record.HasVersionValue =
                    observedPlan.HasVersionValue;
                observedPlan.Record.Key = observedPlan.Key;
                observedPlan.Record.VersionValue =
                    observedPlan.VersionValue;
            }

            host.SetRefreshing(false, null);
            host.RaiseRefreshCompleted();
            return true;
        }

    }
}
