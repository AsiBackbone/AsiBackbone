using System.Collections.ObjectModel;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.Core.Emissions;

/// <summary>
/// Represents a provider-neutral governance emission envelope that can be handed to outbox storage or downstream providers.
/// </summary>
/// <remarks>
/// The envelope carries minimized governance context and safe diagnostics. It does not contain provider SDK payloads, cloud-specific routing, raw secrets, raw tokens, prompts, or protected content.
/// </remarks>
public sealed class GovernanceEmissionEnvelope
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private GovernanceEmissionEnvelope(
        string envelopeId,
        string schemaVersion,
        GovernanceEmissionEventType eventType,
        string? eventId,
        DateTimeOffset occurredUtc,
        DateTimeOffset createdUtc,
        string? correlationId,
        string? decisionReceiptId,
        DecisionReceiptLifecycleStage? lifecycleStage,
        string? policyVersion,
        string? policyHash,
        string? traceId,
        string? spanId,
        string? parentSpanId,
        string? operationName,
        string? outcome,
        string? actorId,
        string? emitterStatus,
        string? emitterProvider,
        long? outboxSequence,
        string? gatewayExecutionId,
        string? decisionStage,
        GovernanceEmissionPayload? payload,
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(envelopeId);

        if (!Enum.IsDefined(eventType))
        {
            throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Governance emission event type must be defined.");
        }

        if (lifecycleStage.HasValue && !Enum.IsDefined(lifecycleStage.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleStage), lifecycleStage, "Lifecycle stage must be defined when supplied.");
        }

        EnvelopeId = envelopeId.Trim();
        SchemaVersion = GovernanceSchemaVersions.Normalize(schemaVersion);
        EventType = eventType;
        EventId = NormalizeOptional(eventId);
        OccurredUtc = occurredUtc.ToUniversalTime();
        CreatedUtc = createdUtc.ToUniversalTime();
        CorrelationId = NormalizeOptional(correlationId);
        DecisionReceiptId = NormalizeOptional(decisionReceiptId);
        LifecycleStage = lifecycleStage;
        LifecycleStageSequence = lifecycleStage.HasValue ? (int)lifecycleStage.Value : null;
        PolicyVersion = NormalizeOptional(policyVersion);
        PolicyHash = NormalizeOptional(policyHash);
        TraceId = NormalizeOptional(traceId);
        SpanId = NormalizeOptional(spanId);
        ParentSpanId = NormalizeOptional(parentSpanId);
        OperationName = NormalizeOptional(operationName);
        Outcome = NormalizeOptional(outcome);
        ActorId = NormalizeOptional(actorId);
        EmitterStatus = NormalizeOptional(emitterStatus);
        EmitterProvider = NormalizeOptional(emitterProvider);
        OutboxSequence = NormalizeNonNegative(outboxSequence, nameof(outboxSequence));
        GatewayExecutionId = NormalizeOptional(gatewayExecutionId);
        DecisionStage = NormalizeOptional(decisionStage);
        Payload = payload;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the stable envelope identifier.
    /// </summary>
    public string EnvelopeId { get; }

    /// <summary>
    /// Gets the schema version for this provider-neutral envelope shape.
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    /// Gets the provider-neutral event category.
    /// </summary>
    public GovernanceEmissionEventType EventType { get; }

    /// <summary>
    /// Gets the source governance event identifier, when available.
    /// </summary>
    public string? EventId { get; }

    /// <summary>
    /// Gets the UTC timestamp when the source governance event occurred.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; }

    /// <summary>
    /// Gets the UTC timestamp when this emission envelope was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    /// Gets the correlation identifier that links the emission to the host workflow, when available.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Gets the decision receipt identifier linked to this emission, when available.
    /// </summary>
    public string? DecisionReceiptId { get; }

    /// <summary>
    /// Gets the decision receipt lifecycle stage linked to this emission, when available.
    /// </summary>
    public DecisionReceiptLifecycleStage? LifecycleStage { get; }

    /// <summary>
    /// Gets the stable lifecycle stage sequence value, when a lifecycle stage is supplied.
    /// </summary>
    public int? LifecycleStageSequence { get; }

    /// <summary>
    /// Gets the policy version associated with the source governance event, when available.
    /// </summary>
    public string? PolicyVersion { get; }

    /// <summary>
    /// Gets the policy hash associated with the source governance event, when available.
    /// </summary>
    public string? PolicyHash { get; }

    /// <summary>
    /// Gets the trace identifier associated with the source governance event, when available.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// Gets the span identifier associated with the source governance event, when available.
    /// </summary>
    public string? SpanId { get; }

    /// <summary>
    /// Gets the parent span identifier associated with the source governance event, when available.
    /// </summary>
    public string? ParentSpanId { get; }

    /// <summary>
    /// Gets the operation name associated with the source governance event, when available.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    /// Gets the outcome associated with the source governance event, when available.
    /// </summary>
    public string? Outcome { get; }

    /// <summary>
    /// Gets the actor identifier associated with the source governance event, when available.
    /// </summary>
    public string? ActorId { get; }

    /// <summary>
    /// Gets the provider-neutral emitter status, when available.
    /// </summary>
    public string? EmitterStatus { get; }

    /// <summary>
    /// Gets the provider-neutral emitter provider name, when available.
    /// </summary>
    public string? EmitterProvider { get; }

    /// <summary>
    /// Gets the outbox sequence associated with the emission, when available.
    /// </summary>
    public long? OutboxSequence { get; }

    /// <summary>
    /// Gets the gateway execution identifier associated with the emission, when available.
    /// </summary>
    public string? GatewayExecutionId { get; }

    /// <summary>
    /// Gets the provider-neutral decision stage associated with the emission, when available.
    /// </summary>
    public string? DecisionStage { get; }

    /// <summary>
    /// Gets the minimized provider-neutral payload descriptor, when available.
    /// </summary>
    public GovernanceEmissionPayload? Payload { get; }

    /// <summary>
    /// Gets minimized provider-neutral metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether correlation metadata is available.
    /// </summary>
    public bool HasCorrelation => CorrelationId is not null || TraceId is not null || DecisionReceiptId is not null;

    /// <summary>
    /// Gets a value indicating whether envelope metadata is present.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Creates a provider-neutral governance emission envelope.
    /// </summary>
    public static GovernanceEmissionEnvelope Create(
        GovernanceEmissionEventType eventType,
        string? eventId = null,
        DateTimeOffset? occurredUtc = null,
        string? envelopeId = null,
        DateTimeOffset? createdUtc = null,
        string? schemaVersion = null,
        string? correlationId = null,
        string? decisionReceiptId = null,
        DecisionReceiptLifecycleStage? lifecycleStage = null,
        string? policyVersion = null,
        string? policyHash = null,
        string? traceId = null,
        string? spanId = null,
        string? parentSpanId = null,
        string? operationName = null,
        string? outcome = null,
        string? actorId = null,
        string? emitterStatus = null,
        string? emitterProvider = null,
        long? outboxSequence = null,
        string? gatewayExecutionId = null,
        string? decisionStage = null,
        GovernanceEmissionPayload? payload = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new GovernanceEmissionEnvelope(
            NormalizeIdentifier(envelopeId),
            schemaVersion ?? GovernanceSchemaVersions.StableArtifactsV1,
            eventType,
            eventId,
            occurredUtc ?? DateTimeOffset.UtcNow,
            createdUtc ?? DateTimeOffset.UtcNow,
            correlationId,
            decisionReceiptId,
            lifecycleStage,
            policyVersion,
            policyHash,
            traceId,
            spanId,
            parentSpanId,
            operationName,
            outcome,
            actorId,
            emitterStatus,
            emitterProvider,
            outboxSequence,
            gatewayExecutionId,
            decisionStage,
            payload,
            NormalizeMetadata(metadata));
    }

    /// <summary>
    /// Creates a provider-neutral governance emission envelope from a decision receipt.
    /// </summary>
    public static GovernanceEmissionEnvelope FromDecisionReceipt(
        IDecisionReceipt receipt,
        GovernanceEmissionEventType eventType = GovernanceEmissionEventType.DecisionReceipt,
        string? envelopeId = null,
        DateTimeOffset? createdUtc = null,
        GovernanceEmissionPayload? payload = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return new GovernanceEmissionEnvelope(
            NormalizeIdentifier(envelopeId),
            receipt.SchemaVersion,
            eventType,
            receipt.EventId,
            receipt.OccurredUtc,
            createdUtc ?? DateTimeOffset.UtcNow,
            receipt.CorrelationId,
            receipt.DecisionReceiptId,
            null,
            receipt.PolicyVersion,
            receipt.PolicyHash,
            receipt.TraceId,
            receipt.SpanId,
            receipt.ParentSpanId,
            receipt.OperationName,
            receipt.Outcome,
            receipt.ActorId,
            receipt.EmitterStatus,
            receipt.EmitterProvider,
            receipt.OutboxSequence,
            receipt.GatewayExecutionId,
            receipt.DecisionStage,
            payload,
            NormalizeMetadata(receipt.Metadata, metadata));
    }

    /// <summary>
    /// Creates a provider-neutral governance emission envelope from an decision receipt lifecycle event.
    /// </summary>
    public static GovernanceEmissionEnvelope FromLifecycleEvent(
        DecisionReceiptLifecycleEvent lifecycleEvent,
        string? envelopeId = null,
        DateTimeOffset? createdUtc = null,
        GovernanceEmissionPayload? payload = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        return new GovernanceEmissionEnvelope(
            NormalizeIdentifier(envelopeId),
            GovernanceSchemaVersions.StableArtifactsV1,
            GovernanceEmissionEventType.AuditLifecycle,
            lifecycleEvent.EventId,
            lifecycleEvent.OccurredUtc,
            createdUtc ?? DateTimeOffset.UtcNow,
            lifecycleEvent.CorrelationId,
            lifecycleEvent.DecisionReceiptId,
            lifecycleEvent.Stage,
            null,
            null,
            lifecycleEvent.TraceId,
            null,
            null,
            lifecycleEvent.OperationName,
            lifecycleEvent.Outcome,
            null,
            null,
            null,
            null,
            null,
            lifecycleEvent.Stage.ToString(),
            payload,
            NormalizeMetadata(lifecycleEvent.Metadata, metadata));
    }

    private static string NormalizeIdentifier(string? identifier)
    {
        return string.IsNullOrWhiteSpace(identifier)
            ? Guid.NewGuid().ToString("N")
            : identifier.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static long? NormalizeNonNegative(long? value, string parameterName)
    {
        return value < 0
            ? throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than or equal to zero.")
            : value;
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        params IReadOnlyDictionary<string, string>?[] metadataSets)
    {
        Dictionary<string, string> normalizedMetadata = new(StringComparer.Ordinal);

        foreach (IReadOnlyDictionary<string, string>? metadata in metadataSets)
        {
            if (metadata is null || metadata.Count == 0)
            {
                continue;
            }

            foreach (KeyValuePair<string, string> item in metadata)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                normalizedMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
            }
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }
}
