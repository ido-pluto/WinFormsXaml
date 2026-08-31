# WinFormsXaml

WinFormsXaml builds normal Windows Forms interfaces from readable XML. It keeps
the native WinForms controls and event model, then adds reactive bindings,
reusable layouts and components, live presets, styleable tabs and scrollbars,
and an optimized `ItemsControl`.

> **Vibe-coding disclaimer:** This project is vibe coded and was developed with extensive AI assistance.

- .NET Framework 2.0 and later
- C# 2.0-compatible runtime
- No runtime NuGet dependencies
- Visual Studio XML IntelliSense through the packaged XSD
- Native WinForms types in application code

```xml
<Form xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
      Class="TaskTracker.UI.MainForm"
      Text="Task tracker"
      Width="560"
      Height="360"
      StartPosition="CenterScreen">
  <StackPanel Margin="16">
    <Label Text="{Binding StatusText}" AutoSize="true" />
    <TextBox Text="{Binding NewTask, Mode=TwoWay}" Margin="0,8,0,8" />
    <Button Text="Add task" Click="AddTask_Click" />
  </StackPanel>
</Form>
```

## Install

Install the package from NuGet, or point the same command at the local feed
created by `build/Pack.ps1` or `build/Pack.sh`:

```powershell
Install-Package WinFormsXaml
```

The package links `WinFormsXaml.xsd` at the project root. Schema paths are
relative to each XML file, so `UI/MainForm.xml` uses
`../WinFormsXaml.xsd` as shown above.

## Create a form

Keep the XML beside a small code-behind class and embed it in the application
assembly:

```text
TaskTracker/
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

`MainForm.cs` exposes state and handles normal WinForms events:

```csharp
using System;
using WinFormsXaml;

namespace TaskTracker.UI
{
    public sealed class MainForm : XmlForm
    {
        public readonly PropertyBinding<string> StatusText =
            new PropertyBinding<string>("Ready");

        public readonly PropertyBinding<string> NewTask =
            new PropertyBinding<string>(String.Empty);

        public MainForm(): base("MainForm.xml")
        {
        }

        private void AddTask_Click(object sender, EventArgs e)
        {
            string task = NewTask.Value.Trim();

            if (task.Length == 0)
                return;

            StatusText.Value = "Added: " + task;
            NewTask.Value = String.Empty;
        }
    }
}
```

The explicit `base("MainForm.xml")` call performs a partial embedded-resource
lookup and finds `TaskTracker.UI.MainForm.xml`. The constructor can be
omitted when the exact resource name follows the
`Derived.Type.FullName.xml` convention.

Start the form from the normal WinForms entry point:

```csharp
using System;
using System.Windows.Forms;
using TaskTracker.UI;

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

`Start()` is the short form of `Application.Run(WinForm)`.

## Bind state

Use a public field for a snapshot that changes only when you explicitly reload
it:

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
public readonly PropertyBinding<bool> IncludeCompleted =
    new PropertyBinding<bool>(false);
```

```xml
<CheckBox Text="Include completed"
          Checked="{Binding IncludeCompleted, Mode=TwoWay}" />
```

Changing `.Value` updates every target. A supported user edit updates the same
binding and raises its change event. Bindings also support nested paths,
functions, interpolation, conditions, and simple comparison expressions.

[Read the binding guide](docs/guide/bindings.md).

## Repeat items

Use `ItemsBinding<T>` for an observable collection. Add, remove, replace, or
reload only the data that changed:

```csharp
public readonly ItemsBinding<TaskItem> Tasks =
    new ItemsBinding<TaskItem>();
```

```xml
<ItemsControl ItemsSource="{Binding Tasks}">
  <ItemsControl.ItemTemplate>
    <StackPanel Orientation="Horizontal" Gap="8">
      <CheckBox Checked="{Binding Done, Mode=TwoWay}" />
      <Label Text="{Binding Title}" AutoSize="true" />
    </StackPanel>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

The default non-virtual path favors normal native-control behavior. Enable and
tune virtualization only after measuring a real large-list workload. Styled
and native scrollbars, smooth scrolling, horizontal layouts, wrapping,
`ScrollIntoView`, keyed replacement, and precise item reloads are documented
with their performance tradeoffs.

[Read the ItemsControl guide](docs/guide/items-and-virtualization.md).

## Use presets

Presets can switch colors, text, sizes, layout, and other bindable values:

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

Select a variant from the owning `XmlForm`:

```csharp
Presets.Select("Theme", "Dark");
```

When a selected variant omits a key, WinFormsXaml uses the declared default
variant. If neither defines the key, the target property is reset to its
original non-preset value.

[Read the presets guide](docs/guide/presets.md).

## Find the right guide

| Goal | Documentation |
| --- | --- |
| Build the first form | [Getting started](docs/guide/getting-started.md) |
| Copy a minimal template | [Authoring templates](docs/guide/authoring-templates.md) |
| Learn elements, properties, events, styles, and layout | [Markup and layout](docs/guide/markup-basics.md) |
| Build responsive rows, columns, and wrapping layouts | [Flex layout](docs/guide/flex-layout.md) |
| Create reusable typed or XML controls | [Components](docs/guide/components.md) |
| Merge shared XML, presets, resources, or controls | [Includes](docs/guide/includes.md) |
| Build fully styleable LTR/RTL tabs | [TabView](docs/guide/tab-view.md) |
| Use Visual Studio completion | [XML IntelliSense](docs/guide/xml-intellisense.md) |
| Check exact C# APIs and XML syntax | [Runtime reference](docs/reference/runtime.md) and [markup reference](docs/reference/markup.md) |
| Tune startup, rendering, and scrolling | [Performance](docs/reference/performance.md) |
| Understand .NET 2 and legacy Windows limits | [Compatibility](docs/reference/compatibility.md) and [legacy Windows](docs/guide/windows-98.md) |

The complete documentation website lives under `docs/`. Run it without adding
tooling to the repository root:

```powershell
npm --prefix docs ci
npm --prefix docs run docs:dev
```

## Samples

The repository includes five focused applications:

- `samples/HelloWorld` — minimal form, component, preset, event, and binding.
- `samples/BindingPlayground` — one-way, two-way, expressions, and functions.
- `samples/ItemsExplorer` — item templates, updates, layout, and scrolling.
- `samples/ComponentsGallery` — reusable typed and XML components.
- `samples/PresetStudio` — inline, embedded, and file-backed presets.

[See sample commands and feature coverage](docs/guide/sample-applications.md).

## Compatibility and trust

Markup creates controls, assigns properties, resolves resources, and connects
code-behind methods. Treat XML and preset files as application code; do not load
untrusted uploaded or network-provided markup.

The runtime remains C# 2.0 and .NET Framework 2.0 compatible. Verification on
current Windows, Wine, or Mono does not by itself prove Windows 98 behavior;
legacy fallbacks have their own documented acceptance boundary.

## Build and verify

Run the complete repository gate on Windows:

```powershell
./build/Verify.ps1
```

Build a local package with an explicit version:

```powershell
./build/Pack.ps1 -PackageVersion 0.1.14
```

The Bash/Wine packaging path is documented in
[packaging/README.md](packaging/README.md). Neither pack command publishes a
package.

See [CONTRIBUTING.md](CONTRIBUTING.md) for compatibility rules and validation
levels, and [SECURITY.md](SECURITY.md) for private vulnerability reporting and
the markup trust boundary.
