using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>
    /// Shared owner-painted implementation for the vertical and horizontal
    /// framework scrollbars. Applications normally instantiate one of the two
    /// orientation-specific subclasses.
    /// </summary>
    [DefaultEvent("Scroll")]
    [DefaultProperty("Value")]
    [ToolboxItem(false)]
    public abstract partial class ScrollBarControl : Control
    {
        private readonly bool _vertical;
        private ScrollBarStyle _style;
        private int _lastStyleThickness;
        private int _minimum;
        private int _maximum;
        private int _largeChange;
        private int _smallChange;
        private int _value;

        /// <summary>
        /// Creates a framework scrollbar with the requested orientation.
        /// </summary>
        /// <param name="vertical">
        /// true for a vertical scrollbar; false for a horizontal scrollbar.
        /// </param>
        protected ScrollBarControl(bool vertical)
        {
            _vertical = vertical;
            _minimum = 0;
            _maximum = 100;
            _largeChange = 10;
            _smallChange = 1;
            _style = new ScrollBarStyle();
            _style.Changed +=
                new EventHandler(ScrollBarStyleChanged);
            _lastStyleThickness = _style.Thickness;

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            TabStop = true;
            AccessibleRole = AccessibleRole.ScrollBar;
            Size = vertical
                ? new Size(_style.Thickness, 100)
                : new Size(100, _style.Thickness);
        }

        /// <summary>Occurs after user input requests a scroll operation.</summary>
        public event ScrollEventHandler Scroll;

        /// <summary>Occurs whenever Value changes.</summary>
        public event EventHandler ValueChanged;

        /// <summary>Gets or sets the colors and metrics used for painting.</summary>
        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Content)]
        public ScrollBarStyle Style
        {
            get { return _style; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                if (Object.ReferenceEquals(_style, value))
                    return;

                int previousThickness = _lastStyleThickness;

                if (_style != null)
                {
                    _style.Changed -=
                        new EventHandler(ScrollBarStyleChanged);
                }

                _style = value;
                _style.Changed +=
                    new EventHandler(ScrollBarStyleChanged);
                _lastStyleThickness = _style.Thickness;
                ApplyStyleThickness(
                    previousThickness,
                    _style.Thickness);
                Invalidate();
            }
        }

        /// <summary>Gets or sets the lower bound of the scroll range.</summary>
        [DefaultValue(0)]
        public int Minimum
        {
            get { return _minimum; }
            set
            {
                if (_minimum == value)
                    return;

                _minimum = value;

                if (_maximum < _minimum)
                    _maximum = _minimum;

                CoerceValueAfterRangeChange();
                Invalidate();
            }
        }

        /// <summary>Gets or sets the upper bound of the scroll range.</summary>
        [DefaultValue(100)]
        public int Maximum
        {
            get { return _maximum; }
            set
            {
                if (_maximum == value)
                    return;

                _maximum = value;

                if (_minimum > _maximum)
                    _minimum = _maximum;

                CoerceValueAfterRangeChange();
                Invalidate();
            }
        }

        /// <summary>Gets or sets the amount moved by a page command.</summary>
        [DefaultValue(10)]
        public int LargeChange
        {
            get { return _largeChange; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        "value",
                        "LargeChange cannot be negative.");
                }

                if (_largeChange == value)
                    return;

                _largeChange = value;
                CoerceValueAfterRangeChange();
                Invalidate();
            }
        }

        /// <summary>Gets or sets the amount moved by a line command.</summary>
        [DefaultValue(1)]
        public int SmallChange
        {
            get { return _smallChange; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        "value",
                        "SmallChange cannot be negative.");
                }

                _smallChange = value;
            }
        }

        /// <summary>Gets or sets the current logical scroll value.</summary>
        [DefaultValue(0)]
        public int Value
        {
            get { return _value; }
            set
            {
                int effectiveMaximum = GetEffectiveMaximum();

                if (value < _minimum || value > effectiveMaximum)
                {
                    throw new ArgumentOutOfRangeException(
                        "value",
                        "Value must be inside the effective scroll range.");
                }

                SetValueProgrammatically(value);
            }
        }

        /// <summary>Gets or sets the scrollbar track color.</summary>
        public Color TrackColor
        {
            get { return _style.TrackColor; }
            set { _style.TrackColor = value; }
        }

        /// <summary>Gets or sets the resting thumb color.</summary>
        public Color ThumbColor
        {
            get { return _style.ThumbColor; }
            set { _style.ThumbColor = value; }
        }

        /// <summary>Gets or sets the hovered thumb color.</summary>
        public Color ThumbHoverColor
        {
            get { return _style.ThumbHoverColor; }
            set { _style.ThumbHoverColor = value; }
        }

        /// <summary>Gets or sets the pressed thumb color.</summary>
        public Color ThumbPressedColor
        {
            get { return _style.ThumbPressedColor; }
            set { _style.ThumbPressedColor = value; }
        }

        /// <summary>Gets or sets the resting arrow color.</summary>
        public Color ArrowColor
        {
            get { return _style.ArrowColor; }
            set { _style.ArrowColor = value; }
        }

        /// <summary>Gets or sets the hovered or pressed arrow color.</summary>
        public Color ArrowHoverColor
        {
            get { return _style.ArrowHoverColor; }
            set { _style.ArrowHoverColor = value; }
        }

        /// <summary>Gets or sets the control and part border color.</summary>
        public Color BorderColor
        {
            get { return _style.BorderColor; }
            set { _style.BorderColor = value; }
        }

        /// <summary>Gets or sets the preferred cross-axis thickness.</summary>
        [DefaultValue(16)]
        public int Thickness
        {
            get { return _style.Thickness; }
            set { _style.Thickness = value; }
        }

        /// <summary>Gets or sets the minimum painted thumb length.</summary>
        [DefaultValue(8)]
        public int MinimumThumbLength
        {
            get { return _style.MinimumThumbLength; }
            set { _style.MinimumThumbLength = value; }
        }

        /// <summary>
        /// Gets whether this scrollbar uses the vertical orientation.
        /// </summary>
        [Browsable(false)]
        public bool IsVertical
        {
            get { return _vertical; }
        }

        /// <summary>
        /// Returns a preferred size based on the style thickness.
        /// </summary>
        /// <param name="proposedSize">The proposed available size.</param>
        /// <returns>The preferred scrollbar size.</returns>
        public override Size GetPreferredSize(Size proposedSize)
        {
            int length = _vertical
                ? proposedSize.Height
                : proposedSize.Width;

            if (length <= 0)
                length = 100;

            return _vertical
                ? new Size(_style.Thickness, length)
                : new Size(length, _style.Thickness);
        }

