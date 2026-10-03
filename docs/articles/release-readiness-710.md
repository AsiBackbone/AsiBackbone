# AsiBackbone 7.1.0 Release Readiness Record

Release date: 2026-10-03

`7.1.0` is a backward-compatible minor release for the stable `7.x` package
family. It adds analyzer rule `ASIB004` and documented `ASIB902` and `ASIB903`
deprecations without removing public APIs or changing their runtime behavior.

This record is a pre-tag checklist. Do not create `v7.1.0` or publish packages
until the release-preparation pull request is merged to protected `main` and
the required hosted checks pass on the release candidate.

## Version and compatibility boundary

- [x] `Directory.Build.props` resolves package version `7.1.0`.
- [x] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.1.0.0`.
- [x] `CITATION.cff` and `.zenodo.json` report version `7.1.0` and release date
  `2026-10-03`.
- [x] Template fallback package references use `7.1.0`.
- [x] NuGet lock files are refreshed for the `7.1.0` in-repository graph.
- [x] Documentation release claims identify `7.1.0` as released and current.
- [x] Public API baselines pass without an unreviewed stable API change.

## Compatibility review

- [x] Package IDs, public namespaces, target framework, durable schemas,
  canonical wire values, and documented runtime defaults are unchanged.
- [x] `ASIB004` is classified as a backward-compatible analyzer addition.
- [x] `ASIB902` and `ASIB903` are warnings, retain working `7.x` compatibility
  paths, name their replacements, and have dedicated migration guides.
- [x] `VerificationPolicyContext.WithLegacySignatureInputAllowed()` remains
  supported for intentional historical evidence verification.
- [x] The `8.0` removal language follows the minimum deprecation-window and
  major-release-spacing policies.

## Local release-candidate validation

- [x] Version consistency passes for `7.1.0` and `v7.1.0`.
- [x] Documentation release-claim validation passes for `v7.1.0`.
- [x] Package lock-file, dependency-suppression, API-baseline, architecture,
  and documentation-continuity validators pass.
- [x] Locked restore passes.
- [x] Debug and Release builds pass with zero warnings and errors.
- [x] The full local test suite passes; provider tests that require hosted SQL
  Server or PostgreSQL are covered by the service-container CI job.
- [x] Formatting verification passes.
- [x] DocFX completes without errors.
- [x] All stable packages pack as `7.1.0` and pass package metadata/content
  validation.
- [x] Template and clean external-consumer smoke tests pass.

## Hosted evidence required before tagging

- [ ] CI, stable release validation, CodeQL, dependency review, dependency
  scanning, provider-contention, template smoke, and external-consumer jobs are
  green for the release-candidate commit.
- [ ] The non-publishing package workflow produces the expected package, SBOM,
  hash, and release-evidence artifacts for maintainer review.
- [ ] The tag will be created from the validated commit already merged to
  protected `main`.

## Post-publish evidence

- [ ] All eleven packages are visible on NuGet.org at `7.1.0` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains the required packages, symbols, SBOMs,
  manifests, hashes, release notes, and provenance evidence.
- [ ] Source Link and GitHub attestation verification pass against the exact
  published artifacts.
- [ ] The documentation site publishes the `7.1.0` release pages and sitemap.

See also:

- [7.1.0 Release Notes](release-notes-710.md)
- [7.1.0 Consumer Verification Guide](consumer-verification-710.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
