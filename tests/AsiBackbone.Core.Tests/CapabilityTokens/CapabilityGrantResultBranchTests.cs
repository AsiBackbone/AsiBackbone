using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityTokens;

/// <summary>
/// Tests for the branches of the CapabilityGrantResult class, including both use results and validation results.
/// </summary>
public sealed class CapabilityGrantResultBranchTests
{
    private static readonly DateTimeOffset IssuedUtc = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresUtc = new(2026, 6, 18, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Tests that the Accepted factory method of CapabilityGrantUseResult correctly normalizes the success branch, ensuring that the result indicates acceptance, has the correct state, use count, and no failure code or message.
    /// </summary>
    [Fact]
    public void UseResultAcceptedNormalizesSuccessBranches()
    {
        var result = CapabilityGrantUseResult.Accepted(2);

        Assert.True(result.IsAccepted);
        Assert.Equal(GrantUseState.Accepted, result.State);
        Assert.Equal(2, result.UseCount);
        Assert.Null(result.FailureCode);
        Assert.Null(result.FailureMessage);
    }

    /// <summary>
    /// Tests that the failure factory methods of CapabilityGrantUseResult correctly normalize the failure branches, ensuring that the result indicates non-acceptance, has the correct state, use count, failure code, and normalized failure message.
    /// </summary>
    /// <param name="failureMessage">The failure message to test.</param>
    /// <param name="expectedState">The expected state of the result.</param>
    /// <param name="expectedFailureCode">The expected failure code of the result.</param>
    /// <param name="expectedUseCount">The expected use count of the result.</param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [Theory]
    [InlineData(" use limit reached ", GrantUseState.UseLimitExceeded, "capability.use-limit-exceeded", 3)]
    [InlineData(" stopped ", GrantUseState.Stopped, "capability.grant-stopped", 0)]
    [InlineData(" cancelled ", GrantUseState.Cancelled, "capability.grant-cancelled", 0)]
    [InlineData(" unavailable ", GrantUseState.Unavailable, "capability.use-store-unavailable", 0)]
    public void UseResultFailureFactoriesNormalizeMessages(
        string failureMessage,
        GrantUseState expectedState,
        string expectedFailureCode,
        int expectedUseCount)
    {
        CapabilityGrantUseResult result = expectedState switch
        {
            GrantUseState.Unspecified => throw new ArgumentException("Unspecified is not a producible result state.", nameof(expectedState)),
            GrantUseState.Accepted => throw new ArgumentException("Accepted is covered by the success-result test.", nameof(expectedState)),
            GrantUseState.UseLimitExceeded => CapabilityGrantUseResult.UseLimitExceeded(expectedUseCount, failureMessage),
            GrantUseState.Stopped => CapabilityGrantUseResult.Stopped(failureMessage),
            GrantUseState.Cancelled => CapabilityGrantUseResult.Cancelled(failureMessage),
            GrantUseState.Unavailable => CapabilityGrantUseResult.Unavailable(failureMessage),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedState), expectedState, null)
        };

