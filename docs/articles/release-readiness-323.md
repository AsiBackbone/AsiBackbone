# AsiBackbone 3.2.3 Release Readiness Record

Release candidate date: 2026-08-30

## Release intent

`3.2.3` is a backward-compatible maintenance patch. Its scope is repository
test/coverage modernization, documentation ownership/navigation cleanup,
documentation-link guardrails, and workflow maintenance accumulated after
`3.2.2`.

No production runtime source files under `src/AsiBackbone.*` changed relative
to `v3.2.2`. This is a pre-tag checklist.

## Version and compatibility

- [ ] Package version is `3.2.3`.
- [ ] `AssemblyVersion` remains `3.0.0.0`.
- [ ] `FileVersion` is `3.2.3.0`.
- [ ] Target framework remains `net10.0`.
- [ ] Package IDs, namespaces, public APIs, runtime governance semantics, and
  stable package boundaries are unchanged.
- [ ] Citation/Zenodo metadata reports `3.2.3`.
- [ ] Template fallback references and applicable lock ranges use `3.2.3`.
- [ ] Source Link validation defaults to `3.2.3`.

## Test and quality infrastructure

- [ ] `xunit.v3` and `xunit.runner.visualstudio` resolve to `4.0.0`.
- [ ] `coverlet.MTP` resolves to `10.0.1`.
- [ ] `global.json` selects Microsoft Testing Platform.
- [ ] MTP/Cobertura coverage generation and all coverage gates pass.
- [ ] Committed lock files match the final dependency graph.

## Documentation

- [ ] Learning remains the canonical organization-level educational source.
- [ ] This repository remains authoritative for package/API/runtime behavior.
- [ ] Release, maintainer, history, and grouped top navigation render correctly.
- [ ] Local/cross-repository documentation links validate.
- [ ] Preserved historical URLs still resolve where required.

## Required validation before tag

- [ ] Locked restore.
- [ ] Debug and Release builds.
- [ ] `dotnet format --verify-no-changes`.
- [ ] Full xUnit 4/MTP suite.
- [ ] Repository/package/Core coverage gates.
- [ ] XML documentation inventory.
- [ ] API baseline and compatibility validation.
- [ ] Version consistency for `3.2.3` and `v3.2.3`.
- [ ] Package creation and NuGet metadata validation.
- [ ] Template fallback smoke test against `3.2.3`.
- [ ] External-consumer and stable-package smoke tests.
- [ ] DocFX and documentation-link validation.
- [ ] CodeQL, dependency review, OpenSSF, actionlint/Zizmor, and OWASP checks.
- [ ] SBOM/provenance artifacts where supported.
- [ ] No package-signing claim for unsigned packages.

## Release sequence

1. Validate `release/3.2.3`.
2. Open the release PR to `main`.
3. Merge only after required checks pass.
4. Tag the validated source as `v3.2.3`.
5. Publish packages and release artifacts.
6. Confirm documentation deployment.
7. Run post-publication Source Link validation.

## Related documentation

- [3.2.3 Release Notes](release-notes-323.md)
- [3.2.3 Consumer Verification Guide](consumer-verification-323.md)
- [Release Validation](release-validation.md)
- [Release Cadence and Readiness](release-cadence-and-readiness.md)
- [Documentation Ownership](documentation-ownership.md)
