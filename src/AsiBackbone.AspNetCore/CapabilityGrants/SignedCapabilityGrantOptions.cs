using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.AspNetCore.CapabilityGrants;

/// <summary>
/// Configures <see cref="SignedCapabilityGrantEndpointValidator" />.
/// </summary>
/// <remarks>
/// The host owns every value here. <see cref="Issuer" /> and <see cref="Audience" /> are required; the validator binds
/// the actor, operation, scopes, and policy context itself from the request, the endpoint descriptor, and the current
/// decision.
/// </remarks>
public sealed class SignedCapabilityGrantOptions
{
    /// <summary>
    /// Gets the default request header that carries the capability grant token.
    /// </summary>
    public const string DefaultTokenHeaderName = "AsiBackbone-Capability-Grant";

    /// <summary>
    /// Gets or sets the issuer every accepted grant must name.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the audience every accepted grant must name, typically this relying party.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets the non-negative tolerance applied to a grant's not-before and expiry times.
    /// </summary>
    /// <remarks>
    /// Defaults to zero. Hosts whose issuer and relying party run on different machines usually allow a few seconds.
    /// </remarks>
    public TimeSpan AllowedClockSkew { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the local use limit. The narrower of this value and the grant's signed
    /// <see cref="CapabilityGrant.MaxUseCount" /> applies.
    /// </summary>
    public int MaxUseCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the grant must carry the policy version and hash of the current decision,
    /// when the decision has them.
    /// </summary>
    public bool BindPolicy { get; set; } = true;

    /// <summary>
    /// Gets or sets the request header that carries the token, or <see langword="null" /> to not read a header.
    /// </summary>
    public string? TokenHeaderName { get; set; } = DefaultTokenHeaderName;

    /// <summary>
    /// Gets or sets the form field that carries the token, or <see langword="null" /> to not read the form.
    /// </summary>
    /// <remarks>
    /// The header is read first. <see cref="FormFieldCapabilityGrantRequestBindingProvider" /> always excludes this field
    /// from the form binding, because the token cannot bind itself.
    /// </remarks>
    public string? TokenFormFieldName { get; set; }

    /// <summary>
    /// Gets or sets the maximum accepted token length, in characters.
    /// </summary>
    public int MaxTokenLength { get; set; } = CapabilityGrantToken.DefaultMaxTokenLength;

    /// <summary>
    /// Gets or sets the signing key identifier the grant proof must use, when set.
    /// </summary>
    public string? ExpectedProofKeyId { get; set; }

    /// <summary>
    /// Gets or sets the signing key version the grant proof must use, when set.
    /// </summary>
    public string? ExpectedProofKeyVersion { get; set; }

    /// <summary>
    /// Gets or sets the signing provider that must have produced the grant proof, when set.
    /// </summary>
    public string? RequiredProofProvider { get; set; }

    /// <summary>
    /// Gets or sets the hash algorithm the grant proof must use, when set.
    /// </summary>
    public string? RequiredProofHashAlgorithm { get; set; }

    /// <summary>
    /// Gets or sets the canonical payload options the issuer signed with; defaults apply when <see langword="null" />.
    /// </summary>
    public CanonicalPayloadOptions? ProofPayloadOptions { get; set; }

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <exception cref="InvalidOperationException">An option is missing or out of range.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("Signed capability grant validation requires an issuer.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Signed capability grant validation requires an audience.");
        }

        if (AllowedClockSkew < TimeSpan.Zero)
        {
            throw new InvalidOperationException("The allowed clock skew must not be negative.");
        }

        if (MaxUseCount < 1)
        {
            throw new InvalidOperationException("The maximum use count must be at least one.");
        }

        if (MaxTokenLength < 1)
        {
            throw new InvalidOperationException("The maximum token length must be at least one character.");
        }

        if (string.IsNullOrWhiteSpace(TokenHeaderName) && string.IsNullOrWhiteSpace(TokenFormFieldName))
        {
            throw new InvalidOperationException("Signed capability grant validation requires a token header name, a token form field name, or both.");
        }
    }
}
