using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>
    /// Provides one header and one visual content root for a TabView.
    /// </summary>
    public class TabViewItem : Panel
    {
        private TabView _owner;
        private bool _requestedVisible;
        private bool _settingOwnerVisibility;
        private string _lastHeader;

        /// <summary>
        /// Initializes an empty tab item.
        /// </summary>
        public TabViewItem()
        {
            _requestedVisible = true;
            _lastHeader = String.Empty;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Margin = Padding.Empty;
            AccessibleRole = AccessibleRole.PageTab;
        }

        /// <summary>
        /// Gets or sets the header text. Header and Text are aliases for the
        /// same value.
        /// </summary>
        [DefaultValue("")]
        [Category("Appearance")]
        public string Header
        {
            get { return Text; }
            set { Text = value == null ? String.Empty : value; }
        }

        /// <summary>Occurs when Header or Text changes.</summary>
        public event EventHandler HeaderChanged;

        internal TabView OwnerTabView
        {
            get { return _owner; }
        }

        internal bool RequestedVisible
        {
            get { return _requestedVisible; }
        }

        internal void SetOwner(TabView owner)
        {
            _owner = owner;

            if (owner == null)
                SetOwnerVisibility(_requestedVisible);
        }

        internal void SetOwnerVisibility(bool value)
        {
            if (base.Visible == value)
                return;

            _settingOwnerVisibility = true;

            try
            {
                base.SetVisibleCore(value);
            }
            finally
            {
                _settingOwnerVisibility = false;
            }
        }

        /// <summary>
        /// Creates a collection that accepts one visual content root.
        /// </summary>
        protected override Control.ControlCollection CreateControlsInstance()
        {
            return new TabViewItemControlCollection(this);
        }

        /// <summary>
        /// Separates application-requested visibility from the temporary
        /// hiding used for unselected pages.
        /// </summary>
        protected override void SetVisibleCore(bool value)
        {
            if (_settingOwnerVisibility)
            {
                base.SetVisibleCore(value);
                return;
            }

            if (_owner == null)
            {
                _requestedVisible = value;
                base.SetVisibleCore(value);
                return;
            }

            if (_requestedVisible == value)
                return;

            _requestedVisible = value;
            _owner.OnItemRequestedVisibilityChanged(this);
        }

        /// <summary>
        /// Notifies the owner and HeaderChanged listeners after Text changes.
        /// </summary>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            if (String.IsNullOrEmpty(AccessibleName) ||
                String.Equals(
                    AccessibleName,
                    _lastHeader,
                    StringComparison.Ordinal))
            {
                AccessibleName = Header;
            }

            _lastHeader = Header;

            EventHandler handler = HeaderChanged;

            if (handler != null)
                handler(this, e);

            if (_owner != null)
                _owner.OnItemHeaderChanged(this);
        }

        /// <summary>
        /// Notifies the owner when keyboard and mouse selection eligibility
        /// changes.
        /// </summary>
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);

            if (_owner != null)
                _owner.OnItemEnabledChanged(this);
        }

        /// <summary>
        /// Uses the owning TabView content surface when this page keeps its
        /// transparent default background.
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_owner != null &&
                BackColor.A == 0 &&
                e != null &&
                e.Graphics != null)
            {
                e.Graphics.Clear(_owner.ContentBackground);
                return;
            }

            base.OnPaintBackground(e);
        }

        private sealed class TabViewItemControlCollection :
            Control.ControlCollection
        {
            internal TabViewItemControlCollection(TabViewItem owner)
                : base(owner)
            {
            }

            public override void Add(Control value)
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                if (Count != 0 && !Contains(value))
                {
                    throw new InvalidOperationException(
                        "TabViewItem can contain only one visual content root. " +
                        "Put multiple controls inside a layout panel.");
                }

                base.Add(value);
            }
        }
    }
}
