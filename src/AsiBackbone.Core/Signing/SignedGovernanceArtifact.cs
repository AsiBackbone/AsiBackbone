namespace AsiBackbone.Core.Signing;

/// <summary>
/// Represents an AsiBackbone governance artifact together with its canonical payload, canonical hash,
/// and optional provider-neutral signing metadata.
/// </summary>
/// <typeparam name="TArtifact">The governance artifact type.</typeparam>
/// <remarks>
/// This type preserves the boundary between unsigned, signing-ready, and signed artifacts. A signed artifact
/// has provider metadata attached, but verification, immutable storage, hash chaining, and external anchoring
/// remain separate host or provider responsibilities.
/// </remarks>
public sealed class SignedGovernanceArtifact<TArtifact>
{
    internal SignedGovernanceArtifact(
        TArtifact artifact,
        CanonicalPayload canonicalPayload,
        CanonicalPayloadHash canonicalHash,
        SigningMetadata signingMetadata)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        ArgumentNullException.ThrowIfNull(canonicalHash);
        ArgumentNullException.ThrowIfNull(signingMetadata);

        if (!string.Equals(canonicalPayload.ArtifactType, canonicalHash.ArtifactType, StringComparison.Ordinal)
            || !string.Equals(canonicalPayload.ArtifactId, canonicalHash.ArtifactId, StringComparison.Ordinal)
            || !string.Equals(canonicalPayload.PayloadSchemaVersion, canonicalHash.PayloadSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(canonicalPayload.CanonicalizationVersion, canonicalHash.CanonicalizationVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("The canonical payload and canonical hash must describe the same governance artifact.", nameof(canonicalHash));
        }

        Artifact = artifact;
        CanonicalPayload = canonicalPayload;
        CanonicalHash = canonicalHash;
        SigningMetadata = signingMetadata;
    }

    /// <summary>
    /// Gets the original governance artifact.
    /// </summary>
    public TArtifact Artifact { get; }

    /// <summary>
    /// Gets the deterministic canonical payload used for hashing and signing.
    /// </summary>
    public CanonicalPayload CanonicalPayload { get; }

    /// <summary>
    /// Gets the canonical payload hash metadata.
    /// </summary>
    public CanonicalPayloadHash CanonicalHash { get; }

    /// <summary>
    /// Gets provider-neutral signing metadata. This may be empty, signing-ready, failed, or signed metadata.
    /// </summary>
    public SigningMetadata SigningMetadata { get; }

    /// <summary>
    /// Gets the stable artifact type bound into the canonical payload and signing metadata.
    /// </summary>
    public string ArtifactType => CanonicalHash.ArtifactType;

    /// <summary>
    /// Gets the stable artifact identifier bound into the canonical payload and signing metadata.
    /// </summary>
    public string ArtifactId => CanonicalHash.ArtifactId;

    /// <summary>
    /// Gets the hash algorithm used to compute <see cref="SigningHash" />.
    /// </summary>
    public string HashAlgorithm => CanonicalHash.HashAlgorithm;

    /// <summary>
    /// Gets the canonical payload hash value that is signed or made signing-ready.
    /// </summary>
    public string SigningHash => CanonicalHash.HashValue;

    /// <summary>
    /// Gets a value indicating whether provider signing metadata includes a signature.
    /// </summary>
    public bool IsSigned => SigningMetadata.IsSigned;

    /// <summary>
    /// Gets a value indicating whether hash metadata is present but no signature has been attached.
    /// </summary>
    public bool IsSigningReady => !SigningMetadata.HasSignature && SigningMetadata.SigningHash is not null;

    /// <summary>
    /// Gets a value indicating whether the artifact carries no signing metadata.
    /// </summary>
    public bool HasNoSignature => !SigningMetadata.HasSignature && SigningMetadata.SigningHash is null;
}

/// <summary>
/// Provides non-generic factories for creating signed governance artifact wrappers.
/// </summary>
public static class SignedGovernanceArtifacts
{
    /// <summary>
    /// Creates an artifact wrapper with no signing metadata attached.
    /// </summary>
    public static SignedGovernanceArtifact<TArtifact> WithoutSignature<TArtifact>(
        TArtifact artifact,
        CanonicalPayload canonicalPayload,
        CanonicalPayloadHash canonicalHash)
    {
        return new SignedGovernanceArtifact<TArtifact>(
            artifact,
            canonicalPayload,
            canonicalHash,
            SigningMetadata.NoSignature);
    }

