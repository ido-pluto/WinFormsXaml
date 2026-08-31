using System;
using System.Collections;
using System.Xml;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private const string ConditionalIncludeAttributePrefix =
            "__WfxIncludeCondition";

        private static bool IsConditionalIncludeMetadataAttribute(
            XmlAttribute attribute)
        {
            return attribute != null &&
                attribute.NamespaceURI.Length == 0 &&
                attribute.LocalName.StartsWith(
                    ConditionalIncludeAttributePrefix,
                    StringComparison.Ordinal);
        }

        private static ArrayList GetConditionalIncludeAttributes(
            XmlElement element)
        {
            ArrayList result = null;
            int i;

            for (i = 0; element != null && i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];

                if (!IsConditionalIncludeMetadataAttribute(attribute))
                    continue;

                if (result == null)
                    result = new ArrayList();

                result.Add(attribute);
            }

            return result;
        }

        private static void ApplyConditionalIncludeMetadata(
            XmlElement includedElement,
            string condition)
        {
            if (includedElement == null || String.IsNullOrEmpty(condition))
                return;

            if (EqualsIgnoreCase(
                    includedElement.LocalName,
                    "Includes.Resources"))
            {
                XmlNode node = includedElement.FirstChild;

                while (node != null)
                {
                    XmlElement style = node as XmlElement;

                    if (style != null &&
                        EqualsIgnoreCase(style.LocalName, "Style"))
                    {
                        AppendConditionalIncludeAttribute(style, condition);
                    }

                    node = node.NextSibling;
                }

                return;
            }

            // Presets are catalogs required to evaluate selected-name and key
            // expressions. Their definitions are imported unconditionally;
            // the include Condition gates visual and style contributions.
            if (EqualsIgnoreCase(includedElement.LocalName, "Presets"))
                return;

            if (IsPropertyElement(includedElement))
            {
                throw new InvalidOperationException(
                    "A conditional include cannot contribute a top-level '" +
                    includedElement.LocalName +
                    "' property element. Put that property inside a conditional " +
                    "visual root, or use Includes.Resources for styles.");
            }

            AppendConditionalIncludeAttribute(includedElement, condition);
        }

        private static void AppendConditionalIncludeAttribute(
            XmlElement element,
            string condition)
        {
            int suffix = 0;
            string name = ConditionalIncludeAttributePrefix;

            while (element.HasAttribute(name))
            {
                suffix++;
                name = ConditionalIncludeAttributePrefix +
                    "." + suffix.ToString();
            }

            element.SetAttribute(name, condition);
        }

        private bool EvaluateConditionalIncludeConditions(
            ArrayList attributes,
            object dataContext,
            out bool hasDynamicCondition)
        {
            hasDynamicCondition = false;
            int i;

            for (i = 0; attributes != null && i < attributes.Count; i++)
            {
                XmlAttribute attribute = attributes[i] as XmlAttribute;

                if (attribute == null)
                    continue;

                if (ContainsDynamicExpression(attribute.Value))
                {
                    hasDynamicCondition = true;
                    continue;
                }

                if (!EvaluateConditionExpressionValue(
                        attribute.Value,
                        dataContext,
                        "Include Condition"))
                {
                    return false;
                }
            }

            return true;
        }

        private ArrayList CaptureConditionalIncludeBindings(
            XmlElement element,
            ArrayList attributes,
            object dataContext,
            ArrayList bindings)
        {
            if (element == null ||
                attributes == null ||
                (_templateBuildDepth != 0 && _componentBuildDepth == 0))
            {
                return bindings;
            }

            int i;

            for (i = 0; i < attributes.Count; i++)
            {
                XmlAttribute attribute = attributes[i] as XmlAttribute;

                if (attribute == null ||
                    !ContainsDynamicExpression(attribute.Value))
                {
                    continue;
                }

                DynamicPropertyBinding binding =
                    new DynamicPropertyBinding();
                binding.PropertyName = "Condition";
                binding.Markup = CaptureDynamicBindingMarkup(
                    element,
                    "Condition",
                    attribute.LocalName);
                binding.Expression = attribute.Value;
                binding.DataContext = dataContext;
                binding.UsesPreset =
                    ContainsPresetExpression(attribute.Value);
                binding.MayUsePreset =
                    binding.UsesPreset ||
                    ComponentDataContextMayUsePresets(dataContext);
                binding.Active = true;
                CaptureComponentScope(binding);
                CaptureInitialDynamicObservableSnapshot(binding);

                if (bindings == null)
                    bindings = new ArrayList();

                bindings.Add(binding);
            }

            return bindings;
        }
    }
}
