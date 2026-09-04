# Changelog

All notable user-visible changes will be documented here.

## Unreleased

- Treat a fully transparent `Form` `Background`/`BackColor` as an unset value.
  Literal, binding, and preset refresh paths now call the native
  `ResetBackColor()` behavior instead of failing through the WinForms setter;
  opaque colors remain reactive and can be applied again after the reset.
- Restored native-style immediate item scrolling as the default.
  `ItemsControl.SmoothScroll` now defaults to `false` in runtime metadata and
  the XSD; animation remains an explicit opt-in and can still be selected for
  `ScrollIntoView` per call.
- Preset keys that disappear from both the selected and configured default
  preset now call the native `ResetBackColor()` path for Control backgrounds
  and clear framework background-explicit metadata. Dark-only colors therefore
  cannot remain after switching to a Light preset that intentionally omits the
  key. Color conversion now also accepts every qualified `SystemColors.*` value
  and `Color.*` named value, including fully qualified `System.Drawing` forms.
- Fixed themed item descendants disappearing or duplicating during mouse-wheel
  input. `SmoothScroll="false"` now uses the live control tree directly for
  native and styled wheel, arrow, and page commands instead of presenting an
  incomplete `DrawToBitmap` snapshot. Native live paths synchronously flush
  only WinForms' already-invalidated regions before returning from the scroll
  message, and styled value changes repaint only the old/new thumb travel.
- Locked down incomplete preset transitions as selected value, configured
  `Default` value, then explicit unset. When a Dark-only key is absent from
  both Light and Default, retained direct attributes and style setters restore
  the pre-preset WinForms/framework baseline, including realized
  `ItemsControl` children; adding or selecting the key again reuses the same
  controls and reapplies it.
- Added reactive selected-preset Boolean expressions such as
  `{Preset Theme == Dark}`. Preset names compare case-insensitively; quoted
  names, preset-key Boolean operands, `==`, `!=`, `!`, `&&`, `||`, and
  parentheses work on every Boolean-compatible dynamic property, including
  `Condition`, `Enabled`, and `Checked`. Selection and referenced-key changes
  refresh only matching expressions, while unknown preset collections retain
  source-located markup diagnostics. The evaluator remains within the C# 2 and
  .NET Framework 2.0 compatibility surface.
- Fixed the source of framework-owned `ItemsControl` scrollbar jumps during
  rapid wheel, keyboard, arrow, page, and smooth-scroll input. Styled bars now
  use only the layout-owned content extent and viewport; they never alternate
  with transient native `ScrollProperties` values. Content and thumb publish
  their current logical position together, and the delayed range-hold timer has
  been removed instead of masking range changes until input settles.
- Redesigned styled scrolling on fully realized and virtualized complex item
  trees. The framework scrollbar HWND is now a fixed sibling overlay outside
  `ItemsControl.Controls`, so `ScrollableControl` cannot translate its track or
  arrow endpoints and no snap-back correction is required. Forbidden
  native-axis and cross-axis styles are rejected before WinForms can expose
  transient native chrome. Stress coverage verifies the native-parent boundary, stable overlay
  bounds, immutable arrow/track geometry, and monotonic thumb movement during
  rapid back-and-forth input.
- Added bounded deferred presentation for eligible nonvirtual native and styled smooth
  scrolling. One bitmap slice supplies intermediate frames while the live row
  HWNDs remain fixed. Styled chrome updates its framework thumb; native chrome
  updates only its non-client thumb through `SetScrollInfo`. The final logical
  position is then committed with one native tree move. Focus, hit testing,
  resize, layout, publication, unsupported
  native-host controls, and disposal leave the cache synchronously; virtual,
  lightweight, wrapping, transparent, and horizontal-RTL paths retain their
  existing correctness paths.
- Reduced styled gesture overhead by keeping scrollbar bounds and z-order fixed,
  skipping redundant rounded-frame publication and post-command range sync,
  and deferring direct-virtual extent changes until the gesture settles.
- Direct Controls virtualization now preserves an item-and-intra-item anchor as
  estimated row sizes are replaced by measurements. Its final correctness pass
  uses the smallest measured extent instead of a one-pixel assumption, keeping
  complex 40-row fixtures bounded rather than realizing the whole source.
- Reduced native and styled Controls-virtualization work for smooth and
  precision scrolling. When a pixel move stays inside the same realized range,
  the runtime now keeps WinForms' existing child-window translation and skips a
  duplicate measure, slot-position, and scroll-extent pass; viewport, range,
  validation, or layout-affecting changes retain the full reconciliation path.
- Empty, `false`, null, and unresolved `VerticalScrollStyle` or
  `HorizontalScrollStyle` values retain the native scrollbar. Runtime
  native/custom transitions preserve the logical offset and never expose both
  implementations as active chrome.

## 0.1.3 - 2026-08-25

- Fixed item scrolling while focus is inside rendered content. Unhandled
  Up/Down or Left/Right keys, Page Up/Down, Home, and End now scroll the
  nearest `ItemsControl`; editors and nested controls retain keys they consume.
- Unified framework-scrollbar arrows, pages, wheel input, and thumb geometry
  on the same logical range, with an explicit extent fallback when hidden
  native range state is unavailable on legacy WinForms implementations.
- Improved smooth scrolling with proportional sub-notch wheel input, a
  fractional velocity-continuous transition that retargets without restarting
  cubic easing, and no scrollbar synchronization for duplicate rounded frames.

## 0.1.2 - 2026-08-25

- Added retained flex-line wrapping to the ordinary `ItemsControl` renderer.
  `Orientation="Vertical"` fills rows with vertical overflow;
  `Orientation="Horizontal"` fills columns with horizontal overflow.
  `Spacing`, `JustifyContent`, `AlignItems`, item-root `FlexGrow`, AutoScroll,
  and effective RTL direction participate without rebuilding unchanged item
  controls. `Wrap="true"` with `Virtualizing="true"` is rejected explicitly,
  and the packaged XSD completes the new literal and dynamic-expression
  attributes only on `ItemsControl` and `FlexPanel`.
- Rebuilt `FlexPanel` measurement and arrangement on one reusable,
  control-independent logical-axis planner. Constrained preferred size now
  wraps like arrangement; collapsed and oversized leading children cannot
  create phantom gaps or empty lines; wrapping and `FlexGrow` use the same
  bounded basis; maximum-size caps redistribute remaining positive grow space;
  and explicit grow bases no longer absorb the prior arranged Bounds on later
  passes. RTL rows start from the right without reversing logical order, while
  wrapped RTL columns progress toward the left. Documentation now describes
  this as a CSS-like subset and explicitly notes that `flex-shrink` is not yet
  implemented.
