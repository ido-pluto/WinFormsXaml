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
        private enum EmbeddedMarkupRootKind
        {
            Other,
            Component,
            Includes
        }

        private sealed class RegisteredInclude
        {
            public Assembly ResourceAssembly;
            public string ResourceName;
            public string TemplateXml;
        }

        private static readonly Hashtable _registeredIncludesByAssembly =
            new Hashtable();

        private static EmbeddedMarkupRootKind GetEmbeddedMarkupRootKind(
            Assembly assembly,
            string resourceName)
        {
            Stream stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new InvalidOperationException(
                    "Embedded XML resource '" + resourceName +
                    "' was not found in assembly '" +
                    assembly.FullName + "'.");
            }

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ConformanceLevel = ConformanceLevel.Document;
            settings.IgnoreComments = false;
            settings.IgnoreWhitespace = false;
            settings.ProhibitDtd = true;
            settings.XmlResolver = null;

            try
            {
                using (stream)
                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    EmbeddedMarkupRootKind kind =
                        EmbeddedMarkupRootKind.Other;
                    bool rootSeen = false;

                    while (reader.Read())
                    {
                        if (!rootSeen &&
                            reader.NodeType == XmlNodeType.Element)
                        {
                            rootSeen = true;

                            if (EqualsIgnoreCase(reader.LocalName, "Component"))
                                kind = EmbeddedMarkupRootKind.Component;
                            else if (EqualsIgnoreCase(reader.LocalName, "Includes"))
                                kind = EmbeddedMarkupRootKind.Includes;
                        }
                    }

                    if (!rootSeen)
                        throw new XmlException("Root element is missing.");

                    return kind;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Embedded XML resource '" + resourceName +
                    "' in assembly '" + assembly.FullName +
                    "' is not valid XML: " + ex.Message,
                    ex);
            }
        }

        private static RegisteredInclude ReadEmbeddedInclude(
            Assembly assembly,
            string resourceName)
        {
            Stream stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new InvalidOperationException(
                    "Embedded XML include resource '" + resourceName +
                    "' was not found in " + assembly.FullName + ".");
            }

            MarkupXmlDocument document = LoadIncludeMarkup(
                stream,
                resourceName);
            XmlElement root = document.DocumentElement;

            if (root == null ||
                !EqualsIgnoreCase(root.LocalName, "Includes"))
            {
                throw new InvalidOperationException(
                    "Embedded XML include resource '" + resourceName +
                    "' must have an <Includes> root.");
            }

            MarkupXmlDocument.SetOrigin(root, resourceName, null);

            RegisteredInclude include = new RegisteredInclude();
            include.ResourceAssembly = assembly;
            include.ResourceName = resourceName;
            include.TemplateXml =
                MarkupXmlDocument.SerializeElementWithLocations(root);
            return include;
        }

        private static Hashtable CreateStagedIncludeIndex(
            ArrayList includes)
        {
            Hashtable byAssembly = new Hashtable();
            int i;

            for (i = 0; i < includes.Count; i++)
            {
                RegisteredInclude include =
                    includes[i] as RegisteredInclude;
                Hashtable resources =
                    byAssembly[include.ResourceAssembly] as Hashtable;

                if (resources == null)
                {
                    resources = new Hashtable(StringComparer.Ordinal);
                    byAssembly.Add(include.ResourceAssembly, resources);
                }

                resources[include.ResourceName] = include;
            }

            return byAssembly;
        }

        private static void ValidateStagedIncludes(
            ArrayList includes,
            Hashtable stagedIncludesByAssembly)
        {
            int i;

            for (i = 0; i < includes.Count; i++)
            {
                RegisteredInclude include =
                    includes[i] as RegisteredInclude;
                XmlDocument parsed = new XmlDocument();
                parsed.PreserveWhitespace = false;
                parsed.XmlResolver = null;
                parsed.LoadXml(include.TemplateXml);

                IncludeCompositionContext context =
                    CreateIncludeCompositionContext(
                        null,
                        include.ResourceAssembly,
                        include.ResourceName,
                        stagedIncludesByAssembly);
                IncludeSourceIdentity identity = CreateResourceIdentity(
                    include.ResourceAssembly,
                    include.ResourceName);
                context.DeferFileIncludes = true;
                context.ActiveSources.Add(identity);
                XmlElement includeRoot = parsed.DocumentElement;

                ValidateIncludeDefinitionRoot(
                    includeRoot,
                    include.ResourceName);

                ExpandIncludesInElement(
                    includeRoot,
                    context);
                MergeSiblingPresetDeclarationsRecursive(
                    includeRoot);
                MergeSiblingResourceDeclarationsRecursive(
                    includeRoot);
                ValidateIncludeDefinitionContent(
                    includeRoot,
                    include.ResourceName);
            }
        }

        private static RegisteredInclude FindRegisteredInclude(
            Assembly assembly,
            string source,
            Hashtable stagedIncludesByAssembly)
        {
            if (assembly != null)
            {
                RegisteredInclude preferred =
                    FindRegisteredIncludeInAssembly(
                        assembly,
                        source,
                        stagedIncludesByAssembly);

                if (preferred != null)
                    return preferred;
            }

            return FindRegisteredIncludeAcrossAssemblies(
                source,
                stagedIncludesByAssembly);
        }

        private static RegisteredInclude FindRegisteredIncludeInAssembly(
            Assembly assembly,
            string source,
            Hashtable stagedIncludesByAssembly)
        {
            Hashtable candidates = new Hashtable(StringComparer.Ordinal);

            lock (_componentRegistrySync)
            {
                Hashtable registered =
                    _registeredIncludesByAssembly[assembly] as Hashtable;

                CopyRegisteredIncludes(registered, candidates);
            }

            if (stagedIncludesByAssembly != null)
            {
                Hashtable staged =
                    stagedIncludesByAssembly[assembly] as Hashtable;
                CopyRegisteredIncludes(staged, candidates);
            }

            string query = NormalizeResourceFragment(source);
            RegisteredInclude best = null;
            int bestRank = Int32.MaxValue;
            ArrayList bestNames = new ArrayList();

            foreach (DictionaryEntry entry in candidates)
            {
                string candidate = entry.Key as string;

                if (String.Equals(
                        candidate,
                        query,
                        StringComparison.Ordinal))
                {
                    return entry.Value as RegisteredInclude;
                }

                int rank = GetRegisteredIncludeMatchRank(
                    candidate,
                    query);

                if (rank == Int32.MaxValue)
                    continue;

                if (rank < bestRank)
                {
                    best = entry.Value as RegisteredInclude;
                    bestRank = rank;
                    bestNames.Clear();
                    bestNames.Add(candidate);
                }
                else if (rank == bestRank)
                {
                    bestNames.Add(candidate);
                }
            }

            if (bestNames.Count > 1)
            {
                throw new InvalidOperationException(
                    "Registered XML include reference '" + source +
                    "' is ambiguous in assembly '" + assembly.FullName +
                    "'. Candidates: " +
                    FormatEmbeddedXmlResourceCandidates(bestNames) +
                    ". Use a more specific registered resource path.");
            }

            return best;
        }

        private static RegisteredInclude
            FindRegisteredIncludeAcrossAssemblies(
            string source,
            Hashtable stagedIncludesByAssembly)
        {
            Hashtable candidatesByAssembly = new Hashtable();

            lock (_componentRegistrySync)
            {
                CopyRegisteredIncludeAssemblies(
                    _registeredIncludesByAssembly,
                    candidatesByAssembly);
            }

            CopyRegisteredIncludeAssemblies(
                stagedIncludesByAssembly,
                candidatesByAssembly);

            string query = NormalizeResourceFragment(source);
            RegisteredInclude best = null;
            int bestRank = Int32.MaxValue;
            ArrayList bestIncludes = new ArrayList();

            foreach (DictionaryEntry assemblyEntry in candidatesByAssembly)
            {
                Hashtable resources = assemblyEntry.Value as Hashtable;

                foreach (DictionaryEntry resourceEntry in resources)
                {
                    RegisteredInclude candidate =
                        resourceEntry.Value as RegisteredInclude;
                    int rank = GetRegisteredIncludeMatchRank(
                        candidate.ResourceName,
                        query);

                    if (rank == Int32.MaxValue)
                        continue;

                    if (rank < bestRank)
                    {
                        best = candidate;
                        bestRank = rank;
                        bestIncludes.Clear();
                        bestIncludes.Add(candidate);
                    }
                    else if (rank == bestRank)
                    {
                        bestIncludes.Add(candidate);
                    }
                }
            }

            if (bestIncludes.Count > 1)
            {
                throw new InvalidOperationException(
                    "Registered XML include reference '" + source +
                    "' is ambiguous across registered assemblies. Candidates: " +
                    FormatRegisteredIncludeCandidates(bestIncludes) +
                    ". Use a full resource name or load from a preferred " +
                    "markup assembly.");
            }

            return best;
        }

        private static int GetRegisteredIncludeMatchRank(
            string candidate,
            string query)
        {
            if (String.Equals(
                    candidate,
                    query,
                    StringComparison.Ordinal))
            {
                return -1;
            }

            if (String.Equals(
                    candidate,
                    query,
                    StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (candidate.EndsWith(
                    "." + query,
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.EndsWith(
                    "." + query + ".xml",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return candidate.IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0
                ? 2
                : Int32.MaxValue;
        }

        private static void CopyRegisteredIncludes(
            Hashtable source,
            Hashtable target)
        {
            if (source == null)
                return;

            foreach (DictionaryEntry entry in source)
                target[entry.Key] = entry.Value;
        }

        private static void CopyRegisteredIncludeAssemblies(
            Hashtable source,
            Hashtable target)
        {
            if (source == null)
                return;

            foreach (DictionaryEntry entry in source)
            {
                Assembly assembly = entry.Key as Assembly;
                Hashtable resources = entry.Value as Hashtable;
                Hashtable targetResources =
                    target[assembly] as Hashtable;

                if (targetResources == null)
                {
                    targetResources = new Hashtable(
                        StringComparer.Ordinal);
                    target[assembly] = targetResources;
                }

                CopyRegisteredIncludes(resources, targetResources);
            }
        }

        private static string FormatRegisteredIncludeCandidates(
            ArrayList includes)
        {
            ArrayList descriptions = new ArrayList();
            int i;

            for (i = 0; i < includes.Count; i++)
            {
                RegisteredInclude include =
                    includes[i] as RegisteredInclude;
                descriptions.Add(
                    include.ResourceName + " (" +
                    include.ResourceAssembly.FullName + ")");
            }

            descriptions.Sort(StringComparer.Ordinal);
            return FormatEmbeddedXmlResourceCandidates(descriptions);
        }

        private static string FindEmbeddedIncludeResource(
            Assembly assembly,
            string resourceNameOrFragment)
        {
            string query = NormalizeResourceFragment(
                resourceNameOrFragment);
            string[] resources = GetEmbeddedResourceNames(assembly);
            string best = null;
            int bestRank = Int32.MaxValue;
            ArrayList bestNames = new ArrayList();
            int i;

            for (i = 0; i < resources.Length; i++)
            {
                string candidate = resources[i];

                if (!candidate.EndsWith(
                        ".xml",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int rank;

                if (String.Equals(
                        candidate,
                        query,
                        StringComparison.Ordinal))
                {
                    rank = -1;
                }
                else if (String.Equals(
                             candidate,
                             query,
                             StringComparison.OrdinalIgnoreCase))
                {
                    rank = 0;
                }
                else if (candidate.EndsWith(
                             "." + query,
                             StringComparison.OrdinalIgnoreCase) ||
                         candidate.EndsWith(
                             "." + query + ".xml",
                             StringComparison.OrdinalIgnoreCase))
                {
                    rank = 1;
                }
                else if (candidate.IndexOf(
                             query,
                             StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    rank = 2;
                }
                else
                {
                    continue;
                }

                EmbeddedMarkupRootKind rootKind =
                    GetEmbeddedMarkupRootKind(
                        assembly,
                        candidate);

                if (rootKind != EmbeddedMarkupRootKind.Includes)
                {
                    if (rank <= 0)
                    {
                        throw new InvalidOperationException(
                            "Embedded XML resource '" + candidate +
                            "' exactly matches include reference '" +
                            resourceNameOrFragment +
                            "' but its root is not <Includes>.");
                    }

                    continue;
                }

                if (rank == -1)
                    return candidate;

                if (rank < bestRank)
                {
                    best = candidate;
                    bestRank = rank;
                    bestNames.Clear();
                    bestNames.Add(candidate);
                }
                else if (rank == bestRank)
                {
                    bestNames.Add(candidate);
                }
            }

            if (bestNames.Count > 1)
            {
                throw new InvalidOperationException(
                    "Embedded XML include reference '" +
                    resourceNameOrFragment +
                    "' is ambiguous in assembly '" + assembly.FullName +
                    "'. Candidates: " +
                    FormatEmbeddedXmlResourceCandidates(bestNames) +
                    ". Use a more specific embedded resource path.");
            }

            if (best != null)
                return best;

            throw new InvalidOperationException(
                "No embedded <Includes> XML resource containing '" +
                resourceNameOrFragment +
                "' was found in assembly '" + assembly.FullName + "'.");
        }

        private static void AddRegisteredResources(
            ArrayList components,
            ArrayList includes)
        {
            if (components == null)
                components = new ArrayList();

            if (includes == null)
                includes = new ArrayList();

            lock (_componentRegistrySync)
            {
                ValidateRegisteredComponentBatch(components);
                ValidateRegisteredIncludeBatch(includes);

                int i;

                for (i = 0; i < includes.Count; i++)
                {
                    RegisteredInclude include =
                        includes[i] as RegisteredInclude;
                    Hashtable resources =
                        _registeredIncludesByAssembly[
                            include.ResourceAssembly] as Hashtable;

                    if (resources == null)
                    {
                        resources = new Hashtable(StringComparer.Ordinal);
                        _registeredIncludesByAssembly.Add(
                            include.ResourceAssembly,
                            resources);
                    }

                    if (!resources.ContainsKey(include.ResourceName))
                        resources.Add(include.ResourceName, include);
                }

                PublishRegisteredComponents(components);
            }
        }

        private static void ValidateRegisteredComponentBatch(
            ArrayList components)
        {
            int i;

            for (i = 0; i < components.Count; i++)
            {
                RegisteredComponent component =
                    components[i] as RegisteredComponent;
                RegisteredComponent existing =
                    _registeredComponents[component.Name] as
                        RegisteredComponent;

                if (existing == null)
                    continue;

                bool sameType =
                    existing.ComponentType != null &&
                    Object.ReferenceEquals(
                        existing.ComponentType,
                        component.ComponentType);
                bool sameResource =
                    existing.ResourceAssembly != null &&
                    Object.ReferenceEquals(
                        existing.ResourceAssembly,
                        component.ResourceAssembly) &&
                    String.Equals(
                        existing.ResourceName,
                        component.ResourceName,
                        StringComparison.Ordinal);

                if (sameType || sameResource)
                    continue;

                throw new InvalidOperationException(
                    "A component named '" + component.Name +
                    "' is already registered from " +
                    DescribeComponentRegistration(existing) +
                    "; the attempted registration came from " +
                    DescribeComponentRegistration(component) + ".");
            }
        }

        private static void ValidateRegisteredIncludeBatch(
            ArrayList includes)
        {
            int i;

            for (i = 0; i < includes.Count; i++)
            {
                RegisteredInclude include =
                    includes[i] as RegisteredInclude;
                Hashtable resources =
                    _registeredIncludesByAssembly[
                        include.ResourceAssembly] as Hashtable;

                if (resources == null)
                    continue;

                RegisteredInclude existing =
                    resources[include.ResourceName] as RegisteredInclude;

                if (existing != null &&
                    !String.Equals(
                        existing.TemplateXml,
                        include.TemplateXml,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Embedded XML include resource '" +
                        include.ResourceName +
                        "' is already registered with different content.");
                }
            }
        }

        private static void PublishRegisteredComponents(
            ArrayList components)
        {
            int i;

            for (i = 0; i < components.Count; i++)
            {
                RegisteredComponent component =
                    components[i] as RegisteredComponent;

                if (!_registeredComponents.ContainsKey(component.Name))
                {
                    _registeredComponents.Add(
                        component.Name,
                        component);
                    _componentRegistryVersion++;
                }
            }
        }
    }
}
