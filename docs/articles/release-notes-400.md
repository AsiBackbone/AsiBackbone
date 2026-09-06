# AsiBackbone 4.0.0 Release Notes

Release date: 2026-09-06

## Summary

`4.0.0` starts the stable `4.x` AsiBackbone package family. Package IDs,
public namespaces, and the `net10.0` target remain unchanged. The binary
assembly identity advances to `4.0.0.0`.

This is a major release because governance outbox claim leasing is enabled by
default. The safer default prevents concurrent hosts using the shipped stores
from independently emitting the same pending entry, but custom stores must now
support the claim-store contract or explicitly opt out.

## Highlights

- Enabled governance outbox claim leasing by default with a per-process worker
  identity.
- Added paged claims, bounded abandoned-claim recovery, and configurable
  dead-letter behavior for entries that repeatedly exhaust their leases.
- Added `CanonicalPayloadBuilder.ForCapabilityTokenGrant` so every validated
  grant field can participate in the signed payload.
- Added explicit signature-verification failure categories and preserved the
  configured canonical-payload hash algorithm.
- Sanitized request-derived endpoint metadata before it reaches policy
  evaluation and made unresolved endpoint ordering failures distinguishable.
- Added trusted opt-in for inbound correlation-ID headers.
- Corrected EF Core store registration when hosts use separate DbContext types
  and preserved terminal outbox records during non-claim mutations.
- Made hosted outbox drain dependency failures visible during host startup.
- Deprecated `RequireGovernancePolicy` in favor of the behaviorally accurate
  `MarkGovernancePolicy` name.

## Breaking change

`AsiBackboneGovernanceOutboxOptions.UseClaimLeases` now defaults to `true`, and
`ClaimWorkerId` defaults to the machine name plus process identifier. Both
shipped outbox stores implement `IAsiBackboneGovernanceOutboxClaimStore` and
require no compatibility setting.

A host supplying a custom `IAsiBackboneGovernanceOutboxStore` must either:

1. implement `IAsiBackboneGovernanceOutboxClaimStore`; or
2. explicitly set `UseClaimLeases = false` and accept that concurrent hosts can
   emit the same envelope more than once.

See the [4.0.0 Migration Guide](upgrade-323-to-400.md) for the upgrade steps and
configuration examples.

## Compatibility

- Package IDs and public namespaces remain unchanged.
- Target framework remains `net10.0`.
- `AssemblyVersion` advances from `3.0.0.0` to `4.0.0.0`.
- `FileVersion` advances to `4.0.0.0`.
- Stable persisted artifact formats remain schema-versioned; no incompatible
  durable payload migration is introduced by this release.
- Existing endpoint calls to `RequireGovernancePolicy` continue to compile but
  produce an obsolete warning; use `MarkGovernancePolicy` instead.
- Authentication, authorization, execution enforcement, persistence, key
  custody, monitoring, and operational safety remain host-owned.

## Package signing posture

NuGet package signing remains intentionally deferred. Source Link, SBOMs,
provenance artifacts, release tags, and public source remain complementary
verification signals.

## Release validation

Before tagging `v4.0.0`, run the complete stable release-validation workflow,
including locked restore, Debug and Release builds, formatting, tests, coverage
gates, API review, package and template smoke tests, version consistency,
DocFX/link validation, security checks, SBOM generation, and provenance where
supported.

After publication:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 4.0.0
```

## Related documentation

- [4.0.0 Migration Guide](upgrade-323-to-400.md)
- [4.0.0 Consumer Verification Guide](consumer-verification-400.md)
- [4.0.0 Release Readiness Record](release-readiness-400.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
