# AsiBackbone 7.5.3 Release Readiness Record

Release date: 2026-11-06

`7.5.3` is a backward-compatible maintenance patch for the stable `7.x`
package family. It improves package discovery, evaluation guidance,
terminology and documentation validation, visual identity, and repository
tooling. Public APIs, durable schemas, canonical wire values, target framework,
documented defaults, runtime behavior, and binary assembly identity remain
unchanged.

This record is a pre-tag checklist. Do not create `v7.5.3` or publish packages
until the release-preparation pull request is merged to protected `main` and the
required hosted checks pass on the release candidate. Check each item only when
its evidence exists.

## Version and compatibility boundary

- [x] `Directory.Build.props` resolves package version `7.5.3`.
- [x] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.5.3.0`.
- [x] `CITATION.cff` and `.zenodo.json` report version `7.5.3`, release date
  `2026-11-06`, and canonical product title `AsiBackbone`.
- [x] Template fallback package references and the five-minute evaluator use
  `7.5.3`.
- [x] Tracked NuGet lock files are refreshed for the `7.5.3` in-repository
  graph. The file-based evaluator lock is intentionally untracked because a
  pre-publication package hash can differ from the tagged release artifact.
- [x] Documentation publication state identifies `7.5.3` as released and
  current for the stable tag candidate.
- [x] Public API baselines pass with no stable API change.

## Compatibility review

- [x] No AsiBackbone source implementation, public signature, durable schema,
  canonical wire value, package ID, namespace, or target-framework changes
  relative to `v7.5.2`.
- [x] The evaluator is sample and validation content, not a new public API or
  production package capability.
- [x] Package description and discovery-tag changes preserve package identity
  and existing runtime behavior.
- [x] `AssemblyVersion` remains `7.0.0.0`; declared runtime dependency minimums
  and durable contracts remain unchanged.

## Local release-candidate validation

- [x] Version and tag consistency pass for `7.5.3` and `v7.5.3`.
- [x] Documentation release-claim and current-terminology validation pass.
- [x] Package lock-file, API-baseline, package-metadata, XML-documentation,
  landing-page, generated-HTML, and documentation-continuity validators pass.
- [x] Locked restore passes.
- [x] Debug and Release builds pass with zero warnings and errors.
- [x] The full local test suite passes; provider tests requiring hosted SQL
  Server or PostgreSQL remain covered by service-container CI.
- [x] Formatting verification passes.
- [x] DocFX completes with zero warnings and errors under `--warningsAsErrors`.
- [x] All eleven stable packages pack as `7.5.3` and pass package metadata and
  content validation.
- [x] Package SBOM and manifest generation passes.
- [x] Template, five-minute evaluator, stable-package integration, and clean
  external-consumer smoke tests pass.
- [x] Full-solution coverage is 97.68% line and 91.03% branch; Core coverage is
  98.64% line and 91.65% branch; all tracked package coverage baselines pass.

### Recorded local evidence

- Debug and Release solution builds completed with zero warnings and errors.
- The full suite completed with 2,253 passing, 14 provider-environment skips,
  and zero failing tests across ten test assemblies.
- Strict DocFX generated 542 HTML documents with zero warnings and errors;
  landing-page, metadata, terminology, release-claim, and link-continuity
  validation passed. The two new release pages are expected to return 404 until
  this build is deployed.
- All eleven `7.5.3` packages passed version and NuGet metadata validation, and
  generated eleven SPDX package SBOMs plus the aggregate manifest.
- Both template host styles built with zero warnings and errors. The external
  consumer smoke test passed its HTTP decision flow and five-minute evaluator;
  the stable-package integration suite passed all six tests.
- Version validation rejects a tracked five-minute evaluator lock file, while
  the external-consumer smoke test validates the evaluator against packages
  built from the exact candidate commit.

## Hosted evidence required before tagging

- [ ] CI, stable release validation, CodeQL, dependency review, dependency
  scanning, provider-contention, template smoke, external-consumer, and
  documentation link validation jobs are green for the release-candidate commit.
- [ ] The non-publishing package workflow produces the expected package, SBOM,
  hash, and release-evidence artifacts for maintainer review.
- [ ] The tag will be created from the validated commit already merged to
  protected `main`, after CI on that merge commit has completed.

## Post-publish evidence

- [ ] All eleven packages are visible on NuGet.org at `7.5.3` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains the required packages, symbols, SBOMs,
  manifests, hashes, release notes, and provenance evidence.
- [ ] Source Link and GitHub attestation verification pass against the exact
  published artifacts.
- [ ] The documentation site publishes the `7.5.3` release pages and sitemap.

See also:

- [7.5.3 Release Notes](release-notes-753.md)
- [7.5.3 Consumer Verification Guide](consumer-verification-753.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
