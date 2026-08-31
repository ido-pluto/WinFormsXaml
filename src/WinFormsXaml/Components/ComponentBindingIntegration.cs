using System;
using System.Collections;
using System.Xml;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private ArrayList ReloadComponentPropertyValues(
            object target,
            string propertyName,
            bool presetsOnly,
            PresetChangedEventArgs presetChange,
            out ArrayList refreshedStates)
        {
            ArrayList changedRoots = new ArrayList();
            refreshedStates = new ArrayList();

            if (_componentInstances == null)
                return changedRoots;

            bool cascadePropertyReload =
                target != null &&
                !String.IsNullOrEmpty(propertyName) &&
                ComponentDeclaresProperty(target, propertyName);
            int i;

            for (i = _componentInstances.Count - 1; i >= 0; i--)
            {
                ComponentInstanceState state =
                    _componentInstances[i] as ComponentInstanceState;

                if (state == null)
                {
                    RemoveComponentInstanceAt(i);
                    continue;
                }

                // A rootless state is retained cleanup debt from a failed
                // construction/virtual condition. It participates only in
                // ReleaseAll retries, never in live binding refreshes.
                if (state.Root == null)
                    continue;

                if (IsDisposedTarget(state.Root))
                {
                    ReleaseComponentInstanceState(state);
                    continue;
                }

                bool sameTargetRoot =
                    target != null &&
                    Object.ReferenceEquals(state.Root, target);
                bool exactTarget = sameTargetRoot;

                if (exactTarget &&
                    !String.IsNullOrEmpty(propertyName))
                {
                    exactTarget = StateDeclaresComponentProperty(
                        state,
                        propertyName);
                }

                if (target != null &&
                    !exactTarget &&
                    (!String.IsNullOrEmpty(propertyName) &&
                     !cascadePropertyReload ||
                     !IsComponentInsideTarget(state.Root, target)))
                {
                    continue;
                }

                bool componentPropertyRequested = false;
                bool proxyValueChanged = false;
                int n;

                for (n = 0; n < state.Properties.Count; n++)
                {
                    ComponentPropertyValue property =
                        state.Properties[n] as ComponentPropertyValue;

                    if (property == null)
                        continue;

                    if (exactTarget &&
                        !String.IsNullOrEmpty(propertyName) &&
                        !EqualsIgnoreCase(
                            property.Definition.Name,
                            propertyName))
                    {
                        continue;
                    }

                    if (exactTarget &&
                        !String.IsNullOrEmpty(propertyName))
                    {
                        componentPropertyRequested = true;
                    }

                    if (!property.Dynamic)
                        continue;

                    if (presetsOnly &&
                        !ExpressionDependsOnPreset(
                            property.Expression,
                            presetChange) &&
                        !IsInsideChangedComponent(
                            state.Root,
                            changedRoots))
                    {
                        continue;
                    }

                    ReplaceComponentObservableBinding(
                        state,
                        property);

                    object nextValue =
                        ConvertComponentPropertyValueForState(
                            state,
                            property);

                    if (SetComponentPropertyProxyValue(
                            property,
                            nextValue))
                    {
                        proxyValueChanged = true;
                    }

                }

                bool changed =
                    state.PendingBindingRefresh ||
                    componentPropertyRequested ||
                    proxyValueChanged;

                if (!changed)
                    continue;

                state.PendingBindingRefresh = true;
                refreshedStates.Add(state);
                changedRoots.Add(state.Root);
            }

            return changedRoots;
        }

        private bool SetComponentPropertyProxyValue(
            ComponentPropertyValue property,
            object value)
        {
            if (property == null ||
                property.ValueProxy == null)
            {
                throw new InvalidOperationException(
                    "A registered component property is missing its observable " +
                    "value proxy.");
            }

            IPropertyBindingRuntime runtimeBinding =
                property.ValueProxy as IPropertyBindingRuntime;
            long ignoredVersion;

            if (runtimeBinding == null)
            {
                throw new InvalidOperationException(
                    "Registered component property '" +
                    property.Definition.Name +
                    "' has an invalid observable value proxy.");
            }

            object previous =
                GetPropertyBindingSnapshot(
                    runtimeBinding,
                    out ignoredVersion);

            if (property.Mode == BindingMode.TwoWay &&
                (property.ObservableRegistration == null ||
                 property.ObservableRegistration.TargetRuntimeBinding == null ||
                 !Object.ReferenceEquals(
                     property.ObservableRegistration.TargetRuntimeBinding,
                     runtimeBinding)))
            {
                throw new InvalidOperationException(
                    "Value for registered component property '" +
                    property.Definition.Name +
                    "' has no active two-way target proxy.");
            }

            bool synchronizePlainMember =
                property.CodeBehind != null &&
                property.CodeMember != null &&
                !property.CodeMember.UsesBindingProxy;
            object previousMemberValue = null;

            if (synchronizePlainMember)
            {
                object convertedMemberValue;

                if (!TryConvertObjectValue(
                        value,
                        property.CodeMember.MemberType,
                        out convertedMemberValue))
                {
                    throw new InvalidOperationException(
                        "Updated value for component property '" +
                        property.Definition.Name +
                        "' cannot be assigned to Component Class member type " +
                        property.CodeMember.MemberType.FullName +
                        ".");
                }

                previousMemberValue =
                    GetComponentCodeMemberValue(
                        property.CodeMember,
                        property.CodeBehind);

                // Publish the plain member first. PropertyBinding<T> commits its
                // value before notifying listeners, so synchronous template
                // refreshes can now observe one coherent new component value.
                SetComponentCodeMemberValue(
                    property.CodeMember,
                    property.CodeBehind,
                    convertedMemberValue);
            }

            bool applied;

            if (property.Mode == BindingMode.TwoWay)
            {
                applied =
                    property.ObservableRegistration != null &&
                    TrySetObservableTargetValue(
                        property.ObservableRegistration,
                        value);
            }
            else
            {
                runtimeBinding.SetValue(value);
                applied = true;
            }

            if (!applied)
            {
                if (synchronizePlainMember)
                {
                    SetComponentCodeMemberValue(
                        property.CodeMember,
                        property.CodeBehind,
                        previousMemberValue);
                }

                throw new InvalidOperationException(
                    "Value for registered component property '" +
                    property.Definition.Name +
                    "' could not be applied to its declared type " +
                    property.Definition.Type.FullName +
                    ".");
            }

            object current =
                GetPropertyBindingSnapshot(
                    runtimeBinding,
                    out ignoredVersion);

            if (!synchronizePlainMember)
            {
                SynchronizeComponentCodeBehindMember(
                    property,
                    current);
            }

            return !Object.Equals(previous, current);
        }

        private void SynchronizeComponentCodeBehindMember(
            ComponentPropertyValue property,
            object value)
        {
            if (property == null ||
                property.CodeBehind == null ||
                property.CodeMember == null ||
                property.CodeMember.UsesBindingProxy)
            {
                return;
            }

            object converted;

            if (!TryConvertObjectValue(
                    value,
                    property.CodeMember.MemberType,
                    out converted))
            {
                throw new InvalidOperationException(
                    "Updated value for component property '" +
                    property.Definition.Name +
                    "' cannot be assigned to Component Class member type " +
                    property.CodeMember.MemberType.FullName +
                    ".");
            }

            SetComponentCodeMemberValue(
                property.CodeMember,
                property.CodeBehind,
                converted);
        }

        private bool ComponentDeclaresProperty(
            object target,
            string propertyName)
        {
            if (_componentInstancesByRoot == null ||
                target == null ||
                String.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            ComponentInstanceState state =
                _componentInstancesByRoot[target] as ComponentInstanceState;

            return StateDeclaresComponentProperty(
                state,
                propertyName);
        }

        private static bool StateDeclaresComponentProperty(
            ComponentInstanceState state,
            string propertyName)
        {
            if (state == null || String.IsNullOrEmpty(propertyName))
                return false;

            int i;

            for (i = 0; i < state.Properties.Count; i++)
            {
                ComponentPropertyValue property =
                    state.Properties[i] as ComponentPropertyValue;

                if (property != null &&
                    EqualsIgnoreCase(
                        property.Definition.Name,
                        propertyName))
                {
                    return true;
                }
            }

            return false;
        }

        private void CompleteComponentBindingRefresh(
            ArrayList refreshedStates)
        {
            if (refreshedStates == null || refreshedStates.Count == 0)
            {
                return;
            }

            int i;

            for (i = 0; i < refreshedStates.Count; i++)
            {
                ComponentInstanceState state =
                    refreshedStates[i] as ComponentInstanceState;

                if (state != null)
                    state.PendingBindingRefresh = false;
            }
        }

        private void ActivateComponentObservableBindings(
            ComponentInstanceState state,
            XmlElement invocation)
        {
            if (state == null || state.Properties == null)
                return;

            int i;

            for (i = 0; i < state.Properties.Count; i++)
            {
                ComponentPropertyValue property =
                    state.Properties[i] as ComponentPropertyValue;

                if (property != null)
                {
                    try
                    {
                        ReplaceComponentObservableBinding(
                            state,
                            property);
                    }
                    catch (WinFormsXamlLoadException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw CreateMarkupLoadException(
                            invocation,
                            property.Definition.Name,
                            ex);
                    }
                }
            }
        }

        private void ReplaceComponentObservableBinding(
            ComponentInstanceState state,
            ComponentPropertyValue property)
        {
            object previousEventTarget = _activeComponentEventTarget;

            try
            {
                if (state != null)
                {
                    _activeComponentEventTarget =
                        state.ParentEventTarget;
                }

                ReplaceComponentObservableBindingCore(
                    state,
                    property);
            }
            finally
            {
                _activeComponentEventTarget = previousEventTarget;
            }
        }

        private void ReplaceComponentObservableBindingCore(
            ComponentInstanceState state,
            ComponentPropertyValue property)
        {
            if (property == null)
                return;

            if (!property.Dynamic ||
                String.IsNullOrEmpty(property.Expression))
            {
                DetachComponentObservableBinding(property);
                return;
            }

            BindingExpressionPlan directPlan;
            BindingPathResult pathResult;

            if (property.HasInitialObservableSnapshot)
            {
                directPlan = property.InitialDirectPlan;
                pathResult = property.InitialPathResult;
                property.HasInitialObservableSnapshot = false;
                property.InitialDirectPlan = null;
                property.InitialPathResult = null;
            }
            else
            {
                pathResult = ResolveObservableExpressionDependencies(
                    property.Expression,
                    state == null
                        ? null
                        : state.ParentDataContext,
                    out directPlan);
            }

            if (property.Mode == BindingMode.TwoWay)
            {
                ValidateComponentTwoWayBinding(
                    property,
                    directPlan,
                    pathResult);
            }

            if (pathResult == null ||
                pathResult.Dependencies.Count == 0)
            {
                DetachComponentObservableBinding(property);
                return;
            }

            if (property.ObservableRegistration != null)
            {
                UpdateObservableBinding(
                    property.ObservableRegistration,
                    pathResult);
                return;
            }

            property.OwnerState = state;
            property.ObservableRegistration =
                AttachObservableBinding(
                    property,
                    property.ValueProxy,
                    "Value",
                    property.Mode,
                    directPlan == null
                        ? BindingUpdateSourceTrigger.PropertyChanged
                        : directPlan.UpdateSourceTrigger,
                    pathResult,
                    OnComponentObservableBindingChanged);
        }

        private void DetachComponentObservableBinding(
            ComponentPropertyValue property)
        {
            if (property == null)
                return;

            ObservableBindingRegistration registration =
                property.ObservableRegistration;
            property.ObservableRegistration = null;

            if (registration != null)
                DetachObservableBinding(registration);
        }

        private void DetachComponentObservableBindings(
            ComponentInstanceState state)
        {
            if (state == null || state.Properties == null)
                return;

            int i;

            for (i = 0; i < state.Properties.Count; i++)
            {
                ComponentPropertyValue property =
                    state.Properties[i] as ComponentPropertyValue;

                if (property != null)
                {
                    DetachComponentObservableBinding(property);
                    property.OwnerState = null;
                }
            }
        }
    }
}
