# AsiBackbone 7.2.0 Release Readiness Record

Release date: 2026-10-17

`7.2.0` is a backward-compatible minor release for the stable `7.x` package
family. It aligns public parameter names with decision-receipt terminology,
updates supported dependencies, and carries documentation, automation, and test
maintenance completed since `7.1.1`.

This record is a pre-tag checklist. Do not create `v7.2.0` or publish packages
until the release-preparation pull request is merged to protected `main` and
the required hosted checks pass on the release candidate. Check each item when
its evidence exists.

## Version and compatibility boundary

- [ ] `Directory.Build.props` resolves package version `7.2.0`.
- [ ] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.2.0.0`.
- [ ] `CITATION.cff` and `.zenodo.json` report version `7.2.0` and release date
  `2026-10-17`.
- [ ] Template fallback package references use `7.2.0`.
- [ ] NuGet lock files are refreshed for the `7.2.0` in-repository graph.
- [ ] Documentation release claims identify `7.2.0` as released and current.
- [ ] Public API baselines reflect only the reviewed parameter-name alignment.

## Compatibility review

- [ ] Package IDs, public namespaces, target framework, durable schemas,
  canonical wire values, runtime behavior, and documented defaults are unchanged.
- [ ] `AssemblyVersion` remains `7.0.0.0`, preserving binary compatibility for
  the stable `7.x` line.
- [ ] Existing compiled consumers, positional callers, and interface
  implementations remain compatible.
- [ ] Named-argument callers using the former `residue` or ambiguous `receipt`
  parameter names are documented to update when rebuilding against `7.2.0`.
- [ ] Centrally managed dependencies and all affected lock graphs resolve to the
  reviewed supported versions.

## Local release-candidate validation

- [ ] Version consistency passes for `7.2.0` and `v7.2.0`.
- [ ] Documentation release-claim validation passes for `v7.2.0`.
- [ ] Package lock-file, dependency-suppression, API-baseline, architecture,
  and documentation-continuity validators pass.
- [ ] Locked restore passes.
- [ ] Debug and Release builds pass with zero warnings and errors.
- [ ] The full local test suite passes; provider tests that require hosted SQL
  Server or PostgreSQL are covered by the service-container CI job.
- [ ] Formatting verification passes.
- [ ] DocFX completes with zero warnings and errors under `--warningsAsErrors`.
- [ ] All eleven stable packages pack as `7.2.0` and pass package
  metadata/content validation.
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

- [ ] All eleven packages are visible on NuGet.org at `7.2.0` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains the required packages, symbols, SBOMs,
  manifests, hashes, release notes, and provenance evidence.
- [ ] Source Link and GitHub attestation verification pass against the exact
  published artifacts.
- [ ] The documentation site publishes the `7.2.0` release pages and sitemap.

See also:

- [7.2.0 Release Notes](release-notes-720.md)
- [7.2.0 Consumer Verification Guide](consumer-verification-720.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
