using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private void ReloadDynamicBindings(
            object target,
            string propertyName,
            bool presetsOnly,
            PresetChangedEventArgs presetChange)
        {
            if (_dynamicFeaturesDisposed ||
                _dynamicPropertyBindings == null)
            {
                return;
            }

            if (_reloadingDynamicBindings)
            {
                QueueDynamicBindingReload(
                    target,
                    propertyName,
                    presetsOnly,
                    presetChange);
                return;
            }

            // A previous pass that threw cannot safely replay requests captured
            // from its half-applied callback stack.
            if (!_drainingDynamicBindingReloads &&
                _pendingDynamicBindingReloads != null &&
                _pendingDynamicBindingReloads.Count > 0)
            {
                _pendingDynamicBindingReloads.Clear();
            }

            Control rootControl = RootControl;
            bool applied = false;
            bool affectsLayout = false;
            bool affectsInheritance = false;
            bool layoutSuspended = false;
            bool requiresFullRootLayout = false;
            Hashtable invalidationTargets = null;
            Hashtable scopedLayoutTargets = null;

            if (rootControl != null && rootControl.IsDisposed)
            {
                DisposeDynamicFeatures();
                return;
            }

            // A byte[] can be shared by many Image/PictureBox bindings. Give
            // this reload pass one generation so its content is fingerprinted
            // at most once, rather than once per control using that source.
            unchecked
            {
                _decodedImageCacheValidationGeneration++;
            }

            if (_decodedImageCacheValidationGeneration == 0)
                _decodedImageCacheValidationGeneration = 1;

            _reloadingDynamicBindings = true;
            _conditionalStyleRefreshTargets =
                new Hashtable(_runtimeObjectReferenceComparer);

            try
            {
                ArrayList refreshedComponentStates;
                ArrayList changedComponentRoots =
                    ReloadComponentPropertyValues(
                        target,
                        propertyName,
                        presetsOnly,
                        presetChange,
                        out refreshedComponentStates);
                bool scanAllBindings = true;
                ArrayList bindingsToScan = _dynamicPropertyBindings;
                DynamicPropertyBinding singleBinding = null;
                bool scanSingleBinding = false;
                string requestedPropertyKey = null;

                if (presetsOnly)
                {
                    scanAllBindings = false;
                    bindingsToScan = _presetDynamicPropertyBindings == null
                        ? null
                        : new ArrayList(
                            _presetDynamicPropertyBindings);
                }

                if (!presetsOnly &&
                    target != null &&
                    !String.IsNullOrEmpty(propertyName) &&
                    !EqualsIgnoreCase(propertyName, "Condition") &&
                    (changedComponentRoots == null ||
                     changedComponentRoots.Count == 0))
                {
                    scanAllBindings = false;
                    scanSingleBinding = true;
                    requestedPropertyKey =
                        GetStylePropertyKey(target, propertyName);
                    singleBinding =
                        FindIndexedDynamicBinding(
                            target,
                            requestedPropertyKey,
                            false);
                }

                int i;
                int bindingCount = scanSingleBinding
                    ? (singleBinding == null ? 0 : 1)
                    : (bindingsToScan == null
                        ? 0
                        : bindingsToScan.Count);

                for (i = bindingCount - 1; i >= 0; i--)
                {
                    if (_dynamicFeaturesDisposed)
                        break;

                    if (!scanSingleBinding &&
                        (bindingsToScan == null ||
                         i >= bindingsToScan.Count))
                        continue;

                    DynamicPropertyBinding binding =
                        scanSingleBinding
                            ? singleBinding
                            : bindingsToScan[i] as DynamicPropertyBinding;

                    if (binding == null ||
                        !binding.Active ||
                        IsDisposedTarget(binding.Target))
                    {
                        DeactivateDynamicBinding(binding);

                        if (scanAllBindings)
                            _dynamicPropertyBindings.RemoveAt(i);
                        else
                            _dynamicPropertyBindings.Remove(binding);

                        continue;
                    }

                    if (presetsOnly)
                    {
                        if (!binding.UsesPreset ||
                            !ExpressionDependsOnPreset(
                                binding.Expression,
                                presetChange))
                        {
                            if (!IsInsideChangedComponent(
                                binding.Target,
                                changedComponentRoots))
                            {
                                continue;
                            }
                        }
                    }

                    // An exact indexed lookup already proves target and
                    // canonical property identity. Avoid walking the Control
                    // ancestry or resolving aliases again on that common path.
                    if (!scanSingleBinding &&
                        target != null &&
                        !IsTargetOrElementDescendant(binding.Target, target))
                    {
                        continue;
                    }

                    if (!scanSingleBinding &&
                        !String.IsNullOrEmpty(propertyName) &&
                        (!Object.ReferenceEquals(binding.Target, target) ||
                         !EqualsIgnoreCase(
                             binding.PropertyKey,
                             GetStylePropertyKey(target, propertyName))) &&
                        !IsInsideChangedComponent(
                            binding.Target,
                            changedComponentRoots))
                    {
                        continue;
                    }

                    if (!layoutSuspended && rootControl != null)
                    {
                        rootControl.SuspendLayout();
                        layoutSuspended = true;
                    }

                    bool bindingAffectsLayout =
                        DynamicBindingAffectsLayout(binding);
                    bool bindingAffectsInheritance =
                        DynamicBindingAffectsInheritance(binding);

                    bool bindingApplied = ApplyDynamicBinding(binding);

                    if (bindingApplied)
                    {
                        applied = true;
                        affectsLayout =
                            affectsLayout || bindingAffectsLayout;
                        affectsInheritance =
                            affectsInheritance || bindingAffectsInheritance;

                        Control affectedControl =
                            binding.Target as Control;

                        if (bindingAffectsLayout)
                        {
                            if (CanScopeDynamicBindingLayout(
                                    binding,
                                    bindingAffectsInheritance,
                                    affectedControl))
                            {
                                AddScopedDynamicLayoutTargets(
                                    affectedControl,
                                    rootControl,
                                    ref scopedLayoutTargets);
                            }
                            else
                            {
                                requiresFullRootLayout = true;
                            }
                        }

                        if (affectedControl != null)
                        {
                            if (invalidationTargets == null)
                            {
                                invalidationTargets =
                                    new Hashtable(
                                        _runtimeObjectReferenceComparer);
                            }

                            invalidationTargets[affectedControl] = true;
                        }
                    }
                }

                ApplyPendingConditionalStyleRefreshes();

                CompleteComponentBindingRefresh(
                    refreshedComponentStates);
            }
            finally
            {
                try
                {
                    if (rootControl != null &&
                        !rootControl.IsDisposed &&
                        layoutSuspended)
                    {
                        rootControl.ResumeLayout(false);

                        if (applied && affectsInheritance)
                            ApplyInheritedProperties(rootControl, null);

                        if (applied && affectsLayout)
                        {
                            if (requiresFullRootLayout ||
                                scopedLayoutTargets == null)
                            {
                                PerformLayoutRecursive(rootControl);
                            }
                            else
                            {
                                PerformScopedDynamicBindingLayouts(
                                    scopedLayoutTargets);
                            }
                        }

                        if (applied && affectsInheritance)
                        {
                            rootControl.Invalidate(true);
                        }
                        else if (applied)
                        {
                            if (affectsLayout)
                                rootControl.Invalidate(false);

                            if (invalidationTargets != null)
                            {
                                IDictionaryEnumerator targets =
                                    invalidationTargets.GetEnumerator();

                                while (targets.MoveNext())
                                {
                                    Control affected =
                                        targets.Key as Control;

                                    if (affected == null ||
                                        affected.IsDisposed ||
                                        (affectsLayout &&
                                         Object.ReferenceEquals(
                                             affected,
                                             rootControl)))
                                    {
                                        continue;
                                    }

                                    affected.Invalidate();
                                }
                            }
                        }
                    }
                }
                finally
                {
                    try
                    {
                        RemoveInactiveDynamicBindings();
                    }
                    finally
                    {
                        _conditionalStyleRefreshTargets = null;
                        _reloadingDynamicBindings = false;
                    }
                }
            }

            DrainPendingDynamicBindingReloads();
        }

        private void QueueDynamicBindingReload(
            object target,
            string propertyName,
            bool presetsOnly,
            PresetChangedEventArgs presetChange)
        {
            if (_pendingDynamicBindingReloads == null)
                return;

            DynamicBindingReloadRequest request =
                new DynamicBindingReloadRequest();

            request.Target = target;
            request.PropertyName = propertyName;
            request.PresetsOnly = presetsOnly;
            request.PresetChange = presetChange;
            _pendingDynamicBindingReloads.Add(request);
        }

        private void DrainPendingDynamicBindingReloads()
        {
            if (_drainingDynamicBindingReloads ||
                _pendingDynamicBindingReloads == null ||
                _pendingDynamicBindingReloads.Count == 0)
            {
                return;
            }

            _drainingDynamicBindingReloads = true;

            try
            {
                int head = 0;

                while (head < _pendingDynamicBindingReloads.Count)
                {
                    DynamicBindingReloadRequest request =
                        _pendingDynamicBindingReloads[head] as
                            DynamicBindingReloadRequest;
                    head++;

                    if (request == null)
                        continue;

                    ReloadDynamicBindings(
                        request.Target,
                        request.PropertyName,
                        request.PresetsOnly,
                        request.PresetChange);
                }

                _pendingDynamicBindingReloads.Clear();
            }
            catch
            {
                _pendingDynamicBindingReloads.Clear();
                throw;
            }
            finally
            {
                _drainingDynamicBindingReloads = false;
            }
        }

        private bool DynamicBindingAffectsLayout(
            DynamicPropertyBinding binding)
        {
            if (binding == null)
                return false;

            if (binding.StyleCondition || binding.ConditionalProperty)
                return true;

            if (EqualsIgnoreCase(binding.PropertyName, "Condition"))
                return true;

            if (IsResourceStyleProperty(
                    binding.Target,
                    binding.PropertyName) ||
                binding.PropertyName.IndexOf('.') >= 0)
            {
                return true;
            }

            if (EqualsIgnoreCase(binding.PropertyName, "Source") &&
                binding.Target is PictureBox)
            {
                ElementInfo info = GetInfo(binding.Target);

                if (info.WidthExplicit && info.HeightExplicit)
                    return false;
            }

            return AttributeCanAffectLayout(binding.PropertyName);
        }

        private static bool CanScopeDynamicBindingLayout(
            DynamicPropertyBinding binding,
            bool affectsInheritance,
            Control affectedControl)
        {
            if (binding == null ||
                affectedControl == null ||
                affectsInheritance ||
                binding.StyleCondition ||
                binding.ConditionalProperty ||
                binding.PropertyName.IndexOf('.') >= 0 ||
                EqualsIgnoreCase(binding.PropertyName, "Condition") ||
                IsResourceStyleProperty(
                    binding.Target,
                    binding.PropertyName))
            {
                return false;
            }

            return true;
        }

        private void AddScopedDynamicLayoutTargets(
            Control affectedControl,
            Control rootControl,
            ref Hashtable targets)
        {
            if (affectedControl == null)
                return;

            if (targets == null)
            {
                targets = new Hashtable(
                    _runtimeObjectReferenceComparer);
            }

            Control current = affectedControl;

            while (current != null)
            {
                targets[current] = true;

                if (Object.ReferenceEquals(current, rootControl))
                    break;

                current = current.Parent;
            }
        }

        private static void PerformScopedDynamicBindingLayouts(
            Hashtable targets)
        {
            if (targets == null || targets.Count == 0)
                return;

            ArrayList pending = new ArrayList(targets.Keys);

            while (pending.Count > 0)
            {
                int deepestIndex = 0;
                int deepestDepth = GetControlDepth(
                    pending[0] as Control);
                int i;

                for (i = 1; i < pending.Count; i++)
                {
                    int depth = GetControlDepth(
                        pending[i] as Control);

                    if (depth > deepestDepth)
                    {
                        deepestDepth = depth;
                        deepestIndex = i;
                    }
                }

                Control control = pending[deepestIndex] as Control;
                pending.RemoveAt(deepestIndex);

                if (control != null &&
                    !control.IsDisposed &&
                    !control.Disposing)
                {
                    control.PerformLayout();
                }
            }
        }

        private static int GetControlDepth(Control control)
        {
            int depth = 0;

            while (control != null)
            {
                depth++;
                control = control.Parent;
            }

            return depth;
        }

        private static bool DynamicBindingAffectsInheritance(
            DynamicPropertyBinding binding)
        {
            if (binding == null)
                return false;

            string name = binding.PropertyName;

            if (binding.StyleCondition)
                return true;

            return PropertyAffectsInheritance(binding.Target, name);
        }

        private static bool PropertyAffectsInheritance(
            object target,
            string name)
        {
            return
                IsResourceStyleProperty(target, name) ||
                EqualsIgnoreCase(name, "Background") ||
                EqualsIgnoreCase(name, "BackColor") ||
                EqualsIgnoreCase(name, "Foreground") ||
                EqualsIgnoreCase(name, "ForeColor") ||
                EqualsIgnoreCase(name, "Font") ||
                EqualsIgnoreCase(name, "FontSize") ||
                EqualsIgnoreCase(name, "FontFamily") ||
                EqualsIgnoreCase(name, "FontWeight") ||
                EqualsIgnoreCase(name, "FontStyle") ||
                EqualsIgnoreCase(name, "TextDecorations") ||
                EqualsIgnoreCase(name, "FlowDirection") ||
                EqualsIgnoreCase(name, "RightToLeft");
        }

        private bool ApplyDynamicBinding(
            DynamicPropertyBinding binding)
        {
            try
            {
                Dictionary<string, StyleDefinition> previousNamedStyles =
                    _activeComponentNamedStyles;
                List<StyleDefinition> previousImplicitStyles =
                    _activeComponentImplicitStyles;
                object previousEventTarget =
                    _activeComponentEventTarget;

                try
                {
                    // A retained binding must always use the resource scope in
                    // which it was declared. Null deliberately selects the
                    // runtime-wide scope for non-component bindings.
                    _activeComponentNamedStyles =
                        binding.ComponentNamedStyles;
                    _activeComponentImplicitStyles =
                        binding.ComponentImplicitStyles;
                    _activeComponentEventTarget =
                        binding.EventTarget;

                    BeginObservableTargetUpdate(
                        binding,
                        binding.InnerText
                            ? "Text"
                            : binding.PropertyName);

                    try
                    {
                        ReplaceDynamicObservableBindings(binding);
                        return ApplyDynamicBindingCore(binding);
                    }
                    finally
                    {
                        EndObservableTargetUpdate(binding);
                    }
                }
                finally
                {
                    _activeComponentNamedStyles = previousNamedStyles;
                    _activeComponentImplicitStyles = previousImplicitStyles;
                    _activeComponentEventTarget = previousEventTarget;
                }
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateDynamicBindingLoadException(
                    binding,
                    ex);
            }
        }

        private bool ApplyDynamicBindingCore(
            DynamicPropertyBinding binding)
        {
            if (binding.StyleCondition)
            {
                QueueConditionalStyleRefresh(binding);
                return true;
            }

            if (binding.ConditionalProperty)
            {
                return ApplyConditionalPropertyBinding(binding);
            }

            if (EqualsIgnoreCase(binding.PropertyName, "Condition"))
            {
                return ApplyDynamicConditionBinding(binding);
            }

            if (binding.InnerText)
            {
                string text =
                    ResolveBindingTextValue(
                        binding.Expression,
                        binding.DataContext);

                if (TryTakeUnsetPresetValue(text))
                {
                    if (!RestoreMissingDynamicPresetValue(binding, "Text"))
                        ResetPresetBoundProperty(binding.Target, "Text");

                    return true;
                }

                PrepareDynamicPresetValue(binding, "Text");

                if (IsUnchangedDynamicClrProperty(
                        binding.Target,
                        "Text",
                        text))
                {
                    CompleteDynamicPresetValue(binding);
                    return false;
                }

                bool textApplied =
                    TrySetProperty(binding.Target, "Text", text);

                if (textApplied)
                    CompleteDynamicPresetValue(binding);

                return textApplied;
            }

            string resolved =
                ResolveBindingAttributeValue(
                    binding.Expression,
                    binding.DataContext);

            if (TryTakeUnsetPresetValue(resolved))
            {
                if (binding.PropertyName.IndexOf('.') < 0 &&
                    !IsResourceStyleProperty(
                        binding.Target,
                        binding.PropertyName))
                {
                    if (!RestoreMissingDynamicPresetValue(
                            binding,
                            binding.PropertyName))
                    {
                        ResetPresetBoundProperty(
                            binding.Target,
                            binding.PropertyName);
                    }
                }

                return true;
            }

            PrepareDynamicPresetValue(
                binding,
                binding.PropertyName);

            if (IsResourceStyleProperty(
                    binding.Target,
                    binding.PropertyName))
            {
                object boundStyle;

                if (TryTakeBoundObject(resolved, out boundStyle))
                {
                    ApplyStyleValue(
                        binding.Target,
                        BindingValueToString(boundStyle));
                }
                else
                {
                    ApplyStyleValue(binding.Target, resolved);
                }

                CompleteDynamicPresetValue(binding);
                return true;
            }

            if (binding.PropertyName.IndexOf('.') >= 0)
            {
                Control child = binding.Target as Control;

                if (child == null)
                {
                    throw new InvalidOperationException(
                        "Attached property '" + binding.PropertyName +
                        "' requires a Control target.");
                }

                object attachedValue;

                if (TryTakeBoundObject(resolved, out attachedValue))
                    resolved = BindingValueToString(attachedValue);

                ApplyAttachedProperty(
                    GetInfo(child),
                    child,
                    binding.PropertyName,
                    resolved);

                CompleteDynamicPresetValue(binding);
                return true;
            }

            if (binding.StyleSetter)
            {
                ApplyStyleSetterAttribute(
                    binding.Target,
                    binding.PropertyName,
                    resolved);
            }
            else
            {
                if (IsUnchangedDynamicClrProperty(
                        binding.Target,
                        binding.PropertyName,
                        resolved))
                {
                    CompleteDynamicPresetValue(binding);
                    return false;
                }

                ApplyAttribute(
                    binding.Target,
                    binding.PropertyName,
                    resolved);
            }

            CompleteDynamicPresetValue(binding);
            return true;
        }

        private void PrepareDynamicPresetValue(
            DynamicPropertyBinding binding,
            string propertyName)
        {
            if (binding == null ||
                !binding.UsesPreset ||
                !binding.PresetValueStateKnown ||
                !binding.PresetValueUnset)
            {
                return;
            }

            if (binding.PresetBaselineRestore == null)
            {
                binding.PresetBaselineRestore =
                    CapturePresetBoundPropertyBaseline(
                        binding.Target,
                        propertyName);
            }

            // Mark the overlay active before invoking the native setter. A
            // setter can synchronously raise application events and cause a
            // reentrant preset refresh; that refresh must see a value that now
            // needs removing, not the previous missing-key state.
            binding.PresetValueUnset = false;
        }

        private static void CompleteDynamicPresetValue(
            DynamicPropertyBinding binding)
        {
            if (binding == null ||
                !binding.UsesPreset ||
                !binding.PresetValueStateKnown)
            {
                return;
            }

            binding.PresetValueUnset = false;
        }

        private bool RestoreMissingDynamicPresetValue(
            DynamicPropertyBinding binding,
            string propertyName)
        {
            if (binding == null ||
                !binding.UsesPreset ||
                !binding.PresetValueStateKnown)
            {
                return false;
            }

            // Missing is a state, not a value to assign. If this binding was
            // already missing, it never installed an overlay that needs to be
            // removed. Resetting here would erase a style, inherited value, or
            // native default that is correctly visible underneath it.
            if (binding.PresetValueUnset &&
                binding.PresetBaselineRestore == null)
            {
                return true;
            }

            // A captured lower layer is authoritative. Otherwise this binding
            // started with a resolved preset value, so remove that value with
            // the normal native/property reset path.
            if (binding.PresetBaselineRestore != null)
            {
                RestorePresetBoundProperty(
                    binding.Target,
                    propertyName,
                    binding.PresetBaselineRestore);
                binding.PresetBaselineRestore = null;
            }
            else
            {
                ResetPresetBoundProperty(
                    binding.Target,
                    propertyName);
            }

            binding.PresetValueUnset = true;
            return true;
        }

        private bool IsUnchangedDynamicClrProperty(
            object target,
            string propertyName,
            string resolvedValue)
        {
            if (target == null || String.IsNullOrEmpty(propertyName))
                return false;

            string key = GetStylePropertyKey(target, propertyName);

            if (UsesMappedPropertyPath(target, propertyName, key))
                return false;

            PropertyInfo property = FindProperty(
                target.GetType(),
                propertyName);

            if (property == null ||
                !property.CanRead ||
                !property.CanWrite ||
                property.GetIndexParameters().Length != 0 ||
                property.DeclaringType == null ||
                property.DeclaringType.Assembly !=
                    typeof(Control).Assembly)
            {
                return false;
            }

            object desired;
            object boundValue;
            bool bound = TryPeekBoundObject(
                resolvedValue,
                out boundValue);

            if (bound)
            {
                if (boundValue == null)
                {
                    if (property.PropertyType.IsValueType)
                        return false;

                    desired = null;
                }
                else if (property.PropertyType.IsAssignableFrom(
                    boundValue.GetType()))
                {
                    desired = boundValue;
                }
                else
                {
                    // Avoid allocating a converted disposable/reference object
                    // merely to test equality. The ordinary assignment path
                    // remains authoritative for non-assignable bound values.
                    return false;
                }
            }
            else
            {
                if (!property.PropertyType.IsValueType &&
                    property.PropertyType != typeof(string))
                {
                    return false;
                }

                try
                {
                    desired = ConvertString(
                        resolvedValue,
                        property.PropertyType);
                }
                catch
                {
                    // Preserve the established conversion diagnostics from the
                    // ordinary assignment path.
                    return false;
                }
            }

            object current;

            if (!TryReadPropertyValue(target, property, out current) ||
                !AreDynamicEffectiveValuesEquivalent(current, desired))
            {
                return false;
            }

            if (bound)
            {
                object ignored;
                TryTakeBoundObject(resolvedValue, out ignored);
            }

            return true;
        }

        private static bool AreDynamicEffectiveValuesEquivalent(
            object left,
            object right)
        {
            if (Object.ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            Type leftType = left.GetType();

            if (leftType != right.GetType())
                return false;

            return leftType.IsValueType || leftType == typeof(string)
                ? Object.Equals(left, right)
                : false;
        }

        private bool ApplyDynamicConditionBinding(
            DynamicPropertyBinding binding)
        {
            object target = binding == null
                ? null
                : binding.Target;

            if (target == null)
            {
                throw new InvalidOperationException(
                    "Condition requires a target object.");
            }

            object value = EvaluateTemplateExpressionValue(
                binding.Expression,
                binding.DataContext);

            if (IsUnsetPresetValue(value))
            {
                ElementInfo unsetInfo = GetInfo(target);
                RemoveElementConditionState(unsetInfo, binding);
                ApplyElementEffectiveVisibility(target, unsetInfo);
                SynchronizeRenderedItemRootCondition(target as Control);
                return true;
            }

            object converted;

            if (!TryConvertObjectValue(
                    value,
                    typeof(bool),
                    out converted))
            {
                throw new InvalidOperationException(
                    "Condition function/binding must return a boolean value.");
            }

            ElementInfo info = GetInfo(target);
            Control control = target as Control;
            bool previousVisible = control != null && control.Visible;
            SetElementConditionState(
                info,
                binding,
                (bool)converted);

            if (!ApplyElementEffectiveVisibility(target, info))
            {
                throw new InvalidOperationException(
                    "Dynamic Condition requires a Control or a writable " +
                    "boolean Visible property.");
            }

            SynchronizeRenderedItemRootCondition(target as Control);
            return control == null || previousVisible != control.Visible;
        }

        private void ApplyRetainedDynamicCondition(
            object target,
            ArrayList bindings)
        {
            int i;

            for (i = 0; bindings != null && i < bindings.Count; i++)
            {
                DynamicPropertyBinding binding =
                    bindings[i] as DynamicPropertyBinding;

                if (binding != null &&
                    binding.Active &&
                    Object.ReferenceEquals(binding.Target, target) &&
                    EqualsIgnoreCase(
                        binding.PropertyName,
                        "Condition"))
                {
                    ApplyDynamicBinding(binding);
                }
            }
        }

        private void ApplyStyleValue(
            object target,
            string styleValue)
        {
            ElementInfo info = GetInfo(target);

            if (info.StyleTransitionActive)
            {
                // A setter or restore callback requested another style. Let the
                // current transition finish as one coherent operation, then apply
                // only the latest nested request.
                if (String.Equals(
                    info.StyleTransitionCurrentValue,
                    styleValue,
                    StringComparison.Ordinal))
                {
                    info.StyleTransitionPending = false;
                    info.StyleTransitionPendingValue = null;
                }
                else
                {
                    info.StyleTransitionPending = true;
                    info.StyleTransitionPendingValue = styleValue;
                }

                return;
            }

            info.StyleTransitionActive = true;
            string requestedStyle = styleValue;

            try
            {
                while (true)
                {
                    info.StyleTransitionCurrentValue = requestedStyle;
                    info.StyleTransitionPending = false;
                    info.StyleTransitionPendingValue = null;

                    ApplyStyleValueCore(target, requestedStyle, info);

                    if (!info.StyleTransitionPending)
                        break;

                    requestedStyle = info.StyleTransitionPendingValue;
                }
            }
            finally
            {
                info.StyleTransitionActive = false;
                info.StyleTransitionPending = false;
                info.StyleTransitionCurrentValue = null;
                info.StyleTransitionPendingValue = null;
            }
        }

        private void ApplyStyleValueCore(
            object target,
            string styleValue,
            ElementInfo info)
        {
            StyleDefinition style = null;

            if (!String.IsNullOrEmpty(styleValue))
            {
                string key = ExtractStaticResourceKey(styleValue);

                if (String.IsNullOrEmpty(key))
                    key = styleValue.Trim();

                if (!GetCurrentNamedStyles().TryGetValue(key, out style))
                {
                    throw new InvalidOperationException(
                        "Style resource '" + key + "' was not found.");
                }
            }


            info.AppliedNamedStyleValue = styleValue;

            RestoreActiveStyleValues(target);
            ReleaseStyleBoundEvents(target);
            DeactivateStyleSetterBindings(target);

            ApplyImplicitStyles(
                target,
                info.XamlType);

            if (style != null)
            {
                ApplyStyleDefinition(
                    target,
                    style,
                    new List<string>());
            }

            ReconcileApplicationIconDefault(target as Form);
            ReapplyConditionalPropertyBindings(target);
        }

    }
}
