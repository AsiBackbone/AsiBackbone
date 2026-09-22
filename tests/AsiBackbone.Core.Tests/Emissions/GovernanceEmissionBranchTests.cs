using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using Xunit;

namespace AsiBackbone.Core.Tests.Emissions;

/// <summary>
/// Focused branch tests for provider-neutral governance emission domain objects.
/// </summary>
public sealed class GovernanceEmissionBranchTests
{
    /// <summary>
    /// Tests that creating a governance emission envelope with an invalid lifecycle stage throws an exception.
    /// </summary>
    [Fact]
    public void CreateRejectsInvalidLifecycleStageWhenSupplied()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GovernanceEmissionEnvelope.Create(
                GovernanceEmissionEventType.AuditLifecycle,
                lifecycleStage: (DecisionReceiptLifecycleStage)999));
    }

    /// <summary>
    /// Tests that creating a governance emission envelope with any of the supported correlation fields results in HasCorrelation being true, and that when all are null, HasCorrelation is false.
    /// </summary>
    /// <param name="correlationId">
    /// The correlation ID for the emission envelope.
    /// </param>
    /// <param name="traceId">
    /// The trace ID for the emission envelope.
    /// </param>
    /// <param name="decisionReceiptId">
    /// The decision receipt ID for the emission envelope.
    /// </param>
    /// <param name="expectedHasCorrelation">
    /// The expected value of the HasCorrelation property.
    /// </param>
    [Theory]
    [InlineData(null, null, null, false)]
    [InlineData(" correlation-123 ", null, null, true)]
    [InlineData(null, " trace-123 ", null, true)]
    [InlineData(null, null, " residue-123 ", true)]
    public void CreateReportsHasCorrelationFromAnySupportedCorrelationField(
        string? correlationId,
        string? traceId,
        string? decisionReceiptId,
        bool expectedHasCorrelation)
    {
        var envelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Decision,
            correlationId: correlationId,
            traceId: traceId,
            decisionReceiptId: decisionReceiptId);

        Assert.Equal(expectedHasCorrelation, envelope.HasCorrelation);
    }

    /// <summary>
    /// Tests that when creating a governance emission envelope from an decision receipt, if the caller-supplied metadata contains keys that overlap with the residue's metadata, the caller's values take precedence in the resulting envelope's metadata.
    /// </summary>
    [Fact]
    public void FromDecisionReceiptUsesCallerMetadataWhenKeysOverlapResidueMetadata()
    {
        var residue = DecisionReceipt.Create(
            GovernanceActorContext.Service("service-123"),
            "document.approve",
            "Allowed",
            eventId: "event-123",
            correlationId: "correlation-123",
            metadata: new Dictionary<string, string>
            {
                [" shared "] = " residue ",
                ["residue-only"] = " retained "
            },
            decisionReceiptId: "residue-123");

        var envelope = GovernanceEmissionEnvelope.FromDecisionReceipt(
            residue,
            metadata: new Dictionary<string, string>
            {
                ["shared"] = " caller ",
                [" caller-only "] = " added "
            });

        Assert.Equal("caller", envelope.Metadata["shared"]);
        Assert.Equal("retained", envelope.Metadata["residue-only"]);
        Assert.Equal("added", envelope.Metadata["caller-only"]);
    }

    /// <summary>
    /// Tests that when creating a governance emission envelope from an decision receipt lifecycle event, if the caller-supplied metadata contains keys that overlap with the lifecycle event's metadata, the caller's values take precedence in the resulting envelope's metadata.
    /// </summary>
    [Fact]
    public void FromLifecycleEventUsesCallerMetadataWhenKeysOverlapLifecycleMetadata()
    {
        var lifecycleEvent = DecisionReceiptLifecycleEvent.Create(
            DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            "correlation-123",
            decisionReceiptId: "residue-123",
            eventId: "lifecycle-123",
            metadata: new Dictionary<string, string>
            {
                [" shared "] = " lifecycle ",
                ["lifecycle-only"] = " retained "
            });

        var envelope = GovernanceEmissionEnvelope.FromLifecycleEvent(
            lifecycleEvent,
            metadata: new Dictionary<string, string>
            {
                ["shared"] = " caller ",
                [" caller-only "] = " added "
            });

        Assert.Equal("caller", envelope.Metadata["shared"]);
        Assert.Equal("retained", envelope.Metadata["lifecycle-only"]);
        Assert.Equal("added", envelope.Metadata["caller-only"]);
    }

    /// <summary>
    /// Tests that creating a governance emission envelope with null, empty, or blank metadata results in an empty metadata view in the resulting envelope.
    /// </summary>
    [Fact]
    public void CreateReturnsEmptyMetadataViewForNullEmptyAndBlankMetadata()
    {
        var nullMetadataEnvelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Decision,
            metadata: null);
        var emptyMetadataEnvelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Decision,
            metadata: new Dictionary<string, string>());
        var blankMetadataEnvelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Decision,
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored"
            });

        Assert.False(nullMetadataEnvelope.HasMetadata);
        Assert.Empty(nullMetadataEnvelope.Metadata);
        Assert.False(emptyMetadataEnvelope.HasMetadata);
        Assert.Empty(emptyMetadataEnvelope.Metadata);
        Assert.False(blankMetadataEnvelope.HasMetadata);
        Assert.Empty(blankMetadataEnvelope.Metadata);
    }

    /// <summary>
    /// Tests that creating a governance emission envelope with optional string parameters trims whitespace and normalizes blank strings to null in the resulting envelope.
    /// </summary>
    [Fact]
    public void CreateTrimsOptionalStringsAndNormalizesBlankStringsToNull()
    {
        var envelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.ProviderEmission,
            eventId: " event-123 ",
            envelopeId: " envelope-123 ",
            correlationId: " ",
            decisionReceiptId: " residue-123 ",
            policyVersion: " v1 ",
            policyHash: " ",
            traceId: " trace-123 ",
            spanId: " ",
            parentSpanId: " parent-span ",
            operationName: " document.approve ",
            outcome: " Allowed ",
            actorId: " actor-123 ",
            emitterStatus: " ",
            emitterProvider: " outbox ",
            gatewayExecutionId: " gateway-123 ",
            decisionStage: " DecisionEvaluated ");

        Assert.Equal("event-123", envelope.EventId);
        Assert.Equal("envelope-123", envelope.EnvelopeId);
        Assert.Null(envelope.CorrelationId);
        Assert.Equal("residue-123", envelope.DecisionReceiptId);
        Assert.Equal("v1", envelope.PolicyVersion);
        Assert.Null(envelope.PolicyHash);
        Assert.Equal("trace-123", envelope.TraceId);
        Assert.Null(envelope.SpanId);
        Assert.Equal("parent-span", envelope.ParentSpanId);
        Assert.Equal("document.approve", envelope.OperationName);
        Assert.Equal("Allowed", envelope.Outcome);
        Assert.Equal("actor-123", envelope.ActorId);
        Assert.Null(envelope.EmitterStatus);
        Assert.Equal("outbox", envelope.EmitterProvider);
        Assert.Equal("gateway-123", envelope.GatewayExecutionId);
        Assert.Equal("DecisionEvaluated", envelope.DecisionStage);
        Assert.True(envelope.HasCorrelation);
    }

    /// <summary>
    /// Tests that creating a governance emission payload trims whitespace from optional string parameters, normalizes blank strings to null, and rejects invalid inputs.
    /// </summary>
    [Fact]
    public void PayloadCreateTrimsOptionalStringsNormalizesMetadataAndRejectsInvalidInputs()
    {
        var payload = GovernanceEmissionPayload.Create(
            " audit-residue ",
            schemaVersion: " ",
            contentType: " application/json ",
            contentHash: " ",
            metadata: new Dictionary<string, string>
            {
                [" key "] = " value ",
                [" "] = "ignored"
            });

        Assert.Equal("audit-residue", payload.PayloadType);
        Assert.Null(payload.SchemaVersion);
        Assert.Equal("application/json", payload.ContentType);
        Assert.Null(payload.ContentHash);
        Assert.Null(payload.SizeBytes);
        Assert.True(payload.HasMetadata);
        Assert.Equal("value", payload.Metadata["key"]);
        Assert.DoesNotContain(payload.Metadata, item => string.IsNullOrEmpty(item.Key));

        _ = Assert.Throws<ArgumentException>(() => GovernanceEmissionPayload.Create(" "));
    }

    /// <summary>
    /// Tests that creating governance emission results and errors trims whitespace from optional string parameters, normalizes blank strings to null, and rejects invalid inputs.
    /// </summary>
    [Fact]
    public void ResultFactoriesNormalizeOptionalStringsAndMetadata()
    {
        var pending = GovernanceEmissionResult.Pending(providerName: " ");
        var delivered = GovernanceEmissionResult.Delivered(
            providerName: " provider ",
            providerRecordId: " record-123 ",
            metadata: new Dictionary<string, string>
            {
                [" result "] = " delivered ",
                [" "] = "ignored"
            });
        var retryableError = GovernanceEmissionError.Create(
            " retry.timeout ",
            " Provider timed out. ",
            isRetryable: true,
            providerName: " error-provider ");
        var failed = GovernanceEmissionResult.Failed(retryableError);

        Assert.Null(pending.ProviderName);
        Assert.False(pending.HasMetadata);
        Assert.Equal("provider", delivered.ProviderName);
        Assert.Equal("record-123", delivered.ProviderRecordId);
        Assert.True(delivered.HasMetadata);
        Assert.Equal("delivered", delivered.Metadata["result"]);
        Assert.DoesNotContain(delivered.Metadata, item => string.IsNullOrEmpty(item.Key));
        Assert.Equal("error-provider", failed.ProviderName);
        Assert.True(failed.ShouldRetry);
    }

    /// <summary>
    /// Tests that creating a governance emission error trims whitespace from optional string parameters and rejects blank required values.
    /// </summary>
    [Fact]
    public void ErrorCreateTrimsOptionalStringsAndRejectsBlankRequiredValues()
    {
        var error = GovernanceEmissionError.Create(
            " provider.timeout ",
            " Provider timed out. ",
            isRetryable: true,
            providerName: " provider ",
            providerErrorCode: " timeout ");

        Assert.Equal("provider.timeout", error.Code);
        Assert.Equal("Provider timed out.", error.Message);
        Assert.True(error.IsRetryable);
        Assert.Equal("provider", error.ProviderName);
        Assert.Equal("timeout", error.ProviderErrorCode);

        _ = Assert.Throws<ArgumentException>(() => GovernanceEmissionError.Create(" ", "message"));
        _ = Assert.Throws<ArgumentException>(() => GovernanceEmissionError.Create("code", " "));
    }
}
