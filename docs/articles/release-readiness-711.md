# AsiBackbone 7.1.1 Release Readiness Record

Release date: 2026-10-15

`7.1.1` is a backward-compatible patch release for the stable `7.x` package
family. Apart from version metadata, package contents change only in their
README files; public APIs, runtime behavior, and package dependencies are
unchanged from `7.1.0`.

This record is a pre-tag checklist. Do not create `v7.1.1` or publish packages
until the release-preparation pull request is merged to protected `main` and
the required hosted checks pass on the release candidate. Check each item when
its evidence exists.

## Version and compatibility boundary

- [ ] `Directory.Build.props` resolves package version `7.1.1`.
- [ ] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.1.1.0`.
- [ ] `CITATION.cff` and `.zenodo.json` report version `7.1.1` and release date
  `2026-10-15`.
- [ ] Template fallback package references use `7.1.1`.
- [ ] NuGet lock files are refreshed for the `7.1.1` in-repository graph.
- [ ] Documentation release claims identify `7.1.1` as released and current.
- [ ] Public API baselines pass with no stable API change.

## Compatibility review

- [ ] No file under `src/` changed since `v7.1.0` except package README files
  and the template fallback package references.
- [ ] Package IDs, public namespaces, target framework, package dependencies,
  durable schemas, canonical wire values, and documented runtime defaults are
  unchanged.
- [ ] The `System.Security.Cryptography.Pkcs` pin is confined to repository
  test restore graphs, and no shipped package or template scaffold depends on
  `Microsoft.Data.SqlClient`.

## Local release-candidate validation

- [ ] Version consistency passes for `7.1.1` and `v7.1.1`.
- [ ] Documentation release-claim validation passes for `v7.1.1`.
- [ ] Package lock-file, dependency-suppression, API-baseline, architecture,
  and documentation-continuity validators pass.
- [ ] Locked restore passes.
- [ ] Debug and Release builds pass with zero warnings and errors.
- [ ] The full local test suite passes; provider tests that require hosted SQL
  Server or PostgreSQL are covered by the service-container CI job.
- [ ] Formatting verification passes.
- [ ] DocFX completes with zero warnings and errors under `--warningsAsErrors`.
- [ ] All stable packages pack as `7.1.1` and pass package metadata/content
  validation.
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

- [ ] All eleven packages are visible on NuGet.org at `7.1.1` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains the required packages, symbols, SBOMs,
  manifests, hashes, release notes, and provenance evidence.
- [ ] Source Link and GitHub attestation verification pass against the exact
  published artifacts.
- [ ] The documentation site publishes the `7.1.1` release pages and sitemap.

See also:

- [7.1.1 Release Notes](release-notes-711.md)
- [7.1.1 Consumer Verification Guide](consumer-verification-711.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
