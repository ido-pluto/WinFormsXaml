namespace WinFormsXaml
{
    /// <summary>
    /// Selects how a virtualizing ItemsControl represents visible rows.
    /// </summary>
    public enum ItemsControlVirtualizationMode
    {
        /// <summary>
        /// Builds normal Windows Forms Control trees for visible rows. This is
        /// the default and supports the complete item-template vocabulary.
        /// </summary>
        Controls,

        /// <summary>
        /// Paints a deliberately restricted fixed-size row template directly
        /// on the ItemsControl without creating native child controls per row.
        /// Unsupported markup is rejected instead of falling back silently.
        /// </summary>
        Lightweight
    }
}
