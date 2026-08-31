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
        private Control BuildTemplateControl(
            ItemsControl host,
            XmlElement templateRoot,
            object dataContext,
            Hashtable functionResults,
            out ArrayList bindingSlots)
        {
            object previousEventTarget = _activeComponentEventTarget;
            ItemTemplateActiveContext previousTemplateContext =
                PushItemTemplateDeclarationContext(host);

            try
            {
                if (host != null)
                {
                    _activeComponentEventTarget =
                        host.TemplateEventTarget;
                }

                return BuildTemplateControlCore(
                    host,
                    templateRoot,
                    dataContext,
                    functionResults,
                    out bindingSlots);
            }
            finally
            {
                _activeComponentEventTarget = previousEventTarget;
                RestoreItemTemplateDeclarationContext(
                    previousTemplateContext);
            }
        }

        private ItemTemplateDeclarationContext
            CaptureItemTemplateDeclarationContext()
        {
            ItemTemplateDeclarationContext context =
                new ItemTemplateDeclarationContext();

            context.NamedStyles =
                new Dictionary<string, StyleDefinition>(
                    GetCurrentNamedStyles(),
                    StringComparer.OrdinalIgnoreCase);
            context.ImplicitStyles =
                new List<StyleDefinition>(
                    GetCurrentImplicitStyles());
            context.MarkupSource =
                String.IsNullOrEmpty(_activeMarkupSource)
                    ? _markupSource
                    : _activeMarkupSource;
            context.ElementPathPrefix =
                _activeMarkupElementPathPrefix;
            context.MarkupAssembly =
                _activeMarkupAssembly == null
                    ? _markupAssembly
                    : _activeMarkupAssembly;

            return context;
        }

        private ItemTemplateActiveContext
            PushItemTemplateDeclarationContext(ItemsControl host)
        {
            ItemTemplateActiveContext previous =
                new ItemTemplateActiveContext();

            previous.NamedStyles = _activeComponentNamedStyles;
            previous.ImplicitStyles = _activeComponentImplicitStyles;
            previous.MarkupSource = _activeMarkupSource;
            previous.ElementPathPrefix =
                _activeMarkupElementPathPrefix;
            previous.MarkupAssembly = _activeMarkupAssembly;

            ItemTemplateDeclarationContext context =
                host == null
                    ? null
                    : host.TemplateContext as
                        ItemTemplateDeclarationContext;

            if (context != null)
            {
                _activeComponentNamedStyles = context.NamedStyles;
                _activeComponentImplicitStyles = context.ImplicitStyles;
                _activeMarkupSource = context.MarkupSource;
                _activeMarkupElementPathPrefix =
                    context.ElementPathPrefix;
                _activeMarkupAssembly = context.MarkupAssembly;
            }

            return previous;
        }

        private void RestoreItemTemplateDeclarationContext(
            ItemTemplateActiveContext context)
        {
            _activeComponentNamedStyles = context.NamedStyles;
            _activeComponentImplicitStyles = context.ImplicitStyles;
            _activeMarkupSource = context.MarkupSource;
            _activeMarkupElementPathPrefix =
                context.ElementPathPrefix;
            _activeMarkupAssembly = context.MarkupAssembly;
        }

        private Control BuildTemplateControlCore(
            ItemsControl host,
            XmlElement templateRoot,
            object dataContext,
            Hashtable functionResults,
            out ArrayList bindingSlots)
        {
            bindingSlots = new ArrayList();
            object bindingDataContext =
                GetItemDataContext(dataContext);

            // Compile the static template shape/binding descriptors once. Per item we
            // clone only the already-annotated tree instead of cloning and rescanning
            // two XML trees on every realization.
            CompiledItemTemplate compiled =
                GetCompiledItemTemplate(host, templateRoot);

            if (IsControlBlueprintCurrent(compiled.ControlBlueprint))
            {
                return BuildTemplateControlFromBlueprint(
                    host,
                    compiled,
                    dataContext,
                    bindingDataContext,
                    functionResults,
                    out bindingSlots);
            }

            string compiledRootCondition =
                GetAttributeIgnoreNamespace(
                    compiled.AnnotatedRoot,
                    "Condition");
            bool retainDynamicRootCondition =
                !String.IsNullOrEmpty(compiledRootCondition) &&
                ContainsDynamicExpression(compiledRootCondition);
            ArrayList compiledRootIncludeConditions =
                GetConditionalIncludeAttributes(compiled.AnnotatedRoot);
            ArrayList retainedRootIncludeConditions = new ArrayList();
            int includeConditionIndex;

            for (includeConditionIndex = 0;
                 compiledRootIncludeConditions != null &&
                 includeConditionIndex < compiledRootIncludeConditions.Count;
                 includeConditionIndex++)
            {
                XmlAttribute includeCondition =
                    compiledRootIncludeConditions[includeConditionIndex]
                        as XmlAttribute;

                if (includeCondition != null &&
                    ContainsDynamicExpression(includeCondition.Value))
                {
                    retainedRootIncludeConditions.Add(
                        includeCondition.LocalName);
                }
            }

            XmlElement copy =
                (XmlElement)compiled.AnnotatedRoot.CloneNode(true);
            Hashtable elementMap = new Hashtable();
            bool useCompiledElementMap =
                compiled.BindingDefinitions != null &&
                compiled.BindingDefinitions.Count >=
                    CompiledTemplateElementMapThreshold;

            if (useCompiledElementMap)
            {
                IndexCompiledTemplateElements(
                    copy,
                    elementMap);
            }

            Hashtable previousCache =
                _activeFunctionResultCache;

            _activeFunctionResultCache =
                functionResults;

            ArrayList evaluatedBindingValues = new ArrayList();

            try
            {
                ApplyCompiledTemplateBindings(
                    copy,
                    compiled,
                    bindingDataContext,
                    evaluatedBindingValues,
                    useCompiledElementMap
                        ? elementMap
                        : null);
            }
            finally
            {
                _activeFunctionResultCache =
                    previousCache;
            }

            // Reuse the same per-row table for the XML-path-to-Control map built
            // below. Binding-heavy templates avoid repeated sibling walks without
            // paying for a second Hashtable allocation.
            elementMap.Clear();

            if (retainDynamicRootCondition)
                RemoveAttributeIgnoreNamespace(copy, "Condition");

            // ApplyCompiledTemplateBindings evaluates root conditions before the
            // Control exists. Remove the evaluated metadata from the build copy
            // so BuildElement retains the root; the compiled Condition slots
            // then own visibility exactly like an authored root Condition.
            for (includeConditionIndex = 0;
                 includeConditionIndex < retainedRootIncludeConditions.Count;
                 includeConditionIndex++)
            {
                RemoveAttributeIgnoreNamespace(
                    copy,
                    retainedRootIncludeConditions[includeConditionIndex]
                        as string);
            }

            object result;
            Hashtable previousMap = _activeTemplateElementMap;
            object previousDataContext = _activeTemplateDataContext;
            ArrayList previousStyleSlots =
                _activeTemplateStyleBindingSlots;
            CompiledItemTemplate previousCompiledTemplate =
                _activeCompiledItemTemplate;
            Hashtable previousBuildFunctionCache =
                _activeFunctionResultCache;
            ArrayList styleSlots = new ArrayList();
            _activeTemplateElementMap = elementMap;
            _activeTemplateDataContext = bindingDataContext;
            _activeTemplateStyleBindingSlots = styleSlots;
            _activeCompiledItemTemplate = compiled;
            _activeFunctionResultCache = functionResults;

            _templateBuildDepth++;

            try
            {
                result =
                    BuildElement(copy);
            }
            finally
            {
                _templateBuildDepth--;
                _activeTemplateElementMap = previousMap;
                _activeTemplateDataContext = previousDataContext;
                _activeTemplateStyleBindingSlots = previousStyleSlots;
                _activeCompiledItemTemplate = previousCompiledTemplate;
                _activeFunctionResultCache = previousBuildFunctionCache;
            }

            if (result == null)
                return null;

            Control control =
                result as Control;

            if (control == null)
            {
                ReleaseCreatedElement(result);
                throw new InvalidOperationException(
                    "ItemsControl template root must create a WinForms Control.");
            }

            try
            {
                RegisteredComponent rootComponent;

                // Registered XML components return their visual root before the
                // normal element-map hook runs for the invocation element. Map the
                // ItemTemplate path explicitly so root Condition slots retain a
                // concrete target and participate in the same layered state as the
                // component template's own Condition.
                if (TryGetRegisteredComponent(
                        compiled.AnnotatedRoot.LocalName,
                        out rootComponent) &&
                    rootComponent.TemplateXml != null)
                {
                    string rootPath = GetAttributeIgnoreNamespace(
                        compiled.AnnotatedRoot,
                        "__WfxPath");

                    if (!String.IsNullOrEmpty(rootPath))
                        elementMap[rootPath] = control;
                }

                bindingSlots = CreateTemplateBindingSlotsFromDefinitions(
                    compiled.BindingDefinitions,
                    elementMap,
                    bindingDataContext,
                    host == null
                        ? _activeComponentEventTarget
                        : host.TemplateEventTarget,
                    functionResults,
                    evaluatedBindingValues);

                bindingSlots.AddRange(styleSlots);

                ApplyDataContextToTree(
                    control,
                    dataContext);

                if (host != null)
                    host.RecordItemTemplateFallbackBuild();

                return control;
            }
            catch
            {
                ReleaseCreatedElement(control);
                throw;
            }
        }

        private CompiledItemTemplate GetCompiledItemTemplate(
            ItemsControl host,
            XmlElement templateRoot)
        {
            if (templateRoot == null)
                return null;

            CompiledItemTemplate compiled;

            if (_compiledItemTemplates != null &&
                _compiledItemTemplates.TryGetValue(
                    templateRoot,
                    out compiled))
            {
                return compiled;
            }

            compiled = new CompiledItemTemplate();
            compiled.AnnotatedRoot =
                (XmlElement)templateRoot.CloneNode(true);
            compiled.StyleScopesByElementPath =
                new Hashtable(StringComparer.Ordinal);
            compiled.LoadedPresetElements = new ArrayList();

            AnnotateTemplateElementPaths(
                compiled.AnnotatedRoot,
                "0");

            try
            {
                ItemTemplateStyleScope inheritedScope =
                    new ItemTemplateStyleScope();
                ItemTemplateDeclarationContext declarationContext =
                    host == null
                        ? null
                        : host.TemplateContext as
                            ItemTemplateDeclarationContext;

                inheritedScope.NamedStyles =
                    new Dictionary<string, StyleDefinition>(
                        declarationContext == null
                            ? GetCurrentNamedStyles()
                            : declarationContext.NamedStyles,
                        StringComparer.OrdinalIgnoreCase);
                inheritedScope.ImplicitStyles =
                    new List<StyleDefinition>(
                        declarationContext == null
                            ? GetCurrentImplicitStyles()
                            : declarationContext.ImplicitStyles);

                CompileItemTemplateStyleScopes(
                    compiled.AnnotatedRoot,
                    inheritedScope,
                    compiled);
                ImportCompiledItemTemplatePresets(
                    compiled.AnnotatedRoot,
                    compiled);

                compiled.BindingDefinitions =
                    CompileTemplateBindingDefinitions(
                        compiled.AnnotatedRoot);
                compiled.ControlBlueprint =
                    TryCompileControlBlueprint(compiled);

                if (_compiledItemTemplates == null)
                {
                    _compiledItemTemplates =
                        new Dictionary<XmlElement, CompiledItemTemplate>();
                }

                _compiledItemTemplates[templateRoot] = compiled;
                return compiled;
            }
            catch
            {
                ReleaseCompiledItemTemplateState(compiled);
                throw;
            }
        }

        private void ReleaseCompiledItemTemplate(
            XmlElement templateRoot)
        {
            if (templateRoot == null || _compiledItemTemplates == null)
                return;

            CompiledItemTemplate compiled;

            if (!_compiledItemTemplates.TryGetValue(
                    templateRoot,
                    out compiled))
            {
                return;
            }

            _compiledItemTemplates.Remove(templateRoot);
            ReleaseCompiledItemTemplateState(compiled);
        }

        private void ReleaseAllCompiledItemTemplates()
        {
            if (_compiledItemTemplates == null)
                return;

            ArrayList compiledTemplates = new ArrayList();

            foreach (CompiledItemTemplate compiled in
                _compiledItemTemplates.Values)
            {
                compiledTemplates.Add(compiled);
            }

            _compiledItemTemplates.Clear();

            int i;

            for (i = 0; i < compiledTemplates.Count; i++)
            {
                ReleaseCompiledItemTemplateState(
                    compiledTemplates[i] as CompiledItemTemplate);
            }
        }

        private void ReleaseCompiledItemTemplateState(
            CompiledItemTemplate compiled)
        {
            if (compiled == null)
                return;

            if (compiled.LoadedPresetElements != null &&
                _loadedPresetElements != null)
            {
                int i;

                for (i = 0;
                     i < compiled.LoadedPresetElements.Count;
                     i++)
                {
                    XmlElement presetElement =
                        compiled.LoadedPresetElements[i] as XmlElement;

                    if (presetElement != null)
                        _loadedPresetElements.Remove(presetElement);
                }
            }

            if (compiled.StyleScopesByElementPath != null)
                compiled.StyleScopesByElementPath.Clear();

            if (compiled.LoadedPresetElements != null)
                compiled.LoadedPresetElements.Clear();

            compiled.AnnotatedRoot = null;
            compiled.BindingDefinitions = null;
            compiled.StyleScopesByElementPath = null;
            compiled.LoadedPresetElements = null;
            compiled.ControlBlueprint = null;
        }

        private ItemTemplateStyleScope
            GetCompiledItemTemplateStyleScope(XmlElement element)
        {
            if (element == null ||
                _activeCompiledItemTemplate == null ||
                _activeCompiledItemTemplate.StyleScopesByElementPath == null)
            {
                return null;
            }

            string path = GetAttributeIgnoreNamespace(
                element,
                "__WfxPath");

            if (String.IsNullOrEmpty(path))
                return null;

            return _activeCompiledItemTemplate
                .StyleScopesByElementPath[path] as ItemTemplateStyleScope;
        }

        private void CompileItemTemplateStyleScopes(
            XmlElement element,
            ItemTemplateStyleScope inheritedScope,
            CompiledItemTemplate compiled)
        {
            if (element == null ||
                inheritedScope == null ||
                compiled == null)
            {
                return;
            }

            if (IsNestedItemsTemplateContainer(element))
                return;

            ItemTemplateStyleScope scope = inheritedScope;
            bool ownsScope = false;
            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null && IsPropertyElement(child))
                {
                    string propertyName =
                        GetPropertyElementName(child.LocalName);

                    if (EqualsIgnoreCase(propertyName, "Resources"))
                    {
                        if (!ownsScope)
                        {
                            scope = CloneItemTemplateStyleScope(
                                inheritedScope);
                            ownsScope = true;
                        }

                        try
                        {
                            ReadResources(
                                child,
                                scope.NamedStyles,
                                scope.ImplicitStyles);
                        }
                        catch (WinFormsXamlLoadException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw CreateMarkupLoadException(
                                child,
                                "Resources",
                                ex);
                        }
                    }
                }

                node = node.NextSibling;
            }

            string path = GetAttributeIgnoreNamespace(
                element,
                "__WfxPath");

            if (!String.IsNullOrEmpty(path))
                compiled.StyleScopesByElementPath[path] = scope;

            node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null &&
                    !IsPresetDefinitionElement(child) &&
                    !IsTemplateResourcesPropertyElement(child) &&
                    !IsNestedItemsTemplateContainer(child))
                {
                    CompileItemTemplateStyleScopes(
                        child,
                        scope,
                        compiled);
                }

                node = node.NextSibling;
            }
        }

        private static ItemTemplateStyleScope
            CloneItemTemplateStyleScope(ItemTemplateStyleScope source)
        {
            ItemTemplateStyleScope clone =
                new ItemTemplateStyleScope();

            clone.NamedStyles =
                new Dictionary<string, StyleDefinition>(
                    source.NamedStyles,
                    StringComparer.OrdinalIgnoreCase);
            clone.ImplicitStyles =
                new List<StyleDefinition>(source.ImplicitStyles);

            return clone;
        }

        private static bool IsTemplateResourcesPropertyElement(
            XmlElement element)
        {
            return
                element != null &&
                IsPropertyElement(element) &&
                EqualsIgnoreCase(
                    GetPropertyElementName(element.LocalName),
                    "Resources");
        }

        private void ImportCompiledItemTemplatePresets(
            XmlElement element,
            CompiledItemTemplate compiled)
        {
            if (element == null ||
                compiled == null ||
                IsNestedItemsTemplateContainer(element))
            {
                return;
            }

            if (!IsPropertyElement(element))
            {
                ImportCompiledPresetChildren(
                    element,
                    compiled);
            }

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null &&
                    !IsPresetDefinitionElement(child) &&
                    !IsTemplateResourcesPropertyElement(child) &&
                    !IsNestedItemsTemplateContainer(child))
                {
                    ImportCompiledItemTemplatePresets(
                        child,
                        compiled);
                }

                node = node.NextSibling;
            }
        }

        private void ImportCompiledPresetChildren(
            XmlElement owner,
            CompiledItemTemplate compiled)
        {
            XmlNode node = owner == null ? null : owner.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (IsPresetDefinitionElement(child))
                {
                    LoadCompiledItemTemplatePreset(child, compiled);
                }
                else if (IsTemplateResourcesPropertyElement(child))
                {
                    ImportCompiledPresetResourceChildren(
                        child,
                        compiled);
                }

                node = node.NextSibling;
            }
        }

        private void ImportCompiledPresetResourceChildren(
            XmlElement resources,
            CompiledItemTemplate compiled)
        {
            XmlNode node = resources == null
                ? null
                : resources.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (IsPresetDefinitionElement(child))
                {
                    LoadCompiledItemTemplatePreset(child, compiled);
                }
                else if (IsTemplateResourcesPropertyElement(child))
                {
                    ImportCompiledPresetResourceChildren(
                        child,
                        compiled);
                }

                node = node.NextSibling;
            }
        }

        private void LoadCompiledItemTemplatePreset(
            XmlElement element,
            CompiledItemTemplate compiled)
        {
            if (element == null ||
                compiled == null ||
                _loadedPresetElements.ContainsKey(element))
            {
                return;
            }

            // Record ownership before import so a failed compilation can release
            // the identity entry installed by LoadPresetDefinition as well.
            compiled.LoadedPresetElements.Add(element);

            try
            {
                LoadPresetDefinition(element);
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    element,
                    HasAttributeIgnoreNamespace(element, "Source")
                        ? "Source"
                        : null,
                    ex);
            }
        }

        private static void RemoveAttributeIgnoreNamespace(
            XmlElement element,
            string localName)
        {
            if (element == null || String.IsNullOrEmpty(localName))
                return;

            int i;

            for (i = element.Attributes.Count - 1; i >= 0; i--)
            {
                XmlAttribute attribute = element.Attributes[i];

                if (EqualsIgnoreCase(attribute.LocalName, localName))
                    element.Attributes.RemoveAt(i);
            }
        }

        private static void AnnotateTemplateElementPaths(
            XmlElement element,
            string path)
        {
            if (element == null)
                return;

            element.SetAttribute("__WfxPath", path);

            int elementIndex = 0;
            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    AnnotateTemplateElementPaths(
                        child,
                        path + "." +
                        elementIndex.ToString(CultureInfo.InvariantCulture));

                    elementIndex++;
                }

                node = node.NextSibling;
            }
        }
    }
}
