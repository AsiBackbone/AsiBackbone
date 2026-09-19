# 6.0 public API naming convention

Use domain qualifiers when they communicate purpose or architectural role. Do not repeat the product name as a namespace substitute.

## Qualifier decisions

| Qualifier | Use |
| --- | --- |
| Governance | Evaluation/actor contexts, constraints, policy evaluation, shared framework limits, signing contracts, and base entities that would otherwise be generic. |
| Policy / Decision | Policy configuration and decision composition; receipts are evidence of a decision, distinct from host execution receipts. |
| Capability / Execution | Delegated authority and host-owned execution. Preserve their existing explicit names. |
| HTTP / Endpoint / EF Core | Host adaptation and persistence roles. Keep these distinctions when removing the product prefix. |
| No added qualifier | Existing descriptive names such as DlpFailurePolicyOptions, AuditLedgerRecord, CapabilityTokenGrant, OperationResult, and ThreatAssessment. |
| AsiBackbone | Product registration/builders, package/namespace identity, assembly discovery, and host-defined analyzer opt-in markers. These identify integration with this particular product. |

## Terminology coordination with #781

DecisionReceipt is the canonical 6.0 name for AuditResidue: evidence of the evaluation outcome and its reasons, not evidence that the host executed an operation. GovernedOperationExecutionReceipt continues to represent host-owned execution. Acknowledgment and capability grant retain their domain meaning. Handshake remains in names for the actual liability/acknowledgment protocol. GovernanceOutbox remains in API names because these stores/drains carry governance artifacts; educational prose may call this an outbox. Governance spine remains architectural positioning, not a type qualifier.

## Namespace review

Keep existing package namespaces and domain subnamespaces (Actors, Constraints, Evaluation, Audit, Signing, Endpoints, Outbox, and provider namespaces). They communicate ownership and maintain discoverability. No namespace moves or compatibility aliases are introduced.

## Full public type inventory

The inventory reviews 232 public type entries across all ten managed package baselines. It renames 103 entries using 102 distinct simple names (the static and generic evaluators share one simple name). The content-only Templates package exposes no managed public types; its generated hosts are updated alongside samples. Each original public type is listed below. Generic arities remain unchanged.

