using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private bool DoesRenderBindingSlotAffectLayout(
            RenderBindingSlot slot)
        {
            if (slot == null || !slot.AffectsLayout)
                return false;

            // Replacing a PictureBox image cannot affect item measurement when both
            // dimensions are explicitly fixed. Avoid invalidating the cached item size
            // and avoid a host layout pass for the common thumbnail-update case.
            if (EqualsIgnoreCase(slot.AttributeName, "Source") &&
                slot.Target is PictureBox)
            {
                ElementInfo info = GetInfo(slot.Target);

                if (info.WidthExplicit && info.HeightExplicit)
                    return false;
            }

            return true;
        }

        private static bool DoesRenderBindingSlotAffectInheritance(
            RenderBindingSlot slot)
        {
            if (slot == null)
                return false;

            return PropertyAffectsInheritance(
                slot.Target,
                slot.AttributeName);
        }

        private int ApplyItemsPatchBatch(
            ItemsRefreshState state,
            int maximumItems)
        {
            int patchedItems = 0;

            while (state.PatchIndex < state.PatchQueue.Count &&
                   patchedItems < maximumItems &&
                   IsItemsRefreshCurrent(state))
            {
                ItemPatchPlan plan =
                    state.PatchQueue[state.PatchIndex] as ItemPatchPlan;

                if (plan != null &&
                    plan.Record != null &&
                    plan.Record.Control != null)
                {
                    if (!ApplyItemPatchPlan(state, plan))
                        return patchedItems;

                    state.AnyVisualChange = state.AnyVisualChange ||
                        plan.Changes.Count > 0;

                    if (plan.AffectsLayout)
                    {
                        state.PatchLayoutDirty = true;
                        state.AnyLayoutChange = true;

                        if (plan.Record != null)
                            plan.Record.MeasureCacheValid = false;
                    }
                }

                state.PatchIndex++;
                patchedItems++;
            }

            return patchedItems;
        }

        private void ApplyItemPatchPlan(ItemPatchPlan plan)
        {
            ApplyItemPatchPlan(null, plan);
        }

        private bool ApplyItemPatchPlan(
            ItemsRefreshState state,
            ItemPatchPlan plan)
        {
            return ApplyItemPatchPlan(
                state,
                plan,
                null,
                -1);
        }

        private bool ApplyItemPatchPlan(
            ItemsRefreshState state,
            ItemPatchPlan plan,
            ItemsControl transitionHost,
            int transitionGeneration)
        {
            RenderedItemRecord record = plan.Record;
            Control root = record.Control;
            int appliedChangeCount = 0;
            bool rootVisibilityChanged = false;
            ItemVisibilityState rootVisibility = record.RootVisibility;

            plan.RootVisibilityCaptured = true;
            plan.RootVisibilityApplied = false;
            plan.OldRootVisibility = record.RootVisibility;
            plan.OldRootConditionVisible = record.RootConditionVisible;

            // Make an in-flight setter visible to a reentrant CancelItemsRefresh.
            // WinForms property events run synchronously inside the setter.
            plan.Applied = true;
            plan.AppliedChangeCount = 0;
            plan.AppliedReactiveChangeCount = 0;
            plan.DataContextApplied = false;

            root.SuspendLayout();

            try
            {
                int i;

                for (i = 0; i < plan.Changes.Count; i++)
                {
                    ItemPatchChange change =
                        plan.Changes[i] as ItemPatchChange;

                    if (change == null || change.Slot == null)
                        continue;

                    RenderBindingSlot slot = change.Slot;
                    bool isRootVisibilitySlot =
                        Object.ReferenceEquals(slot.Target, root) &&
                        (slot.Kind == RenderBindingSlotKind.Condition ||
                         EqualsIgnoreCase(slot.AttributeName, "Visibility") ||
                         EqualsIgnoreCase(slot.AttributeName, "Visible"));

                    // Include the current change in a rollback attempt. A custom property
                    // setter can mutate its target and then throw.
                    appliedChangeCount = i + 1;
                    plan.AppliedChangeCount = appliedChangeCount;

                    if (isRootVisibilitySlot)
                        plan.RootVisibilityApplied = true;

                    ApplyRenderBindingSlotValue(
                        slot,
                        change.NewValue);

                    if (!IsItemPatchContextCurrent(
                            state,
                            transitionHost,
                            transitionGeneration))
                        return false;

                    CommitRenderBindingSlotValue(
                        slot,
                        change.NewValue);
                    slot.ForceNextApply = false;

                    if (isRootVisibilitySlot)
                    {
                        rootVisibilityChanged = true;

                        if (slot.Kind != RenderBindingSlotKind.Condition)
                        {
                            rootVisibility =
                                ConvertItemVisibilityState(
                                    change.NewValue);
                        }
                    }
                    // Native WinForms property setters already invalidate the affected
                    // child when required. Avoid issuing a second redundant paint request.
                }

                record.Item = plan.NewItem;
                record.FunctionResults = plan.FunctionResults;

                if (rootVisibilityChanged)
                {
                    record.RootVisibility = rootVisibility;
                    record.RootConditionVisible =
                        GetInitialRootConditionVisibility(
                            record.BindingSlots,
                            root);
                    ApplyItemRootVisibility(record);

                    if (!IsItemPatchContextCurrent(
                            state,
                            transitionHost,
                            transitionGeneration))
                        return false;
                }

                if (!Object.ReferenceEquals(
                    plan.OldItem,
                    plan.NewItem))
                {
                    plan.DataContextApplied = true;
                    UpdateDataContextToTree(
                        root,
                        plan.NewItem);

                    if (!IsItemPatchContextCurrent(
                            state,
                            transitionHost,
                            transitionGeneration))
                        return false;
                }

                for (i = 0; i < plan.ReactiveChanges.Count; i++)
                {
                    ItemReactiveBindingChange reactiveChange =
                        plan.ReactiveChanges[i] as
                            ItemReactiveBindingChange;

                    if (reactiveChange == null ||
                        reactiveChange.Slot == null)
                    {
                        continue;
                    }

                    plan.AppliedReactiveChangeCount = i + 1;
                    ApplyItemReactiveBindingChange(
                        reactiveChange,
                        true);

                    if (!IsItemPatchContextCurrent(
                            state,
                            transitionHost,
                            transitionGeneration))
                        return false;
                }

                if (plan.AffectsInheritance)
                {
                    ApplyInheritedProperties(
                        root,
                        root.Parent == null
                            ? transitionHost
                            : root.Parent);

                    if (!IsItemPatchContextCurrent(
                            state,
                            transitionHost,
                            transitionGeneration))
                        return false;
                }
            }
            catch
            {
                // The caller owns rollback. Both full refreshes and direct reactive
                // patches guard that rollback with their transition generation so a
                // reentrant SetItems call always keeps ownership of the newer tree.
                throw;
            }
            finally
            {
                try
                {
                    root.ResumeLayout(false);
                }
                catch
                {
                    if (IsItemPatchContextCurrent(
                            state,
                            transitionHost,
                            transitionGeneration))
                        throw;
                }
            }

            return IsItemPatchContextCurrent(
                state,
                transitionHost,
                transitionGeneration);
        }

        private static ItemVisibilityState ConvertItemVisibilityState(
            object value)
        {
            object converted;

            if (TryConvertObjectValue(
                value,
                typeof(bool),
                out converted))
            {
                return (bool)converted
                    ? ItemVisibilityState.Visible
                    : ItemVisibilityState.Hidden;
            }

            string text = BindingValueToString(value);

            if (EqualsIgnoreCase(text, "Visible"))
                return ItemVisibilityState.Visible;

            if (EqualsIgnoreCase(text, "Hidden"))
                return ItemVisibilityState.Hidden;

            if (EqualsIgnoreCase(text, "Collapsed"))
                return ItemVisibilityState.Collapsed;

            throw new InvalidOperationException(
                "Visibility binding must resolve to Visible, Hidden, " +
                "Collapsed, or a boolean-compatible value.");
        }

        private void ApplyItemRootVisibility(
            RenderedItemRecord record)
        {
            if (record == null || record.Control == null)
                return;

            ElementInfo info = GetInfo(record.Control);
            SetElementVisibilityState(
                info,
                record.RootVisibility == ItemVisibilityState.Hidden,
                record.RootVisibility == ItemVisibilityState.Collapsed);
            SetElementConditionState(
                info,
                _itemRootConditionStateKey,
                record.RootConditionVisible);
            record.IntendedVisible =
                record.RootVisibility == ItemVisibilityState.Visible &&
                record.RootConditionVisible;
            ApplyElementEffectiveVisibility(record.Control, info);
        }

        private static bool IsItemPatchContextCurrent(
            ItemsRefreshState state,
            ItemsControl transitionHost,
            int transitionGeneration)
        {
            if (state != null)
                return IsItemsRefreshCurrent(state);

            if (transitionHost != null)
            {
                return OwnsItemsTransition(
                    transitionHost,
                    transitionGeneration);
            }

            return true;
        }

        private static bool IsItemsRefreshCurrent(
            ItemsRefreshState state)
        {
            return state != null &&
                   state.Host != null &&
                   !state.Host.IsDisposed &&
                   state.Host.PendingRefresh == state &&
                   state.Generation == state.Host.RefreshGeneration;
        }

        private Exception RestoreItemPatchPlan(
            ItemPatchPlan plan,
            int appliedChangeCount,
            bool restoreDataContext)
        {
            return RestoreItemPatchPlan(
                plan,
                appliedChangeCount,
                restoreDataContext,
                null,
                -1);
        }

        private Exception RestoreItemPatchPlan(
            ItemPatchPlan plan,
            int appliedChangeCount,
            bool restoreDataContext,
            ItemsControl transitionHost,
            int transitionGeneration)
        {
            if (plan == null)
                return null;

            Exception firstError = null;
            int count = plan.Changes == null
                ? 0
                : Math.Min(appliedChangeCount, plan.Changes.Count);
            int i;

            for (i = count - 1; i >= 0; i--)
            {
                if (!OwnsItemPatchRollback(
                    transitionHost,
                    transitionGeneration))
                    return firstError;

                ItemPatchChange change =
                    plan.Changes[i] as ItemPatchChange;

                if (change == null || change.Slot == null)
                    continue;

                bool restored = false;

                try
                {
                    ApplyRenderBindingSlotValue(
                        change.Slot,
                        change.OldValue);
                    restored = true;
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }

                // Property setters can synchronously start and even commit a nested
                // refresh. Never publish stale slot metadata after ownership changes.
                if (!OwnsItemPatchRollback(
                    transitionHost,
                    transitionGeneration))
                    return firstError;

                if (restored)
                {
                    CommitRenderBindingSlotValue(
                        change.Slot,
                        change.OldValue);
                    change.Slot.ForceNextApply = false;
                }
                else
                {
                    // Preserve the real committed rollback value. A separate
                    // flag forces the next setter call without ever exposing an
                    // internal marker to conversion or to a later rollback.
                    CommitRenderBindingSlotValue(
                        change.Slot,
                        change.OldValue);
                    change.Slot.ForceNextApply = true;
                }
            }

            if (!OwnsItemPatchRollback(
                transitionHost,
                transitionGeneration))
                return firstError;

            RenderedItemRecord record = plan.Record;

            if (record != null)
            {
                if (plan.RootVisibilityCaptured &&
                    plan.RootVisibilityApplied &&
                    record.Control != null)
                {
                    record.RootVisibility = plan.OldRootVisibility;
                    record.RootConditionVisible =
                        plan.OldRootConditionVisible;

                    try
                    {
                        ApplyItemRootVisibility(record);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }

                    if (!OwnsItemPatchRollback(
                        transitionHost,
                        transitionGeneration))
                    {
                        return firstError;
                    }
                }

                record.Item = plan.OldItem;
                record.FunctionResults = plan.OldFunctionResults;

                if (restoreDataContext && record.Control != null)
                {
                    if (transitionHost == null ||
                        OwnsItemsTransition(
                            transitionHost,
                            transitionGeneration))
                    {
                        try
                        {
                            UpdateDataContextToTree(
                                record.Control,
                                plan.OldItem);
                        }
                        catch (Exception ex)
                        {
                            if (firstError == null)
                                firstError = ex;
                        }

                        if (!OwnsItemPatchRollback(
                            transitionHost,
                            transitionGeneration))
                            return firstError;
                    }
                }

            }

            int reactiveCount = plan.ReactiveChanges == null
                ? 0
                : Math.Min(
                    plan.AppliedReactiveChangeCount,
                    plan.ReactiveChanges.Count);

            for (i = reactiveCount - 1; i >= 0; i--)
            {
                if (!OwnsItemPatchRollback(
                    transitionHost,
                    transitionGeneration))
                {
                    return firstError;
                }

                ItemReactiveBindingChange reactiveChange =
                    plan.ReactiveChanges[i] as
                        ItemReactiveBindingChange;

                if (reactiveChange == null ||
                    reactiveChange.Slot == null)
                {
                    continue;
                }

                try
                {
                    ApplyItemReactiveBindingChange(
                        reactiveChange,
                        false);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (plan.AffectsInheritance &&
                count > 0 &&
                record != null &&
                record.Control != null &&
                !record.Control.IsDisposed &&
                OwnsItemPatchRollback(
                    transitionHost,
                    transitionGeneration))
            {
                try
                {
                    ApplyInheritedProperties(
                        record.Control,
                        record.Control.Parent == null
                            ? transitionHost
                            : record.Control.Parent);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (firstError == null &&
                OwnsItemPatchRollback(
                    transitionHost,
                    transitionGeneration))
            {
                plan.Applied = false;
                plan.AppliedChangeCount = 0;
                plan.AppliedReactiveChangeCount = 0;
                plan.DataContextApplied = false;
                plan.RootVisibilityApplied = false;
            }

            return firstError;
        }

        private static bool OwnsItemPatchRollback(
            ItemsControl transitionHost,
            int transitionGeneration)
        {
            return transitionHost == null ||
                   OwnsItemsTransition(
                       transitionHost,
                       transitionGeneration);
        }

        private Exception RollbackAppliedItemsPatches(
            ItemsRefreshState state,
            bool updateHost,
            int transitionGeneration)
        {
            if (state == null || state.PatchQueue == null)
                return null;

            Exception firstError = null;
            bool anyPatch = false;
            bool layoutChanged = false;
            int i;

            for (i = state.PatchQueue.Count - 1; i >= 0; i--)
            {
                if (!OwnsItemsTransition(
                    state.Host,
                    transitionGeneration))
                {
                    break;
                }

                ItemPatchPlan plan =
                    state.PatchQueue[i] as ItemPatchPlan;

                if (plan == null || !plan.Applied)
                    continue;

                anyPatch = true;
                layoutChanged = layoutChanged || plan.AffectsLayout;

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
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                Exception rollbackError = RestoreItemPatchPlan(
                    plan,
                    plan.AppliedChangeCount,
                    plan.DataContextApplied,
                    state.Host,
                    transitionGeneration);

                if (firstError == null && rollbackError != null)
                    firstError = rollbackError;

                if (layoutSuspended)
                {
                    try
                    {
                        root.ResumeLayout(false);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }
            }

            ItemsControl host = state.Host;

            if (updateHost &&
                anyPatch &&
                host != null &&
                !host.IsDisposed &&
                OwnsItemsTransition(host, transitionGeneration))
            {
                if (layoutChanged)
                {
                    try
                    {
                        host.PerformLayout();
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }

                    if (OwnsItemsTransition(
                        host,
                        transitionGeneration))
                    {
                        try
                        {
                            RestoreItemsScrollPosition(
                                host,
                                state.RollbackScrollX,
                                state.RollbackScrollY);
                        }
                        catch (Exception ex)
                        {
                            if (firstError == null)
                                firstError = ex;
                        }
                    }
                }

                if (OwnsItemsTransition(
                    host,
                    transitionGeneration))
                {
                    try
                    {
                        host.Invalidate(false);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }
            }

            state.PatchLayoutDirty = false;
            return firstError;
        }

        private void ApplyRenderBindingSlotValue(
            RenderBindingSlot slot,
            object value)
        {
            if (slot == null || slot.Target == null)
                return;

            BeginObservableTargetUpdate(
                slot,
                GetRenderBindingTargetPropertyName(slot));

            bool validateMutableImageSource =
                value is byte[] &&
                IsByteImageSourceSlot(slot);

            try
            {
                if (validateMutableImageSource)
                    BeginDecodedImageCacheContentValidation();

                ApplyRenderBindingSlotValueCore(
                    slot,
                    value);
            }
            finally
            {
                if (validateMutableImageSource)
                    EndDecodedImageCacheContentValidation();

                EndObservableTargetUpdate(slot);
            }
        }

        private void ApplyRenderBindingSlotValueCore(
            RenderBindingSlot slot,
            object value)
        {

            if (slot.Kind == RenderBindingSlotKind.Condition)
            {
                if (IsUnsetPresetValue(value))
                {
                    ElementInfo unsetInfo = GetInfo(slot.Target);
                    RemoveElementConditionState(unsetInfo, slot);
                    ApplyElementEffectiveVisibility(
                        slot.Target,
                        unsetInfo);
                    return;
                }

                object converted;

                if (!TryConvertObjectValue(
                    value,
                    typeof(bool),
                    out converted))
                {
                    throw new InvalidOperationException(
                        "Condition binding must return a boolean-compatible value.");
                }

                bool visible = (bool)converted;
                ElementInfo info = GetInfo(slot.Target);
                SetElementConditionState(info, slot, visible);
                ApplyElementEffectiveVisibility(slot.Target, info);

                return;
            }

            if (slot.Kind == RenderBindingSlotKind.InnerText)
            {
                if (IsUnsetPresetValue(value))
                {
                    if (slot.PresetBaselineRestore != null)
                    {
                        RestorePresetBoundProperty(
                            slot.Target,
                            "Text",
                            slot.PresetBaselineRestore);
                        slot.PresetBaselineRestore = null;
                    }
                    else if (!IsUnsetPresetValue(slot.LastValue))
                    {
                        ResetPresetBoundProperty(slot.Target, "Text");
                    }

                    return;
                }

                CaptureRenderPresetBaselineIfNeeded(slot, "Text");

                ApplyRuntimeInnerText(
                    slot.Target,
                    BindingValueToString(value));

                return;
            }

            if (IsUnsetPresetValue(value))
            {
                if (slot.AttributeName.IndexOf('.') < 0 &&
                    !IsResourceStyleProperty(
                        slot.Target,
                        slot.AttributeName))
                {
                    if (slot.PresetBaselineRestore != null)
                    {
                        RestorePresetBoundProperty(
                            slot.Target,
                            slot.AttributeName,
                            slot.PresetBaselineRestore);
                        slot.PresetBaselineRestore = null;
                    }
                    else if (!IsUnsetPresetValue(slot.LastValue))
                    {
                        ResetPresetBoundProperty(
                            slot.Target,
                            slot.AttributeName);
                    }
                }

                return;
            }

            CaptureRenderPresetBaselineIfNeeded(
                slot,
                slot.AttributeName);

            if (!ApplyBoundObjectAttribute(
                    slot.Target,
                    slot.AttributeName,
                    value,
                    slot.StyleSetter))
            {
                ApplyAttribute(
                    slot.Target,
                    slot.AttributeName,
                    BindingValueToString(value));
            }
        }

        private void CaptureRenderPresetBaselineIfNeeded(
            RenderBindingSlot slot,
            string propertyName)
        {
            if (slot == null ||
                !IsUnsetPresetValue(slot.LastValue) ||
                slot.PresetBaselineRestore != null)
            {
                return;
            }

            slot.PresetBaselineRestore =
                CapturePresetBoundPropertyBaseline(
                    slot.Target,
                    propertyName);
        }

        private void ApplyRuntimeInnerText(
            Control target,
            string text)
        {
            if (target == null)
                return;

            // Standard WinForms text controls expose Text. Using the native
            // property avoids rebuilding the XML subtree.
            target.Text = text == null ? String.Empty : text;
        }
    }
}
