using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private ItemPatchPlan CreateItemPatchPlan(
            ItemsControl host,
            RenderedItemRecord oldRecord,
            object newItem,
            bool functionsOnly)
        {
            return CreateItemPatchPlan(
                host,
                oldRecord,
                newItem,
                functionsOnly,
                false);
        }

        private ItemPatchPlan CreateDataContextPatchPlan(
            RenderedItemRecord oldRecord,
            RenderedItemRecord newRecord,
            object newItem)
        {
            ItemPatchPlan plan = new ItemPatchPlan();
            plan.Record = newRecord;
            plan.OldItem = oldRecord == null
                ? null
                : oldRecord.Item;
            plan.NewItem = newItem;
            plan.OldFunctionResults = oldRecord == null
                ? null
                : oldRecord.FunctionResults;
            plan.FunctionResults = newRecord == null
                ? null
                : newRecord.FunctionResults;
            plan.Changes = new ArrayList();
            plan.ReactiveChanges = new ArrayList();
            plan.RequiresRebuild = false;
            plan.AffectsLayout = false;
            plan.AffectsInheritance = false;
            plan.Applied = false;
            return plan;
        }

        private bool TryResolveRenderBindingSlotPathResult(
            RenderBindingSlot slot,
            object dataContext,
            out BindingExpressionPlan directPlan,
            out BindingPathResult pathResult)
        {
            object previousEventTarget = _activeComponentEventTarget;

            try
            {
                if (slot != null)
                    _activeComponentEventTarget = slot.EventTarget;

                return TryResolveRenderBindingSlotPathResultCore(
                    slot,
                    dataContext,
                    out directPlan,
                    out pathResult);
            }
            finally
            {
                _activeComponentEventTarget = previousEventTarget;
            }
        }

        private bool TryResolveRenderBindingSlotPathResultCore(
            RenderBindingSlot slot,
            object dataContext,
            out BindingExpressionPlan directPlan,
            out BindingPathResult pathResult)
        {
            dataContext = GetItemDataContext(dataContext);
            directPlan = slot == null
                ? null
                : slot.DirectPlan;
            pathResult = null;

            if (slot == null || String.IsNullOrEmpty(slot.Expression))
                return false;

            if (directPlan == null)
            {
                TryParseBindingExpression(
                    slot.Expression,
                    out directPlan);
            }

            if (directPlan != null)
            {
                object source = ResolveBindingSource(
                    dataContext,
                    directPlan);

                pathResult = ResolveBindingExpressionResult(
                    source,
                    directPlan);
                return true;
            }

            BindingExpressionPlan unexpectedDirectPlan;
            pathResult = ResolveObservableExpressionDependencies(
                slot.Expression,
                dataContext,
                out unexpectedDirectPlan);

            if (unexpectedDirectPlan != null)
            {
                directPlan = unexpectedDirectPlan;
                return true;
            }

            return pathResult != null ||
                   slot.Expression.IndexOf(
                       "{Binding",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private object EvaluateRenderBindingSlotExpression(
            RenderBindingSlot slot,
            object dataContext)
        {
            object previousEventTarget = _activeComponentEventTarget;

            try
            {
                if (slot != null)
                    _activeComponentEventTarget = slot.EventTarget;

                return EvaluateTemplateExpressionValue(
                    slot == null ? null : slot.Expression,
                    dataContext);
            }
            finally
            {
                _activeComponentEventTarget = previousEventTarget;
            }
        }

        private bool TryResolveLocatedRenderBindingSlotPathResult(
            RenderBindingSlot slot,
            ItemsControl host,
            object dataContext,
            out BindingExpressionPlan directPlan,
            out BindingPathResult pathResult)
        {
            try
            {
                return TryResolveRenderBindingSlotPathResult(
                    slot,
                    dataContext,
                    out directPlan,
                    out pathResult);
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateRenderBindingSlotLoadException(
                    host,
                    slot,
                    ex);
            }
        }

        private object EvaluateLocatedRenderBindingSlotExpression(
            RenderBindingSlot slot,
            ItemsControl host,
            object dataContext)
        {
            try
            {
                return EvaluateRenderBindingSlotExpression(
                    slot,
                    dataContext);
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateRenderBindingSlotLoadException(
                    host,
                    slot,
                    ex);
            }
        }

        private WinFormsXamlLoadException
            CreateRenderBindingSlotLoadException(
                ItemsControl host,
                RenderBindingSlot slot,
                Exception innerException)
        {
            ItemTemplateActiveContext previousContext =
                PushItemTemplateDeclarationContext(host);

            try
            {
                return CreateMarkupLoadException(
                    slot == null
                        ? null
                        : slot.SourceElement,
                    slot == null
                        ? null
                        : slot.AttributeName,
                    innerException);
            }
            finally
            {
                RestoreItemTemplateDeclarationContext(
                    previousContext);
            }
        }

        private static string GetRenderBindingTargetPropertyName(
            RenderBindingSlot slot)
        {
            if (slot == null)
                return null;

            return slot.Kind == RenderBindingSlotKind.InnerText
                ? "Text"
                : slot.AttributeName;
        }

        private static bool RenderBindingSlotNeedsSubscription(
            BindingExpressionPlan directPlan,
            BindingPathResult pathResult)
        {
            if (pathResult == null)
                return false;

            return pathResult.Dependencies.Count > 0 ||
                   (directPlan != null &&
                    directPlan.Mode == BindingMode.TwoWay);
        }

        private bool RenderBindingSlotSubscriptionMatches(
            RenderBindingSlot slot,
            BindingExpressionPlan directPlan,
            BindingPathResult pathResult)
        {
            bool needsSubscription =
                RenderBindingSlotNeedsSubscription(
                    directPlan,
                    pathResult);

            if (!needsSubscription)
            {
                return slot == null ||
                       slot.ObservableRegistration == null;
            }

            return slot != null &&
                   slot.ObservableRegistration != null &&
                   ObservableBindingMatches(
                       slot.ObservableRegistration,
                       pathResult);
        }

        private static void ValidateRenderBindingSlotPlan(
            RenderBindingSlot slot,
            BindingExpressionPlan directPlan,
            BindingPathResult pathResult)
        {
            if (directPlan == null ||
                directPlan.Mode != BindingMode.TwoWay)
            {
                return;
            }

            if (directPlan.HasComputedExpression)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay cannot be combined with a computed " +
                    "Binding expression.");
            }

            if (directPlan.HasNegation)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay cannot be combined with the ! binding operator.");
            }

            if (slot == null || slot.Kind == RenderBindingSlotKind.Condition)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by item-template Condition bindings.");
            }

            if (slot.Kind == RenderBindingSlotKind.RebuildOnChange)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by item-template bindings " +
                    "that rebuild components, styles, or attached properties.");
            }

            if (slot.StyleSetter)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by Style bindings. " +
                    "Bind the target's local property instead.");
            }

            if (!String.IsNullOrEmpty(slot.AttributeName) &&
                slot.AttributeName.IndexOf('.') >= 0)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by attached properties.");
            }

            if (EqualsIgnoreCase(slot.AttributeName, "ItemsSource"))
            {
                throw new InvalidOperationException(
                    "ItemsSource is one-way. Modify the observable list or " +
                    "replace the source PropertyBinding value instead.");
            }

            if (slot.Target == null)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay requires a realized item-template target control.");
            }

            if (pathResult == null ||
                pathResult.TerminalDependency == null)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay requires the Binding path to end in a " +
                    "writable PropertyBinding<T> or notifying CLR property.");
            }
        }

        private ItemReactiveBindingChange CreateItemReactiveBindingChange(
            RenderBindingSlot slot,
            ItemsControl host,
            object newDataContext,
            BindingPathResult newPathResult)
        {
            ItemReactiveBindingChange change =
                new ItemReactiveBindingChange();

            change.Slot = slot;
            change.OldDataContext = slot.DataContext;
            change.NewDataContext = newDataContext;
            change.OldHost = slot.Host;
            change.NewHost = host;
            change.OldPathResult = slot.PathResult;
            change.NewPathResult = newPathResult;
            change.OldSubscriptionActive =
                slot.ObservableRegistration != null;
            change.OldReactiveDirty = slot.ReactiveDirty;
            return change;
        }

        private static bool RenderedItemRecordRequiresReactiveValidation(
            RenderedItemRecord record,
            object dataContext)
        {
            if (record == null || record.BindingSlots == null)
                return false;

            int i;

            for (i = 0; i < record.BindingSlots.Count; i++)
            {
                RenderBindingSlot slot =
                    record.BindingSlots[i] as RenderBindingSlot;

                if (slot == null)
                    continue;

                if (slot.ReactiveDirty ||
                    (slot.ObservableDependencyKnown &&
                     (slot.ObservableRegistration == null ||
                      !Object.ReferenceEquals(
                          slot.DataContext,
                          dataContext))))
                {
                    return true;
                }

            }

            return false;
        }

        private static bool RenderedItemRecordHasReactiveDirtySlot(
            RenderedItemRecord record)
        {
            if (record == null || record.BindingSlots == null)
                return false;

            int i;

            for (i = 0; i < record.BindingSlots.Count; i++)
            {
                RenderBindingSlot slot =
                    record.BindingSlots[i] as RenderBindingSlot;

                if (slot != null && slot.ReactiveDirty)
                    return true;
            }

            return false;
        }

        private ItemPatchPlan CreateItemPatchPlan(
            ItemsControl host,
            RenderedItemRecord oldRecord,
            object newItem,
            bool functionsOnly,
            bool presetsOnly)
        {
            ItemPatchPlan plan = new ItemPatchPlan();
            plan.Record = oldRecord;
            plan.OldItem = oldRecord == null
                ? null
                : oldRecord.Item;
            plan.NewItem = newItem;
            plan.OldFunctionResults = oldRecord == null
                ? null
                : oldRecord.FunctionResults;
            plan.FunctionResults =
                (presetsOnly ||
                 (host != null &&
                  !host.ReevaluateFunctionsOnRefresh &&
                  functionsOnly)) &&
                oldRecord != null &&
                oldRecord.FunctionResults != null
                    ? CloneHashtable(oldRecord.FunctionResults)
                    : new Hashtable();
            plan.Changes = new ArrayList();
            plan.ReactiveChanges = new ArrayList();
            plan.RequiresRebuild = false;
            plan.AffectsLayout = false;
            plan.AffectsInheritance = false;
            plan.Applied = false;

            if (oldRecord == null ||
                oldRecord.Control == null ||
                oldRecord.BindingSlots == null)
            {
                plan.RequiresRebuild = true;
                return plan;
            }

            Hashtable previousCache = _activeFunctionResultCache;
            _activeFunctionResultCache = plan.FunctionResults;

            try
            {
                int i;

                for (i = 0; i < oldRecord.BindingSlots.Count; i++)
                {
                    RenderBindingSlot slot =
                        oldRecord.BindingSlots[i] as RenderBindingSlot;

                    if (slot == null ||
                        String.IsNullOrEmpty(slot.Expression))
                    {
                        continue;
                    }

                    bool containsFunction =
                        ExpressionContainsFunctionCall(
                            slot.Expression);

                    // The registered component owns live subscriptions for its
                    // invocation properties. With an unchanged item version,
                    // resolving the same parent path again adds no validation
                    // and may invoke user getters unnecessarily. A replacement
                    // data context still follows the normal dependency handoff.
                    if (functionsOnly &&
                        slot.ComponentOwned &&
                        !containsFunction &&
                        !slot.ReactiveDirty &&
                        Object.ReferenceEquals(
                            slot.DataContext,
                            newItem))
                    {
                        continue;
                    }

                    BindingExpressionPlan directPlan;
                    BindingPathResult newPathResult;
                    bool containsBinding =
                        TryResolveLocatedRenderBindingSlotPathResult(
                            slot,
                            host,
                            newItem,
                            out directPlan,
                            out newPathResult);
                    bool reactiveUpdateRequired = false;

                    if (containsBinding)
                    {
                        slot.DirectPlan = directPlan;
                        ValidateRenderBindingSlotPlan(
                            slot,
                            directPlan,
                            newPathResult);

                        reactiveUpdateRequired =
                            slot.ReactiveDirty ||
                            !Object.ReferenceEquals(
                                slot.DataContext,
                                newItem) ||
                            !RenderBindingSlotSubscriptionMatches(
                                slot,
                                directPlan,
                                newPathResult);

                        if (reactiveUpdateRequired)
                        {
                            plan.ReactiveChanges.Add(
                                CreateItemReactiveBindingChange(
                                    slot,
                                    host,
                                    newItem,
                                    newPathResult));
                        }
                    }

                    if (presetsOnly &&
                        !ContainsPresetExpression(slot.Expression) &&
                        !reactiveUpdateRequired)
                    {
                        continue;
                    }

                    // ItemVersionPath lets the application guarantee that ordinary data
                    // bindings are unchanged. We still re-run Function expressions when
                    // requested because they may depend on external state.
                    if (functionsOnly &&
                        !containsFunction &&
                        !reactiveUpdateRequired)
                    {
                        continue;
                    }

                    // Preserve the public switch from earlier versions. Direct Function
                    // bindings can be frozen independently from ordinary data bindings. A
                    // mixed interpolated expression is still evaluated because it may also
                    // contain normal bindings that must remain correct.
                    if (host != null &&
                        !host.ReevaluateFunctionsOnRefresh &&
                        functionsOnly &&
                        IsDirectFunctionExpression(slot.Expression) &&
                        !reactiveUpdateRequired)
                    {
                        continue;
                    }

                    object newValue =
                        EvaluateLocatedRenderBindingSlotExpression(
                            slot,
                            host,
                            newItem);

                    if (!slot.ForceNextApply &&
                        AreRenderBindingSlotValuesEquivalent(
                            slot,
                            newValue))
                    {
                        continue;
                    }

                    // A Condition or nonvisual resource/style definition can require
                    // rebuilding even though it has no Control target of its own.
                    // Other changes in an already-absent subtree remain irrelevant.
                    if (slot.Target == null)
                    {
                        if (slot.Kind == RenderBindingSlotKind.Condition ||
                            slot.Kind == RenderBindingSlotKind.RebuildOnChange)
                        {
                            plan.RequiresRebuild = true;
                            return plan;
                        }

                        continue;
                    }

                    if (slot.Kind == RenderBindingSlotKind.RebuildOnChange)
                    {
                        plan.RequiresRebuild = true;
                        return plan;
                    }

                    ItemPatchChange change =
                        new ItemPatchChange();

                    change.Slot = slot;
                    change.OldValue = slot.LastValue;
                    change.NewValue = newValue;
                    plan.Changes.Add(change);

                    if (DoesRenderBindingSlotAffectLayout(slot) ||
                        slot.Kind == RenderBindingSlotKind.Condition)
                    {
                        plan.AffectsLayout = true;
                    }

                    if (DoesRenderBindingSlotAffectInheritance(slot))
                        plan.AffectsInheritance = true;
                }
            }
            finally
            {
                _activeFunctionResultCache = previousCache;
            }

            return plan;
        }

        private void SetRenderBindingSlotSubscription(
            RenderBindingSlot slot,
            ItemsControl host,
            object dataContext,
            BindingPathResult pathResult,
            bool active,
            bool reactiveDirty)
        {
            if (slot == null)
                return;

            BindingExpressionPlan directPlan = slot.DirectPlan;

            if (active)
            {
                ValidateRenderBindingSlotPlan(
                    slot,
                    directPlan,
                    pathResult);
            }

            bool needsSubscription =
                active &&
                !slot.ComponentOwned &&
                RenderBindingSlotNeedsSubscription(
                    directPlan,
                    pathResult);

            slot.Host = host;
            slot.DataContext = dataContext;
            slot.PathResult = pathResult;
            slot.ReactiveDirty = reactiveDirty;

            if (active)
                slot.ObservableDependencyKnown = needsSubscription;

            if (!needsSubscription)
            {
                if (slot.ObservableRegistration != null)
                {
                    DetachObservableBinding(
                        slot.ObservableRegistration);
                    slot.ObservableRegistration = null;
                }

                return;
            }

            BindingMode mode = directPlan == null
                ? BindingMode.OneWay
                : directPlan.Mode;
            Control target = slot.Target;

            if (slot.ObservableRegistration == null)
            {
                ObservableBindingRegistration registration =
                    AttachObservableBinding(
                        slot,
                        target,
                        GetRenderBindingTargetPropertyName(slot),
                        mode,
                        directPlan == null
                            ? BindingUpdateSourceTrigger.PropertyChanged
                            : directPlan.UpdateSourceTrigger,
                        pathResult,
                        OnRenderBindingSlotObservableChanged);

                // Adding an INotifyPropertyChanged handler is application code.
                // It can synchronously replace the ItemsControl source, which
                // retires this staged slot before attachment returns. Never
                // publish that obsolete registration back into the cleared slot.
                if (IsRenderBindingSlotSubscriptionContextCurrent(
                        slot,
                        host,
                        dataContext,
                        pathResult,
                        target) &&
                    slot.ObservableRegistration == null)
                {
                    slot.ObservableRegistration = registration;
                }
                else if (registration != null)
                {
                    DetachObservableBinding(registration);
                }

                return;
            }

            ObservableBindingRegistration currentRegistration =
                slot.ObservableRegistration;

            UpdateObservableBinding(
                currentRegistration,
                pathResult);

            if (!IsRenderBindingSlotSubscriptionContextCurrent(
                    slot,
                    host,
                    dataContext,
                    pathResult,
                    target) ||
                !Object.ReferenceEquals(
                    slot.ObservableRegistration,
                    currentRegistration))
            {
                return;
            }

            if (!ObservableBindingMatches(
                    currentRegistration,
                    pathResult))
            {
                DetachObservableBinding(
                    currentRegistration);
                slot.ObservableRegistration = null;

                ObservableBindingRegistration replacement =
                    AttachObservableBinding(
                        slot,
                        target,
                        GetRenderBindingTargetPropertyName(slot),
                        mode,
                        directPlan == null
                            ? BindingUpdateSourceTrigger.PropertyChanged
                            : directPlan.UpdateSourceTrigger,
                        pathResult,
                        OnRenderBindingSlotObservableChanged);

                if (IsRenderBindingSlotSubscriptionContextCurrent(
                        slot,
                        host,
                        dataContext,
                        pathResult,
                        target) &&
                    slot.ObservableRegistration == null)
                {
                    slot.ObservableRegistration = replacement;
                }
                else if (replacement != null)
                {
                    DetachObservableBinding(replacement);
                }
            }
        }

        private static bool
            IsRenderBindingSlotSubscriptionContextCurrent(
                RenderBindingSlot slot,
                ItemsControl host,
                object dataContext,
                BindingPathResult pathResult,
                Control target)
        {
            return slot != null &&
                   Object.ReferenceEquals(slot.Host, host) &&
                   Object.ReferenceEquals(
                       slot.DataContext,
                       dataContext) &&
                   Object.ReferenceEquals(
                       slot.PathResult,
                       pathResult) &&
                   Object.ReferenceEquals(slot.Target, target);
        }

        private void ApplyItemReactiveBindingChange(
            ItemReactiveBindingChange change,
            bool useNewState)
        {
            if (change == null || change.Slot == null)
                return;

            if (useNewState)
            {
                SetRenderBindingSlotSubscription(
                    change.Slot,
                    change.NewHost,
                    change.NewDataContext,
                    change.NewPathResult,
                    true,
                    false);
                return;
            }

            SetRenderBindingSlotSubscription(
                change.Slot,
                change.OldHost,
                change.OldDataContext,
                change.OldPathResult,
                change.OldSubscriptionActive,
                change.OldReactiveDirty);
        }

        private void ActivateRenderedItemRecordBindings(
            RenderedItemRecord record,
            ItemsControl host,
            object dataContext)
        {
            if (record == null || record.BindingSlots == null)
                return;

            ArrayList bindingSlots = record.BindingSlots;
            int activationGeneration = host == null
                ? -1
                : host.RefreshGeneration;
            Hashtable previousFunctionResults =
                _activeFunctionResultCache;

            try
            {
                _activeFunctionResultCache =
                    record.FunctionResults;
                int i;

                for (i = 0; i < bindingSlots.Count; i++)
                {
                    RenderBindingSlot slot =
                        bindingSlots[i] as RenderBindingSlot;

                    if (slot == null)
                        continue;

                    BindingExpressionPlan directPlan;
                    BindingPathResult pathResult;
                    bool containsBinding =
                        TryResolveLocatedRenderBindingSlotPathResult(
                            slot,
                            host,
                            dataContext,
                        out directPlan,
                        out pathResult);

                    if (!IsRenderedItemBindingActivationCurrent(
                            record,
                            host,
                            bindingSlots,
                            activationGeneration))
                    {
                        return;
                    }

                    slot.DirectPlan = directPlan;

                    if (containsBinding)
                    {
                        SetRenderBindingSlotSubscription(
                            slot,
                            host,
                            dataContext,
                            pathResult,
                            true,
                            false);

                        if (!IsRenderedItemBindingActivationCurrent(
                                record,
                                host,
                                bindingSlots,
                                activationGeneration))
                        {
                            return;
                        }

                        object currentValue =
                            EvaluateLocatedRenderBindingSlotExpression(
                                slot,
                                host,
                                dataContext);

                        if (!IsRenderedItemBindingActivationCurrent(
                                record,
                                host,
                                bindingSlots,
                                activationGeneration))
                        {
                            return;
                        }

                        if (!AreRenderBindingSlotValuesEquivalent(
                                slot,
                                currentValue) ||
                            (slot.Kind == RenderBindingSlotKind.Condition &&
                             slot.Target != null &&
                             !Object.ReferenceEquals(
                                 slot.Target,
                                 record.Control)))
                        {
                            bool requiresRebuild =
                                slot.Kind ==
                                    RenderBindingSlotKind.RebuildOnChange ||
                                (slot.Target == null &&
                                 slot.Kind ==
                                    RenderBindingSlotKind.Condition);

                            if (requiresRebuild)
                            {
                                slot.ReactiveDirty = true;
                                QueueReactiveItemReload(host);
                            }
                            else if (!(slot.Kind ==
                                          RenderBindingSlotKind.Condition &&
                                      Object.ReferenceEquals(
                                          slot.Target,
                                          record.Control)))
                            {
                                ApplyRenderBindingSlotValue(
                                    slot,
                                    currentValue);
                            }

                            if (!requiresRebuild)
                            {
                                CommitRenderBindingSlotValue(
                                    slot,
                                    currentValue);
                            }

                            if (!IsRenderedItemBindingActivationCurrent(
                                    record,
                                    host,
                                    bindingSlots,
                                    activationGeneration))
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        slot.Host = host;
                        slot.DataContext = dataContext;
                        slot.PathResult = null;
                        slot.ReactiveDirty = false;
                    }
                }
            }
            catch
            {
                DeactivateRenderBindingSlots(bindingSlots);
                throw;
            }
            finally
            {
                _activeFunctionResultCache =
                    previousFunctionResults;
            }
        }

        private static bool IsRenderedItemBindingActivationCurrent(
            RenderedItemRecord record,
            ItemsControl host,
            ArrayList bindingSlots,
            int activationGeneration)
        {
            return record != null &&
                   host != null &&
                   !host.IsDisposed &&
                   record.Owner == host &&
                   Object.ReferenceEquals(
                       record.BindingSlots,
                       bindingSlots) &&
                   host.RefreshGeneration == activationGeneration;
        }

        private void DeactivateRenderBindingSlots(
            ArrayList slots)
        {
            if (slots == null)
                return;

            Exception firstError = null;
            int i;

            for (i = slots.Count - 1; i >= 0; i--)
            {
                RenderBindingSlot slot =
                    slots[i] as RenderBindingSlot;

                if (slot == null)
                    continue;

                try
                {
                    DeactivateRenderBindingSlot(slot);
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(
                        firstError,
                        ex);
                }
            }

            // A failed temporary deactivation keeps that slot's remaining
            // metadata intact so the owner can retry it. Independent slots
            // have still been deactivated and cannot retain avoidable work.
            if (firstError != null)
                throw firstError;
        }

        private void DeactivateRenderBindingSlot(
            RenderBindingSlot slot)
        {
            if (slot == null)
                return;

            if (slot.ObservableRegistration != null)
            {
                DetachObservableBinding(
                    slot.ObservableRegistration);
                slot.ObservableRegistration = null;
            }

            if (slot.Kind == RenderBindingSlotKind.Condition &&
                slot.Target != null)
            {
                ElementInfo info;

                if (_elementInfos.TryGetValue(slot.Target, out info))
                {
                    RemoveElementConditionState(info, slot);

                    if (!slot.Target.IsDisposed)
                    {
                        ApplyElementEffectiveVisibility(
                            slot.Target,
                            info);
                    }
                }
            }

            slot.Host = null;
            slot.DataContext = null;
            slot.PathResult = null;
            slot.ReactiveDirty = false;
        }

        private Exception ReleaseRenderBindingSlots(
            ArrayList slots)
        {
            Exception firstError = null;
            int i;

            for (i = slots == null ? -1 : slots.Count - 1;
                 i >= 0;
                 i--)
            {
                RenderBindingSlot slot =
                    slots[i] as RenderBindingSlot;

                if (slot == null)
                    continue;

                try
                {
                    DeactivateRenderBindingSlot(slot);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
                finally
                {
                    // Permanent record retirement must not let an externally
                    // retained slot keep a component event target, item, host,
                    // or native Control alive. Observable detach debt remains
                    // owned by the runtime registration indexes.
                    slot.ObservableRegistration = null;
                    slot.Host = null;
                    slot.DataContext = null;
                    slot.EventTarget = null;
                    slot.SourceElement = null;
                    slot.PathResult = null;
                    slot.Target = null;
                    slot.LastValue = null;
                    slot.LastByteImageFingerprintKnown = false;
                    slot.LastByteImageFingerprint = 0;
                    slot.ReactiveDirty = false;
                    slot.ForceNextApply = false;
                }
            }

            return firstError;
        }

        private void DeactivateItemsControlBindingSlots(
            ItemsControl host)
        {
            if (host == null)
                return;

            lock (_reactiveItemUpdateSync)
            {
                ReactiveItemUpdateBatch pending =
                    _pendingReactiveItemUpdates[host] as
                        ReactiveItemUpdateBatch;

                _pendingReactiveItemUpdates.Remove(host);

                if (pending != null)
                {
                    pending.Slots.Clear();
                    pending.SlotSet.Clear();
                }
            }

            Exception firstError = null;

            try
            {
                DeactivateRenderedItemRecordListBindings(
                    host.RenderedItems);
            }
            catch (Exception ex)
            {
                firstError = ex;
            }

            try
            {
                DeactivateRenderedItemRecordListBindings(
                    host.DirectVirtualCacheRecords);
            }
            catch (Exception ex)
            {
                firstError = FirstItemsCommitError(
                    firstError,
                    ex);
            }

            if (firstError != null)
                throw firstError;
        }

        private void DeactivateRenderedItemRecordListBindings(
            ArrayList records)
        {
            Exception firstError = null;
            int i;

            for (i = 0; records != null && i < records.Count; i++)
            {
                RenderedItemRecord record =
                    records[i] as RenderedItemRecord;

                if (record != null)
                {
                    try
                    {
                        DeactivateRenderBindingSlots(record.BindingSlots);
                    }
                    catch (Exception ex)
                    {
                        firstError = FirstItemsCommitError(
                            firstError,
                            ex);
                    }
                }
            }

            if (firstError != null)
                throw firstError;
        }

    }
}
