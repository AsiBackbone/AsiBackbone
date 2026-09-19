using System.Text;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Builds the exact bytes a signing provider signs and a verification provider verifies for a governance artifact.
/// </summary>
/// <remarks>
/// <para>
/// Before 6.0, providers signed only the UTF-8 text of the canonical payload hash. Every other value recorded in
/// <see cref="SigningMetadata" />, including the signing policy version and policy hash, was an unauthenticated label: a
/// holder of a validly signed artifact could relabel it and verification still succeeded.
/// </para>
/// <para>
/// The version 1 signature input is a canonical JSON document that binds the format identifier, the canonical artifact
/// descriptors, the hash algorithm, the hash value, and the signing policy context recorded under
/// <see cref="PolicyVersionMetadataKey" /> and <see cref="PolicyHashMetadataKey" />. An absent policy value is bound as
/// JSON <c>null</c>, so adding, removing, or changing a policy label after signing invalidates the signature.
/// </para>
/// <para>
/// Key identifier, key version, provider, and signing timestamp are not part of the input, because managed-key providers
/// commonly resolve the key version and timestamp during signing. They are authenticated only to the extent that the
/// verification service resolves its verification key from <see cref="SigningMetadata.KeyId" /> and
/// <see cref="SigningMetadata.KeyVersion" /> and rejects a <see cref="SigningMetadata.Provider" /> it does not own.
/// </para>
/// </remarks>
public static class GovernanceSignatureInput
{
    /// <summary>
    /// Identifies the version 1 signature input format.
    /// </summary>
    public const string FormatV1 = "asibackbone.signature-input.v1";

    /// <summary>
    /// The signing metadata key whose value is bound into the version 1 signature input as the signing policy version.
    /// </summary>
    public const string PolicyVersionMetadataKey = "policy_version";

    /// <summary>
    /// The signing metadata key whose value is bound into the version 1 signature input as the signing policy hash.
    /// </summary>
    public const string PolicyHashMetadataKey = "policy_hash";

    /// <summary>
    /// Creates the version 1 signature input for a canonical payload hash and the signing policy context in the supplied
    /// signing metadata.
    /// </summary>
    /// <param name="canonicalHash">The canonical payload hash being signed or verified.</param>
    /// <param name="signingMetadata">
    /// Signing metadata supplying the <see cref="PolicyVersionMetadataKey" /> and <see cref="PolicyHashMetadataKey" />
    /// values. Missing, empty, or whitespace-only values are bound as JSON <c>null</c>; other values are trimmed.
    /// </param>
    /// <returns>The UTF-8 canonical JSON bytes to sign or verify.</returns>
    public static ReadOnlyMemory<byte> CreateV1(
        CanonicalPayloadHash canonicalHash,
        IReadOnlyDictionary<string, string>? signingMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(canonicalHash);

        SortedDictionary<string, object?> input = new(StringComparer.Ordinal)
        {
            ["artifactId"] = canonicalHash.ArtifactId,
            ["artifactType"] = canonicalHash.ArtifactType,
            ["canonicalizationVersion"] = canonicalHash.CanonicalizationVersion,
            ["format"] = FormatV1,
            ["hashAlgorithm"] = canonicalHash.HashAlgorithm,
            ["hashValue"] = canonicalHash.HashValue,
            ["payloadSchemaVersion"] = canonicalHash.PayloadSchemaVersion,
            ["policyHash"] = GetBoundValue(signingMetadata, PolicyHashMetadataKey),
            ["policyVersion"] = GetBoundValue(signingMetadata, PolicyVersionMetadataKey)
        };

        return Encoding.UTF8.GetBytes(CanonicalPayloadJson.Serialize(input));
    }

    /// <summary>
    /// Creates the pre-6.0 signature input: the UTF-8 text of the signing hash alone.
    /// </summary>
    /// <remarks>
    /// This input authenticates the canonical payload hash only. Core uses it for artifacts signed before 6.0 when a
    /// verification context explicitly opts in through
    /// <see cref="VerificationPolicyContext.WithLegacySignatureInputAllowed" />, and as the fallback for provider requests
    /// constructed without an explicit signature input.
    /// </remarks>
    /// <param name="signingHash">The canonical payload hash value.</param>
    /// <returns>The UTF-8 bytes of the trimmed signing hash.</returns>
    public static ReadOnlyMemory<byte> CreateLegacy(string signingHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingHash);

        return Encoding.UTF8.GetBytes(signingHash.Trim());
    }

    private static string? GetBoundValue(IReadOnlyDictionary<string, string>? metadata, string key)
    {
        return metadata is not null
            && metadata.TryGetValue(key, out string? value)
            && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }
}
