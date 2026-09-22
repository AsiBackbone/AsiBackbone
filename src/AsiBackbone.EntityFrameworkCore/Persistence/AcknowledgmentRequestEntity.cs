using AsiBackbone.Core.Acknowledgments;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Entities;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>
/// Represents the Entity Framework Core persistence shape for an AsiBackbone handshake request.
/// </summary>
public sealed class AcknowledgmentRequestEntity : GovernanceEntity
{
    /// <summary>
    /// Gets or sets the stable handshake identifier.
    /// </summary>
    public string HandshakeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized schema version for this handshake request.
    /// </summary>
    public string SchemaVersion { get; set; } = GovernanceSchemaVersions.StableArtifactsV1;

    /// <summary>
    /// Gets or sets the stable actor identifier associated with the handshake.
    /// </summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actor type associated with the handshake.
    /// </summary>
    public GovernanceActorType ActorType { get; set; }

    /// <summary>
    /// Gets or sets the optional display name or label associated with the actor.
    /// </summary>
    public string? ActorDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the operation name requiring acknowledgment.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine-readable reason code explaining why the handshake is required.
    /// </summary>
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable message explaining why the handshake is required.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the required acknowledgment code the host may display or require before execution.
    /// </summary>
    public string RequiredAcknowledgmentCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the required acknowledgment text the host may display before execution.
    /// </summary>
    public string RequiredAcknowledgmentText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the risk level associated with the handshake.
    /// </summary>
    public AcknowledgmentRiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Gets or sets the optional host-defined risk category associated with the handshake.
    /// </summary>
    public string? RiskCategory { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier associated with the handshake, when supplied by the host.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the trace identifier associated with the handshake, when supplied by the host.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the policy version associated with the handshake, when supplied by the host.
    /// </summary>
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Gets or sets the policy hash associated with the handshake, when supplied by the host.
    /// </summary>
    public string? PolicyHash { get; set; }
}
