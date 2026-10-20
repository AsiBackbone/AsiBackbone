using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.CapabilityGrants;

/// <summary>
/// Represents the outcome of decoding a capability grant token with <see cref="CapabilityGrantToken.Decode" />.
/// </summary>
/// <remarks>
/// A successful decode establishes only that the token is well formed. The decoded grant has not been verified: pass
/// <see cref="SignedGrant" /> to <see cref="CapabilityGrantValidator.ValidateAsync" /> with a proof-requiring profile
/// before trusting any of its values.
/// </remarks>
public sealed class CapabilityGrantTokenDecodeResult
{
    private CapabilityGrantTokenDecodeResult(
        SignedGovernanceArtifact<CapabilityGrant>? signedGrant,
        string? failureCode,
        string? failureMessage)
    {
        SignedGrant = signedGrant;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
    }

    /// <summary>
    /// Gets a value indicating whether the token decoded into a signed grant.
    /// </summary>
    public bool Succeeded => SignedGrant is not null;

    /// <summary>
    /// Gets the decoded, not yet verified, signed grant when decoding succeeded.
    /// </summary>
    /// <remarks>
    /// The canonical payload and hash are rebuilt from the decoded grant fields rather than read from the token, so a
    /// grant field edited after signing produces a hash the signature does not cover.
    /// </remarks>
    public SignedGovernanceArtifact<CapabilityGrant>? SignedGrant { get; }

    /// <summary>
    /// Gets the machine-readable failure code, such as <c>capability.token-malformed</c>, when decoding failed.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets a human-readable failure message, when decoding failed.
    /// </summary>
    /// <remarks>
    /// The message describes the token structure only and never echoes token content.
    /// </remarks>
    public string? FailureMessage { get; }

    internal static CapabilityGrantTokenDecodeResult Success(SignedGovernanceArtifact<CapabilityGrant> signedGrant)
    {
        return new CapabilityGrantTokenDecodeResult(signedGrant, failureCode: null, failureMessage: null);
    }

    internal static CapabilityGrantTokenDecodeResult Failure(string failureCode, string failureMessage)
    {
        return new CapabilityGrantTokenDecodeResult(signedGrant: null, failureCode, failureMessage);
    }
}
