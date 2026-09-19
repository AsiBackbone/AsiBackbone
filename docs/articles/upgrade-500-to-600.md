# Upgrade from 5.x to 6.0

Version 6.0 renames public types and removes seven public members whose obsolete compatibility windows have completed. It also makes signature-verification pin mismatches and missing signatures deny by default, and changes providers to sign a versioned signature input that covers the signing policy context; see [Verification policy defaults](#verification-policy-defaults) and [Signature input](#signature-input). These are intentional major-version breaks. Rebuild consumers against the 6.0 packages after migrating.

## Complete obsolete-member inventory

The repository-wide inventory of source attributes (`Obsolete` / `ObsoleteAttribute`), public API baselines, warning suppressions, analyzer diagnostics, tests, samples, and documentation found exactly these seven obsolete public members. No other obsolete aliases or forwarding APIs were found.

| Removed API | Supported replacement |
| --- | --- |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy = null)` | Builder with `AddConstraints` and `WithDecisionPolicy` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy, options)` | Builder with `AddConstraints`, `WithDecisionPolicy`, and `WithOptions` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy, options, logger)` | Builder with `AddConstraints`, `WithDecisionPolicy`, `WithOptions`, and `WithLogger` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, threatModelContributors, decisionPolicy = null)` | Builder with `AddConstraints`, `AddThreatModelContributors`, and `WithDecisionPolicy` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, threatModelContributors, decisionPolicy, options)` | Builder with `AddConstraints`, `AddThreatModelContributors`, `WithDecisionPolicy`, and `WithOptions` |
| `RequireGovernancePolicy<TPolicy>(RouteHandlerBuilder)` | `MarkGovernancePolicy<TPolicy>()` for decision-policy types; `MarkGovernancePolicy(typeof(TPolicy))` for plain marker types |
| `RequireGovernancePolicy<TBuilder>(TBuilder, Type)` | `MarkGovernancePolicy(builder, policyType)` |

Start manual construction with `DefaultGovernancePolicyEvaluator.CreateBuilder<TContext>()`, configure the dependencies, and call `Build()`. For any removed evaluator constructor, direct construction and dependency injection can instead use the supported full-dependency constructor:

```csharp
var evaluator = new DefaultGovernancePolicyEvaluator<MyPolicyContext>(
    constraints,
    threatModelContributors: null,
    decisionPolicy: null,
    options: null,
    logger: null);
