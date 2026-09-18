using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Tests the guards that keep verification policy and capability validation options from being configured into silently
/// permissive states.
/// </summary>
public sealed class VerificationPolicySafetyTests
{
    /// <summary>
    /// Verifies that an untrusted key denies by default rather than escalating.
    /// </summary>
    [Fact]
    public void UntrustedKeyDeniesByDefault()
    {
        Assert.Equal(
            VerificationPolicyAction.Deny,
            VerificationPolicyOptions.Default.GetAction(SignatureVerificationCategory.UntrustedKey));
    }

    /// <summary>
    /// Verifies that a signing-context pin mismatch (provider or policy context) denies by default rather than deferring or escalating.
    /// </summary>
    [Fact]
    public void UntrustedSigningContextDeniesByDefault()
    {
        Assert.Equal(
            VerificationPolicyAction.Deny,
            VerificationPolicyOptions.Default.GetAction(SignatureVerificationCategory.UntrustedSigningContext));
    }

    /// <summary>
    /// Pins the complete default action map. Only Valid allows, only the three operational categories use softer actions,
    /// and every integrity or trust failure denies. A newly added category fails this test until it is deliberately
    /// classified, so a future category cannot silently inherit a soft outcome.
    /// </summary>
    [Fact]
    public void DefaultActionMapDeniesEveryIntegrityAndTrustFailure()
    {
        Dictionary<SignatureVerificationCategory, VerificationPolicyAction> expected = new()
        {
            [SignatureVerificationCategory.Valid] = VerificationPolicyAction.Allow,
            [SignatureVerificationCategory.InvalidSignature] = VerificationPolicyAction.Deny,
            [SignatureVerificationCategory.HashMismatch] = VerificationPolicyAction.Deny,
            [SignatureVerificationCategory.MissingSignature] = VerificationPolicyAction.Deny,
            [SignatureVerificationCategory.RevokedKey] = VerificationPolicyAction.Deny,
            [SignatureVerificationCategory.CanonicalizationMismatch] = VerificationPolicyAction.Deny,
            [SignatureVerificationCategory.UnsupportedAlgorithm] = VerificationPolicyAction.Deny,
            [SignatureVerificationCategory.UntrustedKey] = VerificationPolicyAction.Deny,
            [SignatureVerificationCategory.UntrustedSigningContext] = VerificationPolicyAction.Deny,
            [SignatureVerificationCategory.ProviderUnavailable] = VerificationPolicyAction.Defer,
            [SignatureVerificationCategory.UnknownKeyVersion] = VerificationPolicyAction.Escalate,
            [SignatureVerificationCategory.Failed] = VerificationPolicyAction.Escalate
        };

        foreach (SignatureVerificationCategory category in Enum.GetValues<SignatureVerificationCategory>())
        {
            if (category is SignatureVerificationCategory.Unspecified)
            {
                continue;
            }

            bool isClassified = expected.TryGetValue(category, out VerificationPolicyAction expectedAction);

            Assert.True(isClassified, $"Category '{category}' has no expected default action; classify it deliberately.");
            Assert.Equal(expectedAction, VerificationPolicyOptions.Default.GetAction(category));
        }
    }

    /// <summary>
    /// Verifies that a host that deliberately accepts unsigned artifacts on a lower-assurance path can still opt back into
    /// acknowledgment for a missing signature.
    /// </summary>
    [Fact]
    public void HostCanOptMissingSignatureBackIntoAcknowledgment()
    {
        Dictionary<SignatureVerificationCategory, VerificationPolicyAction> overrides = new()
        {
            [SignatureVerificationCategory.MissingSignature] = VerificationPolicyAction.RequireAcknowledgment
        };

        var options = VerificationPolicyOptions.Create(overrides);

        Assert.Equal(
            VerificationPolicyAction.RequireAcknowledgment,
            options.GetAction(SignatureVerificationCategory.MissingSignature));
    }

    /// <summary>
    /// Verifies that mapping a failure category to Allow is refused without the explicit opt-in.
    /// </summary>
    [Fact]
    public void CreateRejectsAllowOverrideForAFailureCategory()
    {
        Dictionary<SignatureVerificationCategory, VerificationPolicyAction> overrides = new()
        {
            [SignatureVerificationCategory.InvalidSignature] = VerificationPolicyAction.Allow
        };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => VerificationPolicyOptions.Create(overrides));

        Assert.Equal("actionOverrides", exception.ParamName);
    }

    /// <summary>
    /// Verifies that the same override is accepted when the caller opts in deliberately.
    /// </summary>
    [Fact]
    public void CreateAcceptsAllowOverrideWithExplicitOptIn()
    {
        Dictionary<SignatureVerificationCategory, VerificationPolicyAction> overrides = new()
        {
            [SignatureVerificationCategory.InvalidSignature] = VerificationPolicyAction.Allow
        };

        var options = VerificationPolicyOptions.Create(overrides, allowUnsafeAllowOverrides: true);

        Assert.Equal(
            VerificationPolicyAction.Allow,
            options.GetAction(SignatureVerificationCategory.InvalidSignature));
    }

    /// <summary>
    /// Verifies that a non-failure override remains available without the opt-in.
    /// </summary>
    [Fact]
    public void CreateAllowsNonAllowOverridesWithoutOptIn()
    {
        Dictionary<SignatureVerificationCategory, VerificationPolicyAction> overrides = new()
        {
            [SignatureVerificationCategory.ProviderUnavailable] = VerificationPolicyAction.Deny
        };

        var options = VerificationPolicyOptions.Create(overrides);

        Assert.Equal(
            VerificationPolicyAction.Deny,
            options.GetAction(SignatureVerificationCategory.ProviderUnavailable));
    }

    /// <summary>
    /// Verifies that requiring proof without an audience expectation is refused.
    /// </summary>
    /// <remarks>
    /// Proof establishes that a grant was signed, not that it was issued for this audience, so the pair has to be stated
    /// together.
    /// </remarks>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CapabilityOptionsRequireAudienceWhenProofOrUseCheckIsRequired(bool requireProof, bool requireUseCheck)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CapabilityGrantValidationOptions.Create(
                requireProof: requireProof,
                requireUseCheck: requireUseCheck));

        Assert.Equal("audience", exception.ParamName);
    }

    /// <summary>
    /// Verifies that metadata-only validation remains available without an audience expectation.
    /// </summary>
    [Fact]
    public void CapabilityOptionsAllowNoAudienceForMetadataOnlyValidation()
    {
        var options = CapabilityGrantValidationOptions.CreateMetadataValidation();

        Assert.False(options.RequireProof);
        Assert.False(options.RequireUseCheck);
        Assert.Null(options.Audience);
    }
}
