using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.HostIntegration;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Locks the v1 canonical wire names for every enum family that contributes to signed payload bytes.
/// </summary>
public sealed class CanonicalEnumWireCompatibilityTests
{
    private static readonly DateTimeOffset FixtureUtc = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Proves the renamed 7.0 enum member reconstructs the exact canonical bytes and hash emitted by 6.0.
    /// </summary>
    [Fact]
    public async Task GovernanceEmissionEventType500Matches60SignedGoldenVector()
    {
        const string expectedCanonicalJson = /*lang=json,strict*/ "{\"artifactId\":\"envelope-500\",\"artifactType\":\"asibackbone.governance-emission-envelope\",\"canonicalizationVersion\":\"asibackbone.canonical-json.v1\",\"content\":{\"actorId\":null,\"auditResidueId\":\"receipt-500\",\"correlationId\":null,\"createdUtc\":\"2026-06-01T12:00:00.0000000Z\",\"decisionStage\":null,\"emitterProvider\":null,\"emitterStatus\":null,\"envelopeId\":\"envelope-500\",\"eventId\":\"event-500\",\"eventType\":\"AuditResidue\",\"gatewayExecutionId\":null,\"lifecycleStage\":null,\"lifecycleStageSequence\":null,\"metadata\":{},\"occurredUtc\":\"2026-06-01T12:00:00.0000000Z\",\"operationName\":null,\"outboxSequence\":null,\"outcome\":null,\"parentSpanId\":null,\"payload\":null,\"policyHash\":null,\"policyVersion\":null,\"schemaVersion\":\"1.0.0\",\"spanId\":null,\"traceId\":null},\"payloadSchemaVersion\":\"1.0.0\"}";
        const string expectedHash = "50e831a2897e1e18e6596f4576cc5ce10dbedba62b9518493292a6af704e2582";

        GovernanceEmissionEnvelope envelope = CreateEnvelope(
            GovernanceEmissionEventType.DecisionReceipt,
            envelopeId: "envelope-500",
            eventId: "event-500",
            decisionReceiptId: "receipt-500");
        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(envelope);
        CanonicalPayloadHash rebuiltHash = CanonicalPayloadHasher.ComputeHash(payload);

        Assert.Equal(expectedCanonicalJson, payload.CanonicalJson);
        Assert.Equal(expectedHash, rebuiltHash.HashValue);

        var retained60Hash = CanonicalPayloadHash.Create(
            payload.ArtifactType,
            payload.ArtifactId,
            payload.PayloadSchemaVersion,
            payload.CanonicalizationVersion,
            payload.HashAlgorithm,
            expectedHash);
        var signingMetadata = SigningMetadata.Create(
            signingHash: expectedHash,
            hashAlgorithm: payload.HashAlgorithm,
            signature: "retained-6.0-signature",
            signatureAlgorithm: "TEST-V1",
            keyId: "key-1",
            provider: "6.0-fixture",
            signedUtc: FixtureUtc,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["artifact_id"] = payload.ArtifactId,
                ["artifact_type"] = payload.ArtifactType,
                ["canonicalization_version"] = payload.CanonicalizationVersion,
                ["payload_schema_version"] = payload.PayloadSchemaVersion
            });
        SignedGovernanceArtifact<GovernanceEmissionEnvelope> retained60Artifact = SignedGovernanceArtifacts.Rehydrate(
            envelope,
            payload,
            retained60Hash,
            signingMetadata);
        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyTypedAsync(
            retained60Artifact,
            verifier,
            value => CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(value),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.ShouldAllow);
        Assert.True(verifier.WasCalled);
    }

    /// <summary>
    /// Locks every governance emission event type to its explicit v1 wire name.
    /// </summary>
    [Theory]
    [InlineData(GovernanceEmissionEventType.Decision, "Decision")]
    [InlineData(GovernanceEmissionEventType.Acknowledgment, "Acknowledgment")]
    [InlineData(GovernanceEmissionEventType.CapabilityToken, "CapabilityToken")]
    [InlineData(GovernanceEmissionEventType.Gateway, "Gateway")]
    [InlineData(GovernanceEmissionEventType.DecisionReceipt, "AuditResidue")]
    [InlineData(GovernanceEmissionEventType.AuditLifecycle, "AuditLifecycle")]
    [InlineData(GovernanceEmissionEventType.Outbox, "Outbox")]
    [InlineData(GovernanceEmissionEventType.ProviderEmission, "ProviderEmission")]
    public void GovernanceEmissionEventTypesUseStableV1WireNames(
        GovernanceEmissionEventType value,
        string expectedWireName)
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(CreateEnvelope(value));

        Assert.Equal(expectedWireName, ReadContentString(payload, "eventType"));
    }

    /// <summary>
    /// Locks every lifecycle stage to its explicit v1 wire name in both signed payload shapes that carry it.
    /// </summary>
    [Theory]
    [InlineData(DecisionReceiptLifecycleStage.DecisionEvaluated, "DecisionEvaluated")]
    [InlineData(DecisionReceiptLifecycleStage.AcknowledgmentRequested, "AcknowledgmentRequested")]
    [InlineData(DecisionReceiptLifecycleStage.AcknowledgmentCompleted, "AcknowledgmentCompleted")]
    [InlineData(DecisionReceiptLifecycleStage.CapabilityTokenIssued, "CapabilityTokenIssued")]
    [InlineData(DecisionReceiptLifecycleStage.GatewayExecutionStarted, "GatewayExecutionStarted")]
    [InlineData(DecisionReceiptLifecycleStage.GatewayExecutionCompleted, "GatewayExecutionCompleted")]
    [InlineData(DecisionReceiptLifecycleStage.GatewayExecutionDenied, "GatewayExecutionDenied")]
    [InlineData(DecisionReceiptLifecycleStage.ExternalEmissionQueued, "ExternalEmissionQueued")]
    [InlineData(DecisionReceiptLifecycleStage.ExternalEmissionDelivered, "ExternalEmissionDelivered")]
    [InlineData(DecisionReceiptLifecycleStage.ExternalEmissionFailed, "ExternalEmissionFailed")]
    [InlineData(DecisionReceiptLifecycleStage.ExternalEmissionDeadLettered, "ExternalEmissionDeadLettered")]
    public void LifecycleStagesUseStableV1WireNames(
        DecisionReceiptLifecycleStage value,
        string expectedWireName)
    {
        var lifecycleEvent = DecisionReceiptLifecycleEvent.Create(
            value,
            "correlation-1",
            eventId: "lifecycle-1",
            occurredUtc: FixtureUtc);
        CanonicalPayload lifecyclePayload = CanonicalPayloadBuilder.ForDecisionReceiptLifecycleEvent(lifecycleEvent);
        CanonicalPayload envelopePayload = CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(
            CreateEnvelope(GovernanceEmissionEventType.AuditLifecycle, lifecycleStage: value));
        CanonicalPayload derivedEnvelopePayload = CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(
            GovernanceEmissionEnvelope.FromLifecycleEvent(lifecycleEvent));

        Assert.Equal(expectedWireName, ReadContentString(lifecyclePayload, "stage"));
        Assert.Equal(expectedWireName, ReadContentString(envelopePayload, "lifecycleStage"));
        Assert.Equal(expectedWireName, ReadContentString(derivedEnvelopePayload, "decisionStage"));
    }

    /// <summary>
    /// Locks governance decision outcomes created through every typed receipt path to explicit v1 wire names.
    /// </summary>
    [Theory]
    [InlineData(GovernanceDecisionOutcome.Allowed, "Allowed")]
    [InlineData(GovernanceDecisionOutcome.Warning, "Warning")]
    [InlineData(GovernanceDecisionOutcome.Denied, "Denied")]
    [InlineData(GovernanceDecisionOutcome.Deferred, "Deferred")]
    [InlineData(GovernanceDecisionOutcome.AcknowledgmentRequired, "AcknowledgmentRequired")]
    [InlineData(GovernanceDecisionOutcome.EscalationRecommended, "EscalationRecommended")]
    public void GovernanceDecisionOutcomesUseStableV1WireNames(
        GovernanceDecisionOutcome value,
        string expectedWireName)
    {
        GovernanceDecision decision = CreateDecision(value);
        var directReceipt = DecisionReceipt.FromDecision(
            GovernanceActorContext.System,
            "orders.approve",
            decision,
            eventId: "event-decision-direct");
        DecisionReceipt builderReceipt = DecisionReceiptBuilder.FromDecision(
                GovernanceActorContext.System,
                "orders.approve",
                decision)
            .WithEventId("event-decision-builder")
            .Build();

        Assert.Equal(expectedWireName, ReadContentString(
            CanonicalPayloadBuilder.ForDecisionReceipt(directReceipt),
            "outcome"));
        Assert.Equal(expectedWireName, ReadContentString(
            CanonicalPayloadBuilder.ForDecisionReceipt(builderReceipt),
            "outcome"));
    }

    /// <summary>
    /// Locks constraint outcomes created through every typed receipt path to explicit v1 wire names.
    /// </summary>
    [Theory]
    [InlineData(ConstraintEvaluationOutcome.NotApplicable, "NotApplicable")]
    [InlineData(ConstraintEvaluationOutcome.Allowed, "Allowed")]
    [InlineData(ConstraintEvaluationOutcome.Warning, "Warning")]
    [InlineData(ConstraintEvaluationOutcome.Denied, "Denied")]
    public void ConstraintOutcomesUseStableV1WireNames(
        ConstraintEvaluationOutcome value,
        string expectedWireName)
    {
        ConstraintEvaluationResult constraintResult = CreateConstraintResult(value);
        var directReceipt = DecisionReceipt.FromConstraint(
            GovernanceActorContext.System,
            "orders.approve",
            constraintResult,
            eventId: "event-constraint-direct");
        DecisionReceipt builderReceipt = DecisionReceiptBuilder.FromConstraint(
                GovernanceActorContext.System,
                "orders.approve",
                constraintResult)
            .WithEventId("event-constraint-builder")
            .Build();

        Assert.Equal(expectedWireName, ReadContentString(
            CanonicalPayloadBuilder.ForDecisionReceipt(directReceipt),
            "outcome"));
        Assert.Equal(expectedWireName, ReadContentString(
            CanonicalPayloadBuilder.ForDecisionReceipt(builderReceipt),
            "outcome"));
    }

    /// <summary>
    /// Locks every governance actor type to its explicit v1 wire name.
    /// </summary>
    [Theory]
    [InlineData(GovernanceActorType.Unknown, "Unknown")]
    [InlineData(GovernanceActorType.Human, "Human")]
    [InlineData(GovernanceActorType.System, "System")]
    [InlineData(GovernanceActorType.Service, "Service")]
    [InlineData(GovernanceActorType.Agent, "Agent")]
    public void GovernanceActorTypesUseStableV1WireNames(GovernanceActorType value, string expectedWireName)
    {
        var receipt = DecisionReceipt.Create(
            CreateActor(value),
            "orders.approve",
            "Allowed",
            eventId: "event-actor",
            occurredUtc: FixtureUtc);
        CanonicalPayload payload = CanonicalPayloadBuilder.ForDecisionReceipt(receipt);

        Assert.Equal(expectedWireName, ReadContentString(payload, "actorType"));
    }

    /// <summary>
    /// Locks every emission status used by signed outbox entries to its explicit v1 wire name.
    /// </summary>
    [Theory]
    [InlineData(GovernanceEmissionStatus.Pending, "Pending")]
    [InlineData(GovernanceEmissionStatus.Delivered, "Delivered")]
    [InlineData(GovernanceEmissionStatus.Deferred, "Deferred")]
    [InlineData(GovernanceEmissionStatus.Failed, "Failed")]
    [InlineData(GovernanceEmissionStatus.RetryableFailure, "RetryableFailure")]
    [InlineData(GovernanceEmissionStatus.DeadLettered, "DeadLettered")]
    public void GovernanceEmissionStatusesUseStableV1WireNames(
        GovernanceEmissionStatus value,
        string expectedWireName)
    {
        var entry = GovernanceOutboxEntry.Restore(
            CreateEnvelope(GovernanceEmissionEventType.Outbox),
            value,
            "outbox-1",
            FixtureUtc,
            FixtureUtc);
        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry);

        Assert.Equal(expectedWireName, ReadContentString(payload, "status"));
    }

    /// <summary>
    /// Locks every governed-operation persistence outcome to its explicit v1 wire name.
    /// </summary>
    [Theory]
    [InlineData(GovernedOperationPersistenceOutcome.Committed, "Committed")]
    [InlineData(GovernedOperationPersistenceOutcome.Failed, "Failed")]
    [InlineData(GovernedOperationPersistenceOutcome.RolledBack, "RolledBack")]
    [InlineData(GovernedOperationPersistenceOutcome.CompletedWithoutMutation, "CompletedWithoutMutation")]
    public void PersistenceOutcomesUseStableV1WireNames(
        GovernedOperationPersistenceOutcome value,
        string expectedWireName)
    {
        bool committed = value is GovernedOperationPersistenceOutcome.Committed;
        var receipt = GovernedOperationExecutionReceipt.Create(
            "operation-1",
            value,
            mutationBatchId: committed ? "batch-1" : null,
            mutationRecordCount: committed ? 1 : 0,
            mutationManifestHash: committed ? "abcdef" : null,
            mutationManifestAlgorithm: committed ? "SHA-256" : null,
            completedUtc: FixtureUtc);
        CanonicalPayload payload = GovernedOperationExecutionReceiptCanonicalPayload.Create(receipt);
        IReadOnlyDictionary<string, string> metadata = receipt.ToLifecycleMetadata();
        var decisionReceipt = DecisionReceipt.Create(
            GovernanceActorContext.System,
            "orders.approve",
            "Allowed",
            eventId: "event-persistence",
            occurredUtc: FixtureUtc,
            correlationId: "correlation-persistence");
        DecisionReceiptLifecycleEvent lifecycleEvent = HostAccountabilityLifecycleEvent.FromExecutionReceipt(
            decisionReceipt,
            receipt,
            eventId: "lifecycle-persistence");
        CanonicalPayload lifecyclePayload = CanonicalPayloadBuilder.ForDecisionReceiptLifecycleEvent(lifecycleEvent);

        Assert.Equal(expectedWireName, ReadContentString(payload, "persistenceOutcome"));
        Assert.Equal(expectedWireName, metadata[HostAccountabilityMetadataKeys.PersistenceOutcome]);
        Assert.Equal(expectedWireName, ReadContentString(lifecyclePayload, "outcome"));
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(
        GovernanceEmissionEventType eventType,
        string envelopeId = "envelope-1",
        string eventId = "event-1",
        string? decisionReceiptId = null,
        DecisionReceiptLifecycleStage? lifecycleStage = null)
    {
        return GovernanceEmissionEnvelope.Create(
            eventType,
            eventId: eventId,
            occurredUtc: FixtureUtc,
            envelopeId: envelopeId,
            createdUtc: FixtureUtc,
            decisionReceiptId: decisionReceiptId,
            lifecycleStage: lifecycleStage);
    }

    private static GovernanceActorContext CreateActor(GovernanceActorType value)
    {
        return value switch
        {
            GovernanceActorType.Unknown => GovernanceActorContext.Unknown,
            GovernanceActorType.Human => GovernanceActorContext.Human("human-1"),
            GovernanceActorType.System => GovernanceActorContext.System,
            GovernanceActorType.Service => GovernanceActorContext.Service("service-1"),
            GovernanceActorType.Agent => GovernanceActorContext.Agent("agent-1"),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Test actor type must be defined.")
        };
    }

    private static GovernanceDecision CreateDecision(GovernanceDecisionOutcome value)
    {
        return value switch
        {
            GovernanceDecisionOutcome.Allowed => GovernanceDecision.Allow(),
            GovernanceDecisionOutcome.Warning => GovernanceDecision.Warning("wire.warning", "Wire-name fixture."),
            GovernanceDecisionOutcome.Denied => GovernanceDecision.Deny("wire.denied", "Wire-name fixture."),
            GovernanceDecisionOutcome.Deferred => GovernanceDecision.Defer("wire.deferred", "Wire-name fixture."),
            GovernanceDecisionOutcome.AcknowledgmentRequired => GovernanceDecision.RequireAcknowledgment(
                "wire.acknowledgment-required",
                "Wire-name fixture."),
            GovernanceDecisionOutcome.EscalationRecommended => GovernanceDecision.Escalate(
                "wire.escalation-recommended",
                "Wire-name fixture."),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Test decision outcome must be defined.")
        };
    }

    private static ConstraintEvaluationResult CreateConstraintResult(ConstraintEvaluationOutcome value)
    {
        return value switch
        {
            ConstraintEvaluationOutcome.NotApplicable => ConstraintEvaluationResult.NotApplicable(),
            ConstraintEvaluationOutcome.Allowed => ConstraintEvaluationResult.Allow(),
            ConstraintEvaluationOutcome.Warning => ConstraintEvaluationResult.Warning(
                "wire.warning",
                "Wire-name fixture."),
            ConstraintEvaluationOutcome.Denied => ConstraintEvaluationResult.Deny(
                "wire.denied",
                "Wire-name fixture."),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Test constraint outcome must be defined.")
        };
    }

    private static string? ReadContentString(CanonicalPayload payload, string propertyName)
    {
        using var document = JsonDocument.Parse(payload.CanonicalJson);
        return document.RootElement.GetProperty("content").GetProperty(propertyName).GetString();
    }

    private sealed class AlwaysValidVerificationService : IGovernanceSignatureVerificationService
    {
        public bool WasCalled { get; private set; }

        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;
            return ValueTask.FromResult(SignatureVerificationResult.Verified());
        }
    }
}
