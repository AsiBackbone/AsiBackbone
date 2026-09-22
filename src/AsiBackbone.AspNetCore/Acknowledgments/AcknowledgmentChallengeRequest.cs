namespace AsiBackbone.AspNetCore.Acknowledgments;

/// <summary>
/// Represents a host-submitted acknowledgment challenge response payload.
/// </summary>
public sealed class AcknowledgmentChallengeRequest
{
    /// <summary>
    /// Gets or sets the handshake identifier being acknowledged or rejected.
    /// </summary>
    public string? HandshakeId { get; set; }

    /// <summary>
    /// Gets or sets the acknowledgment code submitted by the actor.
    /// </summary>
    public string? AcknowledgmentCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the actor accepted the acknowledgment challenge.
    /// </summary>
    public bool Acknowledged { get; set; }

    /// <summary>
    /// Gets or sets optional host-provided response metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}
