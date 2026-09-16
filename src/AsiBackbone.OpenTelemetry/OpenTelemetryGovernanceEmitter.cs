using System.Diagnostics;
using System.Diagnostics.Metrics;
using AsiBackbone.Core.Emissions;

namespace AsiBackbone.OpenTelemetry;

/// <summary>
/// Emits provider-neutral governance envelopes through OpenTelemetry-friendly .NET diagnostics primitives.
/// </summary>
/// <remarks>
/// This emitter records activity events, activity tags, and low-cardinality metrics. It does not configure exporters or depend on Azure, SIEM, Event Hubs, Purview, robotics, or cloud-provider SDKs.
/// </remarks>
public sealed class OpenTelemetryGovernanceEmitter : IGovernanceEmitter
{
    private static readonly ActivitySource ActivitySource = new(OpenTelemetryGovernanceInstrumentation.ActivitySourceName);
    private static readonly Meter Meter = new(OpenTelemetryGovernanceInstrumentation.MeterName);
    private static readonly Counter<long> EmissionsCounter = Meter.CreateCounter<long>(
        OpenTelemetryGovernanceInstrumentation.EmissionsCounterName,
        description: "Counts AsiBackbone governance emission attempts accepted by the OpenTelemetry diagnostics provider.");
    private static readonly Counter<long> EmissionFailuresCounter = Meter.CreateCounter<long>(
        OpenTelemetryGovernanceInstrumentation.EmissionFailuresCounterName,
        description: "Counts AsiBackbone governance emission failures normalized by the OpenTelemetry diagnostics provider.");
    private static readonly Histogram<double> EmissionLatencyHistogram = Meter.CreateHistogram<double>(
        OpenTelemetryGovernanceInstrumentation.EmissionLatencyHistogramName,
        unit: "ms",
        description: "Measures local OpenTelemetry governance emission latency in milliseconds.");

