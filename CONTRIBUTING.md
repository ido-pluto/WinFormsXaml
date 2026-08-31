# Contributing

WinFormsXaml provides a readable, high-performance XML authoring experience for
Windows Forms while keeping a small .NET Framework 2.0-compatible runtime.
Changes should preserve native WinForms terminology and behavior, predictable
performance, and the documented compatibility contract.

## Before changing runtime code

- Keep `src/WinFormsXaml` valid C# 2.0.
- Use only APIs present in .NET Framework 2.0.
- Preserve unrelated behavior in the existing layout, diffing, and virtualization paths.
- Prefer a focused capability fallback over a second complete control stack.
- Add or update documentation for public markup or API behavior.

## Verification levels

State exactly what you verified: static review, .NET 2 reference compile,
current-Windows behavior, or behavior on a named legacy OS/runtime combination.
Do not infer target-system behavior from compile evidence alone.

The complete repository gate is:

```powershell
./build/Verify.ps1
```

It includes classic-project source parity, a classic solution rebuild, all
.NET 2.0 validation builds and test runners, the isolated Windows-native marquee
process, and the documentation build. Unsupported local hosts report a precise
native-marquee `SKIP`; Windows CI passes `-RequireNativeMarquee` and therefore
requires that process to report `PASS`. Other skip switches are useful on hosts
without Visual Studio MSBuild, Wine/Mono, or Node, but list every skipped level
in the change report.

The documentation site is self-contained under `docs/`. To work on only that
site without installing anything in the repository root:

```powershell
npm --prefix docs ci
npm --prefix docs run docs:dev
```

Pull requests build and retain the rendered site as a workflow artifact. A
push to `main` additionally uploads the same output to the `github-pages`
environment and deploys it after GitHub Pages is configured to use Actions.
The repository-name base path is supplied only by CI, so local preview remains
root-relative.

## Source organization

Files follow responsibilities rather than arbitrary size limits. Split a file when a subsystem has a clear contract and can be understood independently; avoid creating one class per tiny helper.

## Pull requests

Keep changes scoped, include a short behavior example, list the strongest completed verification level, and call out any guest test that remains pending.
