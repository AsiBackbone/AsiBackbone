using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Configures canonical payload construction and hashing behavior.
/// </summary>
/// <remarks>
/// Metadata is excluded from canonical payloads unless an explicit allow-list is supplied. This prevents host-specific diagnostics from silently changing signing payloads.
/// </remarks>
public sealed class CanonicalPayloadOptions
{
    /// <summary>
    /// Gets the default canonicalization version for deterministic AsiBackbone JSON signing payloads.
    /// </summary>
    public const string DefaultCanonicalizationVersion = "asibackbone.canonical-json.v1";

    /// <summary>
    /// Gets the default hash algorithm used by the provider-neutral payload hasher.
    /// </summary>
    public const string DefaultHashAlgorithm = "SHA-256";

    private static readonly IReadOnlyCollection<string> EmptyMetadataKeyAllowList =
        new ReadOnlyCollection<string>(Array.Empty<string>());

    private CanonicalPayloadOptions(
        string canonicalizationVersion,
        string hashAlgorithm,
        IReadOnlyCollection<string> metadataKeyAllowList)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalizationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);

        CanonicalizationVersion = canonicalizationVersion.Trim();
        HashAlgorithm = CanonicalPayloadHash.NormalizeHashAlgorithm(hashAlgorithm);
        MetadataKeyAllowList = metadataKeyAllowList;
    }

    /// <summary>
    /// Gets default canonical payload options.
    /// </summary>
    public static CanonicalPayloadOptions Default { get; } = Create();

    /// <summary>
    /// Gets the canonicalization version stamped onto payloads.
    /// </summary>
    public string CanonicalizationVersion { get; }

    /// <summary>
    /// Gets the hash algorithm descriptor used by <see cref="CanonicalPayloadHasher" />.
    /// </summary>
    public string HashAlgorithm { get; }

    /// <summary>
    /// Gets the metadata keys that may be included in canonical payloads.
    /// </summary>
    public IReadOnlyCollection<string> MetadataKeyAllowList { get; }

    /// <summary>
    /// Creates canonical payload options.
    /// </summary>
    public static CanonicalPayloadOptions Create(
        IEnumerable<string>? metadataKeyAllowList = null,
        string? canonicalizationVersion = null,
        string? hashAlgorithm = null)
    {
        return new CanonicalPayloadOptions(
            string.IsNullOrWhiteSpace(canonicalizationVersion)
                ? DefaultCanonicalizationVersion
                : canonicalizationVersion,
            string.IsNullOrWhiteSpace(hashAlgorithm)
                ? DefaultHashAlgorithm
                : hashAlgorithm,
            NormalizeMetadataKeyAllowList(metadataKeyAllowList));
    }

    /// <summary>
    /// Determines whether the supplied metadata key may be included in a canonical payload.
    /// </summary>
    public bool AllowsMetadataKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key)
            && MetadataKeyAllowList.Contains(key.Trim(), StringComparer.Ordinal);
    }

    private static IReadOnlyCollection<string> NormalizeMetadataKeyAllowList(IEnumerable<string>? metadataKeyAllowList)
    {
        if (metadataKeyAllowList is null)
        {
            return EmptyMetadataKeyAllowList;
        }

        string[] normalizedKeys = [.. metadataKeyAllowList
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)];

        return normalizedKeys.Length == 0
            ? EmptyMetadataKeyAllowList
            : new ReadOnlyCollection<string>(normalizedKeys);
    }
}
