# Implementation plan

This plan keeps the runtime small and compatible while improving it in
responsibility-sized slices. A successful modern build is not treated as a
Windows 98 result.

"Canonical package syntax" below refers only to the public WinFormsXaml
markup/API spellings. It does not relax the C# 2.0, .NET Framework 2.0,
Visual Studio 2005, or Windows 98 compatibility requirements.

## 1. Standalone baseline

- [x] Extract the original single-file runtime into a Visual Studio
  2005-compatible library.
- [x] Split the source by major responsibility without changing the public
  layout, diffing, or virtualization paths arbitrarily.
- [x] Add an SDK-based .NET 2.0 reference-assembly validation build.
- [x] Add a C# 2.0 sample, dependency-free behavior runner, CI, and VitePress
  documentation.
- [x] Keep the VitePress content, Node version, package manifest, lock file,
  dependencies, cache, and output self-contained under `docs/`.
- [x] Build the documentation on pull requests and deploy the same artifact to
  GitHub Pages on `main` once the future repository enables Pages via Actions.
- [x] Add four focused C# 2.0 sample applications for bindings, presets, large
  item lists, and XML components/Flex, and include every sample XML file in the
  schema contract gates.
- [x] Initialize the local Git repository on `main`.
- [x] Add issue forms for correctness, features, and performance plus a pull
  request checklist that preserves the C# 2.0/.NET 2.0, canonical markup,
  documentation, XSD, resource-lifetime, and Windows 98 evidence contracts.

## 2. Requested public features

- [x] Retain and reload ordinary property bindings globally, by named subtree,
  or by one named property.
- [x] Add stable `PropertyBinding<T>` values with public change events,
  automatic one-way refresh, nested dependency rebinding, and WPF-like
  `Mode=TwoWay` control updates.
- [x] Observe ordinary .NET 2.0 `INotifyPropertyChanged` properties across
  one-way, nested, wildcard, two-way, component, preset, item, and
  `ItemsSource` paths, with pooled subscriptions and deterministic cleanup.
- [x] Add explicit `Source=Current` and `Source=CodeBehind` binding selection so
  nested item/component markup can use local data or shared Form state without
  named-control lookup, while preserving omitted-source behavior.
- [x] Add declarative `ItemsSource`, observable `ItemsBinding<T>` collections,
  bounded diff-based complete-list replacement, and reactive/two-way
  item-template properties without requiring named-control lookup code.
- [x] Add opt-in TwoWay `UpdateSourceTrigger=LostFocus|Explicit` with named and
  object-target commit APIs while preserving immediate writeback as default.
- [x] Add `ItemsBinding<T>.ReloadItems()` and the indexed `ReloadItem(index)`
  path so snapshot mutations and external Function state can refresh every
  observing host or one logical row without needless control recreation.
- [x] Add logical item/index `ScrollIntoView` APIs with Nearest, Start, Center,
  and End alignment; immediate or smooth movement; vertical, horizontal, and
  RTL geometry; progressive-refresh and queued-list-change deferral;
  single-dispatch worker-thread coalescing; measured/reflowed animation
  retargeting; and `ItemsBinding<T>` forwarding to every observing host.
- [x] Intercept eligible native owner-scrollbar line/page messages before the
  redundant immediate O(N) child move when smooth scrolling is enabled, while
  preserving Scroll callbacks, native physical values, RTL mapping, and all
  thumb/themed/pass-through behavior.
- [x] Add retained, non-virtual `ItemsControl` wrapping with row or column flow,
  item/line gaps, free-space justification, cross-axis alignment, item-root
  `FlexGrow`, resize reflow, and explicit rejection of wrapped virtualization.
- [x] Make dynamic `Condition` values reactive while keeping them independent
  from `Visibility` and combining component/template conditions predictably.
- [x] Include registered item-template bindings in all/subtree reloads while
  keeping preset refreshes on their narrower patch path.
- [x] Treat unresolved Binding paths and unknown Preset sets inside an item
  template as structured, source-located refresh failures instead of silently
  suppressing a child. A missing key in a known set is an optional markup value:
  keep the child, leave/reset that property to its normal default, and retain
  the reactive slot so adding the key later updates it in place.
- [x] Add inline, file, and embedded-resource presets with selected/default
  fallback and a C# mutation API.
- [x] Resolve a missing selected-preset key from the configured default only.
  Do not search unrelated presets. Markup leaves the target property at its
  normal default when both miss; strict C# `Resolve` still throws and
  `TryResolve` reports false.
- [x] Make preset imports transactional and define Merge, PreserveExisting,
  and Replace behavior.
