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
        private const int MaximumIncludeDepth = 64;
        private const int MaximumIncludeExpansionCount = 1024;

        private static void VerifyIncludeExpansionBudget(
            IncludeCompositionContext context,
            XmlElement marker)
        {
            context.Budget.ExpansionCount++;

            if (context.Budget.ExpansionCount <=
                MaximumIncludeExpansionCount)
            {
                return;
            }

            throw CreateIncludeMarkerException(
                marker,
                context,
                "Source",
                new InvalidOperationException(
                    "XML include composition exceeds the maximum of " +
                    MaximumIncludeExpansionCount.ToString() +
                    " expanded include documents."));
        }

        private static Assembly ResolveIncludeAssembly(
            string assemblyName,
            Assembly fallback,
            XmlElement marker,
            IncludeCompositionContext context)
        {
            if (String.IsNullOrEmpty(assemblyName))
                return fallback;

            try
            {
                return Assembly.Load(assemblyName.Trim());
            }
            catch (Exception ex)
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Assembly",
                    ex);
            }
        }

        private static ResolvedInclude ResolveInclude(
            string source,
            IncludeSourceKind sourceKind,
            Assembly assembly,
            IncludeCompositionContext context,
            XmlElement marker)
        {
            string normalized = source == null
                ? String.Empty
                : source.Trim();

            bool embeddedPrefix = normalized.StartsWith(
                "embedded://",
                StringComparison.OrdinalIgnoreCase);
            bool filePrefix = normalized.StartsWith(
                "file://",
                StringComparison.OrdinalIgnoreCase);

            if (embeddedPrefix)
            {
                if (sourceKind != IncludeSourceKind.EmbeddedResource)
                {
                    throw new InvalidOperationException(
                        "An embedded:// include requires " +
                        "SourceKind='EmbeddedResource'.");
                }

                normalized = normalized.Substring("embedded://".Length);
            }
            else if (filePrefix && sourceKind != IncludeSourceKind.File)
            {
                throw new InvalidOperationException(
                    "A file:// include requires SourceKind='File'.");
            }

            if (sourceKind != IncludeSourceKind.File &&
                NormalizeResourceFragment(normalized).Length == 0)
            {
                throw new InvalidOperationException(
                    "An embedded or registered XML include requires a " +
                    "non-empty resource reference.");
            }

            if (sourceKind == IncludeSourceKind.File)
            {
                return LoadFileInclude(
                    normalized,
                    context,
                    marker);
            }

            if (sourceKind == IncludeSourceKind.EmbeddedResource)
            {
                if (assembly == null)
                {
                    throw new InvalidOperationException(
                        "An embedded XML include requires a markup assembly.");
                }

                return LoadEmbeddedInclude(
                    assembly,
                    normalized,
                    context,
                    marker);
            }

            return LoadRegisteredInclude(
                assembly,
                normalized,
                context,
                marker);
        }

        private static ResolvedInclude LoadRegisteredInclude(
            Assembly assembly,
            string source,
            IncludeCompositionContext context,
            XmlElement marker)
        {
            RegisteredInclude registered = FindRegisteredInclude(
                assembly,
                source,
                context.StagedIncludesByAssembly);

            if (registered == null)
            {
                throw new InvalidOperationException(
                    "Registered XML include '" + source +
                    "' was not found" +
                    (assembly == null
                        ? " in the registered XML include catalog"
                        : " in preferred assembly '" +
                          assembly.FullName +
                          "' or the global registered XML include catalog") +
                    ". Call XamlRuntime.Register for its XML path or use " +
                    "SourceKind='EmbeddedResource' or SourceKind='File'.");
            }

            MarkupXmlDocument document = new MarkupXmlDocument();
            document.PreserveWhitespace = false;
            document.XmlResolver = null;
            document.LoadMarkup(registered.TemplateXml);
            MarkupXmlDocument.RestoreSerializedMetadata(
                document.DocumentElement);

            ResolvedInclude resolved = new ResolvedInclude();
            resolved.Document = document;
            resolved.Identity = CreateResourceIdentity(
                registered.ResourceAssembly,
                registered.ResourceName);
            resolved.MarkupAssembly = registered.ResourceAssembly;
            resolved.MarkupSource = registered.ResourceName;
            resolved.BasePath = context.BasePath;
            return resolved;
        }

        private static ResolvedInclude LoadEmbeddedInclude(
            Assembly assembly,
            string source,
            IncludeCompositionContext context,
            XmlElement marker)
        {
            string resourceName = FindEmbeddedIncludeResource(
                assembly,
                source);
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
            ResolvedInclude resolved = new ResolvedInclude();
            resolved.Document = document;
            resolved.Identity = CreateResourceIdentity(
                assembly,
                resourceName);
            resolved.MarkupAssembly = assembly;
            resolved.MarkupSource = resourceName;
            resolved.BasePath = context.BasePath;
            return resolved;
        }

        private static ResolvedInclude LoadFileInclude(
            string source,
            IncludeCompositionContext context,
            XmlElement marker)
        {
            string path = source;

            if (source.StartsWith(
                    "file://",
                    StringComparison.OrdinalIgnoreCase))
            {
                Uri uri;

                if (!Uri.TryCreate(source, UriKind.Absolute, out uri) ||
                    !uri.IsFile)
                {
                    throw new InvalidOperationException(
                        "The XML include file URI is invalid: '" + source + "'.");
                }

                path = uri.LocalPath;
            }

            if (!Path.IsPathRooted(path) &&
                !String.IsNullOrEmpty(context.BasePath))
            {
                path = Path.Combine(context.BasePath, path);
            }

            path = Path.GetFullPath(path);
            Stream stream = File.OpenRead(path);
            MarkupXmlDocument document = LoadIncludeMarkup(stream, path);
            ResolvedInclude resolved = new ResolvedInclude();
            IncludeSourceIdentity identity = new IncludeSourceIdentity();
            identity.FilePath = path;
            identity.DisplayName = path;
            resolved.Document = document;
            resolved.Identity = identity;
            resolved.MarkupAssembly = context.MarkupAssembly;
            resolved.MarkupSource = path;
            resolved.BasePath = Path.GetDirectoryName(path);
            return resolved;
        }

        private static MarkupXmlDocument LoadIncludeMarkup(
            Stream stream,
            string markupSource)
        {
            MarkupXmlDocument document = new MarkupXmlDocument();
            document.PreserveWhitespace = false;
            document.XmlResolver = null;

            try
            {
                using (stream)
                    document.LoadMarkup(stream);
            }
            catch (XmlException ex)
            {
                throw new WinFormsXamlLoadException(
                    markupSource,
                    null,
                    null,
                    ex.LineNumber,
                    ex.LinePosition,
                    ex);
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WinFormsXamlLoadException(
                    markupSource,
                    null,
                    null,
                    0,
                    0,
                    ex);
            }

            return document;
        }

        private static IncludeSourceIdentity CreateResourceIdentity(
            Assembly assembly,
            string resourceName)
        {
            IncludeSourceIdentity identity = new IncludeSourceIdentity();
            identity.Assembly = assembly;
            identity.ResourceName = resourceName;
            identity.DisplayName = resourceName;
            return identity;
        }

        private static void VerifyIncludeCycle(
            IncludeSourceIdentity identity,
            IncludeCompositionContext context,
            XmlElement marker)
        {
            if (context.ActiveSources.Count >= MaximumIncludeDepth)
            {
                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Source",
                    new InvalidOperationException(
                        "XML include nesting exceeds the maximum depth of " +
                        MaximumIncludeDepth.ToString() + "."));
            }

            int i;

            for (i = 0; i < context.ActiveSources.Count; i++)
            {
                IncludeSourceIdentity active =
                    context.ActiveSources[i] as IncludeSourceIdentity;

                if (!AreSameIncludeSource(active, identity))
                    continue;

                StringBuilder chain = new StringBuilder();
                int chainIndex;

                for (chainIndex = i;
                     chainIndex < context.ActiveSources.Count;
                     chainIndex++)
                {
                    if (chain.Length != 0)
                        chain.Append(" -> ");

                    chain.Append(
                        ((IncludeSourceIdentity)
                            context.ActiveSources[chainIndex]).DisplayName);
                }

                if (chain.Length != 0)
                    chain.Append(" -> ");

                chain.Append(identity.DisplayName);

                throw CreateIncludeMarkerException(
                    marker,
                    context,
                    "Source",
                    new InvalidOperationException(
                        "Circular XML include chain: " +
                        chain.ToString() + "."));
            }
        }

        private static bool AreSameIncludeSource(
            IncludeSourceIdentity left,
            IncludeSourceIdentity right)
        {
            if (left == null || right == null)
                return false;

            if (left.FilePath != null || right.FilePath != null)
            {
                return left.FilePath != null &&
                    right.FilePath != null &&
                    String.Equals(
                        left.FilePath,
                        right.FilePath,
                        StringComparison.OrdinalIgnoreCase);
            }

            return Object.ReferenceEquals(left.Assembly, right.Assembly) &&
                String.Equals(
                    left.ResourceName,
                    right.ResourceName,
                    StringComparison.Ordinal);
        }

        private static string BuildIncludePathPrefix(
            XmlElement marker,
            IncludeCompositionContext context,
            string includedSource)
        {
            if (marker == null)
                return "programmatic include '" + includedSource + "'";

            string source = GetIncludeElementMarkupSource(marker, context);
            string path = GetIncludeElementPath(marker);
            return "include '" + includedSource + "' from '" +
                source + "'" + path;
        }

        private static string GetIncludeElementPath(XmlElement element)
        {
            return GetMarkupElementPath(
                element,
                MarkupXmlDocument.GetElementPathPrefix(element));
        }

        private static string GetIncludeElementMarkupSource(
            XmlElement element,
            IncludeCompositionContext context)
        {
            string source = MarkupXmlDocument.GetMarkupSource(element);

            return String.IsNullOrEmpty(source)
                ? context.MarkupSource
                : source;
        }

        private static WinFormsXamlLoadException
            CreateIncludeMarkerException(
                XmlElement marker,
                IncludeCompositionContext context,
                string propertyName,
                Exception innerException)
        {
            WinFormsXamlLoadException existing =
                innerException as WinFormsXamlLoadException;

            if (existing != null)
            {
                string includePath = marker == null
                    ? "programmatic include from '" +
                      context.MarkupSource + "'"
                    : BuildIncludePathPrefix(
                        marker,
                        context,
                        existing.MarkupSource);

                if (!String.IsNullOrEmpty(existing.ElementPath))
                {
                    includePath += " -> " + existing.ElementPath;
                }

                return new WinFormsXamlLoadException(
                    existing.MarkupSource,
                    includePath,
                    existing.PropertyName,
                    existing.LineNumber,
                    existing.LinePosition,
                    existing);
            }

            int lineNumber = 0;
            int linePosition = 0;

            if (marker != null)
            {
                MarkupXmlDocument.GetLocation(
                    marker,
                    propertyName,
                    out lineNumber,
                    out linePosition);
            }

            return new WinFormsXamlLoadException(
                marker == null
                    ? context.MarkupSource
                    : GetIncludeElementMarkupSource(marker, context),
                marker == null
                    ? null
                    : GetIncludeElementPath(marker),
                propertyName,
                lineNumber,
                linePosition,
                innerException);
        }

        private static WinFormsXamlLoadException
            CreateProgrammaticIncludeException(
                string source,
                IncludeCompositionContext context,
                Exception innerException)
        {
            WinFormsXamlLoadException existing =
                innerException as WinFormsXamlLoadException;
            string path = "programmatic include '" + source +
                "' from '" + context.MarkupSource + "'";

            if (existing != null)
            {
                if (!String.IsNullOrEmpty(existing.ElementPath))
                    path += " -> " + existing.ElementPath;

                return new WinFormsXamlLoadException(
                    existing.MarkupSource,
                    path,
                    existing.PropertyName,
                    existing.LineNumber,
                    existing.LinePosition,
                    existing);
            }

            return new WinFormsXamlLoadException(
                context.MarkupSource,
                path,
                "Source",
                0,
                0,
                innerException);
        }

        private static WinFormsXamlLoadException
            CreateIncludedDocumentException(
                string markupSource,
                XmlElement element,
                Exception innerException)
        {
            int lineNumber = 0;
            int linePosition = 0;

            if (element != null)
            {
                MarkupXmlDocument.GetLocation(
                    element,
                    null,
                    out lineNumber,
                    out linePosition);
            }

            return new WinFormsXamlLoadException(
                markupSource,
                element == null
                    ? null
                    : GetIncludeElementPath(element),
                null,
                lineNumber,
                linePosition,
                innerException);
        }

    }
}