- Added item-aware scrolling to `ItemsControl` and `ItemsBinding<T>`.
  `ScrollIntoView` supports logical `Nearest`, `Start`, `Center`, and `End`
  alignment on vertical, horizontal, and horizontal-RTL hosts across the normal,
  Controls-virtualized, and Lightweight renderers. Its default overload follows
  `SmoothScroll`; the explicit `animate` overload can force animation on or off.
  `ItemsBinding<T>.ScrollIntoView(item)` selects the first equal occurrence,
  while `ScrollIndexIntoView(index)` addresses duplicates exactly and broadcasts
  to every observing host through weak, disposal-safe observers. The retained
  `ScrollToIndex` API remains an immediate leading-edge operation.
- Completed the packaged XSD contracts for the framework-owned scrollbars and
  `TabView`: `Minimum`, `Maximum`, and `Value` now retain integer/expression
  IntelliSense on `VerticalScrollBar` and `HorizontalScrollBar`, and all twelve
  public TabView appearance-change events are explicitly completed instead of
  falling through the lax custom-control extension boundary.
- Added bounded comparison and logical expressions to complete one-way
  `{Binding ...}` targets, including `Condition`. Paths, parentheses, strings,
  finite numbers, booleans, `null`, `!`, numeric relational operators,
  non-coercing equality (`===`/`!==` aliases, with numeric CLR cross-type
  comparison), `&&`, and `||` are supported with deterministic precedence.
  Every operand resolves and subscribes eagerly;
  computed bindings reject `Mode=TwoWay`, calls, indexers, arithmetic,
  assignment, and ternaries, and enforce length/token/depth limits. The guide,
  reference, package README, and XSD document XML-safe `&lt;` and `&amp;&amp;`
  authoring.
- Added the framework-owned `TabView`/`TabViewItem` tab surface. Header and
  content backgrounds, foregrounds, borders, per-edge thickness, padding, and
  spacing are styleable through literals, bindings, functions, presets, and
  resource styles without native `TabControl` chrome or application
  `DrawItem` handlers. Direct items and `TabView.TabItems` share one mutable
  read-only collection property; selection supports `SelectedIndex`,
  `SelectedItem`, two-way bindings, and consolidated old/new notifications.
  Effective `FlowDirection`/`RightToLeft` inheritance works across forms,
  containers, and registered components: RTL mirrors header placement and
  physical navigation without reversing logical collection/index order, and
  live direction reloads relayout existing headers.
- Added reusable `<Includes>` composition for registered, embedded-resource,
  and file-backed XML fragments, including nested content, local styles,
  presets, registered components, and visual children. Include source metadata
  is static and resolved before bindings; `XmlForm.Include` queues ordered
  sources before the first lazy-load access. Resource and same-name preset
  declarations in one receiving owner scope merge in document order;
  registered lookup has deterministic
  assembly/global ambiguity checks, source diagnostics retain the include
  chain, and cycle/depth/expansion guards bound composition. The authoring XSD
  and project documentation cover the same canonical grammar.
- Made `ItemsControl` virtualization explicitly opt-in. Omitted
  `Virtualizing` now stays on the ordinary renderer at every item count, while
  `Virtualizing="true"` retains the Controls/Lightweight backends and threshold
  tuning. Rapid alternating-scroll coverage now exercises fixed and complex
  variable-height rows, resizes, thumb jumps, reversals, and end clamps.
- Removed the redundant second full item-layout pass from non-scrolling initial
  and append-only non-virtual commits, and stopped building the old-control
  identity table when the initial snapshot is empty. Auto-scrolling first
  publication and commits that retire controls retain their legacy native-range
  stabilization pass. Fast immediate and smooth-scroll regression coverage now
  proves that the ordinary path does not measure, rebuild, dispose, or replace
  rows while the viewport moves.
- Fixed zero-height holes after cached Controls rows were reused: detaching a
  row no longer erases its measured bounds before an explicit-height template
  is laid out again. `StackPanel.Spacing` is also applied and measured as its
  documented nonnegative main-axis gap.
- Preset keys now resolve from the selected preset and then the configured
  default, without searching unrelated presets. A known-set markup miss leaves
  or resets the target to its normal default while retaining the live slot;
  strict C# `Resolve` still throws and `TryResolve` reports false. Missing
  binding members and unknown preset sets remain source-located failures.
- Added `ItemsControl.ScrollBarGap` for spacing between repeated content and
  either its native or framework-owned vertical or horizontal scrollbar,
  including RTL. Styled frames now verify and remove native chrome before each
  frame paints, without entering the item layout pipeline.
- Removed per-child layout requests from attached-property application and
  narrowed dynamic layout-binding repaint work to the parent surface plus the
  affected controls. Inherited visual-property changes retain recursive
  invalidation, while item-preset patches invalidate only changed row subtrees
  and repaint the host itself only when row layout changed.
- Added the explicit `ItemsControl.VirtualizationMode="Lightweight"` backend:
  fixed-height vertical Border/StackPanel/Label/CheckBox/HyperlinkLabel/Image rows
  are painted on one surface with visible-only painting, direction-aware fixed-
  budget overscan preparation, and hit testing. Final eligibility is
  attribute-order-independent even without an ItemsSource, and rejected live
  configuration refreshes restore their prior policy. PropertyBinding/INPC and
  explicit Function-path dependencies now rebuild only the affected cached row
  through one aggregate registration that is detached at retirement. Visited
  links use prepared destination-aware keys and deterministic oldest-first
  eviction at 256 entries per host. Unsupported templates fail with
  source-location diagnostics instead of silently creating control trees.
  Repeated downscaling of runtime-owned static Icon/encoded-byte conversions
  now uses a weak-source, 16-entry thumbnail
  cache with a 65,536-pixel entry ceiling and deterministic eviction/reset
  disposal; caller-owned mutable `Image` instances, animated images,
  transformed paint surfaces, and upscaled paths remain uncached.
- Added opt-in `ItemsControl.ItemRecycling="Explicit"` for the Controls
  backend. Cross-item reuse is attempted only for detached template roots that
  implement `IRecyclableItemControl`; the runtime deactivates old bindings,
  requires an explicit reset decision, reapplies every compiled dynamic slot,
  and never publishes or pools a rejected or failed tree.
- Made both halves of the legacy Marquee fallback reveal the same 100%-filled
  native Blocks HWND while an empty parent owns the one border. Growing and
  draining frames now have identical block size, end-cap shading, and inner
  bevel instead of switching between two differently rendered progress values.
- Removed success-path allocation from unchanged direct-viewport layout,
  stopped cloning the published record snapshot during range changes, delayed
  cleanup-error storage until an error exists, and use linear retirement scans
  for ordinary bounded viewports. The detached native-row cache is now created
  only when a Controls viewport first performs virtual realization, instead of
  by every ItemsControl. Scroll direction now redistributes the existing
  `2*OverscanItems` budget without allocating or increasing it. Transaction-local
  staging remains in place across callback-capable work to preserve reentrant
  publication safety.
