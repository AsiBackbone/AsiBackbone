namespace AsiBackbone.Core.Signing;

/// <summary>
/// Provides provider-neutral helpers for verifying signed governance artifacts and applying verification policy.
/// </summary>
/// <remarks>
/// The verifier wrapper does not resolve provider-specific keys in Core and does not imply legal evidence, compliance certification, immutable storage, or tamper-evidence.
/// </remarks>
public static class GovernanceArtifactVerifier
{
    /// <summary>
    /// Verifies a signed governance artifact and maps the result to a host-facing policy outcome.
    /// </summary>
    public static async ValueTask<VerificationPolicyOutcome> VerifyAsync<TArtifact>(
        SignedGovernanceArtifact<TArtifact> artifact,
        IAsiBackboneSignatureVerificationService verificationService,
        VerificationPolicyOptions? options = null,
        VerificationPolicyContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(verificationService);
        cancellationToken.ThrowIfCancellationRequested();

        VerificationPolicyContext effectiveContext = context ?? VerificationPolicyContext.Default;
        SignatureVerificationResult? preflightResult = ValidateBeforeProvider(artifact, effectiveContext);

        if (preflightResult is not null)
        {
            return VerificationPolicyEvaluator.Evaluate(artifact, preflightResult, options);
        }

        try
        {
            SignatureVerificationResult verificationResult = await verificationService
                .VerifyAsync(
                    new SignatureVerificationRequest(
                        artifact.SigningHash,
                        artifact.SigningMetadata,
                        purpose: effectiveContext.Purpose ?? artifact.ArtifactType,
                        metadata: effectiveContext.Metadata),
                    cancellationToken)
                .ConfigureAwait(false);

            return VerificationPolicyEvaluator.Evaluate(artifact, verificationResult, options);
        }
        catch (InvalidOperationException exception)
        {
            return CreateProviderUnavailableOutcome(artifact, options, exception);
        }
        catch (NotSupportedException exception)
        {
            return CreateProviderUnavailableOutcome(artifact, options, exception);
        }
        catch (TimeoutException exception)
        {
            return CreateProviderUnavailableOutcome(artifact, options, exception);
        }
    }

    private static VerificationPolicyOutcome CreateProviderUnavailableOutcome<TArtifact>(
        SignedGovernanceArtifact<TArtifact> artifact,
        VerificationPolicyOptions? options,
        Exception exception)
    {
        var providerUnavailableResult = SignatureVerificationResult.Failed(
            "signature.provider-unavailable",
            SignatureVerificationCategory.ProviderUnavailable,
            exception.GetType().Name);

        return VerificationPolicyEvaluator.Evaluate(artifact, providerUnavailableResult, options);
    }

    private static SignatureVerificationResult? ValidateBeforeProvider<TArtifact>(
        SignedGovernanceArtifact<TArtifact> artifact,
        VerificationPolicyContext context)
    {
        SignatureVerificationResult? metadataResult = ValidateSigningMetadata(artifact, context);

        return metadataResult ?? ValidateCanonicalBinding(artifact);
    }

    /// <summary>
    /// Recomputes the canonical payload hash and rejects an artifact whose signed hash is not the hash of its own canonical payload.
    /// </summary>
    /// <remarks>
    /// Without this step the provider verifies a signature over a hash the artifact carries about itself, which leaves the
    /// artifact content unbound to the signature. A caller that rehydrates an artifact from storage or a queue can otherwise
    /// present modified content beside an authentic hash and signature pair and receive a valid outcome.
    /// </remarks>
    private static SignatureVerificationResult? ValidateCanonicalBinding<TArtifact>(
        SignedGovernanceArtifact<TArtifact> artifact)
    {
        CanonicalPayloadHash recomputedHash;

        try
        {
            recomputedHash = CanonicalPayloadHasher.ComputeHash(artifact.CanonicalPayload, artifact.HashAlgorithm);
        }
        catch (NotSupportedException)
        {
            return SignatureVerificationResult.Failed(
                "signature.hash-algorithm-unsupported",
                SignatureVerificationCategory.UnsupportedAlgorithm,
                "The canonical payload hash cannot be recomputed with the built-in hasher, so the signed hash is not bound to the artifact content.");
        }

        return string.Equals(recomputedHash.HashValue, artifact.CanonicalHash.HashValue, StringComparison.Ordinal)
            ? null
            : SignatureVerificationResult.Failed(
                "signature.hash-mismatch",
                SignatureVerificationCategory.HashMismatch,
                "The canonical payload does not hash to the signed canonical hash value.");
    }

