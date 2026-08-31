# ComponentsGallery sample

This Visual Studio 2005 / .NET Framework 2.0 sample focuses on reusable embedded
XML components.

`Program.cs` performs one global manifest-fragment registration:

```csharp
using WinFormsXaml;

XamlRuntime.Register("UI.Components");
```

That registers `GalleryCard.xml` and `MetricTile.xml` by filename. The sample
shows:

- typed component properties (`Int32` and `System.Drawing.Color`), defaults,
  inline literal values, and reactive Form bindings;
- optional per-invocation `Class` code-behind in `GalleryCard`, with stable
  `PropertyBinding<string>` property injection and a component-owned event;
- one empty `<Children />` slot in `GalleryCard`, with a caller-supplied
  `FlexPanel` subtree and scoped `ChildrenBind.Get<T>` lookup;
- the context boundary: the card title binds to the component's `Title`
  property, while the component property and projected editor both bind two-way
  to `MainForm.CardTitle`;
- caller-owned names such as `TitleEditor` and `CallerActions`;
- wrapping row layout, gaps, alignment, and a growing editor with `FlexPanel`;
- stable public readonly `PropertyBinding<T>` fields for component inputs and
  other reactive values, updated through `.Value` with no manual element lookup
  or binding reload.

The Form and both component files are embedded resources. Each XML document is
associated with the repository XSD for ordinary Visual Studio XML IntelliSense.