- Bounded runtime method, parameter, event, XAML-type, resolved-type,
  expression-plan, and Function-argument metadata caches. Preset, binding,
  Function, and interpolation paths now share the compiled expression plans,
  and runtime disposal releases all retained plan/type entries.
- Added TwoWay `UpdateSourceTrigger=LostFocus|Explicit` while preserving
  immediate `PropertyChanged` writeback as the default. Explicit targets can be
  committed by name or object through `UpdateBindingSource`, and deferred
  triggers continue receiving source changes normally.

- Added an all-or-nothing compiled control blueprint for eligible
  `ItemsControl.ItemTemplate` trees. It pre-resolves parameterless Control
  constructors, writable CLR properties or events, shareable immutable static
  constants, names, and exact child-attachment strategies, while preserving
  binding, Function, preset, diagnostics, event-lifetime, and ownership
  behavior. Eligible rows avoid generic target-property/event lookup, static
  string conversion, and child dispatch. Mapped aliases, applicable resource
  styles, and other unsupported semantics select the existing renderer before
  row construction; later component registration invalidates an older plan.
- Added per-`ItemsControl` lifetime counters that distinguish successful
  compiled-blueprint row construction from the complete renderer, and made the
  interactive Controls fixture blueprint-eligible so the measured path is
  explicit rather than inferred.
- Added item-control-tree disposal and current active item-binding subscription
  diagnostics, and report both around the interactive scroll scenario so cache
  eviction and recycling cleanup are observable.
- Reuse successful `GetPreferredSize` results inside one outer custom-layout
  pass for Grid, StackPanel, FlexPanel, DockPanel, Canvas, Border, Viewbox, and
  ScrollViewer. The bounded allocation-free hit path is keyed by control
  identity and proposed size, releases all control references in `finally`, and
  never carries measurements into a later pass. Redundant child bounds writes
  are skipped as well.
- Added bounded Function invocation plans for value-independent exact overload
  matches. Repeated calls reuse the selected method and parameter metadata and
  pass already-compatible argument arrays directly; value-sensitive string,
  numeric, enum, and converter paths retain the original per-value overload
  selection behavior.
- Made broad embedded-component registration inspect each matching XML stream
  before allocating a location-preserving DOM. Non-Component documents are
  validated to end-of-file and ignored without building a tree; Component
  documents are reopened for the existing full validation and diagnostics, and
  exact resource registration remains strict.
- Added bounded implicit-style match plans per effective resource scope and
  control/XAML type. Repeated controls no longer rescan every implicit style;
  source-list growth invalidates that scope, original declaration order is
  retained, and disposal releases all cached scope and Type references.
  `BasedOn` chains are likewise resolved and cycle-checked once per named-style
  scope, then replayed base-first without repeated dictionary traversal.
- Reuse bounded invariant string conversions for framework value types and
  enums. Repeated static attributes and resolved preset strings now avoid
  repeated enum/TypeConverter parsing while custom reference values, Images,
  Fonts, and arbitrary custom value converters retain per-assignment behavior.
- Added a separate interactive .NET 2 performance harness with a real WinForms
  message loop. It records cold/warm first-presented-frame time, calibrated
  heartbeat and scroll latency distributions, virtual creation/reuse counters,
  managed/process resource deltas, and repeated Form open/close cycles. It
  never forces collection and queries GDI/USER counts only on Windows XP+.
  Controls, Lightweight, and explicit-recycling row profiles can now be run
  separately with the selected profile and cross-item reset counters recorded.
- Replaced the timer/progressive virtual-range scheduler with a direct,
  synchronous logical-index viewport. Scroll and resize operations realize the
  visible range plus fixed-budget direction-aware overscan immediately, stage
  entering controls before atomic publication, and use a bounded same-item cache
  only as a reuse hint. Initial and stationary ranges remain symmetric. The
  retained tuning surface is `Virtualizing`,
  `VirtualizationThreshold`, `OverscanItems`, `EstimatedItemSize`,
  `VirtualizationCacheItems`, and `FixedItemSize`; removed
  `RecycleVirtualItems`, `DirectionalOverscan`,
  `CriticalViewportBufferItems`, and `CriticalViewportMaxPasses`. The design
  takes high-level inspiration from Mono `ListView`'s logical
  `VirtualListSize`, visible retrieval, and cache-hint separation, but is an
  independent XML-template implementation and does not claim feature parity.
- Corrected direct-viewport range and layout geometry for padded hosts and
  horizontal right-to-left lists, made variable-size estimate correction
  bounded and viewport-batched, and reduced direct native layout convergence
  from the normal renderer's nine possible passes to two. Root collapse through
  markup, bindings, components, or effective styles now selects the normal
  keyed renderer before a partial direct range can be published; `Hidden`
  continues to retain its layout slot.
- Fixed persistent blank areas after rapid mixed-height virtual scrolling.
  Bounded measurement correction now retains the union of ranges visited,
  detects native scroll-origin clamps caused by extent changes, repairs final
  visible-range coverage, shifts unused end-of-source capacity backward in one
  viewport-bounded batch, and repositions controls against the settled native
  origin before returning from the scroll event. The final guard reserves the
  worst-case positive row count, so even extreme estimate-to-one-pixel changes
  cannot publish an uncovered native viewport.
- Dispose an XmlForm-owned native Form tree before marking its XamlRuntime
  state disposed. Active virtual ItemsControl controls can now retire during
  Form.Close or direct runtime disposal without a close-time
  ObjectDisposedException; repeated disposal remains idempotent.
- Added allocation-free ItemsControl diagnostics for the current realized
  range, retained-range reuse, detached-cache reuse, newly created virtual
  trees, and normal progressive batches. The monotonic counters support legacy
  performance runs without requiring XP-only GUI-resource APIs.
- Compiled and bounded event-forwarder factories by delegate type. Repeated
  XML event wiring now avoids revalidating delegate signatures, rebuilding
  closed generic forwarder types, and reflecting their `Invoke` method while
  preserving the existing detachable event-lifetime wrapper.
- Parse embedded Form XML directly from its manifest stream, removing the
  intermediate full-text allocation. Successful normalized partial resource
  queries now use a thread-safe cache bounded globally, per assembly, and by
  query length; misses keep the existing complete diagnostics.
- Made item-template `Image.Source` and `PictureBox.Source` detect an encoded
  `byte[]` mutated in place when `ItemsBinding.ReloadItem`, `ReloadItems`, or an
  equivalent explicit item refresh runs. Fingerprints are retained only on
  relevant render slots; unchanged rows keep their controls and decoded bitmap,
  while changed shared sources decode once and retire the previous bitmap after
  its last owner moves.
