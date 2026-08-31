using System.Reflection;

namespace WinFormsXaml
{
    /// <summary>
    /// Selects how an XML include reference is resolved.
    /// </summary>
    public enum IncludeSourceKind
    {
        /// <summary>
        /// Resolves a reusable XML document registered through XamlRuntime.
        /// </summary>
        Registered = 0,

        /// <summary>Resolves the reference as an embedded XML resource.</summary>
        EmbeddedResource = 1,

        /// <summary>Resolves the reference as an XML file.</summary>
        File = 2
    }

    /// <summary>
    /// Immutable input passed from XmlForm to the XML include composition
    /// stage. It is internal because applications configure it through the
    /// protected XmlForm.Include overloads.
    /// </summary>
    internal sealed class XmlIncludeRequest
    {
        private readonly string _source;
        private readonly IncludeSourceKind _sourceKind;
        private readonly Assembly _assembly;

        internal XmlIncludeRequest(
            string source,
            IncludeSourceKind sourceKind,
            Assembly assembly)
        {
            _source = source;
            _sourceKind = sourceKind;
            _assembly = assembly;
        }

        internal string Source
        {
            get { return _source; }
        }

        internal IncludeSourceKind SourceKind
        {
            get { return _sourceKind; }
        }

        internal Assembly Assembly
        {
            get { return _assembly; }
        }
    }
}
