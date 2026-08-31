# Copy-paste authoring templates

These templates are the smallest recommended starting points for a Form, an XML
component, and a shared preset file. They use only the public runtime grammar,
C# 2.0 syntax, embedded resources, and the packaged `WinFormsXaml.xsd` link.

## Project layout and embedded resources

Keep the schema link at the project root and interface files below `UI`:

```text
MyProduct/
  Program.cs
  WinFormsXaml.xsd
  UI/
    MainForm.cs
    MainForm.xml
    ThemePresets.xml
    Components/
      NoticeCard.xml
```

Open the application's `.csproj` file as text and manually add this
`ItemGroup` inside the root `<Project>` element. WinFormsXaml does not add
application XML files to the build automatically:

```xml
<ItemGroup>
  <EmbeddedResource Include="UI\*.xml" />
  <EmbeddedResource Include="UI\Components\*.xml" />
</ItemGroup>
```

Alternatively, select each XML file in Visual Studio and set its **Build
Action** to **Embedded Resource**.

Schema locations are relative to each XML file, not to the executable or
project file:

| XML location | `xsi:noNamespaceSchemaLocation` |
| --- | --- |
| Project root | `WinFormsXaml.xsd` |
| `UI/MainForm.xml` or `UI/ThemePresets.xml` | `../WinFormsXaml.xsd` |
| `UI/Components/NoticeCard.xml` | `../../WinFormsXaml.xsd` |

## Minimal Form

`UI/MainForm.xml` associates the schema, verifies its code-behind class, and
uses ordinary WinForms controls:

```xml
<Form xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
      Class="MyProduct.UI.MainForm"
      Name="MainForm"
      Text="Customer search"
      Width="560"
      Height="340"
      StartPosition="CenterScreen">
  <StackPanel Margin="16">
    <Label Text="{Binding StatusText}" AutoSize="true" />
    <TextBox Text="{Binding Query, Mode=TwoWay}"
             Margin="0,8,0,8" />
    <CheckBox Text="Include archived"
              Checked="{Binding IncludeArchived, Mode=TwoWay}" />
    <Button Text="Search"
            ToolTip="Search with the current values"
            Click="Search_Click"
            Margin="0,8,0,0" />
  </StackPanel>
</Form>
```

`UI/MainForm.cs` keeps reactive and two-way state in stable
`PropertyBinding<T>` fields:

```csharp
using System;
using WinFormsXaml;

namespace MyProduct.UI
{
    public sealed class MainForm : XmlForm
    {
        public readonly PropertyBinding<string> Query =
            new PropertyBinding<string>(String.Empty);

        public readonly PropertyBinding<bool> IncludeArchived =
            new PropertyBinding<bool>(false);

        public readonly PropertyBinding<string> StatusText =
            new PropertyBinding<string>("Ready");

        public MainForm()
            : base("MainForm.xml")
        {
        }

        private void Search_Click(object sender, EventArgs e)
        {
            StatusText.Value =
                "Searching for " + Query.Value + "...";
        }
    }
}
```

Start the Form and register any embedded XML components from `Program.cs`:

```csharp
using System;
using System.Windows.Forms;
using WinFormsXaml;

namespace MyProduct
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            XamlRuntime.Register();
            new UI.MainForm().Start();
        }
    }
}
```

## Minimal XML component

`UI/Components/NoticeCard.xml` declares its public inputs and one optional
caller-content slot. Omit `Class` when the component does not need C#
code-behind:

```xml
<Component xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
           xsi:noNamespaceSchemaLocation="../../WinFormsXaml.xsd">
  <Component.Properties>
    <Property Name="Title" Type="String" Required="true" />
    <Property Name="AccentColor"
              Type="System.Drawing.Color"
              Default="SteelBlue" />
  </Component.Properties>

  <Border BorderBrush="{Binding AccentColor}"
          BorderThickness="1"
          Padding="10">
    <StackPanel>
      <Label Text="{Binding Title}" FontWeight="Bold" />
      <Children />
    </StackPanel>
  </Border>
</Component>
```

After `XamlRuntime.Register()`, use the resource filename as the short element
name. Projected children retain the consuming Form's binding and event context:

```xml
<NoticeCard Title="{Binding StatusText}">
  <Button Text="Search again" Click="Search_Click" />
</NoticeCard>
```

## Minimal shared presets

`UI/ThemePresets.xml` is a standalone embedded preset document:

```xml
<Presets xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
         Name="Theme"
         Selected="Light"
         Default="Light">
  <Preset Name="Light">
    <Set Key="Surface" Value="White" />
    <Set Key="Foreground" Value="#202020" />
  </Preset>
  <Preset Name="Dark">
    <Set Key="Surface" Value="#202020" />
    <Set Key="Foreground" Value="White" />
  </Preset>
</Presets>
```

Import the manifest resource and consume its selected values from any Form:

```xml
<Presets Source="MyProduct.UI.ThemePresets.xml"
         SourceKind="EmbeddedResource" />

<Panel BackColor="{Preset Theme.Surface}"
       ForeColor="{Preset Theme.Foreground}" />
```

Switch the complete selected set from its `XmlForm` code-behind:

```csharp
Presets.Select("Theme", "Dark");
```

For the detailed contracts behind these templates, continue with
[XML IntelliSense](./xml-intellisense), [bindings](./bindings),
[components](./components), and [presets](./presets).
