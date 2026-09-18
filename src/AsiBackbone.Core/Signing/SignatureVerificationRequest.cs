using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Represents a provider-neutral request to verify signing metadata against a precomputed artifact hash.
/// </summary>
public sealed class SignatureVerificationRequest
{
    private readonly byte[]? signatureInput;

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureVerificationRequest" /> class.
    /// </summary>
    public SignatureVerificationRequest(
        string signingHash,
        SigningMetadata signingMetadata,
        string? purpose = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingHash);
        ArgumentNullException.ThrowIfNull(signingMetadata);

        SigningHash = signingHash.Trim();
        SigningMetadata = signingMetadata;
        Purpose = NormalizeOptional(purpose);
        Metadata = NormalizeMetadata(metadata);
    }

    /// <summary>
    /// Gets the precomputed artifact hash expected to have been signed.
    /// </summary>
    public string SigningHash { get; }

    /// <summary>
    /// Gets the provider-neutral signing metadata to verify.
    /// </summary>
    public SigningMetadata SigningMetadata { get; }

    /// <summary>
    /// Gets the host-defined verification purpose, when supplied.
    /// </summary>
    public string? Purpose { get; }

    /// <summary>
    /// Gets additional provider-neutral request metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether metadata is present.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Gets the exact bytes the verification provider must verify the signature against.
    /// </summary>
    /// <remarks>
    /// <see cref="GovernanceArtifactVerifier" /> sets this to the version 1 input rebuilt from the artifact and its signing
    /// metadata, or to the pre-6.0 input when the verification context explicitly allows it. Verification providers must
    /// verify these bytes rather than <see cref="SigningHash" />; verifying the hash text instead leaves the signing policy
    /// context unauthenticated. When no input was supplied, this returns the pre-6.0 input from
    /// <see cref="GovernanceSignatureInput.CreateLegacy" />. The supplied value is copied.
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
