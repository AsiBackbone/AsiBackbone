# AsiBackbone 7.2.1 Consumer Verification Guide

This guide records the consumer checks for the `7.2.1` package family. Perform
the artifact-verification steps against the published `v7.2.1` GitHub Release
and NuGet packages after publication.

## Confirm the compatibility boundary

- Package IDs remain `AsiBackbone.*`.
- All packages continue to target `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.2.1` and `7.2.1.0`.
- Package dependencies, public APIs, durable schemas, canonical wire values,
  and public namespaces are unchanged from `7.2.0`.

## Verify endpoint capability enforcement

For a governed endpoint that requires a capability grant:

1. Exercise acknowledgment-required, deferred, and escalation-recommended
   policy outcomes without the required grant. Expect denial before any
   challenge, deferral, or escalation flow begins.
2. Repeat with a valid grant. Expect the original governance outcome to be
   preserved so the host continues its normal flow.
3. If capability validation is intentionally fail-open when no validator is
   registered, verify that configured audit and acknowledgment requirements
   still run.

Hosts that implement `IEndpointCapabilityGrantValidator` should return a denial
for a missing grant and otherwise return the supplied `currentDecision`.

## Verify the published packages

Download a package and its release evidence from the `v7.2.1` GitHub Release,
then verify its provenance:

```powershell
gh release download v7.2.1 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.AspNetCore.7.2.1.nupkg'
gh attestation verify ./AsiBackbone.AspNetCore.7.2.1.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect the package metadata and Source Link repository commit, and compare the
package SHA-256 value with the release evidence manifest. Repeat for each
package adopted by the application.

## Build a clean consumer

Create a clean `net10.0` project, add only the required `AsiBackbone.*` packages
at `7.2.1`, restore, build with warnings enabled, and exercise the integration
boundary used by the application. Consumers upgrading from `6.x` must first
complete the [6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

See also:

- [7.2.1 Release Notes](release-notes-721.md)
- [7.2.1 Release Readiness Record](release-readiness-721.md)
- [7.2.0 Release Notes](release-notes-720.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
