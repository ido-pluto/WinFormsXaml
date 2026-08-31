using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>
    /// A LinkLabel with a WPF-style NavigateUri property that opens the
    /// destination through the operating system's default application.
    /// </summary>
    public class HyperlinkLabel : LinkLabel
    {
        private string _navigateUri;

        /// <summary>
        /// Gets or sets the URI opened when the link is activated.
        /// </summary>
        [DefaultValue(null)]
        [Category("Behavior")]
        [Description("The URI opened by the default system application.")]
        public string NavigateUri
        {
            get { return _navigateUri; }
            set
            {
                if (String.Equals(
                    _navigateUri,
                    value,
                    StringComparison.Ordinal))
                {
                    return;
                }

                _navigateUri = value;
                OnNavigateUriChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Occurs when NavigateUri changes.
        /// </summary>
        public event EventHandler NavigateUriChanged;

        /// <summary>
        /// Occurs before the captured destination is opened. Set Handled on
        /// the event data to use application-specific navigation instead.
        /// </summary>
        public event HyperlinkNavigateEventHandler RequestNavigate;

        /// <summary>
        /// Opens NavigateUri through the operating system's default
        /// application. Returns false when no destination is configured or a
        /// RequestNavigate listener handles the request.
        /// </summary>
        public bool Navigate()
        {
            return RequestNavigation(_navigateUri);
        }

        private bool RequestNavigation(string destination)
        {
            if (String.IsNullOrEmpty(destination) ||
                destination.Trim().Length == 0)
            {
                return false;
            }

            HyperlinkNavigateEventArgs navigateEvent =
                new HyperlinkNavigateEventArgs(destination);
            OnRequestNavigate(navigateEvent);

            return navigateEvent.Handled
                ? false
                : NavigateTo(destination);
        }

        private bool NavigateTo(string destination)
        {
            if (String.IsNullOrEmpty(destination) ||
                destination.Trim().Length == 0)
            {
                return false;
            }

            OpenNavigateUri(destination);
            LinkVisited = true;
            return true;
        }

        /// <summary>
        /// Raises NavigateUriChanged.
        /// </summary>
        protected virtual void OnNavigateUriChanged(EventArgs e)
        {
            EventHandler handler = NavigateUriChanged;

            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises RequestNavigate.
        /// </summary>
        protected virtual void OnRequestNavigate(
            HyperlinkNavigateEventArgs e)
        {
            HyperlinkNavigateEventHandler handler = RequestNavigate;

            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Opens a destination. Derived controls may override this to use an
        /// application-specific navigation service.
        /// </summary>
        protected virtual void OpenNavigateUri(string navigateUri)
        {
            ProcessStartInfo startInfo =
                new ProcessStartInfo(navigateUri);
            startInfo.UseShellExecute = true;

            Process process = Process.Start(startInfo);

            if (process != null)
                process.Dispose();
        }

        /// <summary>
        /// Preserves the normal LinkLabel event and then performs automatic
        /// navigation when NavigateUri is configured.
        /// </summary>
        protected override void OnLinkClicked(
            LinkLabelLinkClickedEventArgs e)
        {
            string destination = _navigateUri;
            base.OnLinkClicked(e);
            RequestNavigation(destination);
        }
    }
}
