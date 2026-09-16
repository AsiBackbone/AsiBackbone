using System.Collections.ObjectModel;
using AsiBackbone.Core.Serialization;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.Integrity;

/// <summary>
/// Represents one append-only hash-chain link for a canonical audit or outbox record hash.
/// </summary>
/// <remarks>
/// A link proves only local sequence continuity when later verified. It does not make storage immutable,
/// externally anchored, or legally non-repudiable by itself.
/// </remarks>
public sealed class AuditIntegrityLink
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    private AuditIntegrityLink(
        string chainId,
        long sequence,
        string recordId,
        string recordType,
        string recordHash,
        string previousLinkHash,
        string linkHash,
        string hashAlgorithm,
        string canonicalizationVersion,
        string schemaVersion,
        DateTimeOffset createdUtc,
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chainId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordType);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(linkHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalizationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Chain sequence must be greater than zero.");
        }

        ChainId = chainId.Trim();
        Sequence = sequence;
        RecordId = recordId.Trim();
        RecordType = recordType.Trim();
        RecordHash = recordHash.Trim().ToLowerInvariant();
        PreviousLinkHash = string.IsNullOrWhiteSpace(previousLinkHash) ? string.Empty : previousLinkHash.Trim().ToLowerInvariant();
        LinkHash = linkHash.Trim().ToLowerInvariant();
        HashAlgorithm = CanonicalPayloadHash.NormalizeHashAlgorithm(hashAlgorithm);
        CanonicalizationVersion = canonicalizationVersion.Trim();
        SchemaVersion = GovernanceSchemaVersions.Normalize(schemaVersion);
        CreatedUtc = createdUtc.ToUniversalTime();
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the logical chain identifier.
    /// </summary>
    public string ChainId { get; }

    /// <summary>
    /// Gets the one-based sequence number within the chain.
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// Gets the record identifier bound into this link.
    /// </summary>
    public string RecordId { get; }

    /// <summary>
    /// Gets the canonical record artifact type bound into this link.
    /// </summary>
    public string RecordType { get; }

    /// <summary>
    /// Gets the canonical record hash bound into this link.
    /// </summary>
    public string RecordHash { get; }

    /// <summary>
    /// Gets the previous link hash. Genesis links use an empty value.
    /// </summary>
    public string PreviousLinkHash { get; }

    /// <summary>
    /// Gets this link's canonical hash.
    /// </summary>
    public string LinkHash { get; }

    /// <summary>
    /// Gets the hash algorithm used for record and link hashes.
    /// </summary>
    public string HashAlgorithm { get; }

    /// <summary>
    /// Gets the canonicalization version used for link hashing.
    /// </summary>
    public string CanonicalizationVersion { get; }

    /// <summary>
    /// Gets the schema version for the integrity link.
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    /// Gets the UTC timestamp when this link was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    /// Gets provider-neutral metadata associated with the link.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether this is the first link in the chain.
    /// </summary>
    public bool IsGenesis => Sequence == 1 && PreviousLinkHash.Length == 0;

    /// <summary>
    /// Creates the first link in a chain.
    /// </summary>
    public static AuditIntegrityLink CreateGenesis(
        string chainId,
        CanonicalPayloadHash recordHash,
        DateTimeOffset createdUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateNext(chainId, 1, recordHash, string.Empty, createdUtc, metadata);
    }

    /// <summary>
    /// Appends a link after the supplied previous link.
    /// </summary>
    public static AuditIntegrityLink Append(
        AuditIntegrityLink previousLink,
        CanonicalPayloadHash recordHash,
        DateTimeOffset createdUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(previousLink);

        return CreateNext(
            previousLink.ChainId,
            previousLink.Sequence + 1,
            recordHash,
            previousLink.LinkHash,
            createdUtc,
            metadata);
    }

    /// <summary>
    /// Rehydrates a persisted link without recomputing its link hash.
    /// </summary>
    public static AuditIntegrityLink Rehydrate(
        string chainId,
        long sequence,
        string recordId,
        string recordType,
        string recordHash,
        string previousLinkHash,
        string linkHash,
        string hashAlgorithm,
        string canonicalizationVersion,
        string schemaVersion,
        DateTimeOffset createdUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new AuditIntegrityLink(
            chainId,
            sequence,
            recordId,
            recordType,
            recordHash,
            previousLinkHash,
            linkHash,
            hashAlgorithm,
            canonicalizationVersion,
            schemaVersion,
            createdUtc,
            NormalizeMetadata(metadata));
    }

    /// <summary>
    /// Recomputes the link hash from the persisted link fields.
    /// </summary>
    public string ComputeExpectedLinkHash()
    {
        return CanonicalPayloadHasher.ComputeHash(BuildCanonicalPayload()).HashValue;
    }

    internal CanonicalPayload BuildCanonicalPayload()
    {
        SortedDictionary<string, object?> content = new(StringComparer.Ordinal)
        {
            ["chainId"] = ChainId,
            ["createdUtc"] = CreatedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture),
            ["hashAlgorithm"] = HashAlgorithm,
            ["previousLinkHash"] = PreviousLinkHash,
            ["recordHash"] = RecordHash,
            ["recordId"] = RecordId,
            ["recordType"] = RecordType,
            ["sequence"] = Sequence
        };

        return CanonicalPayload.Create(
            CanonicalArtifactTypes.AuditIntegrityLink,
            $"{ChainId}:{Sequence}",
            SchemaVersion,
            CanonicalizationVersion,
            content);
    }

    private static AuditIntegrityLink CreateNext(
        string chainId,
        long sequence,
        CanonicalPayloadHash recordHash,
        string previousLinkHash,
        DateTimeOffset createdUtc,
        IReadOnlyDictionary<string, string>? metadata)
    {
        ArgumentNullException.ThrowIfNull(recordHash);

        string schemaVersion = GovernanceSchemaVersions.StableArtifactsV1;
        string canonicalizationVersion = recordHash.CanonicalizationVersion;
        var draft = new AuditIntegrityLink(
            chainId,
            sequence,
            recordHash.ArtifactId,
            recordHash.ArtifactType,
            recordHash.HashValue,
            previousLinkHash,
            recordHash.HashValue,
            recordHash.HashAlgorithm,
            canonicalizationVersion,
            schemaVersion,
            createdUtc,
            NormalizeMetadata(metadata));

        string linkHash = CanonicalPayloadHasher.ComputeHash(draft.BuildCanonicalPayload()).HashValue;

        return new AuditIntegrityLink(
            draft.ChainId,
            draft.Sequence,
            draft.RecordId,
            draft.RecordType,
            draft.RecordHash,
            draft.PreviousLinkHash,
            linkHash,
            draft.HashAlgorithm,
            draft.CanonicalizationVersion,
            draft.SchemaVersion,
            draft.CreatedUtc,
            draft.Metadata);
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
            {
                normalized[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
            }
        }

        return normalized.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalized);
    }
}
