# AsiBackbone 7.5.2 Consumer Verification Guide

This guide describes checks for the `7.5.2` package family. Perform artifact
verification against the published `v7.5.2` GitHub Release and NuGet packages
after publication; this guide alone is not evidence of publication.

## Confirm compatibility and dependencies

- Package IDs remain `AsiBackbone.*`, and the target remains `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.5.2` and `7.5.2.0`.
- Public APIs, durable schemas, canonical wire values, documented defaults,
  and AsiBackbone runtime decision logic are unchanged from `7.5.1`.
- Declared Microsoft dependency minimums advance to `10.0.14`. Review central
  version overrides and confirm the consumer graph resolves without downgrade
  warnings. Aspire and FsCheck remain sample/test dependencies.

## Verify the published artifacts

```powershell
gh release download v7.5.2 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.5.2.nupkg'
gh attestation verify ./AsiBackbone.Core.7.5.2.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect NuGet metadata, declared dependencies, and Source Link repository
commit. Compare SHA-256 hashes with the release evidence manifest and repeat
for each adopted package. Package dependency IDs and framework references
remain unchanged; declared Microsoft dependency versions increase.

## Build and exercise a clean consumer

Create a clean `net10.0` project, install only the required `AsiBackbone.*`
packages at `7.5.2`, restore, build with warnings enabled, and run the host's
tests. Exercise EF Core persistence and provider paths where adopted.

Retain decision-before-side-effect tests and production-signing configuration
checks described in the [7.5.1 Consumer Verification Guide](consumer-verification-751.md).
Consumers moving from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

See also:

- [7.5.2 Release Notes](release-notes-752.md)
- [7.5.2 Release Readiness Record](release-readiness-752.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
