using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>
    /// A tab container that preserves native TabControl chrome until an
    /// effective framework appearance is requested.
    /// </summary>
    public partial class TabView : Panel
    {
        private readonly TabViewItemCollection _tabItems;
        private TabViewItem _selectedItem;
        private bool _xamlInitializationComplete;
        private bool _hasPendingSelectedIndex;
        private int _pendingSelectedIndex;
        private bool _hasPendingSelectedItem;
        private TabViewItem _pendingSelectedItem;
        private bool _automaticSelection;
        private int _itemsChangeDepth;
        private int _itemsChangeOldIndex;
        private TabViewItem _itemsChangeOldItem;
        private int _preferredSelectionIndex;
        private int _selectionRevision;

        /// <summary>
        /// Initializes an empty TabView with the first visible item selected
        /// automatically when one is added.
        /// </summary>
        public TabView()
        {
            _xamlInitializationComplete = true;
            _automaticSelection = true;
            _preferredSelectionIndex = -1;
            _tabItems = new TabViewItemCollection(this);

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            TabStop = true;
            AccessibleRole = AccessibleRole.PageTabList;
            InitializeTabViewAppearance();
            InitializeNativeTabs();
        }

        /// <summary>
        /// Gets the logical tab-item collection. Its order does not change in
        /// right-to-left layouts; only the physical header placement changes.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Category("Data")]
        public TabViewItemCollection TabItems
        {
            get { return _tabItems; }
        }

        /// <summary>
        /// Gets or sets the selected logical item index, or -1 for no
        /// selection.
        /// </summary>
        [DefaultValue(-1)]
        [Category("Behavior")]
        public int SelectedIndex
        {
            get { return IndexOfItem(_selectedItem); }
            set
            {
                if (!_xamlInitializationComplete)
                {
                    if (value < -1)
                        throw new ArgumentOutOfRangeException("value");

                    _hasPendingSelectedIndex = true;
                    _hasPendingSelectedItem = false;
                    _pendingSelectedItem = null;
                    _pendingSelectedIndex = value;
                    return;
                }

                SetSelectedIndex(value, true);
            }
        }

        /// <summary>
        /// Gets or sets the selected item, or null for no selection.
        /// </summary>
        [DefaultValue(null)]
        [Browsable(false)]
        public TabViewItem SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (!_xamlInitializationComplete)
                {
                    _hasPendingSelectedIndex = false;
                    _hasPendingSelectedItem = true;
                    _pendingSelectedItem = value;
                    return;
                }

                if (value != null && IndexOfItem(value) < 0)
                {
                    throw new ArgumentException(
                        "SelectedItem must belong to this TabView.",
                        "value");
                }

                if (value != null && !value.RequestedVisible)
                {
                    throw new ArgumentException(
                        "A hidden TabViewItem cannot be selected.",
                        "value");
                }

                SetSelection(value, value != null);
            }
        }

        /// <summary>Occurs when SelectedIndex changes.</summary>
        public event EventHandler SelectedIndexChanged;

        /// <summary>Occurs when SelectedItem changes.</summary>
        public event EventHandler SelectedItemChanged;

        /// <summary>
        /// Occurs after a selection transition and supplies both the old and
        /// new item and logical index.
        /// </summary>
        public event TabViewSelectionChangedEventHandler SelectionChanged;

        /// <summary>Releases cached drawing resources owned by the TabView.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposePaintResources();

            base.Dispose(disposing);
        }

        /// <summary>Refreshes cached brushes after system colors change.</summary>
        protected override void OnSystemColorsChanged(EventArgs e)
        {
            DisposePaintResources();
            base.OnSystemColorsChanged(e);
            Invalidate();
        }

        internal void BeginXamlInitialization()
        {
            _xamlInitializationComplete = false;
            _hasPendingSelectedIndex = false;
            _pendingSelectedIndex = -1;
            _hasPendingSelectedItem = false;
            _pendingSelectedItem = null;
        }

        internal void CompleteXamlInitialization()
        {
            if (_xamlInitializationComplete)
                return;

            int oldIndex = IndexOfItem(_selectedItem);
            TabViewItem oldItem = _selectedItem;
            _xamlInitializationComplete = true;

            if (_hasPendingSelectedItem)
            {
                TabViewItem requestedItem = _pendingSelectedItem;
                _hasPendingSelectedItem = false;
                _pendingSelectedItem = null;

                if (requestedItem == null)
                {
                    _selectedItem = null;
                    _automaticSelection = false;
                }
                else
                {
                    if (IndexOfItem(requestedItem) < 0)
                    {
                        throw new InvalidOperationException(
                            "The bound SelectedItem does not belong to this TabView.");
                    }

                    if (!requestedItem.RequestedVisible)
                    {
                        throw new InvalidOperationException(
                            "The bound SelectedItem is hidden.");
                    }

                    _selectedItem = requestedItem;
                    _automaticSelection = true;
                }
            }
            else if (_hasPendingSelectedIndex)
            {
                int requestedIndex = _pendingSelectedIndex;
                _hasPendingSelectedIndex = false;

                if (requestedIndex < -1 || requestedIndex >= TabItems.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        "SelectedIndex",
                        requestedIndex,
                        "SelectedIndex must be -1 or identify a declared TabViewItem.");
                }

                if (requestedIndex < 0)
                {
                    _selectedItem = null;
                    _automaticSelection = false;
                }
                else
                {
                    TabViewItem requestedItem = TabItems[requestedIndex];

                    if (!requestedItem.RequestedVisible)
                    {
                        throw new InvalidOperationException(
                            "SelectedIndex identifies a hidden TabViewItem.");
                    }

                    _selectedItem = requestedItem;
                    _automaticSelection = true;
                }
            }
            else if (_selectedItem == null && _automaticSelection)
            {
                _selectedItem = FindVisibleItem(0, 1);
            }

            ApplySelectedPageState();
            PerformLayout();
            Invalidate();
            PublishSelectionChange(oldIndex, oldItem);
        }

        internal void OnItemRequestedVisibilityChanged(TabViewItem item)
        {
            if (item == null || !Object.ReferenceEquals(item.OwnerTabView, this))
                return;

            int oldIndex = IndexOfItem(_selectedItem);
            TabViewItem oldItem = _selectedItem;
            _headerMetricsDirty = true;

            if (Object.ReferenceEquals(item, _selectedItem) &&
                !item.RequestedVisible)
            {
                int itemIndex = IndexOfItem(item);
                _selectedItem = FindNearestVisibleItem(itemIndex, item);
                _automaticSelection = true;
            }
            else if (_selectedItem == null &&
                _automaticSelection &&
                item.RequestedVisible)
            {
                _selectedItem = item;
            }

            ApplySelectedPageState();
            SynchronizeNativeItems();
            PerformLayout();
            Invalidate();
            PublishSelectionChange(oldIndex, oldItem);
        }

        internal void OnItemHeaderChanged(TabViewItem item)
        {
            if (!Object.ReferenceEquals(item.OwnerTabView, this))
                return;

            InvalidateTabMetrics();
            SynchronizeNativeItems();
        }

        internal void OnItemEnabledChanged(TabViewItem item)
        {
            if (!Object.ReferenceEquals(item.OwnerTabView, this))
                return;

            Invalidate();
            SynchronizeNativeItems();
        }

        internal void InsertItem(int index, TabViewItem item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (index < 0 || index > TabItems.Count)
                throw new ArgumentOutOfRangeException("index");

            NativeTabViewControlCollection collection =
                (NativeTabViewControlCollection)Controls;
            collection.InsertItem(index, item);
        }

        internal void MoveItem(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= TabItems.Count)
                throw new ArgumentOutOfRangeException("oldIndex");

            if (newIndex < 0 || newIndex >= TabItems.Count)
                throw new ArgumentOutOfRangeException("newIndex");

            if (oldIndex == newIndex)
                return;

            NativeTabViewControlCollection collection =
                (NativeTabViewControlCollection)Controls;
            collection.MoveItem(oldIndex, newIndex);
        }

        internal void ClearItems()
        {
            NativeTabViewControlCollection collection =
                (NativeTabViewControlCollection)Controls;
            collection.ClearItems();
        }

        internal void BeginItemsChange()
        {
            if (_itemsChangeDepth == 0)
            {
                _itemsChangeOldIndex = IndexOfItem(_selectedItem);
                _itemsChangeOldItem = _selectedItem;
                _preferredSelectionIndex = _itemsChangeOldIndex;
            }

            _itemsChangeDepth++;
        }

        internal void EndItemsChange()
        {
            if (_itemsChangeDepth <= 0)
                return;

            _itemsChangeDepth--;

            if (_itemsChangeDepth != 0)
                return;

            if (_selectedItem != null && IndexOfItem(_selectedItem) < 0)
                _selectedItem = null;

            if (_selectedItem == null &&
                _automaticSelection &&
                _xamlInitializationComplete)
            {
                _selectedItem = FindNearestVisibleItem(
                    _preferredSelectionIndex,
                    null);
            }

            _preferredSelectionIndex = -1;
            ApplySelectedPageState();
            SynchronizeNativeItems();
            PerformLayout();
            Invalidate();
            PublishSelectionChange(
                _itemsChangeOldIndex,
                _itemsChangeOldItem);
        }

        /// <summary>
        /// Creates a control collection that accepts TabViewItem instances.
        /// </summary>
        protected override Control.ControlCollection CreateControlsInstance()
        {
            return new NativeTabViewControlCollection(this);
        }

        /// <summary>Attaches a newly added item to this owner.</summary>
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);

            TabViewItem item = e.Control as TabViewItem;

            if (item != null)
                item.SetOwner(this);
        }

        /// <summary>Detaches a removed item from this owner.</summary>
        protected override void OnControlRemoved(ControlEventArgs e)
        {
            TabViewItem item = e.Control as TabViewItem;

            if (item != null)
            {
                if (Object.ReferenceEquals(_selectedItem, item))
                    _selectedItem = null;

                item.SetOwner(null);
            }

            base.OnControlRemoved(e);
        }

        /// <summary>
        /// Treats arrow keys as TabView navigation keys.
        /// </summary>
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;

            if (key == Keys.Left ||
                key == Keys.Right ||
                key == Keys.Home ||
                key == Keys.End)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        /// <summary>
        /// Implements physical Left/Right navigation, logical Home/End, and
        /// logical Ctrl+Tab cycling.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e == null)
            {
                base.OnKeyDown(e);
                return;
            }

            int direction = 0;
            int startIndex = SelectedIndex;

            if (e.Control && e.KeyCode == Keys.Tab)
            {
                direction = e.Shift ? -1 : 1;
            }
            else if (e.KeyCode == Keys.Right)
            {
                direction = RightToLeft == RightToLeft.Yes ? -1 : 1;
            }
            else if (e.KeyCode == Keys.Left)
            {
                direction = RightToLeft == RightToLeft.Yes ? 1 : -1;
            }
            else if (e.KeyCode == Keys.Home)
            {
                SelectNavigableBoundary(true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            else if (e.KeyCode == Keys.End)
            {
                SelectNavigableBoundary(false);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (direction != 0)
            {
                SelectNavigableRelative(startIndex, direction, e.Control);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
        }

        /// <summary>
        /// Handles Ctrl+Tab before WinForms treats Tab as dialog navigation.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;

            if (key == Keys.Tab && (modifiers & Keys.Control) == Keys.Control)
            {
                int direction = (modifiers & Keys.Shift) == Keys.Shift
                    ? -1
                    : 1;
                SelectNavigableRelative(SelectedIndex, direction, true);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Raises SelectedIndexChanged.</summary>
        protected virtual void OnSelectedIndexChanged(EventArgs e)
        {
            EventHandler handler = SelectedIndexChanged;

            if (handler != null)
                handler(this, e);
        }

        /// <summary>Raises SelectedItemChanged.</summary>
        protected virtual void OnSelectedItemChanged(EventArgs e)
        {
            EventHandler handler = SelectedItemChanged;

            if (handler != null)
                handler(this, e);
        }

        /// <summary>Raises SelectionChanged.</summary>
        protected virtual void OnSelectionChanged(
            TabViewSelectionChangedEventArgs e)
        {
            TabViewSelectionChangedEventHandler handler = SelectionChanged;

            if (handler != null)
                handler(this, e);
        }

        private void SetSelectedIndex(int value, bool explicitSelection)
        {
            if (value < -1 || value >= TabItems.Count)
                throw new ArgumentOutOfRangeException("value");

            TabViewItem item = value < 0 ? null : TabItems[value];

            if (item != null && !item.RequestedVisible)
            {
                throw new ArgumentException(
                    "A hidden TabViewItem cannot be selected.",
                    "value");
            }

            SetSelection(item, explicitSelection && item != null);
        }

        private void SetSelection(TabViewItem item, bool automaticSelection)
        {
            int oldIndex = IndexOfItem(_selectedItem);
            TabViewItem oldItem = _selectedItem;
            _selectedItem = item;
            _automaticSelection = automaticSelection;
            ApplySelectedPageState();
            SynchronizeNativeSelection();
            PerformLayout();
            Invalidate();
            PublishSelectionChange(oldIndex, oldItem);
        }

        private void PublishSelectionChange(
            int oldIndex,
            TabViewItem oldItem)
        {
            int newIndex = IndexOfItem(_selectedItem);
            TabViewItem newItem = _selectedItem;

            if (oldIndex == newIndex && Object.ReferenceEquals(oldItem, newItem))
                return;

            _selectionRevision++;
            int revision = _selectionRevision;

            if (oldIndex != newIndex)
            {
                OnSelectedIndexChanged(EventArgs.Empty);

                if (_selectionRevision != revision)
                    return;
            }

            if (!Object.ReferenceEquals(oldItem, newItem))
            {
                OnSelectedItemChanged(EventArgs.Empty);

                if (_selectionRevision != revision)
                    return;
            }

            OnSelectionChanged(
                new TabViewSelectionChangedEventArgs(
                    oldIndex,
                    newIndex,
                    oldItem,
                    newItem));
        }

        private void ApplySelectedPageState()
        {
            _tabLayoutDirty = true;
            _revealSelectedHeader = true;
            int i;

            for (i = 0; i < TabItems.Count; i++)
            {
                TabViewItem item = TabItems[i];
                item.SetOwnerVisibility(
                    item.RequestedVisible &&
                    Object.ReferenceEquals(item, _selectedItem));
            }

            SynchronizeNativeSelection();
        }

        private TabViewItem FindNearestVisibleItem(
            int preferredIndex,
            TabViewItem excludedItem)
        {
            if (TabItems.Count == 0)
                return null;

            int start = preferredIndex;

            if (start < 0)
                start = 0;

            if (start >= TabItems.Count)
                start = TabItems.Count - 1;

            TabViewItem item = FindVisibleItem(start, 1, excludedItem);

            if (item != null)
                return item;

            return FindVisibleItem(start - 1, -1, excludedItem);
        }

        private TabViewItem FindVisibleItem(int startIndex, int direction)
        {
            return FindVisibleItem(startIndex, direction, null);
        }

        private TabViewItem FindVisibleItem(
            int startIndex,
            int direction,
            TabViewItem excludedItem)
        {
            int i;

            for (i = startIndex;
                 i >= 0 && i < TabItems.Count;
                 i += direction)
            {
                TabViewItem item = TabItems[i];

                if (!Object.ReferenceEquals(item, excludedItem) &&
                    item.RequestedVisible)
                {
                    return item;
                }
            }

            return null;
        }

        private void SelectNavigableBoundary(bool first)
        {
            int start = first ? 0 : TabItems.Count - 1;
            int direction = first ? 1 : -1;
            TabViewItem item = FindNavigableItem(start, direction, false);

            if (item != null)
                SetSelection(item, true);
        }

        private void SelectNavigableRelative(
            int startIndex,
            int direction,
            bool wrap)
        {
            if (TabItems.Count == 0)
                return;

            int index = startIndex;

            if (index < 0)
                index = direction > 0 ? -1 : TabItems.Count;

            TabViewItem item = FindNavigableItem(
                index + direction,
                direction,
                wrap);

            if (item != null)
                SetSelection(item, true);
        }

        private TabViewItem FindNavigableItem(
            int startIndex,
            int direction,
            bool wrap)
        {
            int visited = 0;
            int index = startIndex;

            while (visited < TabItems.Count)
            {
                if (index < 0 || index >= TabItems.Count)
                {
                    if (!wrap)
                        return null;

                    index = direction > 0 ? 0 : TabItems.Count - 1;
                }

                TabViewItem item = TabItems[index];

                if (item.RequestedVisible && item.Enabled)
                    return item;

                index += direction;
                visited++;
            }

            return null;
        }

        private int IndexOfItem(TabViewItem item)
        {
            if (item == null)
                return -1;

            return ((NativeTabViewControlCollection)Controls).IndexOfItem(item);
        }

        internal sealed class NativeTabViewControlCollection :
            Control.ControlCollection
        {
            private readonly TabView _owner;
            private readonly ArrayList _items;

            internal NativeTabViewControlCollection(TabView owner)
                : base(owner)
            {
                _owner = owner;
                _items = new ArrayList();
            }

            internal int ItemCount
            {
                get { return _items.Count; }
            }

            internal TabViewItem GetItem(int index)
            {
                return (TabViewItem)_items[index];
            }

            internal int IndexOfItem(TabViewItem item)
            {
                return item == null ? -1 : _items.IndexOf(item);
            }

            internal bool ContainsItem(TabViewItem item)
            {
                return item != null && _items.Contains(item);
            }

            internal void AddNativeControl(Control value)
            {
                base.Add(value);
                base.SetChildIndex(value, Count - 1);
            }

            public override void Add(Control value)
            {
                ValidateItem(value);
                _owner.BeginItemsChange();

                try
                {
                    base.Add(value);
                    _items.Add(value);
                }
                finally
                {
                    _owner.EndItemsChange();
                }
            }

            public override void Remove(Control value)
            {
                TabViewItem item = value as TabViewItem;

                if (item == null)
                {
                    if (value is TabControl)
                        base.Remove(value);

                    return;
                }

                _owner.BeginItemsChange();

                try
                {
                    base.Remove(value);
                    _items.Remove(item);
                }
                finally
                {
                    _owner.EndItemsChange();
                }
            }

            internal void InsertItem(int index, TabViewItem item)
            {
                ValidateItem(item);
                _owner.BeginItemsChange();

                try
                {
                    base.Add(item);
                    _items.Insert(index, item);
                }
                finally
                {
                    _owner.EndItemsChange();
                }
            }

            internal void MoveItem(int oldIndex, int newIndex)
            {
                _owner.BeginItemsChange();

                try
                {
                    object item = _items[oldIndex];
                    _items.RemoveAt(oldIndex);
                    _items.Insert(newIndex, item);
                }
                finally
                {
                    _owner.EndItemsChange();
                }
            }

            internal void ClearItems()
            {
                _owner.BeginItemsChange();

                try
                {
                    while (_items.Count != 0)
                    {
                        TabViewItem item =
                            (TabViewItem)_items[_items.Count - 1];
                        base.Remove(item);
                        _items.RemoveAt(_items.Count - 1);
                    }
                }
                finally
                {
                    _owner.EndItemsChange();
                }
            }

            private void ValidateItem(Control value)
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                TabViewItem item = value as TabViewItem;

                if (item == null)
                {
                    throw new InvalidOperationException(
                        "TabView can contain only TabViewItem controls.");
                }

                if (Object.ReferenceEquals(item.OwnerTabView, _owner))
                {
                    throw new InvalidOperationException(
                        "The TabViewItem already belongs to this TabView.");
                }

                if (item.OwnerTabView != null)
                {
                    throw new InvalidOperationException(
                        "Remove the TabViewItem from its current TabView before " +
                        "adding it to another TabView.");
                }
            }
        }
    }

    /// <summary>
    /// Provides ordered access to the TabViewItem controls owned by a TabView.
    /// </summary>
    public sealed class TabViewItemCollection : IEnumerable
    {
        private readonly TabView _owner;

        internal TabViewItemCollection(TabView owner)
        {
            _owner = owner;
        }

        /// <summary>Gets the number of tab items.</summary>
        public int Count
        {
            get
            {
                return ((TabView.NativeTabViewControlCollection)
                    _owner.Controls).ItemCount;
            }
        }

        /// <summary>Gets the item at a logical index.</summary>
        public TabViewItem this[int index]
        {
            get
            {
                return ((TabView.NativeTabViewControlCollection)
                    _owner.Controls).GetItem(index);
            }
        }

        /// <summary>Adds an item and returns its logical index.</summary>
        public int Add(TabViewItem item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            _owner.Controls.Add(item);
            return IndexOf(item);
        }

        /// <summary>Inserts an item at a logical index.</summary>
        public void Insert(int index, TabViewItem item)
        {
            _owner.InsertItem(index, item);
        }

        /// <summary>Removes an item and reports whether it was present.</summary>
        public bool Remove(TabViewItem item)
        {
            if (item == null || !Contains(item))
                return false;

            _owner.Controls.Remove(item);
            return true;
        }

        /// <summary>Removes the item at a logical index.</summary>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException("index");

            _owner.Controls.Remove(this[index]);
        }

        /// <summary>Removes every item.</summary>
        public void Clear()
        {
            _owner.ClearItems();
        }

        /// <summary>Moves an item without changing its identity.</summary>
        public void Move(int oldIndex, int newIndex)
        {
            _owner.MoveItem(oldIndex, newIndex);
        }

        /// <summary>Returns whether the collection contains an item.</summary>
        public bool Contains(TabViewItem item)
        {
            return ((TabView.NativeTabViewControlCollection)
                _owner.Controls).ContainsItem(item);
        }

        /// <summary>Returns an item's logical index, or -1.</summary>
        public int IndexOf(TabViewItem item)
        {
            return ((TabView.NativeTabViewControlCollection)
                _owner.Controls).IndexOfItem(item);
        }

        /// <summary>Copies the logical item sequence to an array.</summary>
        public void CopyTo(TabViewItem[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            if (index < 0 || index > array.Length)
                throw new ArgumentOutOfRangeException("index");

            if (array.Length - index < Count)
                throw new ArgumentException("The destination array is too small.", "array");

            int i;

            for (i = 0; i < Count; i++)
                array[index + i] = this[i];
        }

        /// <summary>Returns an enumerator in logical item order.</summary>
        public IEnumerator GetEnumerator()
        {
            return _owner.Controls.GetEnumerator();
        }
    }
}
