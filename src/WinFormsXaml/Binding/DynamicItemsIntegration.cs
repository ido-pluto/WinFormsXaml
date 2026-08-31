using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private void RegisterItemsControl(ItemsControl items)
        {
            if (!_dynamicFeaturesDisposed &&
                items != null &&
                _itemsControlSet != null &&
                !_itemsControlSet.ContainsKey(items))
            {
                _itemsControls.Add(items);

                try
                {
                    _itemsControlSet.Add(items, true);
                    RefreshItemsControlPresetIndex(items, false);
                }
                catch
                {
                    _itemsControlSet.Remove(items);
                    _itemsControls.RemoveAt(
                        _itemsControls.Count - 1);
                    throw;
                }
            }
        }

        private void UnregisterItemsControl(ItemsControl items)
        {
            if (items == null)
                return;

            UnindexItemsControlPresetUse(items);

            if (_itemsControlSet != null)
                _itemsControlSet.Remove(items);

            if (_itemsControls != null)
                _itemsControls.Remove(items);
        }

        private void OnItemsControlTemplateChanged(ItemsControl items)
        {
            if (items == null || _dynamicFeaturesDisposed)
                return;

            if (items.VirtualizationMode !=
                    ItemsControlVirtualizationMode.Lightweight ||
                items.IsXamlInitializationComplete)
            {
                ValidateLightweightItemsControlConfiguration(items);
            }

            UnindexItemsControlPresetUse(items);

            RefreshItemsControlPresetIndex(items, false);
        }

        private void RefreshItemsControlPresetIndex(
            ItemsControl items,
            bool retainExisting)
        {
            if (items == null ||
                items.IsDisposed ||
                _itemsControls == null ||
                _itemsControlSet == null ||
                !_itemsControlSet.ContainsKey(items) ||
                _presetItemsControls == null ||
                _presetItemsControlSet == null)
            {
                return;
            }

            if (retainExisting &&
                _presetItemsControlSet.ContainsKey(items))
                return;

            bool mayUsePreset =
                ContainsPresetExpression(items.TemplateOuterXml) ||
                (retainExisting &&
                 (RenderedItemsMayUsePresets(items.RenderedItems) ||
                  RenderedItemsMayUsePresets(
                      items.DirectVirtualCacheRecords)));

            if (!mayUsePreset)
            {
                UnindexItemsControlPresetUse(items);
                return;
            }

            if (_presetItemsControlSet.ContainsKey(items))
                return;

            int itemsIndex;
            bool appendedToPrimary =
                _itemsControls.Count > 0 &&
                Object.ReferenceEquals(
                    _itemsControls[_itemsControls.Count - 1],
                    items);

            if (appendedToPrimary)
            {
                itemsIndex = _itemsControls.Count - 1;
            }
            else
            {
                itemsIndex = _itemsControls.IndexOf(items);
            }

            if (itemsIndex < 0)
                return;

            int insertIndex = appendedToPrimary
                ? _presetItemsControls.Count
                : 0;
            int i;

            for (i = 0;
                 !appendedToPrimary && i < itemsIndex;
                 i++)
            {
                if (_presetItemsControlSet.ContainsKey(_itemsControls[i]))
                    insertIndex++;
            }

            try
            {
                if (insertIndex >= _presetItemsControls.Count)
                    _presetItemsControls.Add(items);
                else
                    _presetItemsControls.Insert(insertIndex, items);

                _presetItemsControlSet.Add(items, true);
            }
            catch
            {
                _presetItemsControls.Remove(items);
                _presetItemsControlSet.Remove(items);
                throw;
            }
        }

        private void UnindexItemsControlPresetUse(ItemsControl items)
        {
            if (items == null ||
                _presetItemsControlSet == null ||
                !_presetItemsControlSet.ContainsKey(items))
            {
                return;
            }

            _presetItemsControlSet.Remove(items);

            if (_presetItemsControls != null)
                _presetItemsControls.Remove(items);
        }

        private static bool RenderedItemsMayUsePresets(
            ArrayList records)
        {
            int i;

            for (i = 0; records != null && i < records.Count; i++)
            {
                RenderedItemRecord record =
                    records[i] as RenderedItemRecord;

                if (record == null || record.BindingSlots == null)
                    continue;

                int slotIndex;

                for (slotIndex = 0;
                     slotIndex < record.BindingSlots.Count;
                     slotIndex++)
                {
                    RenderBindingSlot slot =
                        record.BindingSlots[slotIndex] as RenderBindingSlot;

                    if (slot != null &&
                        ContainsPresetExpression(slot.Expression))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ReloadRegisteredItemsControls(object target)
        {
            if (_dynamicFeaturesDisposed || _itemsControls == null)
                return;

            int i;

            for (i = _itemsControls.Count - 1; i >= 0; i--)
            {
                ItemsControl items =
                    _itemsControls[i] as ItemsControl;

                if (items == null)
                {
                    _itemsControls.RemoveAt(i);
                    continue;
                }

                if (items.IsDisposed)
                    UnregisterItemsControl(items);
            }

            // Snapshot before refreshing. An outer ItemsControl may rebuild and
            // replace nested ItemsControls while its refresh is being started.
            ArrayList matches = null;

            for (i = 0; i < _itemsControls.Count; i++)
            {
                ItemsControl items =
                    _itemsControls[i] as ItemsControl;

                if (items != null &&
                    (target == null ||
                     IsTargetOrElementDescendant(items, target)))
                {
                    if (matches == null)
                        matches = new ArrayList();

                    matches.Add(items);
                }
            }

            if (matches == null)
                return;

            for (i = 0; i < matches.Count; i++)
            {
                if (_dynamicFeaturesDisposed)
                    break;

                ItemsControl items =
                    matches[i] as ItemsControl;

                if (items != null && !items.IsDisposed)
                    items.ReloadItems();
            }
        }

        private void ReloadItemPresetBindings(
            ItemsControl items,
            PresetChangedEventArgs change)
        {
            if (items == null)
                return;

            if (items.LightweightActive)
            {
                // Lightweight snapshots deliberately cache evaluated paint
                // values for only the visible rows. A preset generation change
                // invalidates that bounded cache through the normal reload API.
                items.ReloadItems();
                return;
            }

            if (items.IsRefreshing)
            {
                items.ForceReloadItems();
                return;
            }

            int recordCount =
                (items.RenderedItems == null
                    ? 0
                    : items.RenderedItems.Count) +
                (items.DirectVirtualCacheRecords == null
                    ? 0
                    : items.DirectVirtualCacheRecords.Count);
            ArrayList records = new ArrayList(recordCount);

            if (items.RenderedItems != null)
                records.AddRange(items.RenderedItems);

            if (items.DirectVirtualCacheRecords != null)
                records.AddRange(items.DirectVirtualCacheRecords);

            bool layoutChanged = false;
            bool requiresRebuild = false;
            int transitionGeneration = items.RefreshGeneration;
            ArrayList plans = new ArrayList(records.Count);
            int i;

            try
            {
                for (i = 0; i < records.Count; i++)
                {
                    RenderedItemRecord record =
                        records[i] as RenderedItemRecord;

                    if (record == null || record.Control == null)
                        continue;

                    ItemPatchPlan plan =
                        CreateItemPatchPlan(
                            items,
                            record,
                            record.Item,
                            false,
                            true);

                    if (!OwnsItemsTransition(
                            items,
                            transitionGeneration))
                    {
                        return;
                    }

                    plans.Add(plan);

                    if (plan.RequiresRebuild)
                    {
                        requiresRebuild = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!OwnsItemsTransition(
                        items,
                        transitionGeneration))
                {
                    return;
                }

                throw ReportReactiveItemUpdateFailure(
                    items,
                    ex);
            }

            if (requiresRebuild)
            {
                items.ForceReloadItems();
                return;
            }

            int appliedPlanCount = 0;

            try
            {
                for (i = 0; i < plans.Count; i++)
                {
                    ItemPatchPlan plan =
                        plans[i] as ItemPatchPlan;

                    if (plan == null || plan.Changes.Count == 0)
                        continue;

                    appliedPlanCount = i + 1;

                    if (!ApplyItemPatchPlan(
                            null,
                            plan,
                            items,
                            transitionGeneration))
                    {
                        return;
                    }

                    layoutChanged = layoutChanged || plan.AffectsLayout;

                    Control patchTarget = plan.Record == null
                        ? null
                        : plan.Record.Control;

                    if (patchTarget != null && !patchTarget.IsDisposed)
                        patchTarget.Invalidate(true);

                    if (plan.AffectsLayout && plan.Record != null)
                        plan.Record.MeasureCacheValid = false;
                }

                if (layoutChanged)
                {
                    items.PerformLayout();
                    items.Invalidate(false);
                }

                if (plans.Count > 0 &&
                    OwnsItemsTransition(
                        items,
                        transitionGeneration))
                {
                    items.SetRefreshing(false, null);
                }
            }
            catch (Exception ex)
            {
                if (!OwnsItemsTransition(
                        items,
                        transitionGeneration))
                {
                    return;
                }

                Exception rollbackError =
                    RollbackReactiveItemPatchPlans(
                        items,
                        transitionGeneration,
                        plans,
                        appliedPlanCount,
                        layoutChanged);

                if (!OwnsItemsTransition(
                        items,
                        transitionGeneration))
                {
                    return;
                }

                ex = IncludeItemsRollbackError(
                    ex,
                    rollbackError);

                throw ReportReactiveItemUpdateFailure(
                    items,
                    ex);
            }
        }

        private bool ItemTemplateDependsOnPreset(
            ItemsControl items,
            PresetChangedEventArgs change)
        {
            if (items == null || items.TemplateRoot == null)
                return false;

            if (ExpressionDependsOnPreset(
                items.TemplateOuterXml,
                change))
            {
                return true;
            }

            if (RenderedItemsDependOnPreset(
                items.RenderedItems,
                change))
            {
                return true;
            }

            return RenderedItemsDependOnPreset(
                items.DirectVirtualCacheRecords,
                change);
        }

        private bool RenderedItemsDependOnPreset(
            ArrayList records,
            PresetChangedEventArgs change)
        {
            int i;

            for (i = 0; records != null && i < records.Count; i++)
            {
                RenderedItemRecord record =
                    records[i] as RenderedItemRecord;

                if (record == null || record.BindingSlots == null)
                    continue;

                int slotIndex;

                for (slotIndex = 0;
                     slotIndex < record.BindingSlots.Count;
                     slotIndex++)
                {
                    RenderBindingSlot slot =
                        record.BindingSlots[slotIndex] as RenderBindingSlot;

                    if (slot != null &&
                        ExpressionDependsOnPreset(slot.Expression, change))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ExpressionDependsOnPreset(
            string expression,
            PresetChangedEventArgs change)
        {
            if (!ContainsPresetExpression(expression))
                return false;

            if (change == null || String.IsNullOrEmpty(change.SetName))
                return true;

            Hashtable memo = _activePresetDependencyMemo;

            if (memo == null)
            {
                memo = new Hashtable(
                    StringComparer.OrdinalIgnoreCase);
            }

            Hashtable evaluated = null;
            bool encounteredCycle;
            bool depends = ExpressionDependsOnPreset(
                expression,
                change,
                null,
                memo,
                ref evaluated,
                out encounteredCycle);

            if (!depends && evaluated != null)
            {
                foreach (DictionaryEntry entry in evaluated)
                    memo[entry.Key] = false;
            }

            return depends;
        }

        private bool ExpressionDependsOnPreset(
            string expression,
            PresetChangedEventArgs change,
            Hashtable visited,
            Hashtable memo,
            ref Hashtable evaluated,
            out bool encounteredCycle)
        {
            encounteredCycle = false;

            if (!ContainsPresetExpression(expression))
                return false;

            if (change == null || String.IsNullOrEmpty(change.SetName))
                return true;

            int searchFrom = 0;

            while (searchFrom < expression.Length)
            {
                int start = expression.IndexOf(
                    "{Preset ",
                    searchFrom,
                    StringComparison.OrdinalIgnoreCase);

                if (start < 0)
                    return false;

                int end = expression.IndexOf('}', start + 1);

                if (end < 0)
                    return false;

                string setName;
                string key;
                string segment = expression.Substring(
                    start,
                    end - start + 1);
                PresetConditionExpressionPlan conditionPlan;

                if (TryParsePresetConditionExpression(
                        segment,
                        out conditionPlan))
                {
                    if (PresetConditionDependsOnChange(
                            conditionPlan,
                            change))
                    {
                        return true;
                    }

                    searchFrom = end + 1;
                    continue;
                }

                if (TryParsePresetExpression(
                        expression,
                        start,
                        end,
                        out setName,
                        out key))
                {
                    if (EqualsIgnoreCase(setName, change.SetName) &&
                        (String.IsNullOrEmpty(change.Key) ||
                         EqualsIgnoreCase(key, change.Key)))
                    {
                        return true;
                    }

                    string identity =
                        GetPresetValueIdentity(setName, key);

                    if (memo.ContainsKey(identity))
                    {
                        if ((bool)memo[identity])
                            return true;
                    }
                    else if (visited != null &&
                        visited.ContainsKey(identity))
                    {
                        encounteredCycle = true;
                    }
                    else
                    {
                        bool nestedCycle = false;
                        bool depends = false;

                        if (evaluated == null)
                        {
                            evaluated = new Hashtable(
                                StringComparer.OrdinalIgnoreCase);
                        }

                        evaluated[identity] = null;

                        if (visited == null)
                        {
                            visited = new Hashtable(
                                StringComparer.OrdinalIgnoreCase);
                        }

                        visited.Add(identity, null);

                        try
                        {
                            object storedValue;

                            if (!_presetManager.TryResolve(
                                    setName,
                                    key,
                                    out storedValue))
                            {
                                memo[identity] = false;
                                searchFrom = end + 1;
                                continue;
                            }

                            string storedExpression =
                                storedValue as string;

                            if (storedExpression != null)
                            {
                                depends = ExpressionDependsOnPreset(
                                    storedExpression,
                                    change,
                                    visited,
                                    memo,
                                    ref evaluated,
                                    out nestedCycle);
                            }
                        }
                        finally
                        {
                            visited.Remove(identity);
                        }

                        if (depends)
                        {
                            memo[identity] = true;
                            return true;
                        }

                        if (nestedCycle)
                        {
                            encounteredCycle = true;
                        }
                        else
                        {
                            memo[identity] = false;
                        }
                    }
                }

                searchFrom = end + 1;
            }

            return false;
        }

    }
}
