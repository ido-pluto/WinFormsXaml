# Dynamic presets

Presets are named values grouped into selectable variants. Changing the selected
variant updates every active property that uses one of its values.

They are useful for much more than color themes: a preset value can be text, a
number, a size, a path, a location, or any CLR object supplied from C#.

The C# examples import the package namespace:

```csharp
using WinFormsXaml;
```

The recommended design keeps preset definitions in XML and changing application
state as `PropertyBinding<T>` fields directly on the `XmlForm` class. Code-behind
uses the protected `Presets.Select(...)` shortcut only to choose a declared
variant. Application code does not need to construct or share a separate preset
object.

## Define presets inline

```xml
<Form Name="MainForm" Text="Preset example">
  <Presets Name="Theme" Selected="Light" Default="Light">
    <Preset Name="Light">
      <Set Key="FormColor" Value="#F7F7F7" />
      <Set Key="TextColor" Value="#202020" />
      <Set Key="AccentColor" Value="#2563EB" />
      <Set Key="ThemeButtonText" Value="Use dark theme" />
    </Preset>

    <Preset Name="Dark">
      <Set Key="FormColor" Value="#202020" />
      <Set Key="TextColor" Value="#F7F7F7" />
      <Set Key="AccentColor" Value="#60A5FA" />
      <Set Key="ThemeButtonText" Value="Use light theme" />
    </Preset>
  </Presets>

  <StackPanel Name="Page"
              BackColor="{Preset Theme.FormColor}"
              ForeColor="{Preset Theme.TextColor}"
              Padding="16">
    <Label Text="Account settings"
           ForeColor="{Preset Theme.TextColor}" />
    <Button Text="{Preset Theme.ThemeButtonText}"
            BackColor="{Preset Theme.AccentColor}"
            Click="ToggleTheme_Click" />
  </StackPanel>
</Form>
```

Select another variant from C#:

```csharp
private bool _darkTheme;

private void ToggleTheme_Click(object sender, EventArgs e)
{
    _darkTheme = !_darkTheme;
    Presets.Select(
        "Theme",
        _darkTheme ? "Dark" : "Light");
}
```

No binding reload call is needed. Selecting a preset refreshes matching form
properties and realized `ItemsControl` values automatically.

`Value` is required on every `Set`. Use `Value=""` for an explicit empty value.
Declare each preset container directly with `<Presets>`.

## Compare the selected preset in markup

Use a preset Boolean expression when a Boolean property depends on which preset
is selected:

```xml
<Label Text="Dark theme is active"
       Condition="{Preset Theme == Dark}" />

<Button Text="Edit"
        Enabled="{Preset Theme != Disabled}" />

<CheckBox Text="Use compact dark layout"
          Checked="{Preset Theme == Dark &amp;&amp; Density == Compact}" />

<Label Text="High contrast"
       Condition='{Preset Theme == "High Contrast"}' />
```

Here `Theme` and `Density` mean the currently selected preset names in those
collections. Simple preset names such as `Dark` are unquoted; quote names that
contain spaces. Name comparisons are ordinal and case-insensitive, matching
preset selection.

The supported grammar is `==`, `!=`, unary `!`, `&&`, `||`, and parentheses.
In an XML attribute, write `&&` as `&amp;&amp;`. A selected preset key can also be
a Boolean operand:

```xml
<Button Enabled="{Preset Theme.CanEdit &amp;&amp; Theme != Disabled}" />
```

These expressions work on any Boolean-compatible dynamic property, not only
`Condition`. Selecting another preset or changing a referenced key re-evaluates
the matching properties and realized item-template slots automatically. An
unknown preset collection is a markup error; a valid comparison that evaluates
false is not.

The parser and evaluator use the project's C# 2.0 and .NET Framework 2.0
surface and require no newer Windows API. This preserves the Windows 98/Me
compatibility design; use the legacy guest-validation procedure before making a
platform-specific runtime claim.

## Bind a preset value to live state

A preset value may itself be a binding, function, or another preset reference:

