# Legacy Windows compatibility

WinFormsXaml is a general-purpose XML UI runtime for Windows Forms. Its primary
API is the same on current and legacy Windows; compatibility fallbacks are
selected internally when an operating-system control does not provide the
requested capability.

## Runtime profile

The shipping assembly targets the .NET Framework 2.0 API surface, uses C# 2.0
syntax, and depends only on framework assemblies. Applications can use the same
library from .NET Framework 2.0 through later .NET Framework versions.

Operating-system compatibility also depends on whether a usable .NET Framework
runtime is installed on that system. A successful build against .NET 2.0
reference assemblies proves the managed API surface, not that a particular
legacy machine can install or run that framework.

## Marquee progress fallback

Some legacy Common Controls versions do not implement the native marquee style.
This affects Windows 95, Windows 98, Windows Me, Windows NT 4.0, and Windows 2000
configurations without a sufficiently capable Common Controls implementation.

Markup still declares a normal `ProgressBar`:

```xml
<ProgressBar Name="Loading"
             Style="Marquee"
             MarqueeAnimationSpeed="35"
             Minimum="0"
             Maximum="100" />
```

C# lookup stays on the normal WinForms type:

```csharp
ProgressBar loading = Get<ProgressBar>("Loading");
loading.Style = ProgressBarStyle.Marquee;
loading.MarqueeAnimationSpeed = 35;
```

Common native members and APIs remain available. The fallback keeps an ordinary
native Blocks progress control. An empty native parent owns the single visible
border, while both the growing and draining phases reveal complementary parts
of the same 100%-filled native Blocks child. This keeps the block size,
end-cap shading, and inner bevel identical in both directions. The child is an
unmanaged HWND, is clipped to the inner track, and does not appear in managed
`Controls`. There is no owner drawing.

When `Application.RenderWithVisualStyles` is true at handle creation,
WinFormsXaml leaves the native marquee path in use. Otherwise the control
provides the native Blocks grow/drain cycle. Call
`Application.EnableVisualStyles()` before creating forms on supported systems
that should use native marquee. Determinate `Blocks` and `Continuous` modes continue to use
the normal WinForms behavior, and the inherited logical range and value are
restored by the native handle when returning to determinate mode.

`PreferMarqueeFallback="true"` forces the fallback on a capable machine for
previewing. Its default `false` uses the fallback only when native marquee is
unavailable. The fallback advances at one-third the requested cadence by using
three times the inherited `MarqueeAnimationSpeed` interval; the native marquee
path still receives the property value unchanged. `MarqueeAnimationSpeed="0"`
pauses without resetting the phase.

Windows 98 does not provide the later `WS_EX_LAYOUTRTL` behavior needed to
reverse a native progress control reliably. WinFormsXaml therefore maps both
left-to-right and `RightToLeftLayout` phases with the normalized native parent
position and clipped empty child rather than depending on that window style.

Detection is capability-based rather than tied only to one Windows product
name. It therefore also covers applications that do not activate version 6
Common Controls, systems whose user/client visual styles are disabled, and
unsupported legacy Windows versions.

The repository validates the opposite, supported branch in a separate
Windows-native process. That process enables visual styles before constructing
any control, shows a Form, and verifies the actual progress HWND keeps
`PBS_MARQUEE`, accepts
`PBM_SETMARQUEE`, and never creates the fallback mask. A direct host without that
capability reports `WINFORMSXAML_NATIVE_MARQUEE: SKIP`; Windows CI requires a
`PASS`. This current-Windows check is not evidence for the Windows 98 fallback,
which still requires target-guest validation.

## Executable icon as the form default

A root `Form` starts with the icon associated with
`Application.ExecutablePath`:

```xml
<Form Name="MainForm" Text="My application" />
```

Set the normal executable icon in the application project:

```xml
<PropertyGroup>
  <ApplicationIcon>app.ico</ApplicationIcon>
</PropertyGroup>
```

An explicit native `Icon` property or binding overrides the default:

```xml
<Form Name="MainForm" Icon="custom.ico" />
```

```xml
<Form Name="MainForm" Icon="{Binding CurrentIcon}" />
```

`UseApplicationIcon` has fallback precedence: a local `Icon` literal, binding,
or style remains authoritative in either XML attribute order, including when
the directive changes reactively.

Use `UseApplicationIcon="false"` to skip the runtime's executable-icon
assignment and leave the native `Form` default behavior in place.

## Designing for low-resource systems

The public API does not change, but a few choices are especially helpful on old
hardware:

```xml
<ItemsControl Name="Results"
              ItemKeyPath="Id"
              ItemVersionPath="Version"
              Virtualizing="true"
              FixedItemSize="48"
              OverscanItems="2"
              VirtualizationCacheItems="8" />
```

- Prefer fixed-size rows when the design permits it.
- Keep item templates shallow and reuse images and fonts.
- Use stable keys and cheap version values for frequently refreshed lists.
- Keep file, network, and decoding work away from the UI thread.
- Measure native handle and GDI-object counts during long scroll and refresh
  sessions.

## Verification boundary

Legacy support is verified per operating system and runtime configuration. The
strongest useful checks are performed inside the actual target environment:

- application and library assembly load;
- embedded XML and preset-resource loading;
- control creation, layout, bindings, events, and preset changes;
- determinate and marquee progress painting;
- executable icon extraction and explicit overrides;
- repeated-item scrolling with no blank viewport regions;
- stable handle and GDI resource use during sustained activity.

Current-Windows tests and a strict .NET 2.0 compile verify useful parts of the
managed contract, but they do not replace guest-visible testing on Windows 95,
Windows 98, Windows Me, Windows NT 4.0, or Windows 2000.

See the [validation contract](/reference/validation) for the exact native-runner
results, repository-gate switches, and legacy guest checklist.
