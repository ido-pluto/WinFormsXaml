using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public partial class TabView
    {
        private TabControl _nativeTabs;
        private bool _forceNativeTabs;
        private bool _usesNativeTabs;
        private bool _synchronizingNativeTabs;

        /// <summary>
        /// Gets or sets whether operating-system tab chrome must be used even
        /// while custom appearance values are stored on this TabView.
        /// </summary>
        [DefaultValue(false)]
        [Category("Behavior")]
        public bool ForceNativeTabs
        {
            get { return _forceNativeTabs; }
            set
            {
                if (_forceNativeTabs == value)
                    return;

                _forceNativeTabs = value;
                UpdateTabRenderingMode();
                OnForceNativeTabsChanged(EventArgs.Empty);
            }
        }

        /// <summary>Occurs when ForceNativeTabs changes.</summary>
        public event EventHandler ForceNativeTabsChanged;

        internal bool UsesNativeTabs
        {
            get { return _usesNativeTabs; }
        }

        internal TabControl NativeTabControl
        {
            get { return _nativeTabs; }
        }

        /// <summary>Raises ForceNativeTabsChanged.</summary>
        protected virtual void OnForceNativeTabsChanged(EventArgs e)
        {
            EventHandler handler = ForceNativeTabsChanged;

            if (handler != null)
                handler(this, e);
        }

        private void InitializeNativeTabs()
        {
            _nativeTabs = new NativeTabControlAdapter();
            _nativeTabs.Name = String.Empty;
            _nativeTabs.TabStop = true;
            _nativeTabs.SelectedIndexChanged +=
                new EventHandler(OnNativeSelectedIndexChanged);
            _nativeTabs.Selecting +=
                new TabControlCancelEventHandler(OnNativeSelecting);

            ((NativeTabViewControlCollection)Controls).AddNativeControl(
                _nativeTabs);

            _usesNativeTabs = true;
            SynchronizeNativeDirection();
            SynchronizeNativeItems();
            UpdateNativeTabBounds();
        }

        private void UpdateTabRenderingMode()
        {
            if (_nativeTabs == null || _nativeTabs.IsDisposed)
                return;

            bool useNative =
                _forceNativeTabs || !HasEffectiveCustomAppearance();

            if (_usesNativeTabs == useNative)
            {
                if (useNative)
                {
                    SynchronizeNativeDirection();
                    SynchronizeNativeItems();
                    UpdateNativeTabBounds();
                }

                return;
            }

            Control focused = FindFocusedDescendant(this);
            SuspendLayout();

            try
            {
                _usesNativeTabs = useNative;
                _nativeTabs.Visible = useNative;

                if (useNative)
                {
                    SynchronizeNativeDirection();
                    SynchronizeNativeItems();
                    UpdateNativeTabBounds();
                    _nativeTabs.SendToBack();
                }

                _tabLayoutDirty = true;
                _headerMetricsDirty = true;
                ApplySelectedPageState();
            }
            finally
            {
                ResumeLayout(true);
            }

            if (focused != null &&
                !focused.IsDisposed &&
                focused.CanFocus &&
                !focused.Focused)
            {
                focused.Focus();
            }

            Invalidate();
        }

        private bool HasEffectiveCustomAppearance()
        {
            return
                BackColor != SystemColors.Control ||
                ForeColor != SystemColors.ControlText ||
                _tabBackground != SystemColors.Control ||
                _selectedTabBackground != SystemColors.Window ||
                _tabForeground != SystemColors.ControlText ||
                _selectedTabForeground != SystemColors.ControlText ||
                _tabBorderBrush != SystemColors.ControlDark ||
                _tabBorderThickness != new Padding(1) ||
                _tabPadding != new Padding(8, 4, 8, 4) ||
                _headerSpacing != 0 ||
                _contentBackground != SystemColors.Window ||
                _contentBorderBrush != SystemColors.ControlDark ||
                _contentBorderThickness != new Padding(1) ||
                _contentPadding != Padding.Empty ||
                _tabCornerRadius != 0 ||
                _selectedTabCornerRadius >= 0;
        }

        /// <summary>Re-evaluates native chrome after BackColor changes.</summary>
        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            UpdateTabRenderingMode();
        }

        /// <summary>Re-evaluates native chrome after ForeColor changes.</summary>
        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            UpdateTabRenderingMode();
        }

        private void SynchronizeNativeItems()
        {
            if (_nativeTabs == null ||
                _nativeTabs.IsDisposed ||
                !_usesNativeTabs ||
                _synchronizingNativeTabs)
            {
                return;
            }

            _synchronizingNativeTabs = true;
            _nativeTabs.SuspendLayout();

            try
            {
                int nativeIndex = 0;
                int i;

                for (i = 0; i < TabItems.Count; i++)
                {
                    TabViewItem item = TabItems[i];

                    if (!item.RequestedVisible)
                        continue;

                    TabPage page = FindOrCreateNativePage(
                        item,
                        nativeIndex);
                    string header = item.Header == null
                        ? String.Empty
                        : item.Header;

                    if (!String.Equals(page.Text, header, StringComparison.Ordinal))
                        page.Text = header;

                    nativeIndex++;
                }

                while (_nativeTabs.TabPages.Count > nativeIndex)
                {
                    TabPage obsolete = _nativeTabs.TabPages[nativeIndex];
                    _nativeTabs.TabPages.RemoveAt(nativeIndex);
                    obsolete.Dispose();
                }

                SynchronizeNativeSelectionCore();
            }
            finally
            {
                _nativeTabs.ResumeLayout(true);
                _synchronizingNativeTabs = false;
            }
        }

        private TabPage FindOrCreateNativePage(
            TabViewItem item,
            int targetIndex)
        {
            if (targetIndex < _nativeTabs.TabPages.Count)
            {
                TabPage current = _nativeTabs.TabPages[targetIndex];

                if (Object.ReferenceEquals(current.Tag, item))
                    return current;
            }

            int i;

            for (i = targetIndex + 1; i < _nativeTabs.TabPages.Count; i++)
            {
                TabPage existing = _nativeTabs.TabPages[i];

                if (!Object.ReferenceEquals(existing.Tag, item))
                    continue;

                _nativeTabs.TabPages.Remove(existing);
                _nativeTabs.TabPages.Insert(targetIndex, existing);
                return existing;
            }

            TabPage page = new TabPage();
            page.Tag = item;
            page.UseVisualStyleBackColor = true;
            _nativeTabs.TabPages.Insert(targetIndex, page);
            return page;
        }

        private void SynchronizeNativeSelection()
        {
            if (_nativeTabs == null ||
                _nativeTabs.IsDisposed ||
                !_usesNativeTabs ||
                _synchronizingNativeTabs)
            {
                return;
            }

            _synchronizingNativeTabs = true;

            try
            {
                SynchronizeNativeSelectionCore();
            }
            finally
            {
                _synchronizingNativeTabs = false;
            }
        }

        private void SynchronizeNativeSelectionCore()
        {
            int nativeIndex = FindNativePageIndex(_selectedItem);

            if (_nativeTabs.SelectedIndex != nativeIndex)
                _nativeTabs.SelectedIndex = nativeIndex;
        }

        private int FindNativePageIndex(TabViewItem item)
        {
            if (_nativeTabs == null || item == null)
                return -1;

            int i;

            for (i = 0; i < _nativeTabs.TabPages.Count; i++)
            {
                if (Object.ReferenceEquals(_nativeTabs.TabPages[i].Tag, item))
                    return i;
            }

            return -1;
        }

        private void OnNativeSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_synchronizingNativeTabs || !_usesNativeTabs)
                return;

            TabPage page = _nativeTabs.SelectedTab;
            TabViewItem item = page == null
                ? null
                : page.Tag as TabViewItem;

            if (!Object.ReferenceEquals(item, _selectedItem))
                SetSelection(item, item != null);
        }

        private void OnNativeSelecting(
            object sender,
            TabControlCancelEventArgs e)
        {
            if (_synchronizingNativeTabs || e == null || e.TabPage == null)
                return;

            TabViewItem item = e.TabPage.Tag as TabViewItem;

            if (item != null && (!item.Enabled || !Enabled))
                e.Cancel = true;
        }

        private void SynchronizeNativeDirection()
        {
            if (_nativeTabs == null || _nativeTabs.IsDisposed)
                return;

            RightToLeft direction = RightToLeft;
            _nativeTabs.RightToLeft = direction;
            _nativeTabs.RightToLeftLayout = direction == RightToLeft.Yes;
        }

        private void UpdateNativeTabBounds()
        {
            if (_nativeTabs == null ||
                _nativeTabs.IsDisposed ||
                !_usesNativeTabs)
            {
                return;
            }

            Rectangle bounds = DeflateRectangle(ClientRectangle, Padding);

            if (_nativeTabs.Bounds != bounds)
                _nativeTabs.Bounds = bounds;

            _nativeTabs.SendToBack();
        }

        private Rectangle GetNativeContentDisplayBounds()
        {
            if (_nativeTabs == null || !_usesNativeTabs)
                return Rectangle.Empty;

            Rectangle display = _nativeTabs.DisplayRectangle;
            display.Offset(_nativeTabs.Left, _nativeTabs.Top);
            return display;
        }

        private Rectangle GetNativeHeaderBounds(TabViewItem item)
        {
            int index = FindNativePageIndex(item);

            if (index < 0)
                return Rectangle.Empty;

            Rectangle bounds = _nativeTabs.GetTabRect(index);

            // Some .NET 2-compatible WinForms implementations expose the
            // unmirrored native rectangle even while RightToLeftLayout is
            // active. Normalize this diagnostic/layout geometry without
            // interfering with the operating system's native hit testing.
            if (_nativeTabs.RightToLeftLayout &&
                NativeHeaderRectanglesNeedMirroring())
            {
                bounds.X = _nativeTabs.ClientRectangle.Right - bounds.Right;
            }

            bounds.Offset(_nativeTabs.Left, _nativeTabs.Top);
            return bounds;
        }

        private bool NativeHeaderRectanglesNeedMirroring()
        {
            if (_nativeTabs.TabPages.Count < 2)
                return false;

            Rectangle first = _nativeTabs.GetTabRect(0);
            Rectangle last = _nativeTabs.GetTabRect(
                _nativeTabs.TabPages.Count - 1);
            return first.Left < last.Left;
        }

        private static Control FindFocusedDescendant(Control root)
        {
            ContainerControl container = root as ContainerControl;

            if (container != null && container.ActiveControl != null)
                return container.ActiveControl;

            int i;

            for (i = 0; i < root.Controls.Count; i++)
            {
                Control child = root.Controls[i];

                if (child.Focused)
                    return child;

                if (child.ContainsFocus)
                {
                    Control nested = FindFocusedDescendant(child);

                    if (nested != null)
                        return nested;
                }
            }

            return root.Focused ? root : null;
        }

        private sealed class NativeTabControlAdapter : TabControl
        {
            public NativeTabControlAdapter()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            }
        }
    }
}