- [x] Preserve runtime state when one manager is shared by several forms.
- [x] Coalesce batches of preset mutations into one UI refresh.
- [x] Resolve preset/binding expressions inside style setters.
- [x] Use independent clones of the executable icon as the default for Form
  roots, while retaining normal attributes/bindings and opt-out behavior.
- [x] Keep native marquee behind canonical `<ProgressBar>` whenever the
  application renders with visual styles; otherwise animate the built-in native Blocks control
  through block-sized grow/opposite-side-drain phases. Preserve the inherited
  Style/speed/range/value API, and allow `PreferMarqueeFallback="true"` to
  force that compatibility path for previewing.
- [x] Add root `Form` sugar, a lazy convention-based `XmlForm` base with
  `WinForm` and `Start()` conveniences, static `Class` code-behind metadata,
  partial embedded-resource paths, and bulk XML-component registration.
- [x] Give `XmlForm` cooperative lifetime-owned `RunThread` workers and a
  non-blocking `PostToUi` shortcut that stops accepting callbacks as Form
  shutdown begins.
- [x] Add optional per-invocation XML-component `Class` code-behind, stable
  declared-property proxy injection, the canonical `<Children />` projection
  slot, and public transactional `ChildrenBind` lookup/replace/wrap support.
- [x] Add an extensible `HyperlinkLabel` based on native `LinkLabel`, with a
  bindable WPF-style `NavigateUri`, automatic default-application navigation,
  schema completion, documentation, and a sample.
- [x] Add canonical WPF-style `<Image>` markup through an extensible
  `ImageControl : PictureBox`, retain native `<PictureBox>`, and keep both on
  the shared decoded-image cache and ownership path without resized copies.
- [x] Remove the undocumented `WindowStartupLocation` markup alias and keep
  native `Form.StartPosition` as the canonical Form terminology.
- [x] Remove the undocumented WPF `ResizeMode` alias and keep native
  `FormBorderStyle`, `MaximizeBox`, and `MinimizeBox` properties canonical.
- [x] Keep one canonical package syntax: `{Function ...}` for code-behind
  functions, a required `Value` attribute on preset `Set` elements, and a direct
  visual root inside `ItemsControl.ItemTemplate`, without backward-compatible
  markup spellings.
- [x] Remove package-level layout, color, preset-wrapper, and boolean import
  aliases while retaining the C# 2.0/.NET 2.0/VS2005/Windows 98 target.
- [x] Package an XSD 1.0 authoring schema and document standard Visual Studio
  IntelliSense association for forms, components, presets, and custom controls.
- [x] Report parsing and construction failures through structured
  `WinFormsXamlLoadException` source, element-path, property, and source-location
  fields while retaining the original inner exception. Keep parser locations
  unchanged; resolve semantic locations to the retained failing attribute
  position or deepest opening element and preserve them across item-template
  clones and registered-component `TemplateXml` round trips.
- [x] Make Form and component resource discovery list deterministic candidates
  on no-match and ambiguity failures, and report both resource/type provenances
  for same-name and global registration conflicts.

## 3. Correctness and performance audit

- [x] Compile the complete source with C# 2.0 syntax and the .NET 2.0 profile at
  warning level 4.
- [x] Document every public and protected runtime API and fail validation and
  package builds when a new publicly visible member lacks XML documentation.
- [x] Add focused tests for import rollback, preset fallback/mutation/batching,
  shared-manager behavior, ordinary reloads, style setters/events, app icons,
  marquee requested/native state, and shared decoded-image lifetime.
- [x] Detach runtime event subscriptions and dispose only runtime-owned native
  images/icons when values are replaced or the root is disposed.
- [x] Put pooled source, two-way target, and `ItemsControl` `IBindingList` event
  handlers behind disable-capable forwarders so partial adds and failed removes
  cannot retain active runtime or control-graph callbacks, and failed item-source
  replacements remain retryable.
- [x] Distinguish retryable disposal ownership from terminal external-remove
  errors, retry retained event-removal debt on later `Dispose` calls, keep those
  retries owner-thread-affine, and compact inert terminal item-source graphs.
- [x] Clear detached pending registrations when a synchronous TwoWay target
  commit drains an older queued source dispatch.
- [x] Make arbitrary `IComponent.Disposed` target hooks transactional and inert
  before source cleanup, with weak retry debt, stale-callback rejection, and safe
  synchronous or reentrant disposal handling.
- [x] Add regression coverage for layout containers and representative
  item-virtualization changes before modifying those hot paths further.
- [x] Replace the asynchronous virtual scheduler that activated at the default
  32-item threshold with a direct synchronous logical-index viewport, bounded
  realization, and a same-item cache used only as a reuse hint.
