# Performance model

WinFormsXaml keeps the general renderer predictable and adds faster paths only
when their behavior can be proven before a control tree is published. Every
runtime cache is bounded or scoped to one operation. A successful benchmark on
a current development machine is not evidence for Windows 98.

## Item-template construction

Eligible `ItemsControl.ItemTemplate` trees are compiled into a control
construction blueprint. The blueprint retains resolved control types and
constructors, writable CLR properties or events, safely shareable immutable
constants, names, dynamic value slots, and the exact child attachment strategy.
Creating another row can then construct the controls without cloning or walking
XML, looking up target properties/events by name, converting static property
strings, or running the generic child dispatcher again.

Blueprint selection is transparent. A template that uses a structural or
dynamic feature the compiler cannot preserve is rejected by the fast-path
probe before any control is created and uses the general renderer instead. The
fallback is a compatibility path, not an error and not a partially compiled
tree. Mapped XAML aliases, an applicable implicit or named resource style,
property elements, dynamic events, and constants that cannot be safely shared
are intentionally kept on that path so mapping, style precedence, event
lifetime, ownership, and error behavior do not change.

Each `ItemsControl` exposes allocation-free lifetime diagnostics for the two
successful construction paths. `ItemTemplateBlueprintBuildCount` counts rows
built from the precompiled plan; `ItemTemplateFallbackBuildCount` counts rows
built by the complete renderer. Reused, recycled, and owner-drawn rows do not
pretend to be new template constructions. `ItemControlTreeDisposedCount`
records actual retired native row trees, while
`ActiveItemBindingSubscriptionCount` scans the runtime's existing registration
index without allocating so Controls recycling and Lightweight snapshot cleanup
can be checked before and after a scroll scenario.

## Non-virtual native-control path

With `Virtualizing="false"` (the default), every item owns its complete native
control tree. Initial construction uses the compiled blueprint when the
template is eligible, progressive work yields to the message loop, and keyed
reloads retain unchanged trees. Progressive construction is transactional: the
current complete tree stays visible while detached replacement rows are built,
then the complete result is published once. A first row is never exposed while
the rest of the viewport is still being constructed. After publication, a pure
scroll does not walk the XML, reevaluate bindings or Functions, recreate
controls, dispose rows, or run the WinFormsXaml item layout engine.

Initial publication with `AutoScroll="false"` and append-only publication run
one complete item arrangement rather than repeating the same O(N) pass. A
scrolling host retains one extra first-publication pass because legacy
`ScrollableControl` can finish initializing its native range only after the
committed child bounds exist. Commits that retire controls also retain their
final repair pass because native scrollbar state can change during removal.
This keeps the fast cases narrow without weakening legacy range correctness.

Once that ordinary tree is fully committed, changing `Spacing`, `Orientation`,
or `AutoScroll` also re-lays out the existing controls without re-enumerating the
item source or starting a refresh transaction. The optimization is deliberately
disabled while a progressive refresh, rollback, reentrant update, provisional
source, or configured virtualization transition is active; those cases retain
the transactional reload path. A settled ordinary layout reuses its measured
visible count and skips the historical safety pass only when the native client,
display origin, viewport, scrollbars, direction, padding, and framework geometry
state are unchanged.

