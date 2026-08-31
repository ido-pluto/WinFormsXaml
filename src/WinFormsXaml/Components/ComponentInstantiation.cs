using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private object GetCurrentBuildDataContext()
        {
            if (_componentContentProjectionDepth != 0)
                return _activeComponentDataContext;

            if (_activeComponentDataContext != null)
                return _activeComponentDataContext;

            if (_templateBuildDepth != 0 &&
                _activeTemplateDataContext != null)
            {
                return _activeTemplateDataContext;
            }

            return _eventTarget;
        }

        private object BuildRegisteredXmlComponent(
            XmlElement invocation,
            RegisteredComponent component)
        {
            bool projectedInvocation =
                GetComponentContentProjection(invocation) != null;

            if (!projectedInvocation &&
                _activeXmlComponentBuildChain != null &&
                ContainsReference(
                    _activeXmlComponentBuildChain,
                    component))
            {
                throw new InvalidOperationException(
                    "Registered XML components contain a circular visual-root " +
                    "component chain involving '" + component.Name + "'.");
            }

            if (_activeXmlComponentBuildChain == null)
                _activeXmlComponentBuildChain = new ArrayList();

            _activeXmlComponentBuildChain.Add(component);

            try
            {
                return BuildRegisteredXmlComponentCore(
                    invocation,
                    component);
            }
            finally
            {
                int last = _activeXmlComponentBuildChain.Count - 1;

                if (last >= 0 &&
                    Object.ReferenceEquals(
                        _activeXmlComponentBuildChain[last],
                        component))
                {
                    _activeXmlComponentBuildChain.RemoveAt(last);
                }
                else
                {
                    int i;

                    for (i = last; i >= 0; i--)
                    {
                        if (Object.ReferenceEquals(
                                _activeXmlComponentBuildChain[i],
                                component))
                        {
                            _activeXmlComponentBuildChain.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private object BuildRegisteredXmlComponentCore(
            XmlElement invocation,
            RegisteredComponent component)
        {
            ValidateOneWayOnlyElementBindings(invocation);

            ArrayList invocationChildren =
                GetComponentInvocationContent(
                    invocation,
                    component);

            if (invocationChildren.Count != 0 &&
                !component.HasChildrenSlot)
            {
                throw CreateMarkupLoadException(
                    invocationChildren[0] as XmlElement,
                    null,
                    new InvalidOperationException(
                        "Registered XML component <" +
                        component.Name +
                        "> does not declare a <Children> slot."));
            }

            object parentDataContext = GetCurrentBuildDataContext();
            ArrayList includeConditionAttributes =
                GetConditionalIncludeAttributes(invocation);
            bool dynamicIncludeCondition;

            try
            {
                if (!EvaluateConditionalIncludeConditions(
                        includeConditionAttributes,
                        parentDataContext,
                        out dynamicIncludeCondition))
                {
                    return null;
                }
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    invocation,
                    "Condition",
                    ex);
            }

            string conditionExpression =
                GetAttributeIgnoreNamespace(
                    invocation,
                    "Condition");
            bool dynamicCondition = false;

            if (!String.IsNullOrEmpty(conditionExpression))
            {
                try
                {
                    dynamicCondition =
                        ContainsDynamicExpression(conditionExpression);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        invocation,
                        "Condition",
                        ex);
                }
            }

            bool conditionMatches = true;

            if (!dynamicCondition)
            {
                try
                {
                    conditionMatches =
                        EvaluateComponentCondition(
                            invocation,
                            parentDataContext);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        invocation,
                        "Condition",
                        ex);
                }
            }

            if (!conditionMatches)
                return null;

            XmlAttribute[] suppliedPropertyAttributes =
                ValidateComponentInvocationAttributes(
                    invocation,
                    component);
            ArrayList attachedBindings =
                PrepareComponentAttachedBindings(
                    invocation,
                    parentDataContext);
            attachedBindings = CaptureConditionalIncludeBindings(
                invocation,
                includeConditionAttributes,
                parentDataContext,
                attachedBindings);

            object codeBehind =
                CreateComponentCodeBehind(
                    component,
                    invocation);
            object parentEventTarget =
                GetComponentEventTarget(parentDataContext);
            object componentEventTarget = codeBehind == null
                ? parentEventTarget
                : codeBehind;
            ComponentInstanceState state =
                CreateComponentInstanceState(
                    invocation,
                    component,
                    parentDataContext,
                    parentEventTarget,
                    componentEventTarget,
                    codeBehind,
                    suppliedPropertyAttributes);

            object previousDataContext = _activeComponentDataContext;
            object previousEventTarget = _activeComponentEventTarget;
            int previousDepth = _componentBuildDepth;
            Dictionary<string, StyleDefinition> previousNamedStyles =
                _activeComponentNamedStyles;
            List<StyleDefinition> previousImplicitStyles =
                _activeComponentImplicitStyles;
            string previousMarkupSource = _activeMarkupSource;
            string previousElementPathPrefix =
                _activeMarkupElementPathPrefix;
            Assembly previousMarkupAssembly =
                _activeMarkupAssembly;
            string componentInvocationPath =
                GetMarkupElementPath(
                    invocation,
                    previousElementPathPrefix,
                    _activeComponentContentRoot);
            Dictionary<string, StyleDefinition> inheritedNamedStyles =
                GetCurrentNamedStyles();
            List<StyleDefinition> inheritedImplicitStyles =
                GetCurrentImplicitStyles();
            bool registerName =
                _templateBuildDepth == 0 &&
                previousDepth == 0;
            ArrayList projectedContentRoots = new ArrayList();
            object root = null;

            try
            {
                XmlDocument templateDocument =
                    CloneRegisteredComponentTemplateDocument(component);

                if (component.HasChildrenSlot)
                {
                    projectedContentRoots =
                        ApplyComponentChildrenProjection(
                            templateDocument,
                            invocationChildren,
                            component,
                            state.ChildrenHost,
                            parentDataContext,
                            parentEventTarget,
                            previousMarkupSource,
                            componentInvocationPath,
                            previousMarkupAssembly,
                            previousDepth);
                }

                _activeComponentDataContext = state.Values;
                _activeComponentEventTarget = componentEventTarget;
                _componentBuildDepth = previousDepth + 1;
                _activeComponentNamedStyles =
                    new Dictionary<string, StyleDefinition>(
                        inheritedNamedStyles,
                        StringComparer.OrdinalIgnoreCase);
                _activeComponentImplicitStyles =
                    new List<StyleDefinition>(inheritedImplicitStyles);
                _activeMarkupSource =
                    String.IsNullOrEmpty(component.ResourceName)
                        ? "registered component " + component.Name
                        : component.ResourceName;
                _activeMarkupAssembly =
                    component.ResourceAssembly == null
                        ? previousMarkupAssembly
                        : component.ResourceAssembly;
                _activeMarkupElementPathPrefix =
                    componentInvocationPath +
                    " -> component " + component.Name;
                root = BuildElement(templateDocument.DocumentElement);
            }
            catch
            {
                RollbackComponentBuild(state, root);
                throw;
            }
            finally
            {
                _componentBuildDepth = previousDepth;
                _activeComponentDataContext = previousDataContext;
                _activeComponentEventTarget = previousEventTarget;
                _activeComponentNamedStyles = previousNamedStyles;
                _activeComponentImplicitStyles = previousImplicitStyles;
                _activeMarkupSource = previousMarkupSource;
                _activeMarkupAssembly = previousMarkupAssembly;
                _activeMarkupElementPathPrefix =
                    previousElementPathPrefix;

                int projectedIndex;

                for (projectedIndex = 0;
                     projectedIndex < projectedContentRoots.Count;
                     projectedIndex++)
                {
                    UnregisterComponentContentProjection(
                        projectedContentRoots[projectedIndex] as XmlElement);
                }

                UnregisterComponentChildrenSlot(
                    state.ChildrenHost);
            }

            Control rootControl = root as Control;

            if (rootControl == null)
            {
                RollbackComponentBuild(state, root);

                throw new InvalidOperationException(
                    "Registered XML component <" +
                    component.Name +
                    "> must produce one WinForms Control root.");
            }

            if (component.HasChildrenSlot &&
                !state.ChildrenHost.Attached)
            {
                RollbackComponentBuild(state, root);
                throw new InvalidOperationException(
                    "Registered XML component <" +
                    component.Name +
                    "> did not attach its validated <Children> slot to a " +
                    "Windows Forms Control parent.");
            }

            state.Root = root;
            HookComponentInstanceDisposal(state);

            TrackComponentInstanceState(state);

            try
            {
                ActivateComponentObservableBindings(
                    state,
                    invocation);
                RegisterDynamicBindings(
                    root,
                    attachedBindings,
                    invocation);

                try
                {
                    ApplyRetainedDynamicCondition(
                        root,
                        attachedBindings);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        invocation,
                        "Condition",
                        ex);
                }

                string declaredName = GetDeclaredName(invocation);

                if (!String.IsNullOrEmpty(declaredName))
                {
                    SetNativeName(root, declaredName);

                    if (registerName)
                        RegisterName(declaredName, root);
                }
            }
            catch
            {
                RollbackComponentBuild(state, root);
                throw;
            }

            return root;
        }

        private void RollbackComponentBuild(
            ComponentInstanceState state,
            object root)
        {
            // Native-tree cleanup must continue even when user IDisposable
            // code fails. ReleaseComponentInstanceState retains unsuccessful
            // state cleanup in the runtime retry list.
            if (root != null)
            {
                try
                {
                    ReleaseCreatedElement(root);
                }
                catch
                {
                }
            }

            try
            {
                ReleaseComponentInstanceState(state);
            }
            catch
            {
            }
        }

        private ArrayList PrepareComponentAttachedBindings(
            XmlElement invocation,
            object dataContext)
        {
            ArrayList bindings = null;
            int i;

            for (i = 0; i < invocation.Attributes.Count; i++)
            {
                XmlAttribute attribute = invocation.Attributes[i];

                bool condition = EqualsIgnoreCase(
                    attribute.LocalName,
                    "Condition");

                if (ShouldIgnoreAttribute(attribute) ||
                    (!condition && attribute.LocalName.IndexOf('.') < 0))
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
                        invocation,
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

                    if (!condition)
                    {
                        object resolved = ResolveComponentValue(
                            attribute.Value,
                            dataContext);
                        attribute.Value = BindingValueToString(resolved);
                    }
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        invocation,
                        attribute.LocalName,
                        ex);
                }
            }

            return bindings;
        }


        private bool EvaluateComponentCondition(
            XmlElement invocation,
            object dataContext)
        {
            string expression =
                GetAttributeIgnoreNamespace(
                    invocation,
                    "Condition");

            if (String.IsNullOrEmpty(expression))
                return true;

            BindingExpressionPlan bindingPlan;

            if (TryParseBindingExpression(
                    expression,
                    out bindingPlan) &&
                bindingPlan.Mode == BindingMode.TwoWay)
            {
                throw new InvalidOperationException(
                    "Condition is structural and supports only OneWay bindings.");
            }

            object value = ResolveComponentValue(expression, dataContext);
            object converted;

            if (!TryConvertObjectValue(value, typeof(bool), out converted))
            {
                throw new InvalidOperationException(
                    "Condition on registered component <" +
                    invocation.LocalName +
                    "> must be boolean-compatible.");
            }

            return (bool)converted;
        }

        private XmlAttribute[] ValidateComponentInvocationAttributes(
            XmlElement invocation,
            RegisteredComponent component)
        {
            return IndexComponentPropertyAttributes(
                invocation,
                component,
                true);
        }

        private XmlAttribute[] IndexComponentPropertyAttributes(
            XmlElement invocation,
            RegisteredComponent component,
            bool validate)
        {
            XmlAttribute[] suppliedProperties = null;
            int i;

            for (i = 0; i < invocation.Attributes.Count; i++)
            {
                XmlAttribute attribute = invocation.Attributes[i];
                ComponentPropertyDefinition definition =
                    FindComponentProperty(
                        component,
                        attribute.LocalName);

                // FindAttributeIgnoreNamespace historically selected the first
                // local-name match even when its namespace was otherwise ignored.
                // Preserve that behavior while indexing all supplied values in
                // this single pass.
                if (definition != null)
                {
                    if (suppliedProperties == null)
                    {
                        suppliedProperties =
                            new XmlAttribute[component.Properties.Length];
                    }

                    if (suppliedProperties[definition.Index] == null)
                    {
                        suppliedProperties[definition.Index] =
                            attribute;
                    }
                }

                if (!validate ||
                    ShouldIgnoreAttribute(attribute) ||
                    EqualsIgnoreCase(attribute.LocalName, "Name") ||
                    EqualsIgnoreCase(attribute.LocalName, "Condition") ||
                    attribute.LocalName.IndexOf('.') >= 0)
                {
                    continue;
                }

                if (definition == null)
                {
                    InvalidOperationException failure =
                        new InvalidOperationException(
                            "Registered XML component <" +
                            component.Name +
                            "> does not declare property '" +
                            attribute.LocalName +
                            "'.");

                    throw CreateMarkupLoadException(
                        invocation,
                        attribute.LocalName,
                        failure);
                }
            }

            return suppliedProperties;
        }

        private object CreateComponentCodeBehind(
            RegisteredComponent component,
            XmlElement invocation)
        {
            if (component == null || component.CodeBehindType == null)
                return null;

            try
            {
                ConstructorInfo constructor =
                    component.CodeBehindConstructor;

                if (constructor == null)
                {
                    throw new InvalidOperationException(
                        "Component Class type '" +
                        component.CodeBehindType.FullName +
                        "' needs a public parameterless constructor.");
                }

                return constructor.Invoke(_emptyObjectArray);
            }
            catch (Exception ex)
            {
                TargetInvocationException invocationFailure =
                    ex as TargetInvocationException;
                Exception detail =
                    invocationFailure != null &&
                    invocationFailure.InnerException != null
                        ? invocationFailure.InnerException
                        : ex;

                throw CreateMarkupLoadException(
                    invocation,
                    null,
                    new InvalidOperationException(
                        "Could not create Component Class '" +
                        component.CodeBehindType.FullName +
                        "' for <" +
                        component.Name +
                        ">: " +
                        detail.Message,
                        detail));
            }
        }

        private ComponentInstanceState CreateComponentInstanceState(
            XmlElement invocation,
            RegisteredComponent component,
            object parentDataContext,
            object parentEventTarget,
            object componentEventTarget,
            object codeBehind,
            XmlAttribute[] suppliedPropertyAttributes)
        {
            ComponentInstanceState state = new ComponentInstanceState();
            state.InstanceIndex = -1;
            state.ParentDataContext = parentDataContext;
            state.ParentEventTarget = parentEventTarget;
            state.CodeBehind = codeBehind;

            try
            {
                state.Values = new ComponentValueContext();
                state.Values.CodeBehind = componentEventTarget;
                state.Values.MayUsePresets =
                    ComponentDataContextMayUsePresets(parentDataContext);
                state.Properties = new ArrayList();
                if (component.HasChildrenSlot)
                {
                    state.Children =
                        ResolveComponentChildrenBind(
                            codeBehind,
                            component);
                    state.ChildrenHost = new ComponentChildrenHost();
                    state.ChildrenHost.Runtime = this;
                    state.ChildrenHost.State = state;
                }

                int i;

                for (i = 0; i < component.Properties.Length; i++)
                {
                    ComponentPropertyDefinition definition =
                        component.Properties[i];

                    try
                    {
                        AddComponentInstanceProperty(
                            state,
                            invocation,
                            component,
                            parentDataContext,
                            definition,
                            suppliedPropertyAttributes == null
                                ? null
                                : suppliedPropertyAttributes[i]);
                    }
                    catch (WinFormsXamlLoadException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw CreateMarkupLoadException(
                            invocation,
                            definition.Name,
                            ex);
                    }
                }
            }
            catch
            {
                try
                {
                    ReleaseComponentInstanceState(state);
                }
                catch
                {
                }

                throw;
            }

            return state;
        }

        private static ChildrenBind ResolveComponentChildrenBind(
            object codeBehind,
            RegisteredComponent component)
        {
            if (codeBehind == null)
                return new ChildrenBind();

            ComponentCodeMember member =
                component == null
                    ? null
                    : component.ChildrenMember;

            if (member == null)
                return new ChildrenBind();

            if (member.MemberType != typeof(ChildrenBind))
            {
                throw new InvalidOperationException(
                    "Public Component Class member 'Children' on " +
                    codeBehind.GetType().FullName +
                    " must have type " +
                    typeof(ChildrenBind).FullName +
                    ".");
            }

            ChildrenBind children =
                GetComponentCodeMemberValue(member, codeBehind) as
                    ChildrenBind;

            if (children != null)
                return children;

            if (!CanWriteComponentCodeMember(member))
            {
                throw new InvalidOperationException(
                    "Public Component Class member 'Children' on " +
                    codeBehind.GetType().FullName +
                    " is null and cannot be assigned. Initialize the readonly " +
                    "ChildrenBind or expose a public setter.");
            }

            children = new ChildrenBind();
            SetComponentCodeMemberValue(
                member,
                codeBehind,
                children);
            return children;
        }

        private static ComponentCodeMember FindPublicComponentCodeMember(
            Type type,
            string name)
        {
            ArrayList exact = new ArrayList();
            ArrayList insensitive = new ArrayList();
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public);
            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Instance |
                BindingFlags.Public);
            int i;

            for (i = 0; i < fields.Length; i++)
            {
                ComponentCodeMember member = new ComponentCodeMember();
                member.Field = fields[i];
                AddComponentCodeMemberMatch(
                    member,
                    fields[i].Name,
                    name,
                    exact,
                    insensitive);
            }

            for (i = 0; i < properties.Length; i++)
            {
                if (properties[i].GetIndexParameters().Length != 0)
                    continue;

                ComponentCodeMember member = new ComponentCodeMember();
                member.Property = properties[i];
                AddComponentCodeMemberMatch(
                    member,
                    properties[i].Name,
                    name,
                    exact,
                    insensitive);
            }

            ArrayList matches = exact.Count == 0
                ? insensitive
                : exact;

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    "Public Component Class member '" +
                    name +
                    "' is ambiguous on " +
                    type.FullName +
                    ". Use one exact public instance field or property.");
            }

            return matches.Count == 0
                ? null
                : matches[0] as ComponentCodeMember;
        }

        private static void AddComponentCodeMemberMatch(
            ComponentCodeMember member,
            string candidateName,
            string requestedName,
            ArrayList exact,
            ArrayList insensitive)
        {
            if (String.Equals(
                    candidateName,
                    requestedName,
                    StringComparison.Ordinal))
            {
                exact.Add(member);
            }
            else if (String.Equals(
                candidateName,
                requestedName,
                StringComparison.OrdinalIgnoreCase))
            {
                insensitive.Add(member);
            }
        }

        private static bool CanReadComponentCodeMember(
            ComponentCodeMember member)
        {
            return member != null &&
                (member.Field != null ||
                 member.Property.GetGetMethod() != null);
        }

        private static bool CanWriteComponentCodeMember(
            ComponentCodeMember member)
        {
            return member != null &&
                (member.Field != null
                    ? !member.Field.IsInitOnly && !member.Field.IsLiteral
                    : member.Property.GetSetMethod() != null);
        }

        private static object GetComponentCodeMemberValue(
            ComponentCodeMember member,
            object target)
        {
            if (!CanReadComponentCodeMember(member))
            {
                throw new InvalidOperationException(
                    "Component Class member '" +
                    (member.Field == null
                        ? member.Property.Name
                        : member.Field.Name) +
                    "' needs a public getter.");
            }

            return member.Field == null
                ? member.Property.GetValue(target, null)
                : member.Field.GetValue(target);
        }

        private static void SetComponentCodeMemberValue(
            ComponentCodeMember member,
            object target,
            object value)
        {
            if (!CanWriteComponentCodeMember(member))
            {
                throw new InvalidOperationException(
                    "Component Class member '" +
                    (member.Field == null
                        ? member.Property.Name
                        : member.Field.Name) +
                    "' needs a public setter or a non-readonly public field.");
            }

            if (member.Field == null)
                member.Property.SetValue(target, value, null);
            else
                member.Field.SetValue(target, value);
        }

        private void AddComponentInstanceProperty(
            ComponentInstanceState state,
            XmlElement invocation,
            RegisteredComponent component,
            object parentDataContext,
            ComponentPropertyDefinition definition,
            XmlAttribute supplied)
        {
            string expression;

            if (supplied != null)
            {
                expression = supplied.Value;
            }
            else if (definition.HasDefaultValue)
            {
                expression = definition.DefaultValue;
            }
            else if (definition.Required)
            {
                throw new InvalidOperationException(
                    "Registered XML component <" +
                    component.Name +
                    "> requires property '" +
                    definition.Name +
                    "'.");
            }
            else
            {
                expression = null;
            }

            ComponentPropertyValue property =
                new ComponentPropertyValue();
            property.Definition = definition;
            property.ComponentName = component.Name;
            property.Expression = expression;
            property.Dynamic =
                supplied != null &&
                ContainsDynamicExpression(expression);

            if (property.Dynamic &&
                ContainsPresetExpression(expression))
            {
                state.Values.MayUsePresets = true;
            }
            property.Mode = BindingMode.OneWay;
            property.OwnerState = state;

            if (property.Dynamic)
            {
                BindingExpressionPlan initialDirectPlan;

                property.InitialPathResult =
                    ResolveObservableExpressionDependencies(
                        expression,
                        parentDataContext,
                        out initialDirectPlan);
                property.InitialDirectPlan = initialDirectPlan;
                property.HasInitialObservableSnapshot = true;

                if (initialDirectPlan != null)
                    property.Mode = initialDirectPlan.Mode;
            }

            object value =
                ConvertComponentPropertyValue(
                    definition,
                    expression,
                    supplied != null,
                    parentDataContext,
                    component.Name);

            property.CodeBehind = state.CodeBehind;

            if (component.CodeBehindPropertyMembers != null)
            {
                ComponentCodeMember codeMember;

                if (component.CodeBehindPropertyMembers.TryGetValue(
                        definition.Name,
                        out codeMember))
                {
                    property.CodeMember = codeMember;
                }
            }

            property.ValueProxy =
                BindComponentCodeBehindProperty(
                    property,
                    value);

            if (property.Mode == BindingMode.TwoWay)
            {
                ValidateComponentTwoWayBinding(
                    property,
                    property.InitialDirectPlan,
                    property.InitialPathResult);

                IPropertyBindingRuntime targetRuntimeBinding =
                    property.ValueProxy as IPropertyBindingRuntime;

                ValidateObservableTwoWayEndpoints(
                    property.ValueProxy,
                    "Value",
                    _missingObservableTargetProperty,
                    targetRuntimeBinding,
                    property.InitialDirectPlan.UpdateSourceTrigger,
                    property.InitialPathResult);
            }

            state.Values[definition.Name] = property.ValueProxy;
            state.Properties.Add(property);
        }

        private static bool ComponentDataContextMayUsePresets(
            object dataContext)
        {
            ComponentValueContext componentContext =
                dataContext as ComponentValueContext;

            return componentContext != null &&
                componentContext.MayUsePresets;
        }

        private static void ValidateComponentTwoWayBinding(
            ComponentPropertyValue property,
            BindingExpressionPlan plan,
            BindingPathResult pathResult)
        {
            if (property == null)
                return;

            if (plan == null || plan.Mode != BindingMode.TwoWay)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay on property '" +
                    property.Definition.Name +
                    "' of registered XML component <" +
                    property.ComponentName +
                    "> requires one complete Binding expression.");
            }

            if (plan.HasComputedExpression)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay on property '" +
                    property.Definition.Name +
                    "' of registered XML component <" +
                    property.ComponentName +
                    "> cannot use a computed Binding expression.");
            }

            if (plan.HasNegation)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay on property '" +
                    property.Definition.Name +
                    "' of registered XML component <" +
                    property.ComponentName +
                    "> cannot use the ! binding operator.");
            }

            if (plan.UpdateSourceTrigger !=
                BindingUpdateSourceTrigger.PropertyChanged)
            {
                throw new InvalidOperationException(
                    "Registered XML component property proxies support " +
                    "UpdateSourceTrigger=PropertyChanged only. Apply " +
                    "LostFocus or Explicit to the concrete control binding " +
                    "inside the component instead.");
            }

            if (pathResult == null ||
                pathResult.TerminalDependency == null)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay on property '" +
                    property.Definition.Name +
                    "' of registered XML component <" +
                    property.ComponentName +
                    "> requires the Binding path to end in a writable " +
                    "PropertyBinding<T> or notifying CLR property.");
            }
        }

        private static object CreateComponentPropertyValueProxy(
            ComponentPropertyDefinition definition,
            object value)
        {
            if (definition == null ||
                definition.ValueProxyConstructor == null)
            {
                throw new InvalidOperationException(
                    "A component property is missing its cached observable " +
                    "proxy constructor.");
            }

            return definition.ValueProxyConstructor.Invoke(
                new object[] { value });
        }

        private object BindComponentCodeBehindProperty(
            ComponentPropertyValue property,
            object value)
        {
            if (property == null)
                return null;

            ComponentCodeMember member = property.CodeMember;

            if (property.CodeBehind != null &&
                member != null &&
                member.UsesBindingProxy)
            {
                object memberValue =
                    GetComponentCodeMemberValue(
                        member,
                        property.CodeBehind);

                if (memberValue == null)
                {
                    if (!CanWriteComponentCodeMember(member))
                    {
                        throw new InvalidOperationException(
                            "Component Class binding member '" +
                            property.Definition.Name +
                            "' is null and readonly. Initialize its stable " +
                            "PropertyBinding<T> instance.");
                    }

                    property.ValueProxy =
                        CreateComponentPropertyValueProxy(
                            property.Definition,
                            value);
                    SetComponentCodeMemberValue(
                        member,
                        property.CodeBehind,
                        property.ValueProxy);
                    memberValue = property.ValueProxy;
                }

                IPropertyBindingRuntime runtimeBinding =
                    memberValue as IPropertyBindingRuntime;

                if (runtimeBinding == null ||
                    runtimeBinding.ValueType != property.Definition.Type)
                {
                    throw new InvalidOperationException(
                        "Component Class binding member '" +
                        property.Definition.Name +
                        "' must be PropertyBinding<" +
                        property.Definition.Type.FullName +
                        "> to match the declared component property type.");
                }

                runtimeBinding.SetValue(value);
                return memberValue;
            }

            property.ValueProxy =
                CreateComponentPropertyValueProxy(
                    property.Definition,
                    value);

            if (property.CodeBehind == null || member == null)
                return property.ValueProxy;

            if (!CanWriteComponentCodeMember(member))
            {
                throw new InvalidOperationException(
                    "Plain Component Class member '" +
                    property.Definition.Name +
                    "' must be writable. Prefer a stable readonly " +
                    "PropertyBinding<T> when the value can change.");
            }

            object converted;

            if (!TryConvertObjectValue(
                    value,
                    member.MemberType,
                    out converted))
            {
                throw new InvalidOperationException(
                    "Component Class member '" +
                    property.Definition.Name +
                    "' of type " +
                    member.MemberType.FullName +
                    " cannot receive declared component property type " +
                    property.Definition.Type.FullName +
                    ".");
            }

            SetComponentCodeMemberValue(
                member,
                property.CodeBehind,
                converted);
            return property.ValueProxy;
        }

        private object ConvertComponentPropertyValue(
            ComponentPropertyDefinition definition,
            string expression,
            bool resolveExpression,
            object dataContext,
            string componentName)
        {
            object rawValue;

            if (expression == null)
            {
                rawValue = definition.Type.IsValueType
                    ? Activator.CreateInstance(definition.Type)
                    : null;
            }
            else if (resolveExpression)
            {
                rawValue = ResolveComponentValue(expression, dataContext);
            }
            else
            {
                rawValue = expression;
            }

            if (IsUnsetPresetValue(rawValue))
            {
                rawValue = definition.HasDefaultValue
                    ? (object)definition.DefaultValue
                    : (definition.Type.IsValueType
                        ? Activator.CreateInstance(definition.Type)
                        : null);
            }

            object converted;

            if (!TryConvertObjectValue(
                rawValue,
                definition.Type,
                out converted))
            {
                throw new InvalidOperationException(
                    "Value for property '" +
                    definition.Name +
                    "' on registered component <" +
                    componentName +
                    "> cannot be converted to " +
                    definition.Type.FullName +
                    ".");
            }

            return converted;
        }

        private object ConvertComponentPropertyValueForState(
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

                return ConvertComponentPropertyValue(
                    property.Definition,
                    property.Expression,
                    true,
                    state.ParentDataContext,
                    state.Root.GetType().Name);
            }
            finally
            {
                _activeComponentEventTarget = previousEventTarget;
            }
        }

        private object ResolveComponentValue(
            string expression,
            object dataContext)
        {
            string resolved =
                ResolveBindingAttributeValue(
                    expression,
                    dataContext);
            object value;

            if (TryTakeBoundObject(resolved, out value))
                return value;

            return resolved;
        }

        private static ComponentPropertyDefinition FindComponentProperty(
            RegisteredComponent component,
            string name)
        {
            if (component == null ||
                component.PropertiesByName == null ||
                String.IsNullOrEmpty(name))
            {
                return null;
            }

            ComponentPropertyDefinition definition;

            return component.PropertiesByName.TryGetValue(
                    name,
                    out definition)
                ? definition
                : null;
        }

        private static XmlAttribute FindAttributeIgnoreNamespace(
            XmlElement element,
            string name)
        {
            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];

                if (EqualsIgnoreCase(attribute.LocalName, name))
                    return attribute;
            }

            return null;
        }
    }
}