    /// <summary>
    /// Creates a signing-ready artifact wrapper. Hash metadata is attached without implying that a signature exists.
    /// </summary>
    public static SignedGovernanceArtifact<TArtifact> SigningReady<TArtifact>(
        TArtifact artifact,
        CanonicalPayload canonicalPayload,
        CanonicalPayloadHash canonicalHash,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new SignedGovernanceArtifact<TArtifact>(
            artifact,
            canonicalPayload,
            canonicalHash,
            canonicalHash.ToSigningMetadata(metadata));
    }

    /// <summary>
    /// Creates an artifact wrapper from a stored canonical payload, hash, and signing metadata, verifying that the payload hashes to the stored hash.
    /// </summary>
    /// <remarks>
    /// Use this factory when rehydrating a previously signed artifact from storage, a queue, or any other channel outside the
    /// signing call itself. <see cref="FromSigningMetadata{TArtifact}" /> accepts the hash a caller supplies, which is correct
    /// immediately after signing but does not establish that a stored payload still hashes to a stored hash. Rehydration
    /// recomputes the hash and rejects the triple when payload and hash disagree.
    /// </remarks>
    /// <exception cref="ArgumentException">The canonical payload does not hash to <paramref name="canonicalHash" />.</exception>
    /// <exception cref="NotSupportedException">The canonical hash algorithm is not supported by the built-in hasher.</exception>
    public static SignedGovernanceArtifact<TArtifact> Rehydrate<TArtifact>(
        TArtifact artifact,
        CanonicalPayload canonicalPayload,
        CanonicalPayloadHash canonicalHash,
        SigningMetadata signingMetadata)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        ArgumentNullException.ThrowIfNull(canonicalHash);

        CanonicalPayloadHash recomputedHash = CanonicalPayloadHasher.ComputeHash(canonicalPayload, canonicalHash.HashAlgorithm);

        return string.Equals(recomputedHash.HashValue, canonicalHash.HashValue, StringComparison.Ordinal)
            ? FromSigningMetadata(artifact, canonicalPayload, canonicalHash, signingMetadata)
            : throw new ArgumentException(
                "The canonical payload does not hash to the supplied canonical hash value.",
                nameof(canonicalHash));
    }

    /// <summary>
    /// Creates an artifact wrapper from signing metadata returned by a host or provider package.
    /// </summary>
    /// <remarks>
    /// The supplied <paramref name="canonicalHash" /> is trusted as given, which is appropriate immediately after a signing
    /// call produced it from <paramref name="canonicalPayload" />. Use <see cref="Rehydrate{TArtifact}" /> when the payload and
    /// hash were read back from storage or received over a wire, so the pair is checked rather than assumed.
    /// </remarks>
    public static SignedGovernanceArtifact<TArtifact> FromSigningMetadata<TArtifact>(
        TArtifact artifact,
        CanonicalPayload canonicalPayload,
        CanonicalPayloadHash canonicalHash,
        SigningMetadata signingMetadata)
    {
        ArgumentNullException.ThrowIfNull(signingMetadata);

        return new SignedGovernanceArtifact<TArtifact>(
            artifact,
            canonicalPayload,
            canonicalHash,
            MergeCanonicalHashMetadata(canonicalHash, signingMetadata));
    }

    private static SigningMetadata MergeCanonicalHashMetadata(
        CanonicalPayloadHash canonicalHash,
        SigningMetadata signingMetadata)
    {
        if (signingMetadata.SigningHash is not null
            && !string.Equals(signingMetadata.SigningHash, canonicalHash.HashValue, StringComparison.Ordinal))
        {
            throw new ArgumentException("Signing metadata hash must match the canonical payload hash.", nameof(signingMetadata));
        }

        Dictionary<string, string> metadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in canonicalHash.ToSigningMetadata().Metadata)
        {
            metadata[item.Key] = item.Value;
        }

        foreach (KeyValuePair<string, string> item in signingMetadata.Metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            metadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        return SigningMetadata.Create(
            signingHash: canonicalHash.HashValue,
            hashAlgorithm: string.IsNullOrWhiteSpace(signingMetadata.HashAlgorithm)
                ? canonicalHash.HashAlgorithm
                : signingMetadata.HashAlgorithm,
            signature: signingMetadata.Signature,
            signatureAlgorithm: signingMetadata.SignatureAlgorithm,
            keyId: signingMetadata.KeyId,
            keyVersion: signingMetadata.KeyVersion,
            provider: signingMetadata.Provider,
            signedUtc: signingMetadata.SignedUtc,
            metadata: metadata);
    }
}
