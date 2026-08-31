# Validation contract

WinFormsXaml separates compile evidence, deterministic behavior tests, native
current-Windows checks, legacy guest acceptance, and performance measurements.
Passing one level does not silently claim the others.

## Repository gate

Run the complete local gate from the repository root:

```powershell
./build/Verify.ps1
```

It validates classic-project source parity and Visual Studio 2005 solution
structure, rebuilds every classic project, validates the XML schema against
sample markup, performs C# 2.0/.NET 2.0 validation builds, runs the
dependency-free test executables, the isolated native-marquee process, and the
documentation site. The following switches narrow that evidence:

| Switch | Skipped work | Work that still runs |
| --- | --- | --- |
| `-SkipClassicSolutionValidation` | Visual Studio 2005 solution-structure validation and classic-project rebuilds | Source parity, schema validation, SDK validation builds, tests, and docs |
| `-SkipTests` | All executable test and native-marquee runs | Test projects still compile |
| `-SkipDocs` | VitePress dependency install and site build | Schema validation still runs |
| `-RequireNativeMarquee` | Nothing | Converts native-marquee `SKIP` into a failed gate; it cannot be combined with `-SkipTests` |

Report every switch used when describing a validation result.

## Reproducible Bash package toolchain

On macOS or Linux with Wine, create the local package with:

```bash
NUGET_EXE=/absolute/path/to/nuget.exe ./build/Pack.sh 0.1.0-preview.1
```

The default path pins `Microsoft.Net.Compilers` 1.3.2 and Microsoft's .NET
Framework 2.0 reference assemblies 1.0.3. The first run announces and restores
those exact packages from NuGet.org into `artifacts/toolchain/pack`; later runs
validate and reuse that stable cache without a toolchain restore. The compiler
is isolated from its host configuration with `/noconfig`, `/langversion:2`, and
`/nostdlib+`, followed by explicit `mscorlib`, System, Windows Forms, drawing,
data, and XML references from the pinned net20 package.

The Bash and native Windows package workflows reject a missing or empty DLL,
XML documentation file, or PDB. They additionally require the PDB to have the
Windows MSF 7.00 signature before NuGet packing begins. `WINFORMSXAML_CSC`,
`WINFORMSXAML_CSC_HOST`, and `WINFORMSXAML_REFERENCE_ROOT` remain advanced
overrides; using them skips the default compiler bootstrap. A custom direct
`csc.exe` can be paired with the reference root, while a hosted `csc.dll` also
requires the Windows host executable.

## Documentation toolchain security

The published documentation is static output. VitePress, Vite, and esbuild are
development-only dependencies and are not shipped by the NuGet package or the
generated site.

As of August 24, 2026, VitePress `1.6.4` is the latest stable release and its
declared Vite range is `^5.4.14`. The lockfile therefore resolves Vite `5.4.21`
and esbuild `0.21.5`. A full `npm audit` reports one high and two moderate
development-tool findings through that graph:

