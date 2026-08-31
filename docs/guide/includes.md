# Reusable includes

`Includes` composes reusable XML into a form before controls, bindings, and
code-behind wiring are created. Use it for shared visual fragments, local
styles, presets, and groups of registered components without turning every
fragment into a component with declared properties.

An include file has one standalone `<Includes>` root. An include directive uses
the same element with a static `Source`:

```xml
<Includes Source="SharedHeader" />
```

`SourceKind` selects `Registered`, `EmbeddedResource`, or `File` and defaults to
`Registered`. An omitted `SourceKind` is inferred from an explicit
`embedded://` or `file://` prefix; an explicitly conflicting kind is an error.
`Source`, `SourceKind`, and `Assembly` are static composition metadata. They do
not accept Binding, Function, or Preset expressions because the include tree is
resolved before those runtime values exist. `Condition` is different: the
source is still resolved and composed once, while its visual and style
contributions are activated by a Boolean literal or one-way Binding, Function,
or Preset expression:

```xml
<Includes Source="DarkTheme"
          Condition="{Preset Theme == Dark}" />
```

`Mode=TwoWay` is invalid for an include condition. A missing preset set or an
invalid expression raises a `WinFormsXamlLoadException` for `Condition`; the
content is not silently discarded.

## Conditional includes

A dynamic conditional include keeps the composed controls in their original
position and changes their effective visibility in place. It does not parse the
source again, rebuild unrelated siblings, or replace registered component
instances. This also applies when an include is the root of an item template:
realized item controls remain the same objects and their nested bindings stay
active.

Styles contributed through `Includes.Resources` participate only while the
include condition is true. When it becomes false, every affected target returns
to its lower style layer, local XML value, or original native WinForms value.
An outside control may therefore reference a named style from a conditional
include safely:

```xml
<Panel>
  <Includes Source="SharedDarkStyles"
            Condition="{Preset Theme == Dark}" />
  <Label ResourceStyle="SharedCaption" Text="Status" />
</Panel>
```

Nested conditional includes combine their conditions with AND: every enclosing
include condition must be true. A static `Condition="false"` skips construction
of the included visual controls; `Condition="true"` behaves as an ordinary
include.

Preset declarations are imported even while a conditional include is inactive.
They are catalogs needed to evaluate selected-name and key expressions, and
their normal merge/precedence rules remain deterministic. The condition gates
visual controls and included styles, not preset availability.

A conditional include may contribute ordinary visual controls, registered
components, `Presets`, and `Includes.Resources`. It cannot contribute another
top-level owner property element such as `Grid.RowDefinitions`; put that
property inside an included conditional visual root. This avoids dynamically
changing an already-created owner's structural property collection.

## Create an include document

Create `UI/Shared/SharedHeader.xml`:

```xml
<Includes xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xsi:noNamespaceSchemaLocation="../../WinFormsXaml.xsd">
  <Includes.Resources>
    <Style Key="SharedHeading" TargetType="Label">
      <Setter Property="AutoSize" Value="true" />
      <Setter Property="ForeColor" Value="#1F2937" />
      <Setter Property="Margin" Value="0,0,0,8" />
    </Style>
  </Includes.Resources>

  <Presets Name="SharedText" Selected="Default">
    <Preset Name="Default">
      <Set Key="Heading" Value="Customer workspace" />
    </Preset>
  </Presets>

  <StackPanel>
    <Label Style="{StaticResource SharedHeading}"
           Text="{Preset SharedText.Heading}" />
    <StatusBadge Text="{Binding ConnectionText}" />
  </StackPanel>
</Includes>
```

`Includes.Resources` holds normal `Style` entries. During composition it is
renamed for the receiving owner, such as `Form.Resources` or
`Component.Resources`. If that owner already has a resources block, the style
entries are merged into it instead of creating a second property block. The
result has the same scope and behavior as resources written directly on that
owner.

Declare normal `<Presets>` directly under `<Includes>`; they are imported with
the included content. The remaining children are inserted at the directive's
position in their original order. An include can contain one visual root,
several visual siblings when the receiving parent accepts them, or only
resources and presets.

## Merge and precedence rules

Composition follows document order. This makes an include behave like markup
written at the location of its directive:

- programmatic `XmlForm.Include` content is prepended in call order, so markup
  authored in the form can override it;
- `Includes.Resources` entries are merged with an existing destination
  resources block while retaining the same relative order;
- sibling `<Presets>` declarations with the same `Name` in the receiving
  owner's direct/resource scope are combined;
- a later matching `Preset`/`Set` replaces the earlier value at its existing
  position, while new presets and keys are appended in declaration order;
- a later `Selected` or `Default` attribute replaces an earlier one; and
- later form-local declarations can override included preset values and
  selections without requiring a separate C# preset state object.

For example, placing an include before a local `Presets Name="Theme"` block
lets the form override only the keys it needs while retaining every other key
from the shared theme.

`StatusBadge` in this example is an ordinary registered XML or C# component.
Register components before loading the form that expands the include.

## Use a registered include

Open the application's `.csproj` file and manually embed the include documents,
then register their resource group once during application startup:

```xml
<ItemGroup>
  <EmbeddedResource Include="UI\Shared\*.xml" />
</ItemGroup>
```