    private readonly OpenTelemetryGovernanceEmitterOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryGovernanceEmitter" /> class using default options.
    /// </summary>
    public OpenTelemetryGovernanceEmitter()
        : this(new OpenTelemetryGovernanceEmitterOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryGovernanceEmitter" /> class using host-owned options.
    /// </summary>
    /// <param name="options">The OpenTelemetry governance emitter options.</param>
    public OpenTelemetryGovernanceEmitter(OpenTelemetryGovernanceEmitterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        this.options = options;
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceEmissionResult> EmitAsync(
        GovernanceEmissionEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (options.BeforeEmitAsync is not null)
            {
                await options.BeforeEmitAsync(envelope, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            double latencyMs = stopwatch.Elapsed.TotalMilliseconds;
            string eventName = OpenTelemetryGovernanceEventNameMapper.GetEventName(
                envelope.EventType,
                envelope.EmitterStatus);
            string providerName = options.ProviderName.Trim();

            if (options.EmitActivityEvents)
            {
                EmitActivity(envelope, eventName, providerName, options.DefaultActivityName, latencyMs);
            }

            if (options.EmitMetrics)
            {
                EmitDeliveredMetrics(envelope, providerName, latencyMs);
            }

            return GovernanceEmissionResult.Delivered(
                providerName,
                envelope.EnvelopeId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["opentelemetry.activity_source"] = OpenTelemetryGovernanceInstrumentation.ActivitySourceName,
                    ["opentelemetry.meter"] = OpenTelemetryGovernanceInstrumentation.MeterName,
                    ["opentelemetry.event_name"] = eventName,
                    ["opentelemetry.emission_latency_ms"] = latencyMs.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            double latencyMs = stopwatch.Elapsed.TotalMilliseconds;
            var error = GovernanceEmissionError.Create(
                "opentelemetry.emission.exception",
                $"OpenTelemetry governance emission failed with {ex.GetType().Name}.",
                isRetryable: true,
                providerName: options.ProviderName,
                providerErrorCode: ex.GetType().FullName);

            if (options.EmitMetrics)
            {
                EmitFailureMetrics(envelope, error, latencyMs);
            }

            return GovernanceEmissionResult.RetryableFailure(
                error,
                providerName: options.ProviderName,
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["opentelemetry.activity_source"] = OpenTelemetryGovernanceInstrumentation.ActivitySourceName,
                    ["opentelemetry.meter"] = OpenTelemetryGovernanceInstrumentation.MeterName,
                    ["opentelemetry.emission_latency_ms"] = latencyMs.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                });
        }
    }

    private static void EmitActivity(
        GovernanceEmissionEnvelope envelope,
        string eventName,
        string providerName,
        string defaultActivityName,
        double latencyMs)
    {
        ActivityTagsCollection tags = BuildTags(envelope, providerName, GovernanceEmissionStatus.Delivered.ToString(), latencyMs);
        string activityName = string.IsNullOrWhiteSpace(envelope.OperationName)
            ? defaultActivityName
            : envelope.OperationName;

        using Activity? activity = ActivitySource.StartActivity(activityName, ActivityKind.Internal);

        if (activity is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object?> tag in tags)
        {
            _ = activity.SetTag(tag.Key, tag.Value);
        }

        _ = activity.AddEvent(new ActivityEvent(eventName, envelope.OccurredUtc, tags));
    }

    private static ActivityTagsCollection BuildTags(
        GovernanceEmissionEnvelope envelope,
        string providerName,
        string result,
        double latencyMs)
    {
        ActivityTagsCollection tags = [];

        AddTag(tags, OpenTelemetryGovernanceAttributes.EnvelopeId, envelope.EnvelopeId);
        AddTag(tags, OpenTelemetryGovernanceAttributes.SchemaVersion, envelope.SchemaVersion);
        AddTag(tags, OpenTelemetryGovernanceAttributes.EventType, envelope.EventType.ToString());
        AddTag(tags, OpenTelemetryGovernanceAttributes.EventId, envelope.EventId);
        AddTag(tags, OpenTelemetryGovernanceAttributes.CorrelationId, envelope.CorrelationId);
        AddTag(tags, OpenTelemetryGovernanceAttributes.AuditResidueId, envelope.AuditResidueId);
        AddTag(tags, OpenTelemetryGovernanceAttributes.TraceId, envelope.TraceId);
        AddTag(tags, OpenTelemetryGovernanceAttributes.SpanId, envelope.SpanId);
        AddTag(tags, OpenTelemetryGovernanceAttributes.ParentSpanId, envelope.ParentSpanId);
        AddTag(tags, OpenTelemetryGovernanceAttributes.DecisionOutcome, envelope.Outcome);
        AddTag(tags, OpenTelemetryGovernanceAttributes.DecisionStage, envelope.DecisionStage);
        AddTag(tags, OpenTelemetryGovernanceAttributes.PolicyVersion, envelope.PolicyVersion);
        AddTag(tags, OpenTelemetryGovernanceAttributes.PolicyHash, envelope.PolicyHash);
        AddTag(tags, OpenTelemetryGovernanceAttributes.LifecycleStage, envelope.LifecycleStage?.ToString());
        AddTag(tags, OpenTelemetryGovernanceAttributes.LifecycleStageSequence, envelope.LifecycleStageSequence);
        AddTag(tags, OpenTelemetryGovernanceAttributes.GatewayExecutionId, envelope.GatewayExecutionId);
        AddTag(tags, OpenTelemetryGovernanceAttributes.OutboxSequence, envelope.OutboxSequence);
        AddTag(tags, OpenTelemetryGovernanceAttributes.EmitterProvider, providerName);
        AddTag(tags, OpenTelemetryGovernanceAttributes.EmitterStatus, envelope.EmitterStatus);
        AddTag(tags, OpenTelemetryGovernanceAttributes.EmitterResult, result);
        AddTag(tags, OpenTelemetryGovernanceAttributes.EmissionLatencyMs, latencyMs);

        if (envelope.Payload is not null)
        {
            AddTag(tags, OpenTelemetryGovernanceAttributes.PayloadType, envelope.Payload.PayloadType);
            AddTag(tags, OpenTelemetryGovernanceAttributes.PayloadSchemaVersion, envelope.Payload.SchemaVersion);
            AddTag(tags, OpenTelemetryGovernanceAttributes.PayloadContentType, envelope.Payload.ContentType);
            AddTag(tags, OpenTelemetryGovernanceAttributes.PayloadContentHash, envelope.Payload.ContentHash);
            AddTag(tags, OpenTelemetryGovernanceAttributes.PayloadSizeBytes, envelope.Payload.SizeBytes);
        }

        return tags;
    }

    private static void EmitDeliveredMetrics(
        GovernanceEmissionEnvelope envelope,
        string providerName,
        double latencyMs)
    {
        TagList tags = BuildMetricTags(envelope, providerName, GovernanceEmissionStatus.Delivered.ToString());
        EmissionsCounter.Add(1, tags);
        EmissionLatencyHistogram.Record(latencyMs, tags);
    }

    private static void EmitFailureMetrics(
        GovernanceEmissionEnvelope envelope,
        GovernanceEmissionError error,
        double latencyMs)
    {
        TagList tags = BuildMetricTags(envelope, error.ProviderName ?? OpenTelemetryGovernanceInstrumentation.ProviderName, GovernanceEmissionStatus.RetryableFailure.ToString());
        tags.Add(OpenTelemetryGovernanceAttributes.MetricFailureCode, error.Code);
        tags.Add(OpenTelemetryGovernanceAttributes.MetricRetryable, error.IsRetryable);

        EmissionFailuresCounter.Add(1, tags);
        EmissionLatencyHistogram.Record(latencyMs, tags);
    }

    private static TagList BuildMetricTags(
        GovernanceEmissionEnvelope envelope,
        string providerName,
        string result)
    {
        TagList tags = new()
        {
            { OpenTelemetryGovernanceAttributes.MetricEventType, envelope.EventType.ToString() },
            { OpenTelemetryGovernanceAttributes.MetricResult, result },
            { OpenTelemetryGovernanceAttributes.MetricProvider, providerName }
        };

        return tags;
    }

    private static void AddTag(ActivityTagsCollection tags, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add(key, value);
        }
    }

    private static void AddTag(ActivityTagsCollection tags, string key, long? value)
    {
        if (value.HasValue)
        {
            tags.Add(key, value.Value);
        }
    }

    private static void AddTag(ActivityTagsCollection tags, string key, int? value)
    {
        if (value.HasValue)
        {
            tags.Add(key, value.Value);
        }
    }

    private static void AddTag(ActivityTagsCollection tags, string key, double value)
    {
        tags.Add(key, value);
    }
}
