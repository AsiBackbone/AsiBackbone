using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.CapabilityTokens;

public sealed class CapabilityGrantValidationOptions
{
    private static readonly string[] EmptyScopes = [];

    private CapabilityGrantValidationOptions(
        string? issuer,
        string? audience,
        IReadOnlyList<string> scopes,
        DateTimeOffset? validationUtc,
        TimeSpan allowedClockSkew,
        string? policyVersion,
        string? policyHash,
        string? acknowledgmentId,
        string? handshakeId,
        string? gatewayBinding,
        string? resourceBinding,
        bool requireProof,
        bool requireAcknowledgmentReference,
        bool requireUseCheck,
        int maxUseCount,
        string? expectedProofKeyId,
        string? expectedProofKeyVersion,
        string? expectedProofPolicyVersion,
        string? expectedProofPolicyHash,
        string? requiredProofProvider,
        string? requiredProofHashAlgorithm,
        CanonicalPayloadOptions? proofPayloadOptions)
    {
        if (allowedClockSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(allowedClockSkew),
                allowedClockSkew,
                "Allowed clock skew must be greater than or equal to zero.");
        }

        if (maxUseCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUseCount), maxUseCount, "Maximum use count must be greater than zero.");
        }

        // A grant can be proof-verified and still be the wrong grant for this caller. Without an audience expectation a
        // grant issued for one gateway validates at another, so requiring proof or a use check without stating the
        // audience is a configuration that cannot deliver what it appears to promise.
        if ((requireProof || requireUseCheck) && string.IsNullOrWhiteSpace(audience))
        {
            throw new ArgumentException(
                "An audience expectation is required when proof or use checking is required, because proof alone does not establish that the grant was issued for this audience.",
                nameof(audience));
        }

        Issuer = NormalizeOptional(issuer);
        Audience = NormalizeOptional(audience);
        Scopes = scopes;
        ValidationUtc = validationUtc?.ToUniversalTime();
        AllowedClockSkew = allowedClockSkew;
        PolicyVersion = NormalizeOptional(policyVersion);
        PolicyHash = NormalizeOptional(policyHash);
        AcknowledgmentId = NormalizeOptional(acknowledgmentId);
        HandshakeId = NormalizeOptional(handshakeId);
        GatewayBinding = NormalizeOptional(gatewayBinding);
        ResourceBinding = NormalizeOptional(resourceBinding);
        RequireProof = requireProof;
        RequireAcknowledgmentReference = requireAcknowledgmentReference;
        RequireUseCheck = requireUseCheck;
        MaxUseCount = maxUseCount;
        ExpectedProofKeyId = NormalizeOptional(expectedProofKeyId);
        ExpectedProofKeyVersion = NormalizeOptional(expectedProofKeyVersion);
        ExpectedProofPolicyVersion = NormalizeOptional(expectedProofPolicyVersion);
        ExpectedProofPolicyHash = NormalizeOptional(expectedProofPolicyHash);
        RequiredProofProvider = NormalizeOptional(requiredProofProvider);
        RequiredProofHashAlgorithm = NormalizeOptional(requiredProofHashAlgorithm);
        ProofPayloadOptions = proofPayloadOptions;
    }

    public string? Issuer { get; }
    public string? Audience { get; }
    public IReadOnlyList<string> Scopes { get; }
    public DateTimeOffset? ValidationUtc { get; }
    public TimeSpan AllowedClockSkew { get; }
    public string? PolicyVersion { get; }
    public string? PolicyHash { get; }
    public string? AcknowledgmentId { get; }
    public string? HandshakeId { get; }
    public string? GatewayBinding { get; }
    public string? ResourceBinding { get; }
    public bool RequireProof { get; }
    public bool RequireAcknowledgmentReference { get; }
    public bool RequireUseCheck { get; }
    public int MaxUseCount { get; }
    public string? ExpectedProofKeyId { get; }
    public string? ExpectedProofKeyVersion { get; }
    public string? ExpectedProofPolicyVersion { get; }
    public string? ExpectedProofPolicyHash { get; }
    public string? RequiredProofProvider { get; }
    public string? RequiredProofHashAlgorithm { get; }

    /// <summary>
    /// Gets the canonical payload options used to rebuild the grant payload when proof is required.
    /// </summary>
    /// <remarks>
    /// Proof validation recomputes the canonical payload from the grant and compares its hash to the signed hash, so these
    /// options must match the options the issuer signed with. The default options bind every grant field except metadata,
    /// whose allow-list is empty until a host opts a key in.
    /// </remarks>
    public CanonicalPayloadOptions? ProofPayloadOptions { get; }

    public static CapabilityGrantValidationOptions Create(
        string? issuer = null,
        string? audience = null,
        IEnumerable<string>? scopes = null,
        DateTimeOffset? validationUtc = null,
        string? policyVersion = null,
        string? policyHash = null,
        string? acknowledgmentId = null,
        string? handshakeId = null,
        string? gatewayBinding = null,
        string? resourceBinding = null,
        bool requireProof = false,
        bool requireAcknowledgmentReference = false,
        bool requireUseCheck = false,
        int maxUseCount = 1,
        TimeSpan allowedClockSkew = default,
        string? expectedProofKeyId = null,
        string? expectedProofKeyVersion = null,
        string? expectedProofPolicyVersion = null,
        string? expectedProofPolicyHash = null,
        string? requiredProofProvider = null,
        string? requiredProofHashAlgorithm = null,
        CanonicalPayloadOptions? proofPayloadOptions = null)
    {
        return new CapabilityGrantValidationOptions(
            issuer,
            audience,
            NormalizeScopes(scopes),
            validationUtc,
            allowedClockSkew,
            policyVersion,
            policyHash,
            acknowledgmentId,
            handshakeId,
            gatewayBinding,
            resourceBinding,
            requireProof,
            requireAcknowledgmentReference,
            requireUseCheck,
            maxUseCount,
            expectedProofKeyId,
            expectedProofKeyVersion,
            expectedProofPolicyVersion,
            expectedProofPolicyHash,
            requiredProofProvider,
            requiredProofHashAlgorithm,
            proofPayloadOptions);
    }

    public static CapabilityGrantValidationOptions CreateExecutionBoundary(
        string? issuer = null,
        string? audience = null,
        IEnumerable<string>? scopes = null,
        DateTimeOffset? validationUtc = null,
        string? policyVersion = null,
        string? policyHash = null,
        string? acknowledgmentId = null,
        string? handshakeId = null,
        string? gatewayBinding = null,
        string? resourceBinding = null,
        bool requireAcknowledgmentReference = false,
        bool requireUseCheck = true,
        int maxUseCount = 1,
        TimeSpan allowedClockSkew = default,
        string? expectedProofKeyId = null,
        string? expectedProofKeyVersion = null,
        string? expectedProofPolicyVersion = null,
        string? expectedProofPolicyHash = null,
        string? requiredProofProvider = null,
        string? requiredProofHashAlgorithm = null,
        CanonicalPayloadOptions? proofPayloadOptions = null)
    {
        return Create(
            issuer: issuer,
            audience: audience,
            scopes: scopes,
            validationUtc: validationUtc,
            policyVersion: policyVersion,
            policyHash: policyHash,
            acknowledgmentId: acknowledgmentId,
            handshakeId: handshakeId,
            gatewayBinding: gatewayBinding,
            resourceBinding: resourceBinding,
            requireProof: true,
            requireAcknowledgmentReference: requireAcknowledgmentReference,
            requireUseCheck: requireUseCheck,
            maxUseCount: maxUseCount,
            allowedClockSkew: allowedClockSkew,
            expectedProofKeyId: expectedProofKeyId,
            expectedProofKeyVersion: expectedProofKeyVersion,
            expectedProofPolicyVersion: expectedProofPolicyVersion,
            expectedProofPolicyHash: expectedProofPolicyHash,
            requiredProofProvider: requiredProofProvider,
            requiredProofHashAlgorithm: requiredProofHashAlgorithm,
            proofPayloadOptions: proofPayloadOptions);
    }

    public static CapabilityGrantValidationOptions CreateMetadataValidation(
        string? issuer = null,
        string? audience = null,
        IEnumerable<string>? scopes = null,
        DateTimeOffset? validationUtc = null,
        string? policyVersion = null,
        string? policyHash = null,
        string? acknowledgmentId = null,
        string? handshakeId = null,
        string? gatewayBinding = null,
        string? resourceBinding = null,
        bool requireAcknowledgmentReference = false,
        TimeSpan allowedClockSkew = default)
    {
        return Create(
            issuer: issuer,
            audience: audience,
            scopes: scopes,
            validationUtc: validationUtc,
            policyVersion: policyVersion,
            policyHash: policyHash,
            acknowledgmentId: acknowledgmentId,
            handshakeId: handshakeId,
            gatewayBinding: gatewayBinding,
            resourceBinding: resourceBinding,
            requireProof: false,
            requireAcknowledgmentReference: requireAcknowledgmentReference,
            requireUseCheck: false,
            maxUseCount: 1,
            allowedClockSkew: allowedClockSkew);
    }

    private static IReadOnlyList<string> NormalizeScopes(IEnumerable<string>? scopes)
    {
        if (scopes is null)
        {
            return EmptyScopes;
        }

        string[] normalized = [.. scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)];

        return normalized.Length == 0 ? EmptyScopes : Array.AsReadOnly(normalized);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
