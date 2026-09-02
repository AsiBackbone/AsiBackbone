using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Entities;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>
/// Represents the Entity Framework Core persistence shape for a durable governance outbox entry.
/// </summary>
public sealed class AsiBackboneGovernanceOutboxEntryEntity : AsiBackboneEntity
{
    /// <summary>
    /// Gets or sets the stable outbox entry identifier.
    /// </summary>
    public string OutboxEntryId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-neutral outbox status.
    /// </summary>
    public GovernanceEmissionStatus Status { get; set; } = GovernanceEmissionStatus.Pending;

    /// <summary>
    /// Gets or sets the UTC timestamp when the entry was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the entry was last updated.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when delivery completed, when applicable.
    /// </summary>
    public DateTimeOffset? DeliveredUtc { get; set; }

    /// <summary>
    /// Gets or sets the failed or deferred attempt count.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the legacy maximum retry count retained for persisted-schema compatibility.
    /// </summary>
    public int MaxRetryCount { get; set; }

    /// <summary>
    /// Gets or sets the next UTC retry timestamp, when retry scheduling is active.
    /// </summary>
    public DateTimeOffset? NextRetryUtc { get; set; }

    /// <summary>
    /// Gets or sets the provider name associated with the most recent attempt, when available.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-side record identifier, when delivery returned one and it is safe to store.
    /// </summary>
    public string? ProviderRecordId { get; set; }

    /// <summary>
    /// Gets or sets the terminal dead-letter reason, when available.
    /// </summary>
    public string? DeadLetterReason { get; set; }

    /// <summary>
    /// Gets or sets the last provider-neutral error code, when available.
    /// </summary>
    public string? LastErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the last provider-neutral diagnostic message, when available.
    /// </summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the last error is retryable.
    /// </summary>
    public bool? LastErrorIsRetryable { get; set; }

    /// <summary>
    /// Gets or sets the provider name associated with the last error, when available.
    /// </summary>
    public string? LastErrorProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific last error code, when safe and available.
    /// </summary>
    public string? LastErrorProviderErrorCode { get; set; }

    /// <summary>
    /// Gets or sets serialized framework-neutral outbox metadata.
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the worker, process, node, or partition owner holding the current claim lease.
    /// </summary>
    public string? ClaimOwner { get; set; }

    /// <summary>
    /// Gets or sets the opaque token for the current claim lease.
    /// </summary>
    public string? ClaimToken { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the current claim was acquired.
    /// </summary>
    public DateTimeOffset? ClaimedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the current claim lease expires.
    /// </summary>
    public DateTimeOffset? ClaimExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of claim or reclaim attempts recorded for this entry.
    /// </summary>
    public int ClaimAttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the stable envelope identifier.
    /// </summary>
    public string EnvelopeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the envelope schema version.
    /// </summary>
    public string EnvelopeSchemaVersion { get; set; } = AsiBackboneSchemaVersions.StableArtifactsV1;

    /// <summary>
    /// Gets or sets the provider-neutral envelope event type.
    /// </summary>
    public GovernanceEmissionEventType EnvelopeEventType { get; set; }

    /// <summary>
    /// Gets or sets the source governance event identifier, when available.
    /// </summary>
    public string? EnvelopeEventId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the source governance event occurred.
    /// </summary>
    public DateTimeOffset EnvelopeOccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the envelope was created.
    /// </summary>
    public DateTimeOffset EnvelopeCreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the envelope correlation identifier, when available.
    /// </summary>
    public string? EnvelopeCorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the envelope audit residue identifier, when available.
    /// </summary>
    public string? EnvelopeAuditResidueId { get; set; }

    /// <summary>
    /// Gets or sets the envelope lifecycle stage, when available.
    /// </summary>
    public AuditResidueLifecycleStage? EnvelopeLifecycleStage { get; set; }

    /// <summary>
    /// Gets or sets the stable lifecycle stage sequence value, when available.
    /// </summary>
    public int? EnvelopeLifecycleStageSequence { get; set; }

    /// <summary>
    /// Gets or sets the envelope policy version, when available.
    /// </summary>
    public string? EnvelopePolicyVersion { get; set; }

    /// <summary>
    /// Gets or sets the envelope policy hash, when available.
    /// </summary>
    public string? EnvelopePolicyHash { get; set; }

    /// <summary>
    /// Gets or sets the envelope trace identifier, when available.
    /// </summary>
    public string? EnvelopeTraceId { get; set; }

    /// <summary>
    /// Gets or sets the envelope span identifier, when available.
    /// </summary>
    public string? EnvelopeSpanId { get; set; }

    /// <summary>
    /// Gets or sets the envelope parent span identifier, when available.
    /// </summary>
    public string? EnvelopeParentSpanId { get; set; }

    /// <summary>
    /// Gets or sets the envelope operation name, when available.
    /// </summary>
    public string? EnvelopeOperationName { get; set; }

    /// <summary>
    /// Gets or sets the envelope outcome, when available.
    /// </summary>
    public string? EnvelopeOutcome { get; set; }

    /// <summary>
    /// Gets or sets the envelope actor identifier, when available.
    /// </summary>
    public string? EnvelopeActorId { get; set; }

    /// <summary>
    /// Gets or sets the envelope emitter status, when available.
    /// </summary>
    public string? EnvelopeEmitterStatus { get; set; }

    /// <summary>
    /// Gets or sets the envelope emitter provider, when available.
    /// </summary>
    public string? EnvelopeEmitterProvider { get; set; }

    /// <summary>
    /// Gets or sets the envelope outbox sequence, when available.
    /// </summary>
    public long? EnvelopeOutboxSequence { get; set; }

    /// <summary>
    /// Gets or sets the envelope gateway execution identifier, when available.
    /// </summary>
    public string? EnvelopeGatewayExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the envelope decision stage, when available.
    /// </summary>
    public string? EnvelopeDecisionStage { get; set; }

    /// <summary>
    /// Gets or sets serialized framework-neutral envelope metadata.
    /// </summary>
    public string EnvelopeMetadataJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the minimized payload type, when available.
    /// </summary>
    public string? EnvelopePayloadType { get; set; }

    /// <summary>
    /// Gets or sets the minimized payload schema version, when available.
    /// </summary>
    public string? EnvelopePayloadSchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the minimized payload content type, when available.
    /// </summary>
    public string? EnvelopePayloadContentType { get; set; }

    /// <summary>
    /// Gets or sets the minimized payload content hash, when available.
    /// </summary>
    public string? EnvelopePayloadContentHash { get; set; }

    /// <summary>
    /// Gets or sets the minimized payload size in bytes, when available.
    /// </summary>
    public long? EnvelopePayloadSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets serialized framework-neutral payload metadata.
    /// </summary>
    public string EnvelopePayloadMetadataJson { get; set; } = "{}";
}
