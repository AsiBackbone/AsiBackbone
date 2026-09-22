using System.Reflection;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Unit tests for the fluent <see cref="DecisionReceiptBuilder" /> construction path.
/// </summary>
public sealed class DecisionReceiptBuilderTests
{
    /// <summary>
    /// Verifies that the fluent builder produces an <see cref="DecisionReceipt" /> equivalent to the direct creation method.
    /// </summary>
    [Fact]
    public void BuilderCreateMatchesDirectCreateSemantics()
    {
        var actor = GovernanceActorContext.Human(" user-123 ", " Chris ");
        DateTimeOffset occurredUtc = new(2026, 6, 26, 10, 30, 0, TimeSpan.FromHours(-5));
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [" region "] = " us-la ",
            ["risk"] = " high "
        };

        var direct = DecisionReceipt.Create(
            actor,
            " documents.approve ",
            " Warning ",
            reasonCodes: [" policy.warning ", " latency.high "],
            eventId: " event-123 ",
            occurredUtc: occurredUtc,
            correlationId: " correlation-123 ",
            traceId: " trace-456 ",
            policyVersion: " v1 ",
            policyHash: " hash-abc ",
            metadata: metadata,
            decisionReceiptId: " residue-123 ",
            spanId: " span-789 ",
            parentSpanId: " parent-456 ",
            decisionLatencyMs: 42,
            constraintSetHash: " constraint-hash ",
            constraintCount: 3,
            riskScore: 0.75,
            policyScope: " payments ",
            tenantHash: " tenant-hash ",
            organizationHash: " org-hash ",
            emitterStatus: " queued ",
            emitterProvider: " open-telemetry ",
            outboxSequence: 12,
            gatewayExecutionId: " gateway-123 ",
            decisionStage: " DecisionEvaluated ",
            schemaVersion: " asi.audit.v9 ");

        DecisionReceipt built = DecisionReceiptBuilder.Create(actor, " documents.approve ", " Warning ")
            .AddReasonCode(" policy.warning ")
            .AddReasonCode(" latency.high ")
            .WithEventId(" event-123 ")
            .WithOccurredUtc(occurredUtc)
            .WithCorrelationId(" correlation-123 ")
            .WithTraceId(" trace-456 ")
            .WithPolicyVersion(" v1 ")
            .WithPolicyHash(" hash-abc ")
            .WithMetadata(metadata)
            .WithDecisionReceiptId(" residue-123 ")
            .WithSpanId(" span-789 ")
            .WithParentSpanId(" parent-456 ")
            .WithDecisionLatencyMs(42)
            .WithConstraintSetHash(" constraint-hash ")
            .WithConstraintCount(3)
            .WithRiskScore(0.75)
            .WithPolicyScope(" payments ")
            .WithTenantHash(" tenant-hash ")
            .WithOrganizationHash(" org-hash ")
            .WithEmitterStatus(" queued ")
            .WithEmitterProvider(" open-telemetry ")
            .WithOutboxSequence(12)
            .WithGatewayExecutionId(" gateway-123 ")
            .WithDecisionStage(" DecisionEvaluated ")
            .WithSchemaVersion(" asi.audit.v9 ")
            .Build();

