# AsiBackbone 3.2.3 Consumer Verification Guide

`3.2.3` is a backward-compatible maintenance patch. Its primary changes are
repository test/coverage modernization and documentation ownership/navigation,
not runtime package behavior.

## Verify package identity

Expected version: `3.2.3`.

Expected package IDs remain `AsiBackbone.Core`,
`AsiBackbone.DependencyInjection`, `AsiBackbone.Storage.InMemory`,
`AsiBackbone.EntityFrameworkCore`, `AsiBackbone.AspNetCore`,
`AsiBackbone.Testing`, `AsiBackbone.Templates`,
`AsiBackbone.Analyzers`, `AsiBackbone.OpenTelemetry`,
`AsiBackbone.Signing.LocalDevelopment`, and
`AsiBackbone.Signing.ManagedKey`.

## Verify compatibility

- target framework: `net10.0`;
- package version: `3.2.3`;
- assembly version: `3.0.0.0`;
- file version: `3.2.3.0`;
- repository: `https://github.com/AsiBackbone/AsiBackbone`;
- project site: `https://asibackbone.github.io/AsiBackbone/`.

Consumers upgrading from `3.2.2` should not require source changes solely
because of this patch. The xUnit 4/MTP migration is repository validation
infrastructure and does not require consumers to adopt xUnit 4.

## Verify Source Link

After publication:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.3
```

Each package should report repository type `git`, the canonical repository URL,
and a non-empty repository commit associated with the final `v3.2.3` source.

## Verify template fallback references

Fallback package references should use `3.2.3` for
`AsiBackbone.AspNetCore`, `AsiBackbone.Core`,
`AsiBackbone.Storage.InMemory`, and `AsiBackbone.Analyzers`.

## Package signing

NuGet package signing remains intentionally deferred. Source Link, SBOMs,
provenance, public source, and release tags are complementary signals rather
than substitutes for a package signature.

## Related documentation

- [3.2.3 Release Notes](release-notes-323.md)
- [3.2.3 Release Readiness Record](release-readiness-323.md)
- [Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Documentation Ownership](documentation-ownership.md)