#if !WINFORMSXAML_PACKAGE
        internal int EffectiveMaximumForTest
        {
            get { return GetEffectiveMaximum(); }
        }
#endif

        /// <summary>
        /// Publishes one complete owner-controlled range snapshot. Assigning
        /// Maximum and LargeChange separately can temporarily coerce Value
        /// against a half-updated range. That intermediate state is observable
        /// by owner-painted scrollbars as a one-frame thumb jump.
        /// </summary>
        internal void SynchronizeState(
            int minimum,
            int maximum,
            int largeChange,
            int smallChange,
            int value,
            bool synchronizeValue)
        {
            if (largeChange < 0)
                throw new ArgumentOutOfRangeException("largeChange");

            if (smallChange < 0)
                throw new ArgumentOutOfRangeException("smallChange");

            if (maximum < minimum)
                maximum = minimum;

            bool rangeChanged = _minimum != minimum ||
                _maximum != maximum ||
                _largeChange != largeChange ||
                _smallChange != smallChange;
            int previousValue = _value;
            Rectangle previousThumb = rangeChanged
                ? Rectangle.Empty
                : GetThumbInvalidationRectangle();

            _minimum = minimum;
            _maximum = maximum;
            _largeChange = largeChange;
            _smallChange = smallChange;

            if (synchronizeValue)
            {
                _value = ClampValue(value);
            }
            else
            {
                _value = ClampValue(_value);
            }

            if (rangeChanged)
                Invalidate();
            else if (previousValue != _value)
                InvalidateThumbTransition(previousThumb);

            if (previousValue != _value)
                OnValueChanged(EventArgs.Empty);
        }

        private void ScrollBarStyleChanged(
            object sender,
            EventArgs e)
        {
            int previousThickness = _lastStyleThickness;
            _lastStyleThickness = _style.Thickness;
            ApplyStyleThickness(
                previousThickness,
                _lastStyleThickness);
            Invalidate();
        }

        private void ApplyStyleThickness(
            int previousThickness,
            int currentThickness)
        {
            if (_vertical)
            {
                if (Width == previousThickness)
                    Width = currentThickness;
            }
            else if (Height == previousThickness)
            {
                Height = currentThickness;
            }
        }

        private int GetEffectiveMaximum()
        {
            if (_maximum <= _minimum)
                return _minimum;

            if (_largeChange <= 0)
                return _maximum;

            long effective =
                (long)_maximum - (long)_largeChange + 1L;

            if (effective <= _minimum)
                return _minimum;

            return effective >= Int32.MaxValue
                ? Int32.MaxValue
                : (int)effective;
        }

        private int ClampValue(long requested)
        {
            int effectiveMaximum = GetEffectiveMaximum();

            if (requested <= _minimum)
                return _minimum;

            if (requested >= effectiveMaximum)
                return effectiveMaximum;

            return (int)requested;
        }

        private void CoerceValueAfterRangeChange()
        {
            int normalized = ClampValue(_value);

            if (normalized == _value)
                return;

            _value = normalized;
            OnValueChanged(EventArgs.Empty);
        }

        private bool SetValueProgrammatically(int value)
        {
            if (_value == value)
                return false;

            Rectangle previousThumb =
                GetThumbInvalidationRectangle();
            _value = value;
            InvalidateThumbTransition(previousThumb);
            OnValueChanged(EventArgs.Empty);
            return true;
        }

        private bool SetValueFromInput(
            long requested,
            ScrollEventType type)
        {
            int oldValue = _value;
            int normalized = ClampValue(requested);

            if (type == ScrollEventType.ThumbTrack &&
                normalized == oldValue)
            {
                return false;
            }

            Rectangle previousThumb =
                GetThumbInvalidationRectangle();
            ScrollEventArgs args = new ScrollEventArgs(
                type,
                oldValue,
                normalized,
                _vertical
                    ? ScrollOrientation.VerticalScroll
                    : ScrollOrientation.HorizontalScroll);

            OnScroll(args);
            normalized = ClampValue(args.NewValue);

            // A Scroll subscriber may synchronize Value while handling the
            // request. Publish only when the requested final value is still
            // different; comparing with the captured old value would raise a
            // duplicate ValueChanged for one logical input operation.
            if (normalized != _value)
            {
                _value = normalized;
                InvalidateThumbTransition(previousThumb);
                OnValueChanged(EventArgs.Empty);
            }

            return _value != oldValue;
        }

        private Rectangle GetThumbInvalidationRectangle()
        {
            Rectangle thumb = CalculateGeometry().Thumb;

            if (thumb.IsEmpty)
                return Rectangle.Empty;

            thumb.Inflate(1, 1);
            return Rectangle.Intersect(
                ClientRectangle,
                thumb);
        }

        private void InvalidateThumbTransition(
            Rectangle previousThumb)
        {
            Rectangle currentThumb =
                GetThumbInvalidationRectangle();

            if (previousThumb.IsEmpty || currentThumb.IsEmpty)
            {
                Invalidate();
                return;
            }

            Invalidate(Rectangle.Union(
                previousThumb,
                currentThumb));
        }

        private void RaiseEndScroll()
        {
            ScrollEventArgs args = new ScrollEventArgs(
                ScrollEventType.EndScroll,
                _value,
                _value,
                _vertical
                    ? ScrollOrientation.VerticalScroll
                    : ScrollOrientation.HorizontalScroll);
            OnScroll(args);
        }

        /// <summary>Raises the <see cref="Scroll"/> event.</summary>
        /// <param name="e">The scroll event data.</param>
        protected virtual void OnScroll(ScrollEventArgs e)
        {
            ScrollEventHandler handler = Scroll;

            if (handler != null)
                handler(this, e);
        }

        /// <summary>Raises the <see cref="ValueChanged"/> event.</summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnValueChanged(EventArgs e)
        {
            EventHandler handler = ValueChanged;

            if (handler != null)
                handler(this, e);
        }

        internal bool ExecuteScrollCommand(ScrollEventType type)
        {
            long requested = _value;

            if (type == ScrollEventType.SmallDecrement)
                requested -= _smallChange;
            else if (type == ScrollEventType.SmallIncrement)
                requested += _smallChange;
            else if (type == ScrollEventType.LargeDecrement)
                requested -= _largeChange;
            else if (type == ScrollEventType.LargeIncrement)
                requested += _largeChange;
            else if (type == ScrollEventType.First)
                requested = _minimum;
            else if (type == ScrollEventType.Last)
                requested = GetEffectiveMaximum();
            else
                return false;

            return SetValueFromInput(requested, type);
        }
    }
}
