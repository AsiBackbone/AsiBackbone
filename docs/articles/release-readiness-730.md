# AsiBackbone 7.3.0 Release Readiness Record

Release date: 2026-10-20

`7.3.0` is a backward-compatible minor release for the stable `7.x` package
family. It adds opt-in, request-bound signed capability grants for ASP.NET Core
endpoints and the supporting Core signing, transport, validation, replay, and
audit-correlation surfaces.

This record is a pre-tag checklist. Do not create `v7.3.0` or publish packages
until the release-preparation pull request is merged to protected `main` and
the required hosted checks pass on the release candidate. Check each item when
its evidence exists.

## Version and compatibility boundary

- [ ] `Directory.Build.props` resolves package version `7.3.0`.
- [ ] `AssemblyVersion` remains `7.0.0.0` and `FileVersion` is `7.3.0.0`.
- [ ] `CITATION.cff` and `.zenodo.json` report version `7.3.0` and release date
  `2026-10-20`.
- [ ] Template fallback package references use `7.3.0`.
- [ ] NuGet lock files are refreshed for the `7.3.0` in-repository graph.
- [ ] Documentation release claims identify `7.3.0` as released and current.
- [ ] Public API baselines contain only the reviewed additive signed-grant,
  decision-receipt, and configured-key surfaces.

## Compatibility review

- [ ] Signed-grant endpoint validation is opt-in and does not replace an
  existing `IEndpointCapabilityGrantValidator`.
- [ ] Existing hosts see no registration, configuration, or runtime behavior
  change unless they opt in.
- [ ] Existing decision receipts retain their prior canonical payload and hash;
  `capabilityGrantId` is emitted only when present.
- [ ] The capability-grant token is a new versioned transport format and does
  not alter an existing durable format.
- [ ] Package IDs, public namespaces, target framework, existing persisted
  schemas, existing canonical wire values, documented defaults, and binary
  assembly identity remain compatible.

## Local release-candidate validation

- [ ] Version consistency passes for `7.3.0` and `v7.3.0`.
- [ ] Documentation release-claim validation passes for `v7.3.0`.
- [ ] Package lock-file, dependency-suppression, API-baseline, architecture,
  and documentation-continuity validators pass.
- [ ] Locked restore passes.
- [ ] Debug and Release builds pass with zero warnings and errors.
- [ ] The full local test suite passes; provider tests that require hosted SQL
  Server or PostgreSQL are covered by the service-container CI job.
- [ ] Formatting verification passes.
- [ ] DocFX completes with zero warnings and errors under `--warningsAsErrors`.
- [ ] All eleven stable packages pack as `7.3.0` and pass package
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

- [ ] All eleven packages are visible on NuGet.org at `7.3.0` with expected
  metadata, README content, icons, and repository commit data.
- [ ] The GitHub Release contains the required packages, symbols, SBOMs,
  manifests, hashes, release notes, and provenance evidence.
- [ ] Source Link and GitHub attestation verification pass against the exact
  published artifacts.
- [ ] The documentation site publishes the `7.3.0` release pages and sitemap.

See also:

- [7.3.0 Release Notes](release-notes-730.md)
- [7.3.0 Consumer Verification Guide](consumer-verification-730.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