```xml
<Presets Name="Theme" Selected="Current">
  <Preset Name="Current">
    <Set Key="Surface" Value="{Binding Surface}" />
    <Set Key="Caption" Value="{Function FormatCaption(Caption)}" />
    <Set Key="Accent" Value="{Preset Brand.Accent}" />
  </Preset>
</Presets>

<Panel BackColor="{Preset Theme.Surface}">
  <Label Text="{Preset Theme.Caption}"
         ForeColor="{Preset Theme.Accent}" />
</Panel>
```

The preset expression is evaluated against the form's code-behind object. A
complete binding keeps its CLR value, so a `PropertyBinding<Color>` reaches
`BackColor` as a `Color`, not as formatted
text. Changes from observed `PropertyBinding<T>` paths refresh every consuming
property automatically; snapshot fields use an explicit reload.
`Source=Current` and `Source=CodeBehind` are both accepted here; preset values
already use the runtime's code-behind context, so they select the same object.
Function arguments such as `Caption` above observe the same discoverable path
dependencies. A function that reads other state internally has no discoverable
dependency; call `ReloadBindings()` after changing that state.

Preset value bindings are source-only and reject `Mode=TwoWay`. Nested preset
references track selection and value changes transitively, and cycles are
rejected with a descriptive error.

## Use presets for text, size, and behavior

```xml
<Presets Name="Density" Selected="Comfortable" Default="Comfortable">
  <Preset Name="Comfortable">
    <Set Key="EditorPadding" Value="14" />
    <Set Key="RowHeight" Value="48" />
    <Set Key="DetailsVisible" Value="true" />
  </Preset>
  <Preset Name="Compact">
    <Set Key="EditorPadding" Value="6" />
    <Set Key="RowHeight" Value="28" />
    <Set Key="DetailsVisible" Value="false" />
  </Preset>
</Presets>

<Panel Padding="{Preset Density.EditorPadding}">
  <Label Text="Details"
         Visible="{Preset Density.DetailsVisible}" />
</Panel>

<ItemsControl Name="Rows"
              Virtualizing="true"
              VirtualizationThreshold="1"
              FixedItemSize="{Preset Density.RowHeight}" />
```

```csharp
Presets.Select("Density", "Compact");
```

The value is converted for the destination property. Binding-backed values can
carry typed CLR objects when text conversion is not appropriate.
Here `VirtualizationThreshold="1"` makes the nonempty Controls list use direct
virtualization, where `FixedItemSize` owns its main-axis geometry. An ordinary
nonvirtual list instead honors each template root's desired size or bound
`Height`/`Width`.

## Put shared presets in an embedded XML file

For presets used by several forms, keep one embedded resource such as
`UI/ThemePresets.xml`:

```xml
<Presets Name="Theme" Selected="Light" Default="Light">
  <Preset Name="Light">
    <Set Key="FormColor" Value="White" />
    <Set Key="TextColor" Value="#202020" />
  </Preset>
  <Preset Name="Dark">
    <Set Key="FormColor" Value="#202020" />
    <Set Key="TextColor" Value="White" />
  </Preset>
</Presets>
```

Open the application's `.csproj` file and manually embed it with the form XML:

```xml
<ItemGroup>
  <EmbeddedResource Include="UI\*.xml" />
</ItemGroup>
```

The Visual Studio equivalent is setting the preset XML file's **Build Action**
to **Embedded Resource**.

Reference its manifest resource name in each form:

```xml
<Form Name="MainForm" Text="Main">
  <Presets Source="MyProduct.UI.ThemePresets.xml"
           SourceKind="EmbeddedResource" />

  <Panel BackColor="{Preset Theme.FormColor}"
         ForeColor="{Preset Theme.TextColor}" />
</Form>
```

When no `Assembly` is specified, the embedded resource is read from the assembly
that contains the loaded markup. Inline markup falls back to the code-behind
object's assembly and then the application entry assembly. This compact form is
equivalent:

```xml
<Presets Source="embedded://MyProduct.UI.ThemePresets.xml" />
```

## Load presets from a file

Use a file when the application should be configurable after deployment:

```xml
<Presets Source="Themes.xml" SourceKind="File" />
```

