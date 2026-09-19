using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Serialization;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Unit tests for provider-neutral audit residue telemetry and traceability fields.
/// </summary>
public sealed class AuditResidueTelemetryFieldsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Verifies that telemetry fields are optional and default to a backward-compatible empty state.
    /// </summary>
    [Fact]
    public void CreateDefaultsTelemetryFieldsToNullAndUsesStableSchemaVersion()
    {
        var residue = DecisionReceipt.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed",
            eventId: "event-123");

        Assert.Equal("event-123", residue.EventId);
        Assert.Equal("event-123", residue.AuditResidueId);
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, residue.SchemaVersion);
        Assert.Null(residue.SpanId);
        Assert.Null(residue.ParentSpanId);
        Assert.Null(residue.DecisionLatencyMs);
        Assert.Null(residue.ConstraintSetHash);
        Assert.Null(residue.ConstraintCount);
        Assert.Null(residue.RiskScore);
        Assert.Null(residue.PolicyScope);
        Assert.Null(residue.TenantHash);
        Assert.Null(residue.OrganizationHash);
        Assert.Null(residue.EmitterStatus);
        Assert.Null(residue.EmitterProvider);
        Assert.Null(residue.OutboxSequence);
        Assert.Null(residue.GatewayExecutionId);
        Assert.Null(residue.DecisionStage);
    }

    /// <summary>
    /// Verifies that telemetry fields are normalized, preserved, and serialized using provider-neutral names.
    /// </summary>
    [Fact]
    public void CreateNormalizesAndSerializesTelemetryFields()
    {
        var residue = DecisionReceipt.Create(
            GovernanceActorContext.Service("service-123"),
            "gateway.execute",
            "Allowed",
            eventId: " event-123 ",
            correlationId: " correlation-123 ",
            traceId: " trace-456 ",
            policyVersion: " v1 ",
            policyHash: " policy-hash ",
            auditResidueId: " residue-789 ",
            spanId: " span-abc ",
            parentSpanId: " parent-span ",
            decisionLatencyMs: 42,
            constraintSetHash: " constraint-hash ",
            constraintCount: 7,
            riskScore: 0.75,
            policyScope: " regional-policy ",
            tenantHash: " tenant-hash ",
            organizationHash: " org-hash ",
            emitterStatus: " queued ",
            emitterProvider: " open-telemetry ",
            outboxSequence: 1234,
            gatewayExecutionId: " gateway-123 ",
            decisionStage: " GatewayExecutionStarted ",
            schemaVersion: " 1.1-test ");

        Assert.Equal("event-123", residue.EventId);
        Assert.Equal("residue-789", residue.AuditResidueId);
        Assert.Equal("1.1-test", residue.SchemaVersion);
        Assert.Equal("correlation-123", residue.CorrelationId);
        Assert.Equal("trace-456", residue.TraceId);
        Assert.Equal("span-abc", residue.SpanId);
        Assert.Equal("parent-span", residue.ParentSpanId);
        Assert.Equal(42, residue.DecisionLatencyMs);
        Assert.Equal("constraint-hash", residue.ConstraintSetHash);
        Assert.Equal(7, residue.ConstraintCount);
        Assert.Equal(0.75, residue.RiskScore.GetValueOrDefault());
        Assert.Equal("regional-policy", residue.PolicyScope);
        Assert.Equal("tenant-hash", residue.TenantHash);
        Assert.Equal("org-hash", residue.OrganizationHash);
        Assert.Equal("queued", residue.EmitterStatus);
        Assert.Equal("open-telemetry", residue.EmitterProvider);
        Assert.Equal(1234, residue.OutboxSequence);
        Assert.Equal("gateway-123", residue.GatewayExecutionId);
        Assert.Equal("GatewayExecutionStarted", residue.DecisionStage);

        string json = JsonSerializer.Serialize(residue, JsonOptions);
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal("residue-789", root.GetProperty("auditResidueId").GetString());
        Assert.Equal("1.1-test", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("trace-456", root.GetProperty("traceId").GetString());
        Assert.Equal("span-abc", root.GetProperty("spanId").GetString());
        Assert.Equal("parent-span", root.GetProperty("parentSpanId").GetString());
        Assert.Equal(42, root.GetProperty("decisionLatencyMs").GetInt64());
        Assert.Equal("constraint-hash", root.GetProperty("constraintSetHash").GetString());
        Assert.Equal(7, root.GetProperty("constraintCount").GetInt32());
        Assert.Equal(0.75, root.GetProperty("riskScore").GetDouble());
        Assert.Equal("regional-policy", root.GetProperty("policyScope").GetString());
        Assert.Equal("tenant-hash", root.GetProperty("tenantHash").GetString());
        Assert.Equal("org-hash", root.GetProperty("organizationHash").GetString());
        Assert.Equal("queued", root.GetProperty("emitterStatus").GetString());
        Assert.Equal("open-telemetry", root.GetProperty("emitterProvider").GetString());
        Assert.Equal(1234, root.GetProperty("outboxSequence").GetInt64());
        Assert.Equal("gateway-123", root.GetProperty("gatewayExecutionId").GetString());
        Assert.Equal("GatewayExecutionStarted", root.GetProperty("decisionStage").GetString());
    }

    /// <summary>
    /// Verifies that a decision receipt can carry neutral telemetry without changing decision correlation behavior.
    /// </summary>
    [Fact]
    public void FromDecisionPreservesCorrelationAndCarriesTelemetryFieldsIntoLedgerRecord()
    {
        var decision = GovernanceDecision.Allow(
            correlationId: "correlation-123",
            traceId: "trace-456",
            policyVersion: "v1",
            policyHash: "policy-hash");

        var residue = DecisionReceipt.FromDecision(
            GovernanceActorContext.Human("user-123", "Chris"),
            "document.approve",
            decision,
            eventId: "event-123",
            auditResidueId: "residue-123",
            spanId: "span-123",
            parentSpanId: "parent-span",
            decisionLatencyMs: 12,
            constraintSetHash: "constraint-hash",
            constraintCount: 3,
            riskScore: 0.2,
            policyScope: "district",
            tenantHash: "tenant-hash",
            organizationHash: "org-hash",
            emitterStatus: "queued",
            emitterProvider: "siem",
            outboxSequence: 9,
            gatewayExecutionId: "gateway-123",
            decisionStage: "DecisionEvaluated");

        var record = AuditLedgerRecord.FromDecisionReceipt(
            residue,
            recordId: "record-123");

        Assert.Equal("correlation-123", record.CorrelationId);
        Assert.Equal("trace-456", record.TraceId);
        Assert.Equal("residue-123", record.AuditResidueId);
        Assert.Equal("span-123", record.SpanId);
        Assert.Equal("parent-span", record.ParentSpanId);
        Assert.Equal(12, record.DecisionLatencyMs);
        Assert.Equal("constraint-hash", record.ConstraintSetHash);
        Assert.Equal(3, record.ConstraintCount);
        Assert.Equal(0.2, record.RiskScore.GetValueOrDefault());
        Assert.Equal("district", record.PolicyScope);
        Assert.Equal("tenant-hash", record.TenantHash);
        Assert.Equal("org-hash", record.OrganizationHash);
        Assert.Equal("queued", record.EmitterStatus);
        Assert.Equal("siem", record.EmitterProvider);
        Assert.Equal(9, record.OutboxSequence);
        Assert.Equal("gateway-123", record.GatewayExecutionId);
        Assert.Equal("DecisionEvaluated", record.DecisionStage);
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, record.SchemaVersion);
    }

    /// <summary>
    /// Verifies that invalid diagnostic numeric fields are rejected before serialization or persistence.
    /// </summary>
    /// <param name="decisionLatencyMs">The decision latency value.</param>
    /// <param name="constraintCount">The constraint count value.</param>
    /// <param name="riskScore">The risk score value.</param>
    /// <param name="outboxSequence">The outbox sequence value.</param>
    [Theory]
    [InlineData(-1L, null, null, null)]
    [InlineData(null, -1, null, null)]
    [InlineData(null, null, -0.1, null)]
    [InlineData(null, null, null, -1L)]
    public void CreateRejectsNegativeTelemetryNumbers(
        long? decisionLatencyMs,
        int? constraintCount,
        double? riskScore,
        long? outboxSequence)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecisionReceipt.Create(
                GovernanceActorContext.System,
                "system.sync",
                "Allowed",
                decisionLatencyMs: decisionLatencyMs,
                constraintCount: constraintCount,
                riskScore: riskScore,
                outboxSequence: outboxSequence));
    }
}
