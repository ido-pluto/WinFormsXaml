# PresetStudio sample

This application demonstrates all three preset authoring paths used most often:

- `UI/ThemePresets.xml` is a shared embedded resource;
- `Density` is declared inline in the Form;
- `UI/FilePresets.xml` is copied beside the executable and loaded as a file;
- `Accent.Color` is backed by a stable public readonly
  `PropertyBinding<Color>` field.

The buttons switch XML-declared selections through the protected `XmlForm`
shortcut and update binding-backed fields through `.Value`. `HighContrast` is
declared beside the other theme variants instead of being constructed in C#.
No separate preset state object or element lookup is needed.

The XML also shows that preset values are not limited to themes: the density
preset controls padding, spacing, and visibility.
