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
        private static bool IsDynamicConditionBinding(
            DynamicPropertyBinding binding)
        {
            return binding != null &&
                EqualsIgnoreCase(binding.PropertyName, "Condition");
        }

        private void ReplaceDynamicObservableBindings(
            DynamicPropertyBinding binding)
        {
            if (binding == null ||
                !binding.Active ||
                binding.Target == null ||
                _dynamicFeaturesDisposed)
            {
                return;
            }

            BindingExpressionPlan directPlan;
            BindingPathResult pathResult;

            if (binding.HasInitialObservableSnapshot)
            {
                directPlan = binding.InitialDirectPlan;
                pathResult = binding.InitialPathResult;
                binding.HasInitialObservableSnapshot = false;
                binding.InitialDirectPlan = null;
                binding.InitialPathResult = null;
            }
            else
            {
                pathResult = ResolveObservableExpressionDependencies(
                    binding.Expression,
                    binding.DataContext,
                    out directPlan);
            }

            if (directPlan != null &&
                directPlan.Mode == BindingMode.TwoWay)
            {
                ValidateTwoWayDynamicBinding(
                    binding,
                    directPlan,
                    pathResult);
            }

            bool shouldRetainRegistration =
                pathResult != null &&
                (pathResult.Dependencies.Count > 0 ||
                 (directPlan != null &&
                  directPlan.Mode == BindingMode.TwoWay));

            ObservableBindingRegistration current = null;

            if (binding.ObservableRegistrations != null &&
                binding.ObservableRegistrations.Count == 1)
            {
                current =
                    binding.ObservableRegistrations[0] as
                        ObservableBindingRegistration;
            }

            if (shouldRetainRegistration && current != null)
            {
                UpdateObservableBinding(current, pathResult);
                return;
            }

            ArrayList replacements = new ArrayList();

            try
            {
                if (shouldRetainRegistration)
                {
                    string targetPropertyName =
                        binding.InnerText
                            ? "Text"
                            : binding.PropertyName;

                    ObservableBindingRegistration registration =
                        AttachObservableBinding(
                            binding,
                            binding.Target,
                            targetPropertyName,
                            directPlan == null
                                ? BindingMode.OneWay
                                : directPlan.Mode,
                            directPlan == null
                                ? BindingUpdateSourceTrigger.PropertyChanged
                                : directPlan.UpdateSourceTrigger,
                            pathResult,
                            OnDynamicObservableBindingChanged);

                    if (registration != null)
                        replacements.Add(registration);
                }
            }
            catch
            {
                DetachObservableBindingList(replacements);
                throw;
            }

            ArrayList previous = binding.ObservableRegistrations;
            binding.ObservableRegistrations = replacements;
            DetachObservableBindingList(previous);
        }

        private void DetachDynamicObservableBindings(
            DynamicPropertyBinding binding)
        {
            if (binding == null)
                return;

            ArrayList registrations = binding.ObservableRegistrations;
            binding.ObservableRegistrations = null;
            DetachObservableBindingList(registrations);
        }

        private void DetachObservableBindingList(
            ArrayList registrations)
        {
            if (registrations == null)
                return;

            int i;

            for (i = registrations.Count - 1; i >= 0; i--)
            {
                ObservableBindingRegistration registration =
                    registrations[i] as ObservableBindingRegistration;

                if (registration != null)
                    DetachObservableBinding(registration);
            }

            registrations.Clear();
        }

        private BindingPathResult ResolveObservableExpressionDependencies(
            string expression,
            object dataContext,
            out BindingExpressionPlan directPlan)
        {
            directPlan = null;

            if (String.IsNullOrEmpty(expression))
                return null;

            TemplateExpressionPlan expressionPlan =
                GetTemplateExpressionPlan(expression);

            if (expressionPlan.Kind == TemplateExpressionKind.Binding)
            {
                directPlan = expressionPlan.BindingPlan;
                return ResolveBindingExpressionResult(
                    ResolveBindingSource(
                        dataContext,
                        directPlan),
                    directPlan);
            }

            BindingPathResult aggregate = null;
            BindingDependencySourceIndex aggregateSources = null;
            int searchFrom = 0;

            while (searchFrom < expression.Length)
            {
                int start = expression.IndexOf(
                    "{Binding",
                    searchFrom,
                    StringComparison.OrdinalIgnoreCase);

                if (start < 0)
                    break;

                int end = expression.IndexOf('}', start + 1);

                if (end < 0)
                    break;

                string segment = expression.Substring(
                    start,
                    end - start + 1);
                TemplateExpressionPlan segmentExpressionPlan =
                    GetTemplateExpressionPlan(segment);
                BindingExpressionPlan segmentPlan =
                    segmentExpressionPlan.Kind ==
                        TemplateExpressionKind.Binding
                            ? segmentExpressionPlan.BindingPlan
                            : null;

                if (segmentPlan != null)
                {
                    if (segmentPlan.Mode == BindingMode.TwoWay)
                    {
                        throw new InvalidOperationException(
                            "Mode=TwoWay requires one complete Binding expression; " +
                            "it cannot be used inside interpolated text.");
                    }

                    BindingPathResult segmentResult =
                        ResolveBindingExpressionResult(
                            ResolveBindingSource(
                                dataContext,
                                segmentPlan),
                            segmentPlan);

                    if (segmentResult.Dependencies.Count > 0)
                    {
                        if (aggregate == null)
                            aggregate = new BindingPathResult();

                        MergeBindingPathDependencies(
                            aggregate,
                            segmentResult,
                            aggregateSources);
                    }
                }

                searchFrom = end + 1;
            }

            BindingPathResult functionDependencies =
                ResolveFunctionExpressionDependencies(
                    expression,
                    dataContext);

            if (functionDependencies != null &&
                functionDependencies.Dependencies.Count > 0)
            {
                if (aggregate == null)
                    aggregate = new BindingPathResult();

                MergeBindingPathDependencies(
                    aggregate,
                    functionDependencies,
                    aggregateSources);
            }

            return MergeReactivePresetDependencies(
                expression,
                aggregate,
                aggregateSources);
        }

        private static void MergeBindingPathDependencies(
            BindingPathResult target,
            BindingPathResult source,
            BindingDependencySourceIndex sourceIndex)
        {
            if (target == null || source == null)
                return;

            sourceIndex = EnsureBindingDependencySourceIndex(
                target,
                sourceIndex);

            int i;

            for (i = 0; i < source.Dependencies.Count; i++)
            {
                BindingPathDependency candidate =
                    source.Dependencies[i] as BindingPathDependency;

                if (candidate == null)
                    continue;

                if (sourceIndex.Add(candidate))
                    target.Dependencies.Add(candidate);
            }
        }

        private static BindingDependencySourceIndex
            EnsureBindingDependencySourceIndex(
                BindingPathResult result,
                BindingDependencySourceIndex candidateIndex)
        {
            if (result == null)
            {
                return candidateIndex == null
                    ? new BindingDependencySourceIndex()
                    : candidateIndex;
            }

            if (result.DependencySourceIndex != null)
                return result.DependencySourceIndex;

            BindingDependencySourceIndex index =
                candidateIndex == null
                    ? new BindingDependencySourceIndex()
                    : candidateIndex;
            int i;

            for (i = 0; i < result.Dependencies.Count; i++)
            {
                index.Add(
                    result.Dependencies[i] as BindingPathDependency);
            }

            result.DependencySourceIndex = index;
            return index;
        }

        private static BindingDependencySourceIndex
            CreateBindingDependencySourceIndex(
                BindingPathResult result)
        {
            return EnsureBindingDependencySourceIndex(
                result,
                null);
        }

        private static void ValidateTwoWayDynamicBinding(
            DynamicPropertyBinding binding,
            BindingExpressionPlan plan,
            BindingPathResult pathResult)
        {
            if (plan.HasComputedExpression)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay cannot be combined with a computed " +
                    "Binding expression.");
            }

            if (plan.HasNegation)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay cannot be combined with the ! binding operator.");
            }

            if (binding.StyleSetter ||
                IsResourceStyleProperty(
                    binding.Target,
                    binding.PropertyName))
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by Style bindings. " +
                    "Bind the target's local property instead.");
            }

            if (EqualsIgnoreCase(binding.PropertyName, "Condition"))
            {
                throw new InvalidOperationException(
                    "Condition is structural and supports only OneWay bindings.");
            }

            if (binding.PropertyName.IndexOf('.') >= 0)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by attached properties.");
            }

            if (EqualsIgnoreCase(binding.PropertyName, "ItemsSource"))
            {
                throw new InvalidOperationException(
                    "ItemsSource is one-way. Modify the observable list or " +
                    "replace the source PropertyBinding value instead.");
            }

            if (pathResult == null ||
                pathResult.TerminalDependency == null)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay requires the Binding path to end in a " +
                    "writable PropertyBinding<T> or notifying CLR property.");
            }
        }

        private void OnDynamicObservableBindingChanged(
            object owner,
            long revision)
        {
            DynamicPropertyBinding binding =
                owner as DynamicPropertyBinding;

            if (binding == null ||
                !binding.Active ||
                _dynamicFeaturesDisposed)
            {
                return;
            }

            if (IsDisposedTarget(binding.Target))
            {
                ReleaseDynamicBindings(binding.Target);
                return;
            }

            ReloadDynamicBindings(
                binding.Target,
                binding.PropertyName,
                false,
                null);
        }

    }
}
