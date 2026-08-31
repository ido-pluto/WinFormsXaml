using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private int _nextConditionalStyleBindingId;

        private string CreateConditionalStyleBindingKey(string kind)
        {
            if (_nextConditionalStyleBindingId != Int32.MaxValue)
                _nextConditionalStyleBindingId++;

            return "__WinFormsXaml." + kind + "Condition." +
                _nextConditionalStyleBindingId.ToString();
        }

        private bool EvaluateConditionalStylePart(
            object target,
            string expression,
            string bindingKey,
            DynamicBindingMarkup markup)
        {
            if (String.IsNullOrEmpty(expression))
                return true;

            try
            {
                object dataContext = GetCurrentBuildDataContext();

                if (ContainsDynamicExpression(expression) &&
                    (_templateBuildDepth == 0 || _componentBuildDepth != 0))
                {
                    RegisterConditionalStyleBinding(
                        target,
                        bindingKey,
                        expression,
                        dataContext,
                        markup);
                }

                return EvaluateConditionExpressionValue(
                    expression,
                    dataContext,
                    "Style/Setter Condition");
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DynamicPropertyBinding diagnosticBinding =
                    new DynamicPropertyBinding();
                diagnosticBinding.PropertyName = "Condition";
                diagnosticBinding.Markup = markup;

                throw CreateDynamicBindingLoadException(
                    diagnosticBinding,
                    ex);
            }
        }

        private bool EvaluateConditionExpressionValue(
            string expression,
            object dataContext,
            string description)
        {
            if (String.IsNullOrEmpty(expression))
                return true;

            object value = EvaluateTemplateExpressionValue(
                expression,
                dataContext);

            if (IsUnsetPresetValue(value))
                return false;

            object converted;

            if (TryConvertObjectValue(value, typeof(bool), out converted))
                return (bool)converted;

            throw new InvalidOperationException(
                description + " must resolve to a boolean value.");
        }

        private void RegisterConditionalStyleBinding(
            object target,
            string bindingKey,
            string expression,
            object dataContext,
            DynamicBindingMarkup markup)
        {
            if (target == null ||
                String.IsNullOrEmpty(bindingKey) ||
                String.IsNullOrEmpty(expression))
            {
                return;
            }

            DynamicPropertyBinding binding =
                new DynamicPropertyBinding();
            binding.Target = target;
            binding.PropertyName = bindingKey;
            binding.PropertyKey = bindingKey;
            binding.Expression = expression;
            binding.DataContext = dataContext;
            binding.UsesPreset = ContainsPresetExpression(expression);
            binding.MayUsePreset =
                binding.UsesPreset ||
                ComponentDataContextMayUsePresets(dataContext);
            binding.StyleCondition = true;
            binding.Active = true;
            binding.Markup = markup;
            CaptureComponentScope(binding);
            UpsertDynamicBinding(binding);
        }

        private void ReapplyConditionalStyleLayers(object target)
        {
            if (target == null)
                return;

            ElementInfo info = GetInfo(target);
            ApplyStyleValue(target, info.AppliedNamedStyleValue);
        }

        private void QueueConditionalStyleRefresh(
            DynamicPropertyBinding binding)
        {
            object target = binding == null ? null : binding.Target;

            if (target == null)
                return;

            if (_reloadingDynamicBindings &&
                _conditionalStyleRefreshTargets != null)
            {
                _conditionalStyleRefreshTargets[target] = binding;
                return;
            }

            ReapplyConditionalStyleLayers(binding);
        }

        private void ApplyPendingConditionalStyleRefreshes()
        {
            if (_conditionalStyleRefreshTargets == null ||
                _conditionalStyleRefreshTargets.Count == 0)
            {
                return;
            }

            object[] bindings = new object[
                _conditionalStyleRefreshTargets.Count];
            _conditionalStyleRefreshTargets.Values.CopyTo(bindings, 0);
            _conditionalStyleRefreshTargets.Clear();
            int i;

            for (i = 0; i < bindings.Length; i++)
            {
                ReapplyConditionalStyleLayers(
                    bindings[i] as DynamicPropertyBinding);
            }
        }

        private void ReapplyConditionalStyleLayers(
            DynamicPropertyBinding binding)
        {
            if (binding == null || binding.Target == null)
                return;

            Dictionary<string, StyleDefinition> previousNamedStyles =
                _activeComponentNamedStyles;
            List<StyleDefinition> previousImplicitStyles =
                _activeComponentImplicitStyles;
            object previousEventTarget = _activeComponentEventTarget;

            try
            {
                _activeComponentNamedStyles =
                    binding.ComponentNamedStyles;
                _activeComponentImplicitStyles =
                    binding.ComponentImplicitStyles;
                _activeComponentEventTarget = binding.EventTarget;
                ReapplyConditionalStyleLayers(binding.Target);
            }
            finally
            {
                _activeComponentNamedStyles = previousNamedStyles;
                _activeComponentImplicitStyles = previousImplicitStyles;
                _activeComponentEventTarget = previousEventTarget;
            }
        }

        private void RegisterConditionalPropertyBinding(
            object target,
            string propertyName,
            object propertyValue,
            object baselineValue,
            string expression,
            object dataContext,
            DynamicBindingMarkup markup)
        {
            DynamicPropertyBinding binding =
                new DynamicPropertyBinding();
            binding.Target = target;
            binding.PropertyName =
                "__WinFormsXaml.PropertyCondition." + propertyName;
            binding.PropertyKey = binding.PropertyName;
            binding.Expression = expression;
            binding.DataContext = dataContext;
            binding.UsesPreset = ContainsPresetExpression(expression);
            binding.MayUsePreset =
                binding.UsesPreset ||
                ComponentDataContextMayUsePresets(dataContext);
            binding.ConditionalProperty = true;
            binding.ConditionedPropertyName = propertyName;
            binding.ConditionedPropertyValue = propertyValue;
            binding.ConditionedPropertyBaseline = baselineValue;
            binding.Active = true;
            binding.Markup = markup;
            CaptureComponentScope(binding);
            UpsertDynamicBinding(binding);
            ApplyConditionalPropertyBinding(binding);
        }

        private bool TryHandleConditionalPropertyElement(
            object parent,
            XmlElement propertyElement,
            string propertyName)
        {
            string condition = GetAttributeIgnoreNamespace(
                propertyElement,
                "Condition");

            if (String.IsNullOrEmpty(condition))
                return false;

            ValidateStructuralOneWayBinding(
                propertyElement,
                "Condition");

            object dataContext = GetCurrentBuildDataContext();
            bool dynamic = ContainsDynamicExpression(condition);

            if (!dynamic)
            {
                if (!EvaluateConditionExpressionValue(
                        condition,
                        dataContext,
                        "Property element Condition"))
                {
                    return true;
                }

                return false;
            }

            PropertyInfo property = FindProperty(
                parent.GetType(),
                propertyName);

            if (property == null || !property.CanWrite)
            {
                throw new InvalidOperationException(
                    "Dynamic Condition on <" + propertyElement.LocalName +
                    "> requires a writable object property.");
            }

            XmlElement valueElement = null;
            XmlNode node = propertyElement.FirstChild;

            while (node != null)
            {
                XmlElement candidate = node as XmlElement;

                if (candidate != null)
                {
                    if (valueElement != null)
                    {
                        throw new InvalidOperationException(
                            "Dynamic Condition on <" +
                            propertyElement.LocalName +
                            "> supports exactly one object value.");
                    }

                    valueElement = candidate;
                }

                node = node.NextSibling;
            }

            if (valueElement == null)
            {
                throw new InvalidOperationException(
                    "Dynamic Condition on <" + propertyElement.LocalName +
                    "> requires one object value.");
            }

            object value = BuildElement(valueElement);

            if (value != null &&
                !property.PropertyType.IsAssignableFrom(value.GetType()))
            {
                ReleaseCreatedElement(value);
                throw new InvalidOperationException(
                    "The conditional value for '" + propertyName +
                    "' is not assignable to " +
                    property.PropertyType.FullName + ".");
            }

            object baseline = property.CanRead
                ? property.GetValue(parent, null)
                : null;

            if (value != null)
                RegisterLogicalChild(parent, value);

            try
            {
                RegisterConditionalPropertyBinding(
                    parent,
                    propertyName,
                    value,
                    baseline,
                    condition,
                    dataContext,
                    CaptureDynamicBindingMarkup(
                        propertyElement,
                        "Condition"));
            }
            catch
            {
                if (value != null)
                {
                    UnregisterLogicalChild(parent, value);
                    ReleaseCreatedElement(value);
                }

                throw;
            }

            return true;
        }

        private bool ApplyConditionalPropertyBinding(
            DynamicPropertyBinding binding)
        {
            if (binding == null || binding.Target == null)
                return false;

            bool active = EvaluateConditionExpressionValue(
                binding.Expression,
                binding.DataContext,
                "Property element Condition");
            object desired = active
                ? binding.ConditionedPropertyValue
                : binding.ConditionedPropertyBaseline;
            PropertyInfo property = FindProperty(
                binding.Target.GetType(),
                binding.ConditionedPropertyName);

            if (property == null || !property.CanWrite)
            {
                throw new InvalidOperationException(
                    "Conditional property element requires a writable '" +
                    binding.ConditionedPropertyName + "' property.");
            }

            object current = property.CanRead
                ? property.GetValue(binding.Target, null)
                : null;

            bool changed =
                !property.CanRead ||
                !AreDynamicEffectiveValuesEquivalent(current, desired);

            if (changed)
                property.SetValue(binding.Target, desired, null);

            binding.ConditionedPropertyApplied = active;
            return changed;
        }

        private void RestoreConditionalPropertyBinding(
            DynamicPropertyBinding binding)
        {
            if (binding == null ||
                !binding.ConditionalProperty ||
                binding.Target == null)
            {
                return;
            }

            PropertyInfo property = FindProperty(
                binding.Target.GetType(),
                binding.ConditionedPropertyName);

            if (property != null && property.CanWrite)
            {
                property.SetValue(
                    binding.Target,
                    binding.ConditionedPropertyBaseline,
                    null);
            }

            binding.ConditionedPropertyApplied = false;
        }

        private void ReapplyConditionalPropertyBindings(object target)
        {
            if (target == null || _dynamicPropertyBindings == null)
                return;

            int i;

            for (i = 0; i < _dynamicPropertyBindings.Count; i++)
            {
                DynamicPropertyBinding binding =
                    _dynamicPropertyBindings[i] as DynamicPropertyBinding;

                if (binding != null &&
                    binding.Active &&
                    binding.ConditionalProperty &&
                    Object.ReferenceEquals(binding.Target, target))
                {
                    ApplyConditionalPropertyBinding(binding);
                }
            }
        }
    }
}
