using System.Security.Cryptography;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Represents provider-neutral hash metadata for a canonical AsiBackbone payload.
/// </summary>
public sealed class CanonicalPayloadHash
{
    private CanonicalPayloadHash(
        string artifactType,
        string artifactId,
        string payloadSchemaVersion,
        string canonicalizationVersion,
        string hashAlgorithm,
        string hashValue)
    {
        ArtifactType = artifactType;
        ArtifactId = artifactId;
        PayloadSchemaVersion = payloadSchemaVersion;
        CanonicalizationVersion = canonicalizationVersion;
        HashAlgorithm = hashAlgorithm;
        HashValue = hashValue;
    }

    /// <summary>
    /// Gets the artifact type bound into the hashed payload.
    /// </summary>
    public string ArtifactType { get; }

    /// <summary>
    /// Gets the artifact identifier bound into the hashed payload.
    /// </summary>
    public string ArtifactId { get; }

    /// <summary>
    /// Gets the payload schema version bound into the hashed payload.
    /// </summary>
    public string PayloadSchemaVersion { get; }

    /// <summary>
    /// Gets the canonicalization version used before hashing.
    /// </summary>
    public string CanonicalizationVersion { get; }

    /// <summary>
    /// Gets the hash algorithm descriptor.
    /// </summary>
    public string HashAlgorithm { get; }

    /// <summary>
    /// Gets the lowercase hexadecimal hash value.
    /// </summary>
    public string HashValue { get; }

    /// <summary>
    /// Creates provider-neutral hash metadata.
    /// </summary>
    public static CanonicalPayloadHash Create(
        string artifactType,
        string artifactId,
        string payloadSchemaVersion,
        string canonicalizationVersion,
        string hashAlgorithm,
        string hashValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactType);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalizationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashValue);

        return new CanonicalPayloadHash(
            artifactType.Trim(),
            artifactId.Trim(),
            payloadSchemaVersion.Trim(),
            canonicalizationVersion.Trim(),
            NormalizeHashAlgorithm(hashAlgorithm),
            hashValue.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Creates signing metadata that carries the hash and canonical payload descriptors without implying that the artifact is signed.
    /// </summary>
    public SigningMetadata ToSigningMetadata(IReadOnlyDictionary<string, string>? metadata = null)
    {
        Dictionary<string, string> hashMetadata = new(StringComparer.Ordinal)
        {
            ["artifact_id"] = ArtifactId,
            ["artifact_type"] = ArtifactType,
            ["canonicalization_version"] = CanonicalizationVersion,
            ["payload_schema_version"] = PayloadSchemaVersion
        };

        if (metadata is not null)
        {
            foreach (KeyValuePair<string, string> item in metadata)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                hashMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
            }
        }

        return SigningMetadata.Create(
            signingHash: HashValue,
            hashAlgorithm: HashAlgorithm,
            metadata: hashMetadata);
    }

    internal static string NormalizeHashAlgorithm(string hashAlgorithm)
    {
        string normalized = hashAlgorithm.Trim();

        return normalized.ToUpperInvariant() switch
        {
            "SHA256" or "SHA-256" => CanonicalPayloadOptions.DefaultHashAlgorithm,
            "SHA512" or "SHA-512" => "SHA-512",
            _ => normalized
        };
    }
}

/// <summary>
/// Computes provider-neutral hashes for canonical AsiBackbone payloads.
/// </summary>
public static class CanonicalPayloadHasher
{
    /// <summary>
    /// Computes a hash over the canonical payload bytes.
    /// </summary>
    public static CanonicalPayloadHash ComputeHash(
        CanonicalPayload payload,
        string? hashAlgorithm = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string algorithm = CanonicalPayloadHash.NormalizeHashAlgorithm(
            string.IsNullOrWhiteSpace(hashAlgorithm)
                ? payload.HashAlgorithm
                : hashAlgorithm);

        byte[] payloadBytes = payload.ToUtf8Bytes();
        byte[] hashBytes = algorithm switch
        {
            CanonicalPayloadOptions.DefaultHashAlgorithm => SHA256.HashData(payloadBytes),
            "SHA-512" => SHA512.HashData(payloadBytes),
            _ => throw new NotSupportedException($"The built-in canonical payload hasher supports {CanonicalPayloadOptions.DefaultHashAlgorithm} and SHA-512. Host or provider packages may implement additional algorithms separately.")
        };

        string hashValue = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return CanonicalPayloadHash.Create(
            payload.ArtifactType,
            payload.ArtifactId,
            payload.PayloadSchemaVersion,
            payload.CanonicalizationVersion,
            algorithm,
            hashValue);
    }
}
