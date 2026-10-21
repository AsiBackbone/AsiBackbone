# AsiBackbone 7.4.0 Consumer Verification Guide

This guide records the consumer checks for the `7.4.0` package family. Perform
the artifact-verification steps against the published `v7.4.0` GitHub Release
and NuGet packages after publication.

## Confirm the compatibility boundary

- Package IDs remain `AsiBackbone.*`.
- All packages continue to target `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.4.0` and `7.4.0.0`.
- Existing canonical wire values, public namespaces, and documented defaults remain unchanged.
- Existing hosts see no public-key-verifier or EF Core replay-store behavior change unless they opt in.

## Verify production signed-grant adoption

For hosts that opt in:

1. Register `AddAsiBackboneKeySignatureVerification(...)` with trusted PEM/JWK public keys or a host-owned `IGovernanceVerificationKeyResolver`.
2. Confirm RSA-PSS SHA-256 or ECDSA P-256 SHA-256 descriptors match the issuer's signing configuration.
3. Exercise current-key, previous-key, revoked-key, unknown-key, malformed-key, and invalid-signature paths.
4. For multi-replica endpoints, register `AddAsiBackboneEfCoreCapabilityGrantUseStore<TDbContext>(...)`, apply the AsiBackbone model configuration, and deploy a host-owned migration.
5. Exercise concurrent consumption, reuse-limit, stopped, cancelled, expiry, retention, purge, and store-unavailable paths.
6. Confirm accepted grant identifiers remain correlated with decision receipts and audit records.

## Verify the published packages

```powershell
gh release download v7.4.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.4.0.nupkg'
gh attestation verify ./AsiBackbone.Core.7.4.0.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect package metadata and Source Link repository commit, compare SHA-256 with
the release evidence manifest, and repeat for each adopted package.

## Build a clean consumer

Create a clean `net10.0` project, add only the required `AsiBackbone.*` packages
at `7.4.0`, restore, build with warnings enabled, exercise the integration
boundary used by the application, and run the consumer's tests.

Consumers upgrading from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

See also:

- [7.4.0 Release Notes](release-notes-740.md)
- [7.4.0 Release Readiness Record](release-readiness-740.md)
- [Signed Capability Grants at ASP.NET Core Endpoints](signed-capability-grants.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
