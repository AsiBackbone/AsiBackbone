# AsiBackbone 7.1.1 Consumer Verification Guide

This guide records the consumer checks for the `7.1.1` package family. Perform
the artifact-verification steps against the published `v7.1.1` GitHub Release
and NuGet packages after publication.

## Confirm the compatibility boundary

- Package IDs remain `AsiBackbone.*`.
- All packages continue to target `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.1.1` and `7.1.1.0`.
- Package dependencies are the same as in `7.1.0`.
- No public API, persisted schema, canonical wire value, namespace, or runtime
  default is changed.

## Confirm the patch carries no behavior change

1. Replace `7.1.0` with `7.1.1` in a consumer that already builds against
   `7.1.0`, restore, and build with warnings enabled. Expect the same
   diagnostics, including any `ASIB004`, `ASIB902`, or `ASIB903` warnings the
   project already reported.
2. Run the consumer's own tests. Expect the same results as on `7.1.0`.
3. Open a package README on NuGet.org and confirm it shows that package's
   version and download badges.

## Verify the published packages

Download a package and its release evidence from the `v7.1.1` GitHub Release,
then verify its provenance:

```powershell
gh release download v7.1.1 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.1.1.nupkg'
gh attestation verify ./AsiBackbone.Core.7.1.1.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect the package metadata and Source Link repository commit, and compare the
package SHA-256 value with the release evidence manifest. Repeat for each
package adopted by the application.

## Build a clean consumer

Create a clean `net10.0` project, add only the required `AsiBackbone.*` packages
at `7.1.1`, restore, build with warnings enabled, and exercise the integration
boundary used by the application. Consumers upgrading from `6.x` must first
complete the [6.x to 7.0 upgrade guide](upgrade-600-to-700.md); the analyzer and
deprecation checks for the `7.1` line are in the
[7.1.0 Consumer Verification Guide](consumer-verification-710.md).

See also:

- [7.1.1 Release Notes](release-notes-711.md)
- [7.1.1 Release Readiness Record](release-readiness-711.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
