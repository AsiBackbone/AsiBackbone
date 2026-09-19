# AsiBackbone 6.0.0 Release Notes

Release date: 2026-09-19

## Summary

`6.0.0` is a major release for the AsiBackbone package family. It completes the
planned 6.0 public API: plain-language semantic type and member names, removal
of the members whose `5.x` deprecation windows completed, and security-semantics
corrections that could only ship at a major boundary.

Package IDs, public namespaces, and the `net10.0` target remain unchanged.
`AssemblyVersion` advances to `6.0.0.0`; package and file versions advance to
`6.0.0` and `6.0.0.0` respectively.

Consumers moving from `5.x` must follow the
[Upgrade from 5.x to 6.0](upgrade-500-to-600.md) guide.

## Breaking changes

### Public API names

- 103 of 232 public type entries are renamed to plain-language semantic names.
  Every entry has a recorded retain or rename decision in the
  [6.0 Public API Naming Convention](public-api-naming-600.md). For example,
  `AuditResidue` is now `DecisionReceipt`,
  `IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>` is now
  `IGovernanceConstraint<GovernanceEvaluationContext>`, and
  `DefaultAsiBackbonePolicyEvaluator<TContext>` is now
  `DefaultGovernancePolicyEvaluator<TContext>`.
- Helper members that operate on decision receipts use the same vocabulary, for
  example `CanonicalPayloadBuilder.ForDecisionReceipt` and
  `GovernanceArtifactSigner.SignDecisionReceiptAsync`.
- The request-correlation extension container is now
  `GovernanceHttpRequestCorrelationDecisionReceiptExtensions`, and
  `DecisionReceiptSinkContract` uses `CreateDecisionReceiptSink` and
  `VerifyDecisionReceiptSinkAcceptsValidReceiptAsync`.
- Public decision-receipt parameters on the renamed canonicalization, signing,
  and contract helpers use `receipt` instead of `residue`; named-argument
  callers must update accordingly.
- `RequireGovernancePolicyAttribute` is renamed to `GovernancePolicyAttribute`.
  It records a policy marker and never required or enforced the policy.
- Documentation and diagnostics use the
  [6.0 Product Terminology](terminology-600.md).

### Removed obsolete members

- The five partial-argument policy-evaluator constructors deprecated under
  `ASIB900` in `5.2.0` are removed. Use
  `DefaultGovernancePolicyEvaluator.CreateBuilder<TContext>()` or the
  all-dependencies constructor.
- The two obsolete `RequireGovernancePolicy` route-builder extensions are
  removed. Use `MarkGovernancePolicy`.

### Signature verification

- Pin mismatches deny by default. A `RequiredProvider`, `ExpectedPolicyVersion`,
  or `ExpectedPolicyHash` mismatch reports the new
  `SignatureVerificationCategory.UntrustedSigningContext`, which defaults to
  `Deny`. Previously a wrong provider deferred and a wrong policy context
  escalated.
- `CanonicalizationMismatch` and `MissingSignature` default to `Deny`. Hosts can
  opt a lower-assurance path back into `RequireAcknowledgment` for
  `MissingSignature` through `VerificationPolicyOptions.Create`.
- Providers sign and verify a versioned signature input instead of the
  canonical hash text. `GovernanceSignatureInput.CreateV1` binds the canonical
  descriptors, hash algorithm, hash value, and the `policy_version` and
  `policy_hash` signing metadata, so a signed artifact can no longer be relabeled
  with a different policy context. Host `IManagedKeySigningClient` and
  `IGovernanceSignatureVerificationService` implementations must sign and verify
  the `SignatureInput` bytes.
- Artifacts signed by `5.x` providers fail version 1 verification unless the
  verification context opts in through
  `VerificationPolicyContext.WithLegacySignatureInputAllowed()`. A signature
  accepted that way cannot satisfy a policy pin.

## Added

- `SignatureVerificationCategory.UntrustedSigningContext`.
- `GovernanceSignatureInput`, with `CreateV1` and `CreateLegacy`.
- `SignatureInput` on `SigningRequest`, `SignatureVerificationRequest`, and
  `ManagedKeySignRequest`.
- `VerificationPolicyContext.WithLegacySignatureInputAllowed()` and
  `AllowLegacySignatureInput`.
- `CapabilityGrantUseResult.RetentionElapsed`.
- Issuer-scoped `StopGrant(issuer, grantId)` and `CancelGrant(issuer, grantId)`
  on `InMemoryCapabilityGrantUseStore`.

## Fixed

- `InMemoryCapabilityGrantUseStore` no longer permits replay of an expired grant
  when a validator's `AllowedClockSkew` exceeds the store's
  `EvictionGracePeriod`. A grant past the retention horizon, measured from the
  latest observed use time, is refused with `capability.use-retention-elapsed`.
- Stop and cancel state in the in-memory use store can be scoped to one issuer,
  consistent with issuer-scoped use counts.
- The local-development verifier rejects provider labels other than its own.

## Maintenance updates

- Updated the Entity Framework Core and Microsoft Extensions packages to
  `10.0.12` and Microsoft.NET.Test.Sdk to `18.10.0`.
- Updated dotnet-stryker to `5.0.0` and refreshed pinned GitHub Actions used by
  CodeQL and workflow-security checks.

## Compatibility

- **Source and binary breaking:** public type and member renames, removed
  obsolete members, and the `GovernancePolicyAttribute` rename. No compatibility
  aliases are provided.
- **Behavior breaking:** verification default actions, the signature-input wire
  format, and the in-memory use-store retention horizon.
- **Unchanged:** package IDs, public namespaces, the `net10.0` target, DI
  registration method names, JSON keys, schema versions, canonical artifact tags,
  canonical payload bytes, diagnostic IDs, and EF table and column names. A type
  rename alone does not require a data migration.
- Hosts that persist `SignatureVerificationCategory` as an integer must accept the
  new value `12`.
- The EF Core audit ledger store does not persist the signing metadata
  dictionary, so a ledger record signed with policy metadata does not re-verify
  after an EF Core round trip. See the
  [migration guide](upgrade-500-to-600.md#signature-input).
- NuGet package author signing remains intentionally deferred.

## Release evidence and package signing

The stable release workflow should retain the attested build packages, matching
SPDX SBOMs, `sbom-manifest.json`, `release-evidence-manifest.json`, and a copy of
these release notes as durable GitHub release assets.

NuGet.org repository-signs packages during ingestion. That repository signature
is a separate trust signal from GitHub build-package attestations and does not
turn the project into an author-signed package publisher.

The current author-signing decision remains documented in the
[NuGet Package Signing Decision Record](nuget-package-signing-decision.md).

## Validation

The final release candidate should pass:

- version consistency for `6.0.0` and `v6.0.0`;
- locked restore and Debug/Release solution builds;
- formatting and warning-as-error checks;
- the complete test suite;
- public API baseline and package-validation checks, with the intentional 6.0
  breaks recorded as exact exceptions;
- XML-documentation validation;
- DocFX, documentation-link, continuity, and release-claim validation;
- package creation, NuGet metadata validation, and package SBOM generation;
- template, external-consumer, and stable-package smoke tests; and
- required security, dependency, workflow-security, and supply-chain checks.

After publication, validate Source Link repository commit metadata with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 6.0.0
```

See the [6.0.0 Consumer Verification Guide](consumer-verification-600.md) and
[6.0.0 Release Readiness Record](release-readiness-600.md).
