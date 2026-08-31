# Getting started

WinFormsXaml creates a normal Windows Forms object tree from XML. You do not
need to know WPF: use WinForms control names, WinForms properties, and WinForms
event signatures.

This guide builds a complete embedded-resource form with a matching C#
code-behind class.

## 1. Add the package

After WinFormsXaml is published, install it from NuGet Package Manager. During
local development, point the same command at the feed produced by the package
build:

```powershell
Install-Package WinFormsXaml
```

The runtime targets .NET Framework 2.0 and later and has no runtime package
dependencies.

The package also includes `WinFormsXaml.xsd`. Associate it with a form XML file
to get element, attribute, and enum-value IntelliSense from Visual Studio's
built-in XML editor. See [XML IntelliSense with the XSD schema](./xml-intellisense).

## 2. Add the XML as an embedded resource

Create this structure:

```text
TaskTracker/
  Program.cs
  WinFormsXaml.xsd  (linked from the NuGet package)
  UI/
    MainForm.cs
    MainForm.xml
```

PackageReference exposes the packaged schema as the root-level
`WinFormsXaml.xsd` link shown above. The schema location is always relative to
the XML file, so `UI/MainForm.xml` refers to it as
`../WinFormsXaml.xsd`.

WinFormsXaml does not add application XML files to the build automatically.
Open `TaskTracker.csproj` as text and manually add this `ItemGroup` inside the
root `<Project>` element:

```xml
<ItemGroup>
  <EmbeddedResource Include="UI\*.xml" />
</ItemGroup>
```

In Visual Studio, the equivalent is selecting each XML file in Solution
Explorer, opening **Properties**, and setting **Build Action** to
**Embedded Resource**.

An embedded resource name is normally:

```text
DefaultNamespace.Folder.FileName.xml
```

With the default namespace `TaskTracker`, the example resource is
`TaskTracker.UI.MainForm.xml`.

Relative file paths inside embedded markup resolve from
`Application.StartupPath`.

## 3. Create MainForm.xml

```xml
<Form xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
      Class="TaskTracker.UI.MainForm"
      Name="MainForm"
      Text="Task tracker"
      Width="560"
      Height="360"
      MinimumSize="420,260"
      StartPosition="CenterScreen">
  <StackPanel Margin="16">
    <Label Text="{Binding HeadingText}"
           AutoSize="true" />

    <TextBox Text="{Binding NewTask, Mode=TwoWay}"
             Margin="0,8,0,8" />

    <Button Text="Add task"
            Click="AddTask_Click" />

    <Label Text="{Binding StatusText}"
           AutoSize="true" />
  </StackPanel>
</Form>
```

The important parts are:

- `Form`, `Label`, `TextBox`, and `Button` are normal WinForms controls.
- `Class` identifies the `XmlForm` code-behind type that owns the Form.
- `StackPanel` is a WinFormsXaml layout container because WinForms has no native
  stack layout with the same behavior.
- `Name` makes a control available to C#.
- `{Binding HeadingText}` reads a public C# field.
- `Mode=TwoWay` writes text-box edits to `NewTask.Value`.
- `Click="AddTask_Click"` connects the normal WinForms event.

## 4. Create MainForm.cs

```csharp
using System;
using WinFormsXaml;

namespace TaskTracker.UI
{
    public sealed class MainForm : XmlForm
    {
        public string HeadingText = "My tasks";

        public readonly PropertyBinding<string> StatusText =
            new PropertyBinding<string>("Ready");

        public readonly PropertyBinding<string> NewTask =
            new PropertyBinding<string>(String.Empty);

        public MainForm()
            : base("MainForm.xml")
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

`XmlForm` supplies the inherited native `WinForm` property, protected `Ui`
runtime, direct `Presets` and `ReloadBinding...` shortcuts, the protected
`Get<T>(name)` helper, and disposal. Loading is deferred until one of these
members is first used, so the
derived class initializes its binding state before XML evaluation. Public
properties and fields are available to `{Binding ...}`; event methods can remain
private.

The example shows the two canonical state forms. `HeadingText` is a simple
snapshot field; if it changes later, call the narrowest suitable inherited
`ReloadBinding...` method. `StatusText` and `NewTask` are stable readonly `PropertyBinding<T>`
fields, so assigning `.Value` refreshes dependent controls automatically and
supports two-way editing.

The explicit `base("MainForm.xml")` call performs a partial embedded-resource
lookup and finds `TaskTracker.UI.MainForm.xml`. Use a longer fragment when an
assembly contains duplicate filenames. You can omit the constructor only when
the exact resource name follows the `Derived.Type.FullName.xml` convention.

Nested item and component markup uses its local context by default. Add
`Source=CodeBehind` to a binding when it intentionally reads shared properties
from this `XmlForm` instead.

The root `Class` value is a static CLR type name. It verifies the code-behind
object supplied by `XmlForm`. The controls remain ordinary WinForms controls.

## 5. Run the form

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

`XamlRuntime.Register()` is not needed for this root-only example. Use it before
`Start()` when a form consumes registered embedded components or includes; the
root Form is loaded directly by `XmlForm`.

That is the complete workflow: one form XML file, one small derived code-behind,
and no runtime field or manual root lookup.

`Start()` is a shortcut for `Application.Run(WinForm)`. `WinForm` is also
available in the derived constructor body if the form must be configured before
the message loop begins. Accessing it there performs the lazy XML load, so any
binding fields assigned in that constructor must be initialized first.

## Direct runtime and component registration

When code owns a runtime directly, `ui.Form` returns the native root form:

```csharp
using WinFormsXaml;

