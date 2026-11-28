# AsiBackbone 7.5.6 Release Readiness Record

Release date: 2026-11-27

`7.5.6` is a backward-compatible governance, dependency, documentation, and
release-validation maintenance patch for the stable `7.x` package family.
Public APIs, durable schemas, canonical wire values, target framework,
documented defaults, runtime behavior, shipped runtime dependency declarations,
and binary assembly identity remain unchanged.

This record is a pre-tag checklist. Do not create `v7.5.6` or publish packages
until the release-preparation pull request is merged to protected `main` and
the required hosted checks pass on the release candidate. Check each item only
when its evidence exists.

## Version and compatibility boundary

- [x] `Directory.Build.props` resolves package version `7.5.6`.
- [x] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.5.6.0`.
- [x] `CITATION.cff` and `.zenodo.json` report version `7.5.6`, release date
  `2026-11-27`, and canonical product title `AsiBackbone`.
- [x] Template fallback package references and the five-minute evaluator use
  `7.5.6`.
- [x] Tracked NuGet lock files are refreshed for the `7.5.6` in-repository
  graph.
- [x] Documentation publication state identifies `7.5.6` as released and
  current for the stable tag candidate.
- [x] The committed API baseline records `7.5.6` with no stable API change.

## Compatibility and release-security review

- [x] No public signature, durable schema, canonical wire value, package ID,
  namespace, target framework, documented default, runtime decision behavior,
  shipped runtime dependency declaration, or binary assembly identity change
  exists relative to `v7.5.5`.
- [x] Framework-coupled shipped dependencies remain on .NET 10.
- [x] Compiler-facing Roslyn dependencies remain aligned with the compiler in
  .NET SDK 10.0.404, and the packed consumer smoke proves analyzer execution.
- [x] Assurance-agent and proposal guidance preserves exclusive human
  maintainer authority for organizational decisions, merges, and releases.
- [x] Sigstore bundles remain distinct from NuGet author signing, NuGet.org
  repository signatures, GitHub build provenance, and runtime
  governance-record signing.

## Local release-candidate validation

- [x] Version and tag consistency pass for `7.5.6` and `v7.5.6`.
- [x] Documentation release-claim and current-terminology validation pass.
- [x] Public API baseline and documentation-continuity validation pass.
- [x] Package lock-file, XML-documentation, landing-page, agent-governance, and
  generated-HTML validators pass.
- [x] Locked Release restore passes.
- [x] Release build passes with zero warnings and errors.
- [x] The full local test suite passes with zero failures.
- [x] Formatting verification passes.
- [x] Strict DocFX generation passes.
- [x] All eleven stable packages pack as `7.5.6` and pass package metadata and
  content validation.
- [x] Package SBOM and manifest generation passes.
- [x] Template, five-minute evaluator, stable-package integration, and clean
  external-consumer smoke tests pass.

### Recorded local evidence

- Locked Release restore completed against the refreshed dependency graph.
- Debug and Release builds completed with zero warnings and zero errors.
- The full Release test run completed with 2,255 passing tests, 14 expected
  provider-environment skips, and zero failures across 10 test assemblies.
- Formatting verification completed without changes.
- Strict DocFX built 553 HTML files with zero warnings and zero errors; the
  landing-page, generated-HTML metadata, strict-workflow, current-terminology,
  release-claim, and cross-repository documentation validators passed. The two
  new current-release URLs are present in the build and remain pending public
  site deployment.
- All 10 stable public API baselines matched the generated Release assemblies.
- All 11 stable packages packed as `7.5.6`, passed version and NuGet metadata
  validation, and produced SPDX SBOMs plus the aggregate manifest.
- Template generation for both supported host styles, the five-minute
  evaluator, the external-consumer test, and all six stable-package integration
  smoke tests completed successfully with zero warnings and zero failures.
- The external-consumer analyzer probe observed `ASIB001` for the unsafe
  discard and then built cleanly after the consumer retained the decision.

## Hosted evidence required before tagging

- [ ] CI, stable release validation, CodeQL, dependency review, dependency
  scanning, provider-contention, template smoke, external-consumer,
  agent-governance, and documentation link validation jobs are green for the
  release-candidate commit.
- [ ] The non-publishing package workflow produces the expected packages,
  SBOMs, hash manifest, and package and SBOM provenance attestations for
  maintainer review.
- [ ] The tag will be created from the validated commit already merged to
  protected `main`, after CI on that merge commit has completed.

## Post-publish evidence

- [ ] All eleven packages are visible on NuGet.org at `7.5.6` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains packages, symbols, SBOMs, manifests, hashes,
  release notes, provenance evidence, and one Sigstore bundle per `.nupkg`.
- [ ] Source Link, GitHub attestation, Sigstore identity, digest, and negative
  verification checks pass against the exact published artifacts.
- [ ] The documentation site publishes the `7.5.6` release pages and sitemap.

See also:

- [7.5.6 Release Notes](release-notes-756.md)
- [7.5.6 Consumer Verification Guide](consumer-verification-756.md)
- [Stable Release Validation](release-validation.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
