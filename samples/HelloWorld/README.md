# HelloWorld sample

This sample uses the recommended one-interface, two-file structure:

```text
UI/
  Components/
    StatusBadge.xml
  MainForm.cs
  MainForm.xml
```

`MainForm` derives from `XmlForm`. Its full type name matches the embedded XML
resource name, so the parameterless base convention finds it automatically.
The inherited `WinForm` property loads and returns the native form. Reactive and
two-way values use stable public readonly `PropertyBinding<T>` fields:

```csharp
public sealed class MainForm : XmlForm
{
    public readonly PropertyBinding<string> Heading;

    public MainForm()
    {
        Heading = new PropertyBinding<string>("Ready");
    }

    private void UpdateHeading()
    {
        Heading.Value = "Updated";
    }
}
```

`Program` scans the assembly for embedded XML components, then starts the form.
The scan ignores well-formed Form and preset documents and registers only
`Component` roots:

```csharp
using WinFormsXaml;

XamlRuntime.Register();
new MainForm().Start();
```

The XML root declares `Class="HelloWorld.UI.MainForm"`. When the derived class
loads the resource, this verifies the supplied code-behind type. An explicit
base constructor can use a partial path such as `base("MainForm")` when the
type-name convention is not appropriate. `WinForm` is available in the derived
constructor body, and `Start()` is the shortcut for
`Application.Run(WinForm)`. Initialize constructor-assigned binding fields
before accessing `WinForm`, because that first access loads the XML.

`XmlForm.WinForm` is the native form exposed by code-behind. A directly created
`XamlRuntime` exposes its native form as `Form`, as shown above.

The sample demonstrates:

- a native `Form` and native WinForms control names;
- embedded-resource loading;
- an `XmlForm` code-behind with no manual runtime field or root lookup;
- embedded XML-only components registered in one bulk startup call;
- `Class`-verified code-behind and partial embedded-resource paths;
- code-behind events;
- stable `PropertyBinding<T>` fields for reactive and two-way Form values;
- immutable item fields plus stable `PropertyBinding<T>` item values;
- `Source=CodeBehind` for shared Form state inside an item template;
- inline preset values and live preset switching;
- the WPF-style `Image` shortcut bound to an application-owned native image;
- the WPF-style `HyperlinkLabel.NavigateUri` default-browser shortcut;
- a simple `ItemsControl` with item bindings and an item event;
- normal `ProgressBar` code with capability-selected native marquee and the
  native Blocks grow/drain fallback;
- default form icon behavior from the executable;
- XSD association for built-in Visual Studio XML IntelliSense.

The project is compatible with Visual Studio 2005 and targets .NET Framework
2.0 using C# 2.0 syntax. The canonical schema is linked into the sample project;
the embedded Form and component XML files use a relative
`xsi:noNamespaceSchemaLocation` so the built-in Visual Studio XML editor can
load it without an extension. `UI/ThemePresets.xml` is the equivalent
standalone-preset authoring fixture used by the schema gate.