```

Supply the host's actual contributors, decision policy, options, and logger wherever configured. Null optional dependencies retain the evaluator's defaults; empty constraints still deny by default.

Type-based dependency injection registration now has only the full constructor to activate. It requires all five dependencies to be resolvable, including the concrete evaluator options object. Hosts using the options pattern should use a factory instead, passing `IOptions<GovernancePolicyOptions>.Value` to `WithOptions` (or the full constructor), and resolving the other configured dependencies explicitly. The sample and template use this factory pattern so unregistered optional dependencies retain their defaults.

`MarkGovernancePolicy` records the same policy metadata. It does not resolve the policy or select/enforce constraints solely from the marker. The attribute formerly named `RequireGovernancePolicyAttribute` is renamed to `GovernancePolicyAttribute` for the same reason; see [Helper member renames](#helper-member-renames).

## Diagnostics and compatibility validation

The internal `AsiBackboneObsoletions` helper and its ASIB900 message, ID, and URL constants existed solely for the removed constructors and were deleted. ASIB900 came from the compiler's obsolete attribute support, not a dedicated Roslyn analyzer; no analyzer diagnostic needed removal. Obsolete-only forwarding and attribute tests and ASIB900 project suppressions were removed. Behavioral evaluator tests now use the full constructor; builder and marker replacement tests remain.

The managed API baselines intentionally remove the seven inventoried members and apply the public type renames below. Package compatibility validation retains its previous-release comparison, with exact exceptions for the intentional type, signature, interface, and generic-constraint changes; other compatibility failures still fail packing. Historical 4.x/5.x release notes and migration guidance remain available.

## Verification policy defaults

5.0 moved a signing-key pin mismatch from `Escalate` to `Deny`, because an artifact signed under the wrong key should not receive a softer outcome than one with a bad signature. 6.0 applies the same rule to the remaining pins and to missing signatures. These are runtime behavior changes with no public signature change, so they take the major-version boundary.

| Condition | 5.x category / code / default action | 6.0 category / code / default action |
| --- | --- | --- |
| `VerificationPolicyContext.RequiredProvider` does not match the signing provider | `ProviderUnavailable` / `signature.provider-unavailable` / `Defer` | `UntrustedSigningContext` / `signature.provider-not-trusted` / `Deny` |
| `ExpectedPolicyVersion` or `ExpectedPolicyHash` does not match signing metadata | `CanonicalizationMismatch` / `signature.canonicalization-mismatch` / `Escalate` | `UntrustedSigningContext` / `signature.policy-context-not-trusted` / `Deny` |
| Canonical artifact descriptors in signing metadata do not match the artifact | `CanonicalizationMismatch` / `signature.canonicalization-mismatch` / `Escalate` | Unchanged category and code / `Deny` |
| Signature metadata is missing | `MissingSignature` / `signature.missing` / `RequireAcknowledgment` | Unchanged category and code / `Deny` |

`ProviderUnavailable` now describes only an operational failure, such as a provider exception or timeout, and still defaults to `Defer`. `UnknownKeyVersion` and `Failed` still default to `Escalate`. Only `Valid` allows.

Capability-grant proof validation follows the same defaults. A provider or policy-context pin mismatch now reports `CapabilityTokenValidationCategory.InvalidProof` with `Deny` instead of `Failed` with `Defer` or `Escalate`, and a provider-reported `CanonicalizationMismatch` now reports `InvalidProof` with `Deny`. Grant validation already denied a missing signature.

Migration actions:

- Update alerting, dashboards, and log queries that match `signature.provider-unavailable` or `signature.canonicalization-mismatch` to also match `signature.provider-not-trusted` and `signature.policy-context-not-trusted`.
- Hosts that persist `SignatureVerificationCategory` as an integer must accept the new value `UntrustedSigningContext = 12`. Existing numeric values are unchanged.
- Hosts that deliberately accept unsigned artifacts on a lower-assurance path can restore the previous behavior for that path only:

```csharp
VerificationPolicyOptions lowerAssurance = VerificationPolicyOptions.Create(
    new Dictionary<SignatureVerificationCategory, VerificationPolicyAction>
    {
        [SignatureVerificationCategory.MissingSignature] = VerificationPolicyAction.RequireAcknowledgment
    });
