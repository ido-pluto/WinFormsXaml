using System;
using WinFormsXaml;

namespace ComponentsGallery.UI
{
    public sealed class MainForm : XmlForm
    {
        public readonly PropertyBinding<string> CardTitle;
        public readonly PropertyBinding<int> ClickCount;
        public readonly PropertyBinding<int> TitleLength;
        public readonly PropertyBinding<string> Status;

        public MainForm()
        {
            CardTitle =
                new PropertyBinding<string>("Caller-owned content");
            ClickCount = new PropertyBinding<int>(3);
            TitleLength =
                new PropertyBinding<int>(CardTitle.Value.Length);
            Status = new PropertyBinding<string>(
                "Edit the projected TextBox or use the buttons.");
            CardTitle.ValueChanged += OnCardTitleValueChanged;
        }

        private void Increment_Click(
            object sender,
            EventArgs e)
        {
            ClickCount.Value = ClickCount.Value + 1;
            Status.Value =
                "The bound Int32 component property changed to " +
                ClickCount.Value.ToString() +
                ".";
        }

        private void Reset_Click(
            object sender,
            EventArgs e)
        {
            ClickCount.Value = 0;
            CardTitle.Value = "Caller-owned content";
            Status.Value = "All reactive binding values were reset.";
        }

        private void OnCardTitleValueChanged(
            object sender,
            EventArgs e)
        {
            TitleLength.Value = String.IsNullOrEmpty(CardTitle.Value)
                ? 0
                : CardTitle.Value.Length;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                CardTitle.ValueChanged -= OnCardTitleValueChanged;

            base.Dispose(disposing);
        }
    }
}
