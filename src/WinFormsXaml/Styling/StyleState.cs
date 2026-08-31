using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private static readonly Hashtable _sharedStylePropertyNames =
            CreateSharedStylePropertyNames();

        private static Hashtable CreateSharedStylePropertyNames()
        {
            string[] names = new string[]
            {
                "AcceptsReturn",
                "AutoScroll",
                "BackColor",
                "Background",
                "BorderBrush",
                "Checked",
                "Content",
                "Direction",
                "Enabled",
                "FlowDirection",
                "ForeColor",
                "Foreground",
                "Header",
                "Height",
                "HorizontalScrollBarVisibility",
                "Icon",
                "Image",
                "ImageLocation",
                "IsChecked",
                "IsEnabled",
                "IsReadOnly",
                "IsTabStop",
                "MaxHeight",
                "MaxWidth",
                "MaximumSize",
                "MinHeight",
                "MinWidth",
                "MinimumSize",
                "Multiline",
                "Orientation",
                "ReadOnly",
                "ResourceStyle",
                "RightToLeft",
                "RightToLeftLayout",
                "Size",
                "SizeMode",
                "Source",
                "StackOrientation",
                "StartPosition",
                "Stretch",
                "TabStop",
                "Text",
                "TextAlign",
                "TextAlignment",
                "TextWrapping",
                "Title",
                "Url",
                "UseApplicationIcon",
                "UseVisualStyleBackColor",
                "VerticalScrollBarVisibility",
                "Visibility",
                "Visible",
                "Width",
                "WordWrap"
            };
            Hashtable result = new Hashtable(
                StringComparer.OrdinalIgnoreCase);
            int i;

            for (i = 0; i < names.Length; i++)
                result.Add(names[i], names[i]);

            return result;
        }

        /// <summary>
        /// Returns the precedence key for a style/local value. WPF aliases that
        /// update the same WinForms state deliberately share one key.
        /// </summary>
        private string GetStylePropertyKey(
            object target,
            string propertyName)
        {
            string name = propertyName == null
                ? String.Empty
                : propertyName.Trim();

            // Most native/custom properties do not share storage with an alias.
            // Avoid the target checks and comparison chain for that common case.
            if (!_sharedStylePropertyNames.ContainsKey(name))
                return name;

            if (EqualsIgnoreCase(name, "ResourceStyle"))
                return "ResourceStyle";

            if (EqualsIgnoreCase(name, "Background") ||
                EqualsIgnoreCase(name, "BackColor") ||
                EqualsIgnoreCase(name, "UseVisualStyleBackColor"))
            {
                return "Background";
            }

            if (EqualsIgnoreCase(name, "Foreground") ||
                EqualsIgnoreCase(name, "ForeColor"))
            {
                return "Foreground";
            }

            if (EqualsIgnoreCase(name, "Text"))
            {
                return "Text";
            }

            if (target is TabViewItem &&
                EqualsIgnoreCase(name, "Header"))
            {
                return "Text";
            }

            if ((EqualsIgnoreCase(name, "Content") ||
                 EqualsIgnoreCase(name, "Header") ||
                 EqualsIgnoreCase(name, "Title")) &&
                !HasWritableProperty(target, name) &&
                HasWritableProperty(target, "Text"))
            {
                return "Text";
            }

            if (EqualsIgnoreCase(name, "FlowDirection") ||
                EqualsIgnoreCase(name, "RightToLeft") ||
                EqualsIgnoreCase(name, "RightToLeftLayout"))
            {
                return "FlowDirection";
            }

            if (EqualsIgnoreCase(name, "Visibility") ||
                EqualsIgnoreCase(name, "Visible"))
            {
                return "Visibility";
            }

            if (EqualsIgnoreCase(name, "IsEnabled") ||
                EqualsIgnoreCase(name, "Enabled"))
            {
                return "Enabled";
            }

            if (EqualsIgnoreCase(name, "IsTabStop") ||
                EqualsIgnoreCase(name, "TabStop"))
            {
                return "TabStop";
            }

            if ((target is CheckBox || target is RadioButton) &&
                (EqualsIgnoreCase(name, "IsChecked") ||
                 EqualsIgnoreCase(name, "Checked")))
            {
                return "Checked";
            }

            if (target is TextBox &&
                (EqualsIgnoreCase(name, "IsReadOnly") ||
                 EqualsIgnoreCase(name, "ReadOnly")))
            {
                return "ReadOnly";
            }

            if (target is PictureBox &&
                (EqualsIgnoreCase(name, "Source") ||
                 EqualsIgnoreCase(name, "Image") ||
                 EqualsIgnoreCase(name, "ImageLocation")))
            {
                return "Source";
            }

            if (target is WebBrowser &&
                (EqualsIgnoreCase(name, "Source") ||
                 EqualsIgnoreCase(name, "Url")))
            {
                return "Source";
            }

            if (target is Form &&
                EqualsIgnoreCase(name, "Icon"))
            {
                return "ApplicationIcon";
            }

            if (target is Form &&
                EqualsIgnoreCase(name, "UseApplicationIcon"))
            {
                return "UseApplicationIcon";
            }

            if ((target is TextBox ||
                 target is Label ||
                 target is ButtonBase) &&
                (EqualsIgnoreCase(name, "TextAlignment") ||
                 EqualsIgnoreCase(name, "TextAlign")))
            {
                return "TextAlignment";
            }

            // Width and Height remain independent axes, while the native Size
            // property conflicts with either one in local/style precedence.
            if (EqualsIgnoreCase(name, "Width"))
                return "Width";

            if (EqualsIgnoreCase(name, "Height"))
                return "Height";

            if (EqualsIgnoreCase(name, "Size"))
                return "Size";

            // Keep the two axes independent. Both map to the same native Size
            // property, but a local MinWidth must not suppress a style MinHeight.
            if (EqualsIgnoreCase(name, "MinWidth"))
                return "MinWidth";

            if (EqualsIgnoreCase(name, "MinHeight"))
                return "MinHeight";

            if (EqualsIgnoreCase(name, "MinimumSize"))
                return "MinimumSize";

            if (EqualsIgnoreCase(name, "MaxWidth"))
                return "MaxWidth";

            if (EqualsIgnoreCase(name, "MaxHeight"))
                return "MaxHeight";

            if (EqualsIgnoreCase(name, "MaximumSize"))
                return "MaximumSize";

            if (target is TextBox &&
                (EqualsIgnoreCase(name, "TextWrapping") ||
                 EqualsIgnoreCase(name, "Multiline") ||
                 EqualsIgnoreCase(name, "AcceptsReturn") ||
                 EqualsIgnoreCase(name, "WordWrap")))
            {
                return "Multiline";
            }

            if (target is PictureBox &&
                (EqualsIgnoreCase(name, "Stretch") ||
                 EqualsIgnoreCase(name, "SizeMode")))
            {
                return "ImageStretch";
            }

            if ((target is StackHost &&
                 (EqualsIgnoreCase(name, "Orientation") ||
                  EqualsIgnoreCase(name, "StackOrientation"))) ||
                (target is FlexPanel &&
                 (EqualsIgnoreCase(name, "Orientation") ||
                  EqualsIgnoreCase(name, "Direction"))))
            {
                return "Orientation";
            }

            if (target is Form &&
                EqualsIgnoreCase(name, "StartPosition"))
            {
                return "StartPosition";
            }

            if (target is BorderHost &&
                EqualsIgnoreCase(name, "BorderBrush"))
            {
                return "BorderBrush";
            }

            if (target is ScrollHost &&
                (EqualsIgnoreCase(name, "VerticalScrollBarVisibility") ||
                 EqualsIgnoreCase(name, "HorizontalScrollBarVisibility") ||
                 EqualsIgnoreCase(name, "AutoScroll")))
            {
                return "ScrollBarVisibility";
            }

            return name;
        }

        private static bool IsFontStylePropertyKey(string key)
        {
            return EqualsIgnoreCase(key, "Font") ||
                   EqualsIgnoreCase(key, "FontFamily") ||
                   EqualsIgnoreCase(key, "FontSize") ||
                   EqualsIgnoreCase(key, "FontWeight") ||
                   EqualsIgnoreCase(key, "FontStyle") ||
                   EqualsIgnoreCase(key, "TextDecorations");
        }

        private void ActivateStyleValue(
            object instance,
            string propertyName)
        {
            if (instance == null || String.IsNullOrEmpty(propertyName))
                return;

            PropertyInfo property = FindProperty(
                instance.GetType(),
                propertyName);
            string key = GetStylePropertyKey(instance, propertyName);

            // Ordinary CLR properties take precedence over same-named events.
            // Known WPF aliases are resolved even earlier, so an unrelated custom
            // event named Content/Background/etc. must not suppress their baseline.
            if ((property == null ||
                 !property.CanWrite ||
                 property.GetIndexParameters().Length != 0) &&
                FindEvent(instance.GetType(), propertyName) != null &&
                !UsesMappedPropertyPath(
                    instance,
                    propertyName,
                    key))
            {
                return;
            }

            ElementInfo info = GetInfo(instance);

            if (info.StyleValueSlots == null)
            {
                info.StyleValueSlots =
                    new Hashtable(StringComparer.OrdinalIgnoreCase);
                info.ActiveStyleValueSlots = new ArrayList();
            }

            StyleValueSlot slot =
                info.StyleValueSlots[key] as StyleValueSlot;

            if (slot == null)
            {
                slot = new StyleValueSlot();
                info.StyleValueSlots[key] = slot;
            }

            // Base/derived and implicit/explicit styles can set the same effect.
            // Capture only the value below the whole style layer.
            if (slot.Active)
                return;

            slot.Restore = CaptureStyleValue(
                instance,
                propertyName,
                key);
            slot.Active = true;
            info.ActiveStyleValueSlots.Add(slot);
        }

        private void RestoreActiveStyleValues(object target)
        {
            if (target == null)
                return;

            ElementInfo info = GetInfo(target);
            ArrayList active = info.ActiveStyleValueSlots;

            if (active == null || active.Count == 0)
                return;

            Exception firstError = null;
            int i;

            for (i = active.Count - 1; i >= 0; i--)
            {
                StyleValueSlot slot = active[i] as StyleValueSlot;

                if (slot == null || !slot.Active)
                {
                    active.RemoveAt(i);
                    continue;
                }

                try
                {
                    if (slot.Restore != null)
                        slot.Restore();

                    slot.Restore = null;
                    slot.Active = false;
                    active.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;

                    // Earlier slots are the lower layers captured by the failed
                    // slot. Restoring them now would invalidate that slot's saved
                    // baseline and could resurrect an old style on the next retry.
                    break;
                }
            }

            if (firstError != null)
            {
                throw new InvalidOperationException(
                    "Could not restore the value below the previous style: " +
                    firstError.Message,
                    firstError);
            }
        }

        private RestoreStyleValue CaptureStyleValue(
            object instance,
            string propertyName,
            string key)
        {
            ElementInfo info = GetInfo(instance);

            if (instance is Form &&
                EqualsIgnoreCase(key, "UseApplicationIcon"))
            {
                FormIconState state = info.FormIcon;
                bool baseline = state != null &&
                    state.UseApplicationIcon;

                return delegate
                {
                    if (state == null)
                        return;

                    state.UseApplicationIcon = baseline;

                    if (state.ConfigurationReady &&
                        !info.StyleTransitionActive)
                    {
                        ReconcileApplicationIconDefault(instance as Form);
                    }
                };
            }

            ArrayList propertyValues = new ArrayList();

            bool mappedControlAlias = UsesMappedPropertyPath(
                instance,
                propertyName,
                key);

            // Mapped XAML names are consumed before ordinary CLR lookup. A
            // same-named custom member is therefore not part of that style value.
            if (!mappedControlAlias)
            {
                CaptureStyleProperty(
                    instance,
                    propertyValues,
                    propertyName,
                    key);
            }

            CaptureStyleAliasProperties(
                instance,
                propertyValues,
                propertyName,
                key);

            StyleMetadataState metadata =
                CaptureStyleMetadata(instance, info, key);

            return delegate
            {
                StyleMetadataState activeMetadata =
                    CaptureStyleMetadata(instance, info, key);
                RestoreStyleMetadata(instance, info, metadata);

                try
                {
                    // Publish the baseline precedence before native setters raise
                    // synchronous layout/font/visibility callbacks.
                    RestoreStyleProperties(
                        instance,
                        propertyValues,
                        activeMetadata,
                        metadata);
                }
                catch (Exception ex)
                {
                    StyleRestoreFailure failure =
                        ex as StyleRestoreFailure;

                    // A compensating native setter normally restores the active
                    // style. If even that setter cannot undo a committed baseline,
                    // keep metadata aligned with the native value that survived.
                    RestoreStyleMetadata(
                        instance,
                        info,
                        failure != null &&
                            failure.BaselineMetadataRequired
                            ? metadata
                            : activeMetadata);
                    throw;
                }
            };
        }

        private bool UsesMappedPropertyPath(
            object instance,
            string propertyName,
            string key)
        {
            if ((EqualsIgnoreCase(propertyName, "Content") ||
                 EqualsIgnoreCase(propertyName, "Header") ||
                 EqualsIgnoreCase(propertyName, "Title")) &&
                !HasWritableProperty(instance, propertyName) &&
                HasWritableProperty(instance, "Text"))
            {
                return true;
            }

            if (EqualsIgnoreCase(key, "Background") &&
                HasWritablePropertyOfType(
                    instance,
                    "BackColor",
                    typeof(Color)))
            {
                return true;
            }

            if (EqualsIgnoreCase(key, "Foreground") &&
                HasWritablePropertyOfType(
                    instance,
                    "ForeColor",
                    typeof(Color)))
            {
                return true;
            }

            if ((EqualsIgnoreCase(propertyName, "FontFamily") ||
                 EqualsIgnoreCase(propertyName, "FontSize") ||
                 EqualsIgnoreCase(propertyName, "FontWeight") ||
                 EqualsIgnoreCase(propertyName, "FontStyle") ||
                 EqualsIgnoreCase(propertyName, "TextDecorations")) &&
                HasWritableObjectFontProperty(instance))
            {
                return true;
            }

            if (!(instance is Control))
                return false;

            if (EqualsIgnoreCase(key, "Background") ||
                EqualsIgnoreCase(key, "Foreground") ||
                EqualsIgnoreCase(key, "Visibility"))
            {
                return true;
            }

            if (EqualsIgnoreCase(propertyName, "FlexGrow") ||
                EqualsIgnoreCase(propertyName, "HorizontalAlignment") ||
                EqualsIgnoreCase(propertyName, "VerticalAlignment") ||
                EqualsIgnoreCase(propertyName, "Padding") ||
                EqualsIgnoreCase(propertyName, "ToolTip"))
            {
                return true;
            }

            if (EqualsIgnoreCase(propertyName, "UseApplicationIcon"))
            {
                return instance is Form;
            }

            if (EqualsIgnoreCase(propertyName, "Source"))
            {
                return instance is PictureBox || instance is WebBrowser;
            }

            if (EqualsIgnoreCase(propertyName, "Stretch"))
                return instance is PictureBox;

            if (EqualsIgnoreCase(propertyName, "Orientation"))
            {
                return instance is StackHost ||
                    instance is FlexPanel ||
                    instance is TrackBar;
            }

            if (EqualsIgnoreCase(propertyName, "TextWrapping"))
                return instance is TextBox || instance is Label;

            if (EqualsIgnoreCase(propertyName, "AcceptsReturn") ||
                EqualsIgnoreCase(propertyName, "AcceptsTab"))
            {
                return instance is TextBox;
            }

            if (EqualsIgnoreCase(propertyName, "BorderBrush") ||
                EqualsIgnoreCase(propertyName, "BorderThickness"))
            {
                return instance is BorderHost;
            }

            if (EqualsIgnoreCase(propertyName, "LastChildFill"))
                return instance is DockHost;

            if (EqualsIgnoreCase(
                    propertyName,
                    "VerticalScrollBarVisibility") ||
                EqualsIgnoreCase(
                    propertyName,
                    "HorizontalScrollBarVisibility"))
            {
                return instance is ScrollHost;
            }

            return EqualsIgnoreCase(propertyName, "FlowDirection") ||
                EqualsIgnoreCase(propertyName, "RightToLeft") ||
                EqualsIgnoreCase(propertyName, "IsEnabled") ||
                EqualsIgnoreCase(propertyName, "IsTabStop") ||
                EqualsIgnoreCase(propertyName, "MinWidth") ||
                EqualsIgnoreCase(propertyName, "MinHeight") ||
                EqualsIgnoreCase(propertyName, "MaxWidth") ||
                EqualsIgnoreCase(propertyName, "MaxHeight") ||
                (EqualsIgnoreCase(propertyName, "IsChecked") &&
                 (instance is CheckBox || instance is RadioButton)) ||
                (EqualsIgnoreCase(propertyName, "IsReadOnly") &&
                 instance is TextBox) ||
                (EqualsIgnoreCase(propertyName, "TextAlignment") &&
                 (instance is TextBox ||
                  instance is Label ||
                  instance is ButtonBase));
        }

        private static bool HasWritableObjectFontProperty(object instance)
        {
            PropertyInfo property = GetObjectFontProperty(instance);

            return property != null && property.CanWrite;
        }

        private static bool HasWritableProperty(
            object instance,
            string propertyName)
        {
            if (instance == null)
                return false;

            PropertyInfo property = FindProperty(
                instance.GetType(),
                propertyName);

            return property != null &&
                property.CanWrite &&
                property.GetIndexParameters().Length == 0;
        }

        private static bool IsResourceStyleProperty(
            object instance,
            string propertyName)
        {
            return EqualsIgnoreCase(propertyName, "ResourceStyle") ||
                (EqualsIgnoreCase(propertyName, "Style") &&
                 !HasWritableProperty(instance, "Style"));
        }

        private static bool HasWritablePropertyOfType(
            object instance,
            string propertyName,
            Type propertyType)
        {
            if (instance == null)
                return false;

            PropertyInfo property = FindProperty(
                instance.GetType(),
                propertyName);

            return property != null &&
                property.CanWrite &&
                property.PropertyType == propertyType &&
                property.GetIndexParameters().Length == 0;
        }

        private void CaptureStyleAliasProperties(
            object instance,
            ArrayList values,
            string propertyName,
            string key)
        {
            if (EqualsIgnoreCase(key, "Background"))
            {
                CaptureStyleProperty(instance, values, "BackColor", key);
                CaptureStyleProperty(
                    instance,
                    values,
                    "UseVisualStyleBackColor",
                    key);
            }
            else if (EqualsIgnoreCase(key, "Foreground"))
            {
                CaptureStyleProperty(instance, values, "ForeColor", key);
            }
            else if (EqualsIgnoreCase(key, "Padding"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(Control),
                    "Padding",
                    key);
            }
            else if (EqualsIgnoreCase(key, "Text"))
            {
                CaptureStyleProperty(instance, values, "Text", key);
            }
            else if (EqualsIgnoreCase(key, "FlowDirection"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(Control),
                    "RightToLeft",
                    key);
                CaptureStyleProperty(instance, values, "RightToLeftLayout", key);
            }
            else if (EqualsIgnoreCase(key, "Visibility"))
            {
                CaptureStyleProperty(instance, values, "Visible", key);
            }
            else if (EqualsIgnoreCase(key, "Enabled"))
            {
                if (EqualsIgnoreCase(propertyName, "IsEnabled"))
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(Control),
                        "Enabled",
                        key);
                }
                else
                {
                    CaptureStyleProperty(instance, values, "Enabled", key);
                }
            }
            else if (EqualsIgnoreCase(key, "TabStop"))
            {
                if (EqualsIgnoreCase(propertyName, "IsTabStop"))
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(Control),
                        "TabStop",
                        key);
                }
                else
                {
                    CaptureStyleProperty(instance, values, "TabStop", key);
                }
            }
            else if (EqualsIgnoreCase(key, "Checked"))
            {
                Type owner = instance is CheckBox
                    ? typeof(CheckBox)
                    : typeof(RadioButton);

                CaptureExactStyleProperty(
                    instance,
                    values,
                    owner,
                    "Checked",
                    key);
            }
            else if (EqualsIgnoreCase(key, "ReadOnly"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(TextBoxBase),
                    "ReadOnly",
                    key);
            }
            else if (EqualsIgnoreCase(key, "Source"))
            {
                if (instance is PictureBox)
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(PictureBox),
                        "Image",
                        key);
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(PictureBox),
                        "ImageLocation",
                        key);
                }
                else if (instance is WebBrowser)
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(WebBrowser),
                        "Url",
                        key);
                }
            }
            else if (EqualsIgnoreCase(key, "ApplicationIcon"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(Form),
                    "Icon",
                    key);
            }
            else if (EqualsIgnoreCase(key, "TextAlignment"))
            {
                Type owner = instance is TextBox
                    ? typeof(TextBox)
                    : instance is Label
                        ? typeof(Label)
                        : typeof(ButtonBase);

                CaptureExactStyleProperty(
                    instance,
                    values,
                    owner,
                    "TextAlign",
                    key);
            }
            else if (EqualsIgnoreCase(key, "Font"))
            {
                CaptureStyleProperty(instance, values, "Font", key);
            }
            else if (IsFontStylePropertyKey(key))
            {
                CaptureStyleFontPart(
                    instance,
                    values,
                    key);
            }
            else if (EqualsIgnoreCase(key, "MinimumSize"))
            {
                CaptureStyleProperty(instance, values, "MinimumSize", key);
            }
            else if (EqualsIgnoreCase(key, "MinWidth"))
            {
                CaptureStyleSizeAxis(
                    instance,
                    values,
                    typeof(Control),
                    "MinimumSize",
                    1);
            }
            else if (EqualsIgnoreCase(key, "MinHeight"))
            {
                CaptureStyleSizeAxis(
                    instance,
                    values,
                    typeof(Control),
                    "MinimumSize",
                    2);
            }
            else if (EqualsIgnoreCase(key, "MaximumSize"))
            {
                CaptureStyleProperty(instance, values, "MaximumSize", key);
            }
            else if (EqualsIgnoreCase(key, "MaxWidth"))
            {
                CaptureStyleSizeAxis(
                    instance,
                    values,
                    typeof(Control),
                    "MaximumSize",
                    1);
            }
            else if (EqualsIgnoreCase(key, "MaxHeight"))
            {
                CaptureStyleSizeAxis(
                    instance,
                    values,
                    typeof(Control),
                    "MaximumSize",
                    2);
            }
            else if (EqualsIgnoreCase(key, "Multiline"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(TextBox),
                    "WordWrap",
                    key);
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(TextBox),
                    "Multiline",
                    key);
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(TextBox),
                    "AcceptsReturn",
                    key);
            }
            else if (EqualsIgnoreCase(key, "AcceptsTab"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(TextBox),
                    "AcceptsTab",
                    key);
            }
            else if (EqualsIgnoreCase(key, "ImageStretch"))
            {
                if (instance is ImageControl)
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(ImageControl),
                        "Stretch",
                        key);
                }
                else
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(PictureBox),
                        "SizeMode",
                        key);
                }
            }
            else if (EqualsIgnoreCase(key, "Orientation"))
            {
                if (instance is StackHost)
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(StackHost),
                        "StackOrientation",
                        key);
                }
                else if (instance is FlexPanel)
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(FlexPanel),
                        "Direction",
                        key);
                }
                else if (instance is TrackBar)
                {
                    CaptureExactStyleProperty(
                        instance,
                        values,
                        typeof(TrackBar),
                        "Orientation",
                        key);
                }
            }
            else if (EqualsIgnoreCase(key, "LastChildFill"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(DockHost),
                    "LastChildFill",
                    key);
            }
            else if (EqualsIgnoreCase(key, "BorderThickness"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(BorderHost),
                    "BorderThickness",
                    key);
            }
            else if (EqualsIgnoreCase(key, "BorderBrush"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(BorderHost),
                    "BorderColor",
                    key);
            }
            else if (EqualsIgnoreCase(key, "ScrollBarVisibility"))
            {
                CaptureExactStyleProperty(
                    instance,
                    values,
                    typeof(ScrollHost),
                    "AutoScroll",
                    key);
            }
        }

        private void CaptureStyleProperty(
            object instance,
            ArrayList values,
            string propertyName,
            string key)
        {
            if (instance == null || String.IsNullOrEmpty(propertyName))
                return;

            PropertyInfo property = FindProperty(
                instance.GetType(),
                propertyName);

            if (property == null ||
                !property.CanRead ||
                !property.CanWrite ||
                property.GetIndexParameters().Length != 0)
            {
                CaptureStyleField(
                    instance,
                    values,
                    propertyName);
                return;
            }

            CaptureStyleProperty(
                instance,
                values,
                property,
                key);
        }

        private void CaptureExactStyleProperty(
            object instance,
            ArrayList values,
            Type ownerType,
            string propertyName,
            string key)
        {
            if (instance == null ||
                ownerType == null ||
                !ownerType.IsInstanceOfType(instance) ||
                String.IsNullOrEmpty(propertyName))
            {
                return;
            }

            PropertyInfo property = ownerType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            if (property != null)
            {
                CaptureStyleProperty(
                    instance,
                    values,
                    property,
                    key);
                return;
            }

            FieldInfo field = ownerType.GetField(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

            CaptureStyleField(
                instance,
                values,
                field);
        }

        private void CaptureStyleProperty(
            object instance,
            ArrayList values,
            PropertyInfo property,
            string key)
        {
            if (property == null ||
                !property.CanRead ||
                !property.CanWrite ||
                property.GetIndexParameters().Length != 0)
            {
                return;
            }

            int i;
            for (i = 0; i < values.Count; i++)
            {
                StylePropertyValue existing =
                    values[i] as StylePropertyValue;

                if (existing != null &&
                    Object.Equals(existing.Property, property))
                {
                    return;
                }
            }

            object value;

            try
            {
                value = property.GetValue(instance, null);
            }
            catch
            {
                return;
            }

            StylePropertyValue captured = new StylePropertyValue();
            captured.Property = property;
            captured.Value = value;

            PropertyInfo resolvedProperty = FindProperty(
                instance.GetType(),
                property.Name);

            try
            {
                if (Object.Equals(resolvedProperty, property))
                {
                    captured.Descriptor = TypeDescriptor
                        .GetProperties(instance)
                        .Find(property.Name, true);
                }

                captured.ResetToDefault =
                    captured.Descriptor != null &&
                    !captured.Descriptor.ShouldSerializeValue(instance);
            }
            catch
            {
                // A custom descriptor must not prevent an otherwise normal CLR
                // property from participating in style replacement.
                captured.Descriptor = null;
                captured.ResetToDefault = false;
            }

            captured.RuntimeOwned = IsRuntimeOwnedPropertyValue(
                instance,
                property.Name,
                value);

            if (captured.RuntimeOwned)
            {
                captured.BaselineOwnershipKey =
                    "__StyleBaseline:" + key + ":" +
                    property.DeclaringType.FullName + "." + property.Name;

                ReplaceOwnedPropertyValue(
                    instance,
                    captured.BaselineOwnershipKey,
                    value as IDisposable);
            }

            values.Add(captured);
        }

        private static PropertyInfo GetControlPaddingProperty()
        {
            return typeof(Control).GetProperty(
                "Padding",
                BindingFlags.Instance | BindingFlags.Public);
        }

        private void CaptureStyleSizeAxis(
            object instance,
            ArrayList values,
            Type ownerType,
            string propertyName,
            int axis)
        {
            if (instance == null ||
                ownerType == null ||
                !ownerType.IsInstanceOfType(instance))
            {
                return;
            }

            PropertyInfo property = ownerType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            if (property == null ||
                !property.CanRead ||
                !property.CanWrite ||
                property.PropertyType != typeof(Size) ||
                property.GetIndexParameters().Length != 0)
            {
                return;
            }

            Size size;

            try
            {
                size = (Size)property.GetValue(instance, null);
            }
            catch
            {
                return;
            }

            StylePropertyValue captured = new StylePropertyValue();
            captured.Property = property;
            captured.Value = axis == 1 ? size.Width : size.Height;
            captured.SizeAxis = axis;
            values.Add(captured);
        }

        private static void CaptureStyleFontPart(
            object instance,
            ArrayList values,
            string key)
        {
            Font font = GetObjectFont(instance);

            if (font == null)
                return;

            StylePropertyValue captured = new StylePropertyValue();

            if (EqualsIgnoreCase(key, "FontFamily"))
            {
                captured.FontPart = 1;
                captured.Value = font.FontFamily.Name;
            }
            else if (EqualsIgnoreCase(key, "FontSize"))
            {
                captured.FontPart = 2;
                captured.Value = font.SizeInPoints;
            }
            else if (EqualsIgnoreCase(key, "FontWeight"))
            {
                captured.FontPart = 3;
                captured.Value = font.Style;
            }
            else if (EqualsIgnoreCase(key, "FontStyle"))
            {
                captured.FontPart = 4;
                captured.Value = font.Style;
            }
            else if (EqualsIgnoreCase(key, "TextDecorations"))
            {
                captured.FontPart = 5;
                captured.Value = font.Style;
            }
            else
            {
                return;
            }

            PropertyInfo fontProperty = GetObjectFontProperty(instance);
            PropertyInfo resolvedFontProperty = instance == null
                ? null
                : FindProperty(instance.GetType(), "Font");

            try
            {
                if (Object.Equals(fontProperty, resolvedFontProperty))
                {
                    captured.Descriptor = TypeDescriptor
                        .GetProperties(instance)
                        .Find("Font", true);
                }

                captured.ResetToDefault =
                    captured.Descriptor != null &&
                    !captured.Descriptor.ShouldSerializeValue(instance);
            }
            catch
            {
                captured.Descriptor = null;
                captured.ResetToDefault = false;
            }

            values.Add(captured);
        }

        private static void CaptureStyleField(
            object instance,
            ArrayList values,
            string fieldName)
        {
            Type type = instance.GetType();
            FieldInfo field = null;

            while (type != null && field == null)
            {
                field = type.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.IgnoreCase |
                    BindingFlags.DeclaredOnly);

                type = type.BaseType;
            }

            CaptureStyleField(
                instance,
                values,
                field);
        }

        private static void CaptureStyleField(
            object instance,
            ArrayList values,
            FieldInfo field)
        {
            if (field == null || field.IsInitOnly || field.IsLiteral)
                return;

            int i;
            for (i = 0; i < values.Count; i++)
            {
                StylePropertyValue existing =
                    values[i] as StylePropertyValue;

                if (existing != null &&
                    Object.Equals(existing.Field, field))
                {
                    return;
                }
            }

            object value;

            try
            {
                value = field.GetValue(instance);
            }
            catch
            {
                return;
            }

            StylePropertyValue captured = new StylePropertyValue();
            captured.Field = field;
            captured.Value = value;
            values.Add(captured);
        }

        private bool IsRuntimeOwnedPropertyValue(
            object target,
            string propertyName,
            object value)
        {
            if (_ownedPropertyValues == null || value == null)
                return false;

            OwnedPropertyValue owned =
                FindOwnedPropertyValue(target, propertyName);

            return owned != null &&
                Object.ReferenceEquals(owned.Value, value);
        }

        private void RestoreStyleProperties(
            object instance,
            ArrayList values,
            StyleMetadataState activeMetadata,
            StyleMetadataState baselineMetadata)
        {
            StylePropertyValue visualStyleBackground = null;
            int i;

            for (i = values.Count - 1; i >= 0; i--)
            {
                StylePropertyValue captured =
                    values[i] as StylePropertyValue;

                if (captured == null)
                    continue;

                if (captured.Property != null &&
                    EqualsIgnoreCase(
                        captured.Property.Name,
                        "UseVisualStyleBackColor"))
                {
                    visualStyleBackground = captured;
                    continue;
                }

                RestoreStyleProperty(
                    instance,
                    captured,
                    activeMetadata,
                    baselineMetadata);
            }

            // ButtonBase.BackColor setters turn native visual-style painting off.
            // Restore this flag after BackColor, regardless of which XAML alias
            // caused the two native properties to be captured.
            if (visualStyleBackground != null)
            {
                RestoreStyleProperty(
                    instance,
                    visualStyleBackground,
                    activeMetadata,
                    baselineMetadata);
            }
        }

        private void RestoreStyleProperty(
            object instance,
            StylePropertyValue captured,
            StyleMetadataState activeMetadata,
            StyleMetadataState baselineMetadata)
        {
            if (captured.FontPart != 0)
            {
                RestoreStyleFontPart(instance, captured);
                return;
            }

            if (captured.Property == null && captured.Field == null)
                return;

            if (captured.Field != null)
            {
                captured.Field.SetValue(instance, captured.Value);
                return;
            }

            PropertyInfo property = captured.Property;
            object activeValue;
            bool activeValueKnown = TryReadPropertyValue(
                instance,
                property,
                out activeValue);
            object baselineValue = captured.Value;

            if (captured.SizeAxis != 0)
            {
                Size current = activeValueKnown && activeValue is Size
                    ? (Size)activeValue
                    : (Size)property.GetValue(instance, null);
                int saved = (int)captured.Value;

                baselineValue = captured.SizeAxis == 1
                    ? new Size(saved, current.Height)
                    : new Size(current.Width, saved);
            }

            try
            {
                if (captured.ResetToDefault &&
                    captured.Descriptor != null)
                {
                    captured.Descriptor.ResetValue(instance);
                }
                else
                {
                    property.SetValue(
                        instance,
                        baselineValue,
                        null);
                }
            }
            catch (Exception ex)
            {
                object actualValue;
                bool actualValueKnown;

                ReconcileFailedPropertyAssignment(
                    instance,
                    property,
                    activeValue,
                    activeValueKnown,
                    baselineValue,
                    activeMetadata,
                    out actualValue,
                    out actualValueKnown);

                bool baselineInstalled =
                    actualValueKnown &&
                    PropertyValuesMatch(actualValue, baselineValue) &&
                    (!activeValueKnown ||
                     !PropertyValuesMatch(actualValue, activeValue));

                if (baselineInstalled)
                {
                    RestoreStyleMetadata(
                        instance,
                        GetInfo(instance),
                        baselineMetadata);
                    CompleteStylePropertyOwnership(
                        instance,
                        captured,
                        baselineValue);
                }

                throw new StyleRestoreFailure(
                    ex,
                    baselineInstalled);
            }

            CompleteStylePropertyOwnership(
                instance,
                captured,
                baselineValue);
        }

        private void CompleteStylePropertyOwnership(
            object instance,
            StylePropertyValue captured,
            object restoredValue)
        {
            if (captured == null || captured.Property == null)
                return;

            if (captured.RuntimeOwned)
            {
                ReplaceOwnedPropertyValue(
                    instance,
                    captured.Property.Name,
                    restoredValue as IDisposable);

                // Capture temporarily owns a second reference so applying the
                // style cannot dispose its baseline. Once the real property owns
                // that baseline again, retire the temporary reference.
                ReleaseOwnedPropertyValue(
                    instance,
                    captured.BaselineOwnershipKey,
                    null);
            }
            else
            {
                ReleaseOwnedPropertyValue(
                    instance,
                    captured.Property.Name,
                    restoredValue);
            }
        }

        private void RestoreStyleFontPart(
            object instance,
            StylePropertyValue captured)
        {
            string partName = GetStyleFontPartName(
                captured.FontPart);

            // Local XAML values were applied after the initial style. Preserve
            // the current local axis instead of restoring the lower baseline.
            if (HasLocalValue(instance, partName))
                return;

            Font activeFont = GetObjectFont(instance);

            // The first font-axis setter may have displaced an ambient parent
            // font. It is restored last (style slots unwind in reverse order),
            // so reset the native Font property instead of pinning an equal-looking
            // concrete Font that would stop following future parent changes.
            if (captured.ResetToDefault &&
                captured.Descriptor != null &&
                !HasAnyLocalFontValue(instance))
            {
                try
                {
                    captured.Descriptor.ResetValue(instance);
                }
                catch (Exception ex)
                {
                    bool baselineInstalled =
                        !TryRestoreActiveStyleFont(
                            instance,
                            activeFont);

                    throw new StyleRestoreFailure(
                        ex,
                        baselineInstalled);
                }

                return;
            }

            Font current = activeFont;

            if (current == null)
                return;

            string family = current.FontFamily.Name;
            float size = current.SizeInPoints;
            FontStyle style = current.Style;

            if (captured.FontPart == 1)
            {
                family = captured.Value as string;
            }
            else if (captured.FontPart == 2)
            {
                size = (float)captured.Value;
            }
            else if (captured.FontPart == 3)
            {
                style = CopyFontStyleBit(
                    style,
                    (FontStyle)captured.Value,
                    FontStyle.Bold);
            }
            else if (captured.FontPart == 4)
            {
                style = CopyFontStyleBit(
                    style,
                    (FontStyle)captured.Value,
                    FontStyle.Italic);
            }
            else if (captured.FontPart == 5)
            {
                style = CopyFontStyleBit(
                    style,
                    (FontStyle)captured.Value,
                    FontStyle.Underline);
                style = CopyFontStyleBit(
                    style,
                    (FontStyle)captured.Value,
                    FontStyle.Strikeout);
            }

            try
            {
                SetObjectFont(
                    instance,
                    family,
                    size,
                    style);
            }
            catch (Exception ex)
            {
                bool baselineInstalled =
                    !TryRestoreActiveStyleFont(
                        instance,
                        activeFont);

                throw new StyleRestoreFailure(
                    ex,
                    baselineInstalled);
            }
        }

        private bool TryRestoreActiveStyleFont(
            object instance,
            Font activeFont)
        {
            PropertyInfo property = GetObjectFontProperty(instance);

            if (property != null &&
                property.CanWrite &&
                activeFont != null)
            {
                try
                {
                    property.SetValue(
                        instance,
                        activeFont,
                        null);
                }
                catch
                {
                }
            }

            Font actualFont = GetObjectFont(instance);
            bool restored = PropertyValuesMatch(
                actualFont,
                activeFont);

            if (!restored)
            {
                ReleaseOwnedPropertyValue(
                    instance,
                    "Font",
                    actualFont);
            }

            return restored;
        }

        private static string GetStyleFontPartName(int fontPart)
        {
            if (fontPart == 1)
                return "FontFamily";

            if (fontPart == 2)
                return "FontSize";

            if (fontPart == 3)
                return "FontWeight";

            if (fontPart == 4)
                return "FontStyle";

            if (fontPart == 5)
                return "TextDecorations";

            return "Font";
        }

        private bool HasAnyLocalFontValue(object instance)
        {
            ElementInfo info = GetInfo(instance);
            ArrayList properties = info.LocalValueProperties;

            if (properties == null)
                return false;

            int i;

            for (i = 0; i < properties.Count; i++)
            {
                if (IsFontStylePropertyKey(
                    properties[i] as string))
                {
                    return true;
                }
            }

            return false;
        }

        private StyleMetadataState CaptureStyleMetadata(
            object instance,
            ElementInfo info,
            string key)
        {
            StyleMetadataState state = new StyleMetadataState();

            if (EqualsIgnoreCase(key, "Width"))
            {
                state.Width = true;
                state.WidthExplicit = info.WidthExplicit;
            }
            else if (EqualsIgnoreCase(key, "Height"))
            {
                state.Height = true;
                state.HeightExplicit = info.HeightExplicit;
            }
            else if (EqualsIgnoreCase(key, "Size"))
            {
                state.Width = true;
                state.WidthExplicit = info.WidthExplicit;
                state.Height = true;
                state.HeightExplicit = info.HeightExplicit;
            }
            else if (EqualsIgnoreCase(key, "Margin"))
            {
                state.Margin = true;
                state.MarginValue = info.Margin;
            }
            else if (EqualsIgnoreCase(key, "HorizontalAlignment"))
            {
                state.HorizontalAlignment = true;
                state.HorizontalAlignmentValue = info.HorizontalAlignment;
            }
            else if (EqualsIgnoreCase(key, "VerticalAlignment"))
            {
                state.VerticalAlignment = true;
                state.VerticalAlignmentValue = info.VerticalAlignment;
            }
            else if (EqualsIgnoreCase(key, "FlexGrow"))
            {
                state.FlexGrow = true;
                state.FlexGrowValue = info.FlexGrow;
            }
            else if (EqualsIgnoreCase(key, "FlowDirection"))
            {
                state.FlowDirection = true;
                state.FlowDirectionExplicit = info.FlowDirectionExplicit;

                ItemsControl items = instance as ItemsControl;
                if (items != null)
                {
                    state.ContentRightToLeft = true;
                    state.ContentRightToLeftValue =
                        items.ContentRightToLeft;
                }
            }
            else if (EqualsIgnoreCase(key, "Foreground"))
            {
                state.Foreground = true;
                state.ForegroundExplicit = info.ForegroundExplicit;
                state.ForegroundSet = info.ForegroundSet;
            }
            else if (EqualsIgnoreCase(key, "Background"))
            {
                state.Background = true;
                state.BackgroundExplicit = info.BackgroundExplicit;
                state.BackgroundSet = info.BackgroundSet;
            }
            else if (EqualsIgnoreCase(key, "Font"))
            {
                state.FontFamily = true;
                state.FontFamilyExplicit = info.FontFamilyExplicit;
                state.FontFamilySet = info.FontFamilySet;
                state.FontSize = true;
                state.FontSizeExplicit = info.FontSizeExplicit;
                state.FontSizeSet = info.FontSizeSet;
                state.FontWeight = true;
                state.FontWeightExplicit = info.FontWeightExplicit;
                state.FontWeightSet = info.FontWeightSet;
                state.FontStyle = true;
                state.FontStyleExplicit = info.FontStyleExplicit;
                state.FontStyleSet = info.FontStyleSet;
                state.TextDecorations = true;
                state.TextDecorationsExplicit =
                    info.TextDecorationsExplicit;
                state.TextDecorationsSet = info.TextDecorationsSet;
            }
            else if (EqualsIgnoreCase(key, "FontFamily"))
            {
                state.FontFamily = true;
                state.FontFamilyExplicit = info.FontFamilyExplicit;
                state.FontFamilySet = info.FontFamilySet;
            }
            else if (EqualsIgnoreCase(key, "FontSize"))
            {
                state.FontSize = true;
                state.FontSizeExplicit = info.FontSizeExplicit;
                state.FontSizeSet = info.FontSizeSet;
            }
            else if (EqualsIgnoreCase(key, "FontWeight"))
            {
                state.FontWeight = true;
                state.FontWeightExplicit = info.FontWeightExplicit;
                state.FontWeightSet = info.FontWeightSet;
            }
            else if (EqualsIgnoreCase(key, "FontStyle"))
            {
                state.FontStyle = true;
                state.FontStyleExplicit = info.FontStyleExplicit;
                state.FontStyleSet = info.FontStyleSet;
            }
            else if (EqualsIgnoreCase(key, "TextDecorations"))
            {
                state.TextDecorations = true;
                state.TextDecorationsExplicit =
                    info.TextDecorationsExplicit;
                state.TextDecorationsSet = info.TextDecorationsSet;
            }
            else if (EqualsIgnoreCase(key, "Visibility"))
            {
                state.Visibility = true;
                state.Hidden = info.Hidden;
                state.Collapsed = info.VisibilityCollapsed;
            }
            else if (EqualsIgnoreCase(key, "ToolTip"))
            {
                Control control = instance as Control;

                if (control != null)
                {
                    state.ToolTip = true;
                    state.ToolTipValue = _toolTip == null
                        ? null
                        : _toolTip.GetToolTip(control);
                }
            }

            return state;
        }

        private void RestoreStyleMetadata(
            object instance,
            ElementInfo info,
            StyleMetadataState state)
        {
            if (state.Width)
                info.WidthExplicit = state.WidthExplicit;

            if (state.Height)
                info.HeightExplicit = state.HeightExplicit;

            if (state.Margin)
                info.Margin = state.MarginValue;

            if (state.HorizontalAlignment)
            {
                info.HorizontalAlignment =
                    state.HorizontalAlignmentValue;
            }

            if (state.VerticalAlignment)
            {
                info.VerticalAlignment =
                    state.VerticalAlignmentValue;
            }

            if (state.FlexGrow)
                info.FlexGrow = state.FlexGrowValue;

            if (state.FlowDirection)
            {
                info.FlowDirectionExplicit =
                    state.FlowDirectionExplicit;
            }

            if (state.ContentRightToLeft)
            {
                ItemsControl items = instance as ItemsControl;
                if (items != null)
                {
                    items.ContentRightToLeft =
                        state.ContentRightToLeftValue;
                }
            }

            if (state.Foreground)
            {
                info.ForegroundExplicit = state.ForegroundExplicit;
                info.ForegroundSet = state.ForegroundSet;
            }

            if (state.Background)
            {
                info.BackgroundExplicit = state.BackgroundExplicit;
                info.BackgroundSet = state.BackgroundSet;
            }

            if (state.FontFamily)
            {
                info.FontFamilyExplicit = state.FontFamilyExplicit;
                info.FontFamilySet = state.FontFamilySet;
            }

            if (state.FontSize)
            {
                info.FontSizeExplicit = state.FontSizeExplicit;
                info.FontSizeSet = state.FontSizeSet;
            }

            if (state.FontWeight)
            {
                info.FontWeightExplicit = state.FontWeightExplicit;
                info.FontWeightSet = state.FontWeightSet;
            }

            if (state.FontStyle)
            {
                info.FontStyleExplicit = state.FontStyleExplicit;
                info.FontStyleSet = state.FontStyleSet;
            }

            if (state.TextDecorations)
            {
                info.TextDecorationsExplicit =
                    state.TextDecorationsExplicit;
                info.TextDecorationsSet = state.TextDecorationsSet;
            }

            if (state.Visibility)
            {
                SetElementVisibilityState(
                    info,
                    state.Hidden,
                    state.Collapsed);

                if (info.ConditionStates != null &&
                    info.ConditionStates.Count != 0)
                {
                    ApplyElementEffectiveVisibility(instance, info);
                }
            }

            if (state.ToolTip && _toolTip != null)
            {
                Control control = instance as Control;
                if (control != null)
                {
                    _toolTip.SetToolTip(
                        control,
                        state.ToolTipValue);
                }
            }
        }

        private void ReleaseStyleBoundEvents(object target)
        {
            if (target == null)
                return;

            ArrayList targetRegistrations =
                GetBoundEventTargetRegistrations(target);

            if (targetRegistrations == null)
                return;

            ArrayList registrations = new ArrayList();
            int i;

            for (i = 0; i < targetRegistrations.Count; i++)
            {
                BoundEventRegistration registration =
                    targetRegistrations[i] as BoundEventRegistration;

                if (registration == null ||
                    !registration.StyleOwner ||
                    !Object.ReferenceEquals(registration.Target, target))
                {
                    continue;
                }

                registrations.Add(registration);
            }

            for (i = registrations.Count - 1; i >= 0; i--)
            {
                BoundEventRegistration registration =
                    registrations[i] as BoundEventRegistration;

                if (!IsBoundEventTracked(registration))
                    continue;

                registration.StyleOwner = false;

                if (registration.LocalOwner)
                    continue;

                DetachBoundEventRegistration(registration, true);
            }
        }
    }
}
