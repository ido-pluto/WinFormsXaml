using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace WinFormsXaml
{
    /// <summary>
    /// Mutable colors and metrics shared by framework-owned scrollbars.
    /// Every property change raises Changed so all attached controls repaint.
    /// </summary>
    [TypeConverter(typeof(ScrollBarStyleConverter))]
    public sealed class ScrollBarStyle
    {
        private Color _trackColor;
        private Color _thumbColor;
        private Color _thumbHoverColor;
        private Color _thumbPressedColor;
        private Color _arrowColor;
        private Color _arrowHoverColor;
        private Color _borderColor;
        private int _thickness;
        private int _minimumThumbLength;

        /// <summary>Creates a style based on the active Windows colors.</summary>
        public ScrollBarStyle()
        {
            _trackColor = SystemColors.ScrollBar;
            _thumbColor = SystemColors.Control;
            _thumbHoverColor = SystemColors.ControlLight;
            _thumbPressedColor = SystemColors.ControlDark;
            _arrowColor = SystemColors.ControlText;
            _arrowHoverColor = SystemColors.HotTrack;
            _borderColor = SystemColors.ControlDarkDark;
            _thickness = 16;
            _minimumThumbLength = 8;
        }

        /// <summary>Occurs after any color or metric changes.</summary>
        public event EventHandler Changed;

        /// <summary>Gets or sets the scrollbar track color.</summary>
        public Color TrackColor
        {
            get { return _trackColor; }
            set
            {
                if (_trackColor == value)
                    return;

                _trackColor = value;
                RaiseChanged();
            }
        }

        /// <summary>Gets or sets the resting thumb color.</summary>
        public Color ThumbColor
        {
            get { return _thumbColor; }
            set
            {
                if (_thumbColor == value)
                    return;

                _thumbColor = value;
                RaiseChanged();
            }
        }

        /// <summary>Gets or sets the hovered thumb color.</summary>
        public Color ThumbHoverColor
        {
            get { return _thumbHoverColor; }
            set
            {
                if (_thumbHoverColor == value)
                    return;

                _thumbHoverColor = value;
                RaiseChanged();
            }
        }

        /// <summary>Gets or sets the pressed thumb color.</summary>
        public Color ThumbPressedColor
        {
            get { return _thumbPressedColor; }
            set
            {
                if (_thumbPressedColor == value)
                    return;

                _thumbPressedColor = value;
                RaiseChanged();
            }
        }

        /// <summary>Gets or sets the resting arrow color.</summary>
        public Color ArrowColor
        {
            get { return _arrowColor; }
            set
            {
                if (_arrowColor == value)
                    return;

                _arrowColor = value;
                RaiseChanged();
            }
        }

        /// <summary>Gets or sets the hovered or pressed arrow color.</summary>
        public Color ArrowHoverColor
        {
            get { return _arrowHoverColor; }
            set
            {
                if (_arrowHoverColor == value)
                    return;

                _arrowHoverColor = value;
                RaiseChanged();
            }
        }

        /// <summary>Gets or sets the control and part border color.</summary>
        public Color BorderColor
        {
            get { return _borderColor; }
            set
            {
                if (_borderColor == value)
                    return;

                _borderColor = value;
                RaiseChanged();
            }
        }

        /// <summary>Gets or sets the preferred cross-axis thickness.</summary>
        [DefaultValue(16)]
        public int Thickness
        {
            get { return _thickness; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        "value",
                        "Thickness must be greater than zero.");
                }

                if (_thickness == value)
                    return;

                _thickness = value;
                RaiseChanged();
            }
        }

        /// <summary>Gets or sets the minimum painted thumb length.</summary>
        [DefaultValue(8)]
        public int MinimumThumbLength
        {
            get { return _minimumThumbLength; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        "value",
                        "MinimumThumbLength must be greater than zero.");
                }

                if (_minimumThumbLength == value)
                    return;

                _minimumThumbLength = value;
                RaiseChanged();
            }
        }

        private void RaiseChanged()
        {
            EventHandler handler = Changed;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        /// <summary>Returns a designer-friendly style description.</summary>
        public override string ToString()
        {
            return "ScrollBarStyle";
        }
    }

    /// <summary>
    /// Preserves the nullable XML contract of ItemsControl scroll styles.
    /// An empty value or false means that no framework-owned style is active,
    /// so the native WinForms scrollbar remains in use.
    /// </summary>
    internal sealed class ScrollBarStyleConverter : ExpandableObjectConverter
    {
        public override bool CanConvertFrom(
            ITypeDescriptorContext context,
            Type sourceType)
        {
            if (sourceType == typeof(string) ||
                sourceType == typeof(bool))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(
            ITypeDescriptorContext context,
            CultureInfo culture,
            object value)
        {
            string text = value as string;

            if (text != null)
            {
                text = text.Trim();

                if (text.Length == 0 ||
                    String.Equals(
                        text,
                        "false",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            if (value is bool && !(bool)value)
                return null;

            return base.ConvertFrom(context, culture, value);
        }
    }
}