For an embedded `XmlForm`, relative preset paths resolve from
`Application.StartupPath`.

Use embedded resources for application defaults and files for values the user
or administrator is expected to replace.

## Keep changing values in `XmlForm` state

Declare the available variants in XML. When one value must change independently
of the selected variant, bind that preset value to a stable field on the owning
`XmlForm`:

```csharp
public sealed class MainForm : XmlForm
{
    public readonly PropertyBinding<Color> AccentColor =
        new PropertyBinding<Color>(Color.RoyalBlue);

    private void UseWarningAccent_Click(object sender, EventArgs e)
    {
        AccentColor.Value = Color.OrangeRed;
    }
}
```

```xml
<Presets Name="Accent" Selected="Current">
  <Preset Name="Current">
    <Set Key="Color" Value="{Binding AccentColor}" />
  </Preset>
</Presets>

<Button BackColor="{Preset Accent.Color}"
        Click="UseWarningAccent_Click" />
```

This is the canonical state path: XML owns the preset structure;
`PropertyBinding<T>` owns live scalar state; `ItemsBinding<T>` owns live lists;
and the `XmlForm` owns the fields and event methods. Updating `.Value`
automatically refreshes every preset consumer without element lookup or a
manual reload.

Define additional variants such as `HighContrast` in XML and select them with
`Presets.Select(...)`. Duplicate keys inside one XML `Preset` are rejected so a
typo cannot silently override an earlier definition.

## Default values for incomplete variants

`Default` provides fallback values. This lets a specialized variant override
only what it needs:

```xml
<Presets Name="Theme" Selected="Dark" Default="Base">
  <Preset Name="Base">
    <Set Key="FontName" Value="Tahoma" />
    <Set Key="ButtonPadding" Value="8,4" />
    <Set Key="TextColor" Value="Black" />
  </Preset>

  <Preset Name="Dark">
    <Set Key="TextColor" Value="White" />
  </Preset>
</Presets>
```

In markup, `Theme.FontName` and `Theme.ButtonPadding` come from `Base`, while
`Theme.TextColor` comes from `Dark`. A `{Preset Theme.Key}` attribute checks only
the selected preset and then the configured `Default`. It never scans unrelated
variants.

If neither of those presets contains the key, the markup value is unresolved.
An unresolved attribute makes no assignment: on initial creation the property
keeps its normal WinForms/framework baseline, and a property that was previously
set by that preset returns to its captured baseline. It can resolve and update
again after a later preset change.

Without `Default`, only the selected preset participates in markup resolution:

```xml
<Presets Name="Theme" Selected="Dark">
  <Preset Name="Base">
    <Set Key="FontName" Value="Tahoma" />
  </Preset>
  <Preset Name="Dark">
    <Set Key="TextColor" Value="White" />
  </Preset>
</Presets>
```

Here `{Preset Theme.FontName}` is unresolved because `Dark` does not define it;
the `Base` declaration is not used merely because it appears first. Add
`Default="Base"` when `Base` is intended to supply incomplete variants.

Markup uses the missing value as the no-assignment signal described above. This
keeps incomplete variants from overwriting native properties with invented
values.

## Share definitions across forms

Put common variants in one embedded XML file and reference that file from every
form. Each form then has the same declarative preset structure without a shared
C# state object:

```xml
<Presets Source="MyProduct.UI.ThemePresets.xml"
         SourceKind="EmbeddedResource" />
```

When the selected variant is application-wide, keep that selection in the
application's normal state and call `Presets.Select(...)` as each `XmlForm` is
opened or when its selection changes. Keep live per-form values as binding
fields on that form. This makes ownership and disposal explicit while retaining
one reusable XML definition.

Preset XML is validated before it changes a form. `Presets` accepts `Name`,
`Selected`, and `Default`; `Preset` accepts `Name`; and `Set` accepts `Key` and
`Value`. Unknown attributes, misspelled child elements, unexpected text, and
unrelated elements are rejected. Normal XML namespace declarations and
`xsi:noNamespaceSchemaLocation` remain valid, so a standalone preset file can
reference the packaged schema.
