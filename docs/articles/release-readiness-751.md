# AsiBackbone 7.5.1 Release Readiness Record

Release date: 2026-10-24

`7.5.1` is a backward-compatible patch release for the stable `7.x` package
family. It strengthens production-signing guidance, host-enforcement
conformance evidence, and package dependency validation without adding public
API or changing package dependencies, durable schemas, canonical wire values,
target framework, documented defaults, or binary assembly identity.

This record is a pre-tag checklist. Do not create `v7.5.1` or publish packages
until the release-preparation pull request is merged to protected `main` and
the required hosted checks pass on the release candidate. Check each item when
its evidence exists.

## Version and compatibility boundary

- [ ] `Directory.Build.props` resolves package version `7.5.1`.
- [ ] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.5.1.0`.
- [ ] `CITATION.cff` and `.zenodo.json` report version `7.5.1` and release date
  `2026-10-24`.
- [ ] Template fallback package references use `7.5.1`.
- [ ] NuGet lock files are refreshed for the `7.5.1` in-repository graph.
- [ ] Documentation release claims identify `7.5.1` as released and current.
- [ ] Public API baselines pass with no stable API change.

## Compatibility review

- [ ] The local-development Production guard behavior and public options are
  unchanged; only its diagnostics and guidance are clarified.
- [ ] Host-enforcement additions are tests and documentation, not package-owned
  execution or a new public enforcement surface.
- [ ] Generated package dependency IDs, versions, and framework references match
  the reviewed `7.5.0` surface.
- [ ] Package IDs, public namespaces, target framework, documented defaults,
  durable schemas, canonical wire values, and binary assembly identity are
  unchanged.

## Local release-candidate validation

- [ ] Version consistency passes for `7.5.1` and `v7.5.1`.
- [ ] Documentation release-claim validation passes for `v7.5.1`.
- [ ] Package lock-file, dependency-suppression, API-baseline, architecture,
  package-metadata, and documentation-continuity validators pass.
- [ ] Locked restore passes.
- [ ] Debug and Release builds pass with zero warnings and errors.
- [ ] The full local test suite passes; provider tests requiring hosted SQL
  Server or PostgreSQL are covered by service-container CI.
- [ ] Formatting verification passes.
- [ ] DocFX completes with zero warnings and errors under `--warningsAsErrors`.
- [ ] All eleven stable packages pack as `7.5.1` and pass package metadata and
  content validation.
- [ ] Template and clean external-consumer smoke tests pass.

## Hosted evidence required before tagging

- [ ] CI, stable release validation, CodeQL, dependency review, dependency
  scanning, provider-contention, template smoke, external-consumer, and
  documentation link validation jobs are green for the release-candidate commit.
- [ ] The non-publishing package workflow produces the expected package, SBOM,
  hash, and release-evidence artifacts for maintainer review.
- [ ] The tag will be created from the validated commit already merged to
  protected `main`, after CI on that merge commit has completed.

## Post-publish evidence

- [ ] All eleven packages are visible on NuGet.org at `7.5.1` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains the required packages, symbols, SBOMs,
  manifests, hashes, release notes, and provenance evidence.
- [ ] Source Link and GitHub attestation verification pass against the exact
  published artifacts.
- [ ] The documentation site publishes the `7.5.1` release pages and sitemap.

See also:

- [7.5.1 Release Notes](release-notes-751.md)
- [7.5.1 Consumer Verification Guide](consumer-verification-751.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
