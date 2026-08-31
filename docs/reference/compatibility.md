# Compatibility

WinFormsXaml keeps its runtime surface intentionally small so the same XML and
C# application model can be used across a wide range of .NET Framework and
Windows versions.

## Shipping assembly

| Property | Contract |
| --- | --- |
| Target framework | .NET Framework 2.0 API surface |
| Language surface | C# 2.0 |
| Architecture | Any CPU managed assembly |
| UI technology | Windows Forms and GDI/GDI+ |
| Runtime dependencies | Framework assemblies only |

Applications targeting later .NET Framework versions can reference the same
assembly.

## Native controls remain native

Elements such as `Form`, `Label`, `TextBox`, `Button`, `PictureBox`, `ListView`,
and `DataGridView` are the corresponding `System.Windows.Forms` objects. This
preserves normal control APIs, accessibility behavior, event arguments, native
handles, and integration with existing WinForms code.

WinFormsXaml adds controls only where WinForms lacks the required behavior, such
as XML layout containers, repeated-item virtualization, and the marquee fallback
inside `ProgressBar`. The WPF-style `Image` element is deliberately a thin,
extensible `ImageControl : PictureBox`; it and native `PictureBox` therefore
share the native location/animation loader and optimized runtime source path.
Its exact `UniformToFill` cover crop is a direct GDI+ draw of that same image,
without a resized-image allocation or cache.

## Legacy Common Controls

Marquee progress is selected by the capability of the current application. If
`Application.RenderWithVisualStyles` is true when the native handle is created,
the version 6 Common Controls marquee is used. Otherwise WinFormsXaml drives the
built-in native Blocks control through a
grow/drain cycle. The empty parent owns the border, and a clipped, fully filled
private progress HWND supplies both directions from one identical native block
raster. This avoids phase-dependent end-cap and bevel differences without
owner drawing or adding a managed child to `Controls`. Consumer markup and
lookup remain the canonical
`ProgressBar` surface, including inherited `Style`, `MarqueeAnimationSpeed`,
`Minimum`, `Maximum`, and `Value` state. `PreferMarqueeFallback="true"` forces
the same path for previewing; its default `false` keeps capability selection.

Call `Application.EnableVisualStyles()` before creating forms when supported
systems should use native marquee. The fallback is designed for old Common
Controls configurations associated with
Windows 95, Windows 98, Windows Me, Windows NT 4.0, and Windows 2000. The
operating system must still have a compatible CLR capable of loading the
application. A control fallback cannot make an unsupported .NET Framework
installation supported. Verify each legacy operating-system, Common Controls,
and CLR combination directly in its target environment.

See [Legacy Windows compatibility](/guide/windows-98).

## Build-tool boundary

The library source is compiled against .NET Framework 2.0 references. Modern
.NET SDK and Node.js dependencies are used only for validation, documentation,
packaging, or continuous integration; they are not runtime dependencies of a
consumer application.

## Verification levels

Different checks answer different questions:

| Check | Evidence |
| --- | --- |
| C# 2.0 compile | The source stays within the configured language surface. |
| .NET 2.0 reference compile | Every referenced managed API exists in that profile. |
| Windows-native marquee process | After enabling visual styles before all control creation, a shown Form's progress HWND retains `PBS_MARQUEE`, accepts `PBM_SETMARQUEE`, and does not create fallback state. |
| Current-Windows behavior tests | XML, controls, binding, presets, items, layout, and resources behave on the tested current environment. |
| Target-system behavior tests | The complete application loads, paints, animates, scrolls, and releases resources on that exact OS/runtime configuration. |

A dedicated `WinFormsXaml.NativeMarqueeValidation` executable isolates native
marquee validation from the general test runner, which intentionally leaves
visual styles disabled to cover automatic fallback selection. The native process
prints `PASS` with exit code 0, `FAIL` with exit code 1, or a precise
unsupported-host `SKIP` with exit code 2. Repository verification accepts
`SKIP` locally; Windows CI requires `PASS`. Wine and Mono results are not
native-Windows evidence. This validation does not run or update the frozen
Win98 benchmark baseline.

See the [validation contract](/reference/validation) for commands, exit codes,
skip semantics, and the target-guest acceptance boundary.

A compile cannot establish the result of native control creation or painting on
another operating system. Test the produced application on every legacy system
claimed by the product that embeds WinFormsXaml.

## Threading

Create and update controls on the WinForms UI thread. Explicit binding reload
and preset mutation APIs are UI-thread operations. A `PropertyBinding<T>` may be
updated from a worker thread; it provides thread-safe value access and versioned
competing-update handling. The runtime coalesces dependent work, waits for
handle creation when necessary, and marshals updates to the runtime's WinForms
owner thread, through `RootControl` when one exists and through a private
dispatcher for a reactive non-Control root. Dispose every loaded runtime on the
thread that loaded it. A wrong-thread attempt is rejected before cleanup begins
so it can be retried safely on the owner thread.

## Resource ownership

Disposing a runtime detaches the markup event handlers and shared-preset
subscription it owns and releases runtime-owned images, icons, fonts, and helper
objects. It also disposes the root and native child tree that it created from
XML. Values supplied and owned by application code continue to follow normal
WinForms ownership rules. Disposing the runtime is therefore sufficient; a
separate root-control disposal call is unnecessary.

## Markup trust boundary

Markup and preset XML are application code. DTD declarations are prohibited and
XML external-entity resolution is disabled, but loading can still instantiate
types, assign properties, read local files or resources, and connect methods.
Do not load untrusted uploaded or network-provided markup directly.
