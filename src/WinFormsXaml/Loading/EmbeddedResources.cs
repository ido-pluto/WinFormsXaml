using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        /// <summary>
        /// Loads XML embedded in the event target's assembly. When no event
        /// target is supplied, the application entry assembly is used.
        /// </summary>
        public static XamlRuntime LoadEmbedded(
            string resourceName,
            object eventTarget)
        {
            return LoadEmbedded(
                ResolveEmbeddedAssembly(eventTarget),
                resourceName,
                eventTarget,
                null);
        }

        /// <summary>
        /// Loads XML embedded in the event target's assembly with a preset
        /// manager that may be shared by several runtimes.
        /// </summary>
        public static XamlRuntime LoadEmbedded(
            string resourceName,
            object eventTarget,
            PresetManager presetManager)
        {
            return LoadEmbedded(
                ResolveEmbeddedAssembly(eventTarget),
                resourceName,
                eventTarget,
                presetManager);
        }

        /// <summary>Loads XML from a named manifest resource.</summary>
        public static XamlRuntime LoadEmbedded(
            Assembly assembly,
            string resourceName,
            object eventTarget)
        {
            return LoadEmbedded(
                assembly,
                resourceName,
                eventTarget,
                null);
        }

        /// <summary>
        /// Loads XML from a named manifest resource with a preset manager that
        /// may be shared by several runtimes.
        /// </summary>
        public static XamlRuntime LoadEmbedded(
            Assembly assembly,
            string resourceName,
            object eventTarget,
            PresetManager presetManager)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly");

            if (String.IsNullOrEmpty(resourceName))
            {
                throw new ArgumentException(
                    "The embedded XML resource name cannot be empty.",
                    "resourceName");
            }

            using (Stream stream =
                assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Embedded XML resource '" + resourceName +
                        "' was not found in " + assembly.FullName + ".");
                }

                return Load(
                    stream,
                    eventTarget,
                    Application.StartupPath,
                    presetManager,
                    assembly,
                    resourceName);
            }
        }

        private static Assembly ResolveEmbeddedAssembly(object eventTarget)
        {
            if (eventTarget != null)
                return eventTarget.GetType().Assembly;

            Assembly assembly = Assembly.GetEntryAssembly();

            return assembly == null
                ? Assembly.GetExecutingAssembly()
                : assembly;
        }
    }
}
