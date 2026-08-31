# Sample applications

The repository contains five runnable Visual Studio 2005 applications. Start
with `HelloWorld`, then open the sample closest to the feature you are building.
For a smaller blank starting point, use the
[copy-paste authoring templates](./authoring-templates).
Every application targets .NET Framework 2.0, embeds its Form XML, links the
same `WinFormsXaml.xsd`, and uses ordinary WinForms controls and event methods.
Several samples use `FlexPanel`; the [Flex layout guide](./flex-layout) explains
the row, column, wrapping, alignment, gap, and growth behavior in isolation.

| Application | Start here for |
| --- | --- |
| `samples/HelloWorld` | The recommended `MainForm.cs` + `MainForm.xml` structure, `Class`, global component registration, embedded presets, bindings, items, WPF-style Image/HyperlinkLabel shortcuts, progress, and the native Form API. |
| `samples/BindingPlayground` | Reactive `PropertyBinding<T>` fields, `ValueChanged`, one-way snapshot fields, nested paths, two-way editors, functions, negation, `Condition`, and the narrow explicit reload API. |
| `samples/PresetStudio` | Inline, embedded-resource, and deployable-file presets; XML-declared variants; binding-backed values on `XmlForm`; and selection changes. |
| `samples/ItemsExplorer` | A minimal observable list first, then stable keys, versions, direct synchronous fixed-size virtualization, bounded cache reuse, and programmatic scrolling for 2,500 rows. |
| `samples/ComponentsGallery` | Global XML-component registration, optional per-invocation code-behind, typed/default properties, `ChildrenBind`, two-way projected content, and Flex layout. |

## Run a sample

Open `WinFormsXaml.sln`, select one sample as the startup project, and run it.
The projects reference the runtime project directly so the application and
library can be debugged together. A package consumer uses the same C# and XML;
only the project reference is replaced by the `WinFormsXaml` NuGet package.

Each sample has its own README explaining the small set of files and the
behavior to try. The XML files are included in the repository schema gate, so a
new public schema change must keep all sample markup valid.

## Choose the simple path first

The samples deliberately separate defaults from tuning:

- `HelloWorld` and the first `ItemsExplorer` tab use the short authoring path;
- `BindingPlayground` shows explicit reload only for an intentional snapshot
  field;
- the large-list tab adds optimization properties only where stable identity,
  version, and row-size guarantees make them safe;
- components use one explicit projection slot for zero or more caller-owned
  visual children instead of introducing a general templating language.

This keeps normal Form code small while leaving the advanced paths visible in a
complete application when they are needed.
