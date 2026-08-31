namespace WinFormsXaml
{
    /// <summary>
    /// Framework-owned, fully styleable vertical scrollbar.
    /// </summary>
    public class VerticalScrollBar : ScrollBarControl
    {
        /// <summary>Creates a vertical framework scrollbar.</summary>
        public VerticalScrollBar()
            : base(true)
        {
        }
    }

    /// <summary>
    /// Framework-owned, fully styleable horizontal scrollbar.
    /// In RightToLeft mode Minimum is placed at the right edge.
    /// </summary>
    public class HorizontalScrollBar : ScrollBarControl
    {
        /// <summary>Creates a horizontal framework scrollbar.</summary>
        public HorizontalScrollBar()
            : base(false)
        {
        }
    }
}