- [x] Make viewport virtualization explicitly opt-in, so an ItemsControl uses
  the ordinary renderer unless `Virtualizing="true"` is requested; align the
  runtime default, schema guidance, samples, documentation, and tests.
- [x] Re-audit the direct viewport against Mono ListView's fixed-position,
  synchronous visible-index invariants and add rapid forward/reverse,
  alternating-speed, thumb-jump, resize, end-clamp, and complex-row stress
  coverage that rejects any uncovered viewport gap.
- [x] Add a non-gating development-host benchmark for large item lists, virtual
  scroll jumps, preset fan-out, and repeated binding refreshes; keep native
  .NET 2.0 and Windows 98 measurements in the guest acceptance stage.
- [x] Complete the new allocation/rebuild/reflection hot-path audit and retain
  only optimizations with clear behavioral invariants and regression coverage.
- [x] Add a real-message-loop interactive performance harness for first paint,
  scroll latency, creation/reuse counters, managed/process deltas, and repeated
  Form lifetime cycles without forced collection or unsupported Win98 handle
  probes; expose construction-path, disposal, and active item-subscription
  diagnostics in the recorded scroll result.
- [x] Compile eligible item-template control trees into direct construction
  blueprints, and fall back before construction for every unsupported dynamic
  or structural feature.
- [x] Cache preferred-size measurements only within one outer custom-layout
  pass, and validate broad component registration by streaming unmatched roots
  instead of allocating a DOM for every embedded XML file.
- [x] Finish the explicit owner-drawn `VirtualizationMode="Lightweight"`
  backend, including its strict fixed-row authoring profile, diagnostics,
  schema, documentation, and source regression coverage.
- [x] Finish opt-in cross-item native-control recycling behind a complete public
  reset contract; never recycle arbitrary trees or suppress reset failures.
- [x] Remove the remaining transient allocation and repeated style/preset
  metadata work from common scroll and construction paths without introducing
  unbounded caches or changing overload/style precedence.
- [x] Continue manual responsibility-based splits where a file still contains
  more than one coherent subsystem.
- [x] Restore omitted properties and style-owned events when a dynamic named
  style changes, while preserving canonical local-value precedence.
- [x] Roll back failed/canceled item refreshes to the committed source, controls,
  virtual model, extent, and cache state.
- [x] Use reference identity for item-refresh control ownership and disposal
  deduplication, so distinct custom controls that override `Equals` remain
  independently owned; keep item-key matching value-based.
- [x] Reduce fixed-size viewport over-realization and normalize the two binding
  fan-out benchmark scenarios before recording the optimized snapshot.
- [x] Remove generic-reflection dispatch from retained `PropertyBinding<T>` hot
  paths while preserving its public API and atomic version semantics.
- [x] Index merged observable dependencies by source, eliminating accidental
  O(N^2) aggregate construction and whole-graph notification lookup while
  preserving ordered dependencies, exact/wildcard matching, and short-path
  fallback behavior.
- [x] Add an exact .NET Framework 2.0 warnings-as-errors compile gate for the
  runtime, runners, sample, and benchmark without executing them.
- [x] Add an offline schema-contract gate that treats schema read/compile
  warnings and errors as fatal, rejects fixture errors while allowing reported
  lax-extension warnings, requires globally declared fixture roots, validates
  source and restored XSD copies, and checks the clean PackageReference
  `None`/non-output projection.
- [ ] Re-run the documentation build and fresh local package/schema gate after
  the feature work is frozen.
- [ ] Run the complete behavior/layout/items runners and clean local package
  consumer after the remaining feature development is frozen.
- [ ] Run the benchmark smoke gate only when the benchmark freeze is lifted.

## 4. Windows 98 acceptance

- [x] Add an exact release-DLL build gate against the .NET Framework 2.0 RTM
  surface.
- [ ] Install and run the packaged sample in a clean Windows 98 guest.
- [ ] Verify binding/preset changes, determinate and marquee progress, executable
  icon extraction/override, and long virtual-scroll sessions visibly in-guest.
- [ ] Record handle/GDI counts during sustained refresh and scrolling.
- [ ] Mark only the guest-tested surface as Windows 98 verified.

## 5. Repository and package release

- [x] Use `WinFormsXaml` as the permanent public namespace and package identity.
- [x] Choose the MIT license and public GitHub visibility, then create the
  remote repository.
- [x] Add baseline NuGet metadata and a repeatable local pack workflow with no
  publish step.
- [x] Add the final license, project URL, and repository metadata.
- [x] Add a clean local-only package-consumer compile and smoke-test gate.
- [ ] Run the exact packed DLL through the Windows 98 acceptance gate.
