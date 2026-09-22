using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Entities;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>
/// Represents the Entity Framework Core persistence shape for an AsiBackbone handshake acknowledgment.
/// </summary>
public sealed class AcknowledgmentResponseEntity : GovernanceEntity
{
    /// <summary>
    /// Gets or sets the stable acknowledgment identifier.
    /// </summary>
    public string AcknowledgmentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized schema version for this handshake acknowledgment.
    /// </summary>
    public string SchemaVersion { get; set; } = GovernanceSchemaVersions.StableArtifactsV1;

    /// <summary>
    /// Gets or sets the handshake identifier associated with the acknowledgment.
    /// </summary>
    public string HandshakeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stable actor identifier associated with the acknowledgment.
    /// </summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actor type associated with the acknowledgment.
    /// </summary>
    public GovernanceActorType ActorType { get; set; }

    /// <summary>
    /// Gets or sets the optional display name or label associated with the actor.
    /// </summary>
    public string? ActorDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the acknowledgment code accepted or rejected by the actor.
    /// </summary>
    public string AcknowledgmentCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the required acknowledgment was accepted.
    /// </summary>
    public bool Acknowledged { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the acknowledgment response occurred.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier associated with the acknowledgment, when supplied by the host.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the trace identifier associated with the acknowledgment, when supplied by the host.
    /// </summary>
    public string? TraceId { get; set; }
}