- Added protected `XmlForm.PostToUi(MethodInvoker)` as a lifetime-aware,
  asynchronous companion to `RunThread`. It queues only through a live Form
  handle, suppresses callbacks after shutdown begins, and removes the common
  need for raw `Control.BeginInvoke` without introducing synchronous-invoke
  deadlocks.
- Reject malformed binding paths with empty member segments instead of silently
  changing their meaning; the documented single-dot whole-context binding
  remains supported.
- Added repository-ready bug, feature, and performance issue forms plus a pull
  request checklist tailored to XML reproductions, .NET 2 compatibility,
  schema/docs synchronization, resource lifetime, and honest Windows 98
  verification evidence.
- Cached preset-change subscriber snapshots when handlers change,
  eliminating an invocation-list allocation from every direct or deferred
  preset mutation while preserving reentrant multicast and failure isolation.
  Mutations performed before any runtime subscribes now also skip unused event
  data allocation outside notification deferrals.
- Expanded `Mode=TwoWay` target-change coverage to mutable geometry,
  multi-line text/RTF, DataGridView viewport properties, the WebBrowser
  `Source` alias, and the RichTextBox `IsReadOnly` alias by routing each value
  through its reliable native WinForms event. Properties without a dependable
  public change signal remain rejected instead of silently behaving one-way.
- Made `Image Stretch="UniformToFill"` an exact WPF-style, centered cover crop.
  It retains PictureBox URL loading and animated-image updates while drawing the
  existing source directly, so painting and resizing create no derived bitmap
  copies. Direct `ImageControl.Source` assignment now also retires an obsolete
  `ImageLocation`, preventing `Source=null` from reloading the old URI.
- Made equal mapped image locations true reload no-ops, preserving pending and
  completed native loads. The weak decoded-image cache now shares `Icon`
  conversions, detects in-place byte-array mutations with an allocation-free
  fingerprint, and drops all remaining weak bookkeeping when its runtime is
  disposed. Externally disposed bound image controls now release their generated
  image ownership even when hostile binding-event cleanup fails; shared images
  remain alive until the final live target releases them.
- Made `ItemsBinding<T>.Replace` safe under synchronous reentrancy. A newer
  replacement supersedes the remaining operations of an older diff, while a
  newer request that fails during enumeration or comparison no longer leaves a
  valid outer replacement partially applied.
- Replaced the per-identity occurrence queues in reference-type
  `ItemsBinding<T>.Replace` planning with one compact encoded index chain.
  Duplicate and null occurrences retain their original deterministic order;
  value types keep the established equality-callback path.
- Made an attached `ChildrenBind.Replace` with the same control references in
  the same order a true no-op. It now skips snapshot publication, `Changed`,
  child-index writes, layout, and invalidation while retaining normal lifecycle,
  owner-thread, and input validation.
- Indexed registered-component property declarations once at registration and
  captured supplied invocation attributes in one pass. Component instances and
  item-template component-condition evaluation no longer rescans every XML attribute
  for every declared property.
- Made preset imports reject unknown attributes, misspelled child elements,
  unexpected content, and unrelated wrapper children before changing live
  state. Removed and replaced preset handles are retired, and semantically
  identical `Replace` imports preserve object identity without triggering a
  broad UI refresh.
- Routed every mapped `PictureBox`/`ImageControl` source assignment through one
  ownership transaction. If a synchronous native or application callback
  throws after an image setter commits, the installed generated image is still
  tracked and external images remain application-owned.
- Made mapped `ImageControl.Source` changes raise `SourceChanged` exactly once.
  Equal reloads stay silent, native `PictureBox` is unaffected, and reentrant or
  throwing handlers retain correct runtime-owned image lifetime.
- Removed the filled green outline and the grow/drain bevel mismatch from the
  legacy marquee fallback. One native progress HWND now owns the outer border
  for the complete cycle; the second HWND is a fully filled Blocks surface
  clipped strictly to the track interior when a right-anchored segment is
  needed. Native background and bar-color messages are mirrored to that
  surface. The fallback advances at one-third its original cadence while
  native marquee and inherited speed readback remain unchanged.
- Included the release Windows PDB beside the .NET 2 DLL in the NuGet package
  and made both pack workflows reject missing, empty, Mono, or portable symbol
  files. The Bash workflow can also host Roslyn inside Windows `dotnet.exe`
  under Wine while forcing C# 2 and explicit .NET 2 references.
- Made the default Bash/Wine pack reproducible without ephemeral compiler
  paths. Its first run restores pinned Microsoft Roslyn and .NET 2 reference
  packages into the ignored `artifacts/toolchain/pack` cache; later runs reuse
  that validated cache while retaining explicit compiler overrides.
- Completed XML documentation for every public and protected runtime member and
  made missing API documentation a warnings-as-errors failure in classic,
  validation, and package builds. Added an Actions-based GitHub Pages deployment
  job while keeping the complete VitePress workspace under `docs/`.
- Hardened the VitePress development and preview servers to loopback-only
  access, loopback CORS, and a disabled editor-launch endpoint while the latest
  stable VitePress dependency graph awaits upstream advisory fixes. Added
  grouped weekly Dependabot checks for the docs toolchain and GitHub Actions.
- Fixed `<Object Type="...">` and `<Control Type="...">` so their static type
  selector is consumed as construction metadata instead of being reapplied as
  a nonexistent property on the newly created object.
- Added canonical WPF-style `<Image>` markup backed by the public, extensible
  `ImageControl : PictureBox`. It provides an aspect-preserving Uniform default
  and mapped `Source`/`Stretch` authoring while native `<PictureBox>` remains
  unchanged. Both controls reuse image objects, the bounded identity-based
  `byte[]` decode cache, reference-counted runtime ownership, and native stretch
  rendering without per-size bitmap copies. XML resolution, docs, the
  HelloWorld sample, and focused .NET 2.0 regressions are included.
- Added the extensible `HyperlinkLabel` control. It keeps the native
  `LinkLabel` appearance and `LinkClicked` event, adds a bindable WPF-style
  `NavigateUri`, and opens that destination through the operating system's
  default application. A cancellable `RequestNavigate` event receives the URI
  captured at activation. The XML element, link appearance properties, events,
  IntelliSense schema, documentation, sample, and no-browser regressions are
  included.
- Fixed a synchronous TwoWay-dispatch cleanup edge case in which a detached
  binding could remain in the pending queue after an owner-thread target edit
  stole an older queued source dispatch.
- Added `ItemsBinding<T>.ReloadItems()` and the indexed
  `ReloadItem(index)` fast path. Snapshot-item or external Function changes can
  now be refreshed directly from the binding; whole reloads retain keyed
  controls, while indexed notifications reach every observing item host and
  enable a requested-row-only patch wherever it is safe.
