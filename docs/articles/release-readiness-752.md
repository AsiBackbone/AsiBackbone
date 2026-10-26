# AsiBackbone 7.5.2 Release Readiness Record

Release date: 2026-10-25

`7.5.2` is a backward-compatible dependency maintenance patch for the stable
`7.x` package family. It updates Microsoft dependency minimums to `10.0.14`,
Aspire sample hosting to `13.6.4`, and FsCheck test tooling to `3.6.0`.
Public APIs, durable schemas, canonical wire values, target framework,
documented defaults, and binary assembly identity remain unchanged.

This record is a pre-tag checklist. Do not create `v7.5.2` or publish packages
until the release-preparation pull request is merged to protected `main` and
the required hosted checks pass on the release candidate. Check each item when
its evidence exists.

## Version and compatibility boundary

- [x] `Directory.Build.props` resolves package version `7.5.2`.
- [x] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.5.2.0`.
- [x] `CITATION.cff` and `.zenodo.json` report version `7.5.2` and release date
  `2026-10-25`.
- [x] Template fallback package references use `7.5.2`.
- [x] NuGet lock files are refreshed for the `7.5.2` in-repository graph.
- [x] Documentation release claims identify `7.5.2` as released and current.
- [ ] Public API baselines pass with no stable API change.

## Compatibility review

- [x] No AsiBackbone source implementation, public signature, durable schema,
  or canonical wire value changes relative to `v7.5.1`.
- [x] Generated dependency IDs and framework references are unchanged; declared
  Microsoft dependency minimums match central `10.0.14` versions.
- [x] Aspire and FsCheck remain isolated to sample/test projects.
- [x] Package IDs, target framework, documented defaults, and binary assembly
  identity remain unchanged.

## Local release-candidate validation

- [x] Version consistency passes for `7.5.2` and `v7.5.2`.
- [x] Documentation release-claim validation passes for `v7.5.2`.
- [ ] Package lock-file, dependency-suppression, API-baseline, architecture,
  package-metadata, and documentation-continuity validators pass.
- [x] Locked restore passes.
- [x] Debug and Release builds pass with zero warnings and errors.
- [x] The full local test suite passes; provider tests requiring hosted SQL
  Server or PostgreSQL are covered by service-container CI.
- [ ] Formatting verification passes.
- [ ] DocFX completes with zero warnings and errors under `--warningsAsErrors`.
- [x] All eleven stable packages pack as `7.5.2` and pass package metadata and
  content validation.
- [ ] Template and clean external-consumer smoke tests pass.

## Recorded local evidence and environment limits

- SDK `10.0.400`; Debug and Release solution builds passed with zero warnings
  and errors. Restore/build used `-m:1` because parallel MSBuild workers cannot
  use named pipes in this workspace.
- Locked Release restore passed after force-evaluating the graph. Both Aspire
  platform lock files were checked; neither required a content change.
- All ten Release test assemblies ran directly through their xUnit in-process
  runners: **2,267 total, 2,253 passed, 14 provider-environment skips, zero
  failures or errors**. The `dotnet test --solution` launcher is blocked by
  sandbox named-pipe permissions; the assemblies themselves passed.
- All eleven `7.5.2` packages packed and passed version and NuGet metadata
  validation. Eleven SPDX SBOMs and their manifest were generated locally.
- Documentation release claims passed across 238 files; release-claim fixtures,
  release-evidence fixtures, Debug coverage configuration, strict DocFX workflow
  checks, landing-page source checks, and the NCAT contract-vector pin passed.
- Canonical project-based DocFX metadata extraction and `dotnet format` are
  blocked by the workspace's named-pipe permissions. An alternate documentation
  build using metadata extracted from the compiled Release assemblies rendered
  538 HTML files with zero warnings and errors. Assembly-based metadata exposes
  implicit constructors differently, so it does not establish a passing result
  for the source-based public API baseline. The canonical DocFX, formatting,
  and API-baseline gates remain required in hosted CI; no API baseline rows
  were changed to accommodate that alternate extraction.
- Both generated template host styles (`plain` and `netcoretemplate`) restored
  and built against the local `7.5.2` packages with zero warnings and errors.
  The complete scripted external-consumer integration smoke remains a hosted gate.
- The documentation link manifest checks local `7.5.2` release pages while its
  remote release probes name the latest published `7.5.1` pages. Publication
  must precede advancing those remote probes.

## Hosted evidence required before tagging

- [ ] CI, stable release validation, CodeQL, dependency review, dependency
  scanning, provider-contention, template smoke, external-consumer, and
  documentation link validation jobs are green for the release-candidate commit.
- [ ] The non-publishing package workflow produces the expected package, SBOM,
  hash, and release-evidence artifacts for maintainer review.
- [ ] The tag will be created from the validated commit already merged to
  protected `main`, after CI on that merge commit has completed.

## Post-publish evidence

- [ ] All eleven packages are visible on NuGet.org at `7.5.2` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains the required packages, symbols, SBOMs,
  manifests, hashes, release notes, and provenance evidence.
- [ ] Source Link and GitHub attestation verification pass against the exact
  published artifacts.
- [ ] The documentation site publishes the `7.5.2` release pages and sitemap.

See also:

- [7.5.2 Release Notes](release-notes-752.md)
- [7.5.2 Consumer Verification Guide](consumer-verification-752.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
