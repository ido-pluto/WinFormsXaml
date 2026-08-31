# WinFormsXaml focused benchmarks

This dependency-free .NET 2.0 console runner measures the runtime paths most
likely to matter in data-heavy applications:

- keyed non-virtual item creation, unchanged reloads, and one-item patches;
- a 10,000-row fixed-size virtual list with deterministic scroll jumps;
- a 10,000-row reactive virtual nested-condition graph with a stable template
  root, initial construction,
  source-indexed full-list rotation timing, off-screen notifying changes,
  sampled one-subscriber guards, and post-disposal cleanup guards;
- repeated registered XML component construction across fresh runtimes;
- event-heavy repeated control construction and event cleanup across fresh runtimes;
- ordinary property-binding fan-out;
- preset-selection fan-out.

The runner reports elapsed time, milliseconds per operation, and the change in
live managed memory reported by `GC.GetTotalMemory`. It deliberately has no
performance pass/fail thresholds: results depend on the CLR, operating system,
CPU, and WinForms implementation. Functional failures still return a non-zero
exit code so a broken scenario is not mistaken for a benchmark result.

Both virtual and both fan-out scenarios create hidden control handles, but the
runner never shows a form and never starts an application message loop. The
reactive scenario drains queued callbacks between changes so its subscription
and cleanup guards cover the complete notification path. Matching handle state
keeps the ordinary-binding and preset fan-out numbers comparable.

The condition-graph scenario rotates the 10,000-item source by one position and
times the indexed graph update before measuring off-screen notifications. It
also verifies that sampled items retain exactly one subscriber across the
rotation and that disposal removes those subscriptions.

Build the classic project with Visual Studio 2005/MSBuild, or use
`build/WinFormsXaml.Benchmarks.Validation.csproj` with a modern SDK that has the
.NET Framework 2.0 reference assemblies available. Run a Release build for
comparisons and record the environment header printed at startup.

See [BASELINE.md](BASELINE.md) for the first recorded development-host run.

For first-paint, UI-stall, native scrolling, and repeated Form-lifetime
measurements, use the separate
[interactive performance harness](WinFormsXaml.InteractiveBenchmarks/README.md).
It owns a real message loop and deliberately remains separate from this
repeatable headless runner. Its Controls profile also reports compiled-blueprint
and complete-renderer construction counts so a fast-path claim is observable in
the result itself.
