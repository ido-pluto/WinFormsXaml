namespace WinFormsXaml
{
    /// <summary>
    /// Selects where an item is placed on an ItemsControl scrolling axis.
    /// </summary>
    public enum ItemScrollAlignment
    {
        /// <summary>
        /// Keeps an already-visible item in place and otherwise performs the
        /// smallest movement that reveals it.
        /// </summary>
        Nearest,

        /// <summary>Places the item at the logical leading viewport edge.</summary>
        Start,

        /// <summary>Places the item at the center of the viewport.</summary>
        Center,

        /// <summary>Places the item at the logical trailing viewport edge.</summary>
        End
    }
}
