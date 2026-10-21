# AsiBackbone 7.3.0 Release Notes

Release date: 2026-10-20

`7.3.0` is a backward-compatible minor release for the stable `7.x` package
family. It adds an opt-in signed capability grant path for ASP.NET Core
endpoints, together with the Core signing, token, replay-protection, request
binding, and audit-correlation pieces needed to use that path safely.

Package IDs, public namespaces, the `net10.0` target, existing persisted
schemas, existing canonical wire values, documented defaults, and binary
assembly identity are unchanged. `AssemblyVersion` remains `7.0.0.0`; package
and file versions advance to `7.3.0` and `7.3.0.0`.

## Signed capability grants at endpoints

- Added `AddAsiBackboneSignedCapabilityGrants(...)` and
  `UseSignedCapabilityGrants(...)` to register the opt-in
  `SignedCapabilityGrantEndpointValidator`.
- Added the versioned `CapabilityGrantToken` transport format and
  `GovernanceArtifactSigner` helpers for creating and signing grants.
- Added request-binding provider abstractions and the built-in form-field
  binding provider so a grant can be tied to the content it authorizes.
- Added strict actor, operation, scope, policy, issuer, audience, proof, and use
  validation. Every validation result other than `Valid` is denied at the
  endpoint boundary.
- Preserved acknowledgment challenges without prematurely consuming a
  single-use grant; the acknowledged retry validates again and consumes it.
- Added `IEndpointCapabilityGrantFeature` and decision-receipt grant identifiers
  so accepted authority can be correlated with audit evidence.
- Added configured PEM key support to the local-development signing provider,
  including verify-only public-key configuration.

See [Signed Capability Grants at ASP.NET Core Endpoints](signed-capability-grants.md)
for registration, issuance, request-binding, rotation, and multi-replica replay
storage guidance.

## Compatibility

- Every new endpoint integration is opt-in. Hosts that do not register the
  signed-grant validator have no registration or runtime behavior change.
- Hosts with an existing claim-backed `IEndpointCapabilityGrantValidator`
  remain supported; signed-grant registration refuses to replace it.
- Existing decision receipts retain their previous canonical payload and hash.
  `capabilityGrantId` is included only when a receipt carries one.
- The capability-grant token is a new serialized format and does not modify an
  existing durable format.
- Package IDs, public namespaces, target framework, and assembly identity remain
  compatible across the `7.x` line.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.3.0` together.
2. If adopting signed grants, register the validator, a verification service, a
   replay-aware `ICapabilityGrantUseStore`, and a request-binding provider.
3. Configure the expected issuer, audience, clock skew, and host-owned key
   material, then exercise acknowledgment and replay behavior in integration
   tests.
4. Restore, rebuild with warnings enabled, and run the consumer's tests.

No data migration or configuration change is required for hosts that do not opt
in. Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

The release candidate is validated through locked restore, Debug and Release
builds, the full test suite, formatting, public API baselines, package metadata
and contents, template and external-consumer smoke tests, documentation release
claims, and DocFX. Stable publication occurs only from the protected `v7.3.0`
tag after the release-preparation pull request is merged to `main`.

See also:

- [7.3.0 Release Readiness Record](release-readiness-730.md)
- [7.3.0 Consumer Verification Guide](consumer-verification-730.md)
- [7.2.1 Release Notes](release-notes-721.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
