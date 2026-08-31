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
        // GRID DEFINITIONS
        // ============================================================

        private enum GridUnit
        {
            Auto,
            Pixel,
            Star
        }

        private sealed class GridDefinition
        {
            public GridUnit Unit;
            public float Value;

            public GridDefinition()
            {
                Unit =
                    GridUnit.Star;

                Value =
                    1.0f;
            }
        }

        // ============================================================
        // CONFIGURE CREATED CONTROLS
        // ============================================================

        private void ConfigureCreatedObject(
            object instance)
        {
            GridHost grid =
                instance as GridHost;

            if (grid != null)
                grid.Runtime = this;

            StackHost stack =
                instance as StackHost;

            if (stack != null)
                stack.Runtime = this;

            ItemsControl itemsControl =
                instance as ItemsControl;

            if (itemsControl != null)
            {
                itemsControl.Runtime = this;
                RegisterItemsControl(itemsControl);
            }

            TabView tabView =
                instance as TabView;

            if (tabView != null)
                tabView.BeginXamlInitialization();

            FlexPanel flexPanel =
                instance as FlexPanel;

            if (flexPanel != null)
                flexPanel.Runtime = this;

            DockHost dock =
                instance as DockHost;

            if (dock != null)
                dock.Runtime = this;

            CanvasHost canvas =
                instance as CanvasHost;

            if (canvas != null)
                canvas.Runtime = this;

            SingleHost single =
                instance as SingleHost;

            if (single != null)
                single.Runtime = this;

            Form form = instance as Form;

            if (form != null)
            {
                form.AutoScaleMode =
                    AutoScaleMode.None;

                ElementInfo info = GetInfo(form);
                FormIconState state = new FormIconState();
                state.UseApplicationIcon = true;
                state.NativeBaseline = form.Icon;
                info.FormIcon = state;
            }

        }

        private void CompleteApplicationIconConfiguration(Form form)
        {
            if (form == null)
                return;

            ElementInfo info = GetInfo(form);
            FormIconState state = info.FormIcon;

            if (state == null)
                return;

            state.ConfigurationReady = true;
            ReconcileApplicationIconDefault(form);
        }

        private void ReconcileApplicationIconDefault(Form form)
        {
            if (form == null)
                return;

            ElementInfo info = GetInfo(form);
            FormIconState state = info.FormIcon;

            if (state == null ||
                !state.ConfigurationReady ||
                HasLocalValue(form, "Icon") ||
                HasActiveStyleValue(form, "Icon"))
            {
                return;
            }

            Icon current = form.Icon;

            if (state.FallbackApplied)
            {
                if (!Object.ReferenceEquals(
                    current,
                    state.FallbackValue))
                {
                    // An imperative assignment displaced the fallback without
                    // going through the runtime's property setter. Retire only
                    // the old owned clone; the caller's Icon remains untouched.
                    ReleaseOwnedPropertyValue(form, "Icon", current);
                    state.FallbackApplied = false;
                    state.FallbackValue = null;
                    return;
                }

                if (state.UseApplicationIcon)
                    return;

                form.Icon = state.NativeBaseline;
                Icon restored = form.Icon;
                ReleaseOwnedPropertyValue(form, "Icon", restored);

                if (!Object.ReferenceEquals(
                    restored,
                    state.FallbackValue))
                {
                    state.FallbackApplied = false;
                    state.FallbackValue = null;
                }

                return;
            }

            if (!state.UseApplicationIcon ||
                !Object.ReferenceEquals(
                    current,
                    state.NativeBaseline))
            {
                return;
            }

            Icon applicationIcon =
                ApplicationIconProvider.GetApplicationIcon();

            if (applicationIcon == null)
                return;

            try
            {
                form.Icon = applicationIcon;
            }
            catch
            {
                applicationIcon.Dispose();
                throw;
            }

            Icon installed = form.Icon;
            ReconcileOwnedPropertyAssignment(
                form,
                "Icon",
                applicationIcon,
                installed,
                true);

            if (Object.ReferenceEquals(installed, applicationIcon))
            {
                state.FallbackApplied = true;
                state.FallbackValue = applicationIcon;
            }
        }

        private bool HasActiveStyleValue(
            object instance,
            string propertyName)
        {
            if (instance == null || String.IsNullOrEmpty(propertyName))
                return false;

            ElementInfo info = GetInfo(instance);

            if (info.StyleValueSlots == null)
                return false;

            string key = GetStylePropertyKey(instance, propertyName);
            StyleValueSlot slot =
                info.StyleValueSlots[key] as StyleValueSlot;

            return slot != null && slot.Active;
        }

        // ============================================================
        // ATTRIBUTES
        // ============================================================

        private void ApplyAttributes(
            object instance,
            XmlElement element,
            Hashtable constructorAttributes)
        {
            int i;

            for (i = 0;
                 i < element.Attributes.Count;
                 i++)
            {
                XmlAttribute attribute =
                    element.Attributes[i];

                if (ShouldIgnoreAttribute(
                    attribute))
                {
                    continue;
                }

                string name =
                    attribute.LocalName;

                if (EqualsIgnoreCase(
                    name,
                    "Name"))
                {
                    continue;
                }

                if (EqualsIgnoreCase(name, "Type") &&
                    (EqualsIgnoreCase(element.LocalName, "Object") ||
                     EqualsIgnoreCase(element.LocalName, "Control")))
                {
                    // Object/Control Type selects the instance type during
                    // construction; it is metadata, not a CLR property to set
                    // again on the created instance.
                    continue;
                }

                if (IsResourceStyleProperty(instance, name))
                {
                    continue;
                }

                if (EqualsIgnoreCase(
                    name,
                    "Condition"))
                {
                    continue;
                }

                if (EqualsIgnoreCase(
                    name,
                    "Tag"))
                {
                    GetInfo(instance).TagExplicit = true;
                }

                if (constructorAttributes != null &&
                    constructorAttributes.ContainsKey(name))
                {
                    continue;
                }

                if (name.IndexOf('.') >= 0)
                    continue;

                try
                {
                    ApplyAttribute(
                        instance,
                        name,
                        attribute.Value);
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

        private bool ShouldIgnoreAttribute(
            XmlAttribute attribute)
        {
            if (String.Equals(
                    attribute.NamespaceURI,
                    "http://www.w3.org/2001/XMLSchema-instance",
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                    attribute.LocalName,
                    "__WfxPath"))
            {
                return true;
            }

            if (IsConditionalIncludeMetadataAttribute(attribute))
                return true;

            if (EqualsIgnoreCase(
                    attribute.LocalName,
                    MarkupXmlDocument.LocationAttributeName))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                    attribute.LocalName,
                    "Class"))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                attribute.Name,
                "xmlns"))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                attribute.Prefix,
                "xmlns"))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                attribute.Prefix,
                "d"))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                attribute.Prefix,
                "mc"))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                attribute.Prefix,
                "xml"))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                attribute.Prefix,
                "x"))
            {
                if (EqualsIgnoreCase(
                    attribute.LocalName,
                    "Name"))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private void ApplyAttribute(
            object instance,
            string name,
            string value)
        {
            ApplyAttribute(
                instance,
                name,
                value,
                false);
        }

        private void ApplyStyleSetterAttribute(
            object instance,
            string name,
            string value)
        {
            ApplyAttribute(
                instance,
                name,
                value,
                true);
        }

        private void ApplyAttribute(
            object instance,
            string name,
            string value,
            bool styleSetter)
        {
            if (IsExecutingCompiledControlBlueprint)
            {
                IncrementCompiledControlBlueprintCounter(
                    ref _compiledControlBlueprintGenericAttributeDispatchCount);
            }

            object boundObject;

            if (TryTakeBoundObject(
                value,
                out boundObject))
            {
                if (ApplyBoundObjectAttribute(
                    instance,
                    name,
                    boundObject,
                    styleSetter))
                {
                    return;
                }
            }

            if (TryApplyWpfProperty(
                instance,
                name,
                value))
            {
                return;
            }

            PropertyInfo property =
                FindProperty(
                    instance.GetType(),
                    name);

            if (property != null)
            {
                if (!property.CanWrite)
                {
                    throw new InvalidOperationException(
                        instance.GetType().Name +
                        "." +
                        property.Name +
                        " is read-only.");
                }

                SetPropertyValue(
                    instance,
                    property,
                    value);

                return;
            }

            EventInfo eventInfo =
                FindEvent(
                    instance.GetType(),
                    name);

            if (eventInfo != null)
            {
                BindEvent(
                    instance,
                    eventInfo,
                    value,
                    styleSetter);

                return;
            }

            throw new InvalidOperationException(
                "Unsupported property/event '" +
                name +
                "' on " +
                instance.GetType().FullName +
                ".");
        }

        private bool ApplyBoundObjectAttribute(
            object instance,
            string name,
            object value)
        {
            return ApplyBoundObjectAttribute(
                instance,
                name,
                value,
                false);
        }

        private bool ApplyBoundObjectAttribute(
            object instance,
            string name,
            object value,
            bool styleSetter)
        {
            if (IsUnsetPresetValue(value))
                return true;

            bool mappedPropertyPath =
                UsesMappedPropertyPath(
                    instance,
                    name,
                    GetStylePropertyKey(instance, name));

            Control mappedControl = instance as Control;

            if (mappedPropertyPath &&
                mappedControl != null &&
                EqualsIgnoreCase(name, "Padding") &&
                value is Padding)
            {
                SetPropertyObjectValue(
                    mappedControl,
                    GetControlPaddingProperty(),
                    value);
                return true;
            }

            // Mapped XAML names use one destination for literal, bound, and
            // style values. Other bindings retain the normal exact-CLR-property
            // behavior, including custom control properties.
            PropertyInfo property =
                FindProperty(
                    instance.GetType(),
                    name);

            if (!mappedPropertyPath &&
                property != null &&
                property.CanWrite)
            {
                SetPropertyObjectValue(
                    instance,
                    property,
                    value);

                return true;
            }

            // WPF Image.Source -> WinForms PictureBox.Image/ImageLocation.
            if (EqualsIgnoreCase(
                name,
                "Source"))
            {
                PictureBox picture =
                    instance as PictureBox;

                if (picture != null)
                {
                    if (value == null)
                    {
                        SetMappedPictureBoxSource(
                            picture,
                            null,
                            null,
                            false,
                            false);

                        return true;
                    }

                    Image image =
                        value as Image;

                    if (image != null)
                    {
                        SetMappedPictureBoxSource(
                            picture,
                            image,
                            null,
                            false,
                            false);

                        return true;
                    }

                    Icon icon =
                        value as Icon;

                    if (icon != null)
                    {
                        Image bitmap = GetDecodedImageFromIcon(icon);
                        SetMappedPictureBoxSource(
                            picture,
                            bitmap,
                            null,
                            false,
                            true);
                        return true;
                    }

                    byte[] bytes =
                        value as byte[];

                    if (bytes != null)
                    {
                        Image decoded = GetDecodedImageFromBytes(bytes);
                        SetMappedPictureBoxSource(
                            picture,
                            decoded,
                            null,
                            false,
                            true);

                        return true;
                    }
                }

                WebBrowser browser =
                    instance as WebBrowser;

                if (browser != null && value == null)
                {
                    browser.Url = null;
                    return true;
                }
            }

            // For WPF aliases (Background, Foreground, Margin, etc.) fall
            // through the existing string property mapper after converting
            // the CLR object to the same textual form normal XAML would use.
            string text =
                BindingValueToString(value);

            if (TryApplyWpfProperty(
                instance,
                name,
                text))
            {
                return true;
            }

            property =
                FindProperty(
                    instance.GetType(),
                    name);

            if (property != null &&
                property.CanWrite)
            {
                SetPropertyValue(
                    instance,
                    property,
                    text);

                return true;
            }

            return false;
        }

        private void ResetPresetBoundProperty(
            object instance,
            string propertyName)
        {
            if (instance == null || String.IsNullOrEmpty(propertyName))
                return;

            string key = GetStylePropertyKey(instance, propertyName);

            if (EqualsIgnoreCase(key, "Background"))
            {
                Control control = instance as Control;

                if (control != null)
                {
                    // BackColor is ambient in WinForms. Assigning a captured
                    // color is not equivalent to removing a Dark-only preset:
                    // ResetBackColor restores the control's native/default or
                    // inherited color and keeps future parent theme changes
                    // observable.
                    ResetMappedControlBackground(control);
                }
                else
                {
                    ResetObjectProperty(instance, "BackColor");
                }

                return;
            }

            if (EqualsIgnoreCase(key, "Foreground"))
            {
                Control control = instance as Control;

                if (control != null)
                    ResetMappedControlForeground(control);
                else
                    ResetObjectProperty(instance, "ForeColor");

                return;
            }

            if (EqualsIgnoreCase(key, "Visibility"))
            {
                ResetObjectProperty(instance, "Visible");
                return;
            }

            if (EqualsIgnoreCase(key, "FlowDirection"))
            {
                ResetObjectProperty(instance, "RightToLeft");

                if (instance is Form)
                    ResetObjectProperty(instance, "RightToLeftLayout");

                return;
            }

            if (EqualsIgnoreCase(key, "Source"))
            {
                if (instance is PictureBox)
                {
                    ApplyBoundObjectAttribute(
                        instance,
                        "Source",
                        null,
                        false);
                    return;
                }

                if (instance is WebBrowser)
                {
                    ((WebBrowser)instance).Url = null;
                    return;
                }
            }

            if (EqualsIgnoreCase(key, "TextAlignment"))
            {
                ResetObjectProperty(instance, "TextAlign");
                return;
            }

            if (EqualsIgnoreCase(key, "ApplicationIcon"))
            {
                ResetObjectProperty(instance, "Icon");
                return;
            }

            if (EqualsIgnoreCase(key, "Font") ||
                EqualsIgnoreCase(key, "FontFamily") ||
                EqualsIgnoreCase(key, "FontSize") ||
                EqualsIgnoreCase(key, "FontWeight") ||
                EqualsIgnoreCase(key, "FontStyle"))
            {
                ResetObjectProperty(instance, "Font");
                return;
            }

            ResetObjectProperty(instance, key);
        }

        private RestoreStyleValue CapturePresetBoundPropertyBaseline(
            object instance,
            string propertyName)
        {
            if (instance == null || String.IsNullOrEmpty(propertyName))
                return null;

            string key = GetStylePropertyKey(instance, propertyName);

            // Background is ambient in WinForms and can also have a lower XML
            // style layer. CaptureStyleValue preserves both the native reset
            // semantics and ButtonBase.UseVisualStyleBackColor, instead of
            // reducing every missing preset to ResetBackColor().
            if (!EqualsIgnoreCase(key, "Background"))
                return null;

            return CaptureStyleValue(instance, propertyName, key);
        }

        private void RestorePresetBoundProperty(
            object instance,
            string propertyName,
            RestoreStyleValue baselineRestore)
        {
            if (baselineRestore != null)
            {
                baselineRestore();
                return;
            }

            ResetPresetBoundProperty(instance, propertyName);
        }

        private void ResetMappedControlBackground(Control control)
        {
            if (control == null)
                return;

            control.ResetBackColor();

            if (control is ButtonBase)
            {
                ResetObjectProperty(
                    control,
                    "UseVisualStyleBackColor");
            }

            ElementInfo info = GetInfo(control);
            info.BackgroundExplicit = false;
            info.BackgroundSet = false;
        }

        private void ResetMappedControlForeground(Control control)
        {
            if (control == null)
                return;

            control.ResetForeColor();

            ElementInfo info = GetInfo(control);
            info.ForegroundExplicit = false;
            info.ForegroundSet = false;
        }

        private void ResetObjectProperty(
            object instance,
            string propertyName)
        {
            PropertyDescriptor descriptor =
                TypeDescriptor.GetProperties(instance).Find(
                    propertyName,
                    true);

            if (descriptor == null || descriptor.IsReadOnly)
                return;

            PropertyInfo property = FindProperty(
                instance.GetType(),
                descriptor.Name);
            DefaultValueAttribute defaultAttribute =
                descriptor.Attributes[typeof(DefaultValueAttribute)] as
                    DefaultValueAttribute;

            if (property != null && property.CanWrite &&
                defaultAttribute != null)
            {
                SetPropertyObjectValue(
                    instance,
                    property,
                    defaultAttribute.Value);
                return;
            }

            if (descriptor.CanResetValue(instance))
            {
                descriptor.ResetValue(instance);
                return;
            }

            if (property == null || !property.CanWrite)
                return;

            object defaultValue = property.PropertyType.IsValueType
                ? Activator.CreateInstance(property.PropertyType)
                : null;
            SetPropertyObjectValue(instance, property, defaultValue);
        }

        private void SetPropertyObjectValue(
            object instance,
            PropertyInfo property,
            object value)
        {
            object converted = null;

            if (!TryConvertObjectValue(
                value,
                property.PropertyType,
                out converted))
            {
                throw new InvalidOperationException(
                    "Cannot assign bound value of type " +
                    (value == null
                        ? "<null>"
                        : value.GetType().FullName) +
                    " to " +
                    instance.GetType().Name +
                    "." +
                    property.Name +
                    " (" +
                    property.PropertyType.FullName +
                    ").");
            }

            object previousValue;
            bool previousValueKnown = TryReadPropertyValue(
                instance,
                property,
                out previousValue);
            StyleMetadataState previousMetadata =
                PublishNativePropertyMetadata(
                    instance,
                    property.Name,
                    converted);

            try
            {
                property.SetValue(
                    instance,
                    converted,
                    null);
            }
            catch (Exception ex)
            {
                object actualValue;
                bool actualValueKnown;

                ReconcileFailedPropertyAssignment(
                    instance,
                    property,
                    previousValue,
                    previousValueKnown,
                    converted,
                    previousMetadata,
                    out actualValue,
                    out actualValueKnown);

                if (!actualValueKnown ||
                    !Object.ReferenceEquals(actualValue, previousValue))
                {
                    ReleaseOwnedPropertyValue(
                        instance,
                        property.Name,
                        actualValueKnown ? actualValue : converted);
                }

                throw new InvalidOperationException(
                    "Could not assign bound object to " +
                    instance.GetType().Name +
                    "." +
                    property.Name +
                    ": " +
                    ex.Message,
                    ex);
            }

            object installedValue;
            bool installedValueKnown = TryReadPropertyValue(
                instance,
                property,
                out installedValue);

            ReleaseOwnedPropertyValue(
                instance,
                property.Name,
                installedValueKnown ? installedValue : converted);
        }

        private void ApplyNativePropertyMetadata(
            object instance,
            string propertyName,
            object value)
        {
            Control control = instance as Control;

            if (control == null)
                return;

            ElementInfo info = GetInfo(instance);

            if (EqualsIgnoreCase(propertyName, "Width"))
            {
                info.WidthExplicit = true;
                InvalidateFlexWidthBasis(info);
            }
            else if (EqualsIgnoreCase(propertyName, "Height"))
            {
                info.HeightExplicit = true;
                InvalidateFlexHeightBasis(info);
            }
            else if (EqualsIgnoreCase(propertyName, "Size"))
            {
                info.WidthExplicit = true;
                info.HeightExplicit = true;
                InvalidateFlexWidthBasis(info);
                InvalidateFlexHeightBasis(info);
            }
            else if (EqualsIgnoreCase(propertyName, "Margin") &&
                     value is Padding)
            {
                info.Margin = (Padding)value;
            }
            else if (EqualsIgnoreCase(propertyName, "Foreground") ||
                     EqualsIgnoreCase(propertyName, "ForeColor"))
            {
                info.ForegroundExplicit = true;
                info.ForegroundSet = true;
            }
            else if (EqualsIgnoreCase(propertyName, "Background") ||
                     EqualsIgnoreCase(propertyName, "BackColor") ||
                     EqualsIgnoreCase(propertyName, "UseVisualStyleBackColor"))
            {
                info.BackgroundExplicit = true;
                info.BackgroundSet = true;
            }
            else if (EqualsIgnoreCase(propertyName, "FlowDirection") ||
                     EqualsIgnoreCase(propertyName, "RightToLeft") ||
                     EqualsIgnoreCase(propertyName, "RightToLeftLayout"))
            {
                info.FlowDirectionExplicit = true;
            }
            else if (EqualsIgnoreCase(propertyName, "Font") &&
                     value is Font)
            {
                info.FontFamilyExplicit = true;
                info.FontFamilySet = true;
                info.FontSizeExplicit = true;
                info.FontSizeSet = true;
                info.FontWeightExplicit = true;
                info.FontWeightSet = true;
                info.FontStyleExplicit = true;
                info.FontStyleSet = true;
                info.TextDecorationsExplicit = true;
                info.TextDecorationsSet = true;
            }
            else if (EqualsIgnoreCase(propertyName, "FontFamily"))
            {
                info.FontFamilyExplicit = true;
                info.FontFamilySet = true;
            }
            else if (EqualsIgnoreCase(propertyName, "FontSize"))
            {
                info.FontSizeExplicit = true;
                info.FontSizeSet = true;
            }
            else if (EqualsIgnoreCase(propertyName, "FontWeight"))
            {
                info.FontWeightExplicit = true;
                info.FontWeightSet = true;
            }
            else if (EqualsIgnoreCase(propertyName, "FontStyle"))
            {
                info.FontStyleExplicit = true;
                info.FontStyleSet = true;
            }
            else if (EqualsIgnoreCase(propertyName, "TextDecorations"))
            {
                info.TextDecorationsExplicit = true;
                info.TextDecorationsSet = true;
            }
            else if ((EqualsIgnoreCase(propertyName, "Visibility") ||
                      EqualsIgnoreCase(propertyName, "Visible")) &&
                     value is bool)
            {
                SetElementVisibilityState(
                    info,
                    !(bool)value,
                    false);

                if (info.ConditionStates != null &&
                    info.ConditionStates.Count != 0)
                {
                    ApplyElementEffectiveVisibility(instance, info);
                }
            }
        }

        private StyleMetadataState PublishNativePropertyMetadata(
            object instance,
            string propertyName,
            object value)
        {
            Control control = instance as Control;

            if (control == null)
                return null;

            ElementInfo info = GetInfo(instance);
            StyleMetadataState previous =
                CaptureStyleMetadata(
                    instance,
                    info,
                    GetStylePropertyKey(instance, propertyName));

            ApplyNativePropertyMetadata(
                instance,
                propertyName,
                value);

            return previous;
        }

        private void RestoreNativePropertyMetadata(
            object instance,
            StyleMetadataState previous)
        {
            if (instance == null || previous == null)
                return;

            RestoreStyleMetadata(
                instance,
                GetInfo(instance),
                previous);
        }

        private static bool TryReadPropertyValue(
            object instance,
            PropertyInfo property,
            out object value)
        {
            value = null;

            if (instance == null ||
                property == null ||
                !property.CanRead ||
                property.GetIndexParameters().Length != 0)
            {
                return false;
            }

            try
            {
                value = property.GetValue(instance, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool PropertyValuesMatch(
            object first,
            object second)
        {
            return Object.ReferenceEquals(first, second) ||
                (first != null && first.Equals(second));
        }

        private void ReconcileFailedPropertyAssignment(
            object instance,
            PropertyInfo property,
            object previousValue,
            bool previousValueKnown,
            object attemptedValue,
            StyleMetadataState previousMetadata,
            out object actualValue,
            out bool actualValueKnown)
        {
            actualValueKnown = TryReadPropertyValue(
                instance,
                property,
                out actualValue);

            bool previousValueStillInstalled =
                previousValueKnown &&
                actualValueKnown &&
                PropertyValuesMatch(actualValue, previousValue);

            if (!previousValueStillInstalled && previousValueKnown)
            {
                // Publish the old precedence before a compensating setter can
                // synchronously raise another native change callback.
                RestoreNativePropertyMetadata(
                    instance,
                    previousMetadata);

                try
                {
                    property.SetValue(
                        instance,
                        previousValue,
                        null);
                }
                catch
                {
                    // The compensating setter can also throw after committing.
                    // The read below decides which state actually survived.
                }

                actualValueKnown = TryReadPropertyValue(
                    instance,
                    property,
                    out actualValue);
                previousValueStillInstalled =
                    actualValueKnown &&
                    PropertyValuesMatch(actualValue, previousValue);
            }

            if (previousValueStillInstalled)
            {
                RestoreNativePropertyMetadata(
                    instance,
                    previousMetadata);
                return;
            }

            // Rollback did not restore the old native value. Keep precedence
            // metadata consistent with the value a callback actually left behind.
            RestoreNativePropertyMetadata(
                instance,
                previousMetadata);
            ApplyNativePropertyMetadata(
                instance,
                property.Name,
                actualValueKnown ? actualValue : attemptedValue);
        }

        private void SetControlVisibility(
            Control control,
            bool hidden,
            bool collapsed)
        {
            PropertyInfo property = FindProperty(
                control.GetType(),
                "Visible");
            object previousValue;
            bool previousValueKnown = TryReadPropertyValue(
                control,
                property,
                out previousValue);
            ElementInfo info = GetInfo(control);
            StyleMetadataState previousMetadata = CaptureStyleMetadata(
                control,
                info,
                "Visibility");

            SetElementVisibilityState(
                info,
                hidden,
                collapsed);
            bool effectiveVisible =
                !info.Hidden && !info.Collapsed;

            try
            {
                property.SetValue(
                    control,
                    effectiveVisible,
                    null);
            }
            catch
            {
                object actualValue;
                bool actualValueKnown;

                ReconcileFailedPropertyAssignment(
                    control,
                    property,
                    previousValue,
                    previousValueKnown,
                    effectiveVisible,
                    previousMetadata,
                    out actualValue,
                    out actualValueKnown);

                if (!actualValueKnown ||
                    (actualValue is bool &&
                     (bool)actualValue == effectiveVisible &&
                     (!previousValueKnown ||
                      !PropertyValuesMatch(actualValue, previousValue))))
                {
                    SetElementVisibilityState(
                        info,
                        hidden,
                        collapsed);
                }

                throw;
            }
        }

        private void SetObjectFontWithMetadata(
            object instance,
            string propertyName,
            object value,
            string family,
            float size,
            FontStyle style)
        {
            Font previousFont = GetObjectFont(instance);
            Font font = GetCachedFont(
                family,
                size,
                style,
                previousFont);
            StyleMetadataState previousMetadata =
                PublishNativePropertyMetadata(
                    instance,
                    propertyName,
                    value);

            if (font == null ||
                (previousFont != null && previousFont.Equals(font)))
            {
                return;
            }

            PropertyInfo property = GetObjectFontProperty(instance);

            if (property == null || !property.CanWrite)
                return;

            try
            {
                property.SetValue(instance, font, null);
            }
            catch
            {
                object actualValue;
                bool actualValueKnown;

                ReconcileFailedPropertyAssignment(
                    instance,
                    property,
                    previousFont,
                    previousFont != null,
                    font,
                    previousMetadata,
                    out actualValue,
                    out actualValueKnown);

                if (!actualValueKnown ||
                    !Object.ReferenceEquals(actualValue, previousFont))
                {
                    ReleaseOwnedPropertyValue(
                        instance,
                        "Font",
                        actualValueKnown ? actualValue : font);
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
}
