using AsiBackbone.Core.Emissions;
using Xunit;

namespace AsiBackbone.OpenTelemetry.Tests;

/// <summary>
/// Exhaustive contract coverage for OpenTelemetry governance event-name mapping.
/// </summary>
public sealed class OpenTelemetryGovernanceEventNameMapperTests
{
    /// <summary>
    /// Gets the stable mappings for all non-provider governance event categories.
    /// </summary>
    public static TheoryData<GovernanceEmissionEventType, string> StableEventNames => new()
    {
        { GovernanceEmissionEventType.Decision, OpenTelemetryGovernanceInstrumentation.DecisionEvaluatedEventName },
        { GovernanceEmissionEventType.Acknowledgment, OpenTelemetryGovernanceInstrumentation.AcknowledgmentRecordedEventName },
        { GovernanceEmissionEventType.CapabilityToken, OpenTelemetryGovernanceInstrumentation.CapabilityTokenIssuedEventName },
        { GovernanceEmissionEventType.Gateway, OpenTelemetryGovernanceInstrumentation.GatewayCompletedEventName },
        { GovernanceEmissionEventType.DecisionReceipt, OpenTelemetryGovernanceInstrumentation.DecisionReceiptCreatedEventName },
        { GovernanceEmissionEventType.AuditLifecycle, OpenTelemetryGovernanceInstrumentation.LifecycleRecordedEventName },
        { GovernanceEmissionEventType.Outbox, OpenTelemetryGovernanceInstrumentation.OutboxUpdatedEventName }
    };

    /// <summary>
    /// Verifies every defined non-provider event category retains its established event name.
    /// </summary>
    [Theory]
    [MemberData(nameof(StableEventNames))]
    public void GetEventNameReturnsStableNameForDefinedCategory(
        GovernanceEmissionEventType eventType,
        string expectedEventName)
    {
        string eventName = OpenTelemetryGovernanceEventNameMapper.GetEventName(eventType, emitterStatus: null);

        Assert.Equal(expectedEventName, eventName);
    }

    /// <summary>
    /// Verifies provider-emission statuses without a failure marker retain the delivered event name.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("delivered")]
    [InlineData("succeeded")]
    [InlineData("queued")]
    public void GetEventNameMapsNonFailureProviderStatusesToDelivered(string? emitterStatus)
    {
        string eventName = OpenTelemetryGovernanceEventNameMapper.GetEventName(
            GovernanceEmissionEventType.ProviderEmission,
            emitterStatus);

        Assert.Equal(OpenTelemetryGovernanceInstrumentation.EmissionDeliveredEventName, eventName);
    }

    /// <summary>
    /// Verifies fail, dead, and retry markers remain case-insensitive and preserve substring matching.
    /// </summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("FAILURE")]
    [InlineData("provider-failover")]
    [InlineData("dead-lettered")]
    [InlineData("already_DEAD")]
    [InlineData("retryable")]
    [InlineData("RETRY-scheduled")]
    public void GetEventNameMapsFailureProviderStatusesToFailed(string emitterStatus)
    {
        string eventName = OpenTelemetryGovernanceEventNameMapper.GetEventName(
            GovernanceEmissionEventType.ProviderEmission,
            emitterStatus);

        Assert.Equal(OpenTelemetryGovernanceInstrumentation.EmissionFailedEventName, eventName);
    }

    /// <summary>
    /// Verifies unrecognized category values use the stable generic fallback event name.
    /// </summary>
    [Fact]
    public void GetEventNameUsesGenericFallbackForUnknownCategory()
    {
        string eventName = OpenTelemetryGovernanceEventNameMapper.GetEventName(
            (GovernanceEmissionEventType)int.MaxValue,
            emitterStatus: "failed");

        Assert.Equal(OpenTelemetryGovernanceInstrumentation.GenericGovernanceEventName, eventName);
    }

    /// <summary>
    /// Verifies the emitter continues to publish mapped names through its provider-neutral result metadata.
    /// </summary>
    [Theory]
    [MemberData(nameof(StableEventNames))]
    public async Task EmitAsyncPreservesMappedEventNameInResultMetadata(
        GovernanceEmissionEventType eventType,
        string expectedEventName)
    {
        OpenTelemetryGovernanceEmitter emitter = new(new OpenTelemetryGovernanceEmitterOptions
        {
            EmitActivityEvents = false,
            EmitMetrics = false
        });

        GovernanceEmissionResult result = await emitter.EmitAsync(
            CreateEnvelope(eventType, emitterStatus: null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedEventName, result.Metadata["opentelemetry.event_name"]);
    }

    /// <summary>
    /// Verifies provider-emission delivered and failed names remain visible to existing result consumers.
    /// </summary>
    [Theory]
    [InlineData("delivered", OpenTelemetryGovernanceInstrumentation.EmissionDeliveredEventName)]
    [InlineData("failed", OpenTelemetryGovernanceInstrumentation.EmissionFailedEventName)]
    [InlineData("dead-lettered", OpenTelemetryGovernanceInstrumentation.EmissionFailedEventName)]
    [InlineData("retryable", OpenTelemetryGovernanceInstrumentation.EmissionFailedEventName)]
    public async Task EmitAsyncPreservesProviderEmissionEventNameInResultMetadata(
        string emitterStatus,
        string expectedEventName)
    {
        OpenTelemetryGovernanceEmitter emitter = new(new OpenTelemetryGovernanceEmitterOptions
        {
            EmitActivityEvents = false,
            EmitMetrics = false
        });

        GovernanceEmissionResult result = await emitter.EmitAsync(
            CreateEnvelope(GovernanceEmissionEventType.ProviderEmission, emitterStatus),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedEventName, result.Metadata["opentelemetry.event_name"]);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(
        GovernanceEmissionEventType eventType,
        string? emitterStatus)
    {
        return GovernanceEmissionEnvelope.Create(
            eventType,
            eventId: $"event-{eventType}",
            occurredUtc: new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventType}",
            createdUtc: new DateTimeOffset(2026, 7, 10, 18, 0, 1, TimeSpan.Zero),
            schemaVersion: "1.0.0",
            correlationId: "opentelemetry-event-name-mapping",
            operationName: "governance.emit",
            outcome: "Recorded",
            emitterStatus: emitterStatus,
            emitterProvider: "test-provider",
            decisionStage: "Emitted");
    }
}
