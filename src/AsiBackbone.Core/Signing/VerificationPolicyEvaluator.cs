namespace AsiBackbone.Core.Signing;

/// <summary>
/// Evaluates provider-neutral signature verification results against host verification policy.
/// </summary>
public static class VerificationPolicyEvaluator
{
    private static readonly CategoryRule[] FailureCodeCategoryRules =
    [
        new(SignatureVerificationCategory.MissingSignature, ["missing"]),
        new(SignatureVerificationCategory.HashMismatch, ["hash"]),
        new(SignatureVerificationCategory.CanonicalizationMismatch, ["canonicalization", "payload-schema", "artifact"]),
        new(SignatureVerificationCategory.UnsupportedAlgorithm, ["unsupported", "algorithm"]),
        new(SignatureVerificationCategory.RevokedKey, ["revoked", "disabled"]),
        new(
            SignatureVerificationCategory.UnknownKeyVersion,
            ["key-version", "key.mismatch", "key-mismatch"],
            ["unknown", "key"]),
        new(SignatureVerificationCategory.ProviderUnavailable, ["provider-unavailable", "unavailable", "timeout", "network"]),
        new(SignatureVerificationCategory.InvalidSignature, ["invalid", "malformed", "signature"])
    ];

    /// <summary>
    /// Evaluates a signed governance artifact and verification result against verification policy.
    /// </summary>
    public static VerificationPolicyOutcome Evaluate<TArtifact>(
        SignedGovernanceArtifact<TArtifact> artifact,
        SignatureVerificationResult verificationResult,
        VerificationPolicyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(verificationResult);

        return VerificationPolicyOutcome.CreateCore(
            artifact.ArtifactType,
            artifact.ArtifactId,
            artifact.SigningHash,
            artifact.HashAlgorithm,
            artifact.SigningMetadata,
            verificationResult,
            options);
    }

    /// <summary>
    /// Maps a provider-neutral verification result to a stable verification category.
    /// </summary>
    public static SignatureVerificationCategory Categorize(SignatureVerificationResult verificationResult)
    {
        ArgumentNullException.ThrowIfNull(verificationResult);

        if (verificationResult.IsValid)
        {
            return SignatureVerificationCategory.Valid;
        }

        if (Matches(verificationResult.Status, "MissingSignature"))
        {
            return SignatureVerificationCategory.MissingSignature;
        }

        if (verificationResult.Category is SignatureVerificationCategory explicitCategory)
        {
            return explicitCategory;
        }

        string failureCode = verificationResult.FailureCode ?? string.Empty;

        foreach (CategoryRule rule in FailureCodeCategoryRules)
        {
            if (rule.IsMatch(failureCode))
            {
                return rule.Category;
            }
        }

        return SignatureVerificationCategory.Failed;
    }

    private static bool Matches(string value, string pattern)
    {
        return value.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct CategoryRule(
        SignatureVerificationCategory Category,
        string[] AnyPatterns,
        string[]? AllPatterns = null)
    {
        public bool IsMatch(string failureCode)
        {
            foreach (string pattern in AnyPatterns)
            {
                if (Matches(failureCode, pattern))
                {
                    return true;
                }
            }

            if (AllPatterns is null)
            {
                return false;
            }

            foreach (string pattern in AllPatterns)
            {
                if (!Matches(failureCode, pattern))
                {
                    return false;
                }
            }

            return AllPatterns.Length > 0;
        }
    }
}
