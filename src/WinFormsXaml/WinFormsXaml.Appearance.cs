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
        // FONT / COLOR HELPERS
        // ============================================================

        private static Font GetObjectFont(
            object instance)
        {
            PropertyInfo property = GetObjectFontProperty(instance);

            if (property != null &&
                property.CanRead &&
                typeof(Font).IsAssignableFrom(
                    property.PropertyType))
            {
                try
                {
                    return property.GetValue(
                        instance,
                        null) as Font;
                }
                catch
                {
                }
            }

            return null;
        }

        private static PropertyInfo GetObjectFontProperty(object instance)
        {
            if (instance == null)
                return null;

            PropertyInfo property = FindProperty(
                instance.GetType(),
                "Font");

            if (IsFontProperty(property))
                return property;

            if (instance is Control)
            {
                property = typeof(Control).GetProperty(
                    "Font",
                    BindingFlags.Instance | BindingFlags.Public);

                if (IsFontProperty(property))
                    return property;
            }

            return null;
        }

        private static bool IsFontProperty(PropertyInfo property)
        {
            return property != null &&
                property.CanRead &&
                typeof(Font).IsAssignableFrom(property.PropertyType) &&
                property.GetIndexParameters().Length == 0;
        }

        private static Font GetCachedFont(
            string family,
            float size,
            FontStyle style,
            Font fallback)
        {
            string effectiveFamily = family;

            if (String.IsNullOrEmpty(effectiveFamily) && fallback != null)
                effectiveFamily = fallback.FontFamily.Name;

            if (String.IsNullOrEmpty(effectiveFamily))
                return fallback;

            string key =
                effectiveFamily.ToLowerInvariant() + "|" +
                size.ToString("R", CultureInfo.InvariantCulture) + "|" +
                ((int)style).ToString(CultureInfo.InvariantCulture);

            lock (_fontCacheLock)
            {
                WeakReference cachedReference =
                    _fontCache[key] as WeakReference;
                Font cached = cachedReference == null
                    ? null
                    : cachedReference.Target as Font;

                if (cached != null)
                    return cached;

                if (cachedReference != null)
                    _fontCache.Remove(key);
            }

            Font created = null;

            try
            {
                created = new Font(
                    effectiveFamily,
                    size,
                    style,
                    GraphicsUnit.Point);
            }
            catch
            {
                if (fallback != null)
                {
                    try
                    {
                        created = new Font(
                            fallback.FontFamily,
                            size,
                            style,
                            GraphicsUnit.Point);
                    }
                    catch
                    {
                        created = fallback;
                    }
                }
            }

            if (created == null)
                return fallback;

            lock (_fontCacheLock)
            {
                WeakReference existingReference =
                    _fontCache[key] as WeakReference;
                Font existing = existingReference == null
                    ? null
                    : existingReference.Target as Font;

                if (existing != null)
                {
                    if (!Object.ReferenceEquals(created, fallback))
                        created.Dispose();

                    return existing;
                }

                _fontCache[key] = new WeakReference(created);
                PruneFontCacheNoLock();
            }

            return created;
        }

        private static void PruneFontCacheNoLock()
        {
            if (_fontCache.Count <= 256)
                return;

            ArrayList removableKeys = new ArrayList();

            foreach (DictionaryEntry entry in _fontCache)
            {
                WeakReference reference = entry.Value as WeakReference;

                if (reference == null || reference.Target == null)
                    removableKeys.Add(entry.Key);
            }

            int i;

            for (i = 0; i < removableKeys.Count; i++)
                _fontCache.Remove(removableKeys[i]);

            // Live fonts are still owned by controls. Drop only cache references
            // when the key table becomes unusually large; never dispose a font a
            // control may still be using.
            if (_fontCache.Count > 512)
            {
                removableKeys.Clear();

                foreach (DictionaryEntry entry in _fontCache)
                {
                    removableKeys.Add(entry.Key);

                    if (removableKeys.Count >= 256)
                        break;
                }

                for (i = 0; i < removableKeys.Count; i++)
                    _fontCache.Remove(removableKeys[i]);
            }
        }

        private void SetObjectFont(
            object instance,
            string family,
            float size,
            FontStyle style)
        {
            Font existing = GetObjectFont(instance);
            Font font = GetCachedFont(
                family,
                size,
                style,
                existing);

            if (font == null)
                return;

            if (existing != null && existing.Equals(font))
                return;

            PropertyInfo property = GetObjectFontProperty(instance);

            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(
                        instance,
                        font,
                        null);
                }
                catch
                {
                    if (existing != null)
                    {
                        try
                        {
                            property.SetValue(
                                instance,
                                existing,
                                null);
                        }
                        catch
                        {
                        }
                    }

                    object actualValue;

                    if (TryReadPropertyValue(
                        instance,
                        property,
                        out actualValue) &&
                        !Object.ReferenceEquals(actualValue, existing))
                    {
                        ReleaseOwnedPropertyValue(
                            instance,
                            "Font",
                            actualValue);
                    }

                    throw;
                }

                object installedFont;
                bool installedFontKnown = TryReadPropertyValue(
                    instance,
                    property,
                    out installedFont);

                ReleaseOwnedPropertyValue(
                    instance,
                    "Font",
                    installedFontKnown ? installedFont : font);
            }
        }

        private static bool TrySetObjectColorProperty(
            object instance,
            string propertyName,
            Color color)
        {
            PropertyInfo property =
                FindProperty(
                    instance.GetType(),
                    propertyName);

            if (property == null ||
                !property.CanWrite ||
                property.PropertyType !=
                    typeof(Color))
            {
                return false;
            }

            property.SetValue(
                instance,
                color,
                null);

            return true;
        }

        private static void SetBackgroundColor(
            Control control,
            Color color)
        {
            try
            {
                control.BackColor =
                    color;
            }
            catch
            {
                return;
            }

            DisableVisualStyleBackground(control);
        }

        private static void DisableVisualStyleBackground(
            Control control)
        {
            if (control == null)
                return;

            PropertyInfo property =
                FindProperty(
                    control.GetType(),
                    "UseVisualStyleBackColor");

            if (property != null &&
                property.CanWrite &&
                property.PropertyType ==
                    typeof(bool))
            {
                try
                {
                    property.SetValue(
                        control,
                        false,
                        null);
                }
                catch
                {
                }
            }
        }

        // ============================================================
        // RTL / LTR
        // ============================================================

        private static void SetFlowDirection(
            Control control,
            bool rtl)
        {
            ItemsControl items =
                control as ItemsControl;

            if (items != null &&
                (items.KeepScrollBarOnRight ||
                 items.Orientation == Orientation.Horizontal))
            {
                // ScrollableControl has a documented RTL+AutoScroll limitation.
                // Horizontal ItemsControl also owns an explicit logical RTL
                // mapping (P=M-L), so it must never depend on a platform's
                // asynchronous SB_RIGHT initialization. Keep the native host
                // LTR while repeated children remember the requested flow.
                control.RightToLeft = RightToLeft.No;

                SetRightToLeftLayoutIfAvailable(
                    control,
                    false);

                // Assign after native RightToLeft. OnRightToLeftChanged can
                // run synchronously and, when KeepScrollBarOnRight is false,
                // derive a temporary value from the native host direction.
                items.ContentRightToLeft = rtl;

                return;
            }

            if (items != null)
                items.ContentRightToLeft = rtl;

            control.RightToLeft =
                rtl
                    ? RightToLeft.Yes
                    : RightToLeft.No;

            SetRightToLeftLayoutIfAvailable(
                control,
                rtl);
        }

        private static void SetInheritedFlowDirectionIfSupported(
            Control control,
            bool rtl)
        {
            try
            {
                SetFlowDirection(
                    control,
                    rtl);
            }
            catch (NotSupportedException)
            {
                // Some WinForms controls expose Control.RightToLeft but reject
                // every assignment. An ambient value must not prevent those
                // controls from being created. Explicit XAML still calls
                // SetFlowDirection directly and therefore reports the error.
            }
        }

        private static void SetRightToLeftLayoutIfAvailable(
            Control control,
            bool rtl)
        {
            PropertyInfo property =
                FindProperty(
                    control.GetType(),
                    "RightToLeftLayout");

            if (property != null &&
                property.CanWrite &&
                property.PropertyType ==
                    typeof(bool))
            {
                try
                {
                    property.SetValue(
                        control,
                        rtl,
                        null);
                }
                catch
                {
                }
            }
        }

        private static bool IsRightToLeft(
            Control control)
        {
            return
                control != null &&
                control.RightToLeft ==
                    RightToLeft.Yes;
        }

        private static HorizontalXamlAlignment
            GetEffectiveHorizontalAlignment(
                Control control,
                HorizontalXamlAlignment alignment)
        {
            if (!IsRightToLeft(
                control))
            {
                return alignment;
            }

            if (alignment ==
                HorizontalXamlAlignment.Left)
            {
                return
                    HorizontalXamlAlignment.Right;
            }

            if (alignment ==
                HorizontalXamlAlignment.Right)
            {
                return
                    HorizontalXamlAlignment.Left;
            }

            return alignment;
        }

        private static Padding GetEffectiveMargin(
            Control control,
            Padding margin)
        {
            if (!IsRightToLeft(
                control))
            {
                return margin;
            }

            return new Padding(
                margin.Right,
                margin.Top,
                margin.Left,
                margin.Bottom);
        }

        private static DockStyle GetEffectiveDock(
            Control parent,
            DockStyle dock)
        {
            if (!IsRightToLeft(
                parent))
            {
                return dock;
            }

            if (dock ==
                DockStyle.Left)
            {
                return
                    DockStyle.Right;
            }

            if (dock ==
                DockStyle.Right)
            {
                return
                    DockStyle.Left;
            }

            return dock;
        }

        // ============================================================
        // INHERIT PROPERTIES
        // ============================================================

        private void ApplyInheritedProperties(
            Control control,
            Control parent)
        {
            ElementInfo info =
                GetInfo(control);

            if (parent != null)
            {
                ElementInfo parentInfo =
                    GetInfo(parent);

                if (!info.FlowDirectionExplicit)
                {
                    ItemsControl itemsParent =
                        parent as ItemsControl;

                    bool rtl =
                        itemsParent != null
                            ? itemsParent.ContentRightToLeft
                            : parent.RightToLeft ==
                                RightToLeft.Yes;

                    SetInheritedFlowDirectionIfSupported(
                        control,
                        rtl);
                }

                if (!info.ForegroundExplicit &&
                    parentInfo.ForegroundSet)
                {
                    control.ForeColor =
                        parent.ForeColor;

                    info.ForegroundSet =
                        true;
                }
                else if (!info.ForegroundExplicit &&
                         info.ForegroundSet)
                {
                    // The parent stopped supplying this ambient value. A
                    // previous inheritance pass assigned a concrete color, so
                    // remove that copy instead of leaving the old theme pinned.
                    ResetMappedControlForeground(control);
                }

                bool tabViewContentSurface =
                    control is TabViewItem &&
                    parent is TabView;

                if (!tabViewContentSurface &&
                    !info.BackgroundExplicit &&
                    parentInfo.BackgroundSet)
                {
                    SetBackgroundColor(
                        control,
                        parent.BackColor);

                    info.BackgroundSet =
                        true;
                }
                else if (!tabViewContentSurface &&
                         !info.BackgroundExplicit &&
                         info.BackgroundSet)
                {
                    // WinForms ambient colors only keep following their parent
                    // while the child property is reset. Clear the concrete
                    // value copied by the previous inheritance pass.
                    ResetMappedControlBackground(control);
                }

                InheritFont(
                    control,
                    parent,
                    info,
                    parentInfo);
            }

            int i;

            for (i = 0;
                 i < control.Controls.Count;
                 i++)
            {
                ApplyInheritedProperties(
                    control.Controls[i],
                    control);
            }
        }

        private void InheritFont(
            Control child,
            Control parent,
            ElementInfo childInfo,
            ElementInfo parentInfo)
        {
            bool changed =
                false;

            string family =
                child.Font.FontFamily.Name;

            float size =
                child.Font.SizeInPoints;

            FontStyle style =
                child.Font.Style;

            if (!childInfo.FontFamilyExplicit &&
                parentInfo.FontFamilySet)
            {
                family =
                    parent.Font.FontFamily.Name;

                childInfo.FontFamilySet =
                    true;

                changed =
                    true;
            }
            else if (!childInfo.FontFamilyExplicit &&
                     childInfo.FontFamilySet)
            {
                family = parent.Font.FontFamily.Name;
                childInfo.FontFamilySet = false;
                changed = true;
            }

            if (!childInfo.FontSizeExplicit &&
                parentInfo.FontSizeSet)
            {
                size =
                    parent.Font.SizeInPoints;

                childInfo.FontSizeSet =
                    true;

                changed =
                    true;
            }
            else if (!childInfo.FontSizeExplicit &&
                     childInfo.FontSizeSet)
            {
                size = parent.Font.SizeInPoints;
                childInfo.FontSizeSet = false;
                changed = true;
            }

            if (!childInfo.FontWeightExplicit &&
                parentInfo.FontWeightSet)
            {
                style =
                    CopyFontStyleBit(
                        style,
                        parent.Font.Style,
                        FontStyle.Bold);

                childInfo.FontWeightSet =
                    true;

                changed =
                    true;
            }
            else if (!childInfo.FontWeightExplicit &&
                     childInfo.FontWeightSet)
            {
                style = CopyFontStyleBit(
                    style,
                    parent.Font.Style,
                    FontStyle.Bold);
                childInfo.FontWeightSet = false;
                changed = true;
            }

            if (!childInfo.FontStyleExplicit &&
                parentInfo.FontStyleSet)
            {
                style =
                    CopyFontStyleBit(
                        style,
                        parent.Font.Style,
                        FontStyle.Italic);

                childInfo.FontStyleSet =
                    true;

                changed =
                    true;
            }
            else if (!childInfo.FontStyleExplicit &&
                     childInfo.FontStyleSet)
            {
                style = CopyFontStyleBit(
                    style,
                    parent.Font.Style,
                    FontStyle.Italic);
                childInfo.FontStyleSet = false;
                changed = true;
            }

            if (!childInfo.TextDecorationsExplicit &&
                parentInfo.TextDecorationsSet)
            {
                style =
                    CopyFontStyleBit(
                        style,
                        parent.Font.Style,
                        FontStyle.Underline);

                style =
                    CopyFontStyleBit(
                        style,
                        parent.Font.Style,
                        FontStyle.Strikeout);

                childInfo.TextDecorationsSet =
                    true;

                changed =
                    true;
            }
            else if (!childInfo.TextDecorationsExplicit &&
                     childInfo.TextDecorationsSet)
            {
                style = CopyFontStyleBit(
                    style,
                    parent.Font.Style,
                    FontStyle.Underline);
                style = CopyFontStyleBit(
                    style,
                    parent.Font.Style,
                    FontStyle.Strikeout);
                childInfo.TextDecorationsSet = false;
                changed = true;
            }

            if (changed)
            {
                SetObjectFont(
                    child,
                    family,
                    size,
                    style);
            }
        }

        private static FontStyle CopyFontStyleBit(
            FontStyle target,
            FontStyle source,
            FontStyle bit)
        {
            if ((source & bit) == bit)
            {
                target |=
                    bit;
            }
            else
            {
                target &=
                    ~bit;
            }

            return target;
        }

        // ============================================================
        // TEXT ALIGNMENT
        // ============================================================

        private static bool ApplyTextAlignment(
            object instance,
            string value)
        {
            HorizontalAlignment alignment;

            if (EqualsIgnoreCase(
                value,
                "Center"))
            {
                alignment =
                    HorizontalAlignment.Center;
            }
            else if (EqualsIgnoreCase(
                value,
                "Right"))
            {
                alignment =
                    HorizontalAlignment.Right;
            }
            else
            {
                alignment =
                    HorizontalAlignment.Left;
            }

            TextBox textBox =
                instance as TextBox;

            if (textBox != null)
            {
                textBox.TextAlign =
                    alignment;

                return true;
            }

            Label label =
                instance as Label;

            if (label != null)
            {
                if (alignment ==
                    HorizontalAlignment.Center)
                {
                    label.TextAlign =
                        ContentAlignment.MiddleCenter;
                }
                else if (alignment ==
                    HorizontalAlignment.Right)
                {
                    label.TextAlign =
                        ContentAlignment.MiddleRight;
                }
                else
                {
                    label.TextAlign =
                        ContentAlignment.MiddleLeft;
                }

                return true;
            }

            ButtonBase button =
                instance as ButtonBase;

            if (button != null)
            {
                if (alignment ==
                    HorizontalAlignment.Center)
                {
                    button.TextAlign =
                        ContentAlignment.MiddleCenter;
                }
                else if (alignment ==
                    HorizontalAlignment.Right)
                {
                    button.TextAlign =
                        ContentAlignment.MiddleRight;
                }
                else
                {
                    button.TextAlign =
                        ContentAlignment.MiddleLeft;
                }

                return true;
            }

            return false;
        }
    }
}
