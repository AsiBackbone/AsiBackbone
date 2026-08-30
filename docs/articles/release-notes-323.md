# AsiBackbone 3.2.3 Release Notes

Release date: 2026-08-30

## Summary

`3.2.3` is a backward-compatible maintenance patch for the stable `3.2.x`
AsiBackbone package family. It carries forward the `3.2.2` runtime and public
API contract while modernizing repository test/coverage infrastructure and
completing documentation-ownership and navigation cleanup.

No production runtime source files under `src/AsiBackbone.*` changed relative
to `v3.2.2`. Package IDs, public namespaces, public APIs, runtime governance
behavior, `net10.0`, and `AssemblyVersion` `3.0.0.0` remain unchanged.

## Highlights

- Migrated repository tests to xUnit `4.0.0` and Microsoft Testing Platform.
- Migrated coverage to `coverlet.MTP` with reusable MTP coverage orchestration.
- Updated CI/release/quality/smoke paths for MTP-compatible execution.
- Established ASI Backbone Learning as the canonical educational source while
  this repository remains authoritative for package/API/runtime documentation.
- Reorganized DocFX release, maintainer-evidence, history, and top navigation.
- Added documentation ownership/link/URL-preservation guardrails.
- Refreshed SHA-pinned CodeQL actions from `4.37.7` to `4.37.8`.

## Compatibility

- Package IDs, public namespaces, public APIs, and runtime governance behavior
  are unchanged.
- Target framework remains `net10.0`.
- `AssemblyVersion` remains `3.0.0.0`.
- `FileVersion` advances to `3.2.3.0`.
- Existing `3.2.x` consumers require no source migration solely because of
  this patch.
- The xUnit/MTP migration is repository test infrastructure; consumers are not
  required to adopt xUnit 4.

## Package signing posture

NuGet package signing remains intentionally deferred. Source Link, SBOMs,
provenance artifacts, release tags, and public source remain complementary
verification signals.

## Release validation

The final release candidate should rerun locked restore, Debug/Release builds,
formatting, the full xUnit 4/MTP suite, coverage gates, API compatibility,
packaging/version checks, template/consumer smoke tests, DocFX/link validation,
security workflows, SBOM generation, and provenance where supported.

After publication:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.3
```
