using System.Collections.ObjectModel;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.Core.Acknowledgments;

/// <summary>
/// Represents a framework-neutral acknowledgment response for a liability or responsibility handshake.
/// </summary>
public sealed class AcknowledgmentResponse
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private AcknowledgmentResponse(
        string acknowledgmentId,
        string? schemaVersion,
        string handshakeId,
        string actorId,
        GovernanceActorType actorType,
        string? actorDisplayName,
        string acknowledgmentCode,
        bool acknowledged,
        DateTimeOffset occurredUtc,
        string? correlationId,
        string? traceId,
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acknowledgmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handshakeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(acknowledgmentCode);

        AcknowledgmentId = acknowledgmentId.Trim();
        SchemaVersion = GovernanceSchemaVersions.Normalize(schemaVersion);
        HandshakeId = handshakeId.Trim();
        ActorId = actorId.Trim();
        ActorType = actorType;
        ActorDisplayName = NormalizeOptional(actorDisplayName);
        AcknowledgmentCode = acknowledgmentCode.Trim();
        Acknowledged = acknowledged;
        OccurredUtc = occurredUtc.ToUniversalTime();
        CorrelationId = NormalizeOptional(correlationId);
        TraceId = NormalizeOptional(traceId);
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the stable acknowledgment identifier.
    /// </summary>
    public string AcknowledgmentId { get; }

    /// <summary>
    /// Gets the schema version for the serialized handshake acknowledgment shape.
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    /// Gets the handshake identifier associated with the acknowledgment.
    /// </summary>
    public string HandshakeId { get; }

    /// <summary>
    /// Gets the stable actor identifier associated with the acknowledgment.
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// Gets the actor type associated with the acknowledgment.
    /// </summary>
    public GovernanceActorType ActorType { get; }

    /// <summary>
    /// Gets the optional display name or label associated with the actor.
    /// </summary>
    public string? ActorDisplayName { get; }

    /// <summary>
    /// Gets the acknowledgment code accepted or rejected by the actor.
    /// </summary>
    public string AcknowledgmentCode { get; }

    /// <summary>
    /// Gets a value indicating whether the required acknowledgment was accepted.
    /// </summary>
    public bool Acknowledged { get; }

    /// <summary>
    /// Gets a value indicating whether the required acknowledgment was rejected.
    /// </summary>
    public bool Rejected => !Acknowledged;

    /// <summary>
    /// Gets the UTC timestamp when the acknowledgment response occurred.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; }

    /// <summary>
    /// Gets the correlation identifier associated with the acknowledgment, when supplied by the host.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Gets the trace identifier associated with the acknowledgment, when supplied by the host.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// Gets additional framework-neutral acknowledgment metadata supplied by the host.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether this acknowledgment contains metadata.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Creates an acknowledgment response for a liability or responsibility handshake.
    /// </summary>
    /// <param name="request">The handshake request being acknowledged.</param>
    /// <param name="actor">The actor responding to the handshake.</param>
    /// <param name="acknowledged">Whether the actor accepted the required acknowledgment.</param>
    /// <param name="acknowledgmentId">Optional acknowledgment identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional response timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="metadata">Optional host-provided metadata.</param>
    /// <param name="schemaVersion">Optional schema version for serialized or persisted acknowledgment records.</param>
    /// <returns>A acknowledgment acknowledgment response.</returns>
    public static AcknowledgmentResponse Create(
        AcknowledgmentRequest request,
        IGovernanceActorContext actor,
        bool acknowledged,
        string? acknowledgmentId = null,
        DateTimeOffset? occurredUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? schemaVersion = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        return new AcknowledgmentResponse(
            NormalizeIdentifier(acknowledgmentId),
            schemaVersion,
            request.HandshakeId,
            actor.ActorId,
            actor.ActorType,
            actor.DisplayName,
            request.RequiredAcknowledgmentCode,
            acknowledged,
            occurredUtc ?? DateTimeOffset.UtcNow,
            request.CorrelationId,
            request.TraceId,
            NormalizeMetadata(metadata));
    }

    /// <summary>
    /// Creates an accepted acknowledgment response for a liability or responsibility handshake.
    /// </summary>
    /// <param name="request">The handshake request being acknowledged.</param>
    /// <param name="actor">The actor accepting the handshake.</param>
    /// <param name="acknowledgmentId">Optional acknowledgment identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional response timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="metadata">Optional host-provided metadata.</param>
    /// <param name="schemaVersion">Optional schema version for serialized or persisted acknowledgment records.</param>
    /// <returns>An accepted acknowledgment acknowledgment response.</returns>
    public static AcknowledgmentResponse Accept(
        AcknowledgmentRequest request,
        IGovernanceActorContext actor,
        string? acknowledgmentId = null,
        DateTimeOffset? occurredUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? schemaVersion = null)
    {
        return Create(request, actor, acknowledged: true, acknowledgmentId, occurredUtc, metadata, schemaVersion);
    }

    /// <summary>
    /// Creates a rejected acknowledgment response for a liability or responsibility handshake.
    /// </summary>
    /// <param name="request">The handshake request being rejected.</param>
    /// <param name="actor">The actor rejecting the handshake.</param>
    /// <param name="acknowledgmentId">Optional acknowledgment identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional response timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="metadata">Optional host-provided metadata.</param>
    /// <param name="schemaVersion">Optional schema version for serialized or persisted acknowledgment records.</param>
    /// <returns>A rejected acknowledgment acknowledgment response.</returns>
    public static AcknowledgmentResponse Reject(
        AcknowledgmentRequest request,
        IGovernanceActorContext actor,
        string? acknowledgmentId = null,
        DateTimeOffset? occurredUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? schemaVersion = null)
    {
        return Create(request, actor, acknowledged: false, acknowledgmentId, occurredUtc, metadata, schemaVersion);
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

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> normalizedMetadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            normalizedMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }
}
