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
        // ============================================================
        // CHILDREN
        // ============================================================

        private void AddChild(
            object parent,
            object child)
        {
            if (IsExecutingCompiledControlBlueprint)
            {
                IncrementCompiledControlBlueprintCounter(
                    ref _compiledControlBlueprintGenericChildDispatchCount);
            }

            if (parent == null)
                throw new ArgumentNullException(
                    "parent");

            if (child == null)
                return;

            if (parent is GridHost ||
                parent is StackHost ||
                parent is FlexPanel ||
                parent is DockHost ||
                parent is CanvasHost ||
                parent is SingleHost)
            {
                Control parentControl =
                    parent as Control;

                Control childControl =
                    child as Control;

                if (childControl == null)
                {
                    throw new InvalidOperationException(
                        parent.GetType().Name +
                        " can only contain Controls.");
                }

                parentControl.Controls.Add(
                    childControl);

                return;
            }

            TabControl tabs =
                parent as TabControl;

            TabPage tabPage =
                child as TabPage;

            if (tabs != null &&
                tabPage != null)
            {
                tabs.TabPages.Add(
                    tabPage);

                return;
            }

            ToolStrip strip =
                parent as ToolStrip;

            ToolStripItem toolItem =
                child as ToolStripItem;

            if (strip != null &&
                toolItem != null)
            {
                strip.Items.Add(
                    toolItem);

                return;
            }

            ToolStripDropDownItem dropDown =
                parent as ToolStripDropDownItem;

            if (dropDown != null &&
                toolItem != null)
            {
                dropDown.DropDownItems.Add(
                    toolItem);

                return;
            }

            TreeView tree =
                parent as TreeView;

            TreeNode treeNode =
                child as TreeNode;

            if (tree != null &&
                treeNode != null)
            {
                tree.Nodes.Add(
                    treeNode);

                return;
            }

            TreeNode parentNode =
                parent as TreeNode;

            if (parentNode != null &&
                treeNode != null)
            {
                parentNode.Nodes.Add(
                    treeNode);

                return;
            }

            ComboBox combo =
                parent as ComboBox;

            if (combo != null)
            {
                combo.Items.Add(
                    child);

                return;
            }

            CheckedListBox checkedList =
                parent as CheckedListBox;

            if (checkedList != null)
            {
                checkedList.Items.Add(
                    child);

                return;
            }

            ListBox list =
                parent as ListBox;

            if (list != null)
            {
                list.Items.Add(
                    child);

                return;
            }

            Control normalParent =
                parent as Control;

            Control normalChild =
                child as Control;

            if (normalParent != null &&
                normalChild != null)
            {
                normalParent.Controls.Add(
                    normalChild);

                ApplyNativeParentLayout(
                    normalParent,
                    normalChild);

                Form form =
                    normalParent as Form;

                MenuStrip menu =
                    normalChild as MenuStrip;

                if (form != null &&
                    menu != null)
                {
                    form.MainMenuStrip =
                        menu;
                }

                return;
            }

            string[] collections =
            {
            "Items",
            "Nodes",
            "TabPages",
            "DropDownItems",
            "Controls",
            "Columns",
            "Rows"
        };

            int i;

            for (i = 0;
                 i < collections.Length;
                 i++)
            {
                if (TryAddToCollectionProperty(
                    parent,
                    collections[i],
                    child))
                {
                    return;
                }
            }

            PropertyInfo[] properties =
                parent.GetType()
                    .GetProperties(
                        BindingFlags.Instance |
                        BindingFlags.Public);

            for (i = 0;
                 i < properties.Length;
                 i++)
            {
                PropertyInfo property =
                    properties[i];

                if (!property.CanRead)
                    continue;

                if (property.GetIndexParameters().Length != 0)
                    continue;

                string propertyName =
                    property.Name;

                bool collectionLike =
                    propertyName.EndsWith("s") ||
                    propertyName.IndexOf("Items") >= 0 ||
                    propertyName.IndexOf("Nodes") >= 0 ||
                    propertyName.IndexOf("Pages") >= 0 ||
                    propertyName.IndexOf("Controls") >= 0;

                if (!collectionLike)
                    continue;

                if (TryAddToCollectionProperty(
                    parent,
                    propertyName,
                    child))
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                "Do not know how to add " +
                child.GetType().FullName +
                " to " +
                parent.GetType().FullName +
                ".");
        }

        private void ApplyNativeParentLayout(
            Control parent,
            Control child)
        {
            if (child is MenuStrip ||
                child is ToolStrip ||
                child is StatusStrip)
            {
                return;
            }

            ElementInfo info =
                GetInfo(
                    child);

            bool contentContainer =
                parent is Form ||
                parent is TabPage ||
                parent is TabViewItem ||
                parent is GroupBox;

            if (!contentContainer)
                return;

            HorizontalXamlAlignment horizontal =
                GetEffectiveHorizontalAlignment(
                    child,
                    info.HorizontalAlignment);

            if (!info.WidthExplicit &&
                !info.HeightExplicit &&
                horizontal ==
                    HorizontalXamlAlignment.Stretch &&
                info.VerticalAlignment ==
                    VerticalXamlAlignment.Stretch &&
                IsZeroPadding(
                    info.Margin))
            {
                child.Dock =
                    DockStyle.Fill;

                return;
            }

            AnchorStyles anchor =
                0;

            if (horizontal ==
                HorizontalXamlAlignment.Stretch)
            {
                anchor |=
                    AnchorStyles.Left |
                    AnchorStyles.Right;
            }
            else if (
                horizontal ==
                HorizontalXamlAlignment.Right)
            {
                anchor |=
                    AnchorStyles.Right;
            }
            else
            {
                anchor |=
                    AnchorStyles.Left;
            }

            if (info.VerticalAlignment ==
                VerticalXamlAlignment.Stretch)
            {
                anchor |=
                    AnchorStyles.Top |
                    AnchorStyles.Bottom;
            }
            else if (
                info.VerticalAlignment ==
                VerticalXamlAlignment.Bottom)
            {
                anchor |=
                    AnchorStyles.Bottom;
            }
            else
            {
                anchor |=
                    AnchorStyles.Top;
            }

            child.Anchor =
                anchor;
        }

        private bool TryAddToCollectionProperty(
            object parent,
            string propertyName,
            object child)
        {
            PropertyInfo property =
                FindProperty(
                    parent.GetType(),
                    propertyName);

            if (property == null ||
                !property.CanRead)
            {
                return false;
            }

            object collection =
                ReadCollectionProperty(
                    parent,
                    property);

            if (collection == null)
                return false;

            return TryCollectionAdd(
                collection,
                child);
        }

        private static object ReadCollectionProperty(
            object parent,
            PropertyInfo property)
        {
            try
            {
                return property.GetValue(
                    parent,
                    null);
            }
            catch (TargetInvocationException ex)
            {
                Exception cause = ex.InnerException == null
                    ? ex
                    : ex.InnerException;

                throw new InvalidOperationException(
                    "Could not read collection property " +
                    parent.GetType().FullName +
                    "." +
                    property.Name +
                    ": " +
                    cause.Message,
                    cause);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not read collection property " +
                    parent.GetType().FullName +
                    "." +
                    property.Name +
                    ": " +
                    ex.Message,
                    ex);
            }
        }

        private bool TryCollectionAdd(
            object collection,
            object child)
        {
            MethodInfo[] methods =
                collection.GetType()
                    .GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Public);

            Type childType =
                child.GetType();

            int i;

            for (i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                if (!EqualsIgnoreCase(
                    method.Name,
                    "Add") ||
                    method.ContainsGenericParameters)
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters.Length != 1)
                    continue;

                Type parameter =
                    parameters[0].ParameterType;

                if (!parameter.IsAssignableFrom(
                        childType) &&
                    parameter != typeof(object))
                {
                    continue;
                }

                try
                {
                    method.Invoke(
                        collection,
                        new object[]
                        {
                        child
                        });

                    return true;
                }
                catch (TargetInvocationException ex)
                {
                    Exception cause = ex.InnerException == null
                        ? ex
                        : ex.InnerException;

                    throw new InvalidOperationException(
                        "Collection " +
                        collection.GetType().FullName +
                        ".Add rejected " +
                        childType.FullName +
                        ": " +
                        cause.Message,
                        cause);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Could not add " +
                        childType.FullName +
                        " to collection " +
                        collection.GetType().FullName +
                        ": " +
                        ex.Message,
                        ex);
                }
            }

            return false;
        }

        // ============================================================
        // PROPERTY ELEMENTS
        // ============================================================

        private void HandlePropertyElement(
            object parent,
            XmlElement propertyElement)
        {
            string propertyName =
                GetPropertyElementName(
                    propertyElement.LocalName);

            if (EqualsIgnoreCase(
                propertyName,
                "Resources"))
            {
                return;
            }

            if (!HasElementChildren(propertyElement) &&
                ContainsDynamicExpression(propertyElement.InnerText))
            {
                string expression = propertyElement.InnerText;
                object dataContext = GetCurrentBuildDataContext();
                bool retainBinding =
                    _templateBuildDepth == 0 ||
                    _componentBuildDepth != 0;
                BindingExpressionPlan initialDirectPlan = null;
                BindingPathResult initialPathResult = null;

                if (retainBinding)
                {
                    initialPathResult =
                        ResolveObservableExpressionDependencies(
                            expression,
                            dataContext,
                            out initialDirectPlan);
                }

                string resolved =
                    ResolveBindingAttributeValue(
                        expression,
                        dataContext);

                ApplyAttribute(
                    parent,
                    propertyName,
                    resolved);

                if (retainBinding)
                {
                    RegisterDynamicBinding(
                        parent,
                        propertyName,
                        expression,
                        dataContext,
                        false,
                        false,
                        true,
                        initialDirectPlan,
                        initialPathResult,
                        CaptureDynamicBindingMarkup(
                            propertyElement,
                            propertyName,
                            null));
                }

                return;
            }

            ItemsControl itemsControl =
                parent as ItemsControl;

            if (itemsControl != null &&
                EqualsIgnoreCase(
                    propertyName,
                    "Template"))
            {
                throw new InvalidOperationException(
                    "Use ItemsControl.ItemTemplate as the canonical item " +
                    "template property element.");
            }

            if (itemsControl != null &&
                EqualsIgnoreCase(
                    propertyName,
                    "ItemTemplate"))
            {
                XmlElement templateRoot =
                    ExtractTemplateRoot(
                        propertyElement);

                itemsControl.SetTemplate(
                    templateRoot,
                    GetComponentEventTarget(
                        GetCurrentBuildDataContext()));

                return;
            }

            GridHost grid =
                parent as GridHost;

            if (grid != null)
            {
                if (EqualsIgnoreCase(
                    propertyName,
                    "RowDefinitions"))
                {
                    ReadRowDefinitions(
                        grid,
                        propertyElement);

                    return;
                }

                if (EqualsIgnoreCase(
                    propertyName,
                    "ColumnDefinitions"))
                {
                    ReadColumnDefinitions(
                        grid,
                        propertyElement);

                    return;
                }
            }

            if (TryHandleConditionalPropertyElement(
                    parent,
                    propertyElement,
                    propertyName))
            {
                return;
            }

            if (!HasElementChildren(
                propertyElement))
            {
                string text =
                    propertyElement.InnerText;

                if (TryApplyWpfProperty(
                    parent,
                    propertyName,
                    text))
                {
                    return;
                }

                PropertyInfo simple =
                    FindProperty(
                        parent.GetType(),
                        propertyName);

                if (simple != null &&
                    simple.CanWrite)
                {
                    SetPropertyValue(
                        parent,
                        simple,
                        text);

                    return;
                }
            }

            PropertyInfo property =
                FindProperty(
                    parent.GetType(),
                    propertyName);

            object collection =
                null;

            if (property != null &&
                property.CanRead)
            {
                collection =
                    ReadCollectionProperty(
                        parent,
                        property);
            }

            XmlNode node =
                propertyElement.FirstChild;

            while (node != null)
            {
                XmlElement childElement =
                    node as XmlElement;

                if (childElement != null)
                {
                    object child =
                        BuildElement(
                            childElement);

                    if (child == null)
                    {
                        node =
                            node.NextSibling;
                        continue;
                    }

                    // Property elements and collection properties can own
                    // non-Control objects that native Control.Controls traversal
                    // cannot see. Establish lifecycle ownership before invoking
                    // user-extensible collection/property code.
                    RegisterLogicalChild(
                        parent,
                        child);

                    bool added =
                        false;

                    try
                    {
                        if (collection != null)
                        {
                            added =
                                TryCollectionAdd(
                                    collection,
                                    child);
                        }

                        if (!added &&
                            property != null &&
                            property.CanWrite &&
                            child != null &&
                            property.PropertyType.IsAssignableFrom(
                                child.GetType()))
                        {
                            property.SetValue(
                                parent,
                                child,
                                null);

                            added =
                                true;
                        }

                        if (!added)
                        {
                            AddChild(
                                parent,
                                child);
                        }

                        ApplyAttachedProperties(
                            parent,
                            child,
                            childElement);
                    }
                    catch
                    {
                        UnregisterLogicalChild(
                            parent,
                            child);

                        try
                        {
                            ReleaseCreatedElement(child);
                        }
                        catch
                        {
                        }

                        throw;
                    }
                }

                node =
                    node.NextSibling;
            }
        }

        private static string GetPropertyElementName(
            string fullName)
        {
            int dot =
                fullName.LastIndexOf('.');

            if (dot >= 0)
            {
                return fullName.Substring(
                    dot + 1);
            }

            return fullName;
        }

        // ============================================================
        // ATTACHED PROPERTIES
        // ============================================================

        private void ApplyAttachedProperties(
            object parent,
            object child,
            XmlElement element)
        {
            ElementInfo info =
                GetInfo(
                    child);

            int i;

            for (i = 0;
                 i < element.Attributes.Count;
                 i++)
            {
                XmlAttribute attribute =
                    element.Attributes[i];

                if (IsConditionalIncludeMetadataAttribute(attribute))
                    continue;

                if (attribute.LocalName.IndexOf('.') < 0)
                    continue;

                try
                {
                    ApplyAttachedProperty(
                        info,
                        child,
                        attribute);
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

            // Every caller applies attached properties inside its containing
            // build or binding-refresh layout transaction. The transaction
            // owns the one final layout pass; forcing the parent here would
            // request an extra pass once for every attached child.
        }

        private void ApplyAttachedProperty(
            ElementInfo info,
            object child,
            XmlAttribute attribute)
        {
            ApplyAttachedProperty(
                info,
                child,
                attribute.LocalName,
                attribute.Value);
        }

        private void ApplyAttachedProperty(
            ElementInfo info,
            object child,
            string name,
            string value)
        {
            int dot =
                name.IndexOf('.');
            string owner =
                name.Substring(
                    0,
                    dot);
            string property =
                name.Substring(
                    dot + 1);
            if (EqualsIgnoreCase(
                owner,
                "Grid"))
            {
                if (EqualsIgnoreCase(
                    property,
                    "Row"))
                {
                    info.GridRow =
                        ParseInt(
                            value);

                    return;
                }

                if (EqualsIgnoreCase(
                    property,
                    "Column"))
                {
                    info.GridColumn =
                        ParseInt(
                            value);

                    return;
                }

                if (EqualsIgnoreCase(
                    property,
                    "RowSpan"))
                {
                    info.GridRowSpan =
                        Math.Max(
                            1,
                            ParseInt(
                                value));

                    return;
                }

                if (EqualsIgnoreCase(
                    property,
                    "ColumnSpan"))
                {
                    info.GridColumnSpan =
                        Math.Max(
                            1,
                            ParseInt(
                                value));

                    return;
                }
            }

            if (EqualsIgnoreCase(
                    owner,
                    "DockPanel") &&
                EqualsIgnoreCase(
                    property,
                    "Dock"))
            {
                info.DockSide =
                    (DockStyle)Enum.Parse(
                        typeof(DockStyle),
                        value,
                        true);

                info.DockExplicit =
                    true;

                return;
            }

            if (EqualsIgnoreCase(
                owner,
                "Canvas"))
            {
                if (EqualsIgnoreCase(
                    property,
                    "Left"))
                {
                    info.CanvasLeft =
                        ParsePixel(
                            value);

                    info.CanvasLeftSet =
                        true;

                    return;
                }

                if (EqualsIgnoreCase(
                    property,
                    "Top"))
                {
                    info.CanvasTop =
                        ParsePixel(
                            value);

                    info.CanvasTopSet =
                        true;

                    return;
                }

                if (EqualsIgnoreCase(
                    property,
                    "Right"))
                {
                    info.CanvasRight =
                        ParsePixel(
                            value);

                    info.CanvasRightSet =
                        true;

                    return;
                }

                if (EqualsIgnoreCase(
                    property,
                    "Bottom"))
                {
                    info.CanvasBottom =
                        ParsePixel(
                            value);

                    info.CanvasBottomSet =
                        true;

                    return;
                }
            }

            if (EqualsIgnoreCase(
                    owner,
                    "Panel") &&
                EqualsIgnoreCase(
                    property,
                    "ZIndex"))
            {
                Control control =
                    child as Control;

                if (control != null &&
                    control.Parent != null)
                {
                    int z =
                        ParseInt(
                            value);

                    int count =
                        control.Parent
                            .Controls.Count;

                    int index =
                        Math.Max(
                            0,
                            Math.Min(
                                count - 1,
                                count - 1 - z));

                    control.Parent.Controls
                        .SetChildIndex(
                            control,
                            index);
                }

                return;
            }

            throw new InvalidOperationException(
                "Unsupported attached property '" +
                name +
                "'.");
        }

    }
}
