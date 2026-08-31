using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private LightweightTemplatePlan CompileLightweightTemplate(
            ItemsControl host)
        {
            ItemTemplateActiveContext previousContext =
                PushItemTemplateDeclarationContext(host);

            try
            {
                LightweightTemplatePlan plan =
                    new LightweightTemplatePlan();
                plan.TemplateXml = host.TemplateOuterXml;
                plan.Root = CompileLightweightNode(
                    host,
                    plan,
                    host.TemplateRoot,
                    true,
                    0);
                return plan;
            }
            finally
            {
                RestoreItemTemplateDeclarationContext(previousContext);
            }
        }

        private LightweightTemplateNode CompileLightweightNode(
            ItemsControl host,
            LightweightTemplatePlan plan,
            XmlElement element,
            bool root,
            int depth)
        {
            if (element == null)
            {
                throw new InvalidOperationException(
                    "Lightweight ItemsControl requires one ItemTemplate root.");
            }

            if (depth > 3)
            {
                throw LightweightMarkupError(
                    element,
                    null,
                    "Lightweight templates support at most a Border, one " +
                    "StackPanel, and paintable leaf elements.");
            }

            LightweightTemplateNode node =
                new LightweightTemplateNode();
            node.Id = plan.NextNodeId++;
            node.SourceElement = element;
            node.Orientation = Orientation.Vertical;

            if (EqualsIgnoreCase(element.LocalName, "Border"))
                node.Kind = LightweightNodeKind.Border;
            else if (EqualsIgnoreCase(element.LocalName, "StackPanel"))
                node.Kind = LightweightNodeKind.StackPanel;
            else if (EqualsIgnoreCase(element.LocalName, "Label"))
                node.Kind = LightweightNodeKind.Label;
            else if (EqualsIgnoreCase(element.LocalName, "CheckBox"))
                node.Kind = LightweightNodeKind.CheckBox;
            else if (EqualsIgnoreCase(element.LocalName, "HyperlinkLabel"))
            {
                node.Kind = LightweightNodeKind.HyperlinkLabel;
                node.LinkId = plan.NextLinkId++;
            }
            else if (EqualsIgnoreCase(element.LocalName, "Image"))
                node.Kind = LightweightNodeKind.Image;
            else
            {
                throw LightweightMarkupError(
                    element,
                    null,
                    "Element '" + element.LocalName + "' is not supported by " +
                    "VirtualizationMode=Lightweight. Supported visual elements " +
                    "are Border, StackPanel, Label, CheckBox, HyperlinkLabel, " +
                    "and Image.");
            }

            CompileLightweightAttributes(host, plan, node, element, root);
            CompileLightweightChildren(host, plan, node, element, depth);
            ValidateCompiledLightweightNode(node, root);
            return node;
        }

        private void CompileLightweightAttributes(
            ItemsControl host,
            LightweightTemplatePlan plan,
            LightweightTemplateNode node,
            XmlElement element,
            bool root)
        {
            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];
                string name = attribute.LocalName;
                string value = attribute.Value;

                if (attribute.Prefix == "xmlns" ||
                    attribute.Name == "xmlns" ||
                    attribute.NamespaceURI ==
                        "http://www.w3.org/2001/XMLSchema-instance" ||
                    name.StartsWith("__Wfx", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    if (EqualsIgnoreCase(name, "Margin"))
                    {
                        RequireStaticLightweightValue(element, name, value);
                        node.Margin = ParseThickness(value);
                    }
                    else if (EqualsIgnoreCase(name, "Padding"))
                    {
                        RequireStaticLightweightValue(element, name, value);
                        node.Padding = ParseThickness(value);
                    }
                    else if (EqualsIgnoreCase(name, "Width"))
                    {
                        if (root && !IsLightweightPaintLeaf(node.Kind))
                        {
                            throw new InvalidOperationException(
                                "The lightweight row root always fills the " +
                                "ItemsControl viewport; Width is not supported there.");
                        }

                        RequireStaticLightweightValue(element, name, value);
                        node.Width = Math.Max(0, ParsePixel(value));
                    }
                    else if (EqualsIgnoreCase(name, "Height"))
                    {
                        if (root && !IsLightweightPaintLeaf(node.Kind))
                        {
                            throw new InvalidOperationException(
                                "Use ItemsControl.FixedItemSize for lightweight " +
                                "row height instead of a root Height.");
                        }

                        RequireStaticLightweightValue(element, name, value);
                        node.Height = Math.Max(0, ParsePixel(value));
                    }
                    else if (EqualsIgnoreCase(name, "Background") ||
                             EqualsIgnoreCase(name, "BackColor"))
                    {
                        node.BackColor = CompileLightweightColor(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "Foreground") ||
                             EqualsIgnoreCase(name, "ForeColor"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        node.ForeColor = CompileLightweightColor(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "Text") ||
                             EqualsIgnoreCase(name, "Content"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        node.Text = CompileLightweightText(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "BorderBrush"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.Border,
                            name);
                        node.BorderColor = CompileLightweightColor(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "BorderThickness"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.Border,
                            name);
                        RequireStaticLightweightValue(element, name, value);
                        node.BorderThickness = ParseThickness(value);
                    }
                    else if (EqualsIgnoreCase(name, "Orientation"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.StackPanel,
                            name);
                        RequireStaticLightweightValue(element, name, value);
                        node.Orientation =
                            (Orientation)Enum.Parse(
                                typeof(Orientation),
                                value,
                                true);
                    }
                    else if (EqualsIgnoreCase(name, "Spacing") ||
                             EqualsIgnoreCase(name, "Gap"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.StackPanel,
                            name);
                        RequireStaticLightweightValue(element, name, value);
                        node.Spacing = Math.Max(0, ParsePixel(value));
                    }
                    else if (EqualsIgnoreCase(name, "Checked") ||
                             EqualsIgnoreCase(name, "IsChecked"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.CheckBox,
                            name);
                        node.Checked = CompileLightweightBoolean(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "Enabled") ||
                             EqualsIgnoreCase(name, "IsEnabled"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        node.Enabled = CompileLightweightBoolean(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "NavigateUri"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.HyperlinkLabel,
                            name);
                        node.NavigateUri = CompileLightweightText(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "LinkColor"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.HyperlinkLabel,
                            name);
                        node.LinkColor = CompileLightweightColor(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "VisitedLinkColor"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.HyperlinkLabel,
                            name);
                        node.VisitedLinkColor = CompileLightweightColor(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "Source"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.Image,
                            name);
                        node.Source = CompileLightweightImageSource(
                            plan,
                            element,
                            name,
                            value);
                    }
                    else if (EqualsIgnoreCase(name, "Stretch"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.Image,
                            name);
                        RequireStaticLightweightValue(element, name, value);
                        node.Stretch = (ImageStretch)Enum.Parse(
                            typeof(ImageStretch),
                            value,
                            true);
                    }
                    else if (EqualsIgnoreCase(name, "FontFamily"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        RequireStaticLightweightValue(element, name, value);
                        node.FontFamily = value.Trim();
                    }
                    else if (EqualsIgnoreCase(name, "FontSize"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        RequireStaticLightweightValue(element, name, value);
                        node.FontSizeInPoints =
                            Math.Max(1.0f, ParseFloat(value) * 0.75f);
                    }
                    else if (EqualsIgnoreCase(name, "FontWeight"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        RequireStaticLightweightValue(element, name, value);
                        node.FontStyleSpecified = true;

                        if (IsBoldFontWeight(value))
                            node.FontStyle |= FontStyle.Bold;
                    }
                    else if (EqualsIgnoreCase(name, "FontStyle"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        RequireStaticLightweightValue(element, name, value);
                        node.FontStyleSpecified = true;

                        if (EqualsIgnoreCase(value, "Italic") ||
                            EqualsIgnoreCase(value, "Oblique"))
                        {
                            node.FontStyle |= FontStyle.Italic;
                        }
                    }
                    else if (EqualsIgnoreCase(name, "TextDecorations"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        RequireStaticLightweightValue(element, name, value);
                        node.FontStyleSpecified = true;

                        if (value.IndexOf(
                                "Underline",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            node.FontStyle |= FontStyle.Underline;
                        }

                        if (value.IndexOf(
                                "Strike",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            node.FontStyle |= FontStyle.Strikeout;
                        }
                    }
                    else if (EqualsIgnoreCase(name, "TextAlign") ||
                             EqualsIgnoreCase(name, "TextAlignment"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        RequireStaticLightweightValue(element, name, value);
                        node.TextAlign = ParseLightweightAlignment(value);
                    }
                    else if (EqualsIgnoreCase(name, "CheckAlign"))
                    {
                        RequireLightweightKind(
                            element,
                            node,
                            LightweightNodeKind.CheckBox,
                            name);
                        RequireStaticLightweightValue(element, name, value);
                        node.CheckAlign = ParseLightweightAlignment(value);
                    }
                    else if (EqualsIgnoreCase(name, "AutoEllipsis"))
                    {
                        RequireLightweightTextLeaf(element, node, name);
                        RequireStaticLightweightValue(element, name, value);
                        node.AutoEllipsis = ParseBoolean(value);
                    }
                    else if (EqualsIgnoreCase(name, "Visibility"))
                    {
                        RequireStaticLightweightValue(element, name, value);

                        if (!EqualsIgnoreCase(value, "Visible") &&
                            !EqualsIgnoreCase(value, "true"))
                        {
                            throw new InvalidOperationException(
                                "Lightweight rows have one fixed logical slot per " +
                                "item and support only Visibility=Visible.");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Property/event '" + name + "' is not supported on " +
                            element.LocalName + " in VirtualizationMode=Lightweight.");
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
                        name,
                        ex);
                }
            }
        }

        private void CompileLightweightChildren(
            ItemsControl host,
            LightweightTemplatePlan plan,
            LightweightTemplateNode node,
            XmlElement element,
            int depth)
        {
            XmlNode childNode = element.FirstChild;
            string text = null;

            while (childNode != null)
            {
                XmlElement childElement = childNode as XmlElement;

                if (childElement != null)
                {
                    if (IsPropertyElement(childElement) ||
                        IsPresetDefinitionElement(childElement))
                    {
                        throw LightweightMarkupError(
                            childElement,
                            null,
                            "Property elements, Resources, Style, and inline " +
                            "Preset declarations are not supported inside a " +
                            "lightweight item template.");
                    }

                    node.Children.Add(
                        CompileLightweightNode(
                            host,
                            plan,
                            childElement,
                            false,
                            depth + 1));
                }
                else if ((childNode.NodeType == XmlNodeType.Text ||
                          childNode.NodeType == XmlNodeType.CDATA) &&
                         childNode.Value != null &&
                         childNode.Value.Trim().Length != 0)
                {
                    text = text == null
                        ? childNode.Value.Trim()
                        : text + childNode.Value.Trim();
                }

                childNode = childNode.NextSibling;
            }

            if (text != null)
            {
                if (!IsLightweightTextLeaf(node.Kind))
                {
                    throw LightweightMarkupError(
                        element,
                        null,
                        "Only Label, CheckBox, and HyperlinkLabel can contain text.");
                }

                if (node.Text != null)
                {
                    throw LightweightMarkupError(
                        element,
                        "Text",
                        "Specify lightweight text either as content or as Text, not both.");
                }

                node.Text = CompileLightweightText(
                    plan,
                    element,
                    "Text",
                    text);
            }
        }

        private void ValidateCompiledLightweightNode(
            LightweightTemplateNode node,
            bool root)
        {
            ValidateLightweightReadOnlySlot(node.Text);
            ValidateLightweightReadOnlySlot(node.ForeColor);
            ValidateLightweightReadOnlySlot(node.BackColor);
            ValidateLightweightReadOnlySlot(node.BorderColor);
            ValidateLightweightReadOnlySlot(node.Enabled);
            ValidateLightweightReadOnlySlot(node.NavigateUri);
            ValidateLightweightReadOnlySlot(node.LinkColor);
            ValidateLightweightReadOnlySlot(node.VisitedLinkColor);
            ValidateLightweightReadOnlySlot(node.Source);

            if (node.Kind == LightweightNodeKind.Border &&
                node.Children.Count > 1)
            {
                throw LightweightMarkupError(
                    node.SourceElement,
                    null,
                    "A lightweight Border accepts at most one child.");
            }

            if (node.Kind == LightweightNodeKind.Border &&
                node.Children.Count == 1 &&
                ((LightweightTemplateNode)node.Children[0]).Kind ==
                    LightweightNodeKind.Border)
            {
                throw LightweightMarkupError(
                    node.SourceElement,
                    null,
                    "Nested lightweight Borders are not supported. Use one " +
                    "row Border around a leaf or StackPanel.");
            }

            if (node.Kind == LightweightNodeKind.StackPanel)
            {
                int i;

                for (i = 0; i < node.Children.Count; i++)
                {
                    LightweightTemplateNode child =
                        node.Children[i] as LightweightTemplateNode;

                    if (child == null ||
                        !IsLightweightPaintLeaf(child.Kind))
                    {
                        throw LightweightMarkupError(
                            node.SourceElement,
                            null,
                            "A lightweight StackPanel accepts only Label, " +
                            "CheckBox, HyperlinkLabel, and Image children.");
                    }
                }
            }

            if (IsLightweightPaintLeaf(node.Kind) &&
                node.Children.Count != 0)
            {
                throw LightweightMarkupError(
                    node.SourceElement,
                    null,
                    node.SourceElement.LocalName +
                    " cannot contain visual children in lightweight mode.");
            }

            if (node.Kind == LightweightNodeKind.CheckBox)
            {
                bool disabledLiteral =
                    node.Enabled != null &&
                    !node.Enabled.Dynamic &&
                    node.Enabled.Literal is bool &&
                    !(bool)node.Enabled.Literal;
                BindingExpressionPlan checkedPlan =
                    GetLightweightBindingPlan(node.Checked);
                bool twoWay =
                    checkedPlan != null &&
                    checkedPlan.Mode == BindingMode.TwoWay;

                if (twoWay && checkedPlan.HasComputedExpression)
                {
                    throw LightweightMarkupError(
                        node.SourceElement,
                        node.Checked.PropertyName,
                        "A TwoWay lightweight CheckBox binding cannot use a " +
                        "computed expression.");
                }

                if (twoWay && checkedPlan.HasNegation)
                {
                    throw LightweightMarkupError(
                        node.SourceElement,
                        node.Checked.PropertyName,
                        "A TwoWay lightweight CheckBox binding cannot negate " +
                        "its source path.");
                }

                if (twoWay &&
                    checkedPlan.UpdateSourceTrigger !=
                        BindingUpdateSourceTrigger.PropertyChanged)
                {
                    throw LightweightMarkupError(
                        node.SourceElement,
                        node.Checked.PropertyName,
                        "An owner-drawn lightweight CheckBox requires " +
                        "UpdateSourceTrigger=PropertyChanged because it has " +
                        "no child Control that can receive focus.");
                }

                if (!disabledLiteral && !twoWay)
                {
                    throw LightweightMarkupError(
                        node.SourceElement,
                        node.Checked == null
                            ? "Checked"
                            : node.Checked.PropertyName,
                        "An enabled lightweight CheckBox requires a complete " +
                        "Checked/IsChecked Binding with Mode=TwoWay so clicks " +
                        "have durable item state. Use Enabled=false for a " +
                        "read-only checkbox.");
                }
            }

            if (node.Kind == LightweightNodeKind.HyperlinkLabel &&
                node.NavigateUri == null)
            {
                throw LightweightMarkupError(
                    node.SourceElement,
                    "NavigateUri",
                    "A lightweight HyperlinkLabel requires NavigateUri.");
            }

            if (node.Kind == LightweightNodeKind.Image &&
                node.Source == null)
            {
                throw LightweightMarkupError(
                    node.SourceElement,
                    "Source",
                    "A lightweight Image requires Source.");
            }

            if (root && node.Kind == LightweightNodeKind.StackPanel)
            {
                // This is valid, but its fixed row slot is supplied by the host.
                return;
            }
        }

        private void ValidateLightweightReadOnlySlot(
            LightweightValueSlot slot)
        {
            BindingExpressionPlan plan =
                GetLightweightBindingPlan(slot);

            if (plan == null || plan.Mode != BindingMode.TwoWay)
            {
                return;
            }

            throw LightweightMarkupError(
                slot.Element,
                slot.PropertyName,
                "Mode=TwoWay is supported only by a lightweight " +
                "CheckBox Checked/IsChecked binding.");
        }

        private static bool IsLightweightPaintLeaf(LightweightNodeKind kind)
        {
            return kind == LightweightNodeKind.Label ||
                   kind == LightweightNodeKind.CheckBox ||
                   kind == LightweightNodeKind.HyperlinkLabel ||
                   kind == LightweightNodeKind.Image;
        }

        private static bool IsLightweightTextLeaf(LightweightNodeKind kind)
        {
            return kind == LightweightNodeKind.Label ||
                   kind == LightweightNodeKind.CheckBox ||
                   kind == LightweightNodeKind.HyperlinkLabel;
        }

        private static void RequireLightweightTextLeaf(
            XmlElement element,
            LightweightTemplateNode node,
            string propertyName)
        {
            if (!IsLightweightTextLeaf(node.Kind))
            {
                throw new InvalidOperationException(
                    propertyName + " is supported only on lightweight " +
                    "Label, CheckBox, and HyperlinkLabel elements.");
            }
        }

        private static void RequireLightweightKind(
            XmlElement element,
            LightweightTemplateNode node,
            LightweightNodeKind expected,
            string propertyName)
        {
            if (node.Kind != expected)
            {
                throw new InvalidOperationException(
                    propertyName + " is not supported on " +
                    element.LocalName + " in lightweight mode.");
            }
        }

        private static void RequireStaticLightweightValue(
            XmlElement element,
            string propertyName,
            string value)
        {
            if (ContainsDynamicExpression(value))
            {
                throw new InvalidOperationException(
                    propertyName + " affects lightweight layout/metadata and " +
                    "must be a static value.");
            }
        }

        private LightweightValueSlot CompileLightweightText(
            LightweightTemplatePlan plan,
            XmlElement element,
            string propertyName,
            string value)
        {
            LightweightValueSlot slot = new LightweightValueSlot();
            slot.Id = plan.NextValueSlotId++;
            slot.Element = element;
            slot.PropertyName = propertyName;
            slot.Expression = value;
            slot.Dynamic = ContainsDynamicExpression(value);
            slot.Literal = slot.Dynamic ? null : (object)value;

            BindingExpressionPlan bindingPlan;

            if (TryParseBindingExpression(value, out bindingPlan))
                slot.BindingPlan = bindingPlan;

            return slot;
        }

        private LightweightValueSlot CompileLightweightBoolean(
            LightweightTemplatePlan plan,
            XmlElement element,
            string propertyName,
            string value)
        {
            LightweightValueSlot slot = CompileLightweightText(
                plan,
                element,
                propertyName,
                value);

            if (!slot.Dynamic)
                slot.Literal = ParseBoolean(value);

            return slot;
        }

        private LightweightValueSlot CompileLightweightColor(
            LightweightTemplatePlan plan,
            XmlElement element,
            string propertyName,
            string value)
        {
            LightweightValueSlot slot = CompileLightweightText(
                plan,
                element,
                propertyName,
                value);

            if (!slot.Dynamic)
                slot.Literal = ParseColor(value);

            return slot;
        }

        private LightweightValueSlot CompileLightweightImageSource(
            LightweightTemplatePlan plan,
            XmlElement element,
            string propertyName,
            string value)
        {
            LightweightValueSlot slot = CompileLightweightText(
                plan,
                element,
                propertyName,
                value);

            BindingExpressionPlan bindingPlan;
            string functionName;
            string functionArguments;
            string presetSet;
            string presetKey;
            bool automaticDataContext;
            bool completeExpression =
                TryParseBindingExpression(value, out bindingPlan) ||
                TryParseFunctionExpression(
                    value,
                    out functionName,
                    out functionArguments,
                    out automaticDataContext) ||
                TryParsePresetExpression(
                    value,
                    out presetSet,
                    out presetKey);

            if (!slot.Dynamic || !completeExpression)
            {
                throw LightweightMarkupError(
                    element,
                    propertyName,
                    "A lightweight Image.Source must be one complete Binding, " +
                    "Function, or Preset expression that returns Image, Icon, " +
                    "or encoded byte[]. " +
                    "URI/file loading requires the Controls backend.");
            }

            return slot;
        }

        private WinFormsXamlLoadException LightweightMarkupError(
            XmlElement element,
            string propertyName,
            string message)
        {
            return CreateMarkupLoadException(
                element,
                propertyName,
                new InvalidOperationException(message));
        }

        private static bool IsBoldFontWeight(string value)
        {
            return EqualsIgnoreCase(value, "Bold") ||
                   EqualsIgnoreCase(value, "SemiBold") ||
                   EqualsIgnoreCase(value, "DemiBold") ||
                   EqualsIgnoreCase(value, "ExtraBold") ||
                   EqualsIgnoreCase(value, "Black");
        }

        private static ContentAlignment ParseLightweightAlignment(
            string value)
        {
            if (EqualsIgnoreCase(value, "Left"))
                return ContentAlignment.MiddleLeft;
            if (EqualsIgnoreCase(value, "Center"))
                return ContentAlignment.MiddleCenter;
            if (EqualsIgnoreCase(value, "Right"))
                return ContentAlignment.MiddleRight;

            return (ContentAlignment)Enum.Parse(
                typeof(ContentAlignment),
                value,
                true);
        }

    }
}
