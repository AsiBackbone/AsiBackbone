using System.Reflection;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Focused coverage for verification-result categorization precedence and fallback behavior.
/// </summary>
public sealed class VerificationPolicyCategorizationTests
{
    /// <summary>
    /// Provides overlapping failure-code cases that document the established first-match precedence.
    /// </summary>
    public static TheoryData<string, SignatureVerificationCategory> PrecedenceCases => new()
    {
        { "signature.missing.hash-mismatch", SignatureVerificationCategory.MissingSignature },
        { "signature.hash-unsupported", SignatureVerificationCategory.HashMismatch },
        { "signature.canonicalization-unsupported", SignatureVerificationCategory.CanonicalizationMismatch },
        { "signature.unsupported-revoked", SignatureVerificationCategory.UnsupportedAlgorithm },
        { "signature.revoked-unknown-key", SignatureVerificationCategory.RevokedKey },
        { "signature.unknown-key-provider-unavailable", SignatureVerificationCategory.UnknownKeyVersion },
        { "signature.provider-unavailable-invalid", SignatureVerificationCategory.ProviderUnavailable },
        { "signature.invalid-fallback", SignatureVerificationCategory.InvalidSignature }
    };

    /// <summary>
    /// Verifies overlapping failure markers retain the established ordered categorization contract.
    /// </summary>
    /// <param name="failureCode">The provider-neutral failure code.</param>
    /// <param name="expectedCategory">The expected first matching verification category.</param>
    [Theory]
    [MemberData(nameof(PrecedenceCases))]
    public void CategorizeUsesStableFirstMatchPrecedence(
        string failureCode,
        SignatureVerificationCategory expectedCategory)
    {
        var result = SignatureVerificationResult.Failed(failureCode);

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(result);

        Assert.Equal(expectedCategory, category);
    }

    /// <summary>
    /// Verifies the dedicated missing-signature status takes precedence over conflicting failure-code markers.
    /// </summary>
    [Fact]
    public void CategorizePrioritizesMissingSignatureStatus()
    {
        SignatureVerificationResult result = CreateResult(
            isValid: false,
            status: "MissingSignature",
            failureCode: "signature.hash-mismatch");

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(result);

        Assert.Equal(SignatureVerificationCategory.MissingSignature, category);
    }

    /// <summary>
    /// Verifies a valid result remains valid even when defensive construction supplies failure-like metadata.
    /// </summary>
    [Fact]
    public void CategorizePrioritizesSuccessfulVerification()
    {
        SignatureVerificationResult result = CreateResult(
            isValid: true,
            status: "Verified",
            failureCode: "signature.invalid");

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(result);

        Assert.Equal(SignatureVerificationCategory.Valid, category);
    }

    /// <summary>
    /// Verifies categorization remains ordinal case-insensitive for provider failure codes.
    /// </summary>
    [Fact]
    public void CategorizeMatchesFailureCodesCaseInsensitively()
    {
        var result = SignatureVerificationResult.Failed("SIGNATURE.KEY-VERSION-UNKNOWN");

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(result);

        Assert.Equal(SignatureVerificationCategory.UnknownKeyVersion, category);
    }

    /// <summary>
    /// Verifies the unknown-key composite rule requires both the unknown and key markers.
    /// </summary>
    /// <param name="failureCode">A near-match code that must not satisfy the composite rule.</param>
    [Theory]
    [InlineData("signature.unknown-provider")]
    [InlineData("signature.key-rotation")]
    public void CategorizeRejectsIncompleteUnknownKeyCompositeMatches(string failureCode)
    {
        var result = SignatureVerificationResult.Failed(failureCode);

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(result);

        Assert.Equal(SignatureVerificationCategory.InvalidSignature, category);
    }

    /// <summary>
    /// Verifies an unrecognized provider failure code maps to the stable failed fallback category.
    /// </summary>
    [Fact]
    public void CategorizeReturnsFailedForUnrecognizedFailureCode()
    {
        var result = SignatureVerificationResult.Failed("verification.provider-rejected");

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(result);

        Assert.Equal(SignatureVerificationCategory.Failed, category);
    }

    /// <summary>
    /// Verifies an explicit category takes precedence over misleading free-form failure-code text.
    /// </summary>
    /// <param name="failureCode">A failure code whose text would otherwise infer a different category.</param>
    /// <param name="explicitCategory">The category supplied by the verification provider.</param>
    [Theory]
    [InlineData("provider.key-unavailable", SignatureVerificationCategory.RevokedKey)]
    [InlineData("verification-error", SignatureVerificationCategory.InvalidSignature)]
    [InlineData("signature.hash-mismatch", SignatureVerificationCategory.Failed)]
    public void CategorizePrioritizesExplicitCategory(
        string failureCode,
        SignatureVerificationCategory explicitCategory)
    {
        var result = SignatureVerificationResult.Failed(
            failureCode,
            explicitCategory);

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(result);

        Assert.Equal(explicitCategory, result.Category);
        Assert.Equal(explicitCategory, category);
    }

    /// <summary>
    /// Verifies the legacy failure factory retains failure-code inference for compatibility.
    /// </summary>
    [Fact]
    public void CategorizeUsesFailureCodeOnlyWhenCategoryIsAbsent()
    {
        var result = SignatureVerificationResult.Failed("provider.key-unavailable");

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(result);

        Assert.Null(result.Category);
        Assert.Equal(SignatureVerificationCategory.ProviderUnavailable, category);
    }

    /// <summary>
    /// Verifies a failed result cannot claim the successful category.
    /// </summary>
    [Fact]
    public void FailedRejectsValidCategory()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SignatureVerificationResult.Failed(
            "verification-error",
            SignatureVerificationCategory.Valid));
    }

    /// <summary>
    /// Verifies the categorizer rejects a null verification result.
    /// </summary>
    [Fact]
    public void CategorizeRejectsNullVerificationResult()
    {
        _ = Assert.Throws<ArgumentNullException>(() => VerificationPolicyEvaluator.Categorize(null!));
    }

    private static SignatureVerificationResult CreateResult(
        bool isValid,
        string status,
        string? failureCode)
    {
        ConstructorInfo constructor = typeof(SignatureVerificationResult).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(bool), typeof(string), typeof(string), typeof(string)],
            modifiers: null)
            ?? throw new InvalidOperationException("SignatureVerificationResult constructor could not be located.");

        return (SignatureVerificationResult)constructor.Invoke(
        [
            isValid,
            status,
            failureCode,
            null
        ]);
    }
}
