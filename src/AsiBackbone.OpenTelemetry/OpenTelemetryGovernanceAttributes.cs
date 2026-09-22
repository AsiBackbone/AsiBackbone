namespace AsiBackbone.OpenTelemetry;

/// <summary>
/// Provides stable attribute names used by the OpenTelemetry governance emitter.
/// </summary>
public static class OpenTelemetryGovernanceAttributes
{
    /// <summary>Envelope correlation identifier.</summary>
    public const string CorrelationId = "asibackbone.correlation_id";

    /// <summary>Decision receipt identifier.</summary>
    public const string DecisionReceiptId = "asibackbone.audit_residue_id";

    /// <summary>Source governance event identifier.</summary>
    public const string EventId = "asibackbone.event_id";

    /// <summary>Emission envelope identifier.</summary>
    public const string EnvelopeId = "asibackbone.envelope_id";

    /// <summary>Provider-neutral schema version.</summary>
    public const string SchemaVersion = "asibackbone.schema_version";

    /// <summary>Trace identifier preserved from the envelope.</summary>
    public const string TraceId = "asibackbone.trace_id";

    /// <summary>Span identifier preserved from the envelope.</summary>
    public const string SpanId = "asibackbone.span_id";

    /// <summary>Parent span identifier preserved from the envelope.</summary>
    public const string ParentSpanId = "asibackbone.parent_span_id";

    /// <summary>Provider-neutral event type.</summary>
    public const string EventType = "asibackbone.event_type";

    /// <summary>Decision outcome.</summary>
    public const string DecisionOutcome = "asibackbone.decision.outcome";

    /// <summary>Decision stage.</summary>
    public const string DecisionStage = "asibackbone.decision.stage";

    /// <summary>Policy version.</summary>
    public const string PolicyVersion = "asibackbone.policy.version";

    /// <summary>Policy hash.</summary>
    public const string PolicyHash = "asibackbone.policy.hash";

    /// <summary>Audit lifecycle stage.</summary>
    public const string LifecycleStage = "asibackbone.lifecycle.stage";

    /// <summary>Audit lifecycle stage sequence.</summary>
    public const string LifecycleStageSequence = "asibackbone.lifecycle.stage_sequence";

    /// <summary>Gateway execution identifier.</summary>
    public const string GatewayExecutionId = "asibackbone.gateway.execution_id";

    /// <summary>Outbox sequence.</summary>
    public const string OutboxSequence = "asibackbone.outbox.sequence";

    /// <summary>Emitter provider name.</summary>
    public const string EmitterProvider = "asibackbone.emitter.provider";

    /// <summary>Emitter status supplied by the source envelope.</summary>
    public const string EmitterStatus = "asibackbone.emitter.status";

    /// <summary>Emitter result status.</summary>
    public const string EmitterResult = "asibackbone.emitter.result";

    /// <summary>Emitter failure code.</summary>
    public const string EmitterFailureCode = "asibackbone.emitter.failure.code";

    /// <summary>Emitter failure retryability flag.</summary>
    public const string EmitterFailureRetryable = "asibackbone.emitter.failure.retryable";

    /// <summary>Safe provider-specific error code.</summary>
    public const string EmitterFailureProviderCode = "asibackbone.emitter.failure.provider_code";

    /// <summary>Emission latency in milliseconds.</summary>
    public const string EmissionLatencyMs = "asibackbone.emission.latency_ms";

    /// <summary>Payload descriptor type.</summary>
    public const string PayloadType = "asibackbone.payload.type";

    /// <summary>Payload descriptor schema version.</summary>
    public const string PayloadSchemaVersion = "asibackbone.payload.schema_version";

    /// <summary>Payload descriptor content type.</summary>
    public const string PayloadContentType = "asibackbone.payload.content_type";

    /// <summary>Payload descriptor content hash.</summary>
    public const string PayloadContentHash = "asibackbone.payload.content_hash";

    /// <summary>Payload descriptor size in bytes.</summary>
    public const string PayloadSizeBytes = "asibackbone.payload.size_bytes";

    /// <summary>Metric dimension for event type.</summary>
    public const string MetricEventType = "event_type";

    /// <summary>Metric dimension for result.</summary>
    public const string MetricResult = "result";

    /// <summary>Metric dimension for provider.</summary>
    public const string MetricProvider = "provider";

    /// <summary>Metric dimension for failure code.</summary>
    public const string MetricFailureCode = "failure_code";

    /// <summary>Metric dimension for retryability.</summary>
    public const string MetricRetryable = "retryable";
}