using (XamlRuntime ui = XamlRuntime.Load(xml, codeBehind))
{
    Application.Run(ui.Form);
}
```

To find every embedded component and reusable include without maintaining a
path glob, use:

```csharp
XamlRuntime.Register();
```

This inspects all embedded `.xml` files in the calling assembly and registers
`Component` and `Includes` roots. Well-formed Form, preset, and unrelated XML
documents are ignored. To limit the scan, pass a partial manifest path:

```csharp
XamlRuntime.Register("UI.Components");
```

An explicit `XmlForm` base path can likewise be partial, such as
`base("MainForm")`. A complete manifest resource name with exact casing wins;
partial matches use deterministic suffix, distance, and resource-name ranking.

## Wire named controls automatically

Use the actual WinForms type from the XML. An `XmlForm` automatically assigns a
named object to a declaration-only instance field with the same name and an
assignable reference type:

```xml
<TextBox Name="searchText" />
<Button Name="searchButton" Text="Search" />
<ListView Name="resultsList" View="Details" />
```

```csharp
private TextBox searchText = null;
private Button searchButton = null;
private ListView resultsList = null;

protected override void OnLoaded(EventArgs e)
{
    // All three fields have been assigned before OnLoaded runs.
    searchText.SelectAll();
    searchButton.Enabled = false;
    resultsList.Items.Clear();
}
```

Initialize an automatically wired field explicitly to `null`; this avoids the
compiler's unassigned-field warning while preserving the empty slot required
for wiring. Exact field names are preferred; one unambiguous case-insensitive
match is also accepted. The field is available before `OnLoaded` and before the
first `WinForm` or `Ui` access returns. `Get<T>(name)` remains available for
dynamic lookup, including `Get<ProgressBar>("Loading")`; it throws a clear
exception if the name does not exist or has the wrong type. Use
`Ui.Contains("searchText")` when a control is optional. Most state should use
bindings; name a child only when imperative WinForms access is genuinely useful.

The exact common case is `private Label connectionStatus = null;` for
`<Label Name="connectionStatus">`. An initializer such as
`private Label connectionStatus = Get<Label>("connectionStatus");` is neither
needed nor legal C# instance-field initialization. Call `Get<T>` from a method
or property only for a non-wired or dynamic lookup.

## Run lifetime-owned background work

Use the protected `RunThread(XmlFormThreadStart)` helper for bounded background
work that belongs to this Form:

```csharp
private void StartImport_Click(object sender, EventArgs e)
{
    RunThread(
        delegate(XmlFormThreadContext context)
        {
            while (!context.StopRequested)
            {
                if (context.StopWaitHandle.WaitOne(250, false))
                    return;

                // Do work; publish results through PropertyBinding<T>.Value.
            }
        });
}
```

`RunThread` starts an `IsBackground` thread immediately. A user close
(`CloseReason.UserClosing`) is canceled while workers remain; their stop handles
are signaled, and the last returning worker asynchronously reposts `Close`.
`XmlForm.Dispose` and direct runtime disposal wait at most two seconds total
before beginning their teardown. Native `Form.Dispose` invokes the same
stop/join path synchronously before runtime and code-behind cleanup. If that
limit expires, framework cleanup can be retried after the worker returns. The
owner message loop makes one automatic retry when it can dispatch; otherwise
call `Dispose()` again on the owner thread. Threads are never aborted.

Update reactive `PropertyBinding<T>` values directly from a worker. For
imperative control work, use the protected asynchronous
`PostToUi(MethodInvoker)` shortcut and handle its `false` result when the Form
is closing or unavailable. Never use synchronous `Control.Invoke`, because
closing or disposal may be waiting for the worker. The delegate must observe
the stop state and return cooperatively.

## Default form icon

A root `Form` uses the icon associated with the executable by default. Set the
normal application icon in the project:

```xml
<PropertyGroup>
  <ApplicationIcon>app.ico</ApplicationIcon>
</PropertyGroup>
```

An explicit native `Icon` property or binding overrides it:

```xml
<Form Name="MainForm" Icon="custom.ico" />
```

```xml
<Form Name="MainForm" Icon="{Binding CurrentIcon}" />
```

`UseApplicationIcon` is only a fallback directive. A local `Icon` literal,
`Icon` binding, or `Icon` style always wins, regardless of the order of the XML
attributes. The directive can itself be bound without taking ownership away
from that explicit icon.

Set `UseApplicationIcon="false"` to skip the runtime's executable-icon
assignment and leave the native `Form` default behavior in place:

```xml
<Form Name="MainForm" UseApplicationIcon="false" />
```

## Load XML from another source

Embedded resources are recommended for application interfaces. When XML is
already in memory, use `Load`:

```csharp
string xml = GetInterfaceXml();
XamlRuntime ui = XamlRuntime.Load(xml, this, Application.StartupPath);
```

The optional base path resolves relative files referenced by the markup.

Treat markup and preset XML as application code. Loading XML may instantiate
types, set properties, resolve local resources, and connect event handlers; do
not load untrusted uploaded XML directly.

XML parsing and tree-construction failures use `WinFormsXamlLoadException`.
Argument validation and missing-resource lookup retain their normal
`ArgumentException` and `InvalidOperationException` contracts. XML syntax
errors include the parser's unchanged line and position. Semantic property
failures include the markup source, element path, property name, and the exact
retained attribute position or, when unavailable, the deepest opening element.
Original coordinates survive item-template and registered-component cloning.
See
[Runtime API: Inspect load failures](/reference/runtime#inspect-load-failures).

## Next

- [Copy-paste authoring templates](./authoring-templates)
- [XML IntelliSense with the XSD schema](./xml-intellisense)
- [Markup and layout](./markup-basics)
- [Bindings and functions](./bindings)
- [Reusable components](./components)
- [Reusable includes](./includes)
- [ItemsControl](./items-and-virtualization)
- [Dynamic presets](./presets)
