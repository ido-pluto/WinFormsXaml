using System;
using System.Drawing;
using WinFormsXaml;

namespace PresetStudio.UI
{
    public sealed class MainForm : XmlForm
    {
        public readonly PropertyBinding<Color> LiveAccent;
        public readonly PropertyBinding<string> Status;
        private bool _darkTheme;
        private bool _compactDensity;

        public MainForm()
        {
            LiveAccent =
                new PropertyBinding<Color>(
                    Color.FromArgb(37, 99, 235));
            Status = new PropertyBinding<string>(
                "Theme and density presets are ready");
        }

        private void ToggleTheme_Click(
            object sender,
            EventArgs e)
        {
            _darkTheme = !_darkTheme;
            string next = _darkTheme ? "Dark" : "Light";

            Presets.Select("Theme", next);
            Status.Value =
                "Selected embedded " + next + " theme";
        }

        private void ToggleDensity_Click(
            object sender,
            EventArgs e)
        {
            _compactDensity = !_compactDensity;
            string next = _compactDensity
                ? "Compact"
                : "Comfortable";

            Presets.Select("Density", next);
            Status.Value =
                "Selected inline " + next + " density";
        }

        private void ChangeAccent_Click(
            object sender,
            EventArgs e)
        {
            LiveAccent.Value =
                LiveAccent.Value.R > 100
                    ? Color.FromArgb(22, 163, 74)
                    : Color.FromArgb(219, 39, 119);
            Status.Value = "A binding-backed preset value changed";
        }

        private void UseHighContrast_Click(
            object sender,
            EventArgs e)
        {
            Presets.Select("Theme", "HighContrast");
            Status.Value =
                "Selected the XML-declared HighContrast theme";
        }
    }
}
