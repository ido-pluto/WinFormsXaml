using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>
    /// Controls whether a virtualized ItemsControl may reuse a detached native
    /// Control tree for a different data item.
    /// </summary>
    public enum ItemRecyclingMode
    {
        /// <summary>
        /// Keeps the default identity-preserving cache. A cached tree is reused
        /// only for the same data-item instance and stable key.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Allows cross-item reuse only when the row root implements
        /// IRecyclableItemControl and explicitly accepts each transition.
        /// </summary>
        Explicit = 1
    }

    /// <summary>
    /// Immutable description of one proposed cross-item Control-tree reuse.
    /// The Control is detached and its item-binding subscriptions are inactive
    /// while the reset callback runs.
    /// </summary>
    public sealed class ItemRecycleContext
    {
        private readonly XamlRuntime.ItemsControl _itemsControl;
        private readonly Control _control;
        private readonly object _oldItem;
        private readonly object _newItem;
        private readonly int _oldIndex;
        private readonly int _newIndex;

        internal ItemRecycleContext(
            XamlRuntime.ItemsControl itemsControl,
            Control control,
            object oldItem,
            object newItem,
            int oldIndex,
            int newIndex)
        {
            _itemsControl = itemsControl;
            _control = control;
            _oldItem = oldItem;
            _newItem = newItem;
            _oldIndex = oldIndex;
            _newIndex = newIndex;
        }

        /// <summary>Gets the ItemsControl that owns the virtual row.</summary>
        public XamlRuntime.ItemsControl ItemsControl
        {
            get { return _itemsControl; }
        }

        /// <summary>Gets the detached row-root Control being considered.</summary>
        public Control Control
        {
            get { return _control; }
        }

        /// <summary>Gets the data item previously represented by the tree.</summary>
        public object OldItem
        {
            get { return _oldItem; }
        }

        /// <summary>Gets the data item the tree will represent if accepted.</summary>
        public object NewItem
        {
            get { return _newItem; }
        }

        /// <summary>Gets the previous logical item index.</summary>
        public int OldIndex
        {
            get { return _oldIndex; }
        }

        /// <summary>Gets the proposed logical item index.</summary>
        public int NewIndex
        {
            get { return _newIndex; }
        }
    }

    /// <summary>
    /// Explicit safety contract for reusing a native row tree across data items.
    /// Implement this on the ItemTemplate root Control. The callback must reset
    /// only transient state not owned by XAML bindings, such as selection,
    /// expansion, hover, edit, or animation state. It must not dispose, reparent,
    /// or structurally change the row tree. Returning false discards the old tree
    /// and asks the runtime to construct a new one. An exception fails the item
    /// refresh and remains visible to the caller.
    /// </summary>
    public interface IRecyclableItemControl
    {
        /// <summary>
        /// Resets transient state before the runtime applies the new item values.
        /// Return true only when the tree is safe to reuse for the proposed item.
        /// </summary>
        bool TryPrepareForRecycle(ItemRecycleContext context);
    }
}