- [GHSA-fx2h-pf6j-xcff](https://github.com/advisories/GHSA-fx2h-pf6j-xcff)
  is a Windows alternate-path `server.fs.deny` bypass;
- [GHSA-v6wh-96g9-6wx3](https://github.com/advisories/GHSA-v6wh-96g9-6wx3)
  is a Windows UNC-path problem in the open-in-editor endpoint;
- [GHSA-4w7w-66w2-5vf9](https://github.com/advisories/GHSA-4w7w-66w2-5vf9)
  can expose predictable source-map files from a network-exposed dev server;
- [GHSA-67mh-4wv8-2f99](https://github.com/evanw/esbuild/security/advisories/GHSA-67mh-4wv8-2f99)
  is inherited from esbuild, but concerns esbuild's own `serve` API. Vite does
  not use that API.

There is no supported stable dependency-only fix today. The relevant Vite
findings are fixed in Vite `6.4.3`, outside VitePress 1.x's declared range, and
VitePress 2 is still an alpha release. Do not force a Vite major with an npm
override or move the documentation to an alpha release merely to silence the
audit.

The checked-in Vite configuration narrows the reachable development surface:

- development and preview bind explicitly to `127.0.0.1`;
- CORS accepts only loopback origins;
- `/__open-in-editor` returns `404` before Vite's vulnerable middleware runs.

Do not pass `--host`, set `server.host` to a wildcard or LAN address, or weaken
the CORS rule while this dependency graph remains in place. `npm audit
--omit=dev` is expected to report zero deployed dependencies, while a full
`npm audit` intentionally continues to report the development-tool advisories.
Revisit the lockfile when a stable VitePress release declares support for Vite
`6.4.3` or newer.

## Windows-native marquee process

`WinFormsXaml.NativeMarqueeValidation.exe` is isolated because
`Application.EnableVisualStyles()` is process-wide and must run before any
control is constructed. Its STA entry point performs that call first, shows a
bounded probe Form, lets its message loop advance, and verifies all of the
following:

- `Application.RenderWithVisualStyles` is true;
- the real progress HWND contains `PBS_MARQUEE`;
- the HWND accepts `PBM_SETMARQUEE`;
- `Style` and `MarqueeAnimationSpeed` retain their public values;
- the compatibility fallback renderer and private mask HWND remain inactive.

The process emits exactly one terminal classification:

| Output | Exit code | Contract |
| --- | ---: | --- |
| `WINFORMSXAML_NATIVE_MARQUEE: PASS` | 0 | The supported Common Controls path was exercised in a shown Form. |
| `WINFORMSXAML_NATIVE_MARQUEE: FAIL` | 1 | Validation ran but the native-path contract was violated. |
| `WINFORMSXAML_NATIVE_MARQUEE: SKIP` | 2 | The direct host cannot provide the required Windows visual-style capability. |

`Verify.ps1` never runs this executable through Wine or Mono. Unsupported local
hosts receive a precise `SKIP`; the Windows CI gate uses
`-RequireNativeMarquee` and therefore requires `PASS`. The general test runner
uses a different process and deliberately does not enable visual styles, keeping
automatic fallback selection covered.

The CI rule intentionally does not infer capability only from the
`windows-latest` label. If that image ever disables client theming or cannot
activate version 6 Common Controls, the validator emits `SKIP` and the required
gate fails with that reason instead of recording false native evidence.

## Legacy guest acceptance

A current-Windows native pass is not evidence for Windows 98. On every claimed
legacy OS, CLR, and Common Controls combination, record the exact environment and
check at least:

- automatic fallback selection without forcing `PreferMarqueeFallback`;
- marquee animation plus speed-zero pause and positive-speed resume;
- transitions between `Marquee`, `Blocks`, and `Continuous` with logical range
  and value preservation;
- left-to-right and right-to-left phases, resize behavior, and repainting;
- repeated handle recreation and disposal without growing HWND or GDI counts.

The complete application must also load its assembly and embedded XML, show its
forms, process bindings and events, and release its resources in that guest.

## Benchmarks

The repository gate compiles but never executes
`WinFormsXaml.Benchmarks`. Run its Release executable separately and record the
OS, CLR, CPU, Common Controls/visual-style state, and source/package identity.
The checked-in baseline is historical comparison data; native-marquee validation
does not update it, and current-host measurements do not establish Windows 98
performance.

### Interactive performance harness

`benchmarks/WinFormsXaml.InteractiveBenchmarks` is the message-loop companion
to the headless runner. It measures cold and warm first-presented-frame time,
calibrated UI heartbeat delay during small and large virtual scroll changes,
virtual-control creation/reuse counters, managed collections and process
memory, and repeated real Form open/close cycles.

The harness never forces a garbage collection. It calls `GetGuiResources` only
on Windows XP or newer, because that API is not a valid Windows 98/Me counter.
Run it interactively with its button or pass `--autorun`; record the same
environment and source/package identity as the headless benchmark.
