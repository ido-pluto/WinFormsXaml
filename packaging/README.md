# Local package workflow

The package workflow compiles the runtime for .NET Framework 2.0, stages the
DLL, PDB symbols, API documentation, and XML IntelliSense schema, and creates a
local NuGet package. It has no push, publish, deployment, API-key, or
registry-write step.

## Windows and CI workflow

Prerequisites:

- PowerShell.
- .NET SDK 8.0.
- NuGet CLI 5.10.0 or newer. Package README support was introduced in that
  client line; both pack scripts reject older or unidentifiable clients.

Build a local package with an explicit version:

```powershell
./build/Pack.ps1 -PackageVersion 0.1.0-preview.1
```

Compile and run a clean consumer against that exact local package:

```powershell
./build/VerifyPackageConsumer.ps1 -PackageVersion 0.1.0-preview.1
```

The consumer verification deletes only its dedicated
`artifacts/package/consumer` directory, restores `WinFormsXaml` through a
package-source mapping that permits only the local package directory, compiles
a separate .NET Framework 2.0 project, and runs its minimal XML load/lookup
smoke test. It requires the restored package to contain its non-empty PDB and
also compiles every restored XSD copy with external resolution disabled. Schema
read and compile warnings or errors are fatal; fixture errors are fatal, while
lax-extension warnings are reported and allowed, and each fixture root must be
globally declared. The gate asserts that PackageReference exposes the schema as
a flattened `None` item with build/publish copy metadata disabled and scans the
build output for an unexpected XSD. It does not execute a separate publish.
NuGet.org is used only for the Microsoft .NET Framework 2.0 reference-assemblies
package.

The Windows compatibility workflow performs both commands with a unique CI
prerelease version. Changes under `packaging/**` trigger the gate. The resulting
`.nupkg` is retained only as a workflow artifact; there is no NuGet push step.

## Bash and Wine workflow

Prerequisites:

- Bash, Wine, `winepath`, and a .NET Framework 4.5-compatible runtime in the
  active Wine prefix. Wine Mono satisfies that compiler-host requirement.
- NuGet CLI 5.10.0 or newer, available as `nuget`/`nuget.exe` or supplied with
  `NUGET_EXE=/absolute/path/to/nuget.exe`.

The first default run explicitly restores these Microsoft-owned packages from
`https://api.nuget.org/v3/index.json`:

- `Microsoft.Net.Compilers` 1.3.2;
- `Microsoft.NETFramework.ReferenceAssemblies.net20` 1.0.3.

Their exact versions are pinned in `build/Pack.sh`. The validated files are
cached under `artifacts/toolchain/pack` and subsequent runs do not restore them.
Roslyn 1.3.2 is intentional here: its Windows `csc.exe` runs on the smaller
.NET Framework 4.5 host surface and emits the required Windows PDB under Wine.
The compiler always receives `/noconfig`, `/langversion:2`, and `/nostdlib+`
before the explicit .NET Framework 2.0 reference set. It cannot accidentally
compile against Wine Mono's host-framework assemblies.

Run:

```bash
NUGET_EXE=/absolute/path/to/nuget.exe ./build/Pack.sh 0.1.0-preview.1
```

Advanced callers may override the validated cache. The Windows-hosted Roslyn
form remains:

```bash
WINFORMSXAML_CSC=/absolute/path/to/csc.dll \
WINFORMSXAML_CSC_HOST=/absolute/path/to/dotnet.exe \
WINFORMSXAML_REFERENCE_ROOT=/absolute/path/to/net20-reference-directory \
NUGET_EXE=/absolute/path/to/nuget.exe \
./build/Pack.sh 0.1.0-preview.1
```

`WINFORMSXAML_CSC` alone retains the previous direct-compiler behavior. Pairing
it with `WINFORMSXAML_REFERENCE_ROOT` enables the same explicit C# 2/.NET 2
reference mode for a custom `csc.exe`; add `WINFORMSXAML_CSC_HOST` only when the
compiler is a hosted `csc.dll`. Explicit overrides skip the pinned compiler
restore entirely.

The PowerShell workflow remains the native Windows path and uses the SDK
package-build project with the same C# 2 and .NET Framework 2.0 constraints.
Both workflows require a non-empty Windows PDB with the MSF 7.00 signature and
stop before packing if the compiler omits it or emits another symbol format.

## Package contents

```text
artifacts/
  toolchain/pack/                pinned Bash/Wine compiler and net20 references
  package/
    build/                       compiler output and generated version metadata
    stage/
      README.md                  self-contained package README
      content/
        WinFormsXaml.xsd         classic packages.config project content
      contentFiles/any/any/
        WinFormsXaml.xsd         PackageReference content
      lib/net20/
        WinFormsXaml.dll
        WinFormsXaml.pdb         .NET Framework debugging symbols
        WinFormsXaml.xml
      schemas/
        WinFormsXaml.xsd         stable tool/copy source inside the package
    output/
      WinFormsXaml.<version>.nupkg
    consumer/                    isolated consumer restore and compile output
```

`packaging/PackageREADME.md` is staged as the package README. Its public links
use the permanent GitHub repository URL so they also work from NuGet clients.

`lib/net20/WinFormsXaml.pdb` is the release assembly's Windows PDB. Keeping it
next to the DLL lets classic .NET Framework debuggers resolve framework stack
frames from the ordinary package without a separate symbol-package restore.

The package declares only .NET Framework assembly references and has no NuGet
runtime dependencies.

## Version policy

The explicit package version controls three related values:

- NuGet package version: the complete value, including a prerelease suffix.
- `AssemblyFileVersion`: the three numeric package components plus `.0`.
- `AssemblyInformationalVersion`: the complete package version.

`AssemblyVersion` is copied unchanged from
`src/WinFormsXaml/Properties/AssemblyInfo.cs`. Keeping it stable avoids needless
binding breaks between compatible package releases. Changing it is an explicit
binary-compatibility decision, not an automatic consequence of packing a new
version.

The generated package assembly metadata lives under `artifacts/package/build`;
the checked-in source file is not rewritten by either pack command.
The Windows pack command reads the completed DLL and fails if any of these
three version values differs from the policy above.

## Package identity

The checked-in NuGet metadata identifies `ido-pluto` as the author, uses the
MIT license expression, and links both the package website and its Git source
to <https://github.com/ido-pluto/WinFormsXaml>.

The pack workflows remain local and never publish a package. Publishing to a
NuGet feed is a separate, explicit release action.