        AssertEquivalentResidue(direct, built);
    }

    /// <summary>
    /// Verifies that the fluent builder produces an <see cref="DecisionReceipt" /> equivalent to the direct creation method when starting from a <see cref="GovernanceDecision" />.
    /// </summary>
    [Fact]
    public void BuilderFromDecisionMatchesDirectFromDecisionSemantics()
    {
        var actor = GovernanceActorContext.Service("service-123");
        var decision = GovernanceDecision.Deny(
            "policy.denied",
            "Policy denied the operation.",
            correlationId: "correlation-123",
            traceId: "trace-456",
            policyVersion: "v1",
            policyHash: "hash-abc");
        DateTimeOffset occurredUtc = new(2026, 6, 26, 15, 30, 0, TimeSpan.Zero);
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["source"] = "unit-test"
        };

        var direct = DecisionReceipt.FromDecision(
            actor,
            "system.sync",
            decision,
            eventId: "event-123",
            occurredUtc: occurredUtc,
            metadata: metadata,
            decisionReceiptId: "residue-123",
            spanId: "span-789",
            parentSpanId: "parent-456",
            decisionLatencyMs: 42,
            constraintSetHash: "constraint-hash",
            constraintCount: 3,
            riskScore: 0.75,
            policyScope: "policy-scope",
            tenantHash: "tenant-hash",
            organizationHash: "org-hash",
            emitterStatus: "queued",
            emitterProvider: "open-telemetry",
            outboxSequence: 12,
            gatewayExecutionId: "gateway-123",
            decisionStage: "DecisionEvaluated",
            schemaVersion: "asi.audit.v9");

        DecisionReceipt built = DecisionReceiptBuilder.FromDecision(actor, "system.sync", decision)
            .WithEventId("event-123")
            .WithOccurredUtc(occurredUtc)
            .WithMetadata(metadata)
            .WithDecisionReceiptId("residue-123")
            .WithSpanId("span-789")
            .WithParentSpanId("parent-456")
            .WithDecisionLatencyMs(42)
            .WithConstraintSetHash("constraint-hash")
            .WithConstraintCount(3)
            .WithRiskScore(0.75)
            .WithPolicyScope("policy-scope")
            .WithTenantHash("tenant-hash")
            .WithOrganizationHash("org-hash")
            .WithEmitterStatus("queued")
            .WithEmitterProvider("open-telemetry")
            .WithOutboxSequence(12)
            .WithGatewayExecutionId("gateway-123")
            .WithDecisionStage("DecisionEvaluated")
            .WithSchemaVersion("asi.audit.v9")
            .Build();

        AssertEquivalentResidue(direct, built);
    }

    /// <summary>
    /// Verifies that the fluent builder produces an <see cref="DecisionReceipt" /> equivalent to the direct creation method when starting from a <see cref="ConstraintEvaluationResult" />.
    /// </summary>
    [Fact]
    public void BuilderFromConstraintMatchesDirectFromConstraintSemantics()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        var constraintResult = ConstraintEvaluationResult.Warning(
            "constraint.warning",
            "Constraint produced a warning.");
        DateTimeOffset occurredUtc = new(2026, 6, 26, 15, 30, 0, TimeSpan.Zero);

        var direct = DecisionReceipt.FromConstraint(
            actor,
            "system.sync",
            constraintResult,
            eventId: "event-123",
            occurredUtc: occurredUtc,
            correlationId: "correlation-123",
            traceId: "trace-456",
            policyVersion: "v1",
            policyHash: "hash-abc",
            metadata: new Dictionary<string, string>
            {
                ["source"] = "unit-test"
            });

        DecisionReceipt built = DecisionReceiptBuilder.FromConstraint(actor, "system.sync", constraintResult)
            .WithEventId("event-123")
            .WithOccurredUtc(occurredUtc)
            .WithCorrelationId("correlation-123")
            .WithTraceId("trace-456")
            .WithPolicyVersion("v1")
            .WithPolicyHash("hash-abc")
            .AddMetadata("source", "unit-test")
            .Build();

        AssertEquivalentResidue(direct, built);
    }

    /// <summary>
    /// Verifies that the fluent builder does not allocate metadata storage when no metadata is added, and that the built <see cref="DecisionReceipt" /> has no metadata.
    /// </summary>
    [Fact]
    public void BuilderDoesNotAllocateMetadataStorageWhenNoMetadataIsAdded()
    {
        var builder = DecisionReceiptBuilder.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed");

        Assert.Null(GetMetadataStorage(builder));

        DecisionReceipt residue = builder.Build();

        Assert.False(residue.HasMetadata);
        Assert.Empty(residue.Metadata);
        Assert.Null(GetMetadataStorage(builder));
    }

    /// <summary>
    /// Verifies that the fluent builder allocates metadata storage when metadata is added, and that the built <see cref="DecisionReceipt" /> has the expected metadata.
    /// </summary>
    [Fact]
    public void AddMetadataInitializesStorageAndBuildsSingleEntry()
    {
        var builder = DecisionReceiptBuilder.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed");

        DecisionReceipt residue = builder
            .AddMetadata("source", "unit-test")
            .Build();

        Dictionary<string, string>? storage = GetMetadataStorage(builder);
        Assert.NotNull(storage);
        _ = Assert.Single(storage);
        Assert.Equal("unit-test", residue.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that the fluent builder allocates metadata storage when metadata is added, and that the built <see cref="DecisionReceipt" /> has the expected metadata.
    /// </summary>
    [Fact]
    public void WithMetadataInitializesStorageAndBuildsMultipleEntries()
    {
        var builder = DecisionReceiptBuilder.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed");
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["source"] = "unit-test",
            ["region"] = "us-la"
        };

        DecisionReceipt residue = builder
            .WithMetadata(metadata)
            .Build();

        Dictionary<string, string>? storage = GetMetadataStorage(builder);
        Assert.NotNull(storage);
        Assert.Equal(2, storage.Count);
        Assert.Equal("unit-test", residue.Metadata["source"]);
        Assert.Equal("us-la", residue.Metadata["region"]);
    }

    /// <summary>
    /// Verifies that the fluent builder overwrites existing metadata entries when the same key is added again, and that the built <see cref="DecisionReceipt" /> has the updated value.
    /// </summary>
    [Fact]
    public void AddMetadataOverwritesExistingEntry()
    {
        DecisionReceipt residue = DecisionReceiptBuilder.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed")
            .AddMetadata("source", "original")
            .AddMetadata("source", "updated")
            .Build();

        _ = Assert.Single(residue.Metadata);
        Assert.Equal("updated", residue.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that the fluent builder clears existing metadata storage when null or empty metadata is provided, and that the built <see cref="DecisionReceipt" /> has no metadata.
    /// </summary>
    [Fact]
    public void WithMetadataClearsStorageForNullAndEmptyInputs()
    {
        DecisionReceiptBuilder builder = DecisionReceiptBuilder.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed")
            .AddMetadata("source", "original");

        Assert.NotNull(GetMetadataStorage(builder));

        DecisionReceipt nullMetadataResidue = builder
            .WithMetadata(null)
            .Build();

        Assert.Null(GetMetadataStorage(builder));
        Assert.Empty(nullMetadataResidue.Metadata);

        DecisionReceipt emptyMetadataResidue = builder
            .AddMetadata("source", "replacement")
            .WithMetadata(new Dictionary<string, string>(StringComparer.Ordinal))
            .Build();

        Assert.Null(GetMetadataStorage(builder));
        Assert.Empty(emptyMetadataResidue.Metadata);
    }

    /// <summary>
    /// Verifies that the built <see cref="DecisionReceipt" /> has immutable metadata, and that attempts to modify the metadata dictionary throw a <see cref="NotSupportedException" />.
    /// </summary>
    [Fact]
    public void BuiltResidueMetadataIsImmutable()
    {
        Dictionary<string, string> sourceMetadata = new(StringComparer.Ordinal)
        {
            ["source"] = "original"
        };

        DecisionReceipt residue = DecisionReceiptBuilder.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed")
            .WithMetadata(sourceMetadata)
            .Build();

        sourceMetadata["source"] = "mutated";

        Assert.Equal("original", residue.Metadata["source"]);
        IDictionary<string, string> mutableMetadata = Assert.IsAssignableFrom<IDictionary<string, string>>(residue.Metadata);
        _ = Assert.Throws<NotSupportedException>(() => mutableMetadata["source"] = "mutated");
    }

    /// <summary>
    /// Verifies that the built <see cref="DecisionReceipt" /> does not change when the builder is reused to build another residue, ensuring that each build produces a separate instance with its own state.
    /// </summary>
    [Fact]
    public void BuiltResidueDoesNotChangeWhenBuilderIsReused()
    {
        DecisionReceiptBuilder builder = DecisionReceiptBuilder.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Warning")
            .AddReasonCode("policy.warning")
            .AddMetadata("source", "original");

        DecisionReceipt first = builder.Build();
        DecisionReceipt second = builder
            .AddReasonCode("policy.added")
            .AddMetadata("source", "mutated")
            .Build();

        Assert.Equal("policy.warning", Assert.Single(first.ReasonCodes));
        Assert.Equal("original", first.Metadata["source"]);
        Assert.Equal(["policy.warning", "policy.added"], second.ReasonCodes);
        Assert.Equal("mutated", second.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that the fluent builder throws an <see cref="ArgumentNullException" /> when attempting to create an <see cref="DecisionReceipt" /> from a null <see cref="GovernanceDecision" />.
    /// </summary>
    [Fact]
    public void BuilderThrowsForMissingDecision()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            DecisionReceiptBuilder.FromDecision(
                GovernanceActorContext.System,
                "system.sync",
                decision: null!));
    }

    /// <summary>
    /// Verifies that the fluent builder throws an <see cref="ArgumentNullException" /> when attempting to create an <see cref="DecisionReceipt" /> from a null <see cref="ConstraintEvaluationResult" />.
    /// </summary>
    [Fact]
    public void BuilderThrowsForMissingConstraintResult()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            DecisionReceiptBuilder.FromConstraint(
                GovernanceActorContext.System,
                "system.sync",
                constraintResult: null!));
    }

    private static Dictionary<string, string>? GetMetadataStorage(DecisionReceiptBuilder builder)
    {
        FieldInfo? field = typeof(DecisionReceiptBuilder).GetField("metadata", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return (Dictionary<string, string>?)field.GetValue(builder);
    }

    private static void AssertEquivalentResidue(DecisionReceipt expected, DecisionReceipt actual)
    {
        Assert.Equal(expected.EventId, actual.EventId);
        Assert.Equal(expected.DecisionReceiptId, actual.DecisionReceiptId);
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.OccurredUtc, actual.OccurredUtc);
        Assert.Equal(expected.ActorId, actual.ActorId);
        Assert.Equal(expected.ActorType, actual.ActorType);
        Assert.Equal(expected.ActorDisplayName, actual.ActorDisplayName);
        Assert.Equal(expected.OperationName, actual.OperationName);
        Assert.Equal(expected.Outcome, actual.Outcome);
        Assert.Equal(expected.ReasonCodes, actual.ReasonCodes);
        Assert.Equal(expected.CorrelationId, actual.CorrelationId);
        Assert.Equal(expected.TraceId, actual.TraceId);
        Assert.Equal(expected.SpanId, actual.SpanId);
        Assert.Equal(expected.ParentSpanId, actual.ParentSpanId);
        Assert.Equal(expected.DecisionLatencyMs, actual.DecisionLatencyMs);
        Assert.Equal(expected.ConstraintSetHash, actual.ConstraintSetHash);
        Assert.Equal(expected.ConstraintCount, actual.ConstraintCount);
        Assert.Equal(expected.RiskScore, actual.RiskScore);
        Assert.Equal(expected.PolicyScope, actual.PolicyScope);
        Assert.Equal(expected.TenantHash, actual.TenantHash);
        Assert.Equal(expected.OrganizationHash, actual.OrganizationHash);
        Assert.Equal(expected.EmitterStatus, actual.EmitterStatus);
        Assert.Equal(expected.EmitterProvider, actual.EmitterProvider);
        Assert.Equal(expected.OutboxSequence, actual.OutboxSequence);
        Assert.Equal(expected.GatewayExecutionId, actual.GatewayExecutionId);
        Assert.Equal(expected.DecisionStage, actual.DecisionStage);
        Assert.Equal(expected.PolicyVersion, actual.PolicyVersion);
        Assert.Equal(expected.PolicyHash, actual.PolicyHash);
        Assert.Equal(expected.Metadata, actual.Metadata);
    }
}
