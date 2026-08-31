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
        private ArrayList CaptureDynamicBindings(
            XmlElement element,
            object dataContext)
        {
            ArrayList bindings = null;

            if (element == null ||
                (_templateBuildDepth != 0 && _componentBuildDepth == 0))
            {
                return bindings;
            }

            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];

                if (ShouldIgnoreAttribute(attribute) ||
                    EqualsIgnoreCase(attribute.LocalName, "Name"))
                {
                    continue;
                }

                try
                {
                    if (!ContainsDynamicExpression(attribute.Value))
                        continue;

                    DynamicPropertyBinding binding =
                        new DynamicPropertyBinding();

                    binding.PropertyName = attribute.LocalName;
                    binding.Markup = CaptureDynamicBindingMarkup(
                        element,
                        attribute.LocalName);
                    binding.Expression = attribute.Value;
                    binding.DataContext = dataContext;
                    binding.UsesPreset =
                        ContainsPresetExpression(attribute.Value);
                    binding.MayUsePreset =
                        binding.UsesPreset ||
                        ComponentDataContextMayUsePresets(dataContext);
                    binding.Active = true;
                    CaptureComponentScope(binding);
                    CaptureInitialDynamicObservableSnapshot(binding);

                    if (bindings == null)
                        bindings = new ArrayList();

                    bindings.Add(binding);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        element,
                        attribute.LocalName,
                        ex);
                }
            }

            if (!HasElementChildren(element) &&
                !HasAttributeIgnoreNamespace(element, "Text") &&
                !HasAttributeIgnoreNamespace(element, "Content") &&
                !HasAttributeIgnoreNamespace(element, "Header"))
            {
                try
                {
                    if (!ContainsDynamicExpression(element.InnerText))
                        return bindings;

                    DynamicPropertyBinding binding =
                        new DynamicPropertyBinding();

                    binding.PropertyName = "Text";
                    binding.Markup = CaptureDynamicBindingMarkup(
                        element,
                        "Text",
                        null);
                    binding.Expression = element.InnerText;
                    binding.DataContext = dataContext;
                    binding.InnerText = true;
                    binding.UsesPreset =
                        ContainsPresetExpression(element.InnerText);
                    binding.MayUsePreset =
                        binding.UsesPreset ||
                        ComponentDataContextMayUsePresets(dataContext);
                    binding.Active = true;
                    CaptureComponentScope(binding);
                    CaptureInitialDynamicObservableSnapshot(binding);

                    if (bindings == null)
                        bindings = new ArrayList();

                    bindings.Add(binding);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        element,
                        "Text",
                        ex);
                }
            }

            return bindings;
        }

        private void RegisterDynamicBindings(
            object target,
            ArrayList bindings,
            XmlElement sourceElement)
        {
            if (target == null || bindings == null || bindings.Count == 0)
                return;

            int i;

            for (i = 0; i < bindings.Count; i++)
            {
                DynamicPropertyBinding binding =
                    bindings[i] as DynamicPropertyBinding;

                if (binding == null)
                    continue;

                try
                {
                    binding.Target = target;
                    binding.PropertyKey = GetStylePropertyKey(
                        target,
                        binding.PropertyName);
                    InitializeCapturedPresetBindingState(
                        binding,
                        sourceElement);
                    UpsertDynamicBinding(binding);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        sourceElement,
                        binding.PropertyName,
                        ex);
                }
            }
        }

        private void RegisterDynamicBinding(
            object target,
            string propertyName,
            string expression,
            object dataContext,
            bool innerText)
        {
            RegisterDynamicBinding(
                target,
                propertyName,
                expression,
                dataContext,
                innerText,
                false);
        }

        private void RegisterDynamicBinding(
            object target,
            string propertyName,
            string expression,
            object dataContext,
            bool innerText,
            bool styleSetter)
        {
            RegisterDynamicBinding(
                target,
                propertyName,
                expression,
                dataContext,
                innerText,
                styleSetter,
                false,
                null,
                null,
                null);
        }

        private void RegisterDynamicBinding(
            object target,
            string propertyName,
            string expression,
            object dataContext,
            bool innerText,
            bool styleSetter,
            DynamicBindingMarkup markup)
        {
            RegisterDynamicBinding(
                target,
                propertyName,
                expression,
                dataContext,
                innerText,
                styleSetter,
                false,
                null,
                null,
                markup);
        }

        private void RegisterDynamicBinding(
            object target,
            string propertyName,
            string expression,
            object dataContext,
            bool innerText,
            bool styleSetter,
            bool hasInitialObservableSnapshot,
            BindingExpressionPlan initialDirectPlan,
            BindingPathResult initialPathResult,
            DynamicBindingMarkup markup)
        {
            if (target == null || String.IsNullOrEmpty(expression))
                return;

            DynamicPropertyBinding binding =
                new DynamicPropertyBinding();

            binding.Target = target;
            binding.PropertyName = propertyName;
            binding.PropertyKey = GetStylePropertyKey(
                target,
                propertyName);
            binding.Expression = expression;
            binding.DataContext = dataContext;
            binding.InnerText = innerText;
            binding.UsesPreset = ContainsPresetExpression(expression);
            binding.MayUsePreset =
                binding.UsesPreset ||
                ComponentDataContextMayUsePresets(dataContext);
            binding.StyleSetter = styleSetter;
            binding.Active = true;
            binding.HasInitialObservableSnapshot =
                hasInitialObservableSnapshot;
            binding.InitialDirectPlan = initialDirectPlan;
            binding.InitialPathResult = initialPathResult;
            binding.Markup = markup;
            CaptureComponentScope(binding);
            InitializeRegisteredPresetBindingState(binding);
            UpsertDynamicBinding(binding);
        }

        private void InitializeCapturedPresetBindingState(
            DynamicPropertyBinding binding,
            XmlElement sourceElement)
        {
            if (binding == null || !binding.UsesPreset)
                return;

            binding.PresetValueStateKnown = true;

            if (!binding.InnerText)
            {
                // ResolveElementBindings removes a local attribute only when
                // its preset value is currently missing. This lets us record
                // the initial overlay state without evaluating the expression
                // (and possibly invoking application code) a second time.
                binding.PresetValueUnset =
                    sourceElement == null ||
                    !HasAttributeIgnoreNamespace(
                        sourceElement,
                        binding.PropertyName);
                return;
            }

            binding.PresetValueUnset = IsUnsetPresetValue(
                EvaluateTemplateExpressionValue(
                    binding.Expression,
                    binding.DataContext));
        }

        private void InitializeRegisteredPresetBindingState(
            DynamicPropertyBinding binding)
        {
            if (binding == null || !binding.UsesPreset)
                return;

            object value = EvaluateTemplateExpressionValue(
                binding.Expression,
                binding.DataContext);

            binding.PresetValueStateKnown = true;
            binding.PresetValueUnset = IsUnsetPresetValue(value);

            // Style bindings are registered before their initial setter is
            // applied, so this is the one place where an initially resolved
            // preset can preserve the exact layer below it.
            if (binding.StyleSetter && !binding.PresetValueUnset)
            {
                binding.PresetBaselineRestore =
                    CapturePresetBoundPropertyBaseline(
                        binding.Target,
                        binding.PropertyName);
            }
        }

        private DynamicBindingMarkup CaptureDynamicBindingMarkup(
            XmlElement element,
            string propertyName)
        {
            return CaptureDynamicBindingMarkup(
                element,
                propertyName,
                propertyName);
        }

        private DynamicBindingMarkup CaptureDynamicBindingMarkup(
            XmlElement element,
            string propertyName,
            string locationPropertyName)
        {
            DynamicBindingMarkup markup =
                new DynamicBindingMarkup();

            string elementMarkupSource =
                MarkupXmlDocument.GetMarkupSource(element);
            string elementPathPrefix =
                MarkupXmlDocument.GetElementPathPrefix(element);

            markup.MarkupSource = !String.IsNullOrEmpty(elementMarkupSource)
                ? elementMarkupSource
                : (String.IsNullOrEmpty(_activeMarkupSource)
                    ? _markupSource
                    : _activeMarkupSource);
            markup.ElementPath = GetMarkupElementPath(
                element,
                !String.IsNullOrEmpty(elementPathPrefix)
                    ? elementPathPrefix
                    : _activeMarkupElementPathPrefix,
                _activeComponentContentRoot);
            markup.PropertyName = propertyName;

            MarkupXmlDocument.GetLocation(
                element,
                locationPropertyName,
                out markup.LineNumber,
                out markup.LinePosition);

            return markup;
        }

        private static WinFormsXamlLoadException
            CreateDynamicBindingLoadException(
                DynamicPropertyBinding binding,
                Exception innerException)
        {
            WinFormsXamlLoadException existing =
                innerException as WinFormsXamlLoadException;

            if (existing != null)
                return existing;

            DynamicBindingMarkup markup = binding == null
                ? null
                : binding.Markup;

            return new WinFormsXamlLoadException(
                markup == null ? null : markup.MarkupSource,
                markup == null ? null : markup.ElementPath,
                markup == null
                    ? (binding == null ? null : binding.PropertyName)
                    : markup.PropertyName,
                markup == null ? 0 : markup.LineNumber,
                markup == null ? 0 : markup.LinePosition,
                innerException);
        }

        private void CaptureComponentScope(
            DynamicPropertyBinding binding)
        {
            if (binding == null)
                return;

            // Item-template bindings retain the component instance that owned
            // the template. Their DataContext remains the row item, so deriving
            // this target again during refresh would incorrectly fall back to
            // the Form code-behind.
            binding.EventTarget =
                GetComponentEventTarget(binding.DataContext);

            if (_activeComponentNamedStyles == null)
                return;

            binding.ComponentNamedStyles = _activeComponentNamedStyles;
            binding.ComponentImplicitStyles = _activeComponentImplicitStyles;
        }

        private void CaptureInitialDynamicObservableSnapshot(
            DynamicPropertyBinding binding)
        {
            if (binding == null)
                return;

            BindingExpressionPlan directPlan;
            BindingPathResult pathResult =
                ResolveObservableExpressionDependencies(
                    binding.Expression,
                    binding.DataContext,
                    out directPlan);

            binding.InitialDirectPlan = directPlan;
            binding.InitialPathResult = pathResult;
            binding.HasInitialObservableSnapshot = true;
        }

        private void UpsertDynamicBinding(
            DynamicPropertyBinding binding)
        {
            if (_dynamicFeaturesDisposed ||
                binding == null ||
                binding.Target == null)
            {
                return;
            }

            if (!RetainDynamicTargetDisposalHook(binding.Target))
                return;

            DynamicPropertyBinding existing =
                FindIndexedDynamicBinding(
                    binding.Target,
                    binding.PropertyKey,
                    IsDynamicConditionBinding(binding));

            if (existing != null && existing.Active)
            {
                // A style reload must never replace a retained local value.
                if (binding.StyleSetter && !existing.StyleSetter)
                    return;

                int index = _dynamicPropertyBindings.IndexOf(existing);

                if (index >= 0)
                {
                    // Keep the original slot, but let the later source win.
                    // Build order is base style, derived/explicit style, then
                    // local attributes/property elements.
                    _dynamicPropertyBindings[index] = binding;

                    try
                    {
                        ReplaceDynamicObservableBindings(binding);
                    }
                    catch
                    {
                        _dynamicPropertyBindings[index] = existing;
                        throw;
                    }

                    IndexDynamicBinding(binding);
                    ReplacePresetDynamicBinding(
                        existing,
                        binding,
                        index);
                    DeactivateDynamicBinding(existing);
                    return;
                }

                // Recover from a stale index defensively. The registration is no
                // longer enumerable, so it must not keep source subscriptions.
                DeactivateDynamicBinding(existing);
            }

            _dynamicPropertyBindings.Add(binding);

            try
            {
                ReplaceDynamicObservableBindings(binding);
                IndexDynamicBinding(binding);
                AppendPresetDynamicBinding(binding);
            }
            catch
            {
                UnindexPresetDynamicBinding(binding);
                UnindexDynamicBinding(binding);
                DetachDynamicObservableBindings(binding);
                _dynamicPropertyBindings.RemoveAt(
                    _dynamicPropertyBindings.Count - 1);
                ReleaseDynamicTargetDisposalHookIfUnused(binding.Target);
                throw;
            }
        }

        private void ReleaseDynamicTargetDisposalHookIfUnused(object target)
        {
            int i;

            for (i = 0;
                 target != null &&
                 _dynamicPropertyBindings != null &&
                 i < _dynamicPropertyBindings.Count;
                 i++)
            {
                DynamicPropertyBinding retained =
                    _dynamicPropertyBindings[i] as DynamicPropertyBinding;

                if (retained != null &&
                    retained.Active &&
                    Object.ReferenceEquals(retained.Target, target))
                {
                    return;
                }
            }

            ReleaseDynamicTargetDisposalHook(target);
        }

        private DynamicPropertyBinding FindIndexedDynamicBinding(
            object target,
            string propertyKey,
            bool multipleAllowed)
        {
            if (multipleAllowed ||
                target == null ||
                String.IsNullOrEmpty(propertyKey) ||
                _dynamicBindingSlotsByTarget == null)
            {
                return null;
            }

            Dictionary<string, DynamicPropertyBinding> slots =
                _dynamicBindingSlotsByTarget[target] as
                    Dictionary<string, DynamicPropertyBinding>;
            DynamicPropertyBinding binding;

            return slots != null &&
                slots.TryGetValue(propertyKey, out binding)
                    ? binding
                    : null;
        }

        private void IndexDynamicBinding(DynamicPropertyBinding binding)
        {
            if (binding == null ||
                !binding.Active ||
                IsDynamicConditionBinding(binding) ||
                binding.Target == null ||
                String.IsNullOrEmpty(binding.PropertyKey) ||
                _dynamicBindingSlotsByTarget == null)
            {
                return;
            }

            Dictionary<string, DynamicPropertyBinding> slots =
                _dynamicBindingSlotsByTarget[binding.Target] as
                    Dictionary<string, DynamicPropertyBinding>;

            if (slots == null)
            {
                slots = new Dictionary<string, DynamicPropertyBinding>(
                    StringComparer.OrdinalIgnoreCase);
                _dynamicBindingSlotsByTarget.Add(binding.Target, slots);
            }

            slots[binding.PropertyKey] = binding;
        }

        private void UnindexDynamicBinding(DynamicPropertyBinding binding)
        {
            if (binding == null ||
                binding.Target == null ||
                String.IsNullOrEmpty(binding.PropertyKey) ||
                _dynamicBindingSlotsByTarget == null)
            {
                return;
            }

            Dictionary<string, DynamicPropertyBinding> slots =
                _dynamicBindingSlotsByTarget[binding.Target] as
                    Dictionary<string, DynamicPropertyBinding>;
            DynamicPropertyBinding indexed;

            if (slots == null ||
                !slots.TryGetValue(binding.PropertyKey, out indexed) ||
                !Object.ReferenceEquals(indexed, binding))
            {
                return;
            }

            slots.Remove(binding.PropertyKey);

            if (slots.Count == 0)
                _dynamicBindingSlotsByTarget.Remove(binding.Target);
        }

        private void AppendPresetDynamicBinding(
            DynamicPropertyBinding binding)
        {
            if (binding == null ||
                !binding.Active ||
                !binding.MayUsePreset ||
                _presetDynamicPropertyBindings == null)
            {
                return;
            }

            // Normal registrations append to the primary binding list, so the
            // filtered list can preserve the same order in O(1).
            _presetDynamicPropertyBindings.Add(binding);
        }

        private void ReplacePresetDynamicBinding(
            DynamicPropertyBinding existing,
            DynamicPropertyBinding replacement,
            int dynamicIndex)
        {
            if (_presetDynamicPropertyBindings == null ||
                replacement == null)
            {
                return;
            }

            int presetIndex = existing == null
                ? -1
                : _presetDynamicPropertyBindings.IndexOf(existing);

            if (presetIndex >= 0)
            {
                if (replacement.MayUsePreset)
                    _presetDynamicPropertyBindings[presetIndex] = replacement;
                else
                    _presetDynamicPropertyBindings.RemoveAt(presetIndex);

                return;
            }

            if (!replacement.MayUsePreset ||
                _dynamicPropertyBindings == null ||
                dynamicIndex < 0)
            {
                return;
            }

            int insertIndex = 0;
            int i;

            for (i = 0; i < dynamicIndex; i++)
            {
                DynamicPropertyBinding preceding =
                    _dynamicPropertyBindings[i] as DynamicPropertyBinding;

                if (preceding != null &&
                    preceding.Active &&
                    preceding.MayUsePreset)
                {
                    insertIndex++;
                }
            }

            if (insertIndex >= _presetDynamicPropertyBindings.Count)
                _presetDynamicPropertyBindings.Add(replacement);
            else
                _presetDynamicPropertyBindings.Insert(
                    insertIndex,
                    replacement);
        }

        private void UnindexPresetDynamicBinding(
            DynamicPropertyBinding binding)
        {
            if (binding != null &&
                _presetDynamicPropertyBindings != null)
            {
                _presetDynamicPropertyBindings.Remove(binding);
            }
        }

        private void DeactivateStyleSetterBinding(
            object target,
            string propertyName)
        {
            if (_dynamicPropertyBindings == null || target == null)
                return;

            string propertyKey = GetStylePropertyKey(
                target,
                propertyName);

            int i;

            for (i = _dynamicPropertyBindings.Count - 1; i >= 0; i--)
            {
                DynamicPropertyBinding binding =
                    _dynamicPropertyBindings[i] as DynamicPropertyBinding;

                if (binding == null ||
                    !binding.StyleSetter ||
                    !Object.ReferenceEquals(binding.Target, target) ||
                    !EqualsIgnoreCase(binding.PropertyKey, propertyKey))
                {
                    continue;
                }

                DeactivateDynamicBinding(binding);

                if (!_reloadingDynamicBindings)
                    _dynamicPropertyBindings.RemoveAt(i);
            }
        }

        private void DeactivateStyleSetterBindings(object target)
        {
            if (_dynamicPropertyBindings == null || target == null)
                return;

            int i;

            for (i = _dynamicPropertyBindings.Count - 1; i >= 0; i--)
            {
                DynamicPropertyBinding binding =
                    _dynamicPropertyBindings[i] as DynamicPropertyBinding;

                if (binding == null ||
                    !binding.StyleSetter ||
                    !Object.ReferenceEquals(binding.Target, target))
                {
                    continue;
                }

                DeactivateDynamicBinding(binding);

                if (!_reloadingDynamicBindings)
                    _dynamicPropertyBindings.RemoveAt(i);
            }
        }

        private void RemoveInactiveDynamicBindings()
        {
            if (_dynamicPropertyBindings == null)
                return;

            int i;

            for (i = _dynamicPropertyBindings.Count - 1; i >= 0; i--)
            {
                DynamicPropertyBinding binding =
                    _dynamicPropertyBindings[i] as DynamicPropertyBinding;

                if (binding != null && !binding.Active)
                {
                    DetachDynamicObservableBindings(binding);
                    _dynamicPropertyBindings.RemoveAt(i);
                }
            }
        }

        private void ReleaseDynamicBindings(object target)
        {
            if (_dynamicPropertyBindings == null || target == null)
                return;

            int i;

            for (i = _dynamicPropertyBindings.Count - 1; i >= 0; i--)
            {
                DynamicPropertyBinding binding =
                    _dynamicPropertyBindings[i] as DynamicPropertyBinding;

                if (binding != null &&
                    Object.ReferenceEquals(binding.Target, target))
                {
                    DeactivateDynamicBinding(binding);
                    _dynamicPropertyBindings.RemoveAt(i);
                }
            }

            ReleaseDynamicTargetDisposalHook(target);
        }

        private void DeactivateDynamicBinding(
            DynamicPropertyBinding binding)
        {
            if (binding == null)
                return;

            binding.Active = false;
            UnindexPresetDynamicBinding(binding);
            UnindexDynamicBinding(binding);
            DetachDynamicObservableBindings(binding);

            if (binding.ConditionalProperty &&
                !_dynamicFeaturesDisposed &&
                !IsDisposedTarget(binding.Target) &&
                !IsDynamicTargetDisposing(binding.Target))
            {
                RestoreConditionalPropertyBinding(binding);
            }

            if (IsDynamicConditionBinding(binding) &&
                binding.Target != null)
            {
                ElementInfo info;

                if (_elementInfos.TryGetValue(binding.Target, out info))
                {
                    RemoveElementConditionState(info, binding);

                    if (!_dynamicFeaturesDisposed &&
                        !IsDisposedTarget(binding.Target) &&
                        !IsDynamicTargetDisposing(binding.Target))
                    {
                        ApplyElementEffectiveVisibility(binding.Target, info);
                    }
                }
            }
        }

    }
}
