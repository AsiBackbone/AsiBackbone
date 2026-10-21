# AsiBackbone 7.5.0 Release Readiness Record

Release date: 2026-10-21

`7.5.0` is a backward-compatible minor release for the stable `7.x` package
family. It restores request trace correlation across endpoint policy and
capability evaluation and adds the compatible Core APIs required to carry and
copy that identifier.

This record is a pre-tag checklist. Do not create `v7.5.0` or publish packages
until the release-preparation pull request is merged to protected `main` and
the required hosted checks pass on the release candidate.

## Version and compatibility boundary

- [ ] `Directory.Build.props` resolves package version `7.5.0`.
- [ ] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.5.0.0`.
- [ ] `CITATION.cff` and `.zenodo.json` report version `7.5.0` and release date `2026-10-21`.
- [ ] Template fallback package references use `7.5.0`.
- [ ] NuGet lock files are refreshed for the `7.5.0` in-repository graph.
- [ ] Documentation release claims identify `7.5.0` as released and current.
- [ ] Public API baselines contain only the reviewed additive trace-correlation surfaces.

## Compatibility review

- [ ] The existing four-argument `GovernanceEvaluationContext` constructor remains available.
- [ ] Existing `IGovernanceEvaluationContext` implementations compile unchanged through the default `TraceId` member.
- [ ] Evaluator-supplied trace identifiers remain authoritative.
- [ ] Decision outcomes, reasons, policy identifiers, canonical wire values, and persistence schemas are unchanged.
- [ ] Package IDs, public namespaces, target framework, documented defaults, and binary assembly identity remain compatible.

## Local release-candidate validation

- [ ] Version consistency passes for `7.5.0` and `v7.5.0`.
- [ ] Documentation release-claim validation passes for `v7.5.0`.
- [ ] Package lock-file, dependency-suppression, API-baseline, architecture, and documentation-continuity validators pass.
- [ ] Locked restore passes.
- [ ] Debug and Release builds pass with zero warnings and errors.
- [ ] The full local test suite passes; provider tests requiring hosted SQL Server or PostgreSQL are covered by service-container CI.
- [ ] Formatting verification passes.
- [ ] DocFX completes with zero warnings and errors under `--warningsAsErrors`.
- [ ] All eleven stable packages pack as `7.5.0` and pass package metadata/content validation.
- [ ] Template and clean external-consumer smoke tests pass.

## Hosted evidence required before tagging

- [ ] CI, stable release validation, CodeQL, dependency review, dependency scanning, provider-contention, template smoke, external-consumer, and documentation link validation jobs are green for the release-candidate commit.
- [ ] The non-publishing package workflow produces the expected package, SBOM, hash, and release-evidence artifacts for maintainer review.
- [ ] The tag will be created from the validated commit already merged to protected `main`, after CI on that merge commit has completed.

## Post-publish evidence

- [ ] All eleven packages are visible on NuGet.org at `7.5.0` with expected metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains the required packages, symbols, SBOMs, manifests, hashes, release notes, and provenance evidence.
- [ ] Source Link and GitHub attestation verification pass against the exact published artifacts.
- [ ] The documentation site publishes the `7.5.0` release pages and sitemap.

See also:

- [7.5.0 Release Notes](release-notes-750.md)
- [7.5.0 Consumer Verification Guide](consumer-verification-750.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