```

Restoring `Defer` or `Escalate` for `UntrustedSigningContext` is possible through the same override but is not recommended: a retry or an approval cannot make an artifact signed under the wrong provider or policy context trustworthy.

## Signature input

Before 6.0, providers signed only the UTF-8 text of the canonical payload hash, so every value in `SigningMetadata` was an unauthenticated label. A holder of a validly signed artifact could change `policy_version` or `policy_hash` and verification still succeeded, which also meant `ExpectedPolicyVersion` and `ExpectedPolicyHash` pins checked values the signature did not cover.

6.0 signs and verifies a versioned signature input instead. See [What the signature covers](cryptographic-security-posture.md#what-the-signature-covers) for the exact format and trust model.

| Surface | 6.0 change |
| --- | --- |
| `GovernanceSignatureInput` | New. `CreateV1(hash, metadata)` builds the version 1 input; `CreateLegacy(signingHash)` builds the pre-6.0 hash-only input. |
| `SigningRequest.SignatureInput`, `SignatureVerificationRequest.SignatureInput` | New. The exact bytes to sign or verify. Falls back to the hash-only input when not supplied; `UsesLegacySignatureInput` reports the fallback. |
| `GovernanceArtifactSigner` | Supplies the version 1 input and restores the signed `policy_version` and `policy_hash` in the stored metadata. |
| `GovernanceArtifactVerifier` | Rebuilds and verifies the version 1 input. |
| `VerificationPolicyContext.WithLegacySignatureInputAllowed()` | New opt-in for artifacts signed before 6.0. |
| `ManagedKeySignRequest.SignatureInput` | New. The managed-key client must sign these bytes. |
| Local-development provider | Signs and verifies the supplied input, and rejects provider labels it does not own (`localdev.signature.provider-not-trusted`, `UntrustedSigningContext`, `Deny`). |

Migration actions:

- **Host managed-key clients** (`IManagedKeySigningClient`) must sign `ManagedKeySignRequest.SignatureInput` instead of `SigningHash`. Pass the bytes as the message to a message-signing API, or hash them with the key's digest algorithm before calling a digest-signing API.
- **Host verification services** (`IGovernanceSignatureVerificationService`) must verify `SignatureVerificationRequest.SignatureInput` instead of `SigningHash`, resolve the verification key from the recorded key ID and key version, and reject provider labels they do not own.
- **Custom signing flows** that sign signing-ready artifacts outside `GovernanceArtifactSigner` must sign `GovernanceSignatureInput.CreateV1(canonicalHash, signingMetadata)` with the same policy metadata they record.
- **Persistence** that rehydrates signed artifacts must retain the `policy_version` and `policy_hash` signing metadata values that were signed. The EF Core audit ledger store persists the individual signature fields but not the signing metadata dictionary, so a ledger record signed with policy metadata does not re-verify after an EF Core round trip. Records signed without policy metadata are unaffected. Hosts that need both should persist the signed policy metadata in a host-owned column.
- **Historical artifacts** signed by 5.x providers fail version 1 verification as `InvalidSignature`. Legacy retry is disabled by default; explicitly opt in by passing a context from `WithLegacySignatureInputAllowed()`. Such signatures authenticate the hash only and cannot satisfy a policy pin (`signature.policy-context-not-authenticated`).

A provider that ignores `SignatureInput` and keeps signing or verifying the hash text fails closed against artifacts produced by the other side of the change, rather than silently accepting unauthenticated labels, as long as signer and verifier are not both left on the hash text. Update both together.

## In-memory capability grant use store

`InMemoryCapabilityGrantUseStore` evicted use records once a grant had been expired for longer than `EvictionGracePeriod`, while `CapabilityGrantValidator` still accepts an expired grant within `AllowedClockSkew`. With a skew above the grace period, a grant's record was evicted while the grant still validated, and the next use was accepted as a first use: a replay.

6.0 changes the store as follows:

- A grant expired for longer than `EvictionGracePeriod`, measured from the latest use time the store has observed, is refused with `ReuseLimitExceeded` and `capability.use-retention-elapsed` (new `CapabilityGrantUseResult.RetentionElapsed`). Measuring from the latest observed time prevents a caller-supplied earlier time from resurrecting an evicted grant.
- `EvictionGracePeriod` rejects negative values.
- New `StopGrant(issuer, grantId)` and `CancelGrant(issuer, grantId)` overloads affect one issuer's grant. The identifier-only overloads still affect every issuer's grant using the identifier, now documented as such.

Migration action: set `EvictionGracePeriod` to at least the largest `AllowedClockSkew` used with the store. With the defaults (zero skew, five-minute grace period) no change is needed.

## Helper member renames

The 6.0 terminology change also renames public helpers that operate on decision
receipts. Apart from the explicitly documented `residue` to `receipt` parameter
rename below, parameter types, return types, behavior, canonical artifact tags,
signed bytes, JSON keys, and EF table and column names are unchanged.

| 5.x member | 6.0 member |
| --- | --- |
| `CanonicalPayloadBuilder.ForAuditResidue` | `ForDecisionReceipt` |
| `CanonicalPayloadBuilder.ForAuditResidueLifecycleEvent` | `ForDecisionReceiptLifecycleEvent` |
| `GovernanceArtifactSigner.CreateUnsignedAuditResidue` | `CreateUnsignedDecisionReceipt` |
| `GovernanceArtifactSigner.CreateSigningReadyAuditResidue` | `CreateSigningReadyDecisionReceipt` |
| `GovernanceArtifactSigner.SignAuditResidueAsync` | `SignDecisionReceiptAsync` |
| `GovernanceArtifactSigner.CreateUnsignedAuditResidueLifecycleEvent` | `CreateUnsignedDecisionReceiptLifecycleEvent` |
| `GovernanceArtifactSigner.CreateSigningReadyAuditResidueLifecycleEvent` | `CreateSigningReadyDecisionReceiptLifecycleEvent` |
| `GovernanceArtifactSigner.SignAuditResidueLifecycleEventAsync` | `SignDecisionReceiptLifecycleEventAsync` |
| `AsiBackboneHttpRequestCorrelationAuditExtensions.CreateAuditResidue` | `GovernanceHttpRequestCorrelationDecisionReceiptExtensions.CreateDecisionReceipt` |
| `GovernanceDecisionContract.VerifyAuditResidue` | `VerifyDecisionReceipt` |
| `AsiBackboneAuditSinkContract.CreateAuditResidue()` | `DecisionReceiptSinkContract.CreateDecisionReceipt()` |
| `AsiBackboneAuditSinkContract.CreateAuditSink()` | `DecisionReceiptSinkContract.CreateDecisionReceiptSink()` |
| `AsiBackboneAuditSinkContract.VerifyAuditSinkAcceptsValidResidueAsync()` | `DecisionReceiptSinkContract.VerifyDecisionReceiptSinkAcceptsValidReceiptAsync()` |
| `RequireGovernancePolicyAttribute` / `[RequireGovernancePolicy(...)]` | `GovernancePolicyAttribute` / `[GovernancePolicy(...)]` |

Contract-test fixtures derived from `DecisionReceiptSinkContract` must rename
their protected `CreateAuditResidue` and `CreateAuditSink` overrides to
`CreateDecisionReceipt` and `CreateDecisionReceiptSink`, respectively. Calls to
the contract verification method must use
`VerifyDecisionReceiptSinkAcceptsValidReceiptAsync`.

The public `IDecisionReceipt` parameter on `ForDecisionReceipt`,
`CreateUnsignedDecisionReceipt`, `CreateSigningReadyDecisionReceipt`,
`SignDecisionReceiptAsync`, and `VerifyDecisionReceipt` is named `receipt` in
6.0. Named-argument callers must update `residue:` to `receipt:`.

Protocol and persisted names remain unchanged: the `AuditResidueId` member
family, `CanonicalArtifactTypes.AuditResidue`, and
`CanonicalArtifactTypes.AuditResidueLifecycleEvent` retain their established
wire meaning.

## Public type renames

See [6.0 public API naming convention](public-api-naming-600.md) for the complete inventory, qualifier decisions, namespace review, terminology coordination with #781, and retained names. No compatibility aliases are carried forward. Namespace and generic arity stay the same.

| Package | 5.x type | 6.0 type |
| --- | --- | --- |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Actors.AsiBackboneHttpActorContextOptions` | `AsiBackbone.AspNetCore.Actors.HttpGovernanceActorContextOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Actors.HttpContextAsiBackboneActorContextResolver` | `AsiBackbone.AspNetCore.Actors.HttpContextGovernanceActorContextResolver` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Actors.IAsiBackboneHttpActorContextResolver` | `AsiBackbone.AspNetCore.Actors.IHttpGovernanceActorContextResolver` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.AsiBackboneHttpRequestCorrelation` | `AsiBackbone.AspNetCore.Correlation.GovernanceHttpRequestCorrelation` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.AsiBackboneHttpRequestCorrelationAuditExtensions` | `AsiBackbone.AspNetCore.Correlation.GovernanceHttpRequestCorrelationDecisionReceiptExtensions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.AsiBackboneHttpRequestMetadataKeys` | `AsiBackbone.AspNetCore.Correlation.GovernanceHttpRequestMetadataKeys` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.HttpContextAsiBackboneRequestCorrelationResolver` | `AsiBackbone.AspNetCore.Correlation.HttpContextGovernanceRequestCorrelationResolver` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.IAsiBackboneHttpRequestCorrelationResolver` | `AsiBackbone.AspNetCore.Correlation.IHttpGovernanceRequestCorrelationResolver` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.DependencyInjection.AsiBackboneAspNetCoreOptions` | `AsiBackbone.AspNetCore.DependencyInjection.AspNetCoreGovernanceOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceApplicationBuilderExtensions` | `AsiBackbone.AspNetCore.Endpoints.EndpointGovernanceApplicationBuilderExtensions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceDescriptor` | `AsiBackbone.AspNetCore.Endpoints.EndpointGovernanceDescriptor` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceMetadataMode` | `AsiBackbone.AspNetCore.Endpoints.EndpointGovernanceMetadataMode` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceMiddleware` | `AsiBackbone.AspNetCore.Endpoints.EndpointGovernanceMiddleware` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceOptions` | `AsiBackbone.AspNetCore.Endpoints.EndpointGovernanceOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceResult` | `AsiBackbone.AspNetCore.Endpoints.EndpointGovernanceResult` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceRouteBuilderExtensions` | `AsiBackbone.AspNetCore.Endpoints.EndpointGovernanceRouteBuilderExtensions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.DefaultAsiBackboneEndpointGovernanceService` | `AsiBackbone.AspNetCore.Endpoints.DefaultEndpointGovernanceService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointAuditEmissionMetadata` | `AsiBackbone.AspNetCore.Endpoints.IEndpointAuditEmissionMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointCapabilityGrantMetadata` | `AsiBackbone.AspNetCore.Endpoints.IEndpointCapabilityGrantMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointCapabilityGrantValidator` | `AsiBackbone.AspNetCore.Endpoints.IEndpointCapabilityGrantValidator` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointGovernanceMetadata` | `AsiBackbone.AspNetCore.Endpoints.IEndpointGovernanceMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointGovernancePolicyMetadata` | `AsiBackbone.AspNetCore.Endpoints.IEndpointGovernancePolicyMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointGovernanceService` | `AsiBackbone.AspNetCore.Endpoints.IEndpointGovernanceService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointLiabilityHandshakeMetadata` | `AsiBackbone.AspNetCore.Endpoints.IEndpointLiabilityHandshakeMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointPolicyEvaluationOptionsMetadata` | `AsiBackbone.AspNetCore.Endpoints.IEndpointPolicyEvaluationOptionsMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallenge` | `AsiBackbone.AspNetCore.Handshakes.AcknowledgmentChallenge` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallengeOptions` | `AsiBackbone.AspNetCore.Handshakes.AcknowledgmentChallengeOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallengeRequest` | `AsiBackbone.AspNetCore.Handshakes.AcknowledgmentChallengeRequest` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallengeResult` | `AsiBackbone.AspNetCore.Handshakes.AcknowledgmentChallengeResult` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.DefaultAsiBackboneAcknowledgmentChallengeService` | `AsiBackbone.AspNetCore.Handshakes.DefaultAcknowledgmentChallengeService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.IAsiBackboneAcknowledgmentChallengeService` | `AsiBackbone.AspNetCore.Handshakes.IAcknowledgmentChallengeService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Outbox.AsiBackboneGovernanceOutboxDrainHostedService` | `AsiBackbone.AspNetCore.Outbox.GovernanceOutboxDrainHostedService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Outbox.AsiBackboneGovernanceOutboxDrainWorkerOptions` | `AsiBackbone.AspNetCore.Outbox.GovernanceOutboxDrainWorkerOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Results.AsiBackboneHttpResultMappingExtensions` | `AsiBackbone.AspNetCore.Results.GovernanceHttpResultMappingExtensions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Results.AsiBackboneHttpResultMappingOptions` | `AsiBackbone.AspNetCore.Results.GovernanceHttpResultMappingOptions` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Actors.AsiBackboneActorContext` | `AsiBackbone.Core.Actors.GovernanceActorContext` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Actors.AsiBackboneActorType` | `AsiBackbone.Core.Actors.GovernanceActorType` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Actors.IAsiBackboneActorContext` | `AsiBackbone.Core.Actors.IGovernanceActorContext` |
| `AsiBackbone.Core` | `AsiBackbone.Core.AsiBackboneIdentifierLimits` | `AsiBackbone.Core.GovernanceIdentifierLimits` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditResidue` | `AsiBackbone.Core.Audit.DecisionReceipt` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditResidueBuilder` | `AsiBackbone.Core.Audit.DecisionReceiptBuilder` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditResidueLifecycleEvent` | `AsiBackbone.Core.Audit.DecisionReceiptLifecycleEvent` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditResidueLifecycleStage` | `AsiBackbone.Core.Audit.DecisionReceiptLifecycleStage` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.IAsiBackboneAuditLedgerStore` | `AsiBackbone.Core.Audit.IGovernanceAuditLedgerStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.IAsiBackboneAuditResidue` | `AsiBackbone.Core.Audit.IDecisionReceipt` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.IAsiBackboneAuditResidueLifecycleStore` | `AsiBackbone.Core.Audit.IDecisionReceiptLifecycleStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.IAsiBackboneAuditSink` | `AsiBackbone.Core.Audit.IDecisionReceiptSink` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DefaultAsiBackboneDlpFailurePolicyResolver` | `AsiBackbone.Core.Classification.DefaultDlpFailurePolicyResolver` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.IAsiBackboneDlpFailurePolicyResolver` | `AsiBackbone.Core.Classification.IDlpFailurePolicyResolver` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Constraints.AsiBackboneConstraintEvaluationContext` | `AsiBackbone.Core.Constraints.GovernanceEvaluationContext` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Constraints.IAsiBackboneConstraintEvaluationContext` | `AsiBackbone.Core.Constraints.IGovernanceEvaluationContext` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Constraints.IAsiBackboneConstraint`1` | `AsiBackbone.Core.Constraints.IGovernanceConstraint`1` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.IAsiBackboneGovernanceEmitter` | `AsiBackbone.Core.Emissions.IGovernanceEmitter` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Entities.AsiBackboneEntity` | `AsiBackbone.Core.Entities.GovernanceEntity` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Entities.IAsiBackboneEntity` | `AsiBackbone.Core.Entities.IGovernanceEntity` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.AsiBackbonePolicyEvaluatorBuilder`1` | `AsiBackbone.Core.Evaluation.GovernancePolicyEvaluatorBuilder`1` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.AsiBackbonePolicyEvaluatorOptions` | `AsiBackbone.Core.Evaluation.GovernancePolicyOptions` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.DefaultAsiBackbonePolicyEvaluator` | `AsiBackbone.Core.Evaluation.DefaultGovernancePolicyEvaluator` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.DefaultAsiBackbonePolicyEvaluator`1` | `AsiBackbone.Core.Evaluation.DefaultGovernancePolicyEvaluator`1` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.IAsiBackboneDecisionPolicy`1` | `AsiBackbone.Core.Evaluation.IGovernanceDecisionPolicy`1` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.IAsiBackbonePolicyEvaluator`1` | `AsiBackbone.Core.Evaluation.IGovernancePolicyEvaluator`1` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.AsiBackboneGovernanceOutboxDrain` | `AsiBackbone.Core.Outbox.GovernanceOutboxDrain` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.AsiBackboneGovernanceOutboxOptions` | `AsiBackbone.Core.Outbox.GovernanceOutboxOptions` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.IAsiBackboneGovernanceOutboxClaimOutcomeStore` | `AsiBackbone.Core.Outbox.IGovernanceOutboxClaimOutcomeStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.IAsiBackboneGovernanceOutboxClaimStore` | `AsiBackbone.Core.Outbox.IGovernanceOutboxClaimStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.IAsiBackboneGovernanceOutboxStore` | `AsiBackbone.Core.Outbox.IGovernanceOutboxStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Results.BackboneResult` | `AsiBackbone.Core.Results.GovernanceOperationResult` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Serialization.AsiBackboneSchemaVersions` | `AsiBackbone.Core.Serialization.GovernanceSchemaVersions` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.IAsiBackboneSignatureVerificationService` | `AsiBackbone.Core.Signing.IGovernanceSignatureVerificationService` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.IAsiBackboneSigningService` | `AsiBackbone.Core.Signing.IGovernanceSigningService` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Audit.EfCoreAuditResidueLifecycleStore` | `AsiBackbone.EntityFrameworkCore.Audit.EfCoreDecisionReceiptLifecycleStore` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneAuditLedgerMetadataEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.AuditLedgerMetadataEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneAuditLedgerReasonCodeEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.AuditLedgerReasonCodeEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneAuditLedgerRecordEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.AuditLedgerRecordEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneAuditResidueLifecycleEventEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.DecisionReceiptLifecycleEventEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneGovernanceOutboxEntryEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.GovernanceOutboxEntryEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneHandshakeAcknowledgmentEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.HandshakeAcknowledgmentEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneHandshakeAcknowledgmentMetadataEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.HandshakeAcknowledgmentMetadataEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneHandshakeRequestEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.HandshakeRequestEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneHandshakeRequestMetadataEntityConfiguration` | `AsiBackbone.EntityFrameworkCore.Configurations.HandshakeRequestMetadataEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneAuditLedgerMetadataEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.AuditLedgerMetadataEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneAuditLedgerReasonCodeEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.AuditLedgerReasonCodeEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneAuditLedgerRecordEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.AuditLedgerRecordEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneAuditResidueLifecycleEventEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.DecisionReceiptLifecycleEventEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneGovernanceOutboxEntryEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.GovernanceOutboxEntryEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneHandshakeAcknowledgmentEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.HandshakeAcknowledgmentEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneHandshakeAcknowledgmentMetadataEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.HandshakeAcknowledgmentMetadataEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneHandshakeRequestEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.HandshakeRequestEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneHandshakeRequestMetadataEntity` | `AsiBackbone.EntityFrameworkCore.Persistence.HandshakeRequestMetadataEntity` |
| `AsiBackbone.Storage.InMemory` | `AsiBackbone.Storage.InMemory.Audit.InMemoryAuditResidueLifecycleStore` | `AsiBackbone.Storage.InMemory.Audit.InMemoryDecisionReceiptLifecycleStore` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestAuditSink` | `AsiBackbone.Testing.GovernanceTestDecisionReceiptSink` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestHarnessEndpointCapabilityGrantValidator` | `AsiBackbone.Testing.GovernanceTestHarnessEndpointCapabilityGrantValidator` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestHarnessOptions` | `AsiBackbone.Testing.GovernanceTestHarnessOptions` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestHarnessPolicyEvaluator` | `AsiBackbone.Testing.GovernanceTestHarnessPolicyEvaluator` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestHarnessServiceCollectionExtensions` | `AsiBackbone.Testing.GovernanceTestHarnessServiceCollectionExtensions` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestSigningService` | `AsiBackbone.Testing.GovernanceTestSigningService` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneAuditSinkContract` | `AsiBackbone.Testing.Contracts.DecisionReceiptSinkContract` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneConstraintContract`1` | `AsiBackbone.Testing.Contracts.GovernanceConstraintContract`1` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneContractViolationException` | `AsiBackbone.Testing.Contracts.GovernanceContractViolationException` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneDecisionContract` | `AsiBackbone.Testing.Contracts.GovernanceDecisionContract` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneDecisionPolicyContract`1` | `AsiBackbone.Testing.Contracts.GovernanceDecisionPolicyContract`1` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneEndpointCapabilityGrantValidatorContract` | `AsiBackbone.Testing.Contracts.EndpointCapabilityGrantValidatorContract` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackbonePolicyEvaluatorContract`1` | `AsiBackbone.Testing.Contracts.GovernancePolicyEvaluatorContract`1` |

Protocol members such as AuditResidueId retain their names and wire meaning; helper methods named for the audit-residue vocabulary are renamed as listed in [Helper member renames](#helper-member-renames). JSON keys, schema versions, canonical tags, signed bytes, and EF table/column names are unchanged. A type rename alone does not require a data migration. Type-based DI, reflection, custom receipt implementations, and host EF model configuration must reference the new CLR types. Rebuild all dependent assemblies.
