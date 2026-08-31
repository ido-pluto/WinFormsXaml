# Compatibility tests

`WinFormsXaml.Tests` is a C# 2.0 console runner with no test-framework
dependency. It exercises preset transactions and batching,
embedded loading and relative paths, ordinary and reentrant binding reloads,
the `XmlForm` property-notification helper, pooled `INotifyPropertyChanged`
one-way/two-way/nested/item binding graphs, explicit current/code-behind
binding sources, reactive function-argument paths in Form and item contexts,
structured markup diagnostics,
registered C# and XML components including caller/item-context content slots
and cross-assembly component preset provenance,
dynamic-style replacement and implicit fallback, mapped-property local-value
precedence, native-setter compensation, markup-event accessor reentrancy,
arbitrary `IComponent.Disposed` accessor cleanup and retry behavior,
application-icon defaults, shared image lifetime, and the marquee fallback's
capability selection, native Blocks frame mapping, and requested/native state.
It creates controls but does not show a form, and exits
with a nonzero code after any failure.

`WinFormsXaml.NativeMarqueeValidation` is a separate C# 2.0 process for the
supported Windows-native marquee branch. Its `[STAThread]` entry point calls
`Application.EnableVisualStyles()` before constructing any control, creates a
shown Form with a `CompatibleProgressBar`, and verifies the actual HWND has
`PBS_MARQUEE`, accepts `PBM_SETMARQUEE`, and has not activated the fallback
renderer or mask window.
It prints one machine-readable result and exits with the corresponding code:

| Result | Exit code | Meaning |
| --- | ---: | --- |
| `WINFORMSXAML_NATIVE_MARQUEE: PASS` | 0 | The enabled version 6 Common Controls path was exercised in a shown Form. |
| `WINFORMSXAML_NATIVE_MARQUEE: FAIL` | 1 | The host advertised the native path but the control did not retain it. |
| `WINFORMSXAML_NATIVE_MARQUEE: SKIP` | 2 | The direct host cannot provide Windows native visual styles or marquee. |

The normal test runner deliberately does not enable visual styles, so it keeps
covering automatic fallback selection in a different process. `Verify.ps1`
accepts a precise native-marquee skip for unsupported local hosts; CI uses
`-RequireNativeMarquee`, which turns that skip into a failed gate. Wine and Mono
are never accepted as Windows-native evidence. CI does not assume capability
from the operating-system label alone: a runner with disabled theming reports
`SKIP`, and the required-capability gate fails visibly.

`WinFormsXaml.LayoutTests` contains dependency-free C# 2.0 checks for
deterministic layout behavior. It loads public markup and checks exact control
bounds for Grid, StackPanel, DockPanel, Canvas, Border, right-to-left layout,
and layout-affecting binding reloads. Its fixtures use fixed panel geometry
rather than text or native preferred sizes, so the assertions do not depend on
fonts, visual styles, or DPI. It never calls `Show` or starts a message loop.

`WinFormsXaml.ItemsTests` contains deterministic checks for keyed reuse and
patching, version-token shortcuts, transactional forced rebuilds and
virtual-cache eviction, reference-identity ownership for custom controls,
clearing, property-element bindings, nested-template boundaries, preset-driven
structural conditions and style setters, bounded viewport realization,
repeated progressive rollback failures, committed source/model/geometry
preservation, root visibility ordering, post-commit failures, reentrant
completion/failure/source-enumeration/planning, runtime-disposal cancellation
and queued reactive-patch cleanup, pre-handle worker-thread rejection, and
enumeration/planning failure reporting. It also covers direct item-template
construction blueprints, strict fixed-row `VirtualizationMode=Lightweight`
final eligibility/configuration rollback, directional overscan preparation,
row-scoped observable reactivity, bounded visited links, visible-only painting,
hit testing, TwoWay checkboxes, image ownership, and opt-in cross-item Control
recycling with accepted, rejected, throwing, and rollback paths. The
progressive checks advance batches directly, so they verify transaction behavior
without depending on timer timing.

Most checks deliberately avoid message-loop timing. The Items runner has one
focused shown-form exception for styled smooth scrolling: it drives rapid wheel
input while the real WinForms timer is active and records every published
content/thumb frame. The application configuration permits the current .NET
Framework CLR for CI while retaining a CLR 2.0 fallback for the legacy
environment. A current-Windows pass is therefore not Windows 98 evidence:
after-handle cross-thread marshaling, prolonged interactive scrolling, native
painting, handle/GDI pressure, and guest-specific behavior still require the
separate target-guest acceptance gate.
