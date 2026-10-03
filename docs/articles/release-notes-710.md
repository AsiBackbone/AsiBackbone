# AsiBackbone 7.1.0 Release Notes

Release date: 2026-10-03

`7.1.0` is a backward-compatible minor release for the stable `7.x` package
family. It adds a signing-input analyzer safety rail and begins documented
deprecation windows for compatibility APIs that should not be used for new
work. Package IDs and the `net10.0` target are unchanged. `AssemblyVersion`
remains `7.0.0.0`; package and file versions advance to `7.1.0` and `7.1.0.0`.

## Signing analyzer safety rail

Analyzer rule `ASIB004` reports a warning when code constructs a
`SigningRequest`, `SignatureVerificationRequest`, or `ManagedKeySignRequest`
without setting `SignatureInput`. Omitting that value silently selects the
pre-6.0 hash-only input, bypasses the `ASIB902` warning on direct legacy-input
creation, and produces signatures that fail default version 1 verification.

Set `SignatureInput` with `GovernanceSignatureInput.CreateV1(...)`. The
first-party `GovernanceArtifactSigner` and `GovernanceArtifactVerifier` paths
already do this.

## Deprecations and migration paths

- `GovernanceSignatureInput.CreateLegacy(...)` now produces warning `ASIB902`.
  Use `CreateV1(...)` for current signing and verification. The legacy method
  remains callable in `7.x`; its earliest possible removal is `8.0` after the
  published deprecation policy is satisfied.
- `GovernanceOutboxDrainWorkerOptions.RetryClock` now produces warning
  `ASIB903`. Register a `TimeProvider` so hosted drain timing, retry readiness,
  claim transitions, and other time-aware services share one clock. The
  delegate remains honored in `7.x`; its earliest possible removal is `8.0`.
- The previously released `ASIB901` warning for
  `CapabilityGrantValidationOptions.CreateExecutionBoundary(...)` now has a
  dedicated migration guide. Use `CreateBoundExecutionBoundary(...)` with
  host-owned binding expectations.

These are compiler and IDE warnings, not errors. Projects that promote warnings
to errors may need to complete the documented migration while upgrading.

## Historical signature verification remains supported

`VerificationPolicyContext.WithLegacySignatureInputAllowed()` is not
deprecated. Applications may continue to opt into verification of retained
artifacts signed with the pre-6.0 input for the duration of their evidence
retention requirements. The new warning applies to creating new legacy signing
inputs, not to intentionally verifying historical evidence.

## Repository and dependency maintenance

- Added the repository-local AsiBackbone code-review agent skill.
- Added an XML sitemap to the DocFX site and corrected stale current-release
  pointers in historical documentation.
- Published the deprecation and major-release policy, including minimum
  deprecation windows, major-release spacing, announcement requirements, and
  preceding-line security support.
- Pinned workflow jobs to Ubuntu 24.04, restored project automation, and
  refreshed test, sample, and infrastructure dependencies. Shipped package
  dependencies are unchanged.

## Upgrade actions

1. Build with `AsiBackbone.Analyzers` enabled and address `ASIB004` by supplying
   version 1 signature input for new signing and verification requests.
2. Replace new uses of `CreateLegacy(...)` with `CreateV1(...)`; retain the
   explicit legacy-verification opt-in only where historical artifacts require
   it.
3. Replace `GovernanceOutboxDrainWorkerOptions.RetryClock` with a registered
   `TimeProvider`.
4. Follow the dedicated `ASIB901`, `ASIB902`, and `ASIB903` migration guides
   before the eventual `8.0` removal boundary.

No persisted schema, canonical wire value, package ID, namespace, service
registration contract, or documented runtime default changes in this release.

## Release verification

The release candidate is validated through locked restore, Debug and Release
builds, the full test suite, formatting, public API baselines, package metadata
and contents, template and external-consumer smoke tests, documentation release
claims, and DocFX. Stable publication occurs only from the protected `v7.1.0`
tag after the release-preparation pull request is merged to `main`.

See also:

- [ASIB901 migration guide](asib901-create-execution-boundary.md)
- [ASIB902 migration guide](asib902-legacy-signature-input.md)
- [ASIB903 migration guide](asib903-outbox-retry-clock.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [7.1.0 Release Readiness Record](release-readiness-710.md)
- [7.1.0 Consumer Verification Guide](consumer-verification-710.md)
- [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