- Completed reversible mapped-property coverage for `IsTabStop`, `Foreground`,
  and `Background`; TwoWay bindings now listen to the corresponding native
  `TabStop`, `ForeColor`, and `BackColor` change events. Non-conventional
  WinForms edit notifications such as selection, calendar, tree, and tab
  changes now use their reliable native events as well. Owner-thread target
  edits commit before later WinForms interaction handlers such as `Click`, while
  sibling repainting remains coalesced.
- Exposed the canonical XML item host as top-level
  `WinFormsXaml.ItemsControl`, while retaining the established nested runtime
  base type for compatibility. Bare `ItemsControl` declaration-only fields in
  `XmlForm` code-behind and the shipped ItemsExplorer sample now resolve to the
  actual XML-created type without an alias.
- Added protected `XmlForm.Presets`, `ReloadBindings(...)`, and
  `ReloadBinding(...)` forwarding helpers so common preset changes and explicit
  snapshot refreshes no longer require routing every call through `Ui`.
  Canonical auto-wired `Name` fields now show an explicit `= null` initializer,
  avoiding the compiler's unassigned-field warning without an eager lookup.
- Made `ItemsBinding<T>(IList<T>)` copy its initial values instead of retaining
  and later mutating the caller-owned list. Removed rendered rows now retire
  their complete binding record so item subscriptions detach deterministically,
  and equal-but-distinct custom Controls keep reference-identity ownership.
- Made an explicit `Value` path segment work when the current binding source is
  itself a `PropertyBinding<T>`, propagated preset changes through nested
  component proxies before handle creation, and normalized semantic XML
  diagnostics to the opening `<` coordinate.
- Replaced the prefix-greedy `ItemsBinding<T>.Replace` reorder planner with a
  bounded longest-increasing-subsequence plan. Large left/right rotations now
  publish one `ItemMoved` instead of overflowing into a reset while preserving
  duplicate, null, reference-identity, value-equality, replacement, and
  enumeration-transaction semantics.
- Added exact bounded `IBindingList` event snapshots. Add, delete, move, and
  replacement batches avoid source enumeration before continuing
  through the existing transactional keyed renderer. Reset, oversized,
  malformed, stale, or otherwise unverifiable batches keep the full-reload
  fallback.

- Expanded the packaged XSD with typed IntelliSense for canonical expressions,
  pixel/Auto dimensions, distinct flow-direction enums, row/column grid
  definitions, collection and resource property elements,
  preset/component constraints, and the compatible progress-bar surface.
  Bindable Boolean, enum, numeric, color, layout, selection, and object value
  types retain literal suggestions while also accepting `{Binding ...}`,
  `{Function ...}`, and `{Preset ...}` strings; `CheckBox.Checked` and
  `CheckState` are covered by the schema contract regression.
  Element completion now covers every public, concrete .NET Framework 2.0
  `Control` with a public parameterless constructor, plus ListView subitems and
  the remaining constructible DataGridView header-cell objects.
  ProgressBar styles and non-negative marquee speed now validate against their
  actual runtime contracts while reflection-discovered application markup stays
  available through the documented lax extension boundary.
  Complete typed expressions are now lexically checked, element-specific
  `Format`, `AutoSizeMode`, and integer range properties no longer share
  incompatible literal vocabularies, floating CLR properties accept exponent
  notation, and native `LinkLabel.Links`/`Link` markup is declared.
- Reused a bounded per-assembly manifest-resource-name snapshot across Form
  discovery and component glob registration, avoiding repeated array creation
  and manifest enumeration on common `XmlForm` and `Register` paths.
- Cached registered CLR component constructor and parameter metadata at
  registration, reused the first component lookup during instantiation, and
  removed empty attached-binding allocations from static component calls.
- Removed the serialize-and-reparse round trip for projected component children
  while preserving caller source coordinates. Rejected non-Control projections
  now release runtime-owned objects, and direct disposal of a projected nested
  component removes stale logical and `ChildrenBind` ownership before teardown.
- Reused normalized style target names and allocated `BasedOn` cycle state only
  for styles that actually inherit, preserving base-first setter order.
- Removed per-item reference-key wrapper allocations from the bounded
  `ItemsBinding<T>.Replace` matcher while preserving identity, null, duplicate,
  and value-type matching rules.
- Made an equal explicit `ItemVersionPath` token the only application-level
  shortcut that declares ordinary item bindings unchanged. Without that
  contract, normal, direct-virtual, and observed `BindingList` refreshes now
  evaluate the compiled binding slots directly instead of recursively
  reflecting and hashing the item object graph.
- Compiled `ItemTemplate` styles and preset declarations once per template
  instead of reparsing each row clone. Template resources now use an isolated
  lexical scope derived from their declaration context, and nested
  `ItemsControl` instances retain that scope for deferred realization without
  leaking named or implicit styles into runtime-wide collections.
- Indexed binding-heavy template clones in one XML traversal, then reused that
  same per-row table for native control targets instead of repeatedly walking
  sibling paths or allocating a second lookup map.
- Skipped binding, Function, and preset parser setup for ordinary static markup
  values that contain no opening brace.
- Reused immutable `PropertyBinding<T>` listener snapshots and cached Function
  parameter metadata across repeated evaluations. Reference-type
  `ItemsBinding<T>.Replace` planning now reads the live identity-only list
  without first copying it, while value types retain the isolated snapshot
  required for reentrant equality. Targeted reloads, preset-expression scans,
  and item/preset refreshes also avoid redundant resolution and empty snapshots.
- Deferred layout until the completed object tree's ordered layout pass instead
  of forcing a duplicate `PerformLayout` call after every XML element.
- Avoided empty retained-binding collections for static elements and made
  targeted binding reloads use their existing property index without allocating
  a temporary scan list; repaint deduplication state is now allocated only when
  a non-layout binding actually needs it.
- Removed per-resolution uppercase-string allocations from case-insensitive
  preset cycle tracking while preserving the original set and key spellings in
  cycle diagnostics.
- Kept `ProgressBar` as the only canonical progress element suggested by the
  packaged schema; it still creates the compatibility-aware implementation
  automatically. Named lookup uses the ordinary `Get<ProgressBar>` API; the
  removed compatibility-specific lookup helper is no longer part of the
  consumer surface.
- Renamed the primary public runtime type to `WinFormsXaml.XamlRuntime`, so
  consumers can use `XamlRuntime` directly after importing the package
  namespace. The former public runtime type is not retained as an alias, and
  the `XmlForm` native-form convenience property is now `WinForm`.
- Added declaration-only `XmlForm` field wiring for XML `Name` values. Private
  and inherited writable reference fields are populated before `OnLoaded` and
  restored only when they still contain the runtime-assigned object.
