using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public partial class TabView
    {
        private Color _tabBackground;
        private Color _selectedTabBackground;
        private Color _tabForeground;
        private Color _selectedTabForeground;
        private Color _tabBorderBrush;
        private Padding _tabBorderThickness;
        private Padding _tabPadding;
        private int _headerSpacing;
        private Color _contentBackground;
        private Color _contentBorderBrush;
        private Padding _contentBorderThickness;
        private Padding _contentPadding;
        private int _tabCornerRadius;
        private int _selectedTabCornerRadius;

        /// <summary>Gets or sets the unselected header background.</summary>
        [DefaultValue(typeof(Color), "Control")]
        [Category("Appearance")]
        public Color TabBackground
        {
            get { return _tabBackground; }
            set
            {
                if (_tabBackground == value)
                    return;

                _tabBackground = value;
                Invalidate();
                OnTabBackgroundChanged(EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the selected header background.</summary>
        [DefaultValue(typeof(Color), "Window")]
        [Category("Appearance")]
        public Color SelectedTabBackground
        {
            get { return _selectedTabBackground; }
            set
            {
                if (_selectedTabBackground == value)
                    return;

                _selectedTabBackground = value;
                Invalidate();
                OnSelectedTabBackgroundChanged(EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the unselected header text color.</summary>
        [DefaultValue(typeof(Color), "ControlText")]
        [Category("Appearance")]
        public Color TabForeground
        {
            get { return _tabForeground; }
            set
            {
                if (_tabForeground == value)
                    return;

                _tabForeground = value;
                Invalidate();
                OnTabForegroundChanged(EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the selected header text color.</summary>
        [DefaultValue(typeof(Color), "ControlText")]
        [Category("Appearance")]
        public Color SelectedTabForeground
        {
            get { return _selectedTabForeground; }
            set
            {
                if (_selectedTabForeground == value)
                    return;

                _selectedTabForeground = value;
                Invalidate();
                OnSelectedTabForegroundChanged(EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the header border color.</summary>
        [DefaultValue(typeof(Color), "ControlDark")]
        [Category("Appearance")]
        public Color TabBorderBrush
        {
            get { return _tabBorderBrush; }
            set
            {
                if (_tabBorderBrush == value)
                    return;

                _tabBorderBrush = value;
                Invalidate();
                OnTabBorderBrushChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the left, top, right, and bottom header border widths.
        /// </summary>
        [DefaultValue(typeof(Padding), "1, 1, 1, 1")]
        [Category("Appearance")]
        public Padding TabBorderThickness
        {
            get { return _tabBorderThickness; }
            set
            {
                ValidateNonNegativePadding(value, "value");

                if (_tabBorderThickness == value)
                    return;

                _tabBorderThickness = value;
                InvalidateTabMetrics();
                OnTabBorderThicknessChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the horizontal and vertical space around header text.
        /// </summary>
        [DefaultValue(typeof(Padding), "8, 4, 8, 4")]
        [Category("Layout")]
        public Padding TabPadding
        {
            get { return _tabPadding; }
            set
            {
                ValidateNonNegativePadding(value, "value");

                if (_tabPadding == value)
                    return;

                _tabPadding = value;
                InvalidateTabMetrics();
                OnTabPaddingChanged(EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the pixels between adjacent headers.</summary>
        [DefaultValue(0)]
        [Category("Layout")]
        public int HeaderSpacing
        {
            get { return _headerSpacing; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");

                if (_headerSpacing == value)
                    return;

                _headerSpacing = value;
                InvalidateTabMetrics();
                OnHeaderSpacingChanged(EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the content-area background.</summary>
        [DefaultValue(typeof(Color), "Window")]
        [Category("Appearance")]
        public Color ContentBackground
        {
            get { return _contentBackground; }
            set
            {
                if (_contentBackground == value)
                    return;

                _contentBackground = value;
                Invalidate();

                if (_selectedItem != null)
                    _selectedItem.Invalidate();

                OnContentBackgroundChanged(EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the content-area border color.</summary>
        [DefaultValue(typeof(Color), "ControlDark")]
        [Category("Appearance")]
        public Color ContentBorderBrush
        {
            get { return _contentBorderBrush; }
            set
            {
                if (_contentBorderBrush == value)
                    return;

                _contentBorderBrush = value;
                Invalidate();
                OnContentBorderBrushChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the left, top, right, and bottom content border widths.
        /// </summary>
        [DefaultValue(typeof(Padding), "1, 1, 1, 1")]
        [Category("Appearance")]
        public Padding ContentBorderThickness
        {
            get { return _contentBorderThickness; }
            set
            {
                ValidateNonNegativePadding(value, "value");

                if (_contentBorderThickness == value)
                    return;

                _contentBorderThickness = value;
                InvalidateTabLayout();
                OnContentBorderThicknessChanged(EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the space inside the content border.</summary>
        [DefaultValue(typeof(Padding), "0, 0, 0, 0")]
        [Category("Layout")]
        public Padding ContentPadding
        {
            get { return _contentPadding; }
            set
            {
                ValidateNonNegativePadding(value, "value");

                if (_contentPadding == value)
                    return;

                _contentPadding = value;
                InvalidateTabLayout();
                OnContentPaddingChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the corner radius of framework-painted tab headers.
        /// Native tabs ignore this value.
        /// </summary>
        [DefaultValue(0)]
        [Category("Appearance")]
        public int TabCornerRadius
        {
            get { return _tabCornerRadius; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");

                if (_tabCornerRadius == value)
                    return;

                _tabCornerRadius = value;
                Invalidate();
                OnTabCornerRadiusChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the selected-header corner radius, or -1 to use
        /// TabCornerRadius. Native tabs ignore this value.
        /// </summary>
        [DefaultValue(-1)]
        [Category("Appearance")]
        public int SelectedTabCornerRadius
        {
            get { return _selectedTabCornerRadius; }
            set
            {
                if (value < -1)
                    throw new ArgumentOutOfRangeException("value");

                if (_selectedTabCornerRadius == value)
                    return;

                _selectedTabCornerRadius = value;
                Invalidate();
                OnSelectedTabCornerRadiusChanged(EventArgs.Empty);
            }
        }

        /// <summary>Occurs when TabBackground changes.</summary>
        public event EventHandler TabBackgroundChanged;

        /// <summary>Occurs when SelectedTabBackground changes.</summary>
        public event EventHandler SelectedTabBackgroundChanged;

        /// <summary>Occurs when TabForeground changes.</summary>
        public event EventHandler TabForegroundChanged;

        /// <summary>Occurs when SelectedTabForeground changes.</summary>
        public event EventHandler SelectedTabForegroundChanged;

        /// <summary>Occurs when TabBorderBrush changes.</summary>
        public event EventHandler TabBorderBrushChanged;

        /// <summary>Occurs when TabBorderThickness changes.</summary>
        public event EventHandler TabBorderThicknessChanged;

        /// <summary>Occurs when TabPadding changes.</summary>
        public event EventHandler TabPaddingChanged;

        /// <summary>Occurs when HeaderSpacing changes.</summary>
        public event EventHandler HeaderSpacingChanged;

        /// <summary>Occurs when ContentBackground changes.</summary>
        public event EventHandler ContentBackgroundChanged;

        /// <summary>Occurs when ContentBorderBrush changes.</summary>
        public event EventHandler ContentBorderBrushChanged;

        /// <summary>Occurs when ContentBorderThickness changes.</summary>
        public event EventHandler ContentBorderThicknessChanged;

        /// <summary>Occurs when ContentPadding changes.</summary>
        public event EventHandler ContentPaddingChanged;

        /// <summary>Occurs when TabCornerRadius changes.</summary>
        public event EventHandler TabCornerRadiusChanged;

        /// <summary>Occurs when SelectedTabCornerRadius changes.</summary>
        public event EventHandler SelectedTabCornerRadiusChanged;

        /// <summary>Raises TabBackgroundChanged.</summary>
        protected virtual void OnTabBackgroundChanged(EventArgs e)
        {
            RaiseAppearanceEvent(TabBackgroundChanged, e);
        }

        /// <summary>Raises SelectedTabBackgroundChanged.</summary>
        protected virtual void OnSelectedTabBackgroundChanged(EventArgs e)
        {
            RaiseAppearanceEvent(SelectedTabBackgroundChanged, e);
        }

        /// <summary>Raises TabForegroundChanged.</summary>
        protected virtual void OnTabForegroundChanged(EventArgs e)
        {
            RaiseAppearanceEvent(TabForegroundChanged, e);
        }

        /// <summary>Raises SelectedTabForegroundChanged.</summary>
        protected virtual void OnSelectedTabForegroundChanged(EventArgs e)
        {
            RaiseAppearanceEvent(SelectedTabForegroundChanged, e);
        }

        /// <summary>Raises TabBorderBrushChanged.</summary>
        protected virtual void OnTabBorderBrushChanged(EventArgs e)
        {
            RaiseAppearanceEvent(TabBorderBrushChanged, e);
        }

        /// <summary>Raises TabBorderThicknessChanged.</summary>
        protected virtual void OnTabBorderThicknessChanged(EventArgs e)
        {
            RaiseAppearanceEvent(TabBorderThicknessChanged, e);
        }

        /// <summary>Raises TabPaddingChanged.</summary>
        protected virtual void OnTabPaddingChanged(EventArgs e)
        {
            RaiseAppearanceEvent(TabPaddingChanged, e);
        }

        /// <summary>Raises HeaderSpacingChanged.</summary>
        protected virtual void OnHeaderSpacingChanged(EventArgs e)
        {
            RaiseAppearanceEvent(HeaderSpacingChanged, e);
        }

        /// <summary>Raises ContentBackgroundChanged.</summary>
        protected virtual void OnContentBackgroundChanged(EventArgs e)
        {
            RaiseAppearanceEvent(ContentBackgroundChanged, e);
        }

        /// <summary>Raises ContentBorderBrushChanged.</summary>
        protected virtual void OnContentBorderBrushChanged(EventArgs e)
        {
            RaiseAppearanceEvent(ContentBorderBrushChanged, e);
        }

        /// <summary>Raises ContentBorderThicknessChanged.</summary>
        protected virtual void OnContentBorderThicknessChanged(EventArgs e)
        {
            RaiseAppearanceEvent(ContentBorderThicknessChanged, e);
        }

        /// <summary>Raises ContentPaddingChanged.</summary>
        protected virtual void OnContentPaddingChanged(EventArgs e)
        {
            RaiseAppearanceEvent(ContentPaddingChanged, e);
        }

        /// <summary>Raises TabCornerRadiusChanged.</summary>
        protected virtual void OnTabCornerRadiusChanged(EventArgs e)
        {
            RaiseAppearanceEvent(TabCornerRadiusChanged, e);
        }

        /// <summary>Raises SelectedTabCornerRadiusChanged.</summary>
        protected virtual void OnSelectedTabCornerRadiusChanged(EventArgs e)
        {
            RaiseAppearanceEvent(SelectedTabCornerRadiusChanged, e);
        }

        private void InitializeTabViewAppearance()
        {
            _tabBackground = SystemColors.Control;
            _selectedTabBackground = SystemColors.Window;
            _tabForeground = SystemColors.ControlText;
            _selectedTabForeground = SystemColors.ControlText;
            _tabBorderBrush = SystemColors.ControlDark;
            _tabBorderThickness = new Padding(1);
            _tabPadding = new Padding(8, 4, 8, 4);
            _headerSpacing = 0;
            _contentBackground = SystemColors.Window;
            _contentBorderBrush = SystemColors.ControlDark;
            _contentBorderThickness = new Padding(1);
            _contentPadding = Padding.Empty;
            _tabCornerRadius = 0;
            _selectedTabCornerRadius = -1;
        }

        private void RaiseAppearanceEvent(EventHandler handler, EventArgs e)
        {
            UpdateTabRenderingMode();

            if (handler != null)
                handler(this, e);
        }

        private static void ValidateNonNegativePadding(
            Padding value,
            string parameterName)
        {
            if (value.Left < 0 ||
                value.Top < 0 ||
                value.Right < 0 ||
                value.Bottom < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
