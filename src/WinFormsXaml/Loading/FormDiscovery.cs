using System;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Xml;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private sealed class EmbeddedXmlResourceNameComparer : IComparer
        {
            public int Compare(object leftValue, object rightValue)
            {
                string left = leftValue as string;
                string right = rightValue as string;
                int comparison = StringComparer.OrdinalIgnoreCase.Compare(
                    left,
                    right);

                return comparison != 0
                    ? comparison
                    : StringComparer.Ordinal.Compare(left, right);
            }
        }

        private static readonly IComparer _embeddedXmlResourceNameComparer =
            new EmbeddedXmlResourceNameComparer();
        private const int EmbeddedResourceAssemblyCacheLimit = 64;
        private static readonly object _embeddedResourceNamesSync =
            new object();
        private static readonly Hashtable _embeddedResourceNamesByAssembly =
            new Hashtable();
        // Manifest names are immutable for a loaded Assembly. Retain only a
        // bounded hot set of successful normalized queries so repeated XmlForm
        // construction avoids another full manifest-name scan.
        private const int EmbeddedResourceResolutionCacheLimit = 512;
        private const int EmbeddedResourceResolutionPerAssemblyLimit = 64;
        private const int EmbeddedResourceResolutionQueryLengthLimit = 1024;
        private static readonly Hashtable
            _embeddedResourceResolutionsByAssembly = new Hashtable();
        private static int _embeddedResourceResolutionCount;

        internal static string FindEmbeddedXmlResource(
            Assembly assembly,
            string resourceNameOrFragment)
        {
            if (String.IsNullOrEmpty(resourceNameOrFragment) ||
                resourceNameOrFragment.Trim().Length == 0)
            {
                throw new ArgumentException(
                    "An embedded Form resource name or path fragment is required.",
                    "resourceNameOrFragment");
            }

            string query = NormalizeResourceFragment(
                resourceNameOrFragment);

            if (query.Length == 0)
            {
                throw new ArgumentException(
                    "An embedded Form resource name or path fragment is required.",
                    "resourceNameOrFragment");
            }

            string cachedResource;

            if (TryGetEmbeddedXmlResourceResolution(
                    assembly,
                    query,
                    out cachedResource))
            {
                return cachedResource;
            }

            string[] resources = GetEmbeddedResourceNames(assembly);
            string best = null;
            int bestRank = Int32.MaxValue;
            int bestDistance = Int32.MaxValue;
            int i;

            // Manifest resource names are case-sensitive. Prefer the true
            // exact name before applying the case-insensitive convenience
            // matching rules below.
            for (i = 0; i < resources.Length; i++)
            {
                if (resources[i].EndsWith(
                        ".xml",
                        StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(
                        resources[i],
                        query,
                        StringComparison.Ordinal))
                {
                    CacheEmbeddedXmlResourceResolution(
                        assembly,
                        query,
                        resources[i]);
                    return resources[i];
                }
            }

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

                int distance = Math.Abs(candidate.Length - query.Length);

                // Select only a strictly better ranked candidate. An equal
                // rank and distance uses the shared resource-name ordering,
                // avoiding manifest enumeration order without sorting or
                // allocating a second resource collection.
                if (rank < bestRank ||
                    (rank == bestRank &&
                     (distance < bestDistance ||
                      (distance == bestDistance &&
                       _embeddedXmlResourceNameComparer.Compare(
                           candidate,
                           best) < 0))))
                {
                    best = candidate;
                    bestRank = rank;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                throw new InvalidOperationException(
                    "No embedded XML resource containing '" +
                    resourceNameOrFragment +
                    "' was found in " +
                    "assembly '" +
                    assembly.FullName +
                    "'" +
                    ". Available embedded XML resources: " +
                    FormatEmbeddedXmlResourceCandidates(resources) +
                    ".");
            }

            CacheEmbeddedXmlResourceResolution(
                assembly,
                query,
                best);

            return best;
        }

        private static bool TryGetEmbeddedXmlResourceResolution(
            Assembly assembly,
            string query,
            out string resourceName)
        {
            resourceName = null;

            if (query == null ||
                query.Length >
                    EmbeddedResourceResolutionQueryLengthLimit)
            {
                return false;
            }

            lock (_embeddedResourceNamesSync)
            {
                Hashtable resolutions =
                    _embeddedResourceResolutionsByAssembly[assembly]
                    as Hashtable;

                if (resolutions == null)
                    return false;

                resourceName = resolutions[query] as string;
                return resourceName != null;
            }
        }

        private static void CacheEmbeddedXmlResourceResolution(
            Assembly assembly,
            string query,
            string resourceName)
        {
            if (query == null ||
                resourceName == null ||
                query.Length >
                    EmbeddedResourceResolutionQueryLengthLimit)
            {
                return;
            }

            lock (_embeddedResourceNamesSync)
            {
                Hashtable resolutions =
                    _embeddedResourceResolutionsByAssembly[assembly]
                    as Hashtable;

                if (resolutions != null &&
                    resolutions.ContainsKey(query))
                {
                    return;
                }

                if (_embeddedResourceResolutionCount >=
                        EmbeddedResourceResolutionCacheLimit)
                {
                    return;
                }

                if (resolutions == null)
                {
                    if (_embeddedResourceResolutionsByAssembly.Count >=
                        EmbeddedResourceAssemblyCacheLimit)
                    {
                        return;
                    }

                    resolutions = new Hashtable(StringComparer.Ordinal);
                    _embeddedResourceResolutionsByAssembly[assembly] =
                        resolutions;
                }

                if (resolutions.Count >=
                    EmbeddedResourceResolutionPerAssemblyLimit)
                {
                    return;
                }

                resolutions.Add(query, resourceName);
                _embeddedResourceResolutionCount++;
            }
        }

        private static string[] GetEmbeddedResourceNames(
            Assembly assembly)
        {
            lock (_embeddedResourceNamesSync)
            {
                string[] cached =
                    _embeddedResourceNamesByAssembly[assembly] as string[];

                if (cached != null)
                    return cached;
            }

            string[] discovered = assembly.GetManifestResourceNames();

            lock (_embeddedResourceNamesSync)
            {
                string[] cached =
                    _embeddedResourceNamesByAssembly[assembly] as string[];

                if (cached != null)
                    return cached;

                if (_embeddedResourceNamesByAssembly.Count <
                    EmbeddedResourceAssemblyCacheLimit)
                {
                    _embeddedResourceNamesByAssembly[assembly] = discovered;
                }
            }

            return discovered;
        }

        private static string FormatEmbeddedXmlResourceCandidates(
            string[] resources)
        {
            ArrayList candidates = new ArrayList();
            int i;

            for (i = 0; resources != null && i < resources.Length; i++)
            {
                string resource = resources[i];

                if (resource != null &&
                    resource.EndsWith(
                        ".xml",
                        StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(resource);
                }
            }

            return FormatEmbeddedXmlResourceCandidates(candidates);
        }

        private static string FormatEmbeddedXmlResourceCandidates(
            ArrayList candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return "none";

            ArrayList ordered = new ArrayList(candidates);
            ordered.Sort(_embeddedXmlResourceNameComparer);

            const int candidateLimit = 8;
            int displayed = Math.Min(candidateLimit, ordered.Count);
            StringBuilder text = new StringBuilder();
            int i;

            for (i = 0; i < displayed; i++)
            {
                if (i != 0)
                    text.Append(", ");

                text.Append('\'');
                text.Append((string)ordered[i]);
                text.Append('\'');
            }

            if (ordered.Count > displayed)
            {
                text.Append(", and ");
                text.Append((ordered.Count - displayed).ToString());
                text.Append(" more");
            }

            return text.ToString();
        }

        private static string NormalizeResourceFragment(string value)
        {
            string normalized = value.Trim()
                .Replace('\\', '.')
                .Replace('/', '.');

            while (normalized.StartsWith("."))
                normalized = normalized.Substring(1);

            while (normalized.EndsWith("."))
                normalized = normalized.Substring(0, normalized.Length - 1);

            return normalized;
        }

        private static Type ResolveMarkupClassType(
            XmlElement root,
            object eventTarget,
            Assembly markupAssembly)
        {
            string className = GetAttributeIgnoreNamespace(root, "Class");

            if (String.IsNullOrEmpty(className))
                return null;

            className = className.Trim();

            if (className.Length == 0 ||
                className.IndexOf('{') >= 0 ||
                className.IndexOf('}') >= 0)
            {
                throw new InvalidOperationException(
                    "Form Class must be a static CLR type name.");
            }

            Type classType = FindMarkupClassType(
                className,
                eventTarget,
                markupAssembly);

            if (classType == null)
            {
                throw new InvalidOperationException(
                    "Form Class type '" + className + "' was not found.");
            }

            if (!classType.IsClass ||
                classType.IsAbstract ||
                classType.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    "Form Class type '" +
                    className +
                    "' must be a concrete, closed class.");
            }

            if (eventTarget != null)
            {
                if (!classType.IsInstanceOfType(eventTarget))
                {
                    throw new InvalidOperationException(
                        "The supplied code-behind object is not an instance of " +
                        "Form Class '" + className + "'.");
                }
            }

            return classType;
        }

        private static object CreateMarkupClassTarget(Type classType)
        {
            if (classType == null)
                throw new ArgumentNullException("classType");

            ConstructorInfo constructor = classType.GetConstructor(
                Type.EmptyTypes);

            if (constructor == null)
            {
                throw new InvalidOperationException(
                    "Form Class type '" +
                    classType.FullName +
                    "' needs a public parameterless constructor.");
            }

            try
            {
                return constructor.Invoke(_emptyObjectArray);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not create Form Class '" +
                    classType.FullName +
                    "': " + ex.Message,
                    ex);
            }
        }

        private static Type FindMarkupClassType(
            string className,
            object eventTarget,
            Assembly markupAssembly)
        {
            Assembly preferred = eventTarget == null
                ? markupAssembly
                : eventTarget.GetType().Assembly;
            Type resolved;

            if (preferred != null)
            {
                resolved = preferred.GetType(className, false, true);

                if (resolved != null)
                    return resolved;
            }

            // Assembly-qualified names and framework types are resolved after
            // the markup/code-behind assembly so an unrelated type in the
            // WinFormsXaml assembly cannot shadow the application's Class.
            resolved = Type.GetType(className, false);

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Type uniqueMatch = resolved;
            int i;

            for (i = 0; i < assemblies.Length; i++)
            {
                if (Object.ReferenceEquals(assemblies[i], preferred))
                    continue;

                resolved = assemblies[i].GetType(className, false, true);

                if (resolved == null)
                    continue;

                if (uniqueMatch == null)
                {
                    uniqueMatch = resolved;
                    continue;
                }

                if (!Object.ReferenceEquals(uniqueMatch, resolved))
                {
                    throw new InvalidOperationException(
                        "Form Class type '" +
                        className +
                        "' is ambiguous across loaded assemblies. " +
                        "Use an assembly-qualified type name.");
                }
            }

            return uniqueMatch;
        }
    }
}
