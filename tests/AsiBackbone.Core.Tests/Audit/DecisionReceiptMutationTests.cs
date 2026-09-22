using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Mutation-focused tests for decision receipt behavior.
/// </summary>
public sealed class DecisionReceiptMutationTests
{
    /// <summary>
    /// Tests that the <see cref="DecisionReceipt.FromDecision"/> method correctly copies decision trace and policy metadata, and ensures that the metadata dictionary is not aliased (i.e., changes to the original dictionary after creation do not affect the residue's metadata).
    /// </summary>
    [Fact]
    public void FromDecisionCopiesDecisionTracePolicyMetadataAndDoesNotAliasMetadata()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Human(" user-123 ", " Chris ");
        var decision = GovernanceDecision.RequireAcknowledgment(
            "decision.ack.required",
            "Acknowledgment is required.",
            correlationId: " corr-123 ",
            traceId: " trace-456 ",
            policyVersion: " v1 ",
            policyHash: " hash-abc ");
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [" region "] = " us-la ",
            ["risk"] = " high "
        };

        var residue = DecisionReceipt.FromDecision(
            actor,
            " document.approve ",
            decision,
            eventId: " event-123 ",
            occurredUtc: new DateTimeOffset(2026, 6, 13, 7, 30, 0, TimeSpan.FromHours(-5)),
            metadata: metadata);

        metadata[" region "] = " changed ";
        metadata["added"] = " later ";

        Assert.Equal("event-123", residue.EventId);
        Assert.Equal(new DateTimeOffset(2026, 6, 13, 12, 30, 0, TimeSpan.Zero), residue.OccurredUtc);
        Assert.Equal("user-123", residue.ActorId);
        Assert.Equal(GovernanceActorType.Human, residue.ActorType);
        Assert.Equal("Chris", residue.ActorDisplayName);
        Assert.Equal("document.approve", residue.OperationName);
        Assert.Equal(nameof(GovernanceDecisionOutcome.AcknowledgmentRequired), residue.Outcome);
        Assert.Equal("decision.ack.required", Assert.Single(residue.ReasonCodes));
        Assert.Equal("corr-123", residue.CorrelationId);
        Assert.Equal("trace-456", residue.TraceId);
        Assert.Equal("v1", residue.PolicyVersion);
        Assert.Equal("hash-abc", residue.PolicyHash);
        Assert.Equal("us-la", residue.Metadata["region"]);
        Assert.Equal("high", residue.Metadata["risk"]);
        Assert.False(residue.Metadata.ContainsKey("added"));
    }

    /// <summary>
    /// Tests that the <see cref="DecisionReceipt.FromConstraint"/> method correctly copies constraint evaluation trace and policy metadata, ensuring that the metadata dictionary is not aliased (i.e., changes to the original dictionary after creation do not affect the residue's metadata).
    /// </summary>
    [Fact]
    public void FromConstraintCopiesFullTracePolicyDataAndMetadata()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service(" service-123 ");
        var constraintResult = ConstraintEvaluationResult.Deny(
            "constraint.denied",
            "Constraint denied the operation.");

        var residue = DecisionReceipt.FromConstraint(
            actor,
            " external.call ",
            constraintResult,
            eventId: " constraint-event-123 ",
            occurredUtc: new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero),
            correlationId: " corr-constraint ",
            traceId: " trace-constraint ",
            policyVersion: " v-constraint ",
            policyHash: " hash-constraint ",
            metadata: new Dictionary<string, string>
            {
                [" source "] = " constraint-test "
            });

        Assert.Equal("constraint-event-123", residue.EventId);
        Assert.Equal("service-123", residue.ActorId);
        Assert.Equal("external.call", residue.OperationName);
        Assert.Equal(nameof(ConstraintEvaluationOutcome.Denied), residue.Outcome);
        Assert.Equal("constraint.denied", Assert.Single(residue.ReasonCodes));
        Assert.Equal("corr-constraint", residue.CorrelationId);
        Assert.Equal("trace-constraint", residue.TraceId);
        Assert.Equal("v-constraint", residue.PolicyVersion);
        Assert.Equal("hash-constraint", residue.PolicyHash);
        Assert.True(residue.HasMetadata);
        Assert.Equal("constraint-test", residue.Metadata["source"]);
    }

    /// <summary>
    /// Tests that the <see cref="DecisionReceipt.Create"/> method generates identifiers and initializes empty collections when optional inputs are missing, ensuring that the residue is still valid and usable even with minimal input.
    /// </summary>
    [Fact]
    public void CreateUsesGeneratedIdentifiersAndEmptyCollectionsWhenOptionalInputsAreMissing()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service(" service-123 ");

        var residue = DecisionReceipt.Create(actor, "gateway.execute", "Allowed");

        Assert.False(string.IsNullOrWhiteSpace(residue.EventId));
        Assert.Equal(residue.EventId, residue.DecisionReceiptId);
        Assert.False(residue.HasReasonCodes);
        Assert.False(residue.HasMetadata);
        Assert.Empty(residue.ReasonCodes);
        Assert.Empty(residue.Metadata);
        Assert.Null(residue.ActorDisplayName);
        Assert.Null(residue.CorrelationId);
        Assert.Null(residue.TraceId);
        Assert.Null(residue.PolicyVersion);
        Assert.Null(residue.PolicyHash);
    }

    /// <summary>
    /// Tests that the <see cref="DecisionReceipt.Create"/> method normalizes reason codes, telemetry identifiers, and metadata keys/values by trimming whitespace and handling nulls appropriately, ensuring that the resulting residue contains clean and consistent data.
    /// </summary>
    [Fact]
    public void CreateNormalizesReasonCodesTelemetryAndMetadataBranches()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Human(" user-123 ", " Chris ");
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [" source "] = " branch-test ",
            [" "] = " ignored ",
            ["nullable"] = null!
        };

        var residue = DecisionReceipt.Create(
            actor,
            " gateway.execute ",
            " Allowed ",
            reasonCodes: [" reason.beta ", " ", "reason.alpha"],
            eventId: " event-123 ",
            occurredUtc: new DateTimeOffset(2026, 6, 13, 7, 30, 0, TimeSpan.FromHours(-5)),
            correlationId: " ",
            traceId: " trace-123 ",
            policyVersion: " policy-v1 ",
            policyHash: " policy-hash ",
            metadata: metadata,
            decisionReceiptId: " residue-123 ",
            spanId: " span-123 ",
            parentSpanId: " parent-span-123 ",
            decisionLatencyMs: 0,
            constraintSetHash: " constraint-hash ",
            constraintCount: 0,
            riskScore: 0,
            policyScope: " regional ",
            tenantHash: " tenant-hash ",
            organizationHash: " organization-hash ",
            emitterStatus: " queued ",
            emitterProvider: " provider ",
            outboxSequence: 0,
            gatewayExecutionId: " gateway-123 ",
            decisionStage: " evaluated ");

        Assert.Equal("event-123", residue.EventId);
        Assert.Equal("residue-123", residue.DecisionReceiptId);
        Assert.Equal("gateway.execute", residue.OperationName);
        Assert.Equal("Allowed", residue.Outcome);
        Assert.True(residue.HasReasonCodes);
        Assert.Collection(
            residue.ReasonCodes,
            reasonCode => Assert.Equal("reason.beta", reasonCode),
            reasonCode => Assert.Equal("reason.alpha", reasonCode));
        Assert.Null(residue.CorrelationId);
        Assert.Equal("trace-123", residue.TraceId);
        Assert.Equal("span-123", residue.SpanId);
        Assert.Equal("parent-span-123", residue.ParentSpanId);
        Assert.Equal(0, residue.DecisionLatencyMs);
        Assert.Equal("constraint-hash", residue.ConstraintSetHash);
        Assert.Equal(0, residue.ConstraintCount);
        Assert.Equal(0, residue.RiskScore);
        Assert.Equal("regional", residue.PolicyScope);
        Assert.Equal("tenant-hash", residue.TenantHash);
        Assert.Equal("organization-hash", residue.OrganizationHash);
        Assert.Equal("queued", residue.EmitterStatus);
        Assert.Equal("provider", residue.EmitterProvider);
        Assert.Equal(0, residue.OutboxSequence);
        Assert.Equal("gateway-123", residue.GatewayExecutionId);
        Assert.Equal("evaluated", residue.DecisionStage);
        Assert.Equal("branch-test", residue.Metadata["source"]);
        Assert.Equal(string.Empty, residue.Metadata["nullable"]);
        Assert.False(residue.Metadata.ContainsKey(string.Empty));
    }

    /// <summary>
    /// Tests that the <see cref="DecisionReceipt.Create"/> method throws an <see cref="ArgumentOutOfRangeException"/> when a negative decision latency is provided, ensuring that invalid input is properly rejected.
    /// </summary>
    [Fact]
    public void CreateRejectsNegativeDecisionLatency()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("service-123");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecisionReceipt.Create(
                actor,
                "gateway.execute",
                "Allowed",
                decisionLatencyMs: -1));
    }

    /// <summary>
    /// Tests that the <see cref="DecisionReceipt.Create"/> method throws an <see cref="ArgumentOutOfRangeException"/> when a negative constraint count is provided, ensuring that invalid input is properly rejected.
    /// </summary>
    [Fact]
    public void CreateRejectsNegativeConstraintCount()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("service-123");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecisionReceipt.Create(
                actor,
                "gateway.execute",
                "Allowed",
                constraintCount: -1));
    }

    /// <summary>
    /// Tests that the <see cref="DecisionReceipt.Create"/> method throws an <see cref="ArgumentOutOfRangeException"/> when a negative outbox sequence is provided, ensuring that invalid input is properly rejected.
    /// </summary>
    [Fact]
    public void CreateRejectsNegativeOutboxSequence()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("service-123");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecisionReceipt.Create(
                actor,
                "gateway.execute",
                "Allowed",
                outboxSequence: -1));
    }

    /// <summary>
    /// Tests that the <see cref="DecisionReceipt.Create"/> method throws an <see cref="ArgumentOutOfRangeException"/> when an invalid risk score is provided (negative, NaN, or positive infinity), ensuring that invalid input is properly rejected.
    /// </summary>
    /// <param name="riskScore">The risk score to test.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void CreateRejectsInvalidRiskScore(double riskScore)
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("service-123");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecisionReceipt.Create(
                actor,
                "gateway.execute",
                "Allowed",
                riskScore: riskScore));
    }
}