- Added `XmlForm.RunThread` with immediate background-thread startup,
  cooperative stop state, user-close deferral, bounded retryable disposal, and
  lifetime pairing for supplied as well as XML-created `XmlForm` code-behind.
- Made `UseApplicationIcon` a true low-precedence Form default. Explicit
  `Icon` literals, bindings, and style setters now win independently of XML
  attribute order; reactive opt-in/opt-out changes preserve that ownership, and
  the executable icon is not extracted when an initial opt-out or explicit icon
  already makes the fallback unnecessary.
- Made `PropertyBinding<T>.ValueChanged` notify every subscribed listener even
  when an earlier listener throws, while still reporting the first failure to
  the assigning caller. This prevents an application listener from starving
  the runtime's retained XML binding listener.

- Added the standalone `WinFormsXaml` .NET Framework 2.0 library with a modular
  source layout and the public `WinFormsXaml` namespace.
- Added embedded-form loading and a paired `MainForm.cs` /
  `MainForm.xml` sample structure.
- Fixed nested embedded preset sources to resolve from the assembly containing
  their loaded markup before falling back to the code-behind or entry assembly.
- Added one canonical `<Children />` slot to embedded XML components.
  Invocations accept zero or more visual children; projected markup keeps the
  caller Form/item context, namescope, event target, bindings, ownership, and
  source diagnostics, while the surrounding template keeps its declared-property
  and optional per-invocation `Class` code-behind context. Public `ChildrenBind`
  provides scoped lookup and transactional replace, clear, and wrap operations;
  same-slot recursive `Changed` mutation is rejected without blocking changes
  to independent components. A code-behind `Children` member is reserved only
  for templates that declare the slot. Direct disposal of a nested component
  root now releases its names, bindings, events, owned values, root index, and
  code-behind immediately without disposing the native root twice.
  Embedded preset imports in cross-assembly component templates prefer the
  component resource assembly without mutating root markup provenance.
- Made the XSD `Component.Property.Required` contract literal-only, matching the
  runtime's `true`/`false` validation instead of suggesting dynamic expressions.
- Added `XamlRuntime.Form`, the lazy `XmlForm` code-behind base with `WinForm`
  and `Start()` conveniences, static form `Class` metadata, and bulk embedded
  component registration. Parameterless `XmlForm` classes load
  `Derived.Type.FullName.xml` from their own assembly by convention, while
  explicit constructors accept manifest names or partial paths.
- Removed the undocumented `WindowStartupLocation` markup alias. Native
  `Form.StartPosition` is now the canonical spelling, matching Form-only
  terminology.
- Removed the undocumented WPF `ResizeMode` markup alias. Native
  `FormBorderStyle`, `MaximizeBox`, and `MinimizeBox` now form the only public
  markup surface for Form resize behavior.
- Standardized package markup on `{Function ...}` and
  `<Set Key="..." Value="..." />`; the older Binding-shaped function forms
  and preset inner-text values are rejected instead of retained as aliases.
- Standardized item markup on `ItemsControl.ItemTemplate` with its visual root
  directly inside it. The old `Template`, `DataTemplate`, and
  `ItemsControl.Template` aliases and nested-wrapper forms are rejected.
- Removed the remaining package compatibility spellings: `WrapPanel`,
  `FlexPanel.FlexGrow`, `TextColor`, `Color`, `BackgroundColor`, `BorderColor`,
  property-element preset wrappers, and boolean `replaceExisting` preset import
  overloads. Use `FlexPanel Wrap="true"`, `FlexGrow`, `ForeColor`/`BackColor` or
  `Foreground`/`Background`, `BorderBrush`, direct `<Presets>`, and
  the explicit preset XML import mode. This does not change C# 2.0, .NET 2.0,
  VS2005, or Windows 98 compatibility.
- Moved the complete VitePress toolchain under `docs/`, including its Node
  version, package manifest, lock file, dependencies, caches, and generated
  output. Repository and CI commands now run the site through `npm --prefix
  docs` without installing documentation dependencies in the root.
- Made Form and component discovery diagnostics actionable: no-match errors
  list deterministic embedded XML candidates, same-name component batch
  collisions identify both resources, and global registration conflicts report
  both the existing and attempted CLR/XML origins.
- Made true exact-cased manifest resource names win before case-insensitive
  convenience matching. Equally ranked Form fragments now use deterministic
  resource-name order; case-only non-exact component-registration ambiguities
  list deterministic candidates instead of depending on manifest enumeration.
- Made explicit `XmlForm` resource constructors accept deterministically ranked
  partial paths while retaining the exact parameterless convention and
  resolved manifest names in markup diagnostics.
- Made component fragment registration tolerate mixed resource folders:
  well-formed non-`Component` XML is ignored and a matched batch that retains no
  components is a no-op. Exact single-resource registration remains strict,
  while malformed XML or malformed components still reject a fragment batch
  atomically.
- Added parameterless `XamlRuntime.Register()` and empty-fragment registration
  to inspect every embedded `.xml` resource in the selected assembly, retaining
  only `Component` roots.
- Made `XmlForm` compatible with conventional property-change notification
  models without making that verbose pattern the recommended authoring API.
- Added pooled, member-filtered `INotifyPropertyChanged` bindings for one-way,
  nested, wildcard, two-way, component, preset, item, `ItemsSource`, and
  worker-thread notification paths. Normal CLR properties use standard
  last-write-wins behavior; `PropertyBinding<T>` retains atomic versioned
  conflict handling.
- Made observable bindings work for non-Control roots as well as Forms and other
  Controls. Same-owner updates use a non-recursive pump; worker notifications
  use a private WinForms dispatcher, native-handle recreation revokes stale
  posts by epoch, and nonreactive non-Control roots allocate no dispatcher.
  Loaded-runtime disposal is owner-thread enforced and retryable without a
  partial teardown or a late-dispatcher publication race.
- Added case-insensitive binding-source selection with `Source=Current` and
  `Source=CodeBehind`. Nested item and component markup can now reach shared
  code-behind state without named-control lookup, including reactive,
  interpolated, two-way, virtualized, cache-resumed, and preset-backed paths;
  omitted `Source` retains the existing current-context behavior.
- Made complete retained function expressions observe explicit notifying path
  arguments using the same current Form, item, or component-local context used
  to evaluate those arguments. Zero-argument functions and state read only
  inside a method remain explicit reload boundaries.
- Hardened pooled source, two-way target, and `ItemsControl` `IBindingList`
  subscriptions with disable-capable forwarders. Event accessors that store and
  then throw during add, or throw during remove, can retain only inert delegates
  rather than the runtime or control graph. Observable source attachment is now
  published before a reentrant add accessor and deferred cleanup runs after that
  accessor returns. Failed old-source removal leaves the requested item source
  unattached and retryable.
