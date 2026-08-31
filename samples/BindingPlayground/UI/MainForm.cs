using System;
using WinFormsXaml;

namespace BindingPlayground.UI
{
    public sealed class MainForm : XmlForm
    {
        public readonly PropertyBinding<string> Header;
        public readonly Profile CurrentProfile;
        public readonly PropertyBinding<bool> IsReady;
        public readonly PropertyBinding<string> ChangeNotice;
        public readonly PropertyBinding<string> LostFocusDraft;
        public readonly PropertyBinding<string> ExplicitDraft;
        public string ManualCaption;

        public MainForm()
        {
            Header = new PropertyBinding<string>("Edit this header");
            CurrentProfile = new Profile("Ada Lovelace");
            IsReady = new PropertyBinding<bool>(true);
            ChangeNotice = new PropertyBinding<string>("No edits yet");
            LostFocusDraft =
                new PropertyBinding<string>("Tab away to commit this edit");
            ExplicitDraft =
                new PropertyBinding<string>("Click Commit to write this edit");
            ManualCaption = "This value is a snapshot";
            Header.ValueChanged += OnHeaderValueChanged;
        }

        public string BuildSummary(
            string header,
            string displayName)
        {
            return displayName + " — " + header;
        }

        public string BuildDeferredSummary(
            string lostFocusDraft,
            string explicitDraft)
        {
            return "Committed values: " + lostFocusDraft + " | " +
                explicitDraft;
        }

        private void SetHeader_Click(
            object sender,
            EventArgs e)
        {
            Header.Value =
                "Changed at " + DateTime.Now.ToLongTimeString();
        }

        private void ToggleReady_Click(
            object sender,
            EventArgs e)
        {
            IsReady.Value = !IsReady.Value;
        }

        private void CommitExplicit_Click(
            object sender,
            EventArgs e)
        {
            UpdateBindingSource("ExplicitDraftBox", "Text");
        }

        private void ReloadManual_Click(
            object sender,
            EventArgs e)
        {
            ManualCaption =
                "Explicitly reloaded at " +
                DateTime.Now.ToLongTimeString();
            ReloadBinding("ManualCaption", "Text");
        }

        private void OnHeaderValueChanged(
            object sender,
            EventArgs e)
        {
            ChangeNotice.Value =
                "PropertyBinding.ValueChanged observed: " +
                Header.Value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Header.ValueChanged -= OnHeaderValueChanged;

            base.Dispose(disposing);
        }

        public sealed class Profile
        {
            public readonly PropertyBinding<string> DisplayName;

            public Profile(string displayName)
            {
                DisplayName =
                    new PropertyBinding<string>(displayName);
            }
        }
    }
}
