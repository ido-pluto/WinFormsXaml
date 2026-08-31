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
        /// <summary>
        /// Immutable, all-or-nothing construction plan for the safe subset of an
        /// ItemsControl.ItemTemplate. A null plan is intentional: the existing XML
        /// builder remains the authoritative path for structural/dynamic cases.
        /// </summary>
        private sealed class CompiledControlBlueprint
        {
            private readonly CompiledControlBlueprintNode _root;
            private readonly int _componentRegistryVersion;

            public CompiledControlBlueprint(
                CompiledControlBlueprintNode root,
                int componentRegistryVersion)
            {
                _root = root;
                _componentRegistryVersion = componentRegistryVersion;
            }

            public CompiledControlBlueprintNode Root
            {
                get { return _root; }
            }

            public int ComponentRegistryVersion
            {
                get { return _componentRegistryVersion; }
            }
        }

        private sealed class CompiledControlBlueprintNode
        {
            private readonly XmlElement _sourceElement;
            private readonly string _elementPath;
            private readonly string _xamlType;
            private readonly Type _type;
            private readonly ConstructorInfo _constructor;
            private readonly string _declaredName;
            private readonly PropertyInfo _nameProperty;
            private readonly CompiledControlBlueprintAttribute[] _attributes;
            private readonly CompiledControlBlueprintTextPart[] _textParts;
            private readonly PropertyInfo _innerTextProperty;
            private readonly CompiledControlBlueprintChild[] _children;
            private readonly bool _innerTextIsLocalValue;

            public CompiledControlBlueprintNode(
                XmlElement sourceElement,
                string elementPath,
                string xamlType,
                Type type,
                ConstructorInfo constructor,
                string declaredName,
                PropertyInfo nameProperty,
                CompiledControlBlueprintAttribute[] attributes,
                CompiledControlBlueprintTextPart[] textParts,
                PropertyInfo innerTextProperty,
                CompiledControlBlueprintChild[] children,
                bool innerTextIsLocalValue)
            {
                _sourceElement = sourceElement;
                _elementPath = elementPath;
                _xamlType = xamlType;
                _type = type;
                _constructor = constructor;
                _declaredName = declaredName;
                _nameProperty = nameProperty;
                _attributes = attributes;
                _textParts = textParts;
                _innerTextProperty = innerTextProperty;
                _children = children;
                _innerTextIsLocalValue = innerTextIsLocalValue;
            }

            public XmlElement SourceElement
            {
                get { return _sourceElement; }
            }

            public string ElementPath
            {
                get { return _elementPath; }
            }

            public string XamlType
            {
                get { return _xamlType; }
            }

            public Type Type
            {
                get { return _type; }
            }

            public ConstructorInfo Constructor
            {
                get { return _constructor; }
            }

            public string DeclaredName
            {
                get { return _declaredName; }
            }

            public PropertyInfo NameProperty
            {
                get { return _nameProperty; }
            }

            public int AttributeCount
            {
                get { return _attributes.Length; }
            }

            public CompiledControlBlueprintAttribute GetAttribute(int index)
            {
                return _attributes[index];
            }

            public int TextPartCount
            {
                get { return _textParts.Length; }
            }

            public CompiledControlBlueprintTextPart GetTextPart(int index)
            {
                return _textParts[index];
            }

            public int ChildCount
            {
                get { return _children.Length; }
            }

            public PropertyInfo InnerTextProperty
            {
                get { return _innerTextProperty; }
            }

            public CompiledControlBlueprintChild GetChild(int index)
            {
                return _children[index];
            }

            public bool InnerTextIsLocalValue
            {
                get { return _innerTextIsLocalValue; }
            }
        }

        private enum CompiledControlBlueprintAssignmentKind
        {
            Property,
            MappedProperty,
            Event
        }

        private sealed class CompiledControlBlueprintAttribute
        {
            private readonly string _name;
            private readonly CompiledControlBlueprintAssignmentKind _kind;
            private readonly PropertyInfo _property;
            private readonly EventInfo _eventInfo;
            private readonly object _staticValue;
            private readonly int _bindingDefinitionIndex;

            public CompiledControlBlueprintAttribute(
                string name,
                CompiledControlBlueprintAssignmentKind kind,
                PropertyInfo property,
                EventInfo eventInfo,
                object staticValue,
                int bindingDefinitionIndex)
            {
                _name = name;
                _kind = kind;
                _property = property;
                _eventInfo = eventInfo;
                _staticValue = staticValue;
                _bindingDefinitionIndex = bindingDefinitionIndex;
            }

            public string Name
            {
                get { return _name; }
            }

            public CompiledControlBlueprintAssignmentKind Kind
            {
                get { return _kind; }
            }

            public PropertyInfo Property
            {
                get { return _property; }
            }

            public EventInfo EventInfo
            {
                get { return _eventInfo; }
            }

            public object StaticValue
            {
                get { return _staticValue; }
            }

            public int BindingDefinitionIndex
            {
                get { return _bindingDefinitionIndex; }
            }
        }

        private enum CompiledControlBlueprintChildAttachmentKind
        {
            LayoutHostControls,
            TabPages,
            ComboBoxItems,
            CheckedListBoxItems,
            ListBoxItems,
            NormalControls
        }

        private sealed class CompiledControlBlueprintChild
        {
            private readonly CompiledControlBlueprintNode _node;
            private readonly CompiledControlBlueprintChildAttachmentKind
                _attachmentKind;

            public CompiledControlBlueprintChild(
                CompiledControlBlueprintNode node,
                CompiledControlBlueprintChildAttachmentKind attachmentKind)
            {
                _node = node;
                _attachmentKind = attachmentKind;
            }

            public CompiledControlBlueprintNode Node
            {
                get { return _node; }
            }

            public CompiledControlBlueprintChildAttachmentKind AttachmentKind
            {
                get { return _attachmentKind; }
            }
        }

        private sealed class CompiledControlBlueprintTextPart
        {
            private readonly string _staticValue;
            private readonly int _bindingDefinitionIndex;

            public CompiledControlBlueprintTextPart(
                string staticValue,
                int bindingDefinitionIndex)
            {
                _staticValue = staticValue;
                _bindingDefinitionIndex = bindingDefinitionIndex;
            }

            public string StaticValue
            {
                get { return _staticValue; }
            }

            public int BindingDefinitionIndex
            {
                get { return _bindingDefinitionIndex; }
            }
        }

        private long _compiledControlBlueprintBuildCount;
        private long _compiledControlBlueprintPropertyAssignmentCount;
        private long _compiledControlBlueprintEventBindingCount;
        private long _compiledControlBlueprintChildAttachmentCount;
        private long _compiledControlBlueprintGenericAttributeDispatchCount;
        private long _compiledControlBlueprintStringConversionCount;
        private long _compiledControlBlueprintGenericChildDispatchCount;
        private long _compiledControlBlueprintMemberLookupCount;

        [ThreadStatic]
        private static XamlRuntime _executingCompiledControlBlueprintRuntime;

        [ThreadStatic]
        private static int _executingCompiledControlBlueprintDepth;

        internal long CompiledControlBlueprintBuildCount
        {
            get { return _compiledControlBlueprintBuildCount; }
        }

        internal long CompiledControlBlueprintPropertyAssignmentCount
        {
            get { return _compiledControlBlueprintPropertyAssignmentCount; }
        }

        internal long CompiledControlBlueprintEventBindingCount
        {
            get { return _compiledControlBlueprintEventBindingCount; }
        }

        internal long CompiledControlBlueprintChildAttachmentCount
        {
            get { return _compiledControlBlueprintChildAttachmentCount; }
        }

        internal long CompiledControlBlueprintGenericAttributeDispatchCount
        {
            get { return _compiledControlBlueprintGenericAttributeDispatchCount; }
        }

        internal long CompiledControlBlueprintStringConversionCount
        {
            get { return _compiledControlBlueprintStringConversionCount; }
        }

        internal long CompiledControlBlueprintGenericChildDispatchCount
        {
            get { return _compiledControlBlueprintGenericChildDispatchCount; }
        }

        internal long CompiledControlBlueprintMemberLookupCount
        {
            get { return _compiledControlBlueprintMemberLookupCount; }
        }

        private bool IsExecutingCompiledControlBlueprint
        {
            get
            {
                return _executingCompiledControlBlueprintDepth != 0 &&
                    Object.ReferenceEquals(
                        _executingCompiledControlBlueprintRuntime,
                        this);
            }
        }

        private static void IncrementCompiledControlBlueprintCounter(
            ref long counter)
        {
            if (counter < Int64.MaxValue)
                counter++;
        }

        private static void RecordCompiledControlBlueprintMemberLookup()
        {
            XamlRuntime runtime =
                _executingCompiledControlBlueprintRuntime;

            if (runtime == null ||
                _executingCompiledControlBlueprintDepth == 0)
            {
                return;
            }

            IncrementCompiledControlBlueprintCounter(
                ref runtime._compiledControlBlueprintMemberLookupCount);
        }

        private CompiledControlBlueprint TryCompileControlBlueprint(
            CompiledItemTemplate compiled)
        {
            // Eligibility is deliberately whole-template and conservative:
            // every node must be a parameterless Control; registered components,
            // Object/Control constructor selection, property/preset elements,
            // Conditions, attached or mapped properties, applicable styles, and
            // rebuild slots keep the complete template on BuildElement. Ordinary
            // property bindings, functions, and presets remain eligible because
            // their compiled RenderBindingDefinitions are evaluated first.
            if (compiled == null || compiled.AnnotatedRoot == null)
                return null;

            int registryVersion = GetComponentRegistryVersion();
            int definitionCount = compiled.BindingDefinitions == null
                ? 0
                : compiled.BindingDefinitions.Count;
            bool[] usedDefinitions = new bool[definitionCount];
            CompiledControlBlueprintNode root;

            if (!TryCompileControlBlueprintNode(
                    compiled,
                    compiled.AnnotatedRoot,
                    usedDefinitions,
                    out root))
            {
                return null;
            }

            int i;

            for (i = 0; i < usedDefinitions.Length; i++)
            {
                if (!usedDefinitions[i])
                    return null;
            }

            // Register is global and may race template compilation. A changed
            // registry invalidates the whole plan; never mix resolutions from two
            // registry snapshots.
            if (registryVersion != GetComponentRegistryVersion())
                return null;

            return new CompiledControlBlueprint(
                root,
                registryVersion);
        }

        private bool TryCompileControlBlueprintNode(
            CompiledItemTemplate compiled,
            XmlElement element,
            bool[] usedDefinitions,
            out CompiledControlBlueprintNode result)
        {
            result = null;

            if (element == null ||
                IsPropertyElement(element) ||
                IsPresetDefinitionElement(element) ||
                IsNestedItemsTemplateContainer(element) ||
                IsSimpleItem(element.LocalName) ||
                EqualsIgnoreCase(element.LocalName, "Children") ||
                EqualsIgnoreCase(element.LocalName, "Object") ||
                EqualsIgnoreCase(element.LocalName, "Control"))
            {
                return false;
            }

            RegisteredComponent registeredComponent;

            if (TryGetRegisteredComponent(
                    element.LocalName,
                    out registeredComponent))
            {
                return false;
            }

            Type type;

            try
            {
                type = ResolveXamlType(
                    element.LocalName,
                    element);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (type == null ||
                type.IsAbstract ||
                type.ContainsGenericParameters ||
                !typeof(Control).IsAssignableFrom(type))
            {
                return false;
            }

            ConstructorInfo constructor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);

            if (constructor == null)
                return false;

            string path = GetAttributeIgnoreNamespace(
                element,
                "__WfxPath");
            ArrayList attributes = new ArrayList();
            string declaredName = null;
            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];

                if (IsConditionalIncludeMetadataAttribute(attribute))
                    return false;

                if (ShouldIgnoreAttribute(attribute))
                    continue;

                string name = attribute.LocalName;

                if (EqualsIgnoreCase(name, "Name"))
                {
                    declaredName = attribute.Value;
                    continue;
                }

                // Conditions change the shape of a realized row. Attached
                // properties and constructor metadata likewise require the
                // structural XML path, so the entire template stays on it.
                if (EqualsIgnoreCase(name, "Condition") ||
                    name.IndexOf('.') >= 0)
                {
                    return false;
                }

                bool mappedProperty =
                    IsCompiledControlBlueprintMappedProperty(name);

                // Mapped XAML aliases win before same-named CLR members in the
                // normal renderer. Common layout and color aliases have a
                // dedicated compiled strategy below; all remaining aliases stay
                // on the authoritative XML path.
                if ((IsMappedXamlPropertyName(name) && !mappedProperty) ||
                    EqualsIgnoreCase(name, "ResourceStyle"))
                {
                    return false;
                }

                int bindingIndex = -1;

                try
                {
                    if (ContainsDynamicExpression(attribute.Value))
                    {
                        bindingIndex = FindControlBlueprintAttributeBinding(
                            compiled,
                            path,
                            attribute,
                            usedDefinitions);

                        if (bindingIndex < 0)
                            return false;

                        RenderBindingDefinition definition =
                            compiled.BindingDefinitions[bindingIndex] as
                                RenderBindingDefinition;

                        if (definition == null ||
                            definition.Kind ==
                                RenderBindingSlotKind.Condition ||
                            definition.Kind ==
                                RenderBindingSlotKind.RebuildOnChange)
                        {
                            return false;
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    return false;
                }

                PropertyInfo property = null;
                EventInfo eventInfo = null;
                CompiledControlBlueprintAssignmentKind assignmentKind;
                object staticValue = null;

                if (mappedProperty)
                {
                    if (!CanCompileControlBlueprintMappedProperty(type, name))
                        return false;

                    assignmentKind =
                        CompiledControlBlueprintAssignmentKind.MappedProperty;

                    if (bindingIndex < 0)
                        staticValue = attribute.Value;
                }
                else
                {
                    property = FindProperty(type, name);

                    // On controls without a writable CLR Style property, Style
                    // is a named-resource selector and wins even over a
                    // same-named event.
                    if (EqualsIgnoreCase(name, "Style") &&
                        (property == null ||
                         !property.CanWrite ||
                         property.GetIndexParameters().Length != 0))
                    {
                        return false;
                    }

                    if (property != null)
                    {
                        if (!property.CanWrite ||
                            property.GetIndexParameters().Length != 0)
                        {
                            return false;
                        }

                        assignmentKind =
                            CompiledControlBlueprintAssignmentKind.Property;

                        if (bindingIndex < 0 &&
                            !TryPreconvertControlBlueprintConstant(
                                attribute.Value,
                                property.PropertyType,
                                out staticValue))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        eventInfo = FindEvent(type, name);

                        // Dynamic handler selection was never a stable
                        // direct-event contract. Keep it on the authoritative
                        // renderer rather than changing token/event replacement
                        // behavior in this fast path.
                        if (eventInfo == null || bindingIndex >= 0)
                            return false;

                        assignmentKind =
                            CompiledControlBlueprintAssignmentKind.Event;
                        staticValue = attribute.Value;
                    }
                }

                attributes.Add(
                    new CompiledControlBlueprintAttribute(
                        name,
                        assignmentKind,
                        property,
                        eventInfo,
                        staticValue,
                        bindingIndex));
            }

            if (!AreControlBlueprintStylesEligible(
                    compiled,
                    path,
                    type,
                    element.LocalName))
            {
                return false;
            }

            ArrayList textParts = new ArrayList();
            ArrayList children = new ArrayList();
            PropertyInfo nameProperty = null;

            if (!String.IsNullOrEmpty(declaredName))
            {
                PropertyInfo candidate = FindProperty(type, "Name");

                if (candidate != null &&
                    candidate.CanWrite &&
                    candidate.PropertyType == typeof(string) &&
                    candidate.GetIndexParameters().Length == 0)
                {
                    nameProperty = candidate;
                }
            }

            bool hasContentAttribute =
                HasControlBlueprintAttribute(attributes, "Text") ||
                HasControlBlueprintAttribute(attributes, "Content") ||
                HasControlBlueprintAttribute(attributes, "Header");
            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement childElement = node as XmlElement;

                if (childElement != null)
                {
                    // Property elements include Resources and ItemTemplate. They
                    // must be rejected before any row object is constructed.
                    if (IsPropertyElement(childElement) ||
                        IsPresetDefinitionElement(childElement))
                    {
                        return false;
                    }

                    CompiledControlBlueprintNode child;

                    if (!TryCompileControlBlueprintNode(
                            compiled,
                            childElement,
                            usedDefinitions,
                            out child))
                    {
                        return false;
                    }

                    CompiledControlBlueprintChildAttachmentKind attachmentKind;

                    if (!TryCompileControlBlueprintChildAttachment(
                            type,
                            child.Type,
                            out attachmentKind))
                    {
                        return false;
                    }

                    children.Add(
                        new CompiledControlBlueprintChild(
                            child,
                            attachmentKind));
                }
                else if (node.NodeType == XmlNodeType.Text ||
                         node.NodeType == XmlNodeType.CDATA ||
                         node.NodeType == XmlNodeType.Whitespace ||
                         node.NodeType == XmlNodeType.SignificantWhitespace)
                {
                    int bindingIndex = -1;

                    try
                    {
                        if (ContainsDynamicExpression(node.Value))
                        {
                            // Inner text is only applied by the normal builder
                            // to leaf elements without an explicit content value.
                            if (hasContentAttribute || HasElementChildren(element))
                                return false;

                            bindingIndex = FindControlBlueprintTextBinding(
                                compiled,
                                path,
                                node.Value,
                                usedDefinitions);

                            if (bindingIndex < 0)
                                return false;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }

                    textParts.Add(
                        new CompiledControlBlueprintTextPart(
                            bindingIndex < 0 ? node.Value : null,
                            bindingIndex));
                }

                node = node.NextSibling;
            }

            bool innerTextIsLocalValue =
                !hasContentAttribute &&
                children.Count == 0 &&
                HasNonWhitespaceControlBlueprintText(textParts);
            PropertyInfo innerTextProperty = null;

            if (innerTextIsLocalValue)
            {
                innerTextProperty = FindProperty(type, "Text");

                if (innerTextProperty == null ||
                    !innerTextProperty.CanWrite ||
                    innerTextProperty.PropertyType != typeof(string) ||
                    innerTextProperty.GetIndexParameters().Length != 0)
                {
                    return false;
                }
            }

            result = new CompiledControlBlueprintNode(
                element,
                path,
                element.LocalName,
                type,
                constructor,
                declaredName,
                nameProperty,
                (CompiledControlBlueprintAttribute[])
                    attributes.ToArray(
                        typeof(CompiledControlBlueprintAttribute)),
                (CompiledControlBlueprintTextPart[])
                    textParts.ToArray(
                        typeof(CompiledControlBlueprintTextPart)),
                innerTextProperty,
                (CompiledControlBlueprintChild[])
                    children.ToArray(
                        typeof(CompiledControlBlueprintChild)),
                innerTextIsLocalValue);

            return true;
        }

        private static bool IsCompiledControlBlueprintMappedProperty(
            string name)
        {
            return
                EqualsIgnoreCase(name, "Width") ||
                EqualsIgnoreCase(name, "Height") ||
                EqualsIgnoreCase(name, "MinWidth") ||
                EqualsIgnoreCase(name, "MinHeight") ||
                EqualsIgnoreCase(name, "MaxWidth") ||
                EqualsIgnoreCase(name, "MaxHeight") ||
                EqualsIgnoreCase(name, "Margin") ||
                EqualsIgnoreCase(name, "Padding") ||
                EqualsIgnoreCase(name, "HorizontalAlignment") ||
                EqualsIgnoreCase(name, "VerticalAlignment") ||
                EqualsIgnoreCase(name, "Background") ||
                EqualsIgnoreCase(name, "BackColor") ||
                EqualsIgnoreCase(name, "Foreground") ||
                EqualsIgnoreCase(name, "ForeColor") ||
                EqualsIgnoreCase(name, "BorderBrush") ||
                EqualsIgnoreCase(name, "BorderThickness");
        }

        private static bool CanCompileControlBlueprintMappedProperty(
            Type type,
            string name)
        {
            if (type == null || !typeof(Control).IsAssignableFrom(type))
                return false;

            if (EqualsIgnoreCase(name, "BorderBrush") ||
                EqualsIgnoreCase(name, "BorderThickness"))
            {
                return typeof(BorderHost).IsAssignableFrom(type);
            }

            return true;
        }

        private bool TryPreconvertControlBlueprintConstant(
            string value,
            Type targetType,
            out object converted)
        {
            converted = null;

            if (!CanPreconvertControlBlueprintConstant(targetType))
                return false;

            try
            {
                converted = ConvertString(value, targetType);
                return converted == null ||
                    !(converted is IDisposable);
            }
            catch
            {
                // The complete renderer owns conversion errors and their exact
                // diagnostics for values outside this proven immutable subset.
                converted = null;
                return false;
            }
        }

        private static bool CanPreconvertControlBlueprintConstant(Type type)
        {
            if (type == null || Nullable.GetUnderlyingType(type) != null)
                return false;

            if (type == typeof(string) || type == typeof(object))
                return true;

            return CanCacheConvertedStringValue(type);
        }

        private static bool TryCompileControlBlueprintChildAttachment(
            Type parentType,
            Type childType,
            out CompiledControlBlueprintChildAttachmentKind result)
        {
            result =
                CompiledControlBlueprintChildAttachmentKind.NormalControls;

            if (parentType == null ||
                childType == null ||
                !typeof(Control).IsAssignableFrom(parentType) ||
                !typeof(Control).IsAssignableFrom(childType))
            {
                return false;
            }

            if (typeof(GridHost).IsAssignableFrom(parentType) ||
                typeof(StackHost).IsAssignableFrom(parentType) ||
                typeof(FlexPanel).IsAssignableFrom(parentType) ||
                typeof(DockHost).IsAssignableFrom(parentType) ||
                typeof(CanvasHost).IsAssignableFrom(parentType) ||
                typeof(SingleHost).IsAssignableFrom(parentType))
            {
                result =
                    CompiledControlBlueprintChildAttachmentKind
                        .LayoutHostControls;
                return true;
            }

            if (typeof(TabControl).IsAssignableFrom(parentType) &&
                typeof(TabPage).IsAssignableFrom(childType))
            {
                result =
                    CompiledControlBlueprintChildAttachmentKind.TabPages;
                return true;
            }

            if (typeof(ComboBox).IsAssignableFrom(parentType))
            {
                result =
                    CompiledControlBlueprintChildAttachmentKind.ComboBoxItems;
                return true;
            }

            if (typeof(CheckedListBox).IsAssignableFrom(parentType))
            {
                result =
                    CompiledControlBlueprintChildAttachmentKind
                        .CheckedListBoxItems;
                return true;
            }

            if (typeof(ListBox).IsAssignableFrom(parentType))
            {
                result =
                    CompiledControlBlueprintChildAttachmentKind.ListBoxItems;
                return true;
            }

            return true;
        }

        private static bool HasControlBlueprintAttribute(
            ArrayList attributes,
            string name)
        {
            int i;

            for (i = 0; attributes != null && i < attributes.Count; i++)
            {
                CompiledControlBlueprintAttribute attribute =
                    attributes[i] as CompiledControlBlueprintAttribute;

                if (attribute != null &&
                    EqualsIgnoreCase(attribute.Name, name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasNonWhitespaceControlBlueprintText(
            ArrayList textParts)
        {
            int i;

            for (i = 0; textParts != null && i < textParts.Count; i++)
            {
                CompiledControlBlueprintTextPart part =
                    textParts[i] as CompiledControlBlueprintTextPart;

                if (part == null)
                    continue;

                if (part.BindingDefinitionIndex >= 0 ||
                    (!String.IsNullOrEmpty(part.StaticValue) &&
                     part.StaticValue.Trim().Length != 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindControlBlueprintAttributeBinding(
            CompiledItemTemplate compiled,
            string path,
            XmlAttribute attribute,
            bool[] usedDefinitions)
        {
            int i;

            for (i = 0;
                 compiled != null &&
                 compiled.BindingDefinitions != null &&
                 i < compiled.BindingDefinitions.Count;
                 i++)
            {
                if (usedDefinitions[i])
                    continue;

                RenderBindingDefinition definition =
                    compiled.BindingDefinitions[i] as
                        RenderBindingDefinition;

                if (definition != null &&
                    !definition.PropertyElementValue &&
                    String.Equals(
                        definition.ElementPath,
                        path,
                        StringComparison.Ordinal) &&
                    String.Equals(
                        definition.XmlAttributeName,
                        attribute.Name,
                        StringComparison.Ordinal) &&
                    String.Equals(
                        definition.Expression,
                        attribute.Value,
                        StringComparison.Ordinal))
                {
                    usedDefinitions[i] = true;
                    return i;
                }
            }

            return -1;
        }

        private static int FindControlBlueprintTextBinding(
            CompiledItemTemplate compiled,
            string path,
            string expression,
            bool[] usedDefinitions)
        {
            int i;

            for (i = 0;
                 compiled != null &&
                 compiled.BindingDefinitions != null &&
                 i < compiled.BindingDefinitions.Count;
                 i++)
            {
                if (usedDefinitions[i])
                    continue;

                RenderBindingDefinition definition =
                    compiled.BindingDefinitions[i] as
                        RenderBindingDefinition;

                if (definition != null &&
                    definition.Kind == RenderBindingSlotKind.InnerText &&
                    !definition.PropertyElementValue &&
                    String.Equals(
                        definition.ElementPath,
                        path,
                        StringComparison.Ordinal) &&
                    String.Equals(
                        definition.Expression,
                        expression,
                        StringComparison.Ordinal))
                {
                    usedDefinitions[i] = true;
                    return i;
                }
            }

            return -1;
        }

        private bool AreControlBlueprintStylesEligible(
            CompiledItemTemplate compiled,
            string path,
            Type type,
            string xamlType)
        {
            ItemTemplateStyleScope scope =
                compiled.StyleScopesByElementPath[path] as
                    ItemTemplateStyleScope;

            if (scope == null)
                return false;

            int i;

            for (i = 0; i < scope.ImplicitStyles.Count; i++)
            {
                StyleDefinition style = scope.ImplicitStyles[i];
                bool matches;

                try
                {
                    matches = ControlBlueprintStyleMatches(
                        style,
                        type,
                        xamlType);
                }
                catch (InvalidOperationException)
                {
                    return false;
                }

                // StyleState captures/restores the layer below a style setter and
                // deliberately routes mapped aliases through the generic mapper.
                // Until that complete state machine has a compiled equivalent, an
                // applicable style makes the whole template ineligible.
                if (matches)
                    return false;
            }

            return true;
        }

        private bool ControlBlueprintStyleMatches(
            StyleDefinition style,
            Type actualType,
            string xamlType)
        {
            if (style == null || String.IsNullOrEmpty(style.TargetType))
                return false;

            string target = style.TargetType;

            if (EqualsIgnoreCase(target, xamlType) ||
                EqualsIgnoreCase(target, actualType.Name) ||
                EqualsIgnoreCase(target, "Control"))
            {
                return true;
            }

            Type targetType;

            targetType = ResolveTypeByName(target);

            if (targetType != null &&
                targetType.IsAssignableFrom(actualType))
            {
                return true;
            }

            Type formsType = typeof(Control).Assembly.GetType(
                "System.Windows.Forms." + target,
                false,
                true);

            return formsType != null &&
                formsType.IsAssignableFrom(actualType);
        }

        private bool IsControlBlueprintCurrent(
            CompiledControlBlueprint blueprint)
        {
            return blueprint != null &&
                blueprint.ComponentRegistryVersion ==
                    GetComponentRegistryVersion();
        }

        private Control BuildTemplateControlFromBlueprint(
            ItemsControl host,
            CompiledItemTemplate compiled,
            object dataContext,
            object bindingDataContext,
            Hashtable functionResults,
            out ArrayList bindingSlots)
        {
            bindingSlots = new ArrayList();
            ArrayList evaluatedValues = new ArrayList();
            Hashtable previousCache = _activeFunctionResultCache;
            _activeFunctionResultCache = functionResults;

            try
            {
                EvaluateControlBlueprintBindings(
                    compiled,
                    bindingDataContext,
                    evaluatedValues);
            }
            finally
            {
                _activeFunctionResultCache = previousCache;
            }

            Hashtable elementMap = new Hashtable();
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
            object result;
            XamlRuntime previousExecutingBlueprintRuntime =
                _executingCompiledControlBlueprintRuntime;
            int previousExecutingBlueprintDepth =
                _executingCompiledControlBlueprintDepth;

            _templateBuildDepth++;
            _executingCompiledControlBlueprintRuntime = this;
            _executingCompiledControlBlueprintDepth =
                Object.ReferenceEquals(
                    previousExecutingBlueprintRuntime,
                    this)
                    ? previousExecutingBlueprintDepth + 1
                    : 1;

            try
            {
                result = BuildControlBlueprintNode(
                    compiled,
                    compiled.ControlBlueprint.Root,
                    evaluatedValues,
                    elementMap);
            }
            finally
            {
                _templateBuildDepth--;
                _executingCompiledControlBlueprintRuntime =
                    previousExecutingBlueprintRuntime;
                _executingCompiledControlBlueprintDepth =
                    previousExecutingBlueprintDepth;
                _activeTemplateElementMap = previousMap;
                _activeTemplateDataContext = previousDataContext;
                _activeTemplateStyleBindingSlots = previousStyleSlots;
                _activeCompiledItemTemplate = previousCompiledTemplate;
                _activeFunctionResultCache = previousBuildFunctionCache;
            }

            Control control = result as Control;

            if (control == null)
            {
                if (result != null)
                    ReleaseCreatedElement(result);

                throw new InvalidOperationException(
                    "ItemsControl template root must create a WinForms Control.");
            }

            try
            {
                bindingSlots = CreateTemplateBindingSlotsFromDefinitions(
                    compiled.BindingDefinitions,
                    elementMap,
                    bindingDataContext,
                    host == null
                        ? _activeComponentEventTarget
                        : host.TemplateEventTarget,
                    functionResults,
                    evaluatedValues);
                bindingSlots.AddRange(styleSlots);

                ApplyDataContextToTree(
                    control,
                    dataContext);

                if (_compiledControlBlueprintBuildCount < Int64.MaxValue)
                    _compiledControlBlueprintBuildCount++;

                if (host != null)
                    host.RecordItemTemplateBlueprintBuild();

                return control;
            }
            catch
            {
                ReleaseCreatedElement(control);
                throw;
            }
        }

        private void EvaluateControlBlueprintBindings(
            CompiledItemTemplate compiled,
            object dataContext,
            ArrayList evaluatedValues)
        {
            int i;

            for (i = 0;
                 compiled.BindingDefinitions != null &&
                 i < compiled.BindingDefinitions.Count;
                 i++)
            {
                RenderBindingDefinition definition =
                    compiled.BindingDefinitions[i] as
                        RenderBindingDefinition;

                if (definition == null)
                {
                    // Keep the array position identical to BindingDefinitions.
                    // Eligibility currently rejects null entries, but retaining
                    // the invariant here prevents a later relaxation from shifting
                    // every following attribute index.
                    evaluatedValues.Add(null);
                    continue;
                }

                XmlElement target = GetCompiledTemplateElement(
                    compiled.AnnotatedRoot,
                    definition.ElementPathIndices);

                try
                {
                    evaluatedValues.Add(
                        EvaluateTemplateExpressionValue(
                            definition.Expression,
                            dataContext));
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        target == null
                            ? compiled.AnnotatedRoot
                            : target,
                        definition.AttributeName,
                        ex);
                }
            }
        }

        private object BuildControlBlueprintNode(
            CompiledItemTemplate compiled,
            CompiledControlBlueprintNode node,
            ArrayList evaluatedValues,
            Hashtable elementMap)
        {
            Dictionary<string, StyleDefinition> previousNamedStyles =
                _activeComponentNamedStyles;
            List<StyleDefinition> previousImplicitStyles =
                _activeComponentImplicitStyles;
            ItemTemplateStyleScope scope =
                compiled.StyleScopesByElementPath[node.ElementPath] as
                    ItemTemplateStyleScope;

            if (scope != null)
            {
                _activeComponentNamedStyles = scope.NamedStyles;
                _activeComponentImplicitStyles = scope.ImplicitStyles;
            }

            try
            {
                return BuildControlBlueprintNodeCore(
                    node,
                    evaluatedValues,
                    elementMap);
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    node.SourceElement,
                    null,
                    ex);
            }
            finally
            {
                _activeComponentNamedStyles = previousNamedStyles;
                _activeComponentImplicitStyles = previousImplicitStyles;
            }
        }

        private object BuildControlBlueprintNodeCore(
            CompiledControlBlueprintNode node,
            ArrayList evaluatedValues,
            Hashtable elementMap)
        {
            object instance;

            try
            {
                instance = node.Constructor.Invoke(null);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not create '" +
                    node.Type.FullName +
                    "' for <" +
                    node.XamlType +
                    ">: " +
                    ex.Message,
                    ex);
            }

            try
            {
                ElementInfo info = new ElementInfo();
                info.XamlType = node.XamlType;
                _elementInfos.Add(instance, info);

                CaptureControlBlueprintLocalValues(
                    info,
                    instance,
                    node,
                    evaluatedValues);
                ConfigureCreatedObject(instance);

                if (!String.IsNullOrEmpty(node.ElementPath))
                    elementMap[node.ElementPath] = instance;

                ApplyControlBlueprintAttributes(
                    instance,
                    node,
                    evaluatedValues);

                if (node.NameProperty != null)
                {
                    node.NameProperty.SetValue(
                        instance,
                        node.DeclaredName,
                        null);
                }

                ApplyControlBlueprintInnerText(
                    instance,
                    node,
                    evaluatedValues);

                Control layoutControl = instance as Control;

                if (layoutControl != null)
                    layoutControl.SuspendLayout();

                try
                {
                    int i;

                    for (i = 0; i < node.ChildCount; i++)
                    {
                        CompiledControlBlueprintChild childPlan =
                            node.GetChild(i);
                        CompiledControlBlueprintNode childNode =
                            childPlan.Node;
                        object child = BuildControlBlueprintNode(
                            _activeCompiledItemTemplate,
                            childNode,
                            evaluatedValues,
                            elementMap);

                        RegisterLogicalChild(
                            instance,
                            child);

                        try
                        {
                            AttachCompiledControlBlueprintChild(
                                instance,
                                child,
                                childPlan.AttachmentKind);
                        }
                        catch (Exception ex)
                        {
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
                                childNode.SourceElement,
                                null,
                                ex);
                        }
                    }
                }
                finally
                {
                    if (layoutControl != null)
                        layoutControl.ResumeLayout(false);
                }

                CompleteApplicationIconConfiguration(
                    instance as Form);
                PostConfigure(instance);

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
                }

                throw;
            }
        }

        private void AttachCompiledControlBlueprintChild(
            object parent,
            object child,
            CompiledControlBlueprintChildAttachmentKind attachmentKind)
        {
            Control parentControl = parent as Control;
            Control childControl = child as Control;

            if (parentControl == null || childControl == null)
            {
                throw new InvalidOperationException(
                    "A compiled control blueprint child edge must contain " +
                    "WinForms Controls.");
            }

            switch (attachmentKind)
            {
                case CompiledControlBlueprintChildAttachmentKind
                    .LayoutHostControls:
                    parentControl.Controls.Add(childControl);
                    break;

                case CompiledControlBlueprintChildAttachmentKind.TabPages:
                    ((TabControl)parent).TabPages.Add((TabPage)child);
                    break;

                case CompiledControlBlueprintChildAttachmentKind.ComboBoxItems:
                    ((ComboBox)parent).Items.Add(child);
                    break;

                case CompiledControlBlueprintChildAttachmentKind
                    .CheckedListBoxItems:
                    ((CheckedListBox)parent).Items.Add(child);
                    break;

                case CompiledControlBlueprintChildAttachmentKind.ListBoxItems:
                    ((ListBox)parent).Items.Add(child);
                    break;

                default:
                    parentControl.Controls.Add(childControl);
                    ApplyNativeParentLayout(
                        parentControl,
                        childControl);

                    Form form = parentControl as Form;
                    MenuStrip menu = childControl as MenuStrip;

                    if (form != null && menu != null)
                        form.MainMenuStrip = menu;
                    break;
            }

            IncrementCompiledControlBlueprintCounter(
                ref _compiledControlBlueprintChildAttachmentCount);
        }

        private void CaptureControlBlueprintLocalValues(
            ElementInfo info,
            object instance,
            CompiledControlBlueprintNode node,
            ArrayList evaluatedValues)
        {
            int i;

            for (i = 0; i < node.AttributeCount; i++)
            {
                CompiledControlBlueprintAttribute attribute =
                    node.GetAttribute(i);

                if (attribute.BindingDefinitionIndex >= 0 &&
                    IsUnsetPresetValue(
                        evaluatedValues[
                            attribute.BindingDefinitionIndex]))
                {
                    continue;
                }

                AddLocalValueProperty(
                    info,
                    GetStylePropertyKey(
                        instance,
                        attribute.Name));
            }

            if (node.InnerTextIsLocalValue &&
                !IsControlBlueprintInnerTextUnset(
                    node,
                    evaluatedValues) &&
                GetControlBlueprintInnerText(
                    node,
                    evaluatedValues).Trim().Length != 0)
            {
                AddLocalValueProperty(
                    info,
                    GetStylePropertyKey(instance, "Text"));
            }
        }

        private void ApplyControlBlueprintAttributes(
            object instance,
            CompiledControlBlueprintNode node,
            ArrayList evaluatedValues)
        {
            int i;

            for (i = 0; i < node.AttributeCount; i++)
            {
                CompiledControlBlueprintAttribute attribute =
                    node.GetAttribute(i);

                if (EqualsIgnoreCase(attribute.Name, "Tag"))
                    GetInfo(instance).TagExplicit = true;

                object value = attribute.BindingDefinitionIndex < 0
                    ? attribute.StaticValue
                    : evaluatedValues[
                        attribute.BindingDefinitionIndex];

                if (IsUnsetPresetValue(value))
                    continue;

                try
                {
                    if (attribute.Kind ==
                        CompiledControlBlueprintAssignmentKind.Property)
                    {
                        ApplyCompiledControlBlueprintProperty(
                            instance,
                            attribute,
                            value);

                        IncrementCompiledControlBlueprintCounter(
                            ref _compiledControlBlueprintPropertyAssignmentCount);
                    }
                    else if (attribute.Kind ==
                        CompiledControlBlueprintAssignmentKind.MappedProperty)
                    {
                        ApplyCompiledControlBlueprintMappedProperty(
                            instance,
                            attribute,
                            value);

                        IncrementCompiledControlBlueprintCounter(
                            ref _compiledControlBlueprintPropertyAssignmentCount);
                    }
                    else
                    {
                        BindEvent(
                            instance,
                            attribute.EventInfo,
                            attribute.StaticValue as string,
                            false);

                        IncrementCompiledControlBlueprintCounter(
                            ref _compiledControlBlueprintEventBindingCount);
                    }
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        node.SourceElement,
                        attribute.Name,
                        ex);
                }
            }
        }

        private void ApplyCompiledControlBlueprintMappedProperty(
            object instance,
            CompiledControlBlueprintAttribute attribute,
            object value)
        {
            bool applied;

            if (attribute.BindingDefinitionIndex < 0)
            {
                applied = TryApplyWpfProperty(
                    instance,
                    attribute.Name,
                    value as string);
            }
            else
            {
                applied = ApplyBoundObjectAttribute(
                    instance,
                    attribute.Name,
                    value,
                    false);
            }

            if (!applied)
            {
                throw new InvalidOperationException(
                    "Unsupported property/event '" +
                    attribute.Name +
                    "' on " +
                    instance.GetType().FullName +
                    ".");
            }
        }

        private void ApplyCompiledControlBlueprintProperty(
            object instance,
            CompiledControlBlueprintAttribute attribute,
            object value)
        {
            if (attribute.BindingDefinitionIndex < 0 ||
                ShouldStoreBoundObject(value))
            {
                SetPropertyObjectValue(
                    instance,
                    attribute.Property,
                    value);
                return;
            }

            // Match the authoritative initial-binding path: scalar values first
            // become invariant XAML text. String/object destinations need no
            // converter; other destinations retain their normal conversion and
            // ownership transaction against the already-resolved PropertyInfo.
            string text = BindingValueToString(value);
            Type propertyType = attribute.Property.PropertyType;

            if (propertyType == typeof(string) ||
                propertyType == typeof(object))
            {
                SetPropertyObjectValue(
                    instance,
                    attribute.Property,
                    text);
                return;
            }

            SetPropertyValue(
                instance,
                attribute.Property,
                text);
        }

        private void ApplyControlBlueprintInnerText(
            object instance,
            CompiledControlBlueprintNode node,
            ArrayList evaluatedValues)
        {
            if (!node.InnerTextIsLocalValue)
                return;

            if (IsControlBlueprintInnerTextUnset(
                    node,
                    evaluatedValues))
            {
                return;
            }

            string text = GetControlBlueprintInnerText(
                node,
                evaluatedValues).Trim();

            if (text.Length == 0)
                return;

            try
            {
                SetPropertyObjectValue(
                    instance,
                    node.InnerTextProperty,
                    text);

                IncrementCompiledControlBlueprintCounter(
                    ref _compiledControlBlueprintPropertyAssignmentCount);
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    node.SourceElement,
                    "Text",
                    ex);
            }
        }

        private string GetControlBlueprintInnerText(
            CompiledControlBlueprintNode node,
            ArrayList evaluatedValues)
        {
            string text = String.Empty;
            int i;

            for (i = 0; i < node.TextPartCount; i++)
            {
                CompiledControlBlueprintTextPart part =
                    node.GetTextPart(i);

                text += part.BindingDefinitionIndex < 0
                    ? part.StaticValue
                    : BindingValueToString(
                        evaluatedValues[
                            part.BindingDefinitionIndex]);
            }

            return text;
        }

        private bool IsControlBlueprintInnerTextUnset(
            CompiledControlBlueprintNode node,
            ArrayList evaluatedValues)
        {
            int i;

            for (i = 0; i < node.TextPartCount; i++)
            {
                CompiledControlBlueprintTextPart part =
                    node.GetTextPart(i);

                if (part.BindingDefinitionIndex >= 0 &&
                    IsUnsetPresetValue(
                        evaluatedValues[
                            part.BindingDefinitionIndex]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
