using System;

namespace WinFormsXaml
{
    /// <summary>
    /// Provides the destination and cancellation state for a
    /// HyperlinkLabel navigation request.
    /// </summary>
    public sealed class HyperlinkNavigateEventArgs : EventArgs
    {
        private readonly string _navigateUri;
        private bool _handled;

        /// <summary>Creates navigation event data for a captured destination.</summary>
        /// <param name="navigateUri">The destination requested by the link.</param>
        public HyperlinkNavigateEventArgs(string navigateUri)
        {
            _navigateUri = navigateUri;
        }

        /// <summary>
        /// Gets the destination captured when the link was activated.
        /// </summary>
        public string NavigateUri
        {
            get { return _navigateUri; }
        }

        /// <summary>
        /// Gets or sets whether application code handled the navigation.
        /// </summary>
        public bool Handled
        {
            get { return _handled; }
            set { _handled = value; }
        }
    }

    /// <summary>
    /// Handles a HyperlinkLabel navigation request.
    /// </summary>
    public delegate void HyperlinkNavigateEventHandler(
        object sender,
        HyperlinkNavigateEventArgs e);
}