- Hardened dynamic bindings on arbitrary `IComponent` targets against synchronous
  disposal, reentrant cleanup, stale callbacks, and hostile `Disposed` accessors.
  Failed detachments retain inert, weakly held retry debt without blocking a
  later binding on the same target or retaining its control tree.
- Kept nested two-way edits intact across endpoint replacement by excluding
  queued notifications from detached `INotifyPropertyChanged` branches during
  replay arbitration.
- Indexed merged observable dependencies by source, removing the accidental
  O(N^2) whole-aggregate equivalence scan from item-condition construction and
  routing notifying-member lookup through the relevant source bucket while
  retaining ordered dependencies and the linear short-path fallback.
- Indexed the dynamic bindings and `ItemsControl` templates that may consume
  presets, so scoped preset changes skip ordinary bindings and item hosts before
  running the existing exact, transitive dependency checks. Component-property
  cascades, template replacement, target disposal, and runtime disposal keep the
  narrower indexes synchronized without changing refresh order.
- Reduced allocation in common `ItemsControl` update paths: unique keyed items
  now avoid one FIFO queue per old record while duplicate keys retain O(1)
  ordering, coalesced realized-property patches transfer their detached slot
  batch without copying it, refresh/model arrays use their known final sizes,
  unchanged records do not allocate a throwaway Function cache, and direct
  virtual scrolling searches only the configured tail of the detached
  same-item cache. The `ItemVersionPath` value captured while the logical model
  is prepared is reused during viewport realization instead of resolving the
  same application token twice.
- Added serializable `WinFormsXamlLoadException` diagnostics with markup source,
  deepest element path, property name, and XML source line and position. Parser
  locations remain unchanged; semantic locations identify the exact retained
  attribute position or fall back to the deepest opening element. Original
  locations survive item-template clones and registered-component `TemplateXml`
  round trips.
- Preserved the originating markup source, element path, property, line, and
  position for retained bindings so failures raised later by `ReloadBinding`,
  `ReloadBindings`, or reactive refreshes remain structured
  `WinFormsXamlLoadException` diagnostics without double-wrapping an existing
  structured load failure.
- Prohibited DTD declarations in Form, component, and preset XML in addition to
  disabling external resolvers, preventing internal entity-expansion work from
  consuming runtime resources before markup construction begins.
- Added a packaged XSD 1.0 authoring schema with Visual Studio IntelliSense
  associations for forms, components, presets, common native controls, layout,
  items, bindings, properties, and events.
- Completed authoring coverage for the runtime-owned `ToolTip` mapping and the
  integer `Panel.ZIndex` attached property, alongside canonical native Form
  resize properties (`FormBorderStyle`, `MaximizeBox`, and `MinimizeBox`).
- Added offline `XmlSchemaSet` compilation for source and restored package
  schemas. Schema read/compile warnings and errors are fatal; fixture errors are
  fatal while lax-extension warnings remain allowed, and fixture roots must be
  globally declared. The clean-consumer gate checks the flattened
  `None`/non-output contract.
- Added global registration for typed C# controls and embedded XML components,
  including constructor values, typed/default properties, nested forwarding,
  and binding reloads.
- Added retained global property-binding reload APIs.
- Added `PropertyBinding<T>` with thread-safe `Value`, `ValueChanged`, automatic
  one-way refresh, and validated `{Binding ..., Mode=TwoWay}` target updates.
  Two-way bindings support native properties plus the reversible text, checked,
  enabled, and read-only markup aliases, and detach with their owning lifecycle.
- Made nested two-way endpoints deterministic under simultaneous source and
  target changes: the newest edit wins, endpoint replacement replays to the new
  terminal, and re-entrant equal-version changes cannot be mistaken for binding
  feedback.
- Added declarative `ItemsControl.ItemsSource` and the .NET 2.0-compatible
  `ItemsBinding<T>`. Notification-capable `IBindingList` changes are coalesced
  onto the owner thread, including pre-handle changes, while ordinary
  `IEnumerable` sources retain the manual reload workflow.
- Made `ItemsBinding<T>.AddRange` snapshot its source before mutation, making
  self-add deterministic and preventing partial appends when source enumeration
  fails while retaining the single-reset notification contract.
- Added `ItemsBinding<T>.Replace` for complete next snapshots. It makes
  self-replacement an O(1) no-op, keeps enumeration failure transactional,
  skips identical reference/value sequences, and emits deterministic item-level add, remove,
  replace, and move notifications for small diffs (including duplicates).
  Diff work is bounded, with one reset for large unrelated changes, so refresh
  cost cannot degrade into an unbounded quadratic path. Existing
  `INotifyPropertyChanged` subscriptions remain active for retained items;
  non-notifying same-instance mutations can use inherited `ResetItem` after
  advancing an explicit `ItemVersionPath` token when one is configured.
- Added coalesced per-item, per-property reactive patches for realized item
  templates. Notifying member and wrapper changes update only the affected slot
  without enumerating the source or reading sibling items, with full reload
  retained for structural and component-boundary changes.
- Optimized coalesced `IBindingList.ItemChanged` notifications for realized
  same-instance records, including safe direct-virtual rows. The runtime now
  plans and patches only the changed indices, absorbs duplicate reactive-slot
  work, and skips source enumeration and unchanged siblings. Adds, deletes,
  moves, resets, replacement objects, structural root conditions, unrealized
  rows, and rebuild-only templates retain the full transactional refresh path.
- Split item rendering, virtualization, reactive slots, refresh transactions,
  and template compilation into focused partial-class files while preserving
  the public API and classic Visual Studio project.
- Added reactive one-way `Condition` values for ordinary elements, registered
  components, and item templates. Conditions compose with each other and with
  `Visibility`; dynamic false elements remain retained so they can become
  visible without manual element lookup.
- Made an item-template or registered-component root `Condition` select the
  normal keyed renderer, because it can remove the root and break the direct
  viewport's one-logical-item/one-slot invariant. Descendant conditions remain
  eligible for direct virtualization and retain ordinary reactive updates.
- Made direct virtual cache records deactivate their binding slots while
  detached and reactivate them only for the same item and key. A structural
  dirty signal discards the hint and compiles a fresh tree, so cache state never
  becomes logical correctness state.
- Extended structured markup diagnostics through compiled item binding and
  registered-component expansion, retaining the exact template or component
  source element, property, line, and position. Circular component chains now
  identify the repeated resource element instead of
  escaping as an unstructured exception.
- Added deterministic cleanup when a retained non-root binding target or a
  registered component root is disposed externally.
