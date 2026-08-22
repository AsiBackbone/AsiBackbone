# AsiBackbone 3.2.2 Release Notes

Release date: 2026-08-22

## Summary

`3.2.2` is a backward-compatible maintenance patch for the stable `3.2.x`
AsiBackbone package family. It refreshes approved .NET dependencies and
SHA-pinned GitHub Actions/security tooling accumulated after `3.2.1`, and it
normalizes one repository metadata file whose line endings could otherwise
produce a false-positive working-tree change on Windows.

No runtime source files changed relative to `v3.2.1`. The public software
contract remains unchanged: package IDs, public namespaces, public APIs,
runtime governance behavior, the `net10.0` target framework, and
`AssemblyVersion` `3.0.0.0` are preserved.

## Changed

### .NET dependency maintenance

The centrally managed dependency set now includes these patch/tooling updates:

| Dependency | `3.2.1` | `3.2.2` |
| --- | --- | --- |
| `Microsoft.EntityFrameworkCore` | `10.0.10` | `10.0.11` |
| `Microsoft.EntityFrameworkCore.InMemory` | `10.0.10` | `10.0.11` |
| `Microsoft.EntityFrameworkCore.Relational` | `10.0.10` | `10.0.11` |
| `Microsoft.EntityFrameworkCore.Sqlite` | `10.0.10` | `10.0.11` |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.10` | `10.0.11` |
| `Microsoft.NET.Test.Sdk` | `18.8.1` | `18.9.0` |

These updates do not add a new AsiBackbone public API or change the documented
host-ownership boundary.

### CI, security, and release-tooling maintenance

SHA-pinned GitHub Actions used by CI, release validation, package publication,
CodeQL, OpenSSF/OWASP, workflow-security analysis, and provenance handling were
refreshed. The release-candidate workflow set includes:

- CodeQL action `4.37.7`;
- `actions/attest-build-provenance` `4.2.2`; and
- Zizmor action `0.6.2`.

Version comments beside immutable action SHAs were also aligned with the pinned
commits so dependency/security tooling can verify the intended versions without
changing the repository's SHA-pinning policy.

### Repository hygiene

`.config/dotnet-tools.json` was renormalized according to the repository's
existing `.gitattributes` policy. This is a line-ending normalization only; it
does not change the configured .NET tool version or tool behavior.

## Compatibility

- Package IDs are unchanged.
- Public namespaces are unchanged.
- Public APIs are unchanged.
- Runtime governance behavior is unchanged.
- Target framework remains `net10.0`.
- `AssemblyVersion` remains `3.0.0.0`.
- `FileVersion` advances to `3.2.2.0`.
- Existing `3.2.x` consumers require no source-code migration solely because
  of this patch release.
- Capability-grant validation profiles introduced in `3.2.0` remain unchanged.
- The organization-owned repository, documentation, Source Link, citation, and
  package metadata established in `3.2.1` remain canonical.
- Authentication, authorization, durable replay protection, signing-key
  custody, persistence, external execution, monitoring, and operational safety
  remain host-owned responsibilities.

## Package signing posture

NuGet package signing remains intentionally deferred while AsiBackbone is
independently maintained. Source Link metadata, SBOMs, provenance artifacts,
release tags, and public source are complementary verification signals and
should not be described individually as a signed-package guarantee.

## Release validation

The release candidate should pass the repository's normal stable-release path,
including:

- locked restore;
- Debug and Release solution builds;
- formatting and analyzer checks;
- unit, integration, and property-based tests;
- repository/package coverage gates;
- Core branch-coverage and XML-documentation inventory checks;
- API baseline and compatibility checks;
- package creation, version consistency, and generated NuGet metadata checks;
- template, external-consumer, and stable-package smoke tests;
- DocFX build and documentation release-claim validation;
- CodeQL, dependency review, OpenSSF, actionlint/Zizmor, and OWASP checks; and
- SBOM and provenance generation where supported.

After publication, validate Source Link repository metadata with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.2
```

The expected repository URL remains:

```text
https://github.com/AsiBackbone/AsiBackbone
```
