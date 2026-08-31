using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private ArrayList CompileTemplateBindingDefinitions(
            XmlElement expressionRoot)
        {
            ArrayList definitions = new ArrayList();
            CompileTemplateBindingDefinitionsRecursive(
                expressionRoot,
                definitions);
            return definitions;
        }

        private void CompileTemplateBindingDefinitionsRecursive(
            XmlElement element,
            ArrayList definitions)
        {
            if (element == null)
                return;

            try
            {
                ValidateStaticElementName(element);
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    element,
                    "Name",
                    ex);
            }

            // A nested ItemsControl owns a separate data context and compiles its
            // template when its own items are realized. Walking into that template
            // here would evaluate child bindings against the outer item.
            if (IsNestedItemsTemplateContainer(element) ||
                IsPresetDefinitionElement(element))
            {
                return;
            }

            string path =
                GetAttributeIgnoreNamespace(
                    element,
                    "__WfxPath");
            RegisteredComponent registeredComponent;
            bool registeredXmlComponent =
                TryGetRegisteredComponent(
                    element.LocalName,
                    out registeredComponent) &&
                registeredComponent.TemplateXml != null;

            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];
                bool includeCondition =
                    IsConditionalIncludeMetadataAttribute(attribute);

                if ((ShouldIgnoreAttribute(attribute) && !includeCondition) ||
                    EqualsIgnoreCase(attribute.LocalName, "Name"))
                {
                    continue;
                }

                string expression = attribute.Value;

                try
                {
                    if (!ContainsDynamicExpression(expression))
                        continue;

                    RenderBindingDefinition definition =
                        new RenderBindingDefinition();

                    definition.SourceElement = element;
                    definition.ElementPath = path;
                    definition.ElementPathIndices = ParseCompiledElementPath(path);
                    definition.TargetElementPath = path;
                    definition.AttributeName = includeCondition
                        ? "Condition"
                        : attribute.LocalName;
                    definition.XmlAttributeName = attribute.Name;
                    definition.Expression = expression;
                    definition.AffectsLayout =
                        AttributeCanAffectLayout(definition.AttributeName);
                    definition.PropertyElementValue = false;

                    if (includeCondition ||
                        EqualsIgnoreCase(attribute.LocalName, "Condition"))
                        definition.Kind = RenderBindingSlotKind.Condition;
                    else if (registeredXmlComponent)
                    {
                        definition.Kind = RenderBindingSlotKind.RebuildOnChange;
                        definition.ComponentOwned = true;
                    }
                    else if (EqualsIgnoreCase(element.LocalName, "Setter") &&
                             EqualsIgnoreCase(attribute.LocalName, "Value"))
                        definition.Kind = RenderBindingSlotKind.RebuildOnChange;
                    else if (EqualsIgnoreCase(attribute.LocalName, "Style") ||
                             EqualsIgnoreCase(attribute.LocalName, "ResourceStyle") ||
                             attribute.LocalName.IndexOf('.') >= 0)
                        definition.Kind = RenderBindingSlotKind.RebuildOnChange;
                    else
                        definition.Kind = RenderBindingSlotKind.Attribute;

                    definition.DirectPlan =
                        GetDirectTemplateBindingPlan(expression);
                    ValidateTemplateBindingDefinitionPlan(definition);
                    definitions.Add(definition);
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        element,
                        attribute.LocalName,
                        ex);
                }
            }

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    CompileTemplateBindingDefinitionsRecursive(
                        child,
                        definitions);
                }
                else if (node.NodeType == XmlNodeType.Text ||
                         node.NodeType == XmlNodeType.CDATA)
                {
                    string diagnosticProperty =
                        IsPropertyElement(element)
                            ? GetPropertyElementName(element.LocalName)
                            : "Text";

                    try
                    {
                        if (ContainsDynamicExpression(node.Value))
                        {
                            RenderBindingDefinition definition =
                                new RenderBindingDefinition();

                            definition.SourceElement = element;
                            definition.ElementPath = path;
                            definition.ElementPathIndices =
                                ParseCompiledElementPath(path);
                            definition.XmlAttributeName = null;
                            definition.Expression = node.Value;

                            XmlElement owner = element.ParentNode as XmlElement;
                            bool propertyElementValue =
                                IsPropertyElement(element) &&
                                owner != null &&
                                !HasElementChildren(element);

                            if (propertyElementValue)
                            {
                                string propertyName =
                                    GetPropertyElementName(element.LocalName);

                                definition.TargetElementPath =
                                    GetAttributeIgnoreNamespace(
                                        owner,
                                        "__WfxPath");
                                definition.AttributeName = propertyName;
                                definition.PropertyElementValue = true;
                                definition.AffectsLayout =
                                    AttributeCanAffectLayout(propertyName);

                                if (EqualsIgnoreCase(propertyName, "Style") ||
                                    EqualsIgnoreCase(
                                        propertyName,
                                        "ResourceStyle") ||
                                    propertyName.IndexOf('.') >= 0)
                                {
                                    definition.Kind =
                                        RenderBindingSlotKind.RebuildOnChange;
                                }
                                else
                                {
                                    definition.Kind =
                                        RenderBindingSlotKind.Attribute;
                                }
                            }
                            else
                            {
                                definition.TargetElementPath = path;
                                definition.AttributeName = "Text";
                                definition.PropertyElementValue = false;
                                definition.Kind =
                                    RenderBindingSlotKind.InnerText;
                                definition.AffectsLayout = true;
                            }

                            definition.DirectPlan =
                                GetDirectTemplateBindingPlan(
                                    definition.Expression);
                            ValidateTemplateBindingDefinitionPlan(definition);
                            definitions.Add(definition);
                        }
                    }
                    catch (WinFormsXamlLoadException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw CreateMarkupLoadException(
                            element,
                            diagnosticProperty,
                            ex);
                    }
                }

                node = node.NextSibling;
            }
        }

        private static BindingExpressionPlan GetDirectTemplateBindingPlan(
            string expression)
        {
            BindingExpressionPlan plan;

            return TryParseBindingExpression(
                    expression,
                    out plan)
                ? plan
                : null;
        }

        private static void ValidateTemplateBindingDefinitionPlan(
            RenderBindingDefinition definition)
        {
            if (definition == null ||
                definition.DirectPlan == null ||
                definition.DirectPlan.Mode != BindingMode.TwoWay)
            {
                return;
            }

            if (definition.DirectPlan.HasComputedExpression)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay cannot be combined with a computed " +
                    "Binding expression.");
            }

            if (definition.DirectPlan.HasNegation)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay cannot be combined with the ! binding operator.");
            }

            if (definition.Kind == RenderBindingSlotKind.Condition)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by item-template Condition bindings.");
            }

            if (definition.Kind == RenderBindingSlotKind.RebuildOnChange)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by item-template bindings " +
                    "that rebuild components, styles, or attached properties.");
            }

            if (!String.IsNullOrEmpty(definition.AttributeName) &&
                definition.AttributeName.IndexOf('.') >= 0)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay is not supported by attached properties.");
            }

            if (EqualsIgnoreCase(
                definition.AttributeName,
                "ItemsSource"))
            {
                throw new InvalidOperationException(
                    "ItemsSource is one-way. Modify the observable list or " +
                    "replace the source PropertyBinding value instead.");
            }
        }

        private static int[] ParseCompiledElementPath(
            string path)
        {
            if (String.IsNullOrEmpty(path) || path == "0")
                return new int[0];

            string[] parts = path.Split('.');

            if (parts.Length <= 1)
                return new int[0];

            int[] result = new int[parts.Length - 1];
            int i;

            for (i = 1; i < parts.Length; i++)
            {
                int index;

                if (!Int32.TryParse(
                    parts[i],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out index))
                {
                    index = 0;
                }

                result[i - 1] = Math.Max(0, index);
            }

            return result;
        }

        private static void IndexCompiledTemplateElements(
            XmlElement element,
            Hashtable elementsByPath)
        {
            if (element == null || elementsByPath == null)
                return;

            // A nested ItemsControl owns and compiles this subtree against its
            // own row context; the outer compiled definition list never targets it.
            if (IsNestedItemsTemplateContainer(element))
                return;

            string path = GetAttributeIgnoreNamespace(
                element,
                "__WfxPath");

            if (!String.IsNullOrEmpty(path))
                elementsByPath[path] = element;

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    IndexCompiledTemplateElements(
                        child,
                        elementsByPath);
                }

                node = node.NextSibling;
            }
        }

        private static XmlElement GetCompiledTemplateElement(
            XmlElement root,
            int[] indices)
        {
            XmlElement current = root;

            if (current == null || indices == null)
                return current;

            int depth;

            for (depth = 0; depth < indices.Length; depth++)
            {
                int wanted = indices[depth];
                int elementIndex = 0;
                XmlNode node = current.FirstChild;
                XmlElement next = null;

                while (node != null)
                {
                    XmlElement candidate = node as XmlElement;

                    if (candidate != null)
                    {
                        if (elementIndex == wanted)
                        {
                            next = candidate;
                            break;
                        }

                        elementIndex++;
                    }

                    node = node.NextSibling;
                }

                if (next == null)
                    return null;

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Applies only expressions known at template-compile time. This avoids recursively
        /// scanning every static XML attribute/text node for every realized item.
        /// </summary>
        private void ApplyCompiledTemplateBindings(
            XmlElement root,
            CompiledItemTemplate compiled,
            object dataContext,
            ArrayList evaluatedValues,
            Hashtable indexedElements)
        {
            if (root == null || compiled == null || compiled.BindingDefinitions == null)
                return;

            int i;

            for (i = 0; i < compiled.BindingDefinitions.Count; i++)
            {
                RenderBindingDefinition definition =
                    compiled.BindingDefinitions[i] as RenderBindingDefinition;

                if (definition == null)
                    continue;

                XmlElement target = indexedElements == null
                    ? GetCompiledTemplateElement(
                        root,
                        definition.ElementPathIndices)
                    : indexedElements[definition.ElementPath] as XmlElement;

                try
                {
                    object evaluatedValue = EvaluateTemplateExpressionValue(
                        definition.Expression,
                        dataContext);

                    if (evaluatedValues != null)
                        evaluatedValues.Add(evaluatedValue);

                    if (target == null)
                        continue;

                    // Registered XML components must receive the original binding
                    // expression. Their property-binding layer owns live updates
                    // inside the component; replacing the expression with its
                    // current value here would turn that binding into a constant.
                    if (definition.ComponentOwned)
                        continue;

                    if (definition.PropertyElementValue)
                    {
                        string resolvedValue =
                            IsUnsetPresetValue(evaluatedValue)
                                ? String.Empty
                                : BindingObjectToAttributeValue(
                                    evaluatedValue);

                        XmlNode valueNode = target.FirstChild;

                        while (valueNode != null)
                        {
                            if ((valueNode.NodeType == XmlNodeType.Text ||
                                 valueNode.NodeType == XmlNodeType.CDATA) &&
                                String.Equals(
                                    valueNode.Value,
                                    definition.Expression,
                                    StringComparison.Ordinal))
                            {
                                valueNode.Value = resolvedValue;
                            }

                            valueNode = valueNode.NextSibling;
                        }

                        continue;
                    }

                    if (definition.Kind == RenderBindingSlotKind.InnerText)
                    {
                        string resolvedText =
                            IsUnsetPresetValue(evaluatedValue)
                                ? String.Empty
                                : BindingValueToString(
                                    evaluatedValue);

                        XmlNode node = target.FirstChild;

                        while (node != null)
                        {
                            if ((node.NodeType == XmlNodeType.Text ||
                                 node.NodeType == XmlNodeType.CDATA) &&
                                String.Equals(
                                    node.Value,
                                    definition.Expression,
                                    StringComparison.Ordinal))
                            {
                                node.Value = resolvedText;
                            }

                            node = node.NextSibling;
                        }

                        continue;
                    }

                    XmlAttribute attribute = null;

                    if (!String.IsNullOrEmpty(definition.XmlAttributeName))
                    {
                        attribute =
                            target.Attributes[definition.XmlAttributeName];
                    }

                    if (attribute == null)
                    {
                        int n;

                        for (n = 0; n < target.Attributes.Count; n++)
                        {
                            XmlAttribute candidate = target.Attributes[n];

                            if (EqualsIgnoreCase(
                                candidate.LocalName,
                                definition.AttributeName))
                            {
                                attribute = candidate;
                                break;
                            }
                        }
                    }

                    if (attribute != null)
                    {
                        if (IsUnsetPresetValue(evaluatedValue))
                            target.Attributes.Remove(attribute);
                        else
                            attribute.Value = BindingObjectToAttributeValue(
                                evaluatedValue);
                    }
                }
                catch (WinFormsXamlLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw CreateMarkupLoadException(
                        target == null ? root : target,
                        definition.AttributeName,
                        ex);
                }
            }
        }

        private ArrayList CreateTemplateBindingSlotsFromDefinitions(
            ArrayList definitions,
            Hashtable elementMap,
            object dataContext,
            object eventTarget,
            Hashtable functionResults,
            ArrayList evaluatedValues)
        {
            ArrayList slots = new ArrayList();
            Hashtable previousCache = _activeFunctionResultCache;
            _activeFunctionResultCache = functionResults;

            try
            {
                int i;

                for (i = 0; definitions != null && i < definitions.Count; i++)
                {
                    RenderBindingDefinition definition =
                        definitions[i] as RenderBindingDefinition;

                    if (definition == null)
                        continue;

                    RenderBindingSlot slot = new RenderBindingSlot();
                    slot.SourceElement = definition.SourceElement;
                    slot.ElementPath = definition.ElementPath;
                    slot.AttributeName = definition.AttributeName;
                    slot.Expression = definition.Expression;
                    slot.DirectPlan = definition.DirectPlan;
                    slot.DataContext = dataContext;
                    slot.EventTarget = eventTarget;
                    slot.Target = elementMap == null
                        ? null
                        : elementMap[
                            String.IsNullOrEmpty(definition.TargetElementPath)
                                ? definition.ElementPath
                                : definition.TargetElementPath] as Control;
                    object initialValue =
                        evaluatedValues != null && i < evaluatedValues.Count
                            ? evaluatedValues[i]
                            : EvaluateTemplateExpressionValue(
                                definition.Expression,
                                dataContext);
                    slot.Kind = definition.Kind;
                    slot.AffectsLayout = definition.AffectsLayout;
                    slot.ComponentOwned = definition.ComponentOwned;
                    CommitRenderBindingSlotValue(
                        slot,
                        initialValue);
                    slots.Add(slot);
                }
            }
            finally
            {
                _activeFunctionResultCache = previousCache;
            }

            return slots;
        }

        private ArrayList CreateTemplateBindingSlots(
            XmlElement expressionRoot,
            Hashtable elementMap,
            object dataContext,
            Hashtable functionResults)
        {
            ArrayList slots = new ArrayList();
            Hashtable previousCache = _activeFunctionResultCache;
            _activeFunctionResultCache = functionResults;

            try
            {
                CreateTemplateBindingSlotsRecursive(
                    expressionRoot,
                    elementMap,
                    dataContext,
                    slots);
            }
            finally
            {
                _activeFunctionResultCache = previousCache;
            }

            return slots;
        }

        private void CreateTemplateBindingSlotsRecursive(
            XmlElement element,
            Hashtable elementMap,
            object dataContext,
            ArrayList slots)
        {
            if (element == null)
                return;

            ValidateStaticElementName(element);

            string path =
                GetAttributeIgnoreNamespace(
                    element,
                    "__WfxPath");

            Control target =
                elementMap == null
                    ? null
                    : elementMap[path] as Control;
            RegisteredComponent registeredComponent;
            bool registeredXmlComponent =
                TryGetRegisteredComponent(
                    element.LocalName,
                    out registeredComponent) &&
                registeredComponent.TemplateXml != null;

            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];
                bool includeCondition =
                    IsConditionalIncludeMetadataAttribute(attribute);

                if ((ShouldIgnoreAttribute(attribute) && !includeCondition) ||
                    EqualsIgnoreCase(attribute.LocalName, "Name"))
                {
                    continue;
                }

                string expression = attribute.Value;

                if (!ContainsDynamicExpression(expression))
                    continue;

                RenderBindingSlot slot = new RenderBindingSlot();
                slot.SourceElement = element;
                slot.ElementPath = path;
                slot.AttributeName = includeCondition
                    ? "Condition"
                    : attribute.LocalName;
                slot.Expression = expression;
                slot.DirectPlan =
                    GetDirectTemplateBindingPlan(expression);
                slot.DataContext = dataContext;
                slot.EventTarget = _activeComponentEventTarget;
                slot.Target = target;
                object initialValue = EvaluateTemplateExpressionValue(
                    expression,
                    dataContext);
                slot.AffectsLayout = AttributeCanAffectLayout(
                    slot.AttributeName);

                if (includeCondition ||
                    EqualsIgnoreCase(attribute.LocalName, "Condition"))
                {
                    slot.Kind = RenderBindingSlotKind.Condition;
                }
                else if (registeredXmlComponent)
                {
                    slot.Kind = RenderBindingSlotKind.RebuildOnChange;
                    slot.ComponentOwned = true;
                }
                else if (EqualsIgnoreCase(attribute.LocalName, "Style") ||
                         EqualsIgnoreCase(attribute.LocalName, "ResourceStyle") ||
                         attribute.LocalName.IndexOf('.') >= 0)
                {
                    slot.Kind = RenderBindingSlotKind.RebuildOnChange;
                }
                else
                {
                    slot.Kind = RenderBindingSlotKind.Attribute;
                }

                CommitRenderBindingSlotValue(
                    slot,
                    initialValue);
                slots.Add(slot);
            }

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    CreateTemplateBindingSlotsRecursive(
                        child,
                        elementMap,
                        dataContext,
                        slots);
                }
                else if ((node.NodeType == XmlNodeType.Text ||
                          node.NodeType == XmlNodeType.CDATA) &&
                         ContainsDynamicExpression(node.Value))
                {
                    RenderBindingSlot slot = new RenderBindingSlot();
                    slot.SourceElement = element;
                    slot.ElementPath = path;
                    slot.AttributeName = "Text";
                    slot.Expression = node.Value;
                    slot.DirectPlan =
                        GetDirectTemplateBindingPlan(node.Value);
                    slot.DataContext = dataContext;
                    slot.EventTarget = _activeComponentEventTarget;
                    slot.Target = target;
                    object initialValue = EvaluateTemplateExpressionValue(
                        node.Value,
                        dataContext);
                    slot.Kind = RenderBindingSlotKind.InnerText;
                    slot.AffectsLayout = true;
                    CommitRenderBindingSlotValue(
                        slot,
                        initialValue);
                    slots.Add(slot);
                }

                node = node.NextSibling;
            }
        }

        private static bool IsDirectFunctionExpression(
            string value)
        {
            string methodName;
            string argumentText;
            bool automaticDataContext;

            return TryParseFunctionExpression(
                value,
                out methodName,
                out argumentText,
                out automaticDataContext);
        }

        private static bool ExpressionContainsFunctionCall(
            string value)
        {
            if (String.IsNullOrEmpty(value))
                return false;

            if (IsDirectFunctionExpression(value))
                return true;

            int searchFrom = 0;

            while (searchFrom < value.Length)
            {
                int start = value.IndexOf('{', searchFrom);

                if (start < 0)
                    break;

                int end = value.IndexOf('}', start + 1);

                if (end < 0)
                    break;

                string expression = value.Substring(
                    start,
                    end - start + 1);

                if (IsDirectFunctionExpression(expression))
                    return true;

                searchFrom = end + 1;
            }

            return false;
        }

        private static bool ContainsDynamicExpression(string value)
        {
            // Every supported dynamic syntax starts with an opening brace. Static
            // attributes and text are the common case, so avoid all parser setup.
            if (String.IsNullOrEmpty(value) || value.IndexOf('{') < 0)
                return false;

            string path;
            string methodName;
            string argumentText;
            string presetSetName;
            string presetKey;
            bool automaticDataContext;

            if (TryParseBindingExpression(value, out path))
                return true;

            if (TryParseFunctionExpression(
                value,
                out methodName,
                out argumentText,
                out automaticDataContext))
            {
                return true;
            }

            if (TryParsePresetExpression(
                value,
                out presetSetName,
                out presetKey))
            {
                return true;
            }

            return value.IndexOf(
                       "{Binding",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(
                       "{Function",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ContainsPresetExpression(value);
        }
    }
}
