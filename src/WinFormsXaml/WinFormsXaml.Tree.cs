using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        // ============================================================
        // BUILD TREE
        // ============================================================

        private object BuildElement(XmlElement element)
        {
            ComponentContentProjection projection =
                GetComponentContentProjection(element);

            if (projection == null)
                return BuildElementInActiveContext(element);

            object previousDataContext = _activeComponentDataContext;
            int previousComponentBuildDepth =
                _componentBuildDepth;
            int previousProjectionDepth =
                _componentContentProjectionDepth;
            string previousMarkupSource = _activeMarkupSource;
            string previousElementPathPrefix =
                _activeMarkupElementPathPrefix;
            Assembly previousMarkupAssembly =
                _activeMarkupAssembly;
            XmlElement previousContentRoot =
                _activeComponentContentRoot;
            object previousEventTarget =
                _activeComponentEventTarget;

            try
            {
                _activeComponentDataContext = projection.DataContext;
                _componentBuildDepth = projection.ComponentBuildDepth;
                _componentContentProjectionDepth =
                    previousProjectionDepth + 1;
                _activeMarkupSource = projection.MarkupSource;
                _activeMarkupElementPathPrefix =
                    projection.ElementPathPrefix;
                _activeMarkupAssembly = projection.MarkupAssembly;
                _activeComponentContentRoot = element;
                _activeComponentEventTarget = projection.EventTarget;

                object projected =
                    BuildElementInActiveContext(element);

                if (projected != null &&
                    projection.ChildrenHost != null)
                {
                    Control projectedControl = projected as Control;

                    if (projectedControl == null)
                    {
                        try
                        {
                            // The projected object cannot be returned to a caller
                            // or attached to the component. Release the runtime's
                            // ownership while keeping the projection error primary.
                            ReleaseCreatedElement(projected);
                        }
                        catch
                        {
                        }

                        throw new InvalidOperationException(
                            "A component <Children> slot can project only " +
                            "Windows Forms Control roots.");
                    }

                    projection.ChildrenHost.ProjectedChildren.Add(
                        projectedControl);
                }

                return projected;
            }
            finally
            {
                _activeComponentDataContext = previousDataContext;
                _componentBuildDepth = previousComponentBuildDepth;
                _componentContentProjectionDepth =
                    previousProjectionDepth;
                _activeMarkupSource = previousMarkupSource;
                _activeMarkupElementPathPrefix =
                    previousElementPathPrefix;
                _activeMarkupAssembly = previousMarkupAssembly;
                _activeComponentContentRoot = previousContentRoot;
                _activeComponentEventTarget = previousEventTarget;
            }
        }

        private object BuildElementInActiveContext(XmlElement element)
        {
            Dictionary<string, StyleDefinition> previousNamedStyles = null;
            List<StyleDefinition> previousImplicitStyles = null;
            bool restoreTemplateStyleScope = false;

            try
            {
                ItemTemplateStyleScope templateStyleScope =
                    GetCompiledItemTemplateStyleScope(element);

                if (templateStyleScope != null)
                {
                    previousNamedStyles = _activeComponentNamedStyles;
                    previousImplicitStyles = _activeComponentImplicitStyles;
                    _activeComponentNamedStyles =
                        templateStyleScope.NamedStyles;
                    _activeComponentImplicitStyles =
                        templateStyleScope.ImplicitStyles;
                    restoreTemplateStyleScope = true;
                }

                return BuildElementCore(element);
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    element,
                    null,
                    ex);
            }
            finally
            {
                if (restoreTemplateStyleScope)
                {
                    _activeComponentNamedStyles = previousNamedStyles;
                    _activeComponentImplicitStyles = previousImplicitStyles;
                }
            }
        }

        private object BuildElementCore(XmlElement element)
        {
            if (EqualsIgnoreCase(
                    element.LocalName,
                    "Children"))
            {
                ComponentChildrenHost childrenHost =
                    GetComponentChildrenSlotHost(element);

                if (childrenHost == null)
                {
                    throw new InvalidOperationException(
                        "<Children> is only valid as the single projection slot " +
                        "inside a registered XML component resource.");
                }

                ComponentChildrenMarker marker =
                    new ComponentChildrenMarker();
                marker.Host = childrenHost;
                return marker;
            }

            RegisteredComponent registeredComponent;

            if (TryGetRegisteredComponent(
                    element.LocalName,
                    out registeredComponent) &&
                registeredComponent.TemplateXml != null)
            {
                return BuildRegisteredXmlComponent(
                    element,
                    registeredComponent);
            }

            // Presets must exist before any attribute on this element is resolved.
            // Capture expressions first because resolution intentionally mutates the
            // working XML copy into plain WinForms-compatible values.
            PreReadPresets(element);

            object dataContext = GetCurrentBuildDataContext();
            string conditionExpression =
                GetAttributeIgnoreNamespace(
                    element,
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
                        element,
                        "Condition",
                        ex);
                }
            }

            ArrayList includeConditionAttributes =
                GetConditionalIncludeAttributes(element);
            bool dynamicIncludeCondition = false;

            try
            {
                if (!EvaluateConditionalIncludeConditions(
                        includeConditionAttributes,
                        dataContext,
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
                    element,
                    "Condition",
                    ex);
            }

            ArrayList pendingBindings =
                CaptureDynamicBindings(
                    element,
                    dataContext);
            pendingBindings = CaptureConditionalIncludeBindings(
                element,
                includeConditionAttributes,
                dataContext,
                pendingBindings);

            // Outside an ItemsControl, bindings/functions use the supplied
            // code-behind object as their data context. Inside an ItemTemplate,
            // ReplaceTemplateBindings has already resolved against the item.
            ValidateOneWayOnlyElementBindings(element);

            if (!dynamicCondition)
            {
                bool conditionMatches;

                try
                {
                    conditionMatches =
                        EvaluateCondition(element);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        element,
                        "Condition",
                        ex);
                }

                if (!conditionMatches)
                    return null;
            }

            if ((dynamicCondition || dynamicIncludeCondition) &&
                IsSimpleItem(element.LocalName))
            {
                throw new InvalidOperationException(
                    "Dynamic Condition is not supported on value-only <Item> " +
                    "elements. Put the condition on a Control or ItemTemplate root.");
            }

            if (dynamicCondition)
                RemoveAttributeIgnoreNamespace(element, "Condition");

            ResolveElementBindings(
                element,
                dataContext);

            PreReadResources(element);

            if (IsSimpleItem(
                element.LocalName))
            {
                return GetSimpleItemValue(
                    element);
            }

            Hashtable constructorAttributes;
            object instance =
                CreateInstance(
                    element,
                    registeredComponent,
                    out constructorAttributes);

            try
            {
                ElementInfo info =
                    new ElementInfo();

                info.XamlType =
                    element.LocalName;

                // Register the prepared metadata before local-value key
                // canonicalization; failure cleanup removes this entry.
                _elementInfos.Add(
                    instance,
                    info);

                CaptureLocalValueProperties(
                    info,
                    instance,
                    element);

                ConfigureCreatedObject(
                    instance);

                if (_activeTemplateElementMap != null)
                {
                    string templateElementPath =
                        GetAttributeIgnoreNamespace(
                            element,
                            "__WfxPath");

                    if (!String.IsNullOrEmpty(templateElementPath))
                        _activeTemplateElementMap[templateElementPath] = instance;
                }

                ApplyImplicitStyles(
                    instance,
                    element.LocalName);

                try
                {
                    ApplyStyleAttribute(
                        instance,
                        element);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    string styleProperty =
                        HasAttributeIgnoreNamespace(
                            element,
                            "ResourceStyle")
                                ? "ResourceStyle"
                                : "Style";

                    throw CreateMarkupLoadException(
                        element,
                        styleProperty,
                        ex);
                }

                ValidateConstructorOnlyBindings(
                    pendingBindings,
                    constructorAttributes,
                    instance,
                    element.LocalName);

                ApplyAttributes(
                    instance,
                    element,
                    constructorAttributes);

                string declaredName =
                    GetDeclaredName(
                        element);

                if (!String.IsNullOrEmpty(
                    declaredName))
                {
                    SetNativeName(
                        instance,
                        declaredName);

                    // ItemTemplate instances have their own logical namescope.
                    // Do not put repeated names into the global name dictionary.
                    if (_templateBuildDepth == 0 &&
                        _componentBuildDepth == 0)
                    {
                        RegisterName(
                            declaredName,
                            instance);
                    }
                }
                else if (_templateBuildDepth == 0 &&
                    _componentBuildDepth == 0)
                {
                    RegisterNativeName(
                        instance);
                }

                try
                {
                    ApplyInnerText(
                        instance,
                        element);
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

                RegisterDynamicBindings(
                    instance,
                    pendingBindings,
                    element);
                ApplyRetainedDynamicCondition(
                    instance,
                    pendingBindings);

                Control layoutControl = instance as Control;

                if (layoutControl != null)
                    layoutControl.SuspendLayout();

                try
                {
                    XmlNode node =
                        element.FirstChild;

                    while (node != null)
                    {
                        XmlElement childElement =
                            node as XmlElement;

                        if (childElement != null)
                        {
                            ItemsControl itemsControl =
                                instance as ItemsControl;

                            if (itemsControl != null &&
                                IsRemovedItemsTemplateAliasElement(
                                    childElement))
                            {
                                throw new InvalidOperationException(
                                    "Use ItemsControl.ItemTemplate with its " +
                                    "visual root directly inside it. Template " +
                                    "and DataTemplate elements are not supported.");
                            }
                            else if (IsPresetDefinitionElement(
                                childElement))
                            {
                                // PreReadPresets already imported this non-visual node.
                            }
                            else if (IsPropertyElement(
                                childElement))
                            {
                                try
                                {
                                    HandlePropertyElement(
                                        instance,
                                        childElement);
                                }
                                catch (WinFormsXamlLoadException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    throw CreateMarkupLoadException(
                                        childElement,
                                        GetPropertyElementName(
                                            childElement.LocalName),
                                        ex);
                                }
                            }
                            else
                            {
                                object child =
                                    BuildElement(
                                        childElement);

                                if (child != null)
                                {
                                    ComponentChildrenMarker childrenMarker =
                                        child as ComponentChildrenMarker;

                                    if (childrenMarker != null)
                                    {
                                        CaptureComponentChildrenSlot(
                                            instance,
                                            childrenMarker);
                                        node = node.NextSibling;
                                        continue;
                                    }

                                    // Register ownership before native attachment.
                                    // If attachment or an attached property throws,
                                    // the parent's build rollback still owns the child.
                                    RegisterLogicalChild(
                                        instance,
                                        child);

                                    try
                                    {
                                        AddChild(
                                            instance,
                                            child);

                                        ApplyAttachedProperties(
                                            instance,
                                            child,
                                            childElement);
                                    }
                                    catch (Exception ex)
                                    {
                                        // The child may be only partly parented when
                                        // user code throws from attachment or layout.
                                        // Dispose it directly and keep the original
                                        // attachment exception as the reported error.
                                        UnregisterLogicalChild(
                                            instance,
                                            child);

                                        try
                                        {
                                            ReleaseCreatedElement(child);
                                        }
                                        catch
                                        {
                                        }

                                        if (ex is WinFormsXamlLoadException)
                                            throw;

                                        throw CreateMarkupLoadException(
                                            childElement,
                                            null,
                                            ex);
                                    }
                                }
                            }
                        }

                        node =
                            node.NextSibling;
                    }
                }
                finally
                {
                    if (layoutControl != null)
                        layoutControl.ResumeLayout(false);
                }

                CompleteApplicationIconConfiguration(
                    instance as Form);

                PostConfigure(instance, element);

                return instance;
            }
            catch
            {
                try
                {
                    ReleaseCreatedElement(instance);
                }
                catch
                {
                    // The construction error explains why the element could
                    // not be loaded. A broken custom Dispose implementation
                    // must not replace that primary failure during rollback.
                }

                throw;
            }
        }

        private WinFormsXamlLoadException CreateMarkupLoadException(
            XmlElement element,
            string propertyName,
            Exception innerException)
        {
            WinFormsXamlLoadException existing =
                innerException as WinFormsXamlLoadException;

            if (existing != null)
                return existing;

            int lineNumber;
            int linePosition;

            MarkupXmlDocument.GetLocation(
                element,
                propertyName,
                out lineNumber,
                out linePosition);

            string elementMarkupSource =
                MarkupXmlDocument.GetMarkupSource(element);
            string elementPathPrefix =
                MarkupXmlDocument.GetElementPathPrefix(element);

            return new WinFormsXamlLoadException(
                !String.IsNullOrEmpty(elementMarkupSource)
                    ? elementMarkupSource
                    : (String.IsNullOrEmpty(_activeMarkupSource)
                        ? _markupSource
                        : _activeMarkupSource),
                GetMarkupElementPath(
                    element,
                    !String.IsNullOrEmpty(elementPathPrefix)
                        ? elementPathPrefix
                        : _activeMarkupElementPathPrefix,
                    _activeComponentContentRoot),
                propertyName,
                lineNumber,
                linePosition,
                innerException);
        }

        private static string GetMarkupElementPath(
            XmlElement element,
            string prefix)
        {
            return GetMarkupElementPath(
                element,
                prefix,
                null);
        }

        private static string GetMarkupElementPath(
            XmlElement element,
            string prefix,
            XmlElement pathRoot)
        {
            if (element == null)
                return prefix;

            ArrayList segments = new ArrayList();
            XmlElement current = element;

            while (current != null)
            {
                string segment = current.LocalName;
                string declaredName = GetDeclaredName(current);

                if (!String.IsNullOrEmpty(declaredName))
                {
                    segment += "#" + declaredName;
                }
                else
                {
                    XmlElement parent =
                        current.ParentNode as XmlElement;

                    if (parent != null)
                    {
                        int matchingCount = 0;
                        int matchingIndex = 0;
                        XmlNode sibling = parent.FirstChild;

                        while (sibling != null)
                        {
                            XmlElement siblingElement =
                                sibling as XmlElement;

                            if (siblingElement != null &&
                                String.Equals(
                                    siblingElement.LocalName,
                                    current.LocalName,
                                    StringComparison.Ordinal))
                            {
                                matchingCount++;

                                if (Object.ReferenceEquals(
                                        siblingElement,
                                        current))
                                {
                                    matchingIndex = matchingCount;
                                }
                            }

                            sibling = sibling.NextSibling;
                        }

                        if (matchingCount > 1)
                        {
                            segment +=
                                "[" + matchingIndex.ToString() + "]";
                        }
                    }
                }

                segments.Insert(0, segment);

                if (Object.ReferenceEquals(current, pathRoot))
                    break;

                current = current.ParentNode as XmlElement;
            }

            string path = String.Empty;
            int i;

            for (i = 0; i < segments.Count; i++)
                path += "/" + (string)segments[i];

            return String.IsNullOrEmpty(prefix)
                ? path
                : prefix + " -> " + path;
        }

        private void ReleaseCreatedElement(object instance)
        {
            if (instance == null)
                return;

            Exception firstError = null;

            try
            {
                RemoveNamesForElementTree(instance);
            }
            catch (Exception ex)
            {
                firstError = ex;
            }

            try
            {
                ReleaseElementObjectTree(
                    instance,
                    new Hashtable(_runtimeObjectReferenceComparer));
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            IDisposable disposable = instance as IDisposable;

            if (disposable != null)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private void RemoveNamesForElementTree(object root)
        {
            if (_namedObjects == null || root == null)
                return;

            Hashtable treeObjects =
                new Hashtable(_runtimeObjectReferenceComparer);
            CollectElementTreeObjects(
                root,
                treeObjects);

            ArrayList names = new ArrayList();

            foreach (KeyValuePair<string, object> entry in _namedObjects)
            {
                if (treeObjects.ContainsKey(entry.Value))
                {
                    names.Add(entry.Key);
                }
            }

            int i;
            Exception firstError = null;

            for (i = 0; i < names.Count; i++)
            {
                string name = names[i] as string;
                object value = _namedObjects[name];

                try
                {
                    UnwireRegisteredName(
                        name,
                        value);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
                finally
                {
                    // Do not retain a disposed element just because a custom
                    // code-behind member failed while its name was unwired.
                    _namedObjects.Remove(name);
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private void RegisterLogicalChild(
            object parent,
            object child)
        {
            if (parent == null || child == null)
                return;

            ElementInfo childInfo;

            // Literal collection items are not runtime-created elements and
            // therefore do not need lifecycle tracking.
            if (!_elementInfos.TryGetValue(child, out childInfo))
                return;

            ElementInfo parentInfo = GetInfo(parent);
            int i;

            for (i = 0; i < parentInfo.LogicalChildren.Count; i++)
            {
                if (Object.ReferenceEquals(
                    parentInfo.LogicalChildren[i],
                    child))
                {
                    return;
                }
            }

            parentInfo.LogicalChildren.Add(child);
        }

        private void UnregisterLogicalChild(
            object parent,
            object child)
        {
            if (parent == null || child == null)
                return;

            ElementInfo parentInfo;

            if (!_elementInfos.TryGetValue(parent, out parentInfo) ||
                parentInfo.LogicalChildren == null)
            {
                return;
            }

            int i;

            for (i = parentInfo.LogicalChildren.Count - 1;
                 i >= 0;
                 i--)
            {
                if (Object.ReferenceEquals(
                    parentInfo.LogicalChildren[i],
                    child))
                {
                    parentInfo.LogicalChildren.RemoveAt(i);
                    return;
                }
            }
        }

        private void CollectElementTreeObjects(
            object value,
            Hashtable visited)
        {
            if (value == null || visited.ContainsKey(value))
                return;

            visited[value] = true;

            ElementInfo info;

            if (_elementInfos.TryGetValue(value, out info) &&
                info.LogicalChildren != null)
            {
                int logicalIndex;

                for (logicalIndex = 0;
                     logicalIndex < info.LogicalChildren.Count;
                     logicalIndex++)
                {
                    CollectElementTreeObjects(
                        info.LogicalChildren[logicalIndex],
                        visited);
                }
            }

            Control control = value as Control;

            if (control == null)
                return;

            int controlIndex;

            for (controlIndex = 0;
                 controlIndex < control.Controls.Count;
                 controlIndex++)
            {
                CollectElementTreeObjects(
                    control.Controls[controlIndex],
                    visited);
            }
        }

        private bool IsTargetOrElementDescendant(
            object candidate,
            object target)
        {
            if (candidate == null || target == null)
                return false;

            if (Object.ReferenceEquals(candidate, target))
                return true;

            return ElementTreeContains(
                target,
                candidate,
                new Hashtable(_runtimeObjectReferenceComparer));
        }

        private bool ElementTreeContains(
            object value,
            object candidate,
            Hashtable visited)
        {
            if (value == null || visited.ContainsKey(value))
                return false;

            visited[value] = true;

            ElementInfo info;

            if (_elementInfos.TryGetValue(value, out info) &&
                info.LogicalChildren != null)
            {
                int logicalIndex;

                for (logicalIndex = 0;
                     logicalIndex < info.LogicalChildren.Count;
                     logicalIndex++)
                {
                    object child =
                        info.LogicalChildren[logicalIndex];

                    if (Object.ReferenceEquals(child, candidate) ||
                        ElementTreeContains(
                            child,
                            candidate,
                            visited))
                    {
                        return true;
                    }
                }
            }

            Control control = value as Control;

            if (control == null)
                return false;

            int controlIndex;

            for (controlIndex = 0;
                 controlIndex < control.Controls.Count;
                 controlIndex++)
            {
                Control child = control.Controls[controlIndex];

                if (Object.ReferenceEquals(child, candidate) ||
                    ElementTreeContains(
                        child,
                        candidate,
                        visited))
                {
                    return true;
                }
            }

            return false;
        }

        // ============================================================
        // CONDITIONAL RENDERING
        // ============================================================

        private void ValidateOneWayOnlyElementBindings(
            XmlElement element)
        {
            if (element == null)
                return;

            try
            {
                ValidateStructuralOneWayBinding(
                    element,
                    "Condition");
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    element,
                    "Condition",
                    ex);
            }

            try
            {
                ValidateStaticElementName(element);
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    element,
                    "Name",
                    ex);
            }
        }

        private static void ValidateStaticElementName(
            XmlElement element)
        {
            if (element == null)
                return;

            XmlAttribute nameAttribute = FindAttributeIgnoreNamespace(
                element,
                "Name");

            if (nameAttribute != null &&
                ContainsDynamicExpression(nameAttribute.Value))
            {
                throw new InvalidOperationException(
                    "Name/x:Name defines element identity and cannot be dynamic. " +
                    "Use a static name and bind the element's normal properties.");
            }
        }

        private static void ValidateStructuralOneWayBinding(
            XmlElement element,
            string attributeName)
        {
            XmlAttribute attribute =
                FindAttributeIgnoreNamespace(
                    element,
                    attributeName);

            if (attribute == null)
                return;

            BindingExpressionPlan plan;

            if (TryParseBindingExpression(
                    attribute.Value,
                    out plan) &&
                plan.Mode == BindingMode.TwoWay)
            {
                throw new InvalidOperationException(
                    attributeName +
                    " is structural and supports only OneWay bindings.");
            }
        }

        private bool EvaluateCondition(
            XmlElement element)
        {
            string value =
                GetAttributeIgnoreNamespace(
                    element,
                    "Condition");

            if (String.IsNullOrEmpty(value))
            {
                return true;
            }

            object boundCondition;

            if (TryTakeBoundObject(
                value,
                out boundCondition))
            {
                object converted;

                if (TryConvertObjectValue(
                    boundCondition,
                    typeof(bool),
                    out converted))
                {
                    return (bool)converted;
                }

                throw new InvalidOperationException(
                    "Condition function/binding must return a boolean value.");
            }

            return ParseBoolean(value);
        }

        // ============================================================
        // RESOURCES / STYLES
        // ============================================================

        private Dictionary<string, StyleDefinition> GetCurrentNamedStyles()
        {
            return _activeComponentNamedStyles == null
                ? _namedStyles
                : _activeComponentNamedStyles;
        }

        private List<StyleDefinition> GetCurrentImplicitStyles()
        {
            return _activeComponentImplicitStyles == null
                ? _implicitStyles
                : _activeComponentImplicitStyles;
        }

        private void PreReadResources(
            XmlElement element)
        {
            // ItemTemplate resources are parsed once into compiled lexical
            // scopes. Re-reading cloned resource XML here would append the same
            // implicit styles for every row and leak named styles into the
            // runtime/component-wide collections.
            if (GetCompiledItemTemplateStyleScope(element) != null)
                return;

            XmlNode node =
                element.FirstChild;

            while (node != null)
            {
                XmlElement child =
                    node as XmlElement;

                if (child != null &&
                    IsPropertyElement(child))
                {
                    string propertyName =
                        GetPropertyElementName(
                            child.LocalName);

                    if (EqualsIgnoreCase(
                        propertyName,
                        "Resources"))
                    {
                        ReadResources(
                            child);
                    }
                }

                node =
                    node.NextSibling;
            }
        }

        private void ReadResources(
            XmlElement resources)
        {
            ReadResources(
                resources,
                GetCurrentNamedStyles(),
                GetCurrentImplicitStyles());
        }

        private void ReadResources(
            XmlElement resources,
            Dictionary<string, StyleDefinition> namedStyles,
            List<StyleDefinition> implicitStyles)
        {
            if (resources == null ||
                namedStyles == null ||
                implicitStyles == null)
            {
                return;
            }

            XmlNode node =
                resources.FirstChild;

            while (node != null)
            {
                XmlElement child =
                    node as XmlElement;

                if (child != null &&
                    EqualsIgnoreCase(
                        child.LocalName,
                        "Style"))
                {
                    StyleDefinition style =
                        ParseStyleDefinition(
                            child);

                    if (style != null)
                    {
                        if (!String.IsNullOrEmpty(
                            style.Key))
                        {
                            InvalidateResolvedStyleChains(namedStyles);
                            namedStyles[style.Key] = style;
                        }
                        else if (
                            !String.IsNullOrEmpty(
                            style.TargetType))
                        {
                            implicitStyles.Add(style);
                        }
                    }
                }

                node =
                    node.NextSibling;
            }
        }

        private void InvalidateResolvedStyleChains(
            Dictionary<string, StyleDefinition> namedStyles)
        {
            if (_resolvedStyleChainCaches == null || namedStyles == null)
                return;

            ResolvedStyleChainScopeCache scopeCache =
                _resolvedStyleChainCaches[namedStyles]
                    as ResolvedStyleChainScopeCache;

            if (scopeCache == null)
                return;

            _resolvedStyleChainCacheEntryCount -=
                scopeCache.Chains.Count;

            if (_resolvedStyleChainCacheEntryCount < 0)
                _resolvedStyleChainCacheEntryCount = 0;

            _resolvedStyleChainCaches.Remove(namedStyles);
        }

        private StyleDefinition ParseStyleDefinition(
            XmlElement styleElement)
        {
            StyleDefinition style =
                new StyleDefinition();

            style.Key =
                GetAttributeIgnoreNamespace(
                    styleElement,
                    "Key");

            style.TargetType =
                NormalizeTypeMarkup(
                    GetAttributeIgnoreNamespace(
                        styleElement,
                        "TargetType"));

            style.BasedOnKey =
                ExtractStaticResourceKey(
                    GetAttributeIgnoreNamespace(
                        styleElement,
                        "BasedOn"));

            style.Condition =
                GetAttributeIgnoreNamespace(
                    styleElement,
                    "Condition");
            style.ConditionBindingKey =
                CreateConditionalStyleBindingKey("Style");
            style.ConditionMarkup =
                CaptureDynamicBindingMarkup(
                    styleElement,
                    "Condition");

            ArrayList includeConditions =
                GetConditionalIncludeAttributes(styleElement);
            int includeConditionIndex;

            for (includeConditionIndex = 0;
                 includeConditions != null &&
                 includeConditionIndex < includeConditions.Count;
                 includeConditionIndex++)
            {
                XmlAttribute includeCondition =
                    includeConditions[includeConditionIndex] as XmlAttribute;

                if (includeCondition == null)
                    continue;

                ConditionalStylePart part =
                    new ConditionalStylePart();
                part.Expression = includeCondition.Value;
                part.BindingKey =
                    CreateConditionalStyleBindingKey("IncludeStyle");
                part.Markup = CaptureDynamicBindingMarkup(
                    styleElement,
                    "Condition",
                    includeCondition.LocalName);
                style.IncludeConditions.Add(part);
            }

            XmlNode node =
                styleElement.FirstChild;

            while (node != null)
            {
                XmlElement setter =
                    node as XmlElement;

                if (setter != null &&
                    EqualsIgnoreCase(
                        setter.LocalName,
                        "Setter"))
                {
                    string property =
                        GetAttributeIgnoreNamespace(
                            setter,
                            "Property");

                    string value =
                        GetAttributeIgnoreNamespace(
                            setter,
                            "Value");
                    bool hasValueAttribute =
                        HasAttributeIgnoreNamespace(
                            setter,
                            "Value");

                    if (value == null)
                    {
                        value =
                            setter.InnerText;
                    }

                    if (!String.IsNullOrEmpty(
                        property))
                    {
                        StyleSetter item =
                            new StyleSetter();

                        item.Property =
                            property;

                        item.Value =
                            value;

                        item.Markup =
                            CaptureDynamicBindingMarkup(
                                setter,
                                property,
                                hasValueAttribute
                                    ? "Value"
                                    : null);

                        item.Condition =
                            GetAttributeIgnoreNamespace(
                                setter,
                                "Condition");
                        item.ConditionBindingKey =
                            CreateConditionalStyleBindingKey("Setter");
                        item.ConditionMarkup =
                            CaptureDynamicBindingMarkup(
                                setter,
                                "Condition");

                        style.Setters.Add(
                            item);
                    }
                }

                node =
                    node.NextSibling;
            }

            return style;
        }

        private void ApplyImplicitStyles(
            object instance,
            string xamlType)
        {
            List<StyleDefinition> implicitStyles =
                GetCurrentImplicitStyles();
            StyleDefinition[] matchingStyles =
                GetMatchingImplicitStyles(
                    implicitStyles,
                    instance,
                    xamlType);
            int i;

            for (i = 0;
                 i < matchingStyles.Length;
                 i++)
            {
                StyleDefinition style =
                    matchingStyles[i];

                ApplyStyleDefinition(
                    instance,
                    style,
                    null);
            }
        }

        private StyleDefinition[] GetMatchingImplicitStyles(
            List<StyleDefinition> implicitStyles,
            object instance,
            string xamlType)
        {
            if (implicitStyles == null ||
                implicitStyles.Count == 0 ||
                instance == null)
            {
                return _emptyStyleDefinitionArray;
            }

            Type instanceType = instance.GetType();
            ImplicitStyleMatchScopeCache scopeCache = null;

            if (_implicitStyleMatchCaches != null)
            {
                scopeCache =
                    _implicitStyleMatchCaches[implicitStyles]
                        as ImplicitStyleMatchScopeCache;

                if (scopeCache != null &&
                    scopeCache.SourceCount != implicitStyles.Count)
                {
                    _implicitStyleMatchCacheEntryCount -=
                        scopeCache.Entries.Count;

                    if (_implicitStyleMatchCacheEntryCount < 0)
                        _implicitStyleMatchCacheEntryCount = 0;

                    scopeCache.Entries.Clear();
                    scopeCache.SourceCount = implicitStyles.Count;
                }

                if (scopeCache != null)
                {
                    int cachedIndex;

                    for (cachedIndex = 0;
                         cachedIndex < scopeCache.Entries.Count;
                         cachedIndex++)
                    {
                        ImplicitStyleMatchCacheEntry cached =
                            scopeCache.Entries[cachedIndex]
                                as ImplicitStyleMatchCacheEntry;

                        if (cached != null &&
                            Object.ReferenceEquals(
                                cached.InstanceType,
                                instanceType) &&
                            String.Equals(
                                cached.XamlType,
                                xamlType,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            _implicitStyleMatchCacheHitCount++;
                            return cached.Matches;
                        }
                    }
                }
            }

            ArrayList matches = new ArrayList();
            int i;

            for (i = 0; i < implicitStyles.Count; i++)
            {
                StyleDefinition style = implicitStyles[i];

                if (StyleMatches(
                        style,
                        instance,
                        xamlType))
                {
                    matches.Add(style);
                }
            }

            StyleDefinition[] result = matches.Count == 0
                ? _emptyStyleDefinitionArray
                : (StyleDefinition[])matches.ToArray(
                    typeof(StyleDefinition));

            if (_implicitStyleMatchCacheEntryCount >=
                    ImplicitStyleMatchCacheLimit)
            {
                return result;
            }

            if (_implicitStyleMatchCaches == null)
            {
                _implicitStyleMatchCaches =
                    new Hashtable(_runtimeObjectReferenceComparer);
            }

            if (scopeCache == null)
            {
                scopeCache = new ImplicitStyleMatchScopeCache();
                scopeCache.SourceCount = implicitStyles.Count;
                _implicitStyleMatchCaches[implicitStyles] = scopeCache;
            }

            if (scopeCache.Entries.Count >=
                ImplicitStyleMatchCachePerScopeLimit)
            {
                return result;
            }

            ImplicitStyleMatchCacheEntry entry =
                new ImplicitStyleMatchCacheEntry();
            entry.InstanceType = instanceType;
            entry.XamlType = xamlType;
            entry.Matches = result;
            scopeCache.Entries.Add(entry);
            _implicitStyleMatchCacheEntryCount++;
            return result;
        }

        private void ApplyStyleAttribute(
            object instance,
            XmlElement element)
        {
            string resourceStyleValue =
                GetAttributeIgnoreNamespace(
                    element,
                    "ResourceStyle");
            bool styleIsResourceSelector =
                IsResourceStyleProperty(instance, "Style");
            string styleValue = styleIsResourceSelector
                ? GetAttributeIgnoreNamespace(element, "Style")
                : null;

            if (!String.IsNullOrEmpty(resourceStyleValue) &&
                !String.IsNullOrEmpty(styleValue))
            {
                throw new InvalidOperationException(
                    "Use either Style or ResourceStyle to select a named " +
                    "resource style, not both.");
            }

            if (!String.IsNullOrEmpty(resourceStyleValue))
                styleValue = resourceStyleValue;

            if (String.IsNullOrEmpty(
                styleValue))
            {
                return;
            }

            object boundStyle;

            if (TryTakeBoundObject(
                styleValue,
                out boundStyle))
            {
                // A null/empty dynamically selected style means "no named style".
                if (boundStyle == null)
                    return;

                styleValue =
                    BindingValueToString(
                        boundStyle);

                if (String.IsNullOrEmpty(styleValue))
                    return;
            }

            string key =
                ExtractStaticResourceKey(
                    styleValue);

            if (String.IsNullOrEmpty(
                key))
            {
                key =
                    styleValue.Trim();
            }

            StyleDefinition style;

            if (!GetCurrentNamedStyles().TryGetValue(
                key,
                out style))
            {
                throw new InvalidOperationException(
                    "Style resource '" +
                    key +
                    "' was not found.");
            }

            GetInfo(instance).AppliedNamedStyleValue = key;

            ApplyStyleDefinition(
                instance,
                style,
                null);
        }

        private void ApplyStyleDefinition(
            object instance,
            StyleDefinition style,
            List<string> chain)
        {
            if (style == null)
                return;

            StyleDefinition[] resolvedStyles =
                GetResolvedStyleChain(
                    GetCurrentNamedStyles(),
                    style,
                    chain);
            int styleIndex;

            for (styleIndex = 0;
                 styleIndex < resolvedStyles.Length;
                 styleIndex++)
            {
                StyleDefinition currentStyle =
                    resolvedStyles[styleIndex];

                if (!EvaluateConditionalStylePart(
                        instance,
                        currentStyle.Condition,
                        currentStyle.ConditionBindingKey,
                        currentStyle.ConditionMarkup))
                {
                    continue;
                }

                bool includeConditionsMatch = true;
                int includeConditionIndex;

                for (includeConditionIndex = 0;
                     includeConditionIndex <
                        currentStyle.IncludeConditions.Count;
                     includeConditionIndex++)
                {
                    ConditionalStylePart includeCondition =
                        currentStyle.IncludeConditions[includeConditionIndex];

                    if (!EvaluateConditionalStylePart(
                            instance,
                            includeCondition.Expression,
                            includeCondition.BindingKey,
                            includeCondition.Markup))
                    {
                        includeConditionsMatch = false;
                        break;
                    }
                }

                if (!includeConditionsMatch)
                    continue;

                int i;

                for (i = 0;
                     i < currentStyle.Setters.Count;
                     i++)
                {
                    StyleSetter setter =
                        currentStyle.Setters[i];

                    if (!EvaluateConditionalStylePart(
                            instance,
                            setter.Condition,
                            setter.ConditionBindingKey,
                            setter.ConditionMarkup))
                    {
                        continue;
                    }

                    RemoveActiveTemplateStyleBinding(
                        instance,
                        setter.Property);

                    if (HasLocalValue(
                        instance,
                        setter.Property))
                    {
                        DeactivateStyleSetterBinding(
                            instance,
                            setter.Property);
                        continue;
                    }

                    ActivateStyleValue(
                        instance,
                        setter.Property);

                    // A static setter must retire a retained dynamic setter from
                    // the previous/base style for the same property.
                    DeactivateStyleSetterBinding(
                        instance,
                        setter.Property);

                    string value = setter.Value;
                    bool valueWasApplied = false;

                    if (ContainsDynamicExpression(value))
                    {
                        if (_templateBuildDepth == 0 ||
                            _componentBuildDepth != 0)
                        {
                            object dataContext =
                                GetCurrentBuildDataContext();

                            // Styles are parsed from resource XML rather than built as
                            // controls, so their setter expressions need an explicit
                            // retained binding for later preset/binding refreshes.
                            RegisterDynamicBinding(
                                instance,
                                setter.Property,
                                value,
                                dataContext,
                                false,
                                true,
                                setter.Markup);

                            value =
                                ResolveBindingAttributeValue(
                                    value,
                                    dataContext);
                        }
                        else
                        {
                            object resolvedValue =
                                EvaluateTemplateExpressionValue(
                                    value,
                                    _activeTemplateDataContext);

                            Control styleTarget = instance as Control;

                            if (styleTarget != null &&
                                _activeTemplateStyleBindingSlots != null)
                            {
                                RenderBindingSlot slot =
                                    new RenderBindingSlot();

                                slot.AttributeName = setter.Property;
                                slot.Expression = value;
                                slot.EventTarget =
                                    _activeComponentEventTarget;
                                slot.Target = styleTarget;
                                slot.LastValue = resolvedValue;
                                slot.StyleSetter = true;
                                if (!IsUnsetPresetValue(resolvedValue))
                                {
                                    slot.PresetBaselineRestore =
                                        CapturePresetBoundPropertyBaseline(
                                            styleTarget,
                                            setter.Property);
                                }
                                slot.Kind =
                                    setter.Property.IndexOf('.') >= 0 ||
                                    EqualsIgnoreCase(setter.Property, "Style") ||
                                    EqualsIgnoreCase(setter.Property, "ResourceStyle")
                                        ? RenderBindingSlotKind.RebuildOnChange
                                        : RenderBindingSlotKind.Attribute;
                                slot.AffectsLayout =
                                    AttributeCanAffectLayout(setter.Property);
                                _activeTemplateStyleBindingSlots.Add(slot);
                            }

                            valueWasApplied =
                                ApplyBoundObjectAttribute(
                                    instance,
                                    setter.Property,
                                    resolvedValue,
                                    true);

                            if (!valueWasApplied)
                                value = BindingValueToString(resolvedValue);
                        }
                    }

                    if (!valueWasApplied)
                    {
                        ApplyStyleSetterAttribute(
                            instance,
                            setter.Property,
                            value);
                    }
                }
            }
        }

        private StyleDefinition[] GetResolvedStyleChain(
            Dictionary<string, StyleDefinition> namedStyles,
            StyleDefinition style,
            List<string> chain)
        {
            if (style == null)
                return _emptyStyleDefinitionArray;

            bool mayCache = chain == null || chain.Count == 0;
            ResolvedStyleChainScopeCache scopeCache = null;

            if (mayCache && _resolvedStyleChainCaches != null)
            {
                scopeCache =
                    _resolvedStyleChainCaches[namedStyles]
                        as ResolvedStyleChainScopeCache;

                if (scopeCache != null)
                {
                    StyleDefinition[] cached =
                        scopeCache.Chains[style]
                            as StyleDefinition[];

                    if (cached != null)
                    {
                        _resolvedStyleChainCacheHitCount++;
                        return cached;
                    }
                }
            }

            List<string> resolving = chain == null
                ? new List<string>()
                : chain;
            ArrayList resolved = new ArrayList();

            AppendResolvedStyleChain(
                namedStyles,
                style,
                resolving,
                resolved);

            StyleDefinition[] result =
                (StyleDefinition[])resolved.ToArray(
                    typeof(StyleDefinition));

            if (!mayCache ||
                _resolvedStyleChainCacheEntryCount >=
                    ResolvedStyleChainCacheLimit)
            {
                return result;
            }

            if (_resolvedStyleChainCaches == null)
            {
                _resolvedStyleChainCaches =
                    new Hashtable(_runtimeObjectReferenceComparer);
            }

            if (scopeCache == null)
            {
                scopeCache = new ResolvedStyleChainScopeCache();
                _resolvedStyleChainCaches[namedStyles] = scopeCache;
            }

            if (scopeCache.Chains.Count >=
                ResolvedStyleChainCachePerScopeLimit)
            {
                return result;
            }

            scopeCache.Chains.Add(style, result);
            _resolvedStyleChainCacheEntryCount++;
            return result;
        }

        private void AppendResolvedStyleChain(
            Dictionary<string, StyleDefinition> namedStyles,
            StyleDefinition style,
            List<string> chain,
            ArrayList resolved)
        {
            bool addedKey = false;

            if (!String.IsNullOrEmpty(style.Key) &&
                !String.IsNullOrEmpty(style.BasedOnKey))
            {
                if (chain.Contains(style.Key))
                {
                    throw new InvalidOperationException(
                        "Circular Style BasedOn reference involving '" +
                        style.Key +
                        "'.");
                }

                chain.Add(style.Key);
                addedKey = true;
            }

            try
            {
                if (!String.IsNullOrEmpty(style.BasedOnKey))
                {
                    StyleDefinition baseStyle;

                    if (namedStyles == null ||
                        !namedStyles.TryGetValue(
                            style.BasedOnKey,
                            out baseStyle))
                    {
                        throw new InvalidOperationException(
                            "Base style '" +
                            style.BasedOnKey +
                            "' was not found.");
                    }

                    AppendResolvedStyleChain(
                        namedStyles,
                        baseStyle,
                        chain,
                        resolved);
                }

                resolved.Add(style);
            }
            finally
            {
                if (addedKey)
                    chain.Remove(style.Key);
            }
        }

        private void RemoveActiveTemplateStyleBinding(
            object instance,
            string propertyName)
        {
            if (_activeTemplateStyleBindingSlots == null ||
                instance == null ||
                String.IsNullOrEmpty(propertyName))
            {
                return;
            }

            int i;

            for (i = _activeTemplateStyleBindingSlots.Count - 1;
                 i >= 0;
                 i--)
            {
                RenderBindingSlot slot =
                    _activeTemplateStyleBindingSlots[i] as RenderBindingSlot;

                if (slot != null &&
                    Object.ReferenceEquals(slot.Target, instance) &&
                    EqualsIgnoreCase(slot.AttributeName, propertyName))
                {
                    _activeTemplateStyleBindingSlots.RemoveAt(i);
                }
            }
        }

        private void CaptureLocalValueProperties(
            ElementInfo info,
            object instance,
            XmlElement element)
        {
            if (info == null || element == null)
                return;

            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];
                string name = attribute.LocalName;

                if (ShouldIgnoreAttribute(attribute) ||
                    EqualsIgnoreCase(name, "Name") ||
                    IsResourceStyleProperty(instance, name) ||
                    EqualsIgnoreCase(name, "Condition"))
                {
                    continue;
                }

                AddLocalValueProperty(
                    info,
                    GetStylePropertyKey(instance, name));
            }

            XmlNode node = element.FirstChild;
            bool hasElementChildren = false;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    hasElementChildren = true;

                    if (IsPropertyElement(child))
                    {
                        string propertyName =
                            GetPropertyElementName(child.LocalName);

                        // A conditional object property is an overlay, not an
                        // unconditional local value. Its inactive baseline is
                        // the active style layer (or the native default).
                        if (!EqualsIgnoreCase(propertyName, "Resources") &&
                            !HasAttributeIgnoreNamespace(child, "Condition"))
                        {
                            AddLocalValueProperty(
                                info,
                                GetStylePropertyKey(
                                    instance,
                                    propertyName));
                        }
                    }
                }

                node = node.NextSibling;
            }

            if (!hasElementChildren &&
                !HasAttributeIgnoreNamespace(element, "Text") &&
                !HasAttributeIgnoreNamespace(element, "Content") &&
                !HasAttributeIgnoreNamespace(element, "Header") &&
                !String.IsNullOrEmpty(element.InnerText) &&
                element.InnerText.Trim().Length != 0)
            {
                AddLocalValueProperty(
                    info,
                    GetStylePropertyKey(instance, "Text"));
            }
        }

        private static void AddLocalValueProperty(
            ElementInfo info,
            string propertyName)
        {
            if (info == null || String.IsNullOrEmpty(propertyName))
                return;

            if (info.LocalValueProperties == null)
                info.LocalValueProperties = new ArrayList();

            int i;

            for (i = 0; i < info.LocalValueProperties.Count; i++)
            {
                if (EqualsIgnoreCase(
                    info.LocalValueProperties[i] as string,
                    propertyName))
                {
                    return;
                }
            }

            info.LocalValueProperties.Add(propertyName);
        }

        private bool HasLocalValue(
            object instance,
            string propertyName)
        {
            if (instance == null || String.IsNullOrEmpty(propertyName))
                return false;

            ElementInfo info = GetInfo(instance);
            ArrayList properties = info.LocalValueProperties;
            string key = GetStylePropertyKey(
                instance,
                propertyName);

            if (properties == null)
                return false;

            int i;

            for (i = 0; i < properties.Count; i++)
            {
                string localKey = properties[i] as string;

                if (EqualsIgnoreCase(localKey, key) ||
                    (IsSizeKey(localKey) &&
                     IsSizeKey(key) &&
                     (EqualsIgnoreCase(localKey, "Size") ||
                      EqualsIgnoreCase(key, "Size"))) ||
                    (IsMinimumSizeKey(localKey) &&
                     IsMinimumSizeKey(key) &&
                     (EqualsIgnoreCase(localKey, "MinimumSize") ||
                      EqualsIgnoreCase(key, "MinimumSize"))) ||
                    (IsMaximumSizeKey(localKey) &&
                     IsMaximumSizeKey(key) &&
                     (EqualsIgnoreCase(localKey, "MaximumSize") ||
                      EqualsIgnoreCase(key, "MaximumSize"))) ||
                    (IsFontStylePropertyKey(localKey) &&
                     IsFontStylePropertyKey(key) &&
                     (EqualsIgnoreCase(localKey, "Font") ||
                      EqualsIgnoreCase(key, "Font"))))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSizeKey(string key)
        {
            return EqualsIgnoreCase(key, "Size") ||
                   EqualsIgnoreCase(key, "Width") ||
                   EqualsIgnoreCase(key, "Height");
        }

        private static bool IsMinimumSizeKey(string key)
        {
            return EqualsIgnoreCase(key, "MinimumSize") ||
                   EqualsIgnoreCase(key, "MinWidth") ||
                   EqualsIgnoreCase(key, "MinHeight");
        }

        private static bool IsMaximumSizeKey(string key)
        {
            return EqualsIgnoreCase(key, "MaximumSize") ||
                   EqualsIgnoreCase(key, "MaxWidth") ||
                   EqualsIgnoreCase(key, "MaxHeight");
        }

        private bool StyleMatches(
            StyleDefinition style,
            object instance,
            string xamlType)
        {
            // ParseStyleDefinition stores the normalized target once. Matching
            // runs for every element, so do not trim and decode x:Type again.
            string target = style.TargetType;

            if (String.IsNullOrEmpty(
                target))
            {
                return false;
            }

            if (EqualsIgnoreCase(
                target,
                xamlType))
            {
                return true;
            }

            Type actual =
                instance.GetType();

            if (EqualsIgnoreCase(
                target,
                actual.Name))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                    target,
                    "Control") &&
                instance is Control)
            {
                return true;
            }

            Type targetType =
                ResolveTypeByName(
                    target);

            if (targetType != null &&
                targetType.IsAssignableFrom(
                    actual))
            {
                return true;
            }

            Type formsType =
                typeof(Control).Assembly.GetType(
                    "System.Windows.Forms." +
                    target,
                    false,
                    true);

            return
                formsType != null &&
                formsType.IsAssignableFrom(
                    actual);
        }

        private static string NormalizeTypeMarkup(
            string value)
        {
            if (String.IsNullOrEmpty(
                value))
            {
                return value;
            }

            value =
                value.Trim();

            if (value.StartsWith(
                "{x:Type ",
                StringComparison.OrdinalIgnoreCase))
            {
                value =
                    value.Substring(8);

                if (value.EndsWith("}"))
                {
                    value =
                        value.Substring(
                            0,
                            value.Length - 1);
                }

                value =
                    value.Trim();
            }

            int colon =
                value.LastIndexOf(':');

            if (colon >= 0 &&
                colon <
                    value.Length - 1)
            {
                value =
                    value.Substring(
                        colon + 1);
            }

            return value;
        }

        private static string ExtractStaticResourceKey(
            string value)
        {
            if (String.IsNullOrEmpty(
                value))
            {
                return null;
            }

            value =
                value.Trim();

            const string prefix =
                "{StaticResource ";

            if (value.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase) &&
                value.EndsWith("}"))
            {
                return value.Substring(
                    prefix.Length,
                    value.Length -
                    prefix.Length -
                    1).Trim();
            }

            return null;
        }

        // ============================================================
        // TYPE RESOLUTION
        // ============================================================

        private object CreateInstance(
            XmlElement element,
            RegisteredComponent registeredComponent,
            out Hashtable constructorAttributes)
        {
            string name =
                element.LocalName;

            constructorAttributes = null;

            RegisteredComponent component = registeredComponent;

            if ((component != null ||
                 TryGetRegisteredComponent(name, out component)) &&
                component.ComponentType != null)
            {
                return CreateRegisteredTypeComponent(
                    component,
                    element,
                    out constructorAttributes);
            }

            Type type =
                ResolveXamlType(
                    name,
                    element);

            if (type == null)
            {
                throw new InvalidOperationException(
                    "Cannot resolve XAML element <" +
                    name +
                    "> to a .NET/WinForms type.");
            }

            if (type.IsAbstract)
            {
                throw new InvalidOperationException(
                    "Type '" +
                    type.FullName +
                    "' is abstract.");
            }

            try
            {
                return Activator.CreateInstance(
                    type);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not create '" +
                    type.FullName +
                    "' for <" +
                    name +
                    ">: " +
                    ex.Message,
                    ex);
            }
        }

        private Type ResolveXamlType(
            string name,
            XmlElement element)
        {
            bool elementSpecific =
                EqualsIgnoreCase(name, "Object") ||
                EqualsIgnoreCase(name, "Control");

            if (!elementSpecific && _xamlTypeCache != null)
            {
                object cached = _xamlTypeCache[name];

                if (cached != null)
                {
                    return Object.ReferenceEquals(cached, _missingTypeCacheValue)
                        ? null
                        : cached as Type;
                }
            }

            Type resolved = ResolveXamlTypeUncached(name, element);

            if (!elementSpecific && _xamlTypeCache != null &&
                _xamlTypeCache.Count < XamlTypeCacheLimit &&
                name.Length <= RuntimeMetadataCacheKeyLengthLimit)
            {
                _xamlTypeCache[name] = resolved == null
                    ? _missingTypeCacheValue
                    : (object)resolved;
            }

            return resolved;
        }

        private Type ResolveXamlTypeUncached(
            string name,
            XmlElement element)
        {
            if (EqualsIgnoreCase(
                name,
                "Grid"))
            {
                return typeof(GridHost);
            }

            if (EqualsIgnoreCase(
                name,
                "StackPanel"))
            {
                return typeof(StackHost);
            }

            if (EqualsIgnoreCase(
                name,
                "ItemsControl"))
            {
                return typeof(global::WinFormsXaml.ItemsControl);
            }

            if (EqualsIgnoreCase(
                name,
                "HyperlinkLabel"))
            {
                return typeof(global::WinFormsXaml.HyperlinkLabel);
            }

            if (EqualsIgnoreCase(
                name,
                "VerticalScrollBar"))
            {
                return typeof(global::WinFormsXaml.VerticalScrollBar);
            }

            if (EqualsIgnoreCase(
                name,
                "HorizontalScrollBar"))
            {
                return typeof(global::WinFormsXaml.HorizontalScrollBar);
            }

            if (EqualsIgnoreCase(
                name,
                "ScrollBarStyle"))
            {
                return typeof(global::WinFormsXaml.ScrollBarStyle);
            }

            if (EqualsIgnoreCase(
                name,
                "Image"))
            {
                return typeof(global::WinFormsXaml.ImageControl);
            }

            if (EqualsIgnoreCase(
                name,
                "Link"))
            {
                return typeof(LinkLabel.Link);
            }

            if (EqualsIgnoreCase(
                name,
                "ProgressBar"))
            {
                return typeof(CompatibleProgressBar);
            }

            if (EqualsIgnoreCase(
                name,
                "FlexPanel"))
            {
                return typeof(FlexPanel);
            }

            if (EqualsIgnoreCase(
                name,
                "DockPanel"))
            {
                return typeof(DockHost);
            }

            if (EqualsIgnoreCase(
                name,
                "Canvas"))
            {
                return typeof(CanvasHost);
            }

            if (EqualsIgnoreCase(
                name,
                "Border"))
            {
                return typeof(BorderHost);
            }

            if (EqualsIgnoreCase(
                name,
                "ScrollViewer"))
            {
                return typeof(ScrollHost);
            }

            if (EqualsIgnoreCase(
                name,
                "Viewbox"))
            {
                return typeof(SingleHost);
            }

            if (EqualsIgnoreCase(
                    name,
                    "Object") ||
                EqualsIgnoreCase(
                    name,
                    "Control"))
            {
                string explicitType =
                    GetAttributeIgnoreNamespace(
                        element,
                        "Type");

                if (!String.IsNullOrEmpty(
                    explicitType))
                {
                    Type resolved =
                        ResolveTypeByName(
                            explicitType);

                    if (resolved != null)
                        return resolved;
                }
            }

            // These historical convenience names mapped directly to existing
            // WinForms types. Native type names are now the only implicit
            // spelling; an application may still claim one explicitly through
            // Register before loading markup.
            if (IsRemovedElementAlias(name))
                return null;

            Type type =
                typeof(Control).Assembly.GetType(
                    "System.Windows.Forms." +
                    name,
                    false,
                    true);

            if (type != null)
                return type;

            Type uniqueType =
                Type.GetType(
                    name,
                    false,
                    true);

            Assembly[] assemblies =
                AppDomain.CurrentDomain
                    .GetAssemblies();

            int i;

            for (i = 0;
                 i < assemblies.Length;
                 i++)
            {
                type =
                    assemblies[i].GetType(
                        name,
                        false,
                        true);

                if (type == null)
                    continue;

                if (uniqueType == null)
                {
                    uniqueType = type;
                }
                else if (!Object.ReferenceEquals(uniqueType, type))
                {
                    ThrowAmbiguousXamlType(name);
                }
            }

            if (uniqueType != null)
                return uniqueType;

            for (i = 0;
                 i < assemblies.Length;
                 i++)
            {
                Type[] types =
                    GetAssemblyTypesSafe(
                        assemblies[i]);

                int n;

                for (n = 0;
                     n < types.Length;
                     n++)
                {
                    Type candidate =
                        types[n];

                    if (candidate == null)
                        continue;

                    if (EqualsIgnoreCase(
                        candidate.Name,
                        name))
                    {
                        if (uniqueType == null)
                        {
                            uniqueType = candidate;
                        }
                        else if (!Object.ReferenceEquals(
                            uniqueType,
                            candidate))
                        {
                            ThrowAmbiguousXamlType(name);
                        }
                    }
                }
            }

            return uniqueType;
        }

        private static void ThrowAmbiguousXamlType(string name)
        {
            throw new InvalidOperationException(
                "XAML type name '" +
                name +
                "' is ambiguous across loaded types. Use <Object Type='" +
                "Namespace.Type, AssemblyName'> or register an explicit " +
                "component name.");
        }

        private static bool IsRemovedElementAlias(string name)
        {
            return EqualsIgnoreCase(name, "Window") ||
                EqualsIgnoreCase(name, "WrapPanel") ||
                EqualsIgnoreCase(name, "TextBlock") ||
                EqualsIgnoreCase(name, "PasswordBox") ||
                EqualsIgnoreCase(name, "Slider") ||
                EqualsIgnoreCase(name, "TabItem") ||
                EqualsIgnoreCase(name, "Calendar") ||
                EqualsIgnoreCase(name, "DatePicker") ||
                EqualsIgnoreCase(name, "Separator") ||
                EqualsIgnoreCase(name, "TreeViewItem") ||
                EqualsIgnoreCase(name, "Frame") ||
                EqualsIgnoreCase(name, "Expander") ||
                EqualsIgnoreCase(name, "DocumentViewer") ||
                EqualsIgnoreCase(name, "ComboBoxItem") ||
                EqualsIgnoreCase(name, "ListBoxItem") ||
                EqualsIgnoreCase(name, "ItemsRepeater") ||
                EqualsIgnoreCase(name, "Hyperlink");
        }

        private Type ResolveTypeByName(
            string name)
        {
            if (String.IsNullOrEmpty(
                name))
            {
                return null;
            }

            name =
                NormalizeTypeMarkup(
                    name);

            if (_resolvedTypeNameCache != null)
            {
                object cached = _resolvedTypeNameCache[name];

                if (cached != null)
                {
                    return Object.ReferenceEquals(cached, _missingTypeCacheValue)
                        ? null
                        : cached as Type;
                }
            }

            Type uniqueType =
                Type.GetType(
                    name,
                    false,
                    true);

            Type type;

            type =
                typeof(Control).Assembly.GetType(
                    "System.Windows.Forms." +
                    name,
                    false,
                    true);

            if (type != null)
            {
                CacheResolvedTypeName(name, type);
                return type;
            }

            Assembly[] assemblies =
                AppDomain.CurrentDomain
                    .GetAssemblies();

            int i;

            for (i = 0;
                 i < assemblies.Length;
                 i++)
            {
                type =
                    assemblies[i].GetType(
                        name,
                        false,
                        true);

                if (type == null)
                    continue;

                if (uniqueType == null)
                {
                    uniqueType = type;
                }
                else if (!Object.ReferenceEquals(uniqueType, type))
                {
                    throw new InvalidOperationException(
                        "Type name '" +
                        name +
                        "' is ambiguous across loaded assemblies. Use an " +
                        "assembly-qualified type name.");
                }
            }

            if (uniqueType != null)
            {
                CacheResolvedTypeName(name, uniqueType);
                return uniqueType;
            }

            CacheResolvedTypeName(name, _missingTypeCacheValue);

            return null;
        }

        private void CacheResolvedTypeName(
            string name,
            object value)
        {
            if (_resolvedTypeNameCache == null ||
                String.IsNullOrEmpty(name) ||
                name.Length > RuntimeMetadataCacheKeyLengthLimit ||
                _resolvedTypeNameCache.Count >= ResolvedTypeNameCacheLimit)
            {
                return;
            }

            _resolvedTypeNameCache[name] = value;
        }

        private static Type[] GetAssemblyTypesSafe(
            Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types == null
                    ? new Type[0]
                    : ex.Types;
            }
            catch
            {
                return new Type[0];
            }
        }

    }
}
