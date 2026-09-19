using AsiBackbone.Core.Entities;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>
/// Represents a normalized metadata row associated with an AsiBackbone handshake request.
/// </summary>
public sealed class HandshakeRequestMetadataEntity : GovernanceEntity
{
    /// <summary>
    /// Gets or sets the identifier of the parent handshake request.
    /// </summary>
    public Guid HandshakeRequestId { get; set; }

    /// <summary>
    /// Gets or sets the metadata key.
    /// </summary>
    public string MetadataKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the metadata value.
    /// </summary>
    public string MetadataValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent handshake request.
    /// </summary>
    public HandshakeRequestEntity HandshakeRequest { get; set; } = null!;
}