- Added shared dynamic presets with inline, file, embedded-resource, mutation, and live-refresh support.
- Made `<Set Key="..." Value="..." />` the required preset syntax. Preset
  values can evaluate bindings,
  functions, and nested preset references; typed `PropertyBinding<T>` results
  refresh consumers automatically with runtime-local subscriptions, transitive
  preset invalidation, cycle detection, and deterministic disposal.
- Removed the obsolete misspelled preset-container alias; `<Presets>` is the
  sole public container element.
- Prevalidated preset imports before live mutation, added explicit import modes
  and one broad post-commit notification, and preserved shared-manager state
  across forms.
- Isolated shared preset-manager subscribers so every runtime observes each
  completed mutation before the first callback failure is rethrown. Preset
  refresh now also covers non-Control roots, skips unrelated component
  properties, memoizes transitive dependency checks per refresh, and imports
  XML strings without invoking equality code on stored application objects.
  A failed refresh retains its merged dependency scope without an automatic
  retry loop; parameterless `ReloadBindings()` explicitly retries it, while a
  later preset mutation receives one fresh attempt.
- Added an automatic marquee progress fallback behind canonical `<ProgressBar>`
  markup and the ordinary native `ProgressBar` API. The inherited `Style` and
  `MarqueeAnimationSpeed` properties remain the single read/write state. The
  legacy path strips the unsupported marquee style at the handle boundary and
  drives native Blocks through grow and opposite-side drain phases. A clipped
  unmanaged empty progress child creates the reverse phase without owner
  drawing or polluting managed `Controls`.
- Added canonical `PreferMarqueeFallback`: false selects the fallback only
  when the application is not rendering controls with visual styles, while
  true forces it for previewing. Capability selection is evaluated for the
  native handle without relying on a cached direct Common Controls DLL query.
  Removed the superseded `LegacyMode` enum/property/markup syntax.
- Stopped the legacy progress timer for determinate, paused, hidden, disabled,
  and pre-handle states instead of retaining a permanent base-property poll.
  The timer advances one normalized phase per tick, so paused and recreated
  handles retain their phase without elapsed-time catch-up jumps. A classic
  native-track geometry approximation determines the step size, with a
  one-hundred-step cap spread across very wide tracks, and repeated cycles skip
  a duplicate empty-frame delay.
- Added default executable-icon behavior for forms with ordinary override support.
- Added runtime ownership tracking for generated icons and shared decoded images,
  plus replaceable/detachable markup event registrations.
- Added a Visual Studio 2005 project, current compatibility-validation projects,
  runnable samples, and complete VitePress guides.
- Added four focused Visual Studio 2005 sample applications: reactive and
  two-way bindings, inline/embedded/file/mutable presets, simple-to-virtualized
  item lists, and typed XML components with caller content and Flex layout. All
  sample XML is now discovered automatically by the schema gates.
- Expanded the packaged XSD with native marquee values, form-icon opt-out,
  complete canvas anchors, canonical flex layout, and item refresh events.
- Expanded XSD completion for usable .NET 2 collection children (`TreeNode`,
  ListView records and columns, ToolStrip status/progress items, and concrete
  DataGridView columns), `DataGrid`, `BindingNavigator`, image/browser
  `Source`, progress fallback mode, column/item properties, common native
  events, and shared CLR enum-name surfaces.
- Added a dependency-free C# 2.0 behavior runner covering the requested feature
  paths without introducing a runtime/test-framework dependency.
- Added separate deterministic layout and ItemsControl runners for container
  geometry, keyed updates, virtualization, cancellation, and failure handling.
- Added a dependency-free benchmark runner and recorded an initial development-
  host baseline for item refresh, virtual scrolling, binding, and preset fan-out.
- Added a 10,000-row reactive item-condition benchmark scenario with
  source-indexed full-list rotation timing, sampled pooled-subscription and
  post-disposal cleanup guards; performance results remain unrecorded until the
  benchmark gate is run.
- Fixed refreshable item property-element bindings, nested-template data-context
  boundaries, preset-driven root conditions, canceled progressive patches,
  offscreen force-rebuild caching, and pre-handle worker-thread access.
- Deferred declarative item rendering until the complete ItemTemplate and
  virtualization configuration are loaded. Templates whose root membership can
  be removed by a preset or binding now use the normal keyed renderer rather
  than publishing an unstable logical viewport.
- Fixed live preset values inside item-template style setters, dynamic layout and
  inherited-color refresh classification, stale bindings from inactive styles,
  and local-value precedence when a dynamic style changes.
- Made dynamic style replacement restore omitted setters to the implicit/native
  value, preserved native-property local precedence, and detached only event
  handlers owned by the replaced style.
- Made item-source refreshes transactional across enumeration, planning,
  progressive build, cancellation, and disposal. Direct viewport realization
  stages incoming ownership before atomic publication, and destructive cache
  eviction occurs only after a forced refresh succeeds.
- Made item-refresh control ownership and disposal deduplication use reference
  identity, so distinct custom controls that override `Equals` remain
  independently owned and disposed. Item-key matching remains value-based.
- Moved the complete ItemsControl type, binding-expression helpers, and item
  layout logic into cohesive files without changing their public API.
- Made native property/style updates transactional across callback-visible
  metadata, compensating setters, and runtime-owned image, icon, and font values.
- Reconciled runtime-owned values with the value actually installed by reentrant
  setters, and indexed ownership by target identity, case-insensitive property,
  and shared-value reference count instead of repeated linear scans.
- Cached immutable parsed registered-component templates per runtime while
  deep-importing an isolated tree for each build and item-condition plan,
  with replacement-aware invalidation and focused benchmark coverage.
- Indexed reactive dispatch by pending registration, bucketed repeated
  property/event and item-member reflection by runtime Type, made duplicate-key
  item reuse FIFO O(1), cached item-template text for preset fan-out, avoided
  direct-preset traversal allocations, and bounded color/thickness parse
  retention.
- Bounded process-wide binding-path, binding-member, property/event reflection,
  and observable target-property caches. Once full, these caches keep their
  established hot entries and resolve novel types or names without retaining
  them instead of growing indefinitely or clearing the whole hot set.
- Serialized ordinary item rollback against reentrant refresh requests and kept
  failed rollback retry state separate from real binding values. Direct
  viewport generations abort stale staged work without replacing the last
  committed range.
- Reworked markup event replacement as a per-event lifecycle so custom add/remove
  accessors, recursive disposal, and failed-removal retries cannot retain a live
  code-behind callback or overwrite a newer handler.
- Indexed bound-event bookkeeping by target reference identity, avoiding global
  key/revision and target-selection scans in event-heavy control trees; added a
  focused construction/cleanup benchmark scenario.
- Bounded repeated Font lookup with weak cache entries and completed mapped-
  property style restoration for metadata-only and target-specific XML
  properties.
