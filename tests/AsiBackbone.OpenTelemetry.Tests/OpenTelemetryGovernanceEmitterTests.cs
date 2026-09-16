using System.Diagnostics;
using System.Diagnostics.Metrics;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using Xunit;

namespace AsiBackbone.OpenTelemetry.Tests;

/// <summary>
/// Unit tests for the OpenTelemetry governance emission provider.
/// </summary>
public sealed class OpenTelemetryGovernanceEmitterTests
{
    /// <summary>
    /// Verifies that a governance envelope is projected into activity tags and an activity event.
    /// </summary>
    [Fact]
    public async Task EmitAsyncRecordsActivityEventWithGovernanceAttributes()
    {
        List<Activity> stoppedActivities = [];
        using ActivityListener listener = new()
        {
            ShouldListenTo = static source => source.Name == OpenTelemetryGovernanceInstrumentation.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add
        };

        ActivitySource.AddActivityListener(listener);

        GovernanceEmissionEnvelope envelope = CreateEnvelope();
        OpenTelemetryGovernanceEmitter emitter = new();

        GovernanceEmissionResult result = await emitter.EmitAsync(envelope, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(GovernanceEmissionStatus.Delivered, result.Status);
        Assert.Equal(OpenTelemetryGovernanceInstrumentation.ProviderName, result.ProviderName);
        Assert.Equal(envelope.EnvelopeId, result.ProviderRecordId);

        Activity activity = Assert.Single(stoppedActivities);
        ActivityEvent activityEvent = Assert.Single(activity.Events);
        Assert.Equal(OpenTelemetryGovernanceInstrumentation.DecisionEvaluatedEventName, activityEvent.Name);

        var tags = activity.TagObjects.ToDictionary(static tag => tag.Key, static tag => tag.Value, StringComparer.Ordinal);
        Assert.Equal(envelope.EnvelopeId, tags[OpenTelemetryGovernanceAttributes.EnvelopeId]?.ToString());
        Assert.Equal(envelope.CorrelationId, tags[OpenTelemetryGovernanceAttributes.CorrelationId]?.ToString());
        Assert.Equal(envelope.AuditResidueId, tags[OpenTelemetryGovernanceAttributes.AuditResidueId]?.ToString());
        Assert.Equal(envelope.PolicyVersion, tags[OpenTelemetryGovernanceAttributes.PolicyVersion]?.ToString());
        Assert.Equal(envelope.PolicyHash, tags[OpenTelemetryGovernanceAttributes.PolicyHash]?.ToString());
        Assert.Equal(envelope.TraceId, tags[OpenTelemetryGovernanceAttributes.TraceId]?.ToString());
        Assert.Equal(envelope.SpanId, tags[OpenTelemetryGovernanceAttributes.SpanId]?.ToString());
        Assert.Equal(envelope.ParentSpanId, tags[OpenTelemetryGovernanceAttributes.ParentSpanId]?.ToString());
        Assert.Equal(envelope.GatewayExecutionId, tags[OpenTelemetryGovernanceAttributes.GatewayExecutionId]?.ToString());
        Assert.Equal(envelope.SchemaVersion, tags[OpenTelemetryGovernanceAttributes.SchemaVersion]?.ToString());
        Assert.Equal(envelope.EmitterStatus, tags[OpenTelemetryGovernanceAttributes.EmitterStatus]?.ToString());
        Assert.Equal(envelope.LifecycleStage?.ToString(), tags[OpenTelemetryGovernanceAttributes.LifecycleStage]?.ToString());
        Assert.Equal(envelope.LifecycleStageSequence?.ToString(System.Globalization.CultureInfo.InvariantCulture), tags[OpenTelemetryGovernanceAttributes.LifecycleStageSequence]?.ToString());
    }

    /// <summary>
    /// Verifies that provider metrics are emitted with low-cardinality labels.
    /// </summary>
    [Fact]
    public async Task EmitAsyncRecordsLowCardinalityMetrics()
    {
        List<string> metricNames = [];
        List<IReadOnlyDictionary<string, object?>> metricTags = [];

        using MeterListener listener = new();
        listener.InstrumentPublished = static (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == OpenTelemetryGovernanceInstrumentation.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            metricNames.Add(instrument.Name);
            metricTags.Add(ToDictionary(tags));
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            metricNames.Add(instrument.Name);
            metricTags.Add(ToDictionary(tags));
        });
        listener.Start();

        OpenTelemetryGovernanceEmitter emitter = new();
        GovernanceEmissionResult result = await emitter.EmitAsync(CreateEnvelope(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains(OpenTelemetryGovernanceInstrumentation.EmissionsCounterName, metricNames);
        Assert.Contains(OpenTelemetryGovernanceInstrumentation.EmissionLatencyHistogramName, metricNames);
        Assert.Contains(metricTags, tags =>
            tags.TryGetValue(OpenTelemetryGovernanceAttributes.MetricEventType, out object? eventType)
            && string.Equals(GovernanceEmissionEventType.Decision.ToString(), eventType?.ToString(), StringComparison.Ordinal)
            && tags.TryGetValue(OpenTelemetryGovernanceAttributes.MetricResult, out object? metricResult)
            && string.Equals(GovernanceEmissionStatus.Delivered.ToString(), metricResult?.ToString(), StringComparison.Ordinal)
            && tags.TryGetValue(OpenTelemetryGovernanceAttributes.MetricProvider, out object? provider)
            && string.Equals(OpenTelemetryGovernanceInstrumentation.ProviderName, provider?.ToString(), StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that instrumentation failures are normalized into provider-neutral retryable results.
    /// </summary>
    [Fact]
    public async Task EmitAsyncNormalizesInstrumentationFailure()
    {
        OpenTelemetryGovernanceEmitter emitter = new(new OpenTelemetryGovernanceEmitterOptions
        {
            BeforeEmitAsync = static (_, _) => throw new InvalidOperationException("Synthetic provider failure.")
        });

        GovernanceEmissionResult result = await emitter.EmitAsync(CreateEnvelope(), TestContext.Current.CancellationToken);

        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, result.Status);
        Assert.True(result.ShouldRetry);
        GovernanceEmissionError error = Assert.IsType<GovernanceEmissionError>(result.Error);
        Assert.Equal("opentelemetry.emission.exception", error.Code);
        Assert.Equal(OpenTelemetryGovernanceInstrumentation.ProviderName, result.ProviderName);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope()
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Decision,
            eventId: "event-197",
            occurredUtc: new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
            envelopeId: "envelope-197",
            createdUtc: new DateTimeOffset(2026, 6, 15, 12, 0, 1, TimeSpan.Zero),
            schemaVersion: "1.0.0",
            correlationId: "correlation-197",
            auditResidueId: "audit-residue-197",
            lifecycleStage: DecisionReceiptLifecycleStage.DecisionEvaluated,
            policyVersion: "policy-v1",
            policyHash: "policy-hash-197",
            traceId: "trace-197",
            spanId: "span-197",
            parentSpanId: "parent-span-197",
            operationName: "issue-197-test-operation",
            outcome: "Allow",
            actorId: "actor-197",
            emitterStatus: GovernanceEmissionStatus.Pending.ToString(),
            emitterProvider: "outbox",
            outboxSequence: 197,
            gatewayExecutionId: "gateway-197",
            decisionStage: "Evaluated",
            payload: GovernanceEmissionPayload.Create(
                "decision",
                schemaVersion: "1.0.0",
                contentType: "application/json",
                contentHash: "sha256:197",
                sizeBytes: 197));
    }

    private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, object?> tag in tags)
        {
            result[tag.Key] = tag.Value;
        }

        return result;
    }
}
