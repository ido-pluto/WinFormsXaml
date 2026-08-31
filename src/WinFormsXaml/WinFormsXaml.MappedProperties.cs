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
        // WPF PROPERTIES
        // ============================================================

        private static readonly Hashtable _mappedXamlPropertyNames =
            CreateMappedXamlPropertyNames();

        private static Hashtable CreateMappedXamlPropertyNames()
        {
            string[] names = new string[]
            {
                "UseApplicationIcon",
                "FlexGrow",
                "Width",
                "Height",
                "Margin",
                "Padding",
                "HorizontalAlignment",
                "VerticalAlignment",
                "FlowDirection",
                "RightToLeft",
                "Title",
                "Content",
                "Header",
                "Foreground",
                "ForeColor",
                "Background",
                "BackColor",
                "FontFamily",
                "FontSize",
                "FontWeight",
                "FontStyle",
                "TextDecorations",
                "TextAlignment",
                "IsEnabled",
                "IsTabStop",
                "IsChecked",
                "IsReadOnly",
                "Visibility",
                "Visible",
                "MinWidth",
                "MinHeight",
                "MaxWidth",
                "MaxHeight",
                "TextWrapping",
                "AcceptsReturn",
                "AcceptsTab",
                "Source",
                "Stretch",
                "Orientation",
                "Spacing",
                "LastChildFill",
                "ToolTip",
                "BorderThickness",
                "BorderBrush",
                "VerticalScrollBarVisibility",
                "HorizontalScrollBarVisibility"
            };
            Hashtable result = new Hashtable(
                StringComparer.OrdinalIgnoreCase);
            int i;

            for (i = 0; i < names.Length; i++)
                result.Add(names[i], names[i]);

            return result;
        }

        private static bool IsMappedXamlPropertyName(string name)
        {
            return !String.IsNullOrEmpty(name) &&
                _mappedXamlPropertyNames.ContainsKey(name);
        }

        private bool TryApplyWpfProperty(
            object instance,
            string name,
            string value)
        {
            // Most markup uses native WinForms properties. Reject those in one
            // lookup instead of walking every alias branch for every attribute.
            if (!IsMappedXamlPropertyName(name))
                return false;

            ElementInfo info =
                GetInfo(instance);

            Control control =
                instance as Control;

            if (EqualsIgnoreCase(
                name,
                "UseApplicationIcon"))
            {
                Form form = instance as Form;

                if (form == null)
                    return false;

                ElementInfo formInfo = GetInfo(form);
                FormIconState state = formInfo.FormIcon;

                if (state == null)
                    return false;

                state.UseApplicationIcon = ParseBoolean(value);

                if (state.ConfigurationReady &&
                    !formInfo.StyleTransitionActive)
                {
                    ReconcileApplicationIconDefault(form);
                }

                return true;
            }

            // FlexGrow is layout metadata rather than a property of the child
            // WinForms control. Therefore it is valid on every Control type.
            // It only has an effect when the direct parent is a FlexPanel.
            if (EqualsIgnoreCase(
                name,
                "FlexGrow"))
            {
                if (control == null)
                    return false;

                info.FlexGrow =
                    Math.Max(
                        0.0f,
                        ParseFloat(value));

                return true;
            }

            if (EqualsIgnoreCase(
                name,
                "Width"))
            {
                if (EqualsIgnoreCase(
                    value,
                    "Auto"))
                {
                    info.WidthExplicit =
                        false;
                    InvalidateFlexWidthBasis(info);

                    return true;
                }

                if (control != null)
                {
                    int width = ParsePixel(value);
                    PropertyInfo widthProperty = FindProperty(
                        control.GetType(),
                        "Width");

                    SetPropertyObjectValue(
                        control,
                        widthProperty,
                        width);

                    InvalidateFlexWidthBasis(info);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "Height"))
            {
                if (EqualsIgnoreCase(
                    value,
                    "Auto"))
                {
                    info.HeightExplicit =
                        false;
                    InvalidateFlexHeightBasis(info);

                    return true;
                }

                if (control != null)
                {
                    int height = ParsePixel(value);
                    PropertyInfo heightProperty = FindProperty(
                        control.GetType(),
                        "Height");

                    SetPropertyObjectValue(
                        control,
                        heightProperty,
                        height);

                    InvalidateFlexHeightBasis(info);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "Margin"))
            {
                if (control != null)
                {
                    Padding margin =
                        ParseThickness(
                            value);
                    PropertyInfo marginProperty = FindProperty(
                        control.GetType(),
                        "Margin");

                    SetPropertyObjectValue(
                        control,
                        marginProperty,
                        margin);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "Padding"))
            {
                if (control != null)
                {
                    control.Padding =
                        ParseThickness(
                            value);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "HorizontalAlignment"))
            {
                info.HorizontalAlignment =
                    ParseHorizontalAlignment(
                        value);

                return true;
            }

            if (EqualsIgnoreCase(
                name,
                "VerticalAlignment"))
            {
                info.VerticalAlignment =
                    ParseVerticalAlignment(
                        value);

                return true;
            }

            // ========================================================
            // RTL / LTR
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "FlowDirection"))
            {
                if (control == null)
                    return false;

                bool rtl;

                if (EqualsIgnoreCase(
                    value,
                    "RightToLeft"))
                {
                    rtl =
                        true;
                }
                else if (EqualsIgnoreCase(
                    value,
                    "LeftToRight"))
                {
                    rtl =
                        false;
                }
                else
                {
                    throw new InvalidOperationException(
                        "FlowDirection must be RightToLeft or LeftToRight.");
                }

                info.FlowDirectionExplicit =
                    true;

                SetFlowDirection(
                    control,
                    rtl);

                return true;
            }

            if (EqualsIgnoreCase(
                name,
                "RightToLeft"))
            {
                if (control != null)
                {
                    RightToLeft direction =
                        (RightToLeft)Enum.Parse(
                            typeof(RightToLeft),
                            value,
                            true);

                    info.FlowDirectionExplicit =
                        true;

                    SetFlowDirection(
                        control,
                        direction ==
                            RightToLeft.Yes);

                    return true;
                }
            }

            // ========================================================
            // CONTENT
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "Title") &&
                !HasWritableProperty(instance, name))
            {
                return TrySetProperty(
                    instance,
                    "Text",
                    value);
            }

            if (EqualsIgnoreCase(
                name,
                "Content") &&
                !HasWritableProperty(instance, name))
            {
                return TrySetProperty(
                    instance,
                    "Text",
                    value);
            }

            if (EqualsIgnoreCase(
                name,
                "Header") &&
                !HasWritableProperty(instance, name))
            {
                return TrySetProperty(
                    instance,
                    "Text",
                    value);
            }

            // ========================================================
            // COLORS
            // ========================================================

            if (EqualsIgnoreCase(
                    name,
                    "Foreground") ||
                EqualsIgnoreCase(
                    name,
                    "ForeColor"))
            {
                Color color =
                    ParseColor(
                        value);

                if (control != null)
                {
                    PropertyInfo foregroundProperty = FindProperty(
                        control.GetType(),
                        "ForeColor");

                    SetPropertyObjectValue(
                        control,
                        foregroundProperty,
                        color);

                    return true;
                }

                return TrySetObjectColorProperty(
                    instance,
                    "ForeColor",
                    color);
            }

            if (EqualsIgnoreCase(
                    name,
                    "Background") ||
                EqualsIgnoreCase(
                    name,
                    "BackColor"))
            {
                Color color =
                    ParseColor(
                        value);

                if (control != null)
                {
                    // A top-level WinForms Form cannot accept a transparent
                    // BackColor. Treat transparent XAML as an explicit request
                    // to remove the framework color and restore the native
                    // form default instead of invoking the rejecting setter.
                    if (control is Form && color.A == 0)
                    {
                        ResetMappedControlBackground(control);
                        return true;
                    }

                    PropertyInfo backgroundProperty = FindProperty(
                        control.GetType(),
                        "BackColor");

                    SetPropertyObjectValue(
                        control,
                        backgroundProperty,
                        color);
                    DisableVisualStyleBackground(control);

                    return true;
                }

                return TrySetObjectColorProperty(
                    instance,
                    "BackColor",
                    color);
            }

            // ========================================================
            // FONT
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "FontFamily"))
            {
                Font current =
                    GetObjectFont(
                        instance);

                if (current != null)
                {
                    SetObjectFontWithMetadata(
                        instance,
                        "FontFamily",
                        value,
                        value,
                        current.SizeInPoints,
                        current.Style);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "FontSize"))
            {
                Font current =
                    GetObjectFont(
                        instance);

                if (current != null)
                {
                    // WPF FontSize is DIP; WinForms is points.
                    float points =
                        ParseFloat(value) *
                        0.75f;

                    if (points < 1.0f)
                        points = 1.0f;

                    SetObjectFontWithMetadata(
                        instance,
                        "FontSize",
                        points,
                        current.FontFamily.Name,
                        points,
                        current.Style);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "FontWeight"))
            {
                Font current =
                    GetObjectFont(
                        instance);

                if (current != null)
                {
                    FontStyle style =
                        current.Style;

                    bool bold =
                        EqualsIgnoreCase(
                            value,
                            "Bold") ||
                        EqualsIgnoreCase(
                            value,
                            "SemiBold") ||
                        EqualsIgnoreCase(
                            value,
                            "DemiBold") ||
                        EqualsIgnoreCase(
                            value,
                            "ExtraBold") ||
                        EqualsIgnoreCase(
                            value,
                            "Black");

                    if (bold)
                    {
                        style |=
                            FontStyle.Bold;
                    }
                    else
                    {
                        style &=
                            ~FontStyle.Bold;
                    }

                    SetObjectFontWithMetadata(
                        instance,
                        "FontWeight",
                        value,
                        current.FontFamily.Name,
                        current.SizeInPoints,
                        style);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "FontStyle"))
            {
                Font current =
                    GetObjectFont(
                        instance);

                if (current != null)
                {
                    FontStyle style =
                        current.Style;

                    bool italic =
                        EqualsIgnoreCase(
                            value,
                            "Italic") ||
                        EqualsIgnoreCase(
                            value,
                            "Oblique");

                    if (italic)
                    {
                        style |=
                            FontStyle.Italic;
                    }
                    else
                    {
                        style &=
                            ~FontStyle.Italic;
                    }

                    SetObjectFontWithMetadata(
                        instance,
                        "FontStyle",
                        value,
                        current.FontFamily.Name,
                        current.SizeInPoints,
                        style);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "TextDecorations"))
            {
                Font current =
                    GetObjectFont(
                        instance);

                if (current != null)
                {
                    FontStyle style =
                        current.Style;

                    style &=
                        ~FontStyle.Underline;

                    style &=
                        ~FontStyle.Strikeout;

                    if (value.IndexOf(
                            "Underline",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        style |=
                            FontStyle.Underline;
                    }

                    if (value.IndexOf(
                            "Strike",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        style |=
                            FontStyle.Strikeout;
                    }

                    SetObjectFontWithMetadata(
                        instance,
                        "TextDecorations",
                        value,
                        current.FontFamily.Name,
                        current.SizeInPoints,
                        style);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "TextAlignment"))
            {
                if (ApplyTextAlignment(
                    instance,
                    value))
                {
                    return true;
                }
            }

            // ========================================================
            // STATE
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "IsEnabled"))
            {
                if (control != null)
                {
                    control.Enabled =
                        ParseBoolean(
                            value);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "IsTabStop"))
            {
                if (control != null)
                {
                    control.TabStop =
                        ParseBoolean(
                            value);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "IsChecked"))
            {
                CheckBox check =
                    instance as CheckBox;

                if (check != null)
                {
                    check.Checked =
                        ParseBoolean(
                            value);

                    return true;
                }

                RadioButton radio =
                    instance as RadioButton;

                if (radio != null)
                {
                    radio.Checked =
                        ParseBoolean(
                            value);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "IsReadOnly"))
            {
                TextBoxBase text =
                    instance as TextBoxBase;

                if (text != null)
                {
                    text.ReadOnly =
                        ParseBoolean(
                            value);

                    return true;
                }
            }

            // ========================================================
            // VISIBILITY
            // ========================================================

            if (EqualsIgnoreCase(
                    name,
                    "Visibility") ||
                EqualsIgnoreCase(
                    name,
                    "Visible"))
            {
                if (control == null)
                    return false;

                bool hidden;
                bool collapsed;

                if (EqualsIgnoreCase(
                    value,
                    "Visible"))
                {
                    hidden = false;
                    collapsed = false;
                }
                else if (EqualsIgnoreCase(
                    value,
                    "Hidden"))
                {
                    hidden = true;
                    collapsed = false;
                }
                else if (EqualsIgnoreCase(
                    value,
                    "Collapsed"))
                {
                    hidden = false;
                    collapsed = true;
                }
                else
                {
                    hidden = !ParseBoolean(value);
                    collapsed = false;
                }

                SetControlVisibility(
                    control,
                    hidden,
                    collapsed);

                return true;
            }

            // ========================================================
            // MIN/MAX
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "MinWidth"))
            {
                if (control != null)
                {
                    control.MinimumSize =
                        new Size(
                            ParsePixel(
                                value),
                            control.MinimumSize.Height);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "MinHeight"))
            {
                if (control != null)
                {
                    control.MinimumSize =
                        new Size(
                            control.MinimumSize.Width,
                            ParsePixel(
                                value));

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "MaxWidth"))
            {
                if (control != null)
                {
                    control.MaximumSize =
                        new Size(
                            ParsePixel(
                                value),
                            control.MaximumSize.Height);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "MaxHeight"))
            {
                if (control != null)
                {
                    control.MaximumSize =
                        new Size(
                            control.MaximumSize.Width,
                            ParsePixel(
                                value));

                    return true;
                }
            }

            // ========================================================
            // TEXT
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "TextWrapping"))
            {
                TextBox text =
                    instance as TextBox;

                if (text != null)
                {
                    bool wrap =
                        !EqualsIgnoreCase(
                            value,
                            "NoWrap");

                    text.WordWrap =
                        wrap;

                    if (wrap)
                    {
                        text.Multiline =
                            true;
                    }

                    return true;
                }

                if (instance is Label)
                    return true;
            }

            if (EqualsIgnoreCase(
                name,
                "AcceptsReturn"))
            {
                TextBox text =
                    instance as TextBox;

                if (text != null)
                {
                    bool enabled =
                        ParseBoolean(
                            value);

                    text.Multiline =
                        enabled;

                    text.AcceptsReturn =
                        enabled;

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "AcceptsTab"))
            {
                TextBox text =
                    instance as TextBox;

                if (text != null)
                {
                    text.AcceptsTab =
                        ParseBoolean(
                            value);

                    return true;
                }
            }

            // ========================================================
            // IMAGE
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "Source"))
            {
                PictureBox image =
                    instance as PictureBox;

                if (image != null)
                {
                    SetMappedPictureBoxSource(
                        image,
                        null,
                        ResolvePath(value),
                        true,
                        false);

                    return true;
                }

                WebBrowser browser =
                    instance as WebBrowser;

                if (browser != null)
                {
                    browser.Url =
                        new Uri(
                            value,
                            UriKind.RelativeOrAbsolute);

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "Stretch"))
            {
                PictureBox image =
                    instance as PictureBox;

                if (image != null)
                {
                    ImageControl imageControl =
                        instance as ImageControl;

                    if (imageControl != null)
                    {
                        ImageStretch stretch;

                        if (EqualsIgnoreCase(value, "None"))
                            stretch = ImageStretch.None;
                        else if (EqualsIgnoreCase(value, "Fill"))
                            stretch = ImageStretch.Fill;
                        else if (EqualsIgnoreCase(value, "Uniform"))
                            stretch = ImageStretch.Uniform;
                        else if (EqualsIgnoreCase(value, "UniformToFill"))
                            stretch = ImageStretch.UniformToFill;
                        else
                            throw new InvalidOperationException(
                                "Image.Stretch must be None, Fill, Uniform, " +
                                "or UniformToFill.");

                        imageControl.Stretch = stretch;

                        return true;
                    }

                    if (EqualsIgnoreCase(
                        value,
                        "Fill"))
                    {
                        image.SizeMode =
                            PictureBoxSizeMode.StretchImage;
                    }
                    else if (
                        EqualsIgnoreCase(
                            value,
                            "Uniform") ||
                        EqualsIgnoreCase(
                            value,
                            "UniformToFill"))
                    {
                        image.SizeMode =
                            PictureBoxSizeMode.Zoom;
                    }
                    else
                    {
                        image.SizeMode =
                            PictureBoxSizeMode.Normal;
                    }

                    return true;
                }
            }

            // ========================================================
            // ORIENTATION
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "Orientation"))
            {
                StackHost stack =
                    instance as StackHost;

                if (stack != null)
                {
                    stack.StackOrientation =
                        EqualsIgnoreCase(
                            value,
                            "Horizontal")
                            ? Orientation.Horizontal
                            : Orientation.Vertical;

                    return true;
                }

                FlexPanel flex =
                    instance as FlexPanel;

                if (flex != null)
                {
                    flex.Direction =
                        EqualsIgnoreCase(
                            value,
                            "Vertical")
                            ? FlexDirection.Column
                            : FlexDirection.Row;

                    return true;
                }

                TrackBar track =
                    instance as TrackBar;

                if (track != null)
                {
                    track.Orientation =
                        EqualsIgnoreCase(
                            value,
                            "Vertical")
                            ? Orientation.Vertical
                            : Orientation.Horizontal;

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "Spacing"))
            {
                StackHost stack =
                    instance as StackHost;

                if (stack != null)
                {
                    stack.StackSpacing =
                        Math.Max(
                            0,
                            ParseInt(value));

                    stack.PerformLayout();

                    return true;
                }
            }

            // ========================================================
            // DOCK PANEL
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "LastChildFill"))
            {
                DockHost dock =
                    instance as DockHost;

                if (dock != null)
                {
                    dock.LastChildFill =
                        ParseBoolean(
                            value);

                    return true;
                }
            }

            // ========================================================
            // TOOLTIP
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "ToolTip"))
            {
                if (control != null)
                {
                    if (_toolTip == null)
                    {
                        _toolTip =
                            new ToolTip();
                    }

                    _toolTip.SetToolTip(
                        control,
                        value);

                    return true;
                }
            }

            // ========================================================
            // BORDER
            // ========================================================

            if (EqualsIgnoreCase(
                name,
                "BorderThickness"))
            {
                BorderHost border =
                    instance as BorderHost;

                if (border != null)
                {
                    border.BorderThickness =
                        ParseThickness(
                            value);

                    border.Invalidate();

                    return true;
                }
            }

            if (EqualsIgnoreCase(
                name,
                "BorderBrush"))
            {
                BorderHost border =
                    instance as BorderHost;

                if (border != null)
                {
                    border.BorderColor =
                        ParseColor(
                            value);

                    border.Invalidate();

                    return true;
                }
            }

            // ========================================================
            // SCROLL
            // ========================================================

            if (EqualsIgnoreCase(
                    name,
                    "VerticalScrollBarVisibility") ||
                EqualsIgnoreCase(
                    name,
                    "HorizontalScrollBarVisibility"))
            {
                ScrollHost scroll =
                    instance as ScrollHost;

                if (scroll != null)
                {
                    scroll.AutoScroll =
                        !EqualsIgnoreCase(
                            value,
                            "Disabled");

                    return true;
                }
            }

            return false;
        }

        private Image GetDecodedImageFromBytes(byte[] bytes)
        {
            if (bytes == null)
                return null;

            int i;
            ulong contentFingerprint = 0;
            bool contentFingerprintKnown = false;
            bool validateMutableContents =
                _reloadingDynamicBindings ||
                _decodedImageCacheForcedValidationDepth > 0;
            Image mruImage;

            if (TryGetDecodedImageFromMru(
                bytes,
                validateMutableContents,
                out mruImage))
            {
                return mruImage;
            }

            if (_decodedImageCache == null)
                _decodedImageCache = new ArrayList();

            // Identity cache: the common fast path is a model/cache returning the same
            // byte[] instance. Weak references avoid pinning every image ever scrolled past.
            for (i = _decodedImageCache.Count - 1; i >= 0; i--)
            {
                WeakDecodedImageCacheEntry entry =
                    _decodedImageCache[i] as WeakDecodedImageCacheEntry;

                if (entry == null || entry.Source == null || entry.Image == null)
                {
                    _decodedImageCache.RemoveAt(i);
                    continue;
                }

                object cachedSource = entry.Source.Target;
                Image cachedImage = entry.Image.Target as Image;

                if (cachedSource == null || cachedImage == null)
                {
                    _decodedImageCache.RemoveAt(i);
                    continue;
                }

                if (Object.ReferenceEquals(cachedSource, bytes))
                {
                    bool validateEntryContents =
                        validateMutableContents &&
                        entry.ContentValidationGeneration !=
                            _decodedImageCacheValidationGeneration;

                    if (validateEntryContents &&
                        !contentFingerprintKnown)
                    {
                        contentFingerprint =
                            ComputeByteImageFingerprint(bytes);
                        contentFingerprintKnown = true;
                    }

                    // byte[] is mutable. Preserve the identity-cache fast path,
                    // but do not serve a stale bitmap after application code
                    // edits the same array and explicitly reloads its binding.
                    if (validateEntryContents &&
                        entry.ContentFingerprint != contentFingerprint)
                    {
                        _decodedImageCache.RemoveAt(i);
                        continue;
                    }

                    if (validateEntryContents)
                    {
                        entry.ContentValidationGeneration =
                            _decodedImageCacheValidationGeneration;
                    }

                    try
                    {
                        // A failed item transaction may have disposed the decoded
                        // value before rolling its byte[] binding back. Do not hand
                        // a disposed GDI+ object back to the restored PictureBox.
                        if (cachedImage.Width >= 0)
                        {
                            PromoteDecodedImageMru(entry);
                            return cachedImage;
                        }
                    }
                    catch
                    {
                        _decodedImageCache.RemoveAt(i);
                    }
                }
            }

            Bitmap decoded;

            using (MemoryStream stream = new MemoryStream(bytes))
            using (Image temporary = Image.FromStream(stream))
            {
                decoded = new Bitmap(temporary);
            }

            WeakDecodedImageCacheEntry newEntry =
                new WeakDecodedImageCacheEntry();

            newEntry.Source = new WeakReference(bytes);
            newEntry.Image = new WeakReference(decoded);
            newEntry.ContentFingerprint = contentFingerprintKnown
                ? contentFingerprint
                : ComputeByteImageFingerprint(bytes);
            newEntry.ContentValidationGeneration =
                (_reloadingDynamicBindings ||
                 _decodedImageCacheForcedValidationDepth > 0)
                    ? _decodedImageCacheValidationGeneration
                    : 0;
            _decodedImageCache.Add(newEntry);
            PromoteDecodedImageMru(newEntry);

            // Bound the identity lookup. PictureBox assignments are tracked
            // separately so a shared decoded image is disposed only after its
            // last runtime-owned assignment is replaced or the runtime ends.
            while (_decodedImageCache.Count > 128)
                _decodedImageCache.RemoveAt(0);

            return decoded;
        }

        private void BeginDecodedImageCacheContentValidation()
        {
            unchecked
            {
                _decodedImageCacheValidationGeneration++;
            }

            if (_decodedImageCacheValidationGeneration == 0)
                _decodedImageCacheValidationGeneration = 1;

            _decodedImageCacheForcedValidationDepth++;
        }

        private void EndDecodedImageCacheContentValidation()
        {
            if (_decodedImageCacheForcedValidationDepth > 0)
                _decodedImageCacheForcedValidationDepth--;
        }

        private Image GetDecodedImageFromIcon(Icon icon)
        {
            if (icon == null)
                return null;

            int i;
            Image mruImage;

            if (TryGetDecodedImageFromMru(
                icon,
                false,
                out mruImage))
            {
                return mruImage;
            }

            if (_decodedImageCache == null)
                _decodedImageCache = new ArrayList();

            // Icon.ToBitmap allocates a new GDI+ bitmap. Share that conversion
            // by source identity just like encoded byte[] values, while weak
            // keys keep application-owned Icon objects collectible.
            for (i = _decodedImageCache.Count - 1; i >= 0; i--)
            {
                WeakDecodedImageCacheEntry entry =
                    _decodedImageCache[i] as WeakDecodedImageCacheEntry;

                if (entry == null || entry.Source == null || entry.Image == null)
                {
                    _decodedImageCache.RemoveAt(i);
                    continue;
                }

                object cachedSource = entry.Source.Target;
                Image cachedImage = entry.Image.Target as Image;

                if (cachedSource == null || cachedImage == null)
                {
                    _decodedImageCache.RemoveAt(i);
                    continue;
                }

                if (!Object.ReferenceEquals(cachedSource, icon))
                    continue;

                try
                {
                    if (cachedImage.Width >= 0)
                    {
                        PromoteDecodedImageMru(entry);
                        return cachedImage;
                    }
                }
                catch
                {
                    _decodedImageCache.RemoveAt(i);
                }
            }

            Bitmap decoded = icon.ToBitmap();
            WeakDecodedImageCacheEntry newEntry =
                new WeakDecodedImageCacheEntry();

            newEntry.Source = new WeakReference(icon);
            newEntry.Image = new WeakReference(decoded);
            _decodedImageCache.Add(newEntry);
            PromoteDecodedImageMru(newEntry);

            while (_decodedImageCache.Count > 128)
                _decodedImageCache.RemoveAt(0);

            return decoded;
        }

        private bool TryGetDecodedImageFromMru(
            object source,
            bool validateMutableContents,
            out Image image)
        {
            image = null;

            if (_decodedImageMru == null)
                return false;

            int i;

            for (i = 0; i < _decodedImageMru.Length; i++)
            {
                WeakDecodedImageCacheEntry entry =
                    _decodedImageMru[i];

                if (entry == null || entry.Source == null ||
                    entry.Image == null)
                {
                    continue;
                }

                object cachedSource = entry.Source.Target;
                Image cachedImage = entry.Image.Target as Image;

                if (cachedSource == null || cachedImage == null)
                {
                    _decodedImageMru[i] = null;
                    continue;
                }

                if (!Object.ReferenceEquals(cachedSource, source))
                    continue;

                if (validateMutableContents &&
                    entry.ContentValidationGeneration !=
                        _decodedImageCacheValidationGeneration)
                {
                    byte[] bytes = source as byte[];

                    if (bytes == null ||
                        entry.ContentFingerprint !=
                            ComputeByteImageFingerprint(bytes))
                    {
                        _decodedImageMru[i] = null;
                        return false;
                    }

                    entry.ContentValidationGeneration =
                        _decodedImageCacheValidationGeneration;
                }

                try
                {
                    if (cachedImage.Width < 0)
                        return false;

                    image = cachedImage;
                    PromoteDecodedImageMru(entry);
                    return true;
                }
                catch
                {
                    _decodedImageMru[i] = null;
                    return false;
                }
            }

            return false;
        }

        private void PromoteDecodedImageMru(
            WeakDecodedImageCacheEntry entry)
        {
            if (entry == null)
                return;

            const int capacity = 8;

            if (_decodedImageMru == null)
                _decodedImageMru =
                    new WeakDecodedImageCacheEntry[capacity];

            int existing = -1;
            int i;

            for (i = 0; i < _decodedImageMru.Length; i++)
            {
                if (Object.ReferenceEquals(_decodedImageMru[i], entry))
                {
                    existing = i;
                    break;
                }
            }

            int last = existing < 0
                ? _decodedImageMru.Length - 1
                : existing;

            for (i = last; i > 0; i--)
                _decodedImageMru[i] = _decodedImageMru[i - 1];

            _decodedImageMru[0] = entry;
        }

        private static ulong ComputeByteImageFingerprint(byte[] bytes)
        {
            // FNV-1a scans only on decode or explicit reload. It adds no buffer
            // allocation and is much cheaper than decoding the image again.
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis;
                int i;

                for (i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= prime;
                }

                hash ^= (ulong)bytes.Length;
                hash *= prime;
                return hash;
            }
        }
    }
}