The Visual Studio equivalent is setting each include XML file's **Build
Action** to **Embedded Resource**.

```csharp
XamlRuntime.Register("UI.Shared");
```

Then use the resource's final name as the default registered identifier:

```xml
<Form Name="MainForm">
  <Includes Source="SharedHeader" />
  <Panel>
    <!-- form-specific content -->
  </Panel>
</Form>
```

Registered lookup first resolves the best unique match inside the containing
markup assembly. Only when that assembly has no match—or the markup has no
assembly context—does the runtime search the global registered include catalog.
Within each tier, an exact identifier or full resource name wins; otherwise one
unique partial match is accepted. Missing and ambiguous partial identifiers,
including ambiguity across registered assemblies, are errors rather than
order-dependent matches. Write
`SourceKind="Registered"` only when the explicit spelling improves clarity; it
is the default.

## Read an embedded resource directly

Direct embedded loading does not require global registration:

```xml
<Includes Source="MyProduct.UI.Shared.SharedHeader.xml"
          SourceKind="EmbeddedResource" />
```

When the resource belongs to another loaded assembly, name that assembly:

```xml
<Includes Source="SharedLibrary.UI.SharedHeader.xml"
          SourceKind="EmbeddedResource"
          Assembly="SharedLibrary" />
```

`Assembly` is an assembly name, not a namespace or file path, and is meaningful
only for `EmbeddedResource`. Omit it when the include belongs to the normal
assembly context of the containing markup.

## Read a file

Use file loading for markup that should remain replaceable after deployment:

```xml
<Includes Source="UI/Shared/SharedHeader.xml"
          SourceKind="File" />
```

Relative paths follow the containing runtime's normal base-path rules. A runtime
loaded with `XamlRuntime.Load(..., basePath)` uses that base path; embedded
`XmlForm` markup uses the application's startup path for relative files.

## Nest includes

An include document can compose smaller registered, embedded, or file-backed
includes, and each nested directive may have its own condition:

```xml
<Includes>
  <Includes Source="SharedCommands" />
  <Includes Source="SharedLibrary.UI.HelpLinks.xml"
            SourceKind="EmbeddedResource"
            Assembly="SharedLibrary" />
  <Includes Source="UI/Overrides.xml"
            SourceKind="File"
            Condition="{Binding EnableOverrides}" />

  <StackPanel>
    <Label Text="Page-specific shared content" />
  </StackPanel>
</Includes>
```

Every nested source is expanded before the resulting visual tree is built.
Keep the final shape valid for the receiving parent: for example, a single-child
host still cannot receive several visual siblings merely because they came from
an include. Circular include chains are rejected with the source chain in the
load error, and nesting is bounded to prevent runaway composition.

Includes inside an item template are expanded before that template is compiled,
not once per rendered item. A dynamic condition updates realized row controls
without enumerating the item source again. Includes used by registered XML
components are expanded when the component is registered. In both cases the
resulting controls still use the destination data context, bindings, presets,
styles, and code-behind event target.

An include placed directly under `<Component>` may contribute `Presets` and
`Includes.Resources`; the runtime promotes that metadata into the component's
single visual root. Ordinary visual elements are not metadata, so the composed
component must still contain exactly one visual root.

## Definition and diagnostic rules

A reusable file must have one `<Includes>` root without `Source`, `SourceKind`,
`Assembly`, or `Condition`. A directive must have a non-empty static `Source`
and must be empty. Its optional `Condition` must be a Boolean literal or a
one-way dynamic expression. DTD processing and external XML resolution are
disabled.

Parsing or binding failures retain the included resource/file name, original
line and position, and the include chain in the element path. Missing sources,
ambiguous partial references, invalid roots, and cycles therefore fail the form
load with a `WinFormsXamlLoadException`; content is never silently omitted.

## Queue includes from `XmlForm`

`XmlForm` can add include sources before its lazy XML load. Calls retain their
order, and their content is inserted before the form's own root content:

```csharp
using WinFormsXaml;

public sealed class MainForm : XmlForm
{
    public MainForm()
        : base("MainForm")
    {
        Include("SharedHeader");
        Include(
            "UI/Shared/DeveloperTools.xml",
            IncludeSourceKind.File);
    }
}
```

The protected overloads are:

```csharp
Include(string source)
Include(string source, IncludeSourceKind sourceKind)
```

The one-argument form uses `IncludeSourceKind.Registered`. Other enum values are
`EmbeddedResource` and `File`.

Queue every programmatic include before the first access to `WinForm`, `Ui`,
`Get<T>`, or `Presets`. Those members trigger the lazy load, so an include added
afterward is too late for initial composition. `Start()` also loads `WinForm`;
call it only after construction has queued all includes.

This also works when the runtime creates an `XmlForm` from a root `Class`
attribute: its constructor may call `Include` before composition. In that
auto-created case the constructor must not access `WinForm`, `Ui`, `Get<T>`, or
`Presets`, because doing so would start a second lazy load while the outer XML
is still being composed. A directly constructed `new MainForm()` does not have
that outer-load restriction.

Use an XML directive when the composition belongs to the markup. Use
`XmlForm.Include` when one code-behind class chooses an optional source before
loading. Neither form replaces a component when the fragment needs its own
typed public properties or per-instance code-behind behavior; use a
[registered component](./components) for that boundary.