There is still a native scaling cost: every item root is an immediate child of
the `ScrollableControl`. WinForms moves the viewport with `ScrollWindowEx` and
then updates the bounds of every immediate child; Mono's compatible
implementation likewise walks the child collection. Therefore immediate
non-virtual scrolling is O(number of realized item roots), even though the
framework renderer performs no per-item refresh work. [Current WinForms
source](https://github.com/dotnet/winforms/blob/main/src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollableControl.cs#L736-L808),
[Mono source](https://github.com/mono/mono/blob/main/mcs/class/System.Windows.Forms/System.Windows.Forms/ScrollableControl.cs#L972-L995).

When bitmap presentation is not eligible, the native scrollbar retains
WinForms' normal `ScrollWindowEx` movement. The runtime synchronously flushes
only the child regions that WinForms already invalidated, preventing half-moved
text and blank rows without invalidating the complete retained list. A
framework-owned scrollbar is a separate sibling HWND outside the translated
item collection, so the same content move cannot move its track or arrow
buttons. The runtime updates only its thumb state.

An eligible non-virtual native or styled gesture with
`SmoothScroll=true` captures a bounded slice around the current viewport and
destination, then paints from that bitmap while leaving the complete live
control tree at one physical origin. The frame is one clipped unscaled
bitmap copy; compatibility scanning is cached for the committed item
publication. The cache is capped at 12 MiB per active host and is released
after the gesture. The default `SmoothScroll=false` path always moves the live
tree directly and never captures themed item content. The cache is deliberately
bypassed for focused content,
transparent hosts, wrapping, background images, Controls or Lightweight
virtualization, horizontal RTL, and native-hosted controls whose pixels cannot
be captured reliably. A hit test, focus entry,
resize, layout, item publication, range change, handle teardown, or disposal
commits the exact visible logical position synchronously before live controls
are exposed. The fallback is the normal retained-control scroll path, not a
different public rendering mode.

For native chrome, each cached frame changes only the non-client scrollbar
position through `SetScrollInfo`; it does not mutate `AutoScrollPosition` or
translate child windows. The styled path updates its fixed sibling thumb through
the same logical transaction. Both therefore perform one live-tree move at
settle while keeping their navigator synchronized throughout the animation.

Controls virtualization also recognizes a native or
styled pixel move that stays inside the same realized range: WinForms has
already translated those child windows, so the runtime publishes the new
logical viewport without repeating item measurement, slot positioning, or
scroll-extent reconciliation. A viewport change, invalid measurement cache,
new realization range, validation, or data patch deliberately returns to the
complete correctness path. `SmoothScroll` defaults to `false` for native-style
wheel and arrow behavior. Set it to `true` only when the interpolated
presentation is desired. For a small or medium list whose complete controls
must stay alive, the normal path is the simplest and most native choice. For a large list,
explicitly select Controls virtualization; for a compatible paint-only
template, Lightweight uses one owner-drawn host and removes the per-row
native-window cost.

On the Microsoft native owner-scrollbar path, eligible line and page messages
are intercepted before `ScrollableControl` performs its per-child move.
WinFormsXaml raises the normal virtual `OnScroll`/public `Scroll` chain and
publishes the bitmap destination; animated input retargets the existing timer,
while immediate input restarts only the short settle timer. This avoids moving
every row forward and then back before the transaction. Thumb, first/last,
end-scroll, axis-mismatched, child-scrollbar, and framework-themed scrollbar
messages keep their established paths.

Scrollbar styling is independent of row representation. Null/omitted
`VerticalScrollStyle` and `HorizontalScrollStyle` use native chrome and add zero
framework chrome work. A non-null style supplies framework-owned chrome only for
the axis selected by `Orientation`; it does not enable virtualization, change a
Controls range, or make an ineligible template eligible for Lightweight.
The styled path hides native chrome when layout or range state changes, then
keeps that hidden state cached. While framework chrome owns an axis, the host
rejects `WS_HSCROLL`/`WS_VSCROLL` additions at `WM_STYLECHANGING`; the native
bar therefore cannot alternate with the fixed custom bar between frames.
Legacy reconciliation remains as a bounded repair for implementations that
bypass the normal style-change message. `ItemsControl.ScrollBarGap`
changes host viewport geometry for either scrollbar renderer. Color-only style
changes repaint the scrollbar surface rather than reevaluating row bindings.

The custom scrollbar retains one brush for each paint role, one border pen,
and two three-point arrow buffers. Stable animation frames therefore create no
new GDI paint resources or arrow arrays; color-state changes replace only the
affected cached brush, and disposal releases every cached GDI object. The native
path returns before framework-bar synchronization when no style is active.

Empty, `false`, null, and unresolved dynamic style values are inactive and keep
the native path. Switching between an inactive value and a real style preserves
the logical offset. The custom bar is a sibling overlay rather than a child of
the scroll-translated viewport; `ScrollableControl` has no path that can move
its arrow or track geometry. The forbidden cross axis is suppressed before it
can create a one-frame client-size change.

With `SmoothScroll=false`, wheel, arrow, and page commands use the live tree and
publish the requested target immediately with either scrollbar implementation.
With `SmoothScroll=true`, they use the bounded presentation transaction and
publish the coalesced intermediate offsets required by one fractional,
velocity-continuous transition. Retargeting changes the destination without
restarting the motion, and rounded duplicate frames do not move content or
resynchronize the custom thumb. Value-only styled updates invalidate only the
union of the old and new thumb rectangles; the fixed arrows and track are not
repainted on every frame. Custom chrome keeps the same fixed bounds and z-order
for the complete gesture; it is not repositioned or raised on each frame.
Direct Controls
virtualization also holds its published extent and page denominator during a
styled smooth burst, retaining only the latest pending extent and publishing it
once when the gesture settles. Native thumb input is immediate on release;
tracking follows the operating-system live-content preference unless
`LiveScroll=true` forces it on. For a framework thumb,
`LiveScroll=true` publishes offsets while dragging; `LiveScroll=false` moves the
thumb without scrolling row content until release. The latter can reduce
repeated viewport work while selecting a distant position in a large list.

WinFormsXaml does not hide this tradeoff behind automatic virtualization. The
default preserves full `Control`, `Parent`, focus, accessibility, and event
semantics. A future one-presenter mode could make the host see one immediate
child, but it would change those public WinForms relationships and remains a
separate design and Windows 98 validation task.

## Virtualization backends

`ItemsControl` offers two deliberately different row representations:

| Mode | Representation | Choose it for |
| --- | --- | --- |
| `Controls` | A bounded visible/overscan set of normal WinForms control trees | Arbitrary templates, editors, buttons, custom controls, components, variable heights, and full markup behavior |
| `Lightweight` | One owner-drawn `ItemsControl` surface with row snapshots and hit testing | Large fixed-height lists made from the documented paintable subset |

Virtualization itself is disabled by default. With `Virtualizing="true"`,
`Controls` is the default backend, but its direct viewport activates only when
the logical count reaches `VirtualizationThreshold`. Below that threshold the
ordinary renderer still realizes every row and honors each template root's
desired size or explicit `Height`/`Width`; `FixedItemSize` and
`EstimatedItemSize` do not replace that geometry. Once active, Controls preserves
normal control behavior while avoiding one control tree per data item. The
direct viewport publishes a complete range synchronously and keeps detached rows
only as a bounded reuse hint. Its
`OverscanItems` setting retains the same `2*N` maximum extra-row budget while
biasing that budget toward the current scroll direction; no direction change
allocates a policy object or grows the configured window.

For variable-height Controls rows, measurement corrections retain an
item-and-intra-item viewport anchor. The styled scrollbar always derives its
range from the same layout-owned content extent and viewport used by logical
scrolling; native scrollbar convergence cannot replace that denominator. Each
frame publishes the current content origin and thumb position directly, with no
delayed settle timer. The final range repair is bounded by the smallest measured
row extent rather than a one-pixel worst case, preventing a small complex source
from being realized wholesale merely to prove viewport coverage.

Every direct virtual offset publication is one engine-owned layout transaction.
Native origin movement, range reconciliation, and row reuse retain their normal
measurement semantics, while nested parent layout requests raised only by final
row bounds and infrastructure z-order publication are suppressed. This prevents
one parent layout pass per moved child without weakening variable-height
measurement or animated `ScrollIntoView` retargeting. The item regression suite
exercises the same nested template across
nonvirtual/virtual and native/styled combinations with rapid forward, reverse,
retargeted, and wheel input. For real message-loop timing, run the interactive
benchmark with `--nonvirtual --smooth`, `--nonvirtual --smooth --styled`,
`--controls --smooth`, and `--controls --smooth --styled`.

`Lightweight` must be requested explicitly. Its first authoring profile is a
vertical, auto-scrolling list with a positive `FixedItemSize`. It accepts the
strict element and property subset documented in the
[ItemsControl guide](/guide/items-and-virtualization#use-the-explicit-lightweight-renderer-for-paint-only-rows).
Unsupported markup produces a source-located load error; it never silently
changes into a different renderer. Only the owner-drawn surface enables the
extra buffering style.

Lightweight prepares visible rows plus the same fixed `2*N` overscan budget as
Controls, biases it ahead only while scroll direction is known, and paints the
visible rows only. Observable dynamic slots share one source-only registration
per cached reactive row; a source change rebuilds that row rather than the
whole list. Visited-link state is destination-aware and capped at 256 entries
per host.

```xml
<ItemsControl ItemsSource="{Binding Notifications}"
              Virtualizing="true"
              VirtualizationMode="Lightweight"
              FixedItemSize="72"
              OverscanItems="2"
              AutoScroll="true">
  <ItemsControl.ItemTemplate>
    <Border Padding="8" BorderThickness="0,0,0,1">
      <StackPanel>
        <Label Text="{Binding Title}" />
        <CheckBox Text="Enabled"
                  Checked="{Binding Enabled, Mode=TwoWay}" />
      </StackPanel>
    </Border>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

## Explicit cross-item recycling

The `Controls` backend normally reuses a detached row only for the same item
identity and key. `ItemRecycling="Explicit"` additionally permits a cached row
to move to another item only when the template root implements
`IRecyclableItemControl`.

The runtime deactivates the old item subscriptions before calling
`TryPrepareForRecycle(ItemRecycleContext)`. Returning `false` rejects that row
and creates a fresh tree. Returning `true` opts into a full dynamic-slot patch
and subscription rebuild. An exception is visible to the caller and the failed
candidate is not published. Arbitrary controls are never inferred to be safe
for cross-item reuse.

Use the callback to reset state owned by the custom row itself, including
uncommitted editor values, validation adorners, focus-related flags, and any
event state that is not represented by markup bindings.

## Bounded runtime work

The runtime removes repeated work without retaining application graphs
indefinitely:

- preferred-size measurements are reused only inside one outer custom-layout
  pass and all control references are released when that pass ends;
- attached properties participate in their containing build or binding-refresh
  layout transaction instead of forcing a layout once per child. Dynamic
  layout bindings repaint the changed control and parent surface without
  recursively invalidating unrelated sibling subtrees; inherited visual values
  still invalidate descendants because those descendants can change;
- an unchanged `Controls` viewport measures and positions its published row
  snapshot with index-based scans, without cloning that snapshot or allocating
  per-row layout descriptors; range changes also use linear retained-row scans
  for ordinary bounded viewports and allocate lookup tables only for unusually
  large overscan ranges; its detached native-row cache is allocated only when
  that backend performs its first virtual realization. Direction selection is
  scalar arithmetic, and variable-height end correction shifts unused visible
  capacity backward in one viewport-bounded request instead of allocating or
  scheduling one repair per newly exposed short row;
- implicit-style matches and flattened `BasedOn` chains use small per-runtime
  bounded caches while preserving declaration and base-first order;
- exact, value-independent Function overload plans and event-forwarder
  factories are bounded and retain the existing overload/error behavior;
- parsed Binding, Function, Preset, and interpolation expression plans plus
  Function argument partitions are shared through bounded per-runtime caches;
- XAML element-type, partial CLR type-name, code-behind method-candidate, and
  reflected parameter lookups have bounded admission and are released with the
  runtime;
- repeated invariant framework value and enum conversions are bounded by type;
- successful partial embedded-resource lookups are bounded, and broad
  component registration streams unmatched XML roots without constructing a
  DOM;
- decoded `Image`, `Icon`, and encoded `byte[]` values use the shared bounded
  decoded-image cache and reference-counted runtime ownership. The lightweight
  backend may reuse a downscaled runtime-owned static conversion from a
  per-control cache capped at 16 entries and 65,536 pixels per entry; weak
  source keys, source/generation invalidation, and deterministic bitmap
  disposal prevent unbounded retention. Caller-owned or animated `Image`
  objects always bypass this thumbnail cache;
- exact `UniformToFill` painting uses one centered cover-crop draw rather than
  first painting `Zoom` and then repainting the same image.

For repeated rows, prefer binding the same cached `Image`, `Icon`, or encoded
`byte[]` value when several items display the same static asset. A string
`Source` intentionally keeps native `PictureBox.ImageLocation` behavior, so
each PictureBox owns its own path/URI load, failure, and animation lifecycle;
the runtime does not silently merge those requests.

Application-owned `Image` instances remain application-owned. Dispose a
runtime after its form tree is no longer needed so subscriptions, compiled
metadata, cached references, and runtime-owned native resources are released
deterministically.

## Measuring the real UI path

The headless benchmark is useful for deterministic comparisons but cannot
measure message-loop stalls. The separate
`benchmarks/WinFormsXaml.InteractiveBenchmarks` executable shows real forms and
can run the Controls, Lightweight, and explicit-recycling profiles separately.
It records:

- cold and warm time to the first fully presented frame;
- calibrated heartbeat delay during small and large scroll changes, including
  median, p95, and maximum latency;
- virtual row creation, retained reuse, detached-cache reuse, and disposal;
- blueprint and complete-renderer item-template construction counts;
- item-control disposal and active item-binding subscription counts;
- managed collection counts plus working-set and private-byte changes;
- repeated form open/close cycles;
- GDI and USER handle deltas only where `GetGuiResources` is supported
  (Windows XP or newer).

It never forces a garbage collection. Run it only after feature work is frozen,
record the exact OS, CLR, CPU, visual-style state, source/package identity, and
configuration, and keep current-Windows and Windows 98 results separate. See
the [validation contract](/reference/validation#interactive-performance-harness)
for the execution and reporting rules.

## Optimization guardrails

WinFormsXaml intentionally does not use these shortcuts:

- blanket double buffering across native controls;
- `GC.Collect` as a runtime or benchmark strategy;
- WinForms control construction on worker threads;
- unbounded image, XML, template, style, or control caches;
- cross-item control reuse without the explicit reset contract;
- cached Function results whose dependencies are unknown.

These techniques can hide a local symptom while increasing memory use,
breaking UI-thread ownership, or leaking state between rows—costs that are
especially visible on the .NET 2 CLR and legacy Windows.
