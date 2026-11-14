# AsiBackbone 7.5.4 Release Readiness Record

Release date: 2026-11-14

`7.5.4` is a backward-compatible supply-chain and validation maintenance patch
for the stable `7.x` package family. It adds Sigstore keyless signatures for
GitHub release build packages, advances Aspire sample hosting, refreshes GitHub
Actions, and strengthens package compatibility validation. Public APIs, durable
schemas, canonical wire values, target framework, documented defaults, runtime
behavior, declared runtime dependency minimums, and binary assembly identity
remain unchanged.

This record is a pre-tag checklist. Do not create `v7.5.4` or publish packages
until the release-preparation pull request is merged to protected `main` and the
required hosted checks pass on the release candidate. Check each item only when
its evidence exists.

## Version and compatibility boundary

- [x] `Directory.Build.props` resolves package version `7.5.4`.
- [x] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.5.4.0`.
- [x] `CITATION.cff` and `.zenodo.json` report version `7.5.4`, release date
  `2026-11-14`, and canonical product title `AsiBackbone`.
- [x] Template fallback package references and the five-minute evaluator use
  `7.5.4`.
- [x] Tracked NuGet lock files are refreshed for the `7.5.4` in-repository
  graph.
- [x] Documentation publication state identifies `7.5.4` as released and
  current for the stable tag candidate.
- [x] The committed API baseline records `7.5.4` with no stable API change.

## Compatibility and release-security review

- [x] No AsiBackbone source implementation, public signature, durable schema,
  canonical wire value, package ID, namespace, target framework, or declared
  runtime dependency change exists relative to `v7.5.3`.
- [x] The Aspire update affects sample hosting only; GitHub Actions updates
  affect repository automation only.
- [x] Sigstore bundles cover exact GitHub release build packages and remain
  distinct from NuGet author signing, NuGet.org repository signatures, GitHub
  build provenance, and runtime governance-record signing.
- [x] Package validation now uses published `7.5.3` as its compatibility
  baseline; obsolete suppressions for pre-7.x breaks are removed.

## Local release-candidate validation

- [x] Version and tag consistency pass for `7.5.4` and `v7.5.4`.
- [x] Documentation release-claim and current-terminology validation pass.
- [x] Public API baseline and documentation-continuity validation pass.
- [ ] Package lock-file, XML-documentation, landing-page, and generated-HTML
  validators pass.
- [x] Locked Release restore passes.
- [x] Release build passes with zero warnings and errors.
- [x] The full local test suite passes with zero failures.
- [x] Formatting verification passes.
- [x] Strict DocFX generation passes.
- [x] All eleven stable packages pack as `7.5.4` and pass package metadata and
  content validation.
- [ ] Package SBOM and manifest generation passes.
- [ ] Template, five-minute evaluator, stable-package integration, and clean
  external-consumer smoke tests pass.

### Recorded local evidence

- SDK `10.0.404`; locked Release restore completed successfully.
- The Release solution build completed with zero warnings and zero errors.
- The full suite completed with 2,253 passing, 14 provider-environment skips,
  and zero failing tests across ten test assemblies.
- Formatting verification completed with no changes required.
- Strict DocFX generated 545 HTML files with zero warnings and zero errors.
  Documentation continuity validation passed; the two new release pages are
  expected to return 404 until this build is deployed.
- All ten stable managed-package public API baselines matched.
- All eleven `7.5.4` packages passed package compatibility, version, and NuGet
  metadata validation.

## Hosted evidence required before tagging

- [ ] CI, stable release validation, CodeQL, dependency review, dependency
  scanning, provider-contention, template smoke, external-consumer, and
  documentation link validation jobs are green for the release-candidate commit.
- [ ] The non-publishing package workflow produces the expected packages,
  SBOMs, hash manifest, and package and SBOM provenance attestations for
  maintainer review.
- [ ] The tag will be created from the validated commit already merged to
  protected `main`, after CI on that merge commit has completed.

## Post-publish evidence

- [ ] All eleven packages are visible on NuGet.org at `7.5.4` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains packages, symbols, SBOMs, manifests, hashes,
  release notes, provenance evidence, and one Sigstore bundle per `.nupkg`.
- [ ] Source Link, GitHub attestation, Sigstore identity, digest, and negative
  verification checks pass against the exact published artifacts.
- [ ] The documentation site publishes the `7.5.4` release pages and sitemap.

See also:

- [7.5.4 Release Notes](release-notes-754.md)
- [7.5.4 Consumer Verification Guide](consumer-verification-754.md)
- [Stable Release Validation](release-validation.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
