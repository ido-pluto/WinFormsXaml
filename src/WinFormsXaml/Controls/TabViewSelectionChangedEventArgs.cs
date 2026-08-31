using System;

namespace WinFormsXaml
{
    /// <summary>
    /// Describes a completed TabView selection change.
    /// </summary>
    public sealed class TabViewSelectionChangedEventArgs : EventArgs
    {
        private readonly int _oldIndex;
        private readonly int _newIndex;
        private readonly TabViewItem _oldItem;
        private readonly TabViewItem _newItem;

        /// <summary>
        /// Initializes selection-change event data.
        /// </summary>
        public TabViewSelectionChangedEventArgs(
            int oldIndex,
            int newIndex,
            TabViewItem oldItem,
            TabViewItem newItem)
        {
            _oldIndex = oldIndex;
            _newIndex = newIndex;
            _oldItem = oldItem;
            _newItem = newItem;
        }

        /// <summary>Gets the previously selected logical index.</summary>
        public int OldIndex
        {
            get { return _oldIndex; }
        }

        /// <summary>Gets the newly selected logical index.</summary>
        public int NewIndex
        {
            get { return _newIndex; }
        }

        /// <summary>Gets the previously selected item.</summary>
        public TabViewItem OldItem
        {
            get { return _oldItem; }
        }

        /// <summary>Gets the newly selected item.</summary>
        public TabViewItem NewItem
        {
            get { return _newItem; }
        }
    }

    /// <summary>
    /// Represents a handler for a detailed TabView selection change.
    /// </summary>
    public delegate void TabViewSelectionChangedEventHandler(
        object sender,
        TabViewSelectionChangedEventArgs e);
}
