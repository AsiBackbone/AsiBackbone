# AsiBackbone 7.1.0 Consumer Verification Guide

This guide records the consumer checks for the `7.1.0` package family. Perform
the artifact-verification steps against the published `v7.1.0` GitHub Release
and NuGet packages after publication.

## Confirm the compatibility boundary

- Package IDs remain `AsiBackbone.*`.
- All packages continue to target `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.1.0` and `7.1.0.0`.
- No persisted schema, canonical wire value, namespace, or runtime default is
  intentionally changed.

## Exercise analyzer and deprecation guidance

1. Reference `AsiBackbone.Analyzers` version `7.1.0` from a clean consumer.
2. Construct a signing request without `SignatureInput` and confirm `ASIB004`
   is reported.
3. Set `SignatureInput` with `GovernanceSignatureInput.CreateV1(...)` and
   confirm the diagnostic clears.
4. Confirm `CreateLegacy(...)` reports `ASIB902` and `RetryClock` reports
   `ASIB903` while both compatibility paths remain callable.
5. Confirm a project that intentionally verifies retained pre-6.0 evidence can
   still use `WithLegacySignatureInputAllowed()` without a deprecation warning.

## Verify the published packages

Download a package and its release evidence from the `v7.1.0` GitHub Release,
then verify its provenance:

```powershell
gh release download v7.1.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.1.0.nupkg'
gh attestation verify ./AsiBackbone.Core.7.1.0.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect the package metadata and Source Link repository commit, and compare the
package SHA-256 value with the release evidence manifest. Repeat for each
package adopted by the application.

## Build a clean consumer

Create a clean `net10.0` project, add only the required `AsiBackbone.*` packages
at `7.1.0`, restore, build with warnings enabled, and exercise the integration
boundary used by the application. Consumers upgrading from `6.x` must first
complete the [6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

See also:

- [7.1.0 Release Notes](release-notes-710.md)
- [7.1.0 Release Readiness Record](release-readiness-710.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
