# AsiBackbone 4.0.0 Release Readiness Record

Release candidate date: 2026-09-06

## Release intent

`4.0.0` starts the stable `4.x` package family. It is a major release because
governance outbox claim leasing becomes the default and can require a custom
outbox store to implement the claim-store contract or explicitly opt out.

This is a pre-tag checklist. Package publication and the GitHub release remain
separate post-merge actions.

## Version and compatibility

- [ ] Package version is `4.0.0`.
- [ ] `AssemblyVersion` is `4.0.0.0`.
- [ ] `FileVersion` is `4.0.0.0`.
- [ ] Target framework remains `net10.0`.
- [ ] Package IDs and public namespaces remain unchanged.
- [ ] Citation and Zenodo metadata report `4.0.0` and `2026-09-06`.
- [ ] Template fallback references and applicable lock ranges use `4.0.0`.
- [ ] Source Link validation resolves `4.0.0` from shared metadata.
- [ ] The 3.2.3-to-4.0.0 migration guide documents the breaking default.

## Runtime boundary

- [ ] Shipped outbox stores support claim leasing.
- [ ] Custom-store failure and explicit opt-out behavior are documented.
- [ ] Claim paging, reclaim limits, and dead-letter defaults are tested.
- [ ] Endpoint metadata sanitization and middleware ordering are tested.
- [ ] Inbound correlation IDs require trusted opt-in.
- [ ] EF Core stores bind to their requested DbContext types.
- [ ] Terminal outbox states cannot be resurrected by non-claim mutations.
- [ ] Canonical capability-grant payloads cover every validated grant field.
- [ ] Signature verification exposes stable failure categories.

## Required validation before tag

- [ ] Locked restore.
- [ ] Debug and Release builds.
- [ ] `dotnet format --verify-no-changes`.
- [ ] Full Microsoft Testing Platform suite.
- [ ] Repository, package, and Core coverage gates.
- [ ] XML documentation inventory.
- [ ] API baseline and compatibility review.
- [ ] Version consistency for `4.0.0` and `v4.0.0`.
- [ ] Package creation and NuGet metadata validation.
- [ ] Template fallback smoke test against `4.0.0`.
- [ ] External-consumer and stable-package smoke tests.
- [ ] DocFX and documentation-link validation.
- [ ] CodeQL, dependency review, OpenSSF, actionlint/Zizmor, and OWASP checks.
- [ ] SBOM and provenance artifacts where supported.
- [ ] No package-signing claim for unsigned packages.

## Release sequence

1. Validate `release/4.0.0`.
2. Open the release pull request to `main`.
3. Merge only after required checks pass.
4. Tag the validated source as `v4.0.0`.
5. Publish packages and release artifacts.
6. Confirm documentation deployment.
7. Run post-publication Source Link validation.

## Related documentation

- [4.0.0 Release Notes](release-notes-400.md)
- [4.0.0 Migration Guide](upgrade-323-to-400.md)
- [4.0.0 Consumer Verification Guide](consumer-verification-400.md)
- [Release Validation](release-validation.md)
- [Release Cadence and Readiness](release-cadence-and-readiness.md)
