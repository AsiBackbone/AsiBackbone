# AsiBackbone 7.3.0 Consumer Verification Guide

This guide records the consumer checks for the `7.3.0` package family. Perform
the artifact-verification steps against the published `v7.3.0` GitHub Release
and NuGet packages after publication.

## Confirm the compatibility boundary

- Package IDs remain `AsiBackbone.*`.
- All packages continue to target `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.3.0` and `7.3.0.0`.
- Existing persisted schemas, existing canonical wire values, public namespaces,
  and documented defaults are unchanged.
- Existing hosts see no signed-grant registration or runtime behavior change
  unless they opt in.

## Verify signed capability grant adoption

For hosts that opt in:

1. Register `AddAsiBackboneSignedCapabilityGrants(...)` or the equivalent
   builder facade, one request-binding provider, a host-owned
   `IGovernanceSignatureVerificationService`, and a shared
   `ICapabilityGrantUseStore` when multiple replicas can serve the endpoint.
2. Configure the expected issuer, audience, clock skew, and production key
   material. Treat the local-development signing package as development-only.
3. Confirm the grant binds the authenticated actor, endpoint operation and
   scopes, current policy identity, and the intended request content.
4. Exercise invalid proof, expiry, future validity, wrong actor, wrong request,
   replay, store-unavailable, acknowledgment, and key-rotation paths.
5. Confirm accepted grant identifiers appear in decision receipts and audit
   records without changing the canonical representation of receipts that do
   not carry a grant.

See [Signed Capability Grants at ASP.NET Core Endpoints](signed-capability-grants.md)
for the full host-owned security boundary.

## Verify the published packages

Download a package and its release evidence from the `v7.3.0` GitHub Release,
then verify its provenance:

```powershell
gh release download v7.3.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.3.0.nupkg'
gh attestation verify ./AsiBackbone.Core.7.3.0.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect the package metadata and Source Link repository commit, and compare the
package SHA-256 value with the release evidence manifest. Repeat for each
package adopted by the application.

## Build a clean consumer

Create a clean `net10.0` project, add only the required `AsiBackbone.*` packages
at `7.3.0`, restore, build with warnings enabled, and exercise the integration
boundary used by the application. Run the consumer's own tests.

Consumers upgrading from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md); the analyzer and deprecation
checks for the `7.1` line are in the
[7.1.0 Consumer Verification Guide](consumer-verification-710.md).

See also:

- [7.3.0 Release Notes](release-notes-730.md)
- [7.3.0 Release Readiness Record](release-readiness-730.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
