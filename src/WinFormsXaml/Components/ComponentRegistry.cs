using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private sealed class ComponentPropertyDefinition
        {
            public int Index;
            public string Name;
            public Type Type;
            public ConstructorInfo ValueProxyConstructor;
            public string DefaultValue;
            public bool HasDefaultValue;
            public bool Required;
        }

        private sealed class RegisteredComponent
        {
            public string Name;
            public Type ComponentType;
            public ComponentConstructorCandidate[] ComponentConstructors;
            public Type CodeBehindType;
            public ConstructorInfo CodeBehindConstructor;
            public ComponentCodeMember ChildrenMember;
            public Dictionary<string, ComponentCodeMember>
                CodeBehindPropertyMembers;
            public Assembly ResourceAssembly;
            public string ResourceName;
            public string TemplateXml;
            public ComponentPropertyDefinition[] Properties;
            public Dictionary<string, ComponentPropertyDefinition>
                PropertiesByName;
            public bool HasChildrenSlot;
        }

        private sealed class ComponentConstructorCandidate
        {
            public ConstructorInfo Constructor;
            public ParameterInfo[] Parameters;
        }

        private sealed class ParsedComponentTemplate
        {
            public string TemplateXml;
            public XmlElement Root;
        }

        private sealed class ComponentContentProjection
        {
            public object DataContext;
            public object EventTarget;
            public string MarkupSource;
            public string ElementPathPrefix;
            public Assembly MarkupAssembly;
            public int ComponentBuildDepth;
            public ComponentChildrenHost ChildrenHost;
        }

        private sealed class ComponentCodeMember
        {
            public FieldInfo Field;
            public PropertyInfo Property;
            public bool UsesBindingProxy;

            public Type MemberType
            {
                get
                {
                    return Field == null
                        ? Property.PropertyType
                        : Field.FieldType;
                }
            }
        }

        private sealed class ComponentPropertyValue
        {
            public ComponentPropertyDefinition Definition;
            public string ComponentName;
            public string Expression;
            public bool Dynamic;
            public BindingMode Mode;
            public object ValueProxy;
            public ComponentInstanceState OwnerState;
            public ObservableBindingRegistration ObservableRegistration;
            public bool HasInitialObservableSnapshot;
            public BindingExpressionPlan InitialDirectPlan;
            public BindingPathResult InitialPathResult;
            public object CodeBehind;
            public ComponentCodeMember CodeMember;
        }

        private sealed class ComponentValueContext : Hashtable
        {
            public bool MayUsePresets;
            public object CodeBehind;

            public ComponentValueContext()
                : base(StringComparer.OrdinalIgnoreCase)
            {
            }
        }

        private sealed class ComponentInstanceState
        {
            public object Root;
            public object ParentDataContext;
            public object ParentEventTarget;
            public ComponentValueContext Values;
            public ArrayList Properties;
            public bool PendingBindingRefresh;
            public EventHandler RootDisposedHandler;
            public object CodeBehind;
            public ChildrenBind Children;
            public ComponentChildrenHost ChildrenHost;
            public bool CodeBehindDisposed;
            public bool Releasing;
            public bool Tracked;
            public int InstanceIndex;
        }

        private sealed class ComponentChildrenMarker
        {
            public ComponentChildrenHost Host;
        }

        private sealed class ComponentChildrenHost : IChildrenBindHost
        {
            public XamlRuntime Runtime;
            public ComponentInstanceState State;
            public Control Parent;
            public XmlElement SlotElement;
            public int SlotIndex;
            public readonly ArrayList ProjectedChildren = new ArrayList();
            public bool Attached;
            public bool Retired;
            public bool Mutating;

            public void ReplaceChildren(
                ChildrenBind owner,
                Control[] children)
            {
                Runtime.ReplaceComponentChildren(
                    this,
                    owner,
                    children);
            }

            public Control WrapChildren(
                ChildrenBind owner,
                Control wrapper)
            {
                return Runtime.WrapComponentChildren(
                    this,
                    owner,
                    wrapper);
            }
        }

        private static readonly object _componentRegistrySync = new object();
        private static readonly Hashtable _registeredComponents =
            new Hashtable(StringComparer.OrdinalIgnoreCase);
        private static volatile int _componentRegistryVersion;
        private const int ComponentTemplateCacheLimit = 128;

        private ArrayList _componentInstances;
        private Hashtable _componentInstancesByRoot;
        private ArrayList _activeXmlComponentBuildChain;
        private readonly object _componentTemplateCacheSync = new object();
        private Hashtable _componentTemplateCache;
        private object _activeComponentDataContext;
        private int _componentBuildDepth;
        private Dictionary<string, StyleDefinition> _activeComponentNamedStyles;
        private List<StyleDefinition> _activeComponentImplicitStyles;
        private Hashtable _componentContentProjections;
        private Hashtable _componentChildrenSlotMarkers;
        private int _componentContentProjectionDepth;
        private XmlElement _activeComponentContentRoot;
        private object _activeComponentEventTarget;

        /// <summary>
        /// Registers a CLR component class under an XML element name.
        /// Registration is global to the current AppDomain.
        /// </summary>
        public static void Register(string name, Type componentType)
        {
            ValidateComponentName(name);

            if (componentType == null)
                throw new ArgumentNullException("componentType");

            if (!componentType.IsClass ||
                componentType.IsAbstract ||
                componentType.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    "A registered component type must be a concrete, closed class.",
                    "componentType");
            }

            ConstructorInfo[] constructors =
                componentType.GetConstructors();

            if (constructors.Length == 0)
            {
                throw new ArgumentException(
                    "Registered component type '" +
                    componentType.FullName +
                    "' has no public constructor.",
                    "componentType");
            }

            RegisteredComponent component = new RegisteredComponent();
            component.Name = name;
            component.ComponentType = componentType;
            component.ComponentConstructors =
                CreateComponentConstructorCandidates(constructors);

            AddRegisteredComponent(component);
        }

        private static ComponentConstructorCandidate[]
            CreateComponentConstructorCandidates(
                ConstructorInfo[] constructors)
        {
            ComponentConstructorCandidate[] candidates =
                new ComponentConstructorCandidate[constructors.Length];
            int i;

            for (i = 0; i < constructors.Length; i++)
            {
                ComponentConstructorCandidate candidate =
                    new ComponentConstructorCandidate();
                candidate.Constructor = constructors[i];
                candidate.Parameters = constructors[i].GetParameters();
                candidates[i] = candidate;
            }

            return candidates;
        }

        /// <summary>
        /// Inspects every embedded XML resource in the calling assembly and
        /// registers documents whose root is Component or Includes.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register()
        {
            Register(
                Assembly.GetCallingAssembly(),
                String.Empty);
        }

        /// <summary>
        /// Registers embedded XML components and reusable includes. An empty or
        /// whitespace fragment
        /// inspects every embedded .xml resource. An exact resource name
        /// registers that resource strictly. Otherwise every embedded .xml
        /// resource whose manifest path contains the supplied fragment is
        /// inspected; well-formed documents without a Component or Includes
        /// root are ignored. Each retained component name is the final
        /// resource-name segment before the .xml extension.
        /// </summary>
        public static void Register(
            Assembly assembly,
            string resourceNameOrFragment)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly");

            bool registerAll =
                String.IsNullOrEmpty(resourceNameOrFragment) ||
                resourceNameOrFragment.Trim().Length == 0;

            bool exactResource;
            string[] resourceNames;

            if (registerAll)
            {
                exactResource = false;
                resourceNames =
                    FindAllEmbeddedXmlResources(assembly);
            }
            else
            {
                resourceNames =
                    FindEmbeddedComponentResources(
                    assembly,
                    resourceNameOrFragment,
                    out exactResource);
            }
            ArrayList componentResourceNames = new ArrayList();
            ArrayList includeResourceNames = new ArrayList();
            int resourceIndex;

            // Classify the complete batch first. Includes are staged before any
            // component template is composed, so manifest enumeration order can
            // never decide whether a component can see an include.
            for (resourceIndex = 0;
                 resourceIndex < resourceNames.Length;
                 resourceIndex++)
            {
                string resourceName = resourceNames[resourceIndex];
                EmbeddedMarkupRootKind rootKind =
                    GetEmbeddedMarkupRootKind(
                        assembly,
                        resourceName);

                if (rootKind == EmbeddedMarkupRootKind.Component)
                {
                    componentResourceNames.Add(resourceName);
                }
                else if (rootKind == EmbeddedMarkupRootKind.Includes)
                {
                    includeResourceNames.Add(resourceName);
                }
                else if (exactResource)
                {
                    throw new InvalidOperationException(
                        "Embedded XML resource '" + resourceName +
                        "' in assembly '" + assembly.FullName +
                        "' must have a <Component> or <Includes> root.");
                }
            }

            ArrayList includes = new ArrayList(
                includeResourceNames.Count);

            for (resourceIndex = 0;
                 resourceIndex < includeResourceNames.Count;
                 resourceIndex++)
            {
                includes.Add(
                    ReadEmbeddedInclude(
                        assembly,
                        includeResourceNames[resourceIndex] as string));
            }

            Hashtable stagedIncludesByAssembly =
                CreateStagedIncludeIndex(includes);
            ValidateStagedIncludes(
                includes,
                stagedIncludesByAssembly);

            ArrayList components = new ArrayList(
                componentResourceNames.Count);
            Hashtable names =
                new Hashtable(StringComparer.OrdinalIgnoreCase);
            int i;

            for (i = 0; i < componentResourceNames.Count; i++)
            {
                string resourceName =
                    componentResourceNames[i] as string;
                string name = null;

                // A complete manifest resource name is an explicit request and
                // retains strict component validation. A fragment is a folder-like
                // glob: well-formed Form, preset, and other XML documents in the
                // same path are intentionally ignored.
                if (exactResource)
                {
                    name = GetComponentNameFromResource(resourceName);
                    ValidateComponentName(name);
                }

                RegisteredComponent component =
                    ReadEmbeddedComponent(
                        assembly,
                        resourceName,
                        false,
                        stagedIncludesByAssembly);

                if (name == null)
                {
                    name = GetComponentNameFromResource(resourceName);
                    ValidateComponentName(name);
                }

                if (names.ContainsKey(name))
                {
                    string previousResource = names[name] as string;

                    throw new InvalidOperationException(
                        "Embedded component registration for " +
                        (registerAll
                            ? "all embedded XML resources"
                            : "fragment '" +
                              resourceNameOrFragment +
                              "'") +
                        " in assembly '" +
                        assembly.FullName +
                        "' resolves more than one component named '" +
                        name +
                        "': '" +
                        previousResource +
                        "' and '" +
                        resourceName +
                        "'. Use a narrower resource path fragment.");
                }

                component.Name = name;
                names.Add(name, resourceName);
                components.Add(component);
            }

            AddRegisteredResources(
                components,
                includes);
        }

        private static string[] FindAllEmbeddedXmlResources(
            Assembly assembly)
        {
            string[] resources = GetEmbeddedResourceNames(assembly);
            ArrayList matches = new ArrayList();
            int i;

            for (i = 0; i < resources.Length; i++)
            {
                string candidate = resources[i];

                if (candidate.EndsWith(
                        ".xml",
                        StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(candidate);
                }
            }

            matches.Sort(_embeddedXmlResourceNameComparer);
            return (string[])matches.ToArray(typeof(string));
        }

        /// <summary>
        /// Registers embedded XML components from the calling assembly. An
        /// empty or whitespace fragment has the same scan-all behavior as the
        /// parameterless overload.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register(string resourceNameOrFragment)
        {
            Register(
                Assembly.GetCallingAssembly(),
                resourceNameOrFragment);
        }

        private static string[] FindEmbeddedComponentResources(
            Assembly assembly,
            string resourceNameOrFragment,
            out bool exactResource)
        {
            exactResource = false;
            string query = NormalizeResourceFragment(
                resourceNameOrFragment);

            if (query.Length == 0)
            {
                throw new ArgumentException(
                    "An embedded component resource name or path fragment is required.",
                    "resourceNameOrFragment");
            }

            string[] resources = GetEmbeddedResourceNames(assembly);
            int i;

            for (i = 0; i < resources.Length; i++)
            {
                if (String.Equals(
                        resources[i],
                        query,
                        StringComparison.Ordinal))
                {
                    exactResource = true;
                    return new string[] { resources[i] };
                }
            }

            ArrayList caseInsensitiveExactMatches = new ArrayList();

            for (i = 0; i < resources.Length; i++)
            {
                if (String.Equals(
                        resources[i],
                        query,
                        StringComparison.OrdinalIgnoreCase))
                {
                    caseInsensitiveExactMatches.Add(resources[i]);
                }
            }

            if (caseInsensitiveExactMatches.Count == 1)
            {
                exactResource = true;
                return new string[]
                {
                    (string)caseInsensitiveExactMatches[0]
                };
            }

            if (caseInsensitiveExactMatches.Count > 1)
            {
                throw new InvalidOperationException(
                    "Embedded XML component resource name '" +
                    resourceNameOrFragment +
                    "' is ambiguous in assembly '" +
                    assembly.FullName +
                    "'. Candidates: " +
                    FormatEmbeddedXmlResourceCandidates(
                        caseInsensitiveExactMatches) +
                    ". Use the exact manifest resource name and casing.");
            }

            ArrayList matches = new ArrayList();

            for (i = 0; i < resources.Length; i++)
            {
                string candidate = resources[i];

                if (candidate.EndsWith(
                        ".xml",
                        StringComparison.OrdinalIgnoreCase) &&
                    candidate.IndexOf(
                        query,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(candidate);
                }
            }

            if (matches.Count == 0)
            {
                throw new InvalidOperationException(
                    "No embedded XML component resource containing '" +
                    resourceNameOrFragment +
                    "' was found in assembly '" +
                    assembly.FullName +
                    "'" +
                    ". Available embedded XML resources: " +
                    FormatEmbeddedXmlResourceCandidates(resources) +
                    ".");
            }

            matches.Sort(_embeddedXmlResourceNameComparer);
            return (string[])matches.ToArray(typeof(string));
        }

        private static void AddRegisteredComponent(
            RegisteredComponent component)
        {
            ArrayList components = new ArrayList(1);
            components.Add(component);
            AddRegisteredComponents(components);
        }

        private static void AddRegisteredComponents(
            ArrayList components)
        {
            AddRegisteredResources(
                components,
                null);
        }

        private static int GetComponentRegistryVersion()
        {
            // Registration writes remain serialized by _componentRegistrySync.
            // Blueprint realization reads this generation for every new row;
            // a volatile read avoids putting the global registry lock on that
            // hot path while still invalidating plans after publication.
            return _componentRegistryVersion;
        }

        private static bool TryGetRegisteredComponent(
            string name,
            out RegisteredComponent component)
        {
            lock (_componentRegistrySync)
            {
                component =
                    _registeredComponents[name] as
                        RegisteredComponent;
            }

            return component != null;
        }

        private static string DescribeComponentRegistration(
            RegisteredComponent component)
        {
            if (component == null)
                return "an unknown source";

            if (component.ComponentType != null)
            {
                return "CLR type '" +
                    component.ComponentType.FullName +
                    "' in assembly '" +
                    component.ComponentType.Assembly.FullName +
                    "'";
            }

            return DescribeEmbeddedComponentResource(
                component.ResourceAssembly,
                component.ResourceName);
        }

        private static string DescribeEmbeddedComponentResource(
            Assembly assembly,
            string resourceName)
        {
            return "embedded XML resource '" +
                resourceName +
                "' in assembly '" +
                (assembly == null
                    ? "<unknown>"
                    : assembly.FullName) +
                "'";
        }

        private XmlDocument CloneRegisteredComponentTemplateDocument(
            RegisteredComponent component)
        {
            XmlElement cachedRoot =
                GetParsedRegisteredComponentTemplate(component);
            XmlDocument document = new XmlDocument();
            document.PreserveWhitespace = false;
            document.XmlResolver = null;
            document.AppendChild(
                document.ImportNode(cachedRoot, true));
            return document;
        }

        private XmlElement GetParsedRegisteredComponentTemplate(
            RegisteredComponent component)
        {
            if (component == null)
                throw new ArgumentNullException("component");

            string templateXml = component.TemplateXml;

            if (templateXml == null)
            {
                throw new InvalidOperationException(
                    "Registered component '" + component.Name +
                    "' does not have an XML template.");
            }

            lock (_componentTemplateCacheSync)
            {
                if (_componentTemplateCache == null)
                {
                    _componentTemplateCache =
                        new Hashtable(_observableReferenceComparer);
                }

                ParsedComponentTemplate cached =
                    _componentTemplateCache[component] as
                        ParsedComponentTemplate;

                if (cached != null &&
                    String.Equals(
                        cached.TemplateXml,
                        templateXml,
                        StringComparison.Ordinal))
                {
                    return cached.Root;
                }

                XmlDocument document = new XmlDocument();
                document.PreserveWhitespace = false;
                document.XmlResolver = null;
                document.LoadXml(templateXml);

                ParsedComponentTemplate parsed =
                    new ParsedComponentTemplate();
                parsed.TemplateXml = templateXml;
                parsed.Root = document.DocumentElement;
                if (_componentTemplateCache.Count <
                    ComponentTemplateCacheLimit)
                {
                    _componentTemplateCache[component] = parsed;
                }

                return parsed.Root;
            }
        }

        private static void ValidateComponentName(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException(
                    "A component element name is required.",
                    "name");
            }

            if (name.IndexOf(':') >= 0 || name.IndexOf('.') >= 0)
            {
                throw new ArgumentException(
                    "A component element name cannot contain ':' or '.'.",
                    "name");
            }

            try
            {
                XmlConvert.VerifyName(name);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "'" + name + "' is not a valid XML element name.",
                    "name",
                    ex);
            }

            if (IsReservedComponentName(name))
            {
                throw new ArgumentException(
                    "'" + name + "' is a built-in WinFormsXaml element name.",
                    "name");
            }

            Type nativeType =
                typeof(Control).Assembly.GetType(
                    "System.Windows.Forms." + name,
                    false,
                    true);

            if (nativeType != null)
            {
                throw new ArgumentException(
                    "'" + name + "' is a native Windows Forms type name and cannot be replaced.",
                    "name");
            }
        }

        private static bool IsReservedComponentName(string name)
        {
            return EqualsIgnoreCase(name, "Grid") ||
                EqualsIgnoreCase(name, "StackPanel") ||
                EqualsIgnoreCase(name, "ItemsControl") ||
                EqualsIgnoreCase(name, "ProgressBar") ||
                EqualsIgnoreCase(name, "FlexPanel") ||
                EqualsIgnoreCase(name, "DockPanel") ||
                EqualsIgnoreCase(name, "Canvas") ||
                EqualsIgnoreCase(name, "Border") ||
                EqualsIgnoreCase(name, "ScrollViewer") ||
                EqualsIgnoreCase(name, "Viewbox") ||
                EqualsIgnoreCase(name, "Children") ||
                EqualsIgnoreCase(name, "Includes") ||
                EqualsIgnoreCase(name, "Object") ||
                EqualsIgnoreCase(name, "Control");
        }

        private static string GetComponentNameFromResource(
            string resourceName)
        {
            string name = resourceName.Trim();

            if (!name.EndsWith(
                ".xml",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "An embedded component resource must use the .xml extension.",
                    "resourceName");
            }

            name = name.Substring(0, name.Length - 4);

            int dot = name.LastIndexOf('.');
            int slash = Math.Max(
                name.LastIndexOf('/'),
                name.LastIndexOf('\\'));
            int separator = Math.Max(dot, slash);

            if (separator >= 0)
                name = name.Substring(separator + 1);

            if (name.Length == 0)
            {
                throw new ArgumentException(
                    "The embedded component resource name does not contain a component name.",
                    "resourceName");
            }

            return name;
        }

        private static RegisteredComponent ReadEmbeddedComponent(
            Assembly assembly,
            string resourceName,
            bool allowNonComponentRoot,
            Hashtable stagedIncludesByAssembly)
        {
            string resourceDescription =
                DescribeEmbeddedComponentResource(
                    assembly,
                    resourceName);

            if (allowNonComponentRoot &&
                !EmbeddedResourceHasComponentRoot(
                    assembly,
                    resourceName,
                    resourceDescription))
            {
                return null;
            }

            Stream stream =
                assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new InvalidOperationException(
                    "The " +
                    resourceDescription +
                    " was not found.");
            }

            MarkupXmlDocument document =
                new MarkupXmlDocument();
            document.PreserveWhitespace = false;
            document.XmlResolver = null;

            try
            {
                using (stream)
                    document.LoadMarkup(stream);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "The " +
                    resourceDescription +
                    " is not valid XML: " +
                    ex.Message,
                    ex);
            }

            XmlElement wrapper = document.DocumentElement;

            if (wrapper == null ||
                !EqualsIgnoreCase(wrapper.LocalName, "Component"))
            {
                if (allowNonComponentRoot && wrapper != null)
                    return null;

                throw new InvalidOperationException(
                    "The " +
                    resourceDescription +
                    " must have a <Component> root.");
            }

            ComposeIncludes(
                document,
                Application.StartupPath,
                assembly,
                resourceName,
                null,
                stagedIncludesByAssembly);

            wrapper = document.DocumentElement;

            ArrayList properties = new ArrayList();
            Hashtable propertyNames =
                new Hashtable(StringComparer.OrdinalIgnoreCase);
            XmlElement visualRoot = null;
            ArrayList metadataBeforeVisual = new ArrayList();
            ArrayList metadataAfterVisual = new ArrayList();
            bool propertiesSeen = false;
            XmlNode node = wrapper.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    if (EqualsIgnoreCase(
                        child.LocalName,
                        "Component.Properties"))
                    {
                        if (propertiesSeen)
                        {
                            throw new InvalidOperationException(
                                "The " +
                                resourceDescription +
                                " contains more than one <Component.Properties> element.");
                        }

                        propertiesSeen = true;
                        ReadComponentProperties(
                            child,
                            assembly,
                            resourceDescription,
                            properties,
                            propertyNames);
                    }
                    else if (IsComponentTemplateMetadata(child))
                    {
                        (visualRoot == null
                            ? metadataBeforeVisual
                            : metadataAfterVisual).Add(child);
                    }
                    else
                    {
                        if (visualRoot != null)
                        {
                            throw new InvalidOperationException(
                                "The " +
                                resourceDescription +
                                " must contain exactly one visual root element.");
                        }

                        visualRoot = child;
                    }
                }
                else if ((node.NodeType == XmlNodeType.Text ||
                          node.NodeType == XmlNodeType.CDATA) &&
                         node.Value != null &&
                         node.Value.Trim().Length != 0)
                {
                    throw new InvalidOperationException(
                        "The " +
                        resourceDescription +
                        " contains text outside its visual root.");
                }

                node = node.NextSibling;
            }

            if (visualRoot == null)
            {
                throw new InvalidOperationException(
                    "The " +
                    resourceDescription +
                    " does not contain a visual root element.");
            }

            PromoteComponentTemplateMetadata(
                document,
                visualRoot,
                metadataBeforeVisual,
                true);
            PromoteComponentTemplateMetadata(
                document,
                visualRoot,
                metadataAfterVisual,
                false);
            MergeSiblingPresetDeclarationsRecursive(visualRoot);
            MergeSiblingResourceDeclarationsRecursive(visualRoot);

            RegisteredComponent component = new RegisteredComponent();
            component.ResourceAssembly = assembly;
            component.ResourceName = resourceName;
            component.CodeBehindType =
                ResolveComponentCodeBehindType(
                    wrapper,
                    assembly,
                    resourceDescription);

            XmlElement templateRoot =
                visualRoot.CloneNode(true) as XmlElement;

            CopyInheritedNamespaceDeclarations(
                wrapper,
                templateRoot);

            component.HasChildrenSlot =
                ValidateComponentChildrenSlot(
                    templateRoot,
                    resourceDescription);
            component.TemplateXml =
                MarkupXmlDocument.SerializeElementWithLocations(
                    templateRoot);
            component.Properties =
                (ComponentPropertyDefinition[])properties.ToArray(
                    typeof(ComponentPropertyDefinition));
            component.PropertiesByName =
                new Dictionary<string, ComponentPropertyDefinition>(
                    StringComparer.OrdinalIgnoreCase);

            int propertyIndex;

            for (propertyIndex = 0;
                 propertyIndex < component.Properties.Length;
                 propertyIndex++)
            {
                ComponentPropertyDefinition definition =
                    component.Properties[propertyIndex];
                definition.Index = propertyIndex;
                component.PropertiesByName.Add(
                    definition.Name,
                    definition);
            }

            CacheComponentCodeBehindMetadata(
                component,
                resourceDescription);

            return component;
        }

        private static bool IsComponentTemplateMetadata(
            XmlElement element)
        {
            return EqualsIgnoreCase(element.LocalName, "Presets") ||
                (IsPropertyElement(element) &&
                 EqualsIgnoreCase(
                     element.LocalName,
                     "Component.Resources"));
        }

        private static void PromoteComponentTemplateMetadata(
            XmlDocument document,
            XmlElement visualRoot,
            ArrayList metadata,
            bool insertBeforeLocalContent)
        {
            XmlNode insertionPoint = insertBeforeLocalContent
                ? visualRoot.FirstChild
                : null;
            int i;

            for (i = 0; i < metadata.Count; i++)
            {
                XmlElement element = metadata[i] as XmlElement;

                if (IsPropertyElement(element) &&
                    EqualsIgnoreCase(
                        element.LocalName,
                        "Component.Resources"))
                {
                    InsertIncludedResources(
                        document,
                        visualRoot,
                        insertionPoint,
                        element);
                    element.ParentNode.RemoveChild(element);
                }
                else
                {
                    visualRoot.InsertBefore(element, insertionPoint);
                }
            }
        }

        private static bool EmbeddedResourceHasComponentRoot(
            Assembly assembly,
            string resourceName,
            string resourceDescription)
        {
            Stream stream =
                assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new InvalidOperationException(
                    "The " +
                    resourceDescription +
                    " was not found.");
            }

            XmlReaderSettings settings =
                new XmlReaderSettings();
            settings.ConformanceLevel = ConformanceLevel.Document;
            settings.IgnoreComments = false;
            settings.IgnoreWhitespace = false;
            // Match MarkupXmlDocument's parser contract. Broad registration
            // must not accept a non-Component document that the authoritative
            // loader would reject, and legacy XmlResolver behavior must never
            // make root inspection an external-resource path.
            settings.ProhibitDtd = true;
            settings.XmlResolver = null;

            try
            {
                using (stream)
                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    bool rootSeen = false;
                    bool componentRoot = false;

                    while (reader.Read())
                    {
                        if (!rootSeen &&
                            reader.NodeType == XmlNodeType.Element)
                        {
                            rootSeen = true;
                            componentRoot = EqualsIgnoreCase(
                                reader.LocalName,
                                "Component");

                            // A Component document is parsed into the location-
                            // preserving DOM below, which performs complete
                            // validation. Other XML is streamed to EOF so a
                            // malformed glob match is not silently ignored.
                            if (componentRoot)
                                return true;
                        }
                    }

                    if (!rootSeen)
                        throw new XmlException("Root element is missing.");

                    return false;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "The " +
                    resourceDescription +
                    " is not valid XML: " +
                    ex.Message,
                    ex);
            }
        }

        private static void CacheComponentCodeBehindMetadata(
            RegisteredComponent component,
            string resourceDescription)
        {
            if (component == null || component.CodeBehindType == null)
                return;

            component.CodeBehindConstructor =
                component.CodeBehindType.GetConstructor(Type.EmptyTypes);

            if (component.CodeBehindConstructor == null)
            {
                throw new InvalidOperationException(
                    "Component Class type '" +
                    component.CodeBehindType.FullName +
                    "' in " +
                    resourceDescription +
                    " needs a public parameterless constructor.");
            }

            if (component.HasChildrenSlot)
            {
                component.ChildrenMember =
                    FindPublicComponentCodeMember(
                        component.CodeBehindType,
                        "Children");

                if (component.ChildrenMember != null &&
                    component.ChildrenMember.MemberType !=
                        typeof(ChildrenBind))
                {
                    throw new InvalidOperationException(
                        "Public Component Class member 'Children' on " +
                        component.CodeBehindType.FullName +
                        " must have type " +
                        typeof(ChildrenBind).FullName +
                        ".");
                }
            }

            component.CodeBehindPropertyMembers =
                new Dictionary<string, ComponentCodeMember>(
                    StringComparer.OrdinalIgnoreCase);

            int i;

            for (i = 0; i < component.Properties.Length; i++)
            {
                ComponentPropertyDefinition definition =
                    component.Properties[i];
                ComponentCodeMember member =
                    FindPublicComponentCodeMember(
                        component.CodeBehindType,
                        definition.Name);

                if (member != null)
                {
                    member.UsesBindingProxy =
                        typeof(IPropertyBindingRuntime).IsAssignableFrom(
                            member.MemberType);
                }

                component.CodeBehindPropertyMembers.Add(
                    definition.Name,
                    member);
            }
        }

        private static Type ResolveComponentCodeBehindType(
            XmlElement wrapper,
            Assembly resourceAssembly,
            string resourceDescription)
        {
            XmlAttribute classAttribute =
                FindAttributeIgnoreNamespace(wrapper, "Class");

            if (classAttribute == null)
                return null;

            string className = classAttribute.Value == null
                ? String.Empty
                : classAttribute.Value.Trim();

            if (className.Length == 0 ||
                className.IndexOf('{') >= 0 ||
                className.IndexOf('}') >= 0)
            {
                throw new InvalidOperationException(
                    "Component Class in " +
                    resourceDescription +
                    " must be a static CLR type name.");
            }

            Type resolved = resourceAssembly == null
                ? null
                : resourceAssembly.GetType(
                    className,
                    false,
                    true);

            if (resolved == null)
                resolved = Type.GetType(className, false);

            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            Type uniqueMatch = resolved;
            int i;

            for (i = 0; i < assemblies.Length; i++)
            {
                if (Object.ReferenceEquals(
                        assemblies[i],
                        resourceAssembly))
                {
                    continue;
                }

                Type candidate = assemblies[i].GetType(
                    className,
                    false,
                    true);

                if (candidate == null)
                    continue;

                if (uniqueMatch == null)
                {
                    uniqueMatch = candidate;
                    continue;
                }

                if (!Object.ReferenceEquals(uniqueMatch, candidate))
                {
                    throw new InvalidOperationException(
                        "Component Class type '" +
                        className +
                        "' in " +
                        resourceDescription +
                        " is ambiguous across loaded assemblies. Use an " +
                        "assembly-qualified type name.");
                }
            }

            if (uniqueMatch == null)
            {
                throw new InvalidOperationException(
                    "Component Class type '" +
                    className +
                    "' in " +
                    resourceDescription +
                    " was not found.");
            }

            if (!uniqueMatch.IsVisible ||
                !uniqueMatch.IsClass ||
                uniqueMatch.IsAbstract ||
                uniqueMatch.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    "Component Class type '" +
                    className +
                    "' in " +
                    resourceDescription +
                    " must be a public, concrete, closed class.");
            }

            if (uniqueMatch.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException(
                    "Component Class type '" +
                    className +
                    "' in " +
                    resourceDescription +
                    " needs a public parameterless constructor.");
            }

            return uniqueMatch;
        }

        private static bool ValidateComponentChildrenSlot(
            XmlElement visualRoot,
            string resourceDescription)
        {
            ArrayList slots = new ArrayList();
            CollectComponentChildrenSlots(
                visualRoot,
                slots);

            if (slots.Count > 1)
            {
                throw new InvalidOperationException(
                    "The " +
                    resourceDescription +
                    " contains more than one <Children>. " +
                    "Registered XML components support one children slot.");
            }

            if (slots.Count == 0)
                return false;

            XmlElement slot = slots[0] as XmlElement;

            if (Object.ReferenceEquals(slot, visualRoot))
            {
                throw new InvalidOperationException(
                    "The " +
                    resourceDescription +
                    " cannot use <Children> as its visual root. " +
                    "Place the children slot inside the component Control root.");
            }

            int i;

            for (i = 0; i < slot.Attributes.Count; i++)
            {
                XmlAttribute attribute = slot.Attributes[i];

                if (!IsChildrenSlotMetadataAttribute(attribute))
                {
                    throw new InvalidOperationException(
                        "The <Children> in the " +
                        resourceDescription +
                        " must be empty and cannot declare attribute '" +
                        attribute.Name +
                        "'.");
                }
            }

            XmlNode node = slot.FirstChild;

            while (node != null)
            {
                if (node is XmlElement ||
                    ((node.NodeType == XmlNodeType.Text ||
                      node.NodeType == XmlNodeType.CDATA) &&
                     node.Value != null &&
                     node.Value.Trim().Length != 0))
                {
                    throw new InvalidOperationException(
                        "The <Children> in the " +
                        resourceDescription +
                        " must be empty.");
                }

                node = node.NextSibling;
            }

            XmlElement ancestor =
                slot.ParentNode as XmlElement;

            while (ancestor != null &&
                   !Object.ReferenceEquals(ancestor, visualRoot))
            {
                if (IsPropertyElement(ancestor) ||
                    IsRemovedItemsTemplateAliasElement(ancestor))
                {
                    throw new InvalidOperationException(
                        "The <Children> in the " +
                        resourceDescription +
                        " must be an ordinary visual child. " +
                        "Content slots are not supported inside property " +
                        "elements or item templates.");
                }

                ancestor = ancestor.ParentNode as XmlElement;
            }

            return true;
        }

        private static void CollectComponentChildrenSlots(
            XmlElement element,
            ArrayList slots)
        {
            if (element == null)
                return;

            if (EqualsIgnoreCase(
                    element.LocalName,
                    "Children"))
            {
                slots.Add(element);
            }

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    CollectComponentChildrenSlots(
                        child,
                        slots);
                }

                node = node.NextSibling;
            }
        }

        private static bool IsChildrenSlotMetadataAttribute(
            XmlAttribute attribute)
        {
            if (attribute == null)
                return false;

            return String.Equals(
                       attribute.NamespaceURI,
                       "http://www.w3.org/2000/xmlns/",
                       StringComparison.Ordinal) ||
                   EqualsIgnoreCase(
                       attribute.LocalName,
                       MarkupXmlDocument.LocationAttributeName) ||
                   EqualsIgnoreCase(
                       attribute.LocalName,
                       "__WfxPath");
        }

        private static void CopyInheritedNamespaceDeclarations(
            XmlElement wrapper,
            XmlElement templateRoot)
        {
            if (wrapper == null || templateRoot == null)
                return;

            int i;

            for (i = 0; i < wrapper.Attributes.Count; i++)
            {
                XmlAttribute attribute = wrapper.Attributes[i];

                if (!String.Equals(
                        attribute.Name,
                        "xmlns",
                        StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(
                        attribute.Prefix,
                        "xmlns",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!templateRoot.HasAttribute(attribute.Name))
                {
                    templateRoot.SetAttribute(
                        attribute.Name,
                        attribute.Value);
                }
            }
        }

        private static void ReadComponentProperties(
            XmlElement propertiesElement,
            Assembly assembly,
            string resourceDescription,
            ArrayList properties,
            Hashtable propertyNames)
        {
            XmlNode node = propertiesElement.FirstChild;

            while (node != null)
            {
                XmlElement propertyElement = node as XmlElement;

                if (propertyElement != null)
                {
                    if (!EqualsIgnoreCase(
                        propertyElement.LocalName,
                        "Property"))
                    {
                        throw new InvalidOperationException(
                            "Only <Property> elements are allowed inside <Component.Properties> in " +
                            resourceDescription +
                            ".");
                    }

                    string propertyName =
                        propertyElement.GetAttribute("Name");

                    if (String.IsNullOrEmpty(propertyName))
                    {
                        throw new InvalidOperationException(
                            "A component property in " +
                            resourceDescription +
                            " is missing Name.");
                    }

                    ValidateComponentPropertyName(
                        propertyName,
                        resourceDescription);

                    if (propertyNames.ContainsKey(propertyName))
                    {
                        throw new InvalidOperationException(
                            "Component property '" +
                            propertyName +
                            "' is declared more than once in " +
                            resourceDescription +
                            ".");
                    }

                    string typeName =
                        propertyElement.GetAttribute("Type");
                    Type propertyType =
                        ResolveComponentPropertyType(
                            assembly,
                            typeName);

                    if (propertyType == null)
                    {
                        throw new InvalidOperationException(
                            "Component property '" +
                            propertyName +
                            "' in " +
                            resourceDescription +
                            " has unknown type '" +
                            typeName +
                            "'.");
                    }

                    ComponentPropertyDefinition property =
                        new ComponentPropertyDefinition();

                    property.Name = propertyName;
                    property.Type = propertyType;
                    Type valueProxyType =
                        typeof(PropertyBinding<>).MakeGenericType(
                            new Type[] { propertyType });
                    property.ValueProxyConstructor =
                        valueProxyType.GetConstructor(
                            new Type[] { propertyType });
                    property.HasDefaultValue =
                        propertyElement.HasAttribute("Default");
                    property.DefaultValue =
                        propertyElement.GetAttribute("Default");

                    if (property.HasDefaultValue &&
                        ContainsDynamicExpression(property.DefaultValue))
                    {
                        throw new InvalidOperationException(
                            "Default for component property '" +
                            propertyName +
                            "' in " +
                            resourceDescription +
                            " must be a literal value.");
                    }

                    string requiredText =
                        propertyElement.GetAttribute("Required");

                    if (requiredText.Length == 0)
                    {
                        property.Required =
                            !property.HasDefaultValue;
                    }
                    else
                    {
                        bool required;

                        if (!Boolean.TryParse(requiredText, out required))
                        {
                            throw new InvalidOperationException(
                                "Required for component property '" +
                                propertyName +
                                "' in " +
                                resourceDescription +
                                " must be true or false.");
                        }

                        property.Required = required;
                    }

                    propertyNames.Add(propertyName, true);
                    properties.Add(property);
                }

                node = node.NextSibling;
            }
        }

        private static void ValidateComponentPropertyName(
            string name,
            string resourceDescription)
        {
            if (name.IndexOf(':') >= 0 || name.IndexOf('.') >= 0)
            {
                throw new InvalidOperationException(
                    "Component property name '" +
                    name +
                    "' in " +
                    resourceDescription +
                    " cannot contain ':' or '.'.");
            }

            try
            {
                XmlConvert.VerifyName(name);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Component property name '" +
                    name +
                    "' in " +
                    resourceDescription +
                    " is not a valid XML name.",
                    ex);
            }

            if (EqualsIgnoreCase(name, "Name") ||
                EqualsIgnoreCase(name, "Condition") ||
                EqualsIgnoreCase(name, "Children"))
            {
                throw new InvalidOperationException(
                    "Component property name '" +
                    name +
                    "' in " +
                    resourceDescription +
                    " is reserved.");
            }
        }

        private static Type ResolveComponentPropertyType(
            Assembly resourceAssembly,
            string typeName)
        {
            if (String.IsNullOrEmpty(typeName))
                return typeof(string);

            Type type =
                resourceAssembly.GetType(
                    typeName,
                    false,
                    true);

            if (type != null)
                return type;

            type = Type.GetType(typeName, false, true);

            if (type != null)
                return type;

            type = Type.GetType(
                "System." + typeName,
                false,
                true);

            if (type != null)
                return type;

            type = typeof(Control).Assembly.GetType(
                "System.Windows.Forms." + typeName,
                false,
                true);

            if (type != null)
                return type;

            type = typeof(System.Drawing.Color).Assembly.GetType(
                typeName,
                false,
                true);

            if (type != null)
                return type;

            type = typeof(System.Drawing.Color).Assembly.GetType(
                "System.Drawing." + typeName,
                false,
                true);

            if (type != null)
                return type;

            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            Type uniqueMatch = null;
            int i;

            for (i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(
                    typeName,
                    false,
                    true);

                if (type == null)
                    continue;

                if (uniqueMatch == null)
                {
                    uniqueMatch = type;
                    continue;
                }

                if (!Object.ReferenceEquals(uniqueMatch, type))
                {
                    throw new InvalidOperationException(
                        "Component property type '" +
                        typeName +
                        "' is ambiguous across loaded assemblies. " +
                        "Use an assembly-qualified type name.");
                }
            }

            return uniqueMatch;
        }
    }
}
