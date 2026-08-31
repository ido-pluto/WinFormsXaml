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
        private TemplateExpressionPlan GetTemplateExpressionPlan(
            string expression)
        {
            if (expression == null)
                expression = String.Empty;

            TemplateExpressionPlan cached =
                _templateExpressionPlanCache == null
                    ? null
                    : _templateExpressionPlanCache[expression]
                        as TemplateExpressionPlan;

            if (cached != null)
                return cached;

            TemplateExpressionPlan plan =
                new TemplateExpressionPlan();

            string methodName;
            string argumentText;
            string presetSetName;
            string presetKey;
            PresetConditionExpressionPlan presetConditionPlan;
            bool automaticDataContext;

            if (TryParseFunctionExpression(
                expression,
                out methodName,
                out argumentText,
                out automaticDataContext))
            {
                plan.Kind = TemplateExpressionKind.Function;
                plan.MethodName = methodName;
                plan.ArgumentText = argumentText;
                plan.AutomaticDataContext = automaticDataContext;
            }
            else if (TryParsePresetConditionExpression(
                expression,
                out presetConditionPlan))
            {
                plan.Kind = TemplateExpressionKind.PresetCondition;
                plan.PresetConditionPlan = presetConditionPlan;
            }
            else if (TryParsePresetExpression(
                expression,
                out presetSetName,
                out presetKey))
            {
                plan.Kind = TemplateExpressionKind.Preset;
                plan.PresetSetName = presetSetName;
                plan.PresetKey = presetKey;
            }
            else
            {
                BindingExpressionPlan bindingPlan;

                if (TryParseBindingExpression(
                    expression,
                    out bindingPlan))
                {
                    plan.Kind = TemplateExpressionKind.Binding;
                    plan.BindingPlan = bindingPlan;
                }
                else if (ContainsDynamicExpression(expression))
                {
                    plan.Kind = TemplateExpressionKind.Interpolated;
                }
                else
                {
                    plan.Kind = TemplateExpressionKind.Literal;
                }
            }

            if (expression.Length <= RuntimeMetadataCacheKeyLengthLimit)
            {
                if (_templateExpressionPlanCache == null)
                {
                    _templateExpressionPlanCache =
                        new Hashtable(StringComparer.Ordinal);
                }

                if (_templateExpressionPlanCache.Count <
                    TemplateExpressionPlanCacheLimit)
                {
                    _templateExpressionPlanCache[expression] = plan;
                }
            }

            return plan;
        }

        private object EvaluateTemplateExpressionValue(
            string expression,
            object dataContext)
        {
            dataContext = GetItemDataContext(dataContext);

            if (String.IsNullOrEmpty(expression))
                return expression;

            TemplateExpressionPlan plan =
                GetTemplateExpressionPlan(expression);

            if (plan.Kind == TemplateExpressionKind.Function)
            {
                if (_activeFunctionResultCache != null &&
                    _activeFunctionResultCache.ContainsKey(expression))
                {
                    return _activeFunctionResultCache[expression];
                }

                object functionResult = InvokeBindingFunction(
                    plan.MethodName,
                    plan.ArgumentText,
                    dataContext,
                    plan.AutomaticDataContext);

                if (_activeFunctionResultCache != null)
                    _activeFunctionResultCache[expression] = functionResult;

                return functionResult;
            }

            if (plan.Kind == TemplateExpressionKind.Binding)
            {
                object source = ResolveBindingSource(
                    dataContext,
                    plan.BindingPlan);

                return ResolveBindingExpressionValue(
                    source,
                    plan.BindingPlan);
            }

            if (plan.Kind == TemplateExpressionKind.Preset)
            {
                return ResolvePresetValue(
                    plan.PresetSetName,
                    plan.PresetKey);
            }

            if (plan.Kind == TemplateExpressionKind.PresetCondition)
            {
                return EvaluatePresetConditionExpression(
                    plan.PresetConditionPlan);
            }

            if (plan.Kind == TemplateExpressionKind.Interpolated)
            {
                string resolved = ResolveInterpolatedText(
                    expression,
                    dataContext);

                if (TryTakeUnsetPresetValue(resolved))
                    return UnsetPresetValue;

                return resolved;
            }

            return expression;
        }

        private static bool AttributeCanAffectLayout(string name)
        {
            if (String.IsNullOrEmpty(name))
                return false;

            return
                EqualsIgnoreCase(name, "Text") ||
                EqualsIgnoreCase(name, "Title") ||
                EqualsIgnoreCase(name, "Content") ||
                EqualsIgnoreCase(name, "Header") ||
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
                EqualsIgnoreCase(name, "Visible") ||
                EqualsIgnoreCase(name, "Visibility") ||
                EqualsIgnoreCase(name, "Font") ||
                EqualsIgnoreCase(name, "FontSize") ||
                EqualsIgnoreCase(name, "FontFamily") ||
                EqualsIgnoreCase(name, "FontWeight") ||
                EqualsIgnoreCase(name, "FontStyle") ||
                EqualsIgnoreCase(name, "TextDecorations") ||
                EqualsIgnoreCase(name, "TextWrapping") ||
                EqualsIgnoreCase(name, "AcceptsReturn") ||
                EqualsIgnoreCase(name, "Source") ||
                EqualsIgnoreCase(name, "Orientation") ||
                EqualsIgnoreCase(name, "FlowDirection") ||
                EqualsIgnoreCase(name, "RightToLeft") ||
                EqualsIgnoreCase(name, "LastChildFill") ||
                EqualsIgnoreCase(name, "BorderThickness") ||
                EqualsIgnoreCase(name, "VerticalScrollBarVisibility") ||
                EqualsIgnoreCase(name, "HorizontalScrollBarVisibility") ||
                EqualsIgnoreCase(name, "AutoSize") ||
                EqualsIgnoreCase(name, "Dock") ||
                EqualsIgnoreCase(name, "Anchor") ||
                EqualsIgnoreCase(name, "Location") ||
                EqualsIgnoreCase(name, "Size") ||
                EqualsIgnoreCase(name, "Bounds") ||
                EqualsIgnoreCase(name, "MinimumSize") ||
                EqualsIgnoreCase(name, "MaximumSize") ||
                EqualsIgnoreCase(name, "Spacing") ||
                EqualsIgnoreCase(name, "Gap") ||
                EqualsIgnoreCase(name, "Direction") ||
                EqualsIgnoreCase(name, "Wrap") ||
                EqualsIgnoreCase(name, "JustifyContent") ||
                EqualsIgnoreCase(name, "AlignItems") ||
                EqualsIgnoreCase(name, "FlexGrow");
        }

        private void ApplyDataContextToTree(
            Control control,
            object dataContext)
        {
            UpdateDataContextToTree(
                control,
                dataContext);
        }

        private void UpdateDataContextToTree(
            Control control,
            object dataContext)
        {
            if (control == null)
                return;

            ElementInfo info = GetInfo(control);

            if (!info.TagExplicit)
                control.Tag = dataContext;

            int i;

            for (i = 0;
                 i < control.Controls.Count;
                 i++)
            {
                UpdateDataContextToTree(
                    control.Controls[i],
                    dataContext);
            }
        }

        private void ReleaseElementTree(
            Control control)
        {
            if (control == null)
                return;

            ReleaseElementObjectTree(
                control,
                new Hashtable(_runtimeObjectReferenceComparer));
        }

        private void ReleaseElementObjectTree(
            object value,
            Hashtable visited)
        {
            if (value == null || visited.ContainsKey(value))
                return;

            visited[value] = true;
            Exception firstError = null;

            ElementInfo info;

            if (_elementInfos.TryGetValue(value, out info) &&
                info.LogicalChildren != null)
            {
                int logicalIndex;

                for (logicalIndex = 0;
                     logicalIndex < info.LogicalChildren.Count;
                     logicalIndex++)
                {
                    try
                    {
                        ReleaseElementObjectTree(
                            info.LogicalChildren[logicalIndex],
                            visited);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }
            }

            Control control = value as Control;

            if (control != null)
            {
                int controlIndex;

                for (controlIndex = 0;
                     controlIndex < control.Controls.Count;
                     controlIndex++)
                {
                    try
                    {
                        ReleaseElementObjectTree(
                            control.Controls[controlIndex],
                            visited);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }
            }

            _elementInfos.Remove(value);

            try
            {
                ReleaseDynamicBindings(value);
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            try
            {
                ReleaseComponentInstance(value);
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            try
            {
                ReleaseBoundEvents(value);
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            try
            {
                ReleaseOwnedPropertyValues(value);
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            ItemsControl items = value as ItemsControl;

            if (items != null)
            {
                try
                {
                    UnregisterItemsControl(items);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private void ResolveElementBindings(
            XmlElement element,
            object dataContext)
        {
            if (element == null)
                return;

            ArrayList unsetAttributes = null;
            int i;

            for (i = 0;
                 i < element.Attributes.Count;
                 i++)
            {
                XmlAttribute attribute =
                    element.Attributes[i];

                if (IsConditionalIncludeMetadataAttribute(attribute) ||
                    EqualsIgnoreCase(
                        attribute.LocalName,
                        MarkupXmlDocument.LocationAttributeName) ||
                    EqualsIgnoreCase(
                        attribute.Name,
                        "xmlns") ||
                    EqualsIgnoreCase(
                        attribute.Prefix,
                        "xmlns"))
                {
                    continue;
                }

                try
                {
                    string resolved = ResolveBindingAttributeValue(
                        attribute.Value,
                        dataContext);

                    if (TryTakeUnsetPresetValue(resolved))
                    {
                        if (unsetAttributes == null)
                            unsetAttributes = new ArrayList();

                        unsetAttributes.Add(attribute);
                    }
                    else
                    {
                        attribute.Value = resolved;
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
                        attribute.LocalName,
                        ex);
                }
            }

            for (i = 0;
                 unsetAttributes != null && i < unsetAttributes.Count;
                 i++)
            {
                element.Attributes.Remove(
                    (XmlAttribute)unsetAttributes[i]);
            }

            // Resolve only direct text/CDATA here. Child elements resolve when
            // BuildElement reaches them. This intentionally leaves an
            // ItemsControl.ItemTemplate untouched until an item is rendered.
            XmlNode node =
                element.FirstChild;

            while (node != null)
            {
                if (node.NodeType ==
                        XmlNodeType.Text ||
                    node.NodeType ==
                        XmlNodeType.CDATA)
                {
                    try
                    {
                        string resolved = ResolveBindingTextValue(
                            node.Value,
                            dataContext);
                        node.Value = TryTakeUnsetPresetValue(resolved)
                            ? String.Empty
                            : resolved;
                    }
                    catch (WinFormsXamlLoadException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw CreateMarkupLoadException(
                            element,
                            "Text",
                            ex);
                    }
                }

                node = node.NextSibling;
            }
        }

        private void ReplaceTemplateBindings(
            XmlElement element,
            object dataContext)
        {
            ArrayList unsetAttributes = null;
            int i;

            for (i = 0;
                 i < element.Attributes.Count;
                 i++)
            {
                XmlAttribute attribute =
                    element.Attributes[i];

                if (EqualsIgnoreCase(
                        attribute.LocalName,
                        MarkupXmlDocument.LocationAttributeName) ||
                    EqualsIgnoreCase(
                        attribute.Name,
                        "xmlns") ||
                    EqualsIgnoreCase(
                        attribute.Prefix,
                        "xmlns"))
                {
                    continue;
                }

                string resolved = ResolveBindingAttributeValue(
                    attribute.Value,
                    dataContext);

                if (TryTakeUnsetPresetValue(resolved))
                {
                    if (unsetAttributes == null)
                        unsetAttributes = new ArrayList();

                    unsetAttributes.Add(attribute);
                }
                else
                {
                    attribute.Value = resolved;
                }
            }

            for (i = 0;
                 unsetAttributes != null && i < unsetAttributes.Count;
                 i++)
            {
                element.Attributes.Remove(
                    (XmlAttribute)unsetAttributes[i]);
            }

            XmlNode node =
                element.FirstChild;

            while (node != null)
            {
                XmlElement child =
                    node as XmlElement;

                if (child != null)
                {
                    ReplaceTemplateBindings(
                        child,
                        dataContext);
                }
                else if (node.NodeType ==
                            XmlNodeType.Text ||
                         node.NodeType ==
                            XmlNodeType.CDATA)
                {
                    string resolved = ResolveBindingTextValue(
                        node.Value,
                        dataContext);
                    node.Value = TryTakeUnsetPresetValue(resolved)
                        ? String.Empty
                        : resolved;
                }

                node = node.NextSibling;
            }
        }

        /// <summary>
        /// Resolves a complete attribute expression. If the value is a real
        /// CLR object that cannot safely be represented as XML text, an
        /// internal token is returned and the object is assigned later by
        /// ApplyAttribute. This is what makes Image-returning functions work.
        /// </summary>
        private string ResolveBindingAttributeValue(
            string value,
            object dataContext)
        {
            if (String.IsNullOrEmpty(value))
                return value;

            object functionResult;

            if (TryResolveFunctionExpression(
                value,
                dataContext,
                out functionResult))
            {
                return BindingObjectToAttributeValue(
                    functionResult);
            }

            string presetSetName;
            string presetKey;
            PresetConditionExpressionPlan presetConditionPlan;

            if (TryParsePresetConditionExpression(
                    value,
                    out presetConditionPlan))
            {
                return BindingObjectToAttributeValue(
                    EvaluatePresetConditionExpression(
                        presetConditionPlan));
            }

            if (TryParsePresetExpression(
                value,
                out presetSetName,
                out presetKey))
            {
                return BindingObjectToAttributeValue(
                    ResolvePresetValue(
                        presetSetName,
                        presetKey));
            }

            BindingExpressionPlan bindingPlan;

            if (TryParseBindingExpression(
                value,
                out bindingPlan))
            {
                object source = ResolveBindingSource(
                    dataContext,
                    bindingPlan);

                return BindingObjectToAttributeValue(
                    ResolveBindingExpressionValue(
                        source,
                        bindingPlan));
            }

            return ResolveInterpolatedText(
                value,
                dataContext);
        }

        private string ResolveBindingTextValue(
            string value,
            object dataContext)
        {
            if (String.IsNullOrEmpty(value))
                return value;

            object functionResult;

            if (TryResolveFunctionExpression(
                value,
                dataContext,
                out functionResult))
            {
                return BindingValueToString(
                    functionResult);
            }

            string presetSetName;
            string presetKey;
            PresetConditionExpressionPlan presetConditionPlan;

            if (TryParsePresetConditionExpression(
                    value,
                    out presetConditionPlan))
            {
                return BindingValueToString(
                    EvaluatePresetConditionExpression(
                        presetConditionPlan));
            }

            if (TryParsePresetExpression(
                value,
                out presetSetName,
                out presetKey))
            {
                object presetValue = ResolvePresetValue(
                    presetSetName,
                    presetKey);

                return IsUnsetPresetValue(presetValue)
                    ? BindingObjectToAttributeValue(UnsetPresetValue)
                    : BindingValueToString(presetValue);
            }

            BindingExpressionPlan bindingPlan;

            if (TryParseBindingExpression(
                value,
                out bindingPlan))
            {
                object source = ResolveBindingSource(
                    dataContext,
                    bindingPlan);

                return BindingValueToString(
                    ResolveBindingExpressionValue(
                        source,
                        bindingPlan));
            }

            return ResolveInterpolatedText(
                value,
                dataContext);
        }

        private string ResolveInterpolatedText(
            string value,
            object dataContext)
        {
            string result = value;

            result = ReplaceInterpolatedPrefix(
                result,
                "{Binding",
                dataContext);

            result = ReplaceInterpolatedPrefix(
                result,
                "{Function",
                dataContext);

            result = ReplaceInterpolatedPrefix(
                result,
                "{Preset",
                dataContext);

            return result;
        }

        private string ReplaceInterpolatedPrefix(
            string value,
            string prefix,
            object dataContext)
        {
            string result = value;
            int searchFrom = 0;

            while (searchFrom < result.Length)
            {
                int start =
                    result.IndexOf(
                        prefix,
                        searchFrom,
                        StringComparison.OrdinalIgnoreCase);

                if (start < 0)
                    break;

                int end =
                    result.IndexOf(
                        '}',
                        start);

                if (end < 0)
                    break;

                string expression =
                    result.Substring(
                        start,
                        end - start + 1);

                object replacementObject;
                string replacement;

                if (TryResolveFunctionExpression(
                    expression,
                    dataContext,
                    out replacementObject))
                {
                    replacement =
                        BindingValueToString(
                            replacementObject);
                }
                else
                {
                    TemplateExpressionPlan interpolationPlan =
                        GetTemplateExpressionPlan(expression);

                    if (interpolationPlan.Kind ==
                        TemplateExpressionKind.Preset)
                    {
                        object presetValue = ResolvePresetValue(
                            interpolationPlan.PresetSetName,
                            interpolationPlan.PresetKey);

                        if (IsUnsetPresetValue(presetValue))
                        {
                            return BindingObjectToAttributeValue(
                                UnsetPresetValue);
                        }

                        replacement = BindingValueToString(presetValue);

                        result =
                            result.Substring(0, start) +
                            replacement +
                            result.Substring(end + 1);

                        searchFrom = start + replacement.Length;
                        continue;
                    }

                    if (interpolationPlan.Kind ==
                        TemplateExpressionKind.PresetCondition)
                    {
                        replacement = BindingValueToString(
                            EvaluatePresetConditionExpression(
                                interpolationPlan.PresetConditionPlan));

                        result =
                            result.Substring(0, start) +
                            replacement +
                            result.Substring(end + 1);

                        searchFrom = start + replacement.Length;
                        continue;
                    }

                    if (interpolationPlan.Kind !=
                        TemplateExpressionKind.Binding)
                    {
                        searchFrom = end + 1;
                        continue;
                    }

                    BindingExpressionPlan bindingPlan =
                        interpolationPlan.BindingPlan;

                    if (bindingPlan.Mode == BindingMode.TwoWay)
                    {
                        throw new InvalidOperationException(
                            "Mode=TwoWay requires one complete Binding expression; " +
                            "it cannot be used inside interpolated text.");
                    }

                    object source = ResolveBindingSource(
                        dataContext,
                        bindingPlan);

                    replacement =
                        BindingValueToString(
                            ResolveBindingExpressionValue(
                                source,
                                bindingPlan));
                }

                result =
                    result.Substring(0, start) +
                    replacement +
                    result.Substring(end + 1);

                searchFrom =
                    start + replacement.Length;
            }

            return result;
        }

        private bool TryTakeUnsetPresetValue(string value)
        {
            object boundValue;

            if (!TryPeekBoundObject(value, out boundValue) ||
                !IsUnsetPresetValue(boundValue))
            {
                return false;
            }

            TryTakeBoundObject(value, out boundValue);
            return true;
        }

        private string BindingObjectToAttributeValue(
            object value)
        {
            if (!ShouldStoreBoundObject(value))
            {
                return BindingValueToString(value);
            }

            string token =
                "__WFXAML_BOUND_OBJECT_" +
                _nextBoundObjectId.ToString(
                    CultureInfo.InvariantCulture) +
                "__";

            _nextBoundObjectId++;

            _boundObjectValues[token] =
                value;

            return token;
        }

        private static bool ShouldStoreBoundObject(
            object value)
        {
            // Keep null as a typed bound value too. This lets reference-type
            // targets (especially Image.Source) receive a real null instead of
            // the empty string.
            if (value == null)
                return true;

            Type type = value.GetType();

            if (type == typeof(string) ||
                type == typeof(char) ||
                type == typeof(bool) ||
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(int) ||
                type == typeof(uint) ||
                type == typeof(long) ||
                type == typeof(ulong) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(Guid) ||
                type == typeof(Color) ||
                type == typeof(Uri) ||
                type.IsEnum)
            {
                return false;
            }

            return true;
        }

        private bool TryTakeBoundObject(
            string value,
            out object result)
        {
            result = null;

            if (String.IsNullOrEmpty(value))
                return false;

            if (!_boundObjectValues.TryGetValue(
                value,
                out result))
            {
                return false;
            }

            _boundObjectValues.Remove(value);
            return true;
        }

        // ============================================================
    }
}
