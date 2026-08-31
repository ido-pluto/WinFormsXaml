using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsXaml;

namespace HelloWorld.UI
{
    public sealed class MainForm : XmlForm
    {
        public readonly PropertyBinding<string> Heading;
        public readonly PropertyBinding<string> StatusText;
        public readonly PropertyBinding<bool> CanOpenResults;
        public readonly ItemsBinding<ResultRow> Results;
        public readonly Image SampleImage;
        private bool _darkTheme;

        public MainForm()
        {
            SampleImage = SystemIcons.Information.ToBitmap();
            Heading = new PropertyBinding<string>(
                "Hello from an embedded WinFormsXaml form");
            StatusText = new PropertyBinding<string>("Ready");
            CanOpenResults = new PropertyBinding<bool>(true);
            Results = new ItemsBinding<ResultRow>();

            Results.Add(new ResultRow(1, "Open the markup guide"));
            Results.Add(new ResultRow(2, "Edit the two-way heading"));
            Results.Add(new ResultRow(3, "Use the inline preset values"));
        }

        private void ChangeHeading_Click(
            object sender,
            EventArgs e)
        {
            int nextResultId = Results.Count + 1;

            Heading.Value =
                "Updated at " +
                DateTime.Now.ToLongTimeString();

            StatusText.Value =
                "Binding values and items updated automatically";
            CanOpenResults.Value = !CanOpenResults.Value;
            Results.Add(
                new ResultRow(
                    nextResultId,
                    "Reactive update " + nextResultId.ToString()));
        }

        private void Result_Click(
            object sender,
            EventArgs e)
        {
            Button button = (Button)sender;
            ResultRow result = (ResultRow)button.Tag;

            result.Title.Value = result.Title.Value + " (selected)";
            StatusText.Value =
                "Selected item " + result.Id.ToString();
        }

        private void ToggleTheme_Click(
            object sender,
            EventArgs e)
        {
            _darkTheme = !_darkTheme;
            string next = _darkTheme ? "Dark" : "Light";

            Presets.Select("Theme", next);
            StatusText.Value = "Selected " + next + " preset";
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
            }
            finally
            {
                if (disposing && SampleImage != null)
                    SampleImage.Dispose();
            }
        }

        public sealed class ResultRow
        {
            public readonly int Id;
            public readonly PropertyBinding<string> Title;

            public ResultRow(int id, string title)
            {
                Id = id;
                Title = new PropertyBinding<string>(title);
            }
        }
    }
}
