# WinFormsXaml

WinFormsXaml creates normal Windows Forms control trees from readable XML. It
targets .NET Framework 2.0 and has no runtime package dependencies.

[Documentation](https://ido-pluto.github.io/WinFormsXaml/) ·
[Source code](https://github.com/ido-pluto/WinFormsXaml)

> **Vibe-coding disclaimer:** This project is vibe coded and was developed with
> extensive AI assistance. Review and test the source, generated behavior, and
> security assumptions for your own application before relying on it in
> production, especially on legacy Windows targets.

The package adds:

- one-way, reactive, and two-way bindings;
- stack, grid, dock, canvas, and flex layout;
- live presets for themes and other shared values;
- reusable XML and C# components;
- includes for shared presets, resources, and markup;
- styleable LTR/RTL tabs and scrollbars;
- observable item collections and opt-in virtualization;
- a packaged XSD for Visual Studio XML IntelliSense.

## Create an embedded form

Keep each XML form beside a small C# code-behind class:

```text
MyProduct/
  Program.cs
  WinFormsXaml.xsd
  UI/
    MainForm.cs
    MainForm.xml
```

WinFormsXaml does not add application XML files to the build automatically.
Open the application's `.csproj` file as text and manually add this
`ItemGroup` inside the root `<Project>` element:

```xml
<ItemGroup>
  <EmbeddedResource Include="UI\*.xml" />
</ItemGroup>
```

In Visual Studio, the equivalent is selecting each XML file in Solution
Explorer, opening **Properties**, and setting **Build Action** to
**Embedded Resource**.

`UI/MainForm.xml`:

```xml
<Form xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
      Class="MyProduct.UI.MainForm"
      Text="Example"
      Width="520"
      Height="320"
      StartPosition="CenterScreen">
  <StackPanel Margin="12">
    <Label Text="{Binding Message}" AutoSize="true" />
    <TextBox Text="{Binding Message, Mode=TwoWay}" Margin="0,8,0,8" />
    <Button Text="Update" Click="UpdateButton_Click" />
  </StackPanel>
</Form>
```

`UI/MainForm.cs`:

```csharp
using System;
using WinFormsXaml;

namespace MyProduct.UI
{
    public sealed class MainForm : XmlForm
    {
        public readonly PropertyBinding<string> Message =
            new PropertyBinding<string>("Ready");

        public MainForm()
            : base("MainForm.xml")
        {
        }

        private void UpdateButton_Click(object sender, EventArgs e)
        {
            Message.Value = "Updated";
        }
    }
}
```

Run it through the normal WinForms entry point:

```csharp
using System;
using System.Windows.Forms;
using MyProduct.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        new MainForm().Start();
    }
}
```

`XamlRuntime.Register()` is not needed for this root-only example. It is used
for registered embedded components and includes, as shown below; `XmlForm`
loads the root Form directly.

The explicit `base("MainForm.xml")` call performs a partial embedded-resource
lookup. Use a longer fragment when an assembly contains duplicate filenames.
The constructor can be omitted only when the resource name exactly follows the
`Derived.Type.FullName.xml` convention.

`XmlForm.WinForm` is the native `System.Windows.Forms.Form`. Loading is lazy,
so initialize binding fields before accessing `WinForm`, `Ui`, `Get<T>`, or
`Presets` from a constructor.

## Choose the state type

Use a public field for a snapshot that changes only at an explicit reload:

```csharp
public string HeadingText = "My tasks";

private void RenameHeading()
{
    HeadingText = "Open tasks";
    ReloadBinding("Heading", "Text");
}
```

Use one stable `PropertyBinding<T>` for reactive or two-way state:

```csharp
public readonly PropertyBinding<bool> Enabled =
    new PropertyBinding<bool>(true);
```

```xml
<CheckBox Text="Enabled"
          Checked="{Binding Enabled, Mode=TwoWay}" />
```

Changing `.Value` updates every target. Supported user edits update the same
binding and raise its change event.

## Repeat data

Bind `ItemsControl.ItemsSource` to `ItemsBinding<T>`:

```csharp
public readonly ItemsBinding<ResultRow> Results =
    new ItemsBinding<ResultRow>();
```

```xml
<ItemsControl Name="Results" ItemsSource="{Binding Results}">
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" AutoSize="true" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`Add`, `Remove`, and `Replace` publish collection changes. Use
`ReloadItem(index)` after an unobserved item change and `ReloadItems()` when
every row must reevaluate. `ScrollIntoView` and `ScrollIndexIntoView` support
nearest, start, center, and end alignment.

`AutoScroll` defaults to `true`. `SmoothScroll` and viewport virtualization
default to `false`; enable either only when its behavior suits the workload.

## Use presets

Presets can switch any supported property, not only colors:

```xml
<Form>
  <Presets Name="Theme" Selected="Light" Default="Light">
    <Preset Name="Light">
      <Set Key="Surface" Value="White" />
      <Set Key="Text" Value="Black" />
    </Preset>
    <Preset Name="Dark">
      <Set Key="Surface" Value="#23272E" />
      <Set Key="Text" Value="#F3F5F7" />
    </Preset>
  </Presets>

  <StackPanel BackColor="{Preset Theme.Surface}">
    <Label Text="Welcome" ForeColor="{Preset Theme.Text}" />
  </StackPanel>
</Form>
```

```csharp
Presets.Select("Theme", "Dark");
```

If the selected variant omits a key, the declared default variant is used. If
neither defines it, the target returns to its original non-preset value.

## Reuse markup

Before loading a form that uses embedded XML components or registered includes,
register those reusable documents once during application startup:

```csharp
XamlRuntime.Register();
new MainForm().Start();
```

The scan registers only `Component` and `Includes` roots. It safely ignores the
root Form, which `XmlForm` loads directly.

- Register embedded `Component` and `Includes` documents with
  `XamlRuntime.Register()`.
- Use `<Component>` for reusable XML control trees with bindable properties
  and projected `<Children />`.
- Use `<Includes Source="SharedTheme" />` to merge shared presets, resources,
  components, or ordinary child markup.
- Use `TabView` for fully styleable tabs and `FlexPanel` for CSS-like rows,
  columns, wrapping, gaps, alignment, and growth.
- Use `HyperlinkLabel` for a LinkLabel that opens a URI through the default
  shell handler.
- Use `Image` for WPF-style source/stretch authoring or native `PictureBox`
  when its standard API is preferred.

## XML IntelliSense

The package exposes `WinFormsXaml.xsd` as a linked project-root file. Associate
an XML form using:

```xml
<Form xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd">
```

The schema covers built-in controls, framework elements, properties, events,
enum values, and expression-capable attributes. Runtime-registered custom CLR
types remain valid even when a static XSD cannot enumerate their members.

## Compatibility and trust

The runtime is compiled as C# 2.0 against .NET Framework 2.0 reference
assemblies. Modern Windows, Wine, Mono, and Windows 98 are separate validation
environments; success in one is not proof for another.

Markup can instantiate types, assign properties, resolve local resources, and
connect code-behind methods. Treat XML and preset files as application code and
do not load untrusted uploaded or network-provided markup directly.

## Package contents

- `lib/net20/WinFormsXaml.dll`
- `lib/net20/WinFormsXaml.pdb`
- `lib/net20/WinFormsXaml.xml`
- `WinFormsXaml.xsd` for PackageReference and classic projects
- this package README
