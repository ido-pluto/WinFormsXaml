# Roadmap

WinFormsXaml is focused on making Windows Forms interfaces easier to build and
maintain without giving up native controls or predictable performance.

## Authoring experience

- Expand complete, runnable examples for forms, reusable components, dialogs,
  menus, data entry, and large data sets.
- Keep the packaged XSD authoring schema synchronized with the public markup
  surface and improve application-specific schema extension guidance.
- Preserve `WinFormsXamlLoadException` source, element, property, and semantic
  XML locations as new markup transforms and cloning paths are introduced.
- Keep XML terminology aligned with native WinForms wherever a native type or
  property already exists.
- Keep simple public fields as the explicit-reload snapshot path and stable
  readonly `PropertyBinding<T>` fields as the reactive and two-way authoring
  path.

## Components and application structure

- Expand component examples for typed controls, XML-only composition, nested
  forwarding, and multi-form applications.
- Keep constructor arguments, stable wrapper bindings, and explicit snapshot
  reload scopes consistent as the component surface grows.
- Keep the single `<Children />` projection slot and `ChildrenBind` mutation API
  consistent with caller data contexts, diagnostics, ownership, optional
  component code-behind, and nested component behavior.
- Keep Form/component discovery candidates deterministic and preserve both
  registration provenances while retaining invocation and template paths in
  load failures.

## Data and performance

- Continue measuring binding reload, preset fan-out, item updates, virtual
  scrolling, native handles, and GDI resources with representative templates.
- Measure direct logical-index viewport scrolling and normal keyed root-condition
  fan-out separately on native .NET 2.0, including subscription cleanup in the
  Windows 98 guest.
- Add more diagnostics for realized controls, cache reuse, progressive batches,
  and expensive item functions.
- Preserve simple `ItemsControl` defaults while adding opt-in tuning only where
  measurements show a clear benefit.
- Extend regression coverage for reentrant events, failed setters, cancellation,
  disposal, and long-running virtualized lists.

## Compatibility

- Keep the runtime assembly within the .NET Framework 2.0 API and C# 2.0
  language surfaces.
- Validate documented behavior on current .NET Framework versions and targeted
  legacy environments.
- Expand capability-based fallbacks only where a native operating-system
  control lacks a required feature.
- Publish exact platform verification results without inferring runtime behavior
  from compile-only checks.

## Distribution

- Keep NuGet contents, symbols, XML documentation, release notes, and embedded
  sample resources reproducible from the repository.
- Verify every release from a clean consumer application rather than only from
  project references inside this repository.
- Maintain a Visual Studio 2005-compatible project alongside current validation
  and documentation tooling.
