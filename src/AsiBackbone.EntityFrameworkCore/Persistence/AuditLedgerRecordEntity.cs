using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Entities;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>
/// Represents the Entity Framework Core persistence shape for an AsiBackbone audit ledger record.
/// </summary>
public sealed class AuditLedgerRecordEntity : GovernanceEntity
{
    /// <summary>
    /// Gets or sets the stable audit ledger record identifier.
    /// </summary>
    public string RecordId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized schema version for this audit ledger record.
    /// </summary>
    public string SchemaVersion { get; set; } = GovernanceSchemaVersions.StableArtifactsV1;

    /// <summary>
    /// Gets or sets the stable audit event identifier.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stable decision receipt identifier.
    /// </summary>
    public string? DecisionReceiptId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the audited event occurred.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the ledger record was created by the host or storage adapter.
    /// </summary>
    public DateTimeOffset RecordedUtc { get; set; }

    /// <summary>
    /// Gets or sets the stable actor identifier associated with the event.
    /// </summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actor type associated with the event.
    /// </summary>
    public GovernanceActorType ActorType { get; set; }

    /// <summary>
    /// Gets or sets the optional display name or label associated with the actor.
    /// </summary>
    public string? ActorDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the operation name associated with the audited event.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the governance, constraint, or host-defined outcome associated with the event.
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized machine-readable reason codes associated with the event.
    /// </summary>
    public string ReasonCodesJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the correlation identifier associated with the event, when supplied by the host.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the trace identifier associated with the event, when supplied by the host.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span identifier associated with the event, when supplied by the host or observability adapter.
    /// </summary>
    public string? SpanId { get; set; }

    /// <summary>
    /// Gets or sets the parent span identifier associated with the event, when supplied by the host or observability adapter.
    /// </summary>
    public string? ParentSpanId { get; set; }

    /// <summary>
    /// Gets or sets the decision latency in milliseconds, when supplied by the host.
    /// </summary>
    public long? DecisionLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the hash of the evaluated constraint set, when supplied by the host.
    /// </summary>
    public string? ConstraintSetHash { get; set; }

    /// <summary>
    /// Gets or sets the number of evaluated constraints, when supplied by the host.
    /// </summary>
    public int? ConstraintCount { get; set; }

    /// <summary>
    /// Gets or sets the host-defined risk score associated with the decision, when supplied by the host.
    /// </summary>
    public double? RiskScore { get; set; }

    /// <summary>
    /// Gets or sets the policy scope associated with the decision, when supplied by the host.
    /// </summary>
    public string? PolicyScope { get; set; }

    /// <summary>
    /// Gets or sets the privacy-preserving tenant hash associated with the decision, when supplied by the host.
    /// </summary>
    public string? TenantHash { get; set; }

    /// <summary>
    /// Gets or sets the privacy-preserving organization hash associated with the decision, when supplied by the host.
    /// </summary>
    public string? OrganizationHash { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral emitter status, when supplied by the host or outbox provider.
    /// </summary>
    public string? EmitterStatus { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral emitter provider name, when supplied by the host or outbox provider.
    /// </summary>
    public string? EmitterProvider { get; set; }

    /// <summary>
    /// Gets or sets the outbox sequence associated with the event, when supplied by the host or outbox provider.
    /// </summary>
    public long? OutboxSequence { get; set; }

    /// <summary>
    /// Gets or sets the gateway execution identifier associated with the event, when supplied by the host or gateway provider.
    /// </summary>
    public string? GatewayExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral decision stage associated with the event, when supplied by the host.
    /// </summary>
    public string? DecisionStage { get; set; }

    /// <summary>
    /// Gets or sets the policy version associated with the event, when supplied by the host.
    /// </summary>
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Gets or sets the policy hash associated with the event, when supplied by the host.
    /// </summary>
    public string? PolicyHash { get; set; }

    /// <summary>
    /// Gets or sets the related responsibility or acknowledgment identifier, when available.
    /// </summary>
    public string? HandshakeId { get; set; }

    /// <summary>
    /// Gets or sets the related acknowledgment identifier, when available.
    /// </summary>
    public string? AcknowledgmentId { get; set; }

    /// <summary>
    /// Gets or sets the related capability grant identifier, when available.
    /// </summary>
    public string? CapabilityTokenId { get; set; }

    /// <summary>
    /// Gets or sets the previous ledger record hash, when supplied by a host or signing package.
    /// </summary>
    public string? PreviousRecordHash { get; set; }

    /// <summary>
    /// Gets or sets this ledger record hash, when supplied by a host or signing package.
    /// </summary>
    public string? RecordHash { get; set; }

    /// <summary>
    /// Gets or sets the hash value that was signed or is intended to be signed, when supplied by a signing package or host.
    /// </summary>
    public string? SigningHash { get; set; }

    /// <summary>
    /// Gets or sets the signature key identifier, when supplied by a signing package or host.
    /// </summary>
    public string? SignatureKeyId { get; set; }

    /// <summary>
    /// Gets or sets the signature key version, when supplied by a signing package or host.
    /// </summary>
    public string? SignatureKeyVersion { get; set; }

    /// <summary>
    /// Gets or sets the signature algorithm, when supplied by a signing package or host.
    /// </summary>
    public string? SignatureAlgorithm { get; set; }

    /// <summary>
    /// Gets or sets the signature value, when supplied by a signing package or host.
    /// </summary>
    public string? SignatureValue { get; set; }

    /// <summary>
    /// Gets or sets the signing provider descriptor, when supplied by a signing package or host.
    /// </summary>
    public string? SignatureProvider { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the signature was produced, when supplied by a signing package or host.
    /// </summary>
    public DateTimeOffset? SignedUtc { get; set; }

    /// <summary>
    /// Gets or sets serialized framework-neutral audit metadata supplied by the host.
    /// </summary>
    public string MetadataJson { get; set; } = "{}";
}
