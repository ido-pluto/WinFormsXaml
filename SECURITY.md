# Security policy

## Report a vulnerability privately

Do not open a public issue for a suspected vulnerability. If the published
repository exposes GitHub private vulnerability reporting, use it. Otherwise,
contact a maintainer through a private channel and ask for a secure reporting
route before sending sensitive details.

Include:

- the affected WinFormsXaml version;
- the .NET Framework and Windows versions;
- a minimal XML and C# reproduction;
- the impact and required attacker access;
- any known workaround.

Please allow the maintainers time to reproduce and coordinate a fix before
public disclosure.

## Markup trust boundary

WinFormsXaml markup and preset XML are application code. Loading can instantiate
types, assign properties, read local files or embedded resources, and connect
code-behind methods. Applications must not load untrusted uploaded or
network-provided markup directly.

DTD declarations are prohibited and XML external-entity resolution is
disabled. New XML and resource-loading features must retain both boundaries and
must not silently enable entity expansion, network access, or local-file reads.
