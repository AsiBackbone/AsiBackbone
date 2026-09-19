using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Serialization;
using Xunit;

namespace AsiBackbone.Core.Tests.Emissions;

/// <summary>
/// Unit tests for the provider-neutral governance emission contract.
/// </summary>
public sealed class GovernanceEmissionContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Verifies that a neutral envelope can be created with minimal information and serialized without provider dependencies.
    /// </summary>
    [Fact]
    public void CreateDefaultsAndSerializesNeutralEnvelope()
    {
        var envelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Decision,
            eventId: " event-123 ",
            envelopeId: " envelope-123 ",
            occurredUtc: new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.FromHours(-5)),
            createdUtc: new DateTimeOffset(2026, 6, 15, 14, 1, 0, TimeSpan.Zero),
            metadata: new Dictionary<string, string>
            {
                [" source "] = " test ",
                [" "] = "ignored"
            });

        Assert.Equal("envelope-123", envelope.EnvelopeId);
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, envelope.SchemaVersion);
        Assert.Equal(GovernanceEmissionEventType.Decision, envelope.EventType);
        Assert.Equal("event-123", envelope.EventId);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero), envelope.OccurredUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 14, 1, 0, TimeSpan.Zero), envelope.CreatedUtc);
        Assert.False(envelope.HasCorrelation);
        Assert.True(envelope.HasMetadata);
        Assert.Equal("test", envelope.Metadata["source"]);
        Assert.False(envelope.Metadata.ContainsKey(""));

        string json = JsonSerializer.Serialize(envelope, JsonOptions);
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal("envelope-123", root.GetProperty("envelopeId").GetString());
        Assert.Equal("event-123", root.GetProperty("eventId").GetString());
        Assert.Equal((int)GovernanceEmissionEventType.Decision, root.GetProperty("eventType").GetInt32());
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, root.GetProperty("schemaVersion").GetString());
    }

    /// <summary>
    /// Verifies that an audit residue envelope preserves correlation, trace, policy, and safe diagnostic telemetry.
    /// </summary>
    [Fact]
    public void FromDecisionReceiptPreservesCorrelationTracePolicyAndTelemetryFields()
    {
        var payload = GovernanceEmissionPayload.Create(
            "audit-residue",
            schemaVersion: "1.1-test",
            contentType: "application/json",
            contentHash: "payload-hash",
            sizeBytes: 512,
            metadata: new Dictionary<string, string>
            {
                ["classification"] = "minimized"
            });

        var residue = DecisionReceipt.Create(
            GovernanceActorContext.Service(" service-123 "),
            " document.approve ",
            " Allowed ",
            eventId: " event-123 ",
            correlationId: " correlation-123 ",
            traceId: " trace-456 ",
            policyVersion: " v1 ",
            policyHash: " policy-hash ",
            metadata: new Dictionary<string, string>
            {
                [" source "] = " residue "
            },
            auditResidueId: " residue-123 ",
            spanId: " span-789 ",
            parentSpanId: " parent-span ",
            emitterStatus: " queued ",
            emitterProvider: " outbox ",
            outboxSequence: 44,
            gatewayExecutionId: " gateway-123 ",
            decisionStage: " DecisionEvaluated ");

        var envelope = GovernanceEmissionEnvelope.FromDecisionReceipt(
            residue,
            envelopeId: " envelope-123 ",
            payload: payload,
            metadata: new Dictionary<string, string>
            {
                [" emission "] = " pending "
            });

        Assert.Equal(GovernanceEmissionEventType.AuditResidue, envelope.EventType);
        Assert.Equal("event-123", envelope.EventId);
        Assert.Equal("correlation-123", envelope.CorrelationId);
        Assert.Equal("residue-123", envelope.AuditResidueId);
        Assert.Equal("trace-456", envelope.TraceId);
        Assert.Equal("span-789", envelope.SpanId);
        Assert.Equal("parent-span", envelope.ParentSpanId);
        Assert.Equal("v1", envelope.PolicyVersion);
        Assert.Equal("policy-hash", envelope.PolicyHash);
        Assert.Equal("service-123", envelope.ActorId);
        Assert.Equal("document.approve", envelope.OperationName);
        Assert.Equal("Allowed", envelope.Outcome);
        Assert.Equal("queued", envelope.EmitterStatus);
        Assert.Equal("outbox", envelope.EmitterProvider);
        Assert.Equal(44, envelope.OutboxSequence);
        Assert.Equal("gateway-123", envelope.GatewayExecutionId);
        Assert.Equal("DecisionEvaluated", envelope.DecisionStage);
        Assert.True(envelope.HasCorrelation);
        Assert.Same(payload, envelope.Payload);
        Assert.Equal("residue", envelope.Metadata["source"]);
        Assert.Equal("pending", envelope.Metadata["emission"]);
    }

    /// <summary>
    /// Verifies that lifecycle events map to the lifecycle category and preserve stage correlation.
    /// </summary>
    [Fact]
    public void FromLifecycleEventPreservesLifecycleStageAndCorrelation()
    {
        var lifecycleEvent = DecisionReceiptLifecycleEvent.Create(
            DecisionReceiptLifecycleStage.ExternalEmissionFailed,
            " correlation-123 ",
            auditResidueId: " residue-123 ",
            eventId: " lifecycle-123 ",
            traceId: " trace-456 ",
            operationName: " governance.emit ",
            outcome: " Failed ",
            metadata: new Dictionary<string, string>
            {
                ["attempt"] = "1"
            });

        var envelope = GovernanceEmissionEnvelope.FromLifecycleEvent(
            lifecycleEvent,
            envelopeId: " envelope-123 ",
            metadata: new Dictionary<string, string>
            {
                ["provider"] = "siem"
            });

        Assert.Equal(GovernanceEmissionEventType.AuditLifecycle, envelope.EventType);
        Assert.Equal("lifecycle-123", envelope.EventId);
        Assert.Equal("correlation-123", envelope.CorrelationId);
        Assert.Equal("residue-123", envelope.AuditResidueId);
        Assert.Equal(DecisionReceiptLifecycleStage.ExternalEmissionFailed, envelope.LifecycleStage);
        Assert.Equal((int)DecisionReceiptLifecycleStage.ExternalEmissionFailed, envelope.LifecycleStageSequence);
        Assert.Equal("ExternalEmissionFailed", envelope.DecisionStage);
        Assert.Equal("trace-456", envelope.TraceId);
        Assert.Equal("governance.emit", envelope.OperationName);
        Assert.Equal("Failed", envelope.Outcome);
        Assert.Equal("1", envelope.Metadata["attempt"]);
        Assert.Equal("siem", envelope.Metadata["provider"]);
    }

    /// <summary>
    /// Verifies provider-neutral result status mapping for delivered, deferred, retryable, and dead-letter outcomes.
    /// </summary>
    [Fact]
    public void ResultFactoriesMapProviderNeutralStatuses()
    {
        var delivered = GovernanceEmissionResult.Delivered(
            providerName: "file",
            providerRecordId: "provider-record-123");
        var deferred = GovernanceEmissionResult.Deferred(
            retryAfterUtc: new DateTimeOffset(2026, 6, 15, 15, 0, 0, TimeSpan.FromHours(-5)));
        var retryError = GovernanceEmissionError.Create(
            "provider.timeout",
            "Provider timed out.",
            isRetryable: true,
            providerName: "siem",
            providerErrorCode: "timeout");
        var retryableFailure = GovernanceEmissionResult.RetryableFailure(retryError);
        var deadLettered = GovernanceEmissionResult.DeadLettered(
            GovernanceEmissionError.Create("provider.rejected", "Provider rejected the minimized envelope."));

        Assert.True(delivered.IsSuccess);
        Assert.True(delivered.IsTerminal);
        Assert.False(delivered.ShouldRetry);
        Assert.Equal("file", delivered.ProviderName);
        Assert.Equal("provider-record-123", delivered.ProviderRecordId);

        Assert.Equal(GovernanceEmissionStatus.Deferred, deferred.Status);
        Assert.True(deferred.ShouldRetry);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 20, 0, 0, TimeSpan.Zero), deferred.RetryAfterUtc);

        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, retryableFailure.Status);
        Assert.True(retryableFailure.ShouldRetry);
        Assert.False(retryableFailure.IsTerminal);
        Assert.Equal("siem", retryableFailure.ProviderName);
        Assert.Equal("timeout", retryableFailure.Error?.ProviderErrorCode);

        Assert.Equal(GovernanceEmissionStatus.DeadLettered, deadLettered.Status);
        Assert.True(deadLettered.IsTerminal);
        Assert.False(deadLettered.ShouldRetry);
    }

    /// <summary>
    /// Verifies that a provider-neutral emitter implementation can accept an envelope and return a neutral result.
    /// </summary>
    [Fact]
    public async Task EmitterContractAcceptsEnvelopeAndReturnsResult()
    {
        var emitter = new CapturingGovernanceEmitter();
        var envelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Outbox,
            eventId: "outbox-123",
            correlationId: "correlation-123");

        GovernanceEmissionResult result = await emitter.EmitAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Same(envelope, emitter.LastEnvelope);
        Assert.Equal(GovernanceEmissionStatus.Delivered, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal("test-emitter", result.ProviderName);
    }

    /// <summary>
    /// Verifies that invalid provider-neutral emission values are rejected before downstream provider execution.
    /// </summary>
    [Fact]
    public void CreateRejectsInvalidEnumAndNegativeDiagnostics()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GovernanceEmissionEnvelope.Create((GovernanceEmissionEventType)999));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GovernanceEmissionEnvelope.Create(
                GovernanceEmissionEventType.Outbox,
                outboxSequence: -1));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GovernanceEmissionPayload.Create(
                "audit-residue",
                sizeBytes: -1));
    }

    private sealed class CapturingGovernanceEmitter : IGovernanceEmitter
    {
        public GovernanceEmissionEnvelope? LastEnvelope { get; private set; }

        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            cancellationToken.ThrowIfCancellationRequested();

            LastEnvelope = envelope;

            return ValueTask.FromResult(
                GovernanceEmissionResult.Delivered(providerName: "test-emitter"));
        }
    }
}