        Assert.False(result.IsAccepted);
        Assert.Equal(expectedState, result.State);
        Assert.Equal(expectedUseCount, result.UseCount);
        Assert.Equal(expectedFailureCode, result.FailureCode);
        Assert.Equal(failureMessage.Trim(), result.FailureMessage);
    }

    /// <summary>
    /// Tests that the factory methods of CapabilityGrantUseResult throw an ArgumentOutOfRangeException when provided with a negative use count, ensuring that invalid input is correctly handled.
    /// </summary>
    [Fact]
    public void UseResultRejectsNegativeUseCount()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CapabilityGrantUseResult.Accepted(-1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CapabilityGrantUseResult.UseLimitExceeded(-1));
    }

    /// <summary>
    /// Tests that the Valid factory method of CapabilityGrantValidationResult correctly includes safe metadata and indicates that the grant should be allowed, ensuring that the result reflects a valid grant with the expected properties.
    /// </summary>
    [Fact]
    public void ValidationResultValidIncludesSafeMetadataAndAllows()
    {
        CapabilityTokenGrant grant = CreateGrant();

        var result = CapabilityGrantValidationResult.Valid(grant);

        Assert.True(result.IsValid);
        Assert.True(result.ShouldAllow);
        Assert.Equal(CapabilityTokenValidationCategory.Valid, result.Category);
        Assert.Equal(VerificationPolicyAction.Allow, result.Action);
        Assert.Equal("Valid", result.Status);
        Assert.Equal("grant-1", result.TokenId);
        Assert.Null(result.FailureCode);
        Assert.Equal("ack-1", result.SafeMetadata["acknowledgment_id"]);
        Assert.Equal("handshake-1", result.SafeMetadata["handshake_id"]);
        Assert.Equal("policy-v1", result.SafeMetadata["policy_version"]);
        Assert.Equal("policy-hash", result.SafeMetadata["policy_hash"]);
        Assert.Equal("robot-arm-1", result.SafeMetadata["resource_binding"]);
    }

    /// <summary>
    /// Tests that the Failed factory method of CapabilityGrantValidationResult correctly normalizes the failure message and metadata, ensuring that the result reflects a failed validation with the expected properties and normalized values.
    /// </summary>
    [Fact]
    public void ValidationResultFailedNormalizesFailureMessageAndMetadata()
    {
        CapabilityTokenGrant grant = CreateGrant(
            acknowledgmentId: " ack-1 ",
            handshakeId: " handshake-1 ",
            policyVersion: " policy-v1 ",
            policyHash: " policy-hash ",
            resourceBinding: " robot-arm-1 ");

        var result = CapabilityGrantValidationResult.Failed(
            grant,
            CapabilityTokenValidationCategory.WrongScope,
            VerificationPolicyAction.Deny,
            " capability.scope-missing ",
            " Missing scope. ");

        Assert.False(result.IsValid);
        Assert.False(result.ShouldAllow);
        Assert.Equal(CapabilityTokenValidationCategory.WrongScope, result.Category);
        Assert.Equal(VerificationPolicyAction.Deny, result.Action);
        Assert.Equal("Failed", result.Status);
        Assert.Equal("capability.scope-missing", result.FailureCode);
        Assert.Equal("Missing scope.", result.FailureMessage);
        Assert.Equal("capability.scope-missing", result.SafeMetadata["failure_code"]);
    }

    /// <summary>
    /// Tests that the Failed factory method of CapabilityGrantValidationResult omits blank optional safe metadata, ensuring that the result does not include any empty or whitespace-only values in its safe metadata dictionary.
    /// </summary>
    [Fact]
    public void ValidationResultOmitsBlankOptionalSafeMetadata()
    {
        CapabilityTokenGrant grant = CreateGrant(
            acknowledgmentId: null,
            handshakeId: null,
            policyVersion: null,
            policyHash: null,
            resourceBinding: null);

        var result = CapabilityGrantValidationResult.Failed(
            grant,
            CapabilityTokenValidationCategory.Failed,
            VerificationPolicyAction.Escalate,
            "capability.failed",
            " ");

        Assert.Null(result.FailureMessage);
        Assert.False(result.SafeMetadata.ContainsKey("acknowledgment_id"));
        Assert.False(result.SafeMetadata.ContainsKey("handshake_id"));
        Assert.False(result.SafeMetadata.ContainsKey("policy_version"));
        Assert.False(result.SafeMetadata.ContainsKey("policy_hash"));
        Assert.False(result.SafeMetadata.ContainsKey("resource_binding"));
    }

    /// <summary>
    /// Tests that the factory methods of CapabilityGrantValidationResult throw appropriate exceptions when provided with null or invalid inputs, ensuring that the methods enforce input validation and handle error cases correctly.
    /// </summary>
    [Fact]
    public void ValidationResultFactoriesRejectNullInputs()
    {
        _ = Assert.Throws<ArgumentNullException>(() => CapabilityGrantValidationResult.Valid(null!));
        _ = Assert.Throws<ArgumentNullException>(() => CapabilityGrantValidationResult.Failed(
            null!,
            CapabilityTokenValidationCategory.Failed,
            VerificationPolicyAction.Escalate,
            "capability.failed"));
        _ = Assert.Throws<ArgumentException>(() => CapabilityGrantValidationResult.Failed(
            CreateGrant(),
            CapabilityTokenValidationCategory.Failed,
            VerificationPolicyAction.Escalate,
            " "));
    }

    private static CapabilityTokenGrant CreateGrant(
        string? acknowledgmentId = "ack-1",
        string? handshakeId = "handshake-1",
        string? policyVersion = "policy-v1",
        string? policyHash = "policy-hash",
        string? resourceBinding = "robot-arm-1")
    {
        return CapabilityTokenGrant.Create(
            tokenId: "grant-1",
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            issuedUtc: IssuedUtc,
            expiresUtc: ExpiresUtc,
            acknowledgmentId: acknowledgmentId,
            handshakeId: handshakeId,
            policyVersion: policyVersion,
            policyHash: policyHash,
            resourceBinding: resourceBinding);
    }
}
