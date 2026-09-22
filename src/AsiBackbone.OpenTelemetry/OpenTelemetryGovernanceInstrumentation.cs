namespace AsiBackbone.OpenTelemetry;

/// <summary>
/// Provides stable OpenTelemetry source, meter, provider, event, metric, and attribute names for AsiBackbone governance emission.
/// </summary>
public static class OpenTelemetryGovernanceInstrumentation
{
    /// <summary>
    /// Gets the ActivitySource name used by the OpenTelemetry governance emitter.
    /// </summary>
    public const string ActivitySourceName = "AsiBackbone.OpenTelemetry";

    /// <summary>
    /// Gets the Meter name used by the OpenTelemetry governance emitter.
    /// </summary>
    public const string MeterName = "AsiBackbone.OpenTelemetry";

    /// <summary>
    /// Gets the provider name returned in provider-neutral emission results.
    /// </summary>
    public const string ProviderName = "open-telemetry";

    /// <summary>
    /// Gets the governance emissions counter name.
    /// </summary>
    public const string EmissionsCounterName = "asibackbone.governance.emissions";

    /// <summary>
    /// Gets the governance emission failures counter name.
    /// </summary>
    public const string EmissionFailuresCounterName = "asibackbone.governance.emission_failures";

    /// <summary>
    /// Gets the governance emission latency histogram name.
    /// </summary>
    public const string EmissionLatencyHistogramName = "asibackbone.governance.emission_latency_ms";

    /// <summary>
    /// Gets the default activity operation name used when the envelope does not provide one.
    /// </summary>
    public const string DefaultActivityName = "asibackbone.governance.emit";

    /// <summary>
    /// Gets the default generic governance event name.
    /// </summary>
    public const string GenericGovernanceEventName = "asibackbone.governance.event";

    /// <summary>
    /// Gets the decision evaluated event name.
    /// </summary>
    public const string DecisionEvaluatedEventName = "asibackbone.decision.evaluated";

    /// <summary>
    /// Gets the acknowledgment recorded event name.
    /// </summary>
    public const string AcknowledgmentRecordedEventName = "asibackbone.acknowledgment.recorded";

    /// <summary>
    /// Gets the capability-token issued event name.
    /// </summary>
    public const string CapabilityTokenIssuedEventName = "asibackbone.capability_token.issued";

    /// <summary>
    /// Gets the gateway completed event name.
    /// </summary>
    public const string GatewayCompletedEventName = "asibackbone.gateway.completed";

    /// <summary>
    /// Gets the audit-residue created event name.
    /// </summary>
    public const string DecisionReceiptCreatedEventName = "asibackbone.audit_residue.created";

    /// <summary>
    /// Gets the audit lifecycle recorded event name.
    /// </summary>
    public const string LifecycleRecordedEventName = "asibackbone.lifecycle.recorded";

    /// <summary>
    /// Gets the outbox updated event name.
    /// </summary>
    public const string OutboxUpdatedEventName = "asibackbone.outbox.updated";

    /// <summary>
    /// Gets the provider-emission delivered event name.
    /// </summary>
    public const string EmissionDeliveredEventName = "asibackbone.emission.delivered";

    /// <summary>
    /// Gets the provider-emission failed event name.
    /// </summary>
    public const string EmissionFailedEventName = "asibackbone.emission.failed";
}