| Package | 5.x public type | 6.0 name / decision |
| --- | --- | --- |
| `AsiBackbone.Analyzers` | `AsiBackbone.Analyzers.GovernanceArtifactPersistenceAnalyzer` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Analyzers` | `AsiBackbone.Analyzers.LocalDevelopmentSigningProductionAnalyzer` | Retain: existing domain/host role is clear. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Actors.AsiBackboneHttpActorContextOptions` | `HttpGovernanceActorContextOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Actors.HttpContextAsiBackboneActorContextResolver` | `HttpContextGovernanceActorContextResolver` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Actors.IAsiBackboneHttpActorContextResolver` | `IHttpGovernanceActorContextResolver` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.AssemblyMarker` | Retain: existing domain/host role is clear. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.AsiBackboneHttpRequestCorrelation` | `GovernanceHttpRequestCorrelation` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.AsiBackboneHttpRequestCorrelationAuditExtensions` | `GovernanceHttpRequestCorrelationDecisionReceiptExtensions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.AsiBackboneHttpRequestMetadataKeys` | `GovernanceHttpRequestMetadataKeys` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.HttpContextAsiBackboneRequestCorrelationResolver` | `HttpContextGovernanceRequestCorrelationResolver` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Correlation.IAsiBackboneHttpRequestCorrelationResolver` | `IHttpGovernanceRequestCorrelationResolver` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.DependencyInjection.AsiBackboneAspNetCoreBuilderExtensions` | Retain: product integration/discovery. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.DependencyInjection.AsiBackboneAspNetCoreOptions` | `AspNetCoreGovernanceOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.DependencyInjection.AsiBackboneAspNetCoreServiceCollectionExtensions` | Retain: product integration/discovery. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.DependencyInjection.AsiBackboneRegulatedGovernanceServiceCollectionExtensions` | Retain: product integration/discovery. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.DependencyInjection.AsiBackboneStrictGovernanceServiceCollectionExtensions` | Retain: product integration/discovery. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AllowMissingGovernanceMetadataAttribute` | Retain: existing domain/host role is clear. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceApplicationBuilderExtensions` | `EndpointGovernanceApplicationBuilderExtensions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceDescriptor` | `EndpointGovernanceDescriptor` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceMetadataMode` | `EndpointGovernanceMetadataMode` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceMiddleware` | `EndpointGovernanceMiddleware` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceOptions` | `EndpointGovernanceOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceResult` | `EndpointGovernanceResult` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.AsiBackboneEndpointGovernanceRouteBuilderExtensions` | `EndpointGovernanceRouteBuilderExtensions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.DefaultAsiBackboneEndpointGovernanceService` | `DefaultEndpointGovernanceService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.EmitGovernanceAuditAttribute` | Retain: existing domain/host role is clear. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointAuditEmissionMetadata` | `IEndpointAuditEmissionMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointCapabilityGrantMetadata` | `IEndpointCapabilityGrantMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointCapabilityGrantValidator` | `IEndpointCapabilityGrantValidator` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointGovernanceMetadata` | `IEndpointGovernanceMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointGovernancePolicyMetadata` | `IEndpointGovernancePolicyMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointGovernanceService` | `IEndpointGovernanceService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointLiabilityHandshakeMetadata` | `IEndpointLiabilityHandshakeMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.IAsiBackboneEndpointPolicyEvaluationOptionsMetadata` | `IEndpointPolicyEvaluationOptionsMetadata` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.RequireCapabilityGrantAttribute` | Retain: existing domain/host role is clear. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.RequireGovernancePolicyAttribute` | Rename to `GovernancePolicyAttribute`: the attribute records a policy marker and does not require or enforce the policy, matching the `MarkGovernancePolicy` route-builder rename. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.RequireLiabilityHandshakeAttribute` | Retain: existing domain/host role is clear. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Endpoints.ShortCircuitOnFirstDenialAttribute` | Retain: existing domain/host role is clear. |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallenge` | `AcknowledgmentChallenge` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallengeOptions` | `AcknowledgmentChallengeOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallengeRequest` | `AcknowledgmentChallengeRequest` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallengeResult` | `AcknowledgmentChallengeResult` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.DefaultAsiBackboneAcknowledgmentChallengeService` | `DefaultAcknowledgmentChallengeService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Handshakes.IAsiBackboneAcknowledgmentChallengeService` | `IAcknowledgmentChallengeService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Outbox.AsiBackboneGovernanceOutboxDrainHostedService` | `GovernanceOutboxDrainHostedService` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Outbox.AsiBackboneGovernanceOutboxDrainWorkerOptions` | `GovernanceOutboxDrainWorkerOptions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Results.AsiBackboneHttpResultMappingExtensions` | `GovernanceHttpResultMappingExtensions` |
| `AsiBackbone.AspNetCore` | `AsiBackbone.AspNetCore.Results.AsiBackboneHttpResultMappingOptions` | `GovernanceHttpResultMappingOptions` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Actors.AsiBackboneActorContext` | `GovernanceActorContext` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Actors.AsiBackboneActorType` | `GovernanceActorType` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Actors.IAsiBackboneActorContext` | `IGovernanceActorContext` |
| `AsiBackbone.Core` | `AsiBackbone.Core.AsiBackboneIdentifierLimits` | `GovernanceIdentifierLimits` |
| `AsiBackbone.Core` | `AsiBackbone.Core.AssemblyReference` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditLedgerRecord` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditResidue` | `DecisionReceipt` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditResidueBuilder` | `DecisionReceiptBuilder` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditResidueLifecycleEvent` | `DecisionReceiptLifecycleEvent` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.AuditResidueLifecycleStage` | `DecisionReceiptLifecycleStage` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.IAsiBackboneAuditLedgerStore` | `IGovernanceAuditLedgerStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.IAsiBackboneAuditResidue` | `IDecisionReceipt` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.IAsiBackboneAuditResidueLifecycleStore` | `IDecisionReceiptLifecycleStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Audit.IAsiBackboneAuditSink` | `IDecisionReceiptSink` |
| `AsiBackbone.Core` | `AsiBackbone.Core.CapabilityTokens.CapabilityGrantUseResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.CapabilityTokens.CapabilityGrantValidationOptions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.CapabilityTokens.CapabilityGrantValidationResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.CapabilityTokens.CapabilityGrantValidator` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.CapabilityTokens.CapabilityTokenGrant` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.CapabilityTokens.CapabilityTokenValidationCategory` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.CapabilityTokens.GrantUseState` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.CapabilityTokens.ICapabilityGrantUseStore` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DefaultAsiBackboneDlpFailurePolicyResolver` | `DefaultDlpFailurePolicyResolver` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DlpClassificationFailureKind` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DlpFailureBehavior` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DlpFailurePolicyContext` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DlpFailurePolicyKey` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DlpFailurePolicyOptions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DlpFailurePolicyResolution` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DlpFailureReasonCodes` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.DlpIntentRiskLevel` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Classification.IAsiBackboneDlpFailurePolicyResolver` | `IDlpFailurePolicyResolver` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Constraints.AsiBackboneConstraintEvaluationContext` | `GovernanceEvaluationContext` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Constraints.ConstraintEvaluationOutcome` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Constraints.ConstraintEvaluationResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Constraints.IAsiBackboneConstraintEvaluationContext` | `IGovernanceEvaluationContext` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Constraints.IAsiBackboneConstraint`1` | `IGovernanceConstraint` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Decisions.GovernanceDecision` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Decisions.GovernanceDecisionOutcome` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.GovernanceEmissionEnvelope` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.GovernanceEmissionError` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.GovernanceEmissionEventType` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.GovernanceEmissionPayload` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.GovernanceEmissionResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.GovernanceEmissionStatus` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.IAsiBackboneGovernanceEmitter` | `IGovernanceEmitter` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Emissions.NoOpGovernanceEmitter` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Entities.AsiBackboneEntity` | `GovernanceEntity` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Entities.IAsiBackboneEntity` | `IGovernanceEntity` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Entities.IConcurrencyTrackedEntity` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.AsiBackbonePolicyEvaluatorBuilder`1` | `GovernancePolicyEvaluatorBuilder` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.AsiBackbonePolicyEvaluatorOptions` | `GovernancePolicyOptions` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.DefaultAsiBackbonePolicyEvaluator` | `DefaultGovernancePolicyEvaluator` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.DefaultAsiBackbonePolicyEvaluator`1` | `DefaultGovernancePolicyEvaluator` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.IAsiBackboneDecisionPolicy`1` | `IGovernanceDecisionPolicy` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Evaluation.IAsiBackbonePolicyEvaluator`1` | `IGovernancePolicyEvaluator` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Handshakes.LiabilityHandshakeAcknowledgment` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Handshakes.LiabilityHandshakeRequest` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Handshakes.LiabilityHandshakeRiskLevel` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.HostIntegration.GovernedOperationExecutionReceipt` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.HostIntegration.GovernedOperationExecutionReceiptCanonicalPayload` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.HostIntegration.GovernedOperationPersistenceOutcome` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.HostIntegration.HostAccountabilityLifecycleEvent` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.HostIntegration.HostAccountabilityMetadataKeys` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Integrity.AuditIntegrityLink` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Integrity.AuditIntegrityVerificationCategory` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Integrity.AuditIntegrityVerificationResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Integrity.AuditIntegrityVerifier` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.DefaultGovernanceMetadataSanitizer` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.GovernanceMetadataBudget` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.GovernanceMetadataBudgetValidationResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.GovernanceMetadataBudgetValidator` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.GovernanceMetadataClassificationContext` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.GovernanceMetadataClassificationResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.GovernanceMetadataSanitizationAction` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.GovernanceMetadataSanitizationReasonCodes` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.GovernanceMetadataSanitizationResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.IGovernanceMetadataClassifier` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Metadata.IGovernanceMetadataSanitizer` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.AsiBackboneGovernanceOutboxDrain` | `GovernanceOutboxDrain` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.AsiBackboneGovernanceOutboxOptions` | `GovernanceOutboxOptions` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.GovernanceOutboxClaim` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.GovernanceOutboxClaimRequest` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.GovernanceOutboxClaimTransitionOutcome` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.GovernanceOutboxClaimTransitionResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.GovernanceOutboxEntry` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.IAsiBackboneGovernanceOutboxClaimOutcomeStore` | `IGovernanceOutboxClaimOutcomeStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.IAsiBackboneGovernanceOutboxClaimStore` | `IGovernanceOutboxClaimStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Outbox.IAsiBackboneGovernanceOutboxStore` | `IGovernanceOutboxStore` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Results.BackboneResult` | `GovernanceOperationResult` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Results.OperationReason` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Results.OperationResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Results.OperationResult`1` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Serialization.AsiBackboneSchemaVersions` | `GovernanceSchemaVersions` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.CanonicalArtifactTypes` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.CanonicalPayload` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.CanonicalPayloadBuilder` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.CanonicalPayloadHash` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.CanonicalPayloadHasher` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.CanonicalPayloadOptions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.GovernanceArtifactSigner` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.GovernanceArtifactVerifier` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.IAsiBackboneSignatureVerificationService` | `IGovernanceSignatureVerificationService` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.IAsiBackboneSigningService` | `IGovernanceSigningService` |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.SignatureVerificationCategory` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.SignatureVerificationRequest` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.SignatureVerificationResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.SignedGovernanceArtifact`1` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.SignedGovernanceArtifacts` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.SigningMetadata` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.SigningRequest` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.SigningResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.VerificationPolicyAction` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.VerificationPolicyContext` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.VerificationPolicyEvaluator` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.VerificationPolicyOptions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.Signing.VerificationPolicyOutcome` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.ThreatModeling.IThreatModelContributor`1` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.ThreatModeling.ThreatAssessment` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.ThreatModeling.ThreatCategories` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Core` | `AsiBackbone.Core.ThreatModeling.ThreatSeverity` | Retain: existing domain/host role is clear. |
| `AsiBackbone.DependencyInjection` | `AsiBackbone.DependencyInjection.AsiBackboneBuilder` | Retain: product integration/discovery. |
| `AsiBackbone.DependencyInjection` | `AsiBackbone.DependencyInjection.AsiBackboneServiceCollectionExtensions` | Retain: product integration/discovery. |
| `AsiBackbone.DependencyInjection` | `AsiBackbone.DependencyInjection.IAsiBackboneBuilder` | Retain: product integration/discovery. |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.AsiBackboneEntityFrameworkCoreBuilderExtensions` | Retain: product integration/discovery. |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.AsiBackboneModelBuilderExtensions` | Retain: product integration/discovery. |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.AssemblyReference` | Retain: existing domain/host role is clear. |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Audit.EfCoreAuditLedgerStore` | Retain: existing domain/host role is clear. |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Audit.EfCoreAuditResidueLifecycleStore` | `EfCoreDecisionReceiptLifecycleStore` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneAuditLedgerMetadataEntityConfiguration` | `AuditLedgerMetadataEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneAuditLedgerReasonCodeEntityConfiguration` | `AuditLedgerReasonCodeEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneAuditLedgerRecordEntityConfiguration` | `AuditLedgerRecordEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneAuditResidueLifecycleEventEntityConfiguration` | `DecisionReceiptLifecycleEventEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneGovernanceOutboxEntryEntityConfiguration` | `GovernanceOutboxEntryEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneHandshakeAcknowledgmentEntityConfiguration` | `HandshakeAcknowledgmentEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneHandshakeAcknowledgmentMetadataEntityConfiguration` | `HandshakeAcknowledgmentMetadataEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneHandshakeRequestEntityConfiguration` | `HandshakeRequestEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Configurations.AsiBackboneHandshakeRequestMetadataEntityConfiguration` | `HandshakeRequestMetadataEntityConfiguration` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Outbox.EfCoreGovernanceOutboxOutcomeStore` | Retain: existing domain/host role is clear. |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Outbox.EfCoreGovernanceOutboxStore` | Retain: existing domain/host role is clear. |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneAuditLedgerMetadataEntity` | `AuditLedgerMetadataEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneAuditLedgerReasonCodeEntity` | `AuditLedgerReasonCodeEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneAuditLedgerRecordEntity` | `AuditLedgerRecordEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneAuditResidueLifecycleEventEntity` | `DecisionReceiptLifecycleEventEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneGovernanceOutboxEntryEntity` | `GovernanceOutboxEntryEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneHandshakeAcknowledgmentEntity` | `HandshakeAcknowledgmentEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneHandshakeAcknowledgmentMetadataEntity` | `HandshakeAcknowledgmentMetadataEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneHandshakeRequestEntity` | `HandshakeRequestEntity` |
| `AsiBackbone.EntityFrameworkCore` | `AsiBackbone.EntityFrameworkCore.Persistence.AsiBackboneHandshakeRequestMetadataEntity` | `HandshakeRequestMetadataEntity` |
| `AsiBackbone.OpenTelemetry` | `AsiBackbone.OpenTelemetry.OpenTelemetryGovernanceAttributes` | Retain: existing domain/host role is clear. |
| `AsiBackbone.OpenTelemetry` | `AsiBackbone.OpenTelemetry.OpenTelemetryGovernanceBuilderExtensions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.OpenTelemetry` | `AsiBackbone.OpenTelemetry.OpenTelemetryGovernanceEmitter` | Retain: existing domain/host role is clear. |
| `AsiBackbone.OpenTelemetry` | `AsiBackbone.OpenTelemetry.OpenTelemetryGovernanceEmitterOptions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.OpenTelemetry` | `AsiBackbone.OpenTelemetry.OpenTelemetryGovernanceInstrumentation` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.LocalDevelopment` | `AsiBackbone.Signing.LocalDevelopment.LocalDevelopmentSigningBuilderExtensions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.LocalDevelopment` | `AsiBackbone.Signing.LocalDevelopment.LocalDevelopmentSigningOptions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.LocalDevelopment` | `AsiBackbone.Signing.LocalDevelopment.LocalDevelopmentSigningService` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.ManagedKey` | `AsiBackbone.Signing.ManagedKey.IManagedKeySigningClient` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.ManagedKey` | `AsiBackbone.Signing.ManagedKey.ManagedKeySignRequest` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.ManagedKey` | `AsiBackbone.Signing.ManagedKey.ManagedKeySignResult` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.ManagedKey` | `AsiBackbone.Signing.ManagedKey.ManagedKeySigningBuilderExtensions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.ManagedKey` | `AsiBackbone.Signing.ManagedKey.ManagedKeySigningException` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.ManagedKey` | `AsiBackbone.Signing.ManagedKey.ManagedKeySigningOptions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.ManagedKey` | `AsiBackbone.Signing.ManagedKey.ManagedKeySigningService` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Signing.ManagedKey` | `AsiBackbone.Signing.ManagedKey.ManagedKeySigningServiceCollectionExtensions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Storage.InMemory` | `AsiBackbone.Storage.InMemory.Audit.InMemoryAuditLedger` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Storage.InMemory` | `AsiBackbone.Storage.InMemory.Audit.InMemoryAuditResidueLifecycleStore` | `InMemoryDecisionReceiptLifecycleStore` |
| `AsiBackbone.Storage.InMemory` | `AsiBackbone.Storage.InMemory.CapabilityTokens.InMemoryCapabilityGrantUseStore` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Storage.InMemory` | `AsiBackbone.Storage.InMemory.InMemoryStorageBuilderExtensions` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Storage.InMemory` | `AsiBackbone.Storage.InMemory.Outbox.InMemoryGovernanceOutboxStore` | Retain: existing domain/host role is clear. |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestAuditSink` | `GovernanceTestDecisionReceiptSink` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestHarnessEndpointCapabilityGrantValidator` | `GovernanceTestHarnessEndpointCapabilityGrantValidator` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestHarnessOptions` | `GovernanceTestHarnessOptions` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestHarnessPolicyEvaluator` | `GovernanceTestHarnessPolicyEvaluator` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestHarnessServiceCollectionExtensions` | `GovernanceTestHarnessServiceCollectionExtensions` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestSigningService` | `GovernanceTestSigningService` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.AsiBackboneTestingAssemblyMarker` | Retain: product integration/discovery. |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneAuditSinkContract` | `DecisionReceiptSinkContract` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneConstraintContract`1` | `GovernanceConstraintContract` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneContractViolationException` | `GovernanceContractViolationException` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneDecisionContract` | `GovernanceDecisionContract` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneDecisionPolicyContract`1` | `GovernanceDecisionPolicyContract` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackboneEndpointCapabilityGrantValidatorContract` | `EndpointCapabilityGrantValidatorContract` |
| `AsiBackbone.Testing` | `AsiBackbone.Testing.Contracts.AsiBackbonePolicyEvaluatorContract`1` | `GovernancePolicyEvaluatorContract` |

## Compatibility boundary

These managed API changes rename types and update receipt diagnostic wording. The default validation label on GovernanceDecisionContract.VerifyDecisionReceipt (formerly VerifyAuditResidue) is now "Decision receipt". Package IDs, namespaces, registration method names, protocol member names, JSON keys, schema versions, canonical artifact tags, signing payloads, diagnostic IDs/host opt-in markers, and EF table/column names retain their established contracts. In particular, AuditResidueId and its related members (DecisionReceiptBuilder.WithAuditResidueId, FindByAuditResidueIdAsync, and the OpenTelemetry AuditResidueId attribute constant) remain protocol member names because the identifier is persisted and serialized. `OpenTelemetryGovernanceInstrumentation.AuditResidueCreatedEventName` likewise retains the emitted value `asibackbone.audit_residue.created` so existing dashboards, alerts, and queries remain compatible. Helper methods that operate on decision receipts were renamed in 6.0 (see [Helper member renames](upgrade-500-to-600.md#helper-member-renames)); renaming a receipt type does not rewrite stored evidence or signed bytes. Historical release/migration records retain 5.x type names.
