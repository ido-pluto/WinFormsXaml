using System;
using System.Collections;
using System.Xml;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        /// <summary>
        /// Determines whether one logical source item always contributes one
        /// ItemTemplate root to the direct logical-index viewport.
        /// </summary>
        /// <remarks>
        /// A Condition on the ItemTemplate root, on its registered-component
        /// invocation, or on a registered XML component template root can remove
        /// that logical item from layout. A collapsed or dynamic root Visibility
        /// has the same variable-membership problem: the fixed model cannot store
        /// zero slots, and an off-screen variable slot cannot observe a later
        /// Collapsed-to-Visible change. Such templates deliberately return false
        /// and use the normal keyed renderer. Hidden/Visible roots retain layout
        /// space and remain eligible. Conditions below the stable visual root
        /// affect descendants only and do not disable virtualization.
        /// A missing template is eligible only for an empty source; a nonempty
        /// source falls back so the existing missing-ItemTemplate validation owns
        /// the user-facing error.
        /// </remarks>
        internal bool CanUseDirectViewportVirtualization(
            XmlElement templateRoot,
            int logicalItemCount)
        {
            if (logicalItemCount < 0)
                throw new ArgumentOutOfRangeException("logicalItemCount");

            if (templateRoot == null)
                return logicalItemCount == 0;

            XmlElement expansionRoot = templateRoot;
            Hashtable componentChain = new Hashtable();

            while (expansionRoot != null)
            {
                // Presence, rather than expression evaluation, is intentional:
                // eligibility is a metadata-only decision and must never build
                // controls or execute item callbacks.
                if (HasAttributeIgnoreNamespace(
                        expansionRoot,
                        "Condition"))
                {
                    return false;
                }

                if (RootVisibilityCanCollapse(expansionRoot))
                    return false;

                RegisteredComponent component;

                if (!TryGetRegisteredComponent(
                        expansionRoot.LocalName,
                        out component))
                {
                    // A normal built-in/CLR visual root ends the expansion. If
                    // it cannot be resolved, conservatively use the keyed path;
                    // that path retains the established load diagnostic.
                    try
                    {
                        return ResolveXamlType(
                                expansionRoot.LocalName,
                                expansionRoot) != null;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }

                if (component.TemplateXml == null)
                {
                    // A registered CLR component is itself the stable root.
                    // A malformed registry entry is not eligible.
                    return component.ComponentType != null;
                }

                if (componentChain.ContainsKey(component))
                    return false;

                componentChain.Add(component, null);

                try
                {
                    expansionRoot =
                        GetParsedRegisteredComponentTemplate(component);
                }
                catch (Exception)
                {
                    // Invalid or missing component metadata must not make the
                    // eligibility probe throw or recurse indefinitely. Normal
                    // rendering remains responsible for its precise diagnostic.
                    return false;
                }
            }

            return false;
        }

        private static bool RootVisibilityCanCollapse(
            XmlElement root)
        {
            string visibility = GetAttributeIgnoreNamespace(
                root,
                "Visibility");

            if (visibility == null)
                return false;

            visibility = visibility.Trim();

            return EqualsIgnoreCase(visibility, "Collapsed") ||
                   ContainsDynamicExpression(visibility);
        }
    }
}
