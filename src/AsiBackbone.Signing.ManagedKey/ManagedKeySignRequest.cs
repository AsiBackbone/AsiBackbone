using System.Collections.ObjectModel;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Signing.ManagedKey;

/// <summary>
/// Represents a managed-key client request to sign a precomputed governance artifact hash.
/// </summary>
public sealed class ManagedKeySignRequest
{
    private readonly byte[]? signatureInput;

    private static readonly ReadOnlyDictionary<string, string> EmptyMetadata =
        new(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedKeySignRequest" /> class.
    /// </summary>
    public ManagedKeySignRequest(
        string signingHash,
        string hashAlgorithm,
        string signatureAlgorithm,
        string keyId,
        string? keyVersion = null,
        string? purpose = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        SigningHash = signingHash.Trim();
        HashAlgorithm = NormalizeRequired(hashAlgorithm);
        SignatureAlgorithm = NormalizeRequired(signatureAlgorithm);
        KeyId = NormalizeRequired(keyId);
        KeyVersion = NormalizeOptional(keyVersion);
        Purpose = NormalizeOptional(purpose);
        Metadata = NormalizeMetadata(metadata);
    }

    /// <summary>
    /// Gets the precomputed hash to sign.
    /// </summary>
    public string SigningHash { get; }

    /// <summary>
    /// Gets the hash algorithm descriptor associated with <see cref="SigningHash" />.
    /// </summary>
    public string HashAlgorithm { get; }

    /// <summary>
    /// Gets the requested provider-neutral signature algorithm descriptor.
    /// </summary>
    public string SignatureAlgorithm { get; }

    /// <summary>
    /// Gets the managed key identifier or key URI reference.
    /// </summary>
    public string KeyId { get; }

    /// <summary>
    /// Gets the managed key version, when supplied.
    /// </summary>
    public string? KeyVersion { get; }

    /// <summary>
    /// Gets the host-defined signing purpose, when supplied.
    /// </summary>
    public string? Purpose { get; }

    /// <summary>
    /// Gets provider-neutral request metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets the exact bytes the managed key must sign.
    /// </summary>
    /// <remarks>
    /// <see cref="ManagedKeySigningService" /> copies <see cref="SigningRequest.SignatureInput" /> here. For artifacts
    /// signed through <see cref="GovernanceArtifactSigner" /> this is the version 1 input that binds the canonical
    /// descriptors, hash, and signing policy context. Clients must sign these bytes, not <see cref="SigningHash" />: pass
    /// them as the message to a message-signing API, or hash them with the key's digest algorithm before calling a
    /// digest-signing API. A client that signs the hash text produces signatures that fail version 1 verification. When no
    /// input was supplied, this returns the pre-6.0 hash-only input. The supplied value is copied.
    /// </remarks>
    public ReadOnlyMemory<byte> SignatureInput
    {
        get => signatureInput ?? GovernanceSignatureInput.CreateLegacy(SigningHash);
        init => signatureInput = value.IsEmpty ? null : [.. value.Span];
    }

    private static string NormalizeRequired(string value)
    {
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ReadOnlyDictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
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

        return normalized.Count == 0 ? EmptyMetadata : new ReadOnlyDictionary<string, string>(normalized);
    }
}
