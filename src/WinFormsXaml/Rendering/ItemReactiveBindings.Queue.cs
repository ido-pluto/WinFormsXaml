using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private void QueueReactiveItemReload(
            ItemsControl host)
        {
            if (host == null ||
                host.IsDisposed ||
                host.Disposing ||
                _dynamicFeaturesDisposed)
            {
                return;
            }

            ReactiveItemUpdateBatch batch;
            bool shouldSchedule = false;

            lock (_reactiveItemUpdateSync)
            {
                batch =
                    _pendingReactiveItemUpdates[host] as
                        ReactiveItemUpdateBatch;

                if (batch == null)
                {
                    batch = new ReactiveItemUpdateBatch();
                    batch.Host = host;
                    _pendingReactiveItemUpdates[host] = batch;
                    shouldSchedule = true;
                }

                batch.ReloadRequired = true;
                batch.Slots.Clear();
                batch.SlotSet.Clear();
            }

            if (shouldSchedule)
                ScheduleReactiveItemUpdate(batch);
        }

        private void QueueReactiveItemPatch(
            ItemsControl host,
            RenderBindingSlot slot)
        {
            if (host == null ||
                slot == null ||
                host.IsDisposed ||
                host.Disposing ||
                _dynamicFeaturesDisposed)
            {
                return;
            }

            ReactiveItemUpdateBatch batch;
            bool shouldSchedule = false;

            lock (_reactiveItemUpdateSync)
            {
                batch =
                    _pendingReactiveItemUpdates[host] as
                        ReactiveItemUpdateBatch;

                if (batch == null)
                {
                    batch = new ReactiveItemUpdateBatch();
                    batch.Host = host;
                    _pendingReactiveItemUpdates[host] = batch;
                    shouldSchedule = true;
                }

                if (!batch.ReloadRequired &&
                    !batch.SlotSet.ContainsKey(slot))
                {
                    batch.SlotSet.Add(slot, true);
                    batch.Slots.Add(slot);
                }
            }

            if (shouldSchedule)
                ScheduleReactiveItemUpdate(batch);
        }

        private void ScheduleReactiveItemUpdate(
            ReactiveItemUpdateBatch batch)
        {
            if (batch == null || batch.Host == null)
                return;

            ItemsControl host = batch.Host;

            MethodInvoker apply =
                delegate
                {
                    ApplyReactiveItemUpdateBatch(batch);
                };

            if (!host.IsHandleCreated)
            {
                ApplyReactiveItemUpdateBatch(batch);
                return;
            }

            try
            {
                host.BeginInvoke(apply);
#if !WINFORMSXAML_PACKAGE
                host.RecordReactiveItemUpdatePostForTest();
#endif
            }
            catch (ObjectDisposedException)
            {
                RemoveReactiveItemUpdateBatch(batch);
            }
            catch (InvalidOperationException)
            {
                if (!host.IsDisposed && !host.Disposing)
                    ApplyReactiveItemUpdateBatch(batch);
                else
                    RemoveReactiveItemUpdateBatch(batch);
            }
        }

        private void RemoveReactiveItemUpdateBatch(
            ReactiveItemUpdateBatch batch)
        {
            if (batch == null || batch.Host == null)
                return;

            lock (_reactiveItemUpdateSync)
            {
                if (Object.ReferenceEquals(
                        _pendingReactiveItemUpdates[batch.Host],
                        batch))
                {
                    _pendingReactiveItemUpdates.Remove(batch.Host);
                    batch.Slots.Clear();
                    batch.SlotSet.Clear();
                }
            }
        }

        private void ApplyReactiveItemUpdateBatch(
            ReactiveItemUpdateBatch batch)
        {
            if (batch == null || batch.Host == null)
                return;

            ItemsControl host = batch.Host;
            bool reloadRequired;
            bool raiseRefreshCompleted;
            ArrayList slots;

            lock (_reactiveItemUpdateSync)
            {
                if (!Object.ReferenceEquals(
                        _pendingReactiveItemUpdates[host],
                        batch))
                {
                    return;
                }

                _pendingReactiveItemUpdates.Remove(host);
                reloadRequired = batch.ReloadRequired;
                raiseRefreshCompleted = batch.RaiseRefreshCompleted;
                // Removing the batch under the same lock transfers ownership of
                // its slot list to this dispatcher. Later notifications create a
                // new batch, so copying every coalesced slot here is unnecessary.
                slots = batch.Slots;
                batch.SlotSet.Clear();
            }

            if (_dynamicFeaturesDisposed ||
                host.IsDisposed ||
                host.Disposing)
            {
                return;
            }

            if (reloadRequired || host.PendingRefresh != null)
            {
                BeginItemsRefresh(host, false);
                return;
            }

            ArrayList plans = new ArrayList(slots.Count);
            bool affectsLayout = false;
            bool fallbackRequired = false;
            int transitionGeneration = host.RefreshGeneration;
            int i;

            try
            {
                for (i = 0; i < slots.Count; i++)
                {
                    RenderBindingSlot slot =
                        slots[i] as RenderBindingSlot;
                    ItemPatchPlan plan;

                    if (!TryCreateReactiveItemPatchPlan(
                            host,
                            slot,
                            out plan))
                    {
                        fallbackRequired = true;
                        break;
                    }

                    if (plan != null)
                    {
                        plans.Add(plan);
                        affectsLayout =
                            affectsLayout || plan.AffectsLayout;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    return;
                }

                throw ReportReactiveItemUpdateFailure(
                    host,
                    ex);
            }

            if (fallbackRequired)
            {
                if (!host.IsDisposed && !host.Disposing)
                    BeginItemsRefresh(host, false);

                return;
            }

            if (!OwnsItemsTransition(
                    host,
                    transitionGeneration))
            {
                return;
            }

            int appliedPlanCount = 0;

            try
            {
                for (i = 0; i < plans.Count; i++)
                {
                    ItemPatchPlan plan =
                        plans[i] as ItemPatchPlan;

                    if (plan == null)
                        continue;

                    appliedPlanCount = i + 1;

                    if (!ApplyItemPatchPlan(
                            null,
                            plan,
                            host,
                            transitionGeneration))
                    {
                        // A native setter synchronously started newer item work. That
                        // transition now owns the visual tree and its slot metadata.
                        return;
                    }

                    if (plan.AffectsLayout && plan.Record != null)
                        plan.Record.MeasureCacheValid = false;

                    SetReactiveItemPatchDirtyState(
                        plan,
                        false);
                }

                if (affectsLayout &&
                    OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    host.PerformLayout();
                }

                if (OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    host.SetRefreshing(false, null);
                }
            }
            catch (Exception ex)
            {
                if (!OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    return;
                }

                Exception rollbackError =
                    RollbackReactiveItemPatchPlans(
                        host,
                        transitionGeneration,
                        plans,
                        appliedPlanCount,
                        affectsLayout);

                if (!OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    return;
                }

                SetReactiveItemPatchDirtyState(
                    plans,
                    true);

                ex = IncludeItemsRollbackError(
                    ex,
                    rollbackError);

                throw ReportReactiveItemUpdateFailure(
                    host,
                    ex);
            }

            if (raiseRefreshCompleted &&
                OwnsItemsTransition(
                    host,
                    transitionGeneration))
            {
                host.RaiseRefreshCompleted();
            }
        }

        private bool CanPatchReactiveItemSlot(
            ItemsControl host,
            RenderBindingSlot slot)
        {
            if (host == null ||
                slot == null ||
                slot.Target == null ||
                slot.Target.IsDisposed ||
                slot.Kind == RenderBindingSlotKind.RebuildOnChange)
            {
                return false;
            }

            if (slot.Kind != RenderBindingSlotKind.Condition)
                return true;

            RenderedItemRecord record =
                FindRenderedItemRecordForTarget(
                    host,
                    slot.Target);

            // Root Condition changes the logical visual structure. Let the
            // transactional renderer and direct-viewport eligibility decision
            // own that transition. Realized descendants can be patched locally.
            return record != null &&
                   !Object.ReferenceEquals(
                       record.Control,
                       slot.Target);
        }

        private bool TryCreateReactiveItemPatchPlan(
            ItemsControl host,
            RenderBindingSlot slot,
            out ItemPatchPlan plan)
        {
            plan = null;

            if (!CanPatchReactiveItemSlot(host, slot) ||
                !Object.ReferenceEquals(slot.Host, host))
            {
                return false;
            }

            BindingExpressionPlan directPlan;
            BindingPathResult pathResult;

            if (!TryResolveRenderBindingSlotPathResult(
                    slot,
                    slot.DataContext,
                    out directPlan,
                    out pathResult))
            {
                return false;
            }

            slot.DirectPlan = directPlan;
            SetRenderBindingSlotSubscription(
                slot,
                host,
                slot.DataContext,
                pathResult,
                true,
                true);

            object newValue =
                EvaluateRenderBindingSlotExpression(
                    slot,
                    slot.DataContext);

            if (!slot.ForceNextApply &&
                AreRenderBindingSlotValuesEquivalent(
                    slot,
                    newValue))
            {
                slot.ReactiveDirty = false;
                return true;
            }

            RenderedItemRecord record =
                FindRenderedItemRecordForTarget(
                    host,
                    slot.Target);

            if (record == null || record.Control == null)
                return false;

            plan = new ItemPatchPlan();
            plan.Record = record;
            plan.OldItem = record.Item;
            plan.NewItem = record.Item;
            plan.OldFunctionResults = record.FunctionResults;
            plan.FunctionResults = record.FunctionResults;
            plan.Changes = new ArrayList(1);
            plan.ReactiveChanges = new ArrayList();
            plan.RequiresRebuild = false;
            plan.AffectsLayout =
                DoesRenderBindingSlotAffectLayout(slot) ||
                slot.Kind == RenderBindingSlotKind.Condition;
            plan.AffectsInheritance =
                DoesRenderBindingSlotAffectInheritance(slot);
            plan.Applied = false;

            ItemPatchChange change = new ItemPatchChange();
            change.Slot = slot;
            change.OldValue = slot.LastValue;
            change.NewValue = newValue;
            plan.Changes.Add(change);

            return true;
        }

        private bool IsPendingReactiveItemPatchOwner(
            ItemsControl host,
            RenderedItemRecord record)
        {
            if (host == null || record == null ||
                record.BindingSlots == null)
            {
                return false;
            }

            lock (_reactiveItemUpdateSync)
            {
                ReactiveItemUpdateBatch batch =
                    _pendingReactiveItemUpdates[host] as
                        ReactiveItemUpdateBatch;

                if (batch == null || batch.ReloadRequired)
                    return false;

                bool hasDirtySlot = false;
                int i;

                for (i = 0; i < record.BindingSlots.Count; i++)
                {
                    RenderBindingSlot slot =
                        record.BindingSlots[i] as RenderBindingSlot;

                    if (slot == null || !slot.ReactiveDirty)
                        continue;

                    hasDirtySlot = true;

                    if (!batch.SlotSet.ContainsKey(slot))
                        return false;
                }

                return hasDirtySlot;
            }
        }

        private static void SetReactiveItemPatchDirtyState(
            ItemPatchPlan plan,
            bool dirty)
        {
            if (plan == null || plan.Changes == null)
                return;

            int i;

            for (i = 0; i < plan.Changes.Count; i++)
            {
                ItemPatchChange change =
                    plan.Changes[i] as ItemPatchChange;

                if (change != null && change.Slot != null)
                    change.Slot.ReactiveDirty = dirty;
            }
        }

        private static void SetReactiveItemPatchDirtyState(
            ArrayList plans,
            bool dirty)
        {
            int i;

            for (i = 0; plans != null && i < plans.Count; i++)
            {
                SetReactiveItemPatchDirtyState(
                    plans[i] as ItemPatchPlan,
                    dirty);
            }
        }

        private Exception RollbackReactiveItemPatchPlans(
            ItemsControl host,
            int transitionGeneration,
            ArrayList plans,
            int appliedPlanCount,
            bool affectsLayout)
        {
            Exception firstError = null;
            int count = Math.Min(
                appliedPlanCount,
                plans == null ? 0 : plans.Count);
            int i;

            for (i = count - 1; i >= 0; i--)
            {
                if (!OwnsItemsTransition(
                        host,
                        transitionGeneration))
                {
                    return firstError;
                }

                ItemPatchPlan plan =
                    plans[i] as ItemPatchPlan;

                if (plan == null || !plan.Applied)
                    continue;

                Control root = plan.Record == null
                    ? null
                    : plan.Record.Control;
                bool layoutSuspended = false;

                if (root != null && !root.IsDisposed)
                {
                    try
                    {
                        root.SuspendLayout();
                        layoutSuspended = true;
                    }
                    catch (Exception ex)
                    {
                        firstError = FirstItemsCommitError(
                            firstError,
                            ex);
                    }
                }

                Exception restoreError =
                    RestoreItemPatchPlan(
                        plan,
                        plan.AppliedChangeCount,
                        false,
                        host,
                        transitionGeneration);

                firstError = FirstItemsCommitError(
                    firstError,
                    restoreError);

                if (layoutSuspended)
                {
                    try
                    {
                        root.ResumeLayout(false);
                    }
                    catch (Exception ex)
                    {
                        firstError = FirstItemsCommitError(
                            firstError,
                            ex);
                    }
                }
            }

            if (affectsLayout &&
                OwnsItemsTransition(
                    host,
                    transitionGeneration))
            {
                try
                {
                    host.PerformLayout();
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(
                        firstError,
                        ex);
                }
            }

            return firstError;
        }

        private Exception ReportReactiveItemUpdateFailure(
            ItemsControl host,
            Exception error)
        {
            if (host == null || host.IsDisposed || host.Disposing)
                return error;

            host.SetRefreshing(false, error);

            try
            {
                host.RaiseRefreshFailed();
            }
            catch (Exception ex)
            {
                error = IncludeItemsRollbackError(
                    error,
                    ex);

                if (!host.IsDisposed && !host.Disposing)
                    host.SetRefreshing(false, error);
            }

            return error;
        }

        private static RenderedItemRecord FindRenderedItemRecordForTarget(
            ItemsControl host,
            Control target)
        {
            if (host == null || target == null)
                return null;

            Control root = target;

            while (root != null && root.Parent != host)
                root = root.Parent;

            if (root == null)
                return null;

            return host.FindRenderedItemRecordByRoot(root)
                as RenderedItemRecord;
        }

    }
}
