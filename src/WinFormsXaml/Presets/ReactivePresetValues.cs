using System;
using System.Collections;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private sealed class UnsetPresetValueMarker
        {
        }

        private static readonly object UnsetPresetValue =
            new UnsetPresetValueMarker();

        private Hashtable _resolvingPresetValues;
        private Hashtable _resolvingPresetDependencies;

        private static bool IsUnsetPresetValue(object value)
        {
            return Object.ReferenceEquals(value, UnsetPresetValue);
        }

        /// <summary>
        /// Resolves a preset's stored value in this runtime. PresetManager keeps
        /// the portable declaration; bindings and functions are evaluated against
        /// this runtime's code-behind object so a shared manager remains safe to
        /// use with more than one form.
        /// </summary>
        private object ResolveReactivePresetValue(
            string setName,
            string key)
        {
            if (_resolvingPresetValues == null)
            {
                _resolvingPresetValues =
                    new Hashtable(StringComparer.OrdinalIgnoreCase);
            }

            string identity = GetPresetValueIdentity(setName, key);

            if (_resolvingPresetValues.ContainsKey(identity))
            {
                throw new InvalidOperationException(
                    "Preset values contain a reference cycle at '" +
                    setName + "." + key + "'.");
            }

            _resolvingPresetValues.Add(identity, null);

            try
            {
                object storedValue;

                if (!_presetManager.TryResolve(
                        setName,
                        key,
                        out storedValue))
                {
                    return UnsetPresetValue;
                }

                return ResolveStoredPresetValue(storedValue);
            }
            finally
            {
                _resolvingPresetValues.Remove(identity);
            }
        }

        private object ResolveStoredPresetValue(object storedValue)
        {
            string expression = storedValue as string;

            if (expression == null)
            {
                return ResolveBindingPath(
                    storedValue,
                    String.Empty);
            }

            TemplateExpressionPlan plan =
                GetTemplateExpressionPlan(expression);

            if (plan.Kind == TemplateExpressionKind.Function)
            {
                object functionResult;

                if (_activeFunctionResultCache != null &&
                    _activeFunctionResultCache.ContainsKey(expression))
                {
                    functionResult =
                        _activeFunctionResultCache[expression];
                }
                else
                {
                    functionResult = InvokeBindingFunction(
                        plan.MethodName,
                        plan.ArgumentText,
                        _eventTarget,
                        plan.AutomaticDataContext);

                    if (_activeFunctionResultCache != null)
                    {
                        _activeFunctionResultCache[expression] =
                            functionResult;
                    }
                }

                return ResolveBindingPath(
                    functionResult,
                    String.Empty);
            }

            if (plan.Kind == TemplateExpressionKind.Preset)
            {
                return ResolveReactivePresetValue(
                    plan.PresetSetName,
                    plan.PresetKey);
            }

            if (plan.Kind == TemplateExpressionKind.PresetCondition)
            {
                return EvaluatePresetConditionExpression(
                    plan.PresetConditionPlan);
            }

            if (plan.Kind == TemplateExpressionKind.Binding)
            {
                if (plan.BindingPlan.Mode == BindingMode.TwoWay)
                {
                    throw new InvalidOperationException(
                        "Preset values are sources and support only OneWay " +
                        "Binding expressions.");
                }

                return ResolveBindingExpressionValue(
                    ResolveBindingSource(
                        _eventTarget,
                        plan.BindingPlan),
                    plan.BindingPlan);
            }

            if (plan.Kind == TemplateExpressionKind.Interpolated)
            {
                string resolved = ResolveInterpolatedText(
                    expression,
                    _eventTarget);

                if (TryTakeUnsetPresetValue(resolved))
                    return UnsetPresetValue;

                return resolved;
            }

            return expression;
        }

        private BindingPathResult MergeReactivePresetDependencies(
            string expression,
            BindingPathResult aggregate,
            BindingDependencySourceIndex aggregateSources)
        {
            if (!ContainsPresetExpression(expression))
                return aggregate;

            if (_resolvingPresetDependencies == null)
            {
                _resolvingPresetDependencies =
                    new Hashtable(StringComparer.OrdinalIgnoreCase);
            }

            int searchFrom = 0;

            while (searchFrom < expression.Length)
            {
                int start = expression.IndexOf(
                    "{Preset ",
                    searchFrom,
                    StringComparison.OrdinalIgnoreCase);

                if (start < 0)
                    break;

                int end = expression.IndexOf('}', start + 1);

                if (end < 0)
                    break;

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
                    if (aggregate == null)
                        aggregate = new BindingPathResult();

                    MergePresetConditionObservableDependencies(
                        conditionPlan,
                        aggregate,
                        aggregateSources);
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
                    BindingPathResult dependencies =
                        ResolveReactivePresetDependencies(
                            setName,
                            key);

                    if (dependencies != null &&
                        dependencies.Dependencies.Count > 0)
                    {
                        if (aggregate == null)
                            aggregate = new BindingPathResult();

                        MergeBindingPathDependencies(
                            aggregate,
                            dependencies,
                            aggregateSources);
                    }
                }

                searchFrom = end + 1;
            }

            return aggregate;
        }

        private BindingPathResult ResolveReactivePresetDependencies(
            string setName,
            string key)
        {
            string identity = GetPresetValueIdentity(setName, key);

            if (_resolvingPresetDependencies.ContainsKey(identity))
            {
                throw new InvalidOperationException(
                    "Preset values contain a reference cycle at '" +
                    setName + "." + key + "'.");
            }

            _resolvingPresetDependencies.Add(identity, null);

            try
            {
                object storedValue;

                if (!_presetManager.TryResolve(
                        setName,
                        key,
                        out storedValue))
                {
                    return null;
                }

                string expression = storedValue as string;

                if (expression == null)
                {
                    return ResolveBindingPathResult(
                        storedValue,
                        String.Empty);
                }

                BindingExpressionPlan directPlan;
                BindingPathResult result =
                    ResolveObservableExpressionDependencies(
                        expression,
                        _eventTarget,
                        out directPlan);

                if (directPlan != null &&
                    directPlan.Mode == BindingMode.TwoWay)
                {
                    throw new InvalidOperationException(
                        "Preset values are sources and support only OneWay " +
                        "Binding expressions.");
                }

                return result;
            }
            finally
            {
                _resolvingPresetDependencies.Remove(identity);
            }
        }

        private static string GetPresetValueIdentity(
            string setName,
            string key)
        {
            return
                (setName == null
                    ? String.Empty
                    : setName) +
                "\u001f" +
                (key == null
                    ? String.Empty
                    : key);
        }
    }
}
