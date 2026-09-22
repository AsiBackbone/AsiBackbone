using AsiBackbone.Core.Entities;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>
/// Represents a normalized metadata row associated with an AsiBackbone handshake acknowledgment.
/// </summary>
public sealed class AcknowledgmentResponseMetadataEntity : GovernanceEntity
{
    /// <summary>
    /// Gets or sets the identifier of the parent handshake acknowledgment.
    /// </summary>
    public Guid AcknowledgmentResponseId { get; set; }

    /// <summary>
    /// Gets or sets the metadata key.
    /// </summary>
    public string MetadataKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the metadata value.
    /// </summary>
    public string MetadataValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent handshake acknowledgment.
    /// </summary>
    public AcknowledgmentResponseEntity AcknowledgmentResponse { get; set; } = null!;
}
