# Development-host baseline

Recorded 2026-08-23 after the first correctness and modularity pass.

- Host: macOS 26.6.2, arm64, Apple M5 Max
- Compatibility layer: Wine 11.0
- Reported managed runtime: 4.0.30319.42000
- Build: optimized C# 2 / SDK 2 profile

| Scenario | Result |
| --- | ---: |
| 250-row nonvirtual initial build | 313.555 ms |
| 250-row unchanged keyed reload | 2.099 ms/reload |
| 250-row single versioned patch | 3.311 ms/reload |
| 10,000-row fixed virtual initialization | 113.550 ms |
| Fixed virtual `ScrollToIndex` jumps | 210.540 ms/jump |
| 400-label ordinary binding fan-out | 4.213 ms/reload |
| 400-label preset fan-out | 22.000 ms/selection |

These numbers are descriptive, not release thresholds. Wine reported a Windows
NT 6.2 environment and used its CLR 4 compatibility runtime. Native .NET 2 and
Windows 98 results must be recorded separately before making legacy-performance
claims. The virtual-jump number is the clearest development-host hotspot in this
run; optimization work should retain the scroll correctness tests while reducing
that cost.

## Post-audit optimized snapshot

Recorded on the same development host after the fixed-size viewport and dynamic
binding invalidation optimizations. The ordinary and preset fan-out cases now
both create a hidden root handle, so their invalidation work is comparable.

| Scenario | Result |
| --- | ---: |
| 250-row nonvirtual initial build | 318.419 ms |
| 250-row unchanged keyed reload | 2.105 ms/reload |
| 250-row single versioned patch | 3.345 ms/reload |
| 10,000-row fixed virtual initialization | 119.329 ms |
| Fixed virtual `ScrollToIndex` jumps | 90.060 ms/jump |
| 400-label ordinary binding fan-out, hidden handle | 20.527 ms/reload |
| 400-label preset fan-out, hidden handle | 20.474 ms/selection |

The fixed-size jump path retained a maximum of 19 realized controls, down from
37 in the pre-optimization run, and improved from about 210.5 ms to about
90.1 ms per deterministic jump on this host. The old 4.2 ms ordinary-binding
number was headless while preset selection had a handle; it must not be compared
directly with the normalized fan-out results above.

## Pre-reactive-batch snapshot

Recorded 2026-08-24 before the later reactive-item and lifecycle changes. This
is retained as historical comparison data, not as a final release-candidate
measurement. It remains a Wine/CLR 4 development-host measurement, not a
Windows 98 result.

| Scenario | Result |
| --- | ---: |
| 250-row nonvirtual initial build | 285.603 ms |
| 250-row unchanged keyed reload | 2.120 ms/reload |
| 250-row single versioned patch | 3.255 ms/reload |
| 10,000-row fixed virtual initialization | 116.801 ms |
| Fixed virtual `ScrollToIndex` jumps | 11.287 ms/jump |
| 400-label ordinary binding fan-out, hidden handle | 24.029 ms/reload |
| 400-label preset fan-out, hidden handle | 23.391 ms/selection |

The virtual path stayed bounded at 19 realized controls and 23 cached controls
during the deterministic jump sequence. Timing values are descriptive and can
vary between runs; correctness and bounded realization are the release gates.

## Historical post-reactive development snapshot

Recorded 2026-08-24 from the then-current source used for that package-gate
run, before later feature and audit changes. This is historical comparison data,
not validation of the latest source or package. The assembly was produced by the
.NET Framework 2.0 C# compiler with optimization enabled and warnings treated as
errors. Wine executed it through its reported CLR 4 compatibility runtime, so
these remain development-host measurements.

| Scenario | Result |
| --- | ---: |
| 250-row nonvirtual initial build | 300.245 ms |
| 250-row unchanged keyed reload | 2.131 ms/reload |
| 250-row single versioned patch | 3.387 ms/reload |
| 10,000-row fixed virtual initialization | 121.316 ms |
| Fixed virtual `ScrollToIndex` jumps | 11.664 ms/jump |
| 400-label ordinary binding fan-out, hidden handle | 26.609 ms/reload |
| 400-label preset fan-out, hidden handle | 25.659 ms/selection |

The deterministic virtual sequence stayed bounded at 19 realized controls and
23 cached controls. Final performance claims for Windows 98 remain gated on a
guest-native run with the .NET Framework 2.0 runtime.
