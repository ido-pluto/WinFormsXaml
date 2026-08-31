using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private sealed class IncludeCompositionContext
        {
            public Assembly MarkupAssembly;
            public string MarkupSource;
            public string BasePath;
            public Hashtable StagedIncludesByAssembly;
            public ArrayList ActiveSources;
            public bool DeferFileIncludes;
            public IncludeExpansionBudget Budget;
        }

        private sealed class IncludeExpansionBudget
        {
            public int ExpansionCount;
        }

        private sealed class IncludeSourceIdentity
        {
            public Assembly Assembly;
            public string ResourceName;
            public string FilePath;
            public string DisplayName;
        }

        private sealed class ResolvedInclude
        {
            public XmlDocument Document;
            public IncludeSourceIdentity Identity;
            public Assembly MarkupAssembly;
            public string MarkupSource;
            public string BasePath;
        }

        internal static void ComposeIncludes(
            MarkupXmlDocument document,
            string basePath,
            Assembly markupAssembly,
            string markupSource,
            IList programmaticIncludes)
        {
            ComposeIncludes(
                document,
                basePath,
                markupAssembly,
                markupSource,
                programmaticIncludes,
                null);
        }

        private static void ComposeIncludes(
            MarkupXmlDocument document,
            string basePath,
            Assembly markupAssembly,
            string markupSource,
            IList programmaticIncludes,
            Hashtable stagedIncludesByAssembly)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            XmlElement root = document.DocumentElement;

            if (root == null)
                return;

            if (String.IsNullOrEmpty(
                    MarkupXmlDocument.GetMarkupSource(root)))
            {
                MarkupXmlDocument.SetOrigin(
                    root,
                    markupSource,
                    MarkupXmlDocument.GetElementPathPrefix(root));
            }

            IncludeCompositionContext context =
                CreateIncludeCompositionContext(
                    basePath,
                    markupAssembly,
                    markupSource,
                    stagedIncludesByAssembly);

            if (programmaticIncludes != null &&
                programmaticIncludes.Count != 0)
            {
                XmlNode insertionPoint = root.FirstChild;
                int i;

                for (i = 0; i < programmaticIncludes.Count; i++)
                {
                    XmlIncludeRequest request =
                        programmaticIncludes[i] as XmlIncludeRequest;

                    if (request == null)
                    {
                        throw new InvalidOperationException(
                            "The XML Form include queue contains an invalid request.");
                    }

                    ExpandProgrammaticInclude(
                        document,
                        root,
                        insertionPoint,
                        request,
                        context);
                }
            }

            ExpandIncludesInElement(root, context);
            PromoteItemTemplateMetadataRecursive(root);
            MergeSiblingPresetDeclarationsRecursive(root);
            MergeSiblingResourceDeclarationsRecursive(root);
        }

        private static IncludeCompositionContext
            CreateIncludeCompositionContext(
                string basePath,
                Assembly markupAssembly,
                string markupSource,
                Hashtable stagedIncludesByAssembly)
        {
            IncludeCompositionContext context =
                new IncludeCompositionContext();
            context.BasePath = basePath;
            context.MarkupAssembly = markupAssembly;
            context.MarkupSource = markupSource;
            context.StagedIncludesByAssembly = stagedIncludesByAssembly;
            context.ActiveSources = new ArrayList();
            context.Budget = new IncludeExpansionBudget();
            return context;
        }

        private static void ExpandProgrammaticInclude(
            XmlDocument ownerDocument,
            XmlElement parent,
            XmlNode insertionPoint,
            XmlIncludeRequest request,
            IncludeCompositionContext context)
        {
            Assembly assembly = request.Assembly == null
                ? context.MarkupAssembly
                : request.Assembly;
            ResolvedInclude resolved;

            try
            {
                resolved = ResolveInclude(
                    request.Source,
                    request.SourceKind,
                    assembly,
                    context,
                    null);
            }
            catch (Exception ex)
            {
                throw CreateProgrammaticIncludeException(
                    request.Source,
                    context,
                    ex);
            }

            ExpandResolvedInclude(
                ownerDocument,
                parent,
                insertionPoint,
                resolved,
                context,
                null);
        }

        private static void ExpandIncludesInElement(
            XmlElement parent,
            IncludeCompositionContext context)
        {
            XmlNode node = parent.FirstChild;

            while (node != null)
            {
                XmlNode next = node.NextSibling;
                XmlElement child = node as XmlElement;

                if (child != null &&
                    EqualsIgnoreCase(child.LocalName, "Includes"))
                {
                    ExpandDeclarativeInclude(
                        parent,
                        child,
                        context);
                }
                else if (child != null)
                {
                    ExpandIncludesInElement(child, context);
                }

                node = next;
            }
        }

        private static void ExpandDeclarativeInclude(
            XmlElement parent,
            XmlElement marker,
            IncludeCompositionContext context)
        {
            ValidateIncludeMarker(marker, context);

            string source = GetAttributeIgnoreNamespace(marker, "Source");
            IncludeSourceKind sourceKind;
            bool sourceKindSpecified =
                HasAttributeIgnoreNamespace(marker, "SourceKind");

            try
            {
                sourceKind = ParseIncludeSourceKind(marker);
            }
            catch (Exception ex)
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "SourceKind",
                    ex);
            }

            string assemblyName =
                GetAttributeIgnoreNamespace(marker, "Assembly");
            bool embeddedPrefix = source.Trim().StartsWith(
                "embedded://",
                StringComparison.OrdinalIgnoreCase);
            bool filePrefix = source.Trim().StartsWith(
                "file://",
                StringComparison.OrdinalIgnoreCase);

            if (!sourceKindSpecified)
            {
                if (embeddedPrefix)
                    sourceKind = IncludeSourceKind.EmbeddedResource;
                else if (filePrefix)
                    sourceKind = IncludeSourceKind.File;
            }

            if ((embeddedPrefix &&
                 sourceKind != IncludeSourceKind.EmbeddedResource) ||
                (filePrefix && sourceKind != IncludeSourceKind.File))
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Source",
                    new InvalidOperationException(
                        embeddedPrefix
                            ? "An embedded:// include requires " +
                              "SourceKind='EmbeddedResource'."
                            : "A file:// include requires " +
                              "SourceKind='File'."));
            }

            if (!String.IsNullOrEmpty(assemblyName) &&
                sourceKind != IncludeSourceKind.EmbeddedResource)
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Assembly",
                    new InvalidOperationException(
                        "Include Assembly is valid only with " +
                        "SourceKind='EmbeddedResource'."));
            }

            if (context.DeferFileIncludes &&
                sourceKind == IncludeSourceKind.File)
            {
                return;
            }

            Assembly assembly = ResolveIncludeAssembly(
                assemblyName,
                context.MarkupAssembly,
                marker,
                context);
            ResolvedInclude resolved;

            try
            {
                resolved = ResolveInclude(
                    source,
                    sourceKind,
                    assembly,
                    context,
                    marker);
            }
            catch (WinFormsXamlLoadException ex)
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Source",
                    ex);
            }
            catch (Exception ex)
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Source",
                    ex);
            }

            ExpandResolvedInclude(
                parent.OwnerDocument,
                parent,
                marker,
                resolved,
                context,
                marker);

            parent.RemoveChild(marker);
        }

        private static void ExpandResolvedInclude(
            XmlDocument ownerDocument,
            XmlElement parent,
            XmlNode insertionPoint,
            ResolvedInclude resolved,
            IncludeCompositionContext parentContext,
            XmlElement marker)
        {
            VerifyIncludeExpansionBudget(
                parentContext,
                marker);
            VerifyIncludeCycle(
                resolved.Identity,
                parentContext,
                marker);

            IncludeCompositionContext nestedContext =
                CreateIncludeCompositionContext(
                    resolved.BasePath,
                    resolved.MarkupAssembly,
                    resolved.MarkupSource,
                    parentContext.StagedIncludesByAssembly);
            nestedContext.ActiveSources = parentContext.ActiveSources;
            nestedContext.DeferFileIncludes =
                parentContext.DeferFileIncludes;
            nestedContext.Budget = parentContext.Budget;
            nestedContext.ActiveSources.Add(resolved.Identity);

            try
            {
                XmlElement includeRoot = resolved.Document.DocumentElement;

                if (includeRoot == null)
                {
                    throw CreateIncludedDocumentException(
                        resolved.MarkupSource,
                        includeRoot,
                        new InvalidOperationException(
                            "An XML include document must have an <Includes> root."));
                }

                MarkupXmlDocument.SetOrigin(
                    includeRoot,
                    resolved.MarkupSource,
                    BuildIncludePathPrefix(
                        marker,
                        parentContext,
                        resolved.MarkupSource));

                if (!EqualsIgnoreCase(includeRoot.LocalName, "Includes"))
                {
                    throw CreateIncludedDocumentException(
                        resolved.MarkupSource,
                        includeRoot,
                        new InvalidOperationException(
                            "An XML include document must have an <Includes> root."));
                }

                ValidateIncludeDefinitionRoot(
                    includeRoot,
                    resolved.MarkupSource);

                ExpandIncludesInElement(
                    includeRoot,
                    nestedContext);
                ValidateIncludeDefinitionContent(
                    includeRoot,
                    resolved.MarkupSource);
                InsertIncludeContent(
                    ownerDocument,
                    parent,
                    insertionPoint,
                    includeRoot,
                    marker == null
                        ? null
                        : GetAttributeIgnoreNamespace(marker, "Condition"));
            }
            finally
            {
                parentContext.ActiveSources.RemoveAt(
                    parentContext.ActiveSources.Count - 1);
            }
        }

        private static void InsertIncludeContent(
            XmlDocument ownerDocument,
            XmlElement parent,
            XmlNode insertionPoint,
            XmlElement includeRoot,
            string condition)
        {
            XmlNode node = includeRoot.FirstChild;

            while (node != null)
            {
                XmlNode next = node.NextSibling;
                XmlElement sourceElement = node as XmlElement;

                if (sourceElement != null)
                {
                    XmlElement prepared = PrepareIncludedElement(
                        ownerDocument,
                        includeRoot,
                        sourceElement);

                    ApplyConditionalIncludeMetadata(
                        prepared,
                        condition);

                    if (EqualsIgnoreCase(
                            prepared.LocalName,
                            "Includes.Resources"))
                    {
                        InsertIncludedResources(
                            ownerDocument,
                            parent,
                            insertionPoint,
                            prepared);
                    }
                    else
                    {
                        parent.InsertBefore(
                            prepared,
                            insertionPoint);
                    }
                }
                else if (node.NodeType == XmlNodeType.Comment)
                {
                    parent.InsertBefore(
                        ownerDocument.ImportNode(node, true),
                        insertionPoint);
                }

                node = next;
            }
        }

        private static void MergeSiblingPresetDeclarationsRecursive(
            XmlElement parent)
        {
            if (parent == null)
                return;

            Hashtable declarations = new Hashtable(
                StringComparer.OrdinalIgnoreCase);
            MergePresetDeclarationsFromOwnerChildren(
                parent,
                parent,
                declarations);

            XmlNode node = parent.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                    MergeSiblingPresetDeclarationsRecursive(child);

                node = node.NextSibling;
            }
        }

        private static void MergePresetDeclarationsFromOwnerChildren(
            XmlElement owner,
            XmlElement container,
            Hashtable declarations)
        {
            XmlNode node = container.FirstChild;

            while (node != null)
            {
                XmlNode next = node.NextSibling;
                XmlElement child = node as XmlElement;

                if (child != null &&
                    EqualsIgnoreCase(child.LocalName, "Presets") &&
                    !HasAttributeIgnoreNamespace(child, "Source"))
                {
                    MergePresetDeclarationIntoOwnerScope(
                        child,
                        declarations);
                }
                else if (container == owner &&
                         child != null &&
                         IsResourcesPropertyForParent(child, owner))
                {
                    MergePresetDeclarationsFromOwnerChildren(
                        owner,
                        child,
                        declarations);
                }

                node = next;
            }
        }

        private static void MergePresetDeclarationIntoOwnerScope(
            XmlElement declaration,
            Hashtable declarations)
        {
            ValidatePresetDeclarationUniqueness(declaration);

            string name = GetAttributeIgnoreNamespace(
                declaration,
                "Name");

            if (String.IsNullOrEmpty(name))
                return;

            XmlElement earlier = declarations[name] as XmlElement;

            if (earlier == null)
            {
                declarations[name] = declaration;
                return;
            }

            MergePresetDeclaration(earlier, declaration);
            declaration.ParentNode.RemoveChild(declaration);
        }

        private static void ValidatePresetDeclarationUniqueness(
            XmlElement presets)
        {
            ValidatePresetAttributes(
                presets,
                new string[] { "Name", "Selected", "Default" });
            ValidatePresetIdentifier(
                presets,
                "Name",
                true);
            ValidatePresetIdentifier(
                presets,
                "Selected",
                false);
            ValidatePresetIdentifier(
                presets,
                "Default",
                false);

            Hashtable presetNames = new Hashtable(
                StringComparer.OrdinalIgnoreCase);
            XmlNode node = presets.FirstChild;

            while (node != null)
            {
                XmlElement preset = node as XmlElement;

                if (preset == null)
                {
                    ValidatePresetIgnorableContent(node, presets);
                }
                else if (!EqualsIgnoreCase(preset.LocalName, "Preset"))
                {
                    ThrowPresetCompositionException(
                        preset,
                        "Unexpected <" + preset.LocalName +
                        "> element inside preset set '" +
                        GetAttributeIgnoreNamespace(presets, "Name") +
                        "'. Only <Preset> elements are allowed.");
                }
                else
                {
                    ValidatePresetAttributes(
                        preset,
                        new string[] { "Name" });
                    ValidatePresetIdentifier(
                        preset,
                        "Name",
                        true);
                    string presetName = GetAttributeIgnoreNamespace(
                        preset,
                        "Name");

                    if (!String.IsNullOrEmpty(presetName))
                    {
                        if (presetNames.ContainsKey(presetName))
                        {
                            throw CreateIncludedDocumentException(
                                MarkupXmlDocument.GetMarkupSource(preset),
                                preset,
                                new InvalidOperationException(
                                    "Preset set '" +
                                    GetAttributeIgnoreNamespace(
                                        presets,
                                        "Name") +
                                    "' contains more than one preset named '" +
                                    presetName + "'."));
                        }

                        presetNames.Add(presetName, null);
                    }

                    ValidatePresetValueUniqueness(preset);
                }

                node = node.NextSibling;
            }
        }

        private static void ValidatePresetValueUniqueness(
            XmlElement preset)
        {
            Hashtable keys = new Hashtable(
                StringComparer.OrdinalIgnoreCase);
            XmlNode node = preset.FirstChild;

            while (node != null)
            {
                XmlElement value = node as XmlElement;

                if (value == null)
                {
                    ValidatePresetIgnorableContent(node, preset);
                }
                else if (!EqualsIgnoreCase(value.LocalName, "Set"))
                {
                    ThrowPresetCompositionException(
                        value,
                        "Unexpected <" + value.LocalName +
                        "> element inside preset '" +
                        GetAttributeIgnoreNamespace(preset, "Name") +
                        "'. Only <Set> elements are allowed.");
                }
                else
                {
                    ValidatePresetAttributes(
                        value,
                        new string[] { "Key", "Value" });
                    ValidatePresetIdentifier(
                        value,
                        "Key",
                        true);

                    string key = GetAttributeIgnoreNamespace(value, "Key");

                    if (!String.IsNullOrEmpty(key))
                    {
                        if (keys.ContainsKey(key))
                        {
                            throw CreateIncludedDocumentException(
                                MarkupXmlDocument.GetMarkupSource(value),
                                value,
                                new InvalidOperationException(
                                    "Preset '" +
                                    GetAttributeIgnoreNamespace(
                                        preset,
                                        "Name") +
                                    "' contains duplicate key '" +
                                    key + "'."));
                        }

                        keys.Add(key, null);
                    }

                    if (!HasAttributeIgnoreNamespace(value, "Value"))
                    {
                        ThrowPresetCompositionException(
                            value,
                            "The <Set> element for key '" + key +
                            "' in preset '" +
                            GetAttributeIgnoreNamespace(preset, "Name") +
                            "' requires a Value attribute.");
                    }

                    if (value.InnerText.Trim().Length != 0 ||
                        HasElementChild(value))
                    {
                        ThrowPresetCompositionException(
                            value,
                            "The <Set> element for key '" + key +
                            "' in preset '" +
                            GetAttributeIgnoreNamespace(preset, "Name") +
                            "' must be empty. Declare its value with the " +
                            "Value attribute.");
                    }
                }

                node = node.NextSibling;
            }
        }

        private static void ValidatePresetAttributes(
            XmlElement element,
            string[] allowedNames)
        {
            Hashtable seen = new Hashtable(
                StringComparer.OrdinalIgnoreCase);
            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];

                if (IsIncludeMetadataAttribute(attribute))
                    continue;

                bool allowed = false;
                int allowedIndex;

                for (allowedIndex = 0;
                     allowedIndex < allowedNames.Length;
                     allowedIndex++)
                {
                    if (attribute.NamespaceURI.Length == 0 &&
                        EqualsIgnoreCase(
                            attribute.LocalName,
                            allowedNames[allowedIndex]))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    ThrowPresetCompositionException(
                        element,
                        "Unexpected '" + attribute.Name +
                        "' attribute on <" + element.LocalName + ">.");
                }

                if (seen.ContainsKey(attribute.LocalName))
                {
                    ThrowPresetCompositionException(
                        element,
                        "The <" + element.LocalName +
                        "> element contains the '" +
                        attribute.LocalName +
                        "' attribute more than once.");
                }

                seen.Add(attribute.LocalName, null);
            }
        }

        private static void ValidatePresetIdentifier(
            XmlElement element,
            string attributeName,
            bool required)
        {
            bool present = HasAttributeIgnoreNamespace(
                element,
                attributeName);

            if (!present && !required)
                return;

            string value = GetAttributeIgnoreNamespace(
                element,
                attributeName);

            if (!present || String.IsNullOrEmpty(value) ||
                value.Trim().Length == 0)
            {
                ThrowPresetCompositionException(
                    element,
                    "The <" + element.LocalName + "> " +
                    attributeName + " attribute must be non-empty.");
            }

            if (value.Length != value.Trim().Length)
            {
                ThrowPresetCompositionException(
                    element,
                    "The <" + element.LocalName + "> " +
                    attributeName +
                    " attribute cannot contain leading or trailing whitespace.");
            }
        }

        private static void ValidatePresetIgnorableContent(
            XmlNode node,
            XmlElement parent)
        {
            if (node == null ||
                node.NodeType == XmlNodeType.Comment ||
                node.NodeType == XmlNodeType.Whitespace ||
                node.NodeType == XmlNodeType.SignificantWhitespace ||
                node.NodeType == XmlNodeType.ProcessingInstruction ||
                ((node.NodeType == XmlNodeType.Text ||
                  node.NodeType == XmlNodeType.CDATA) &&
                 node.Value != null &&
                 node.Value.Trim().Length == 0))
            {
                return;
            }

            ThrowPresetCompositionException(
                parent,
                "Unexpected content inside <" +
                parent.LocalName + ">.");
        }

        private static bool HasElementChild(XmlElement element)
        {
            XmlNode node = element.FirstChild;

            while (node != null)
            {
                if (node is XmlElement)
                    return true;

                node = node.NextSibling;
            }

            return false;
        }

        private static void ThrowPresetCompositionException(
            XmlElement element,
            string message)
        {
            throw CreateIncludedDocumentException(
                MarkupXmlDocument.GetMarkupSource(element),
                element,
                new InvalidOperationException(message));
        }

        private static void MergePresetDeclaration(
            XmlElement earlier,
            XmlElement later)
        {
            ReplaceAttributeWhenPresent(later, earlier, "Selected");
            ReplaceAttributeWhenPresent(later, earlier, "Default");
            PreserveUnmergedAttributes(
                later,
                earlier,
                new string[] { "Name", "Selected", "Default" });

            XmlNode node = later.FirstChild;

            while (node != null)
            {
                XmlNode next = node.NextSibling;
                XmlElement laterPreset = node as XmlElement;

                if (laterPreset != null &&
                    EqualsIgnoreCase(laterPreset.LocalName, "Preset"))
                {
                    string presetName = GetAttributeIgnoreNamespace(
                        laterPreset,
                        "Name");
                    XmlElement earlierPreset = FindNamedPreset(
                        earlier,
                        presetName);

                    if (earlierPreset == null)
                    {
                        earlier.AppendChild(laterPreset);
                    }
                    else
                    {
                        PreserveUnmergedAttributes(
                            laterPreset,
                            earlierPreset,
                            new string[] { "Name" });
                        MergePresetValues(
                            earlierPreset,
                            laterPreset);
                    }
                }
                else
                {
                    earlier.AppendChild(node);
                }

                node = next;
            }
        }

        private static void MergePresetValues(
            XmlElement earlierPreset,
            XmlElement laterPreset)
        {
            XmlNode node = laterPreset.FirstChild;

            while (node != null)
            {
                XmlNode next = node.NextSibling;
                XmlElement laterValue = node as XmlElement;

                if (laterValue != null &&
                    EqualsIgnoreCase(laterValue.LocalName, "Set"))
                {
                    string key = GetAttributeIgnoreNamespace(
                        laterValue,
                        "Key");
                    XmlElement earlierValue = FindPresetValue(
                        earlierPreset,
                        key);

                    if (earlierValue == null)
                    {
                        earlierPreset.AppendChild(laterValue);
                    }
                    else
                    {
                        earlierPreset.ReplaceChild(
                            laterValue,
                            earlierValue);
                    }
                }
                else
                {
                    earlierPreset.AppendChild(node);
                }

                node = next;
            }
        }

        private static void PreserveUnmergedAttributes(
            XmlElement source,
            XmlElement target,
            string[] mergedNames)
        {
            int i;

            for (i = 0; i < source.Attributes.Count; i++)
            {
                XmlAttribute attribute = source.Attributes[i];

                if (IsIncludeMetadataAttribute(attribute) ||
                    IsMergedPresetAttribute(attribute, mergedNames) ||
                    target.HasAttribute(
                        attribute.LocalName,
                        attribute.NamespaceURI))
                {
                    continue;
                }

                target.Attributes.Append(
                    target.OwnerDocument.ImportNode(
                        attribute,
                        true) as XmlAttribute);
            }
        }

        private static bool IsMergedPresetAttribute(
            XmlAttribute attribute,
            string[] names)
        {
            if (attribute.NamespaceURI.Length != 0)
                return false;

            int i;

            for (i = 0; i < names.Length; i++)
            {
                if (EqualsIgnoreCase(attribute.LocalName, names[i]))
                    return true;
            }

            return false;
        }

        private static bool IsIncludeMetadataAttribute(
            XmlAttribute attribute)
        {
            return IsConditionalIncludeMetadataAttribute(attribute) ||
                String.Equals(
                       attribute.Name,
                       "xmlns",
                       StringComparison.Ordinal) ||
                String.Equals(
                    attribute.Prefix,
                    "xmlns",
                    StringComparison.Ordinal) ||
                (attribute.NamespaceURI.Length == 0 &&
                 (String.Equals(
                      attribute.Name,
                      "__WfxPath",
                      StringComparison.Ordinal) ||
                  String.Equals(
                      attribute.Name,
                      MarkupXmlDocument.LocationAttributeName,
                      StringComparison.Ordinal))) ||
                (String.Equals(
                     attribute.NamespaceURI,
                     "http://www.w3.org/2001/XMLSchema-instance",
                     StringComparison.Ordinal) &&
                 (String.Equals(
                      attribute.LocalName,
                      "schemaLocation",
                      StringComparison.Ordinal) ||
                  String.Equals(
                      attribute.LocalName,
                      "noNamespaceSchemaLocation",
                      StringComparison.Ordinal)));
        }

        private static XmlElement FindNamedPreset(
            XmlElement presets,
            string name)
        {
            XmlNode node = presets.FirstChild;

            while (node != null)
            {
                XmlElement preset = node as XmlElement;

                if (preset != null &&
                    EqualsIgnoreCase(preset.LocalName, "Preset") &&
                    String.Equals(
                        GetAttributeIgnoreNamespace(preset, "Name"),
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return preset;
                }

                node = node.NextSibling;
            }

            return null;
        }

        private static XmlElement FindPresetValue(
            XmlElement preset,
            string key)
        {
            XmlNode node = preset.FirstChild;

            while (node != null)
            {
                XmlElement value = node as XmlElement;

                if (value != null &&
                    EqualsIgnoreCase(value.LocalName, "Set") &&
                    String.Equals(
                        GetAttributeIgnoreNamespace(value, "Key"),
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }

                node = node.NextSibling;
            }

            return null;
        }

        private static void ReplaceAttributeWhenPresent(
            XmlElement source,
            XmlElement target,
            string name)
        {
            XmlAttribute sourceAttribute =
                FindAttributeIgnoreNamespace(source, name);

            if (sourceAttribute == null)
                return;

            XmlAttribute existing =
                FindAttributeIgnoreNamespace(target, name);

            if (existing != null)
                target.Attributes.Remove(existing);

            target.Attributes.Append(
                target.OwnerDocument.ImportNode(
                    sourceAttribute,
                    true) as XmlAttribute);
        }

        private static XmlElement PrepareIncludedElement(
            XmlDocument ownerDocument,
            XmlElement includeRoot,
            XmlElement sourceElement)
        {
            XmlElement serializable =
                sourceElement.CloneNode(true) as XmlElement;

            CopyIncludeNamespaceDeclarations(
                includeRoot,
                serializable);
            MarkupXmlDocument.PersistElementLocations(serializable);

            return ownerDocument.ImportNode(
                serializable,
                true) as XmlElement;
        }

        private static void InsertIncludedResources(
            XmlDocument ownerDocument,
            XmlElement parent,
            XmlNode insertionPoint,
            XmlElement includedResources)
        {
            if (IsPropertyElement(parent) &&
                EqualsIgnoreCase(
                    GetPropertyElementName(parent.LocalName),
                    "Resources"))
            {
                XmlNode resourceNode = includedResources.FirstChild;

                while (resourceNode != null)
                {
                    XmlNode next = resourceNode.NextSibling;
                    parent.InsertBefore(
                        resourceNode,
                        insertionPoint);
                    resourceNode = next;
                }

                return;
            }

            XmlElement normalized = ownerDocument.CreateElement(
                includedResources.Prefix,
                parent.LocalName + ".Resources",
                includedResources.NamespaceURI);
            CopyElementContents(
                includedResources,
                normalized);
            parent.InsertBefore(
                normalized,
                insertionPoint);
        }

        private static void PromoteItemTemplateMetadataRecursive(
            XmlElement element)
        {
            if (element == null)
                return;

            if (IsItemTemplatePropertyElement(element))
                PromoteItemTemplateMetadata(element);

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                    PromoteItemTemplateMetadataRecursive(child);

                node = node.NextSibling;
            }
        }

        private static bool IsItemTemplatePropertyElement(
            XmlElement element)
        {
            XmlElement owner = element.ParentNode as XmlElement;

            return owner != null &&
                EqualsIgnoreCase(owner.LocalName, "ItemsControl") &&
                IsPropertyElement(element) &&
                EqualsIgnoreCase(
                    GetPropertyElementName(element.LocalName),
                    "ItemTemplate");
        }

        private static void PromoteItemTemplateMetadata(
            XmlElement itemTemplate)
        {
            XmlElement visualRoot = null;
            ArrayList metadataBeforeVisual = new ArrayList();
            ArrayList metadataAfterVisual = new ArrayList();
            XmlNode node = itemTemplate.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    if (IsItemTemplateMetadata(child))
                    {
                        (visualRoot == null
                            ? metadataBeforeVisual
                            : metadataAfterVisual).Add(child);
                    }
                    else if (visualRoot == null)
                    {
                        visualRoot = child;
                    }
                    else
                    {
                        throw CreateIncludedDocumentException(
                            MarkupXmlDocument.GetMarkupSource(child),
                            child,
                            new InvalidOperationException(
                                "ItemsControl.ItemTemplate must contain " +
                                "exactly one visual root element."));
                    }
                }

                node = node.NextSibling;
            }

            if (metadataBeforeVisual.Count == 0 &&
                metadataAfterVisual.Count == 0)
            {
                return;
            }

            if (visualRoot == null)
            {
                XmlElement metadata = metadataBeforeVisual.Count != 0
                    ? metadataBeforeVisual[0] as XmlElement
                    : metadataAfterVisual[0] as XmlElement;

                throw CreateIncludedDocumentException(
                    MarkupXmlDocument.GetMarkupSource(metadata),
                    metadata,
                    new InvalidOperationException(
                        "ItemsControl.ItemTemplate include metadata requires " +
                        "a visual template root."));
            }

            PromoteItemTemplateMetadata(
                itemTemplate,
                visualRoot,
                metadataBeforeVisual,
                true);
            PromoteItemTemplateMetadata(
                itemTemplate,
                visualRoot,
                metadataAfterVisual,
                false);
        }

        private static bool IsItemTemplateMetadata(XmlElement element)
        {
            return EqualsIgnoreCase(element.LocalName, "Presets") ||
                (IsPropertyElement(element) &&
                 EqualsIgnoreCase(
                     GetPropertyElementName(element.LocalName),
                     "Resources"));
        }

        private static void PromoteItemTemplateMetadata(
            XmlElement itemTemplate,
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
                        GetPropertyElementName(element.LocalName),
                        "Resources"))
                {
                    InsertIncludedResources(
                        itemTemplate.OwnerDocument,
                        visualRoot,
                        insertionPoint,
                        element);
                    itemTemplate.RemoveChild(element);
                }
                else
                {
                    visualRoot.InsertBefore(element, insertionPoint);
                }
            }
        }

        private static void MergeSiblingResourceDeclarationsRecursive(
            XmlElement parent)
        {
            if (parent == null)
                return;

            XmlElement retained = null;
            XmlNode node = parent.FirstChild;

            while (node != null)
            {
                XmlNode next = node.NextSibling;
                XmlElement child = node as XmlElement;

                if (child != null &&
                    IsResourcesPropertyForParent(child, parent))
                {
                    if (retained == null)
                    {
                        retained = child;
                    }
                    else
                    {
                        MoveResourceContents(child, retained);
                        parent.RemoveChild(child);
                    }
                }

                node = next;
            }

            node = parent.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                    MergeSiblingResourceDeclarationsRecursive(child);

                node = node.NextSibling;
            }
        }

        private static bool IsResourcesPropertyForParent(
            XmlElement element,
            XmlElement parent)
        {
            if (!IsPropertyElement(element) || parent == null)
                return false;

            string localName = element.LocalName;
            int separator = localName.LastIndexOf('.');

            if (separator <= 0 ||
                !EqualsIgnoreCase(
                    localName.Substring(separator + 1),
                    "Resources"))
            {
                return false;
            }

            return true;
        }

        private static void MoveResourceContents(
            XmlElement source,
            XmlElement target)
        {
            PreserveUnmergedAttributes(
                source,
                target,
                new string[0]);

            XmlNode node = source.FirstChild;

            while (node != null)
            {
                XmlNode next = node.NextSibling;
                XmlElement element = node as XmlElement;

                if (element != null)
                {
                    CopyIncludeNamespaceDeclarations(
                        source,
                        element);
                }

                target.AppendChild(node);
                node = next;
            }
        }

        private static void CopyElementContents(
            XmlElement source,
            XmlElement target)
        {
            int i;

            for (i = 0; i < source.Attributes.Count; i++)
            {
                XmlAttribute attribute = source.Attributes[i];
                target.Attributes.Append(
                    target.OwnerDocument.ImportNode(
                        attribute,
                        true) as XmlAttribute);
            }

            XmlNode child = source.FirstChild;

            while (child != null)
            {
                target.AppendChild(
                    target.OwnerDocument.ImportNode(
                        child,
                        true));
                child = child.NextSibling;
            }
        }

        private static void CopyIncludeNamespaceDeclarations(
            XmlElement includeRoot,
            XmlElement includedRoot)
        {
            if (includeRoot == null || includedRoot == null)
                return;

            int i;

            for (i = 0; i < includeRoot.Attributes.Count; i++)
            {
                XmlAttribute attribute = includeRoot.Attributes[i];
                bool namespaceDeclaration =
                    String.Equals(
                        attribute.Name,
                        "xmlns",
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(
                        attribute.Prefix,
                        "xmlns",
                        StringComparison.OrdinalIgnoreCase);

                if (namespaceDeclaration &&
                    !includedRoot.HasAttribute(attribute.Name))
                {
                    includedRoot.SetAttribute(
                        attribute.Name,
                        attribute.Value);
                }
            }
        }

        private static void ValidateIncludeMarker(
            XmlElement marker,
            IncludeCompositionContext context)
        {
            int attributeIndex;
            Hashtable seenAttributes = new Hashtable(
                StringComparer.OrdinalIgnoreCase);

            for (attributeIndex = 0;
                 attributeIndex < marker.Attributes.Count;
                 attributeIndex++)
            {
                XmlAttribute attribute =
                    marker.Attributes[attributeIndex];

                if (IsIncludeMetadataAttribute(attribute))
                {
                    continue;
                }

                bool allowed = attribute.NamespaceURI.Length == 0 &&
                    (EqualsIgnoreCase(attribute.LocalName, "Source") ||
                     EqualsIgnoreCase(attribute.LocalName, "SourceKind") ||
                     EqualsIgnoreCase(attribute.LocalName, "Assembly") ||
                     EqualsIgnoreCase(attribute.LocalName, "Condition"));

                if (allowed &&
                    seenAttributes.ContainsKey(attribute.LocalName))
                {
                    throw CreateIncludeMarkerException(
                        marker,
                        context,
                        attribute.LocalName,
                        new InvalidOperationException(
                            "An <Includes> reference contains the '" +
                            attribute.LocalName +
                            "' attribute more than once."));
                }

                if (allowed)
                {
                    seenAttributes.Add(attribute.LocalName, null);
                    continue;
                }

                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    attribute.LocalName,
                    new InvalidOperationException(
                        "Unexpected '" + attribute.Name +
                        "' attribute on an <Includes> reference. " +
                        "Allowed attributes are Source, SourceKind, Assembly, " +
                        "and Condition."));
            }

            string source = GetAttributeIgnoreNamespace(marker, "Source");

            if (String.IsNullOrEmpty(source) ||
                source.Trim().Length == 0)
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Source",
                    new InvalidOperationException(
                        "An <Includes> reference requires a non-empty Source attribute."));
            }

            string sourceKind = GetAttributeIgnoreNamespace(
                marker,
                "SourceKind");
            string assemblyName = GetAttributeIgnoreNamespace(
                marker,
                "Assembly");
            string condition = GetAttributeIgnoreNamespace(
                marker,
                "Condition");

            if (source.IndexOf('{') >= 0 ||
                source.IndexOf('}') >= 0)
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Source",
                    new InvalidOperationException(
                        "Include Source must be a static XML reference, not a binding expression."));
            }

            if ((!String.IsNullOrEmpty(sourceKind) &&
                 (sourceKind.IndexOf('{') >= 0 ||
                  sourceKind.IndexOf('}') >= 0)) ||
                (!String.IsNullOrEmpty(assemblyName) &&
                 (assemblyName.IndexOf('{') >= 0 ||
                  assemblyName.IndexOf('}') >= 0)))
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    !String.IsNullOrEmpty(sourceKind) &&
                        (sourceKind.IndexOf('{') >= 0 ||
                         sourceKind.IndexOf('}') >= 0)
                            ? "SourceKind"
                            : "Assembly",
                    new InvalidOperationException(
                        "Include SourceKind and Assembly must be static values."));
            }

            if (HasAttributeIgnoreNamespace(marker, "Condition"))
            {
                if (String.IsNullOrEmpty(condition) ||
                    condition.Trim().Length == 0)
                {
                    throw CreateIncludeMarkerException(
                        marker,
                        context,
                        "Condition",
                        new InvalidOperationException(
                            "Include Condition cannot be empty."));
                }

                BindingExpressionPlan plan;

                if (TryParseBindingExpression(condition, out plan) &&
                    plan.Mode == BindingMode.TwoWay)
                {
                    throw CreateIncludeMarkerException(
                        marker,
                        context,
                        "Condition",
                        new InvalidOperationException(
                            "Include Condition is structural and supports only " +
                            "OneWay bindings."));
                }

                if (condition.IndexOf('{') < 0 &&
                    condition.IndexOf('}') < 0)
                {
                    bool ignored;

                    if (!Boolean.TryParse(condition.Trim(), out ignored))
                    {
                        throw CreateIncludeMarkerException(
                            marker,
                            context,
                            "Condition",
                            new InvalidOperationException(
                                "Include Condition must be a Boolean literal or " +
                                "a Binding, Function, or Preset expression."));
                    }
                }
            }

            XmlNode node = marker.FirstChild;

            while (node != null)
            {
                if (node is XmlElement ||
                    ((node.NodeType == XmlNodeType.Text ||
                      node.NodeType == XmlNodeType.CDATA) &&
                     node.Value != null &&
                     node.Value.Trim().Length != 0))
                {
                    throw CreateIncludeMarkerException(
                        marker,
                        context,
                        null,
                        new InvalidOperationException(
                            "An <Includes Source='...'> reference must be empty."));
                }

                node = node.NextSibling;
            }
        }

        private static void ValidateIncludeDefinitionRoot(
            XmlElement includeRoot,
            string markupSource)
        {
            int i;

            for (i = 0; i < includeRoot.Attributes.Count; i++)
            {
                XmlAttribute attribute = includeRoot.Attributes[i];

                if (IsIncludeMetadataAttribute(attribute))
                    continue;

                throw CreateIncludedDocumentException(
                    markupSource,
                    includeRoot,
                    new InvalidOperationException(
                        "The root <Includes> definition cannot declare '" +
                        attribute.Name + "'."));
            }
        }

        private static void ValidateIncludeDefinitionContent(
            XmlElement includeRoot,
            string markupSource)
        {
            XmlNode node = includeRoot.FirstChild;

            while (node != null)
            {
                if ((node.NodeType == XmlNodeType.Text ||
                     node.NodeType == XmlNodeType.CDATA) &&
                    node.Value != null &&
                    node.Value.Trim().Length != 0)
                {
                    throw CreateIncludedDocumentException(
                        markupSource,
                        includeRoot,
                        new InvalidOperationException(
                            "An <Includes> definition accepts XML elements, not root text content."));
                }

                node = node.NextSibling;
            }
        }

        private static IncludeSourceKind ParseIncludeSourceKind(
            XmlElement marker)
        {
            string value = GetAttributeIgnoreNamespace(
                marker,
                "SourceKind");

            if (String.IsNullOrEmpty(value))
            {
                if (HasAttributeIgnoreNamespace(marker, "SourceKind"))
                {
                    throw new InvalidOperationException(
                        "Include SourceKind cannot be empty.");
                }

                return IncludeSourceKind.Registered;
            }

            if (EqualsIgnoreCase(value.Trim(), "Registered"))
                return IncludeSourceKind.Registered;

            if (EqualsIgnoreCase(value.Trim(), "EmbeddedResource"))
                return IncludeSourceKind.EmbeddedResource;

            if (EqualsIgnoreCase(value.Trim(), "File"))
                return IncludeSourceKind.File;

            throw new InvalidOperationException(
                "Include SourceKind must be Registered, EmbeddedResource, or File.");
        }

    }
}
