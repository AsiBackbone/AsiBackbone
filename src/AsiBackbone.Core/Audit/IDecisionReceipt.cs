using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.Core.Audit;

/// <summary>
/// Defines the framework-neutral decision receipt produced by an AsiBackbone operation.
/// </summary>
public interface IDecisionReceipt
{
    /// <summary>
    /// Gets the stable audit event identifier.
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// Gets the stable decision receipt identifier when available.
    /// </summary>
    /// <remarks>
    /// Existing residue implementations may use <see cref="EventId" /> as the residue identifier.
    /// </remarks>
    string? DecisionReceiptId => EventId;

    /// <summary>
    /// Gets the serialized schema version for the decision receipt shape.
    /// </summary>
    string SchemaVersion => GovernanceSchemaVersions.StableArtifactsV1;

    /// <summary>
    /// Gets the UTC timestamp when the audited event occurred.
    /// </summary>
    DateTimeOffset OccurredUtc { get; }

    /// <summary>
    /// Gets the stable actor identifier associated with the event.
    /// </summary>
    string ActorId { get; }

    /// <summary>
    /// Gets the actor type associated with the event.
    /// </summary>
    GovernanceActorType ActorType { get; }

    /// <summary>
    /// Gets the optional display name or label associated with the actor.
    /// </summary>
    string? ActorDisplayName { get; }

    /// <summary>
    /// Gets the operation name associated with the audited event.
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// Gets the governance, constraint, or host-defined outcome associated with the event.
    /// </summary>
    string Outcome { get; }

    /// <summary>
    /// Gets machine-readable reason codes associated with the event.
    /// </summary>
    IReadOnlyList<string> ReasonCodes { get; }

    /// <summary>
    /// Gets the correlation identifier associated with the event, when supplied by the host.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the trace identifier associated with the event, when supplied by the host.
    /// </summary>
    string? TraceId { get; }

    /// <summary>
    /// Gets the span identifier associated with the event, when supplied by the host or observability adapter.
    /// </summary>
    string? SpanId => null;

    /// <summary>
    /// Gets the parent span identifier associated with the event, when supplied by the host or observability adapter.
    /// </summary>
    string? ParentSpanId => null;

    /// <summary>
    /// Gets the decision latency in milliseconds, when supplied by the host.
    /// </summary>
    long? DecisionLatencyMs => null;

    /// <summary>
    /// Gets the hash of the evaluated constraint set, when supplied by the host.
    /// </summary>
    string? ConstraintSetHash => null;

    /// <summary>
    /// Gets the number of constraints evaluated for the decision, when supplied by the host.
    /// </summary>
    int? ConstraintCount => null;

    /// <summary>
    /// Gets the host-defined risk score associated with the decision, when supplied by the host.
    /// </summary>
    double? RiskScore => null;

    /// <summary>
    /// Gets the policy scope associated with the decision, when supplied by the host.
    /// </summary>
    string? PolicyScope => null;

    /// <summary>
    /// Gets the privacy-preserving tenant hash associated with the decision, when supplied by the host.
    /// </summary>
    string? TenantHash => null;

    /// <summary>
    /// Gets the privacy-preserving organization hash associated with the decision, when supplied by the host.
    /// </summary>
    string? OrganizationHash => null;

    /// <summary>
    /// Gets the provider-neutral emitter status, when supplied by the host or outbox provider.
    /// </summary>
    string? EmitterStatus => null;

    /// <summary>
    /// Gets the provider-neutral emitter provider name, when supplied by the host or outbox provider.
    /// </summary>
    string? EmitterProvider => null;

    /// <summary>
    /// Gets the outbox sequence associated with the event, when supplied by the host or outbox provider.
    /// </summary>
    long? OutboxSequence => null;

    /// <summary>
    /// Gets the gateway execution identifier associated with the event, when supplied by the host or gateway provider.
    /// </summary>
    string? GatewayExecutionId => null;

    /// <summary>
    /// Gets the provider-neutral decision stage associated with the event, when supplied by the host.
    /// </summary>
    string? DecisionStage => null;

    /// <summary>
    /// Gets the policy version associated with the event, when supplied by the host.
    /// </summary>
    string? PolicyVersion { get; }

    /// <summary>
    /// Gets the policy hash associated with the event, when supplied by the host.
    /// </summary>
    string? PolicyHash { get; }

    /// <summary>
    /// Gets additional framework-neutral audit metadata supplied by the host.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}
