# Interactive performance harness

This separate .NET Framework 2.0 WinForms executable measures paths that the
headless benchmark intentionally cannot observe:

- cold and warm time to the first fully invalidated and presented Form frame;
- small scroll changes and large `ScrollToIndex` jumps through a selectable
  10,000-row Controls, Lightweight, or explicit-recycling template;
- real `WM_MOUSEWHEEL` dispatch through a bounded 192-row non-virtual tree of
  nested native controls, including a deterministic shared in-memory image;
- calibrated UI-heartbeat delay plus median, p95, and maximum synchronous
  scroll cost;
- realized, created, retained, and detached-cache virtualization counters;
- compiled-blueprint versus complete-renderer construction counters;
- item-control disposal and active item-binding subscription counts;
- Gen 0/1/2 collections, working set, private bytes, and repeated Form
  open/close cycles;
- GDI and USER object deltas on Windows XP or newer only.

The harness never calls `GC.Collect`. It calibrates the WinForms timer before
scrolling because the legacy Windows timer resolution is not the same as a
modern Windows timer. `GetGuiResources` is never called on Windows 98, Me, or
Windows 2000.

Build `WinFormsXaml.InteractiveBenchmarks.csproj` with Visual Studio 2005 or
build `build/WinFormsXaml.InteractiveBenchmarks.Validation.csproj` with the
repository validation toolchain. Start it normally and press **Run interactive
measurements**, or pass `--autorun`. Choose one row profile per run:

- `--controls` (the default) measures normal Controls virtualization and
  compiled item construction;
- `--lightweight` measures the strict owner-drawn fixed-row backend;
- `--recycling` measures native Controls whose root implements the explicit
  cross-item reset contract;
- `--nonvirtual` disables virtualization and fully materializes 192 complex
  native rows. It measures immediate native wheel input and `ScrollToIndex`
  jumps without interpreting virtualization-only diagnostics.

Add `--smooth` to any profile to replace the immediate/direct workload with
native wheel messages handled by the runtime's 120 ms coalesced smooth-scroll
path. Add `--styled` to use the framework-owned styled scrollbar instead of the
native scrollbar:

```text
WinFormsXaml.InteractiveBenchmarks.exe --nonvirtual --smooth --autorun
WinFormsXaml.InteractiveBenchmarks.exe --nonvirtual --smooth --styled --autorun
WinFormsXaml.InteractiveBenchmarks.exe --controls --smooth --autorun
WinFormsXaml.InteractiveBenchmarks.exe --controls --smooth --styled --autorun
```

The harness continues heartbeat sampling until the final smooth transition has
settled. Scroll-command timings measure synchronous message dispatch, while
heartbeat latency captures UI-thread animation, native child-window work, and
framework scrollbar painting. These four commands form the primary
nonvirtual/virtual by native/styled comparison matrix; Lightweight and
Recycling profiles accept the same switches.

The output records the profile and reports successful and rejected cross-item
resets with the existing creation and cache counters. The default Controls
fixture deliberately uses only blueprint-safe native CLR properties inside its
item template, so its construction counters verify that the measured rows
actually use the compiled path. Run the same machine, OS, visual-style state,
and build once per profile for a useful comparison.

Every profile reports item-template construction, item-tree disposal, active
binding subscription, native scroll-event, heartbeat-latency, and resource
counters. The non-virtual profile reports logical and retained item-tree counts
instead of treating virtual realization/cache counters as meaningful.

Results are descriptive and must record the OS, CLR, CPU, package/source
identity, visual-style state, and whether the run used a real Windows 98 guest.
They are not a substitute for correctness tests.