    private static SignatureVerificationResult? ValidateSigningMetadata<TArtifact>(
        SignedGovernanceArtifact<TArtifact> artifact,
        VerificationPolicyContext context)
    {
        SigningMetadata metadata = artifact.SigningMetadata;

        return artifact.HasNoSignature || !metadata.HasSignature
            ? SignatureVerificationResult.MissingSignature("The governance artifact does not carry signature metadata.")
            : string.IsNullOrWhiteSpace(metadata.SigningHash)
            ? SignatureVerificationResult.MissingSignature("The governance artifact does not carry the hash that was signed.")
            : !string.Equals(metadata.SigningHash, artifact.SigningHash, StringComparison.Ordinal)
            ? SignatureVerificationResult.Failed(
                "signature.hash-mismatch",
                SignatureVerificationCategory.HashMismatch,
                "The signing metadata hash does not match the canonical artifact hash.")
            : metadata.HashAlgorithm is not null
            && !string.Equals(metadata.HashAlgorithm, artifact.HashAlgorithm, StringComparison.OrdinalIgnoreCase)
            ? SignatureVerificationResult.Failed(
                "signature.hash-algorithm-unsupported",
                SignatureVerificationCategory.HashMismatch,
                "The signing metadata hash algorithm does not match the canonical artifact hash algorithm.")
            : context.RequiredHashAlgorithm is not null
            && !string.Equals(context.RequiredHashAlgorithm, artifact.HashAlgorithm, StringComparison.OrdinalIgnoreCase)
            ? SignatureVerificationResult.Failed(
                "signature.hash-algorithm-unsupported",
                SignatureVerificationCategory.HashMismatch,
                "The canonical artifact hash algorithm does not match the required verification policy algorithm.")
            : !MatchesCanonicalMetadata(metadata, "artifact_id", artifact.ArtifactId)
            || !MatchesCanonicalMetadata(metadata, "artifact_type", artifact.ArtifactType)
            || !MatchesCanonicalMetadata(metadata, "canonicalization_version", artifact.CanonicalHash.CanonicalizationVersion)
            || !MatchesCanonicalMetadata(metadata, "payload_schema_version", artifact.CanonicalHash.PayloadSchemaVersion)
            ? SignatureVerificationResult.Failed(
                "signature.canonicalization-mismatch",
                SignatureVerificationCategory.CanonicalizationMismatch,
                "The signing metadata canonical artifact descriptors do not match the artifact being verified.")
            : context.ExpectedKeyId is not null
            && !string.Equals(context.ExpectedKeyId, metadata.KeyId, StringComparison.Ordinal)
            ? SignatureVerificationResult.Failed(
                "signature.key-not-trusted",
                SignatureVerificationCategory.UntrustedKey,
                "The signing key identifier does not match the verification policy expectation.")
            : context.ExpectedKeyVersion is not null
            && !string.Equals(context.ExpectedKeyVersion, metadata.KeyVersion, StringComparison.Ordinal)
            ? SignatureVerificationResult.Failed(
                "signature.key-not-trusted",
                SignatureVerificationCategory.UntrustedKey,
                "The signing key version does not match the verification policy expectation.")
            : context.RequiredProvider is not null
            && !string.Equals(context.RequiredProvider, metadata.Provider, StringComparison.Ordinal)
            ? SignatureVerificationResult.Failed(
                "signature.provider-unavailable",
                SignatureVerificationCategory.ProviderUnavailable,
                "The signing provider does not match the required verification policy provider.")
            : !MatchesOptionalPolicyMetadata(metadata, "policy_version", context.ExpectedPolicyVersion)
            || !MatchesOptionalPolicyMetadata(metadata, "policy_hash", context.ExpectedPolicyHash)
            ? SignatureVerificationResult.Failed(
                "signature.canonicalization-mismatch",
                SignatureVerificationCategory.CanonicalizationMismatch,
                "The signing metadata policy context does not match the verification policy expectation.")
            : null;
    }

    /// <summary>
    /// Requires a canonical descriptor to be present in signing metadata and to match the artifact being verified.
    /// </summary>
    /// <remarks>
    /// An absent descriptor previously matched anything, which let a signature produced for one artifact type or identifier
    /// be presented alongside a different artifact. The shipped factories always write these descriptors, so a signed
    /// artifact that is missing one did not come from a canonical signing path.
    /// </remarks>
    private static bool MatchesCanonicalMetadata(SigningMetadata metadata, string key, string expectedValue)
    {
        return metadata.Metadata.TryGetValue(key, out string? value)
            && string.Equals(value, expectedValue, StringComparison.Ordinal);
    }

    private static bool MatchesOptionalPolicyMetadata(SigningMetadata metadata, string key, string? expectedValue)
    {
        return expectedValue is null
            || (metadata.Metadata.TryGetValue(key, out string? value)
                && string.Equals(value, expectedValue, StringComparison.Ordinal));
    }
}
