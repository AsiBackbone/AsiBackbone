using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Represents a provider-neutral request to sign a precomputed artifact hash.
/// </summary>
/// <remarks>
/// The request is intentionally hash-oriented so production providers can use key-based signing APIs without exposing raw signing secrets to Core.
/// </remarks>
public sealed class SigningRequest
{
    private readonly byte[]? signatureInput;

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningRequest" /> class.
    /// </summary>
    public SigningRequest(
        string signingHash,
        string? hashAlgorithm = null,
        string? purpose = null,
        string? keyId = null,
        string? keyVersion = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingHash);

        SigningHash = signingHash.Trim();
        HashAlgorithm = NormalizeOptional(hashAlgorithm);
        Purpose = NormalizeOptional(purpose);
        KeyId = NormalizeOptional(keyId);
        KeyVersion = NormalizeOptional(keyVersion);
        Metadata = NormalizeMetadata(metadata);
    }

    /// <summary>
    /// Gets the precomputed artifact hash to sign.
    /// </summary>
    public string SigningHash { get; }

    /// <summary>
    /// Gets the hash algorithm or descriptor associated with <see cref="SigningHash" />, when supplied.
    /// </summary>
    public string? HashAlgorithm { get; }

    /// <summary>
    /// Gets the host-defined signing purpose, when supplied.
    /// </summary>
    public string? Purpose { get; }

    /// <summary>
    /// Gets the requested signing key identifier, when supplied.
    /// </summary>
    public string? KeyId { get; }

    /// <summary>
    /// Gets the requested signing key version, when supplied.
    /// </summary>
    public string? KeyVersion { get; }

    /// <summary>
    /// Gets additional provider-neutral request metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether metadata is present.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Gets the exact bytes the signing provider must sign.
    /// </summary>
    /// <remarks>
    /// <see cref="GovernanceArtifactSigner" /> sets this to the version 1 input from
    /// <see cref="GovernanceSignatureInput.CreateV1" />, which binds the canonical descriptors, hash, and signing policy
    /// context. Providers must sign these bytes rather than <see cref="SigningHash" />; a provider that signs the hash text
    /// produces a signature that fails version 1 verification. When no input was supplied, this returns the pre-6.0 input
    /// from <see cref="GovernanceSignatureInput.CreateLegacy" />. The supplied value is copied.
    /// </remarks>
    public ReadOnlyMemory<byte> SignatureInput
    {
        get => signatureInput ?? GovernanceSignatureInput.CreateLegacy(SigningHash);
        init => signatureInput = value.IsEmpty ? null : [.. value.Span];
    }

    /// <summary>
    /// Gets a value indicating whether no explicit signature input was supplied, so <see cref="SignatureInput" /> is the
    /// pre-6.0 hash-only input.
    /// </summary>
    public bool UsesLegacySignatureInput => signatureInput is null;

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
