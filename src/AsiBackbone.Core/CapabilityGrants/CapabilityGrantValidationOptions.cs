using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.CapabilityGrants;

/// <summary>
/// Describes the expectations a capability grant is validated against by <see cref="CapabilityGrantValidator" />.
/// </summary>
/// <remarks>
/// <para>
/// Each optional expectation is checked only when it is set; string comparisons are ordinal, and blank values are
/// treated as absent. Choose a factory that matches the validation context:
/// <see cref="CreateBoundExecutionBoundary" /> for an execution boundary that requires proof and binds the grant to
/// the authenticated subject, <see cref="CreateMetadataValidation" /> for proof-free inspection of grant metadata, and
/// <see cref="Create" /> for a fully custom profile.
/// </para>
/// <para>
/// Requiring proof or a use check without an audience expectation is rejected, because proof alone does not establish
/// that the grant was issued for this audience.
/// </para>
/// </remarks>
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
        string? expectedSubjectId,
        string? expectedOperationName,
        bool requireSubjectBinding,
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
        ExpectedSubjectId = NormalizeOptional(expectedSubjectId);
        if (requireSubjectBinding && ExpectedSubjectId is null)
        {
            throw new ArgumentException(
                "A subject expectation is required for this validation profile.",
                nameof(expectedSubjectId));
        }

        ExpectedOperationName = NormalizeOptional(expectedOperationName);
        RequireSubjectBinding = requireSubjectBinding;
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

    /// <summary>
    /// Gets the issuer the grant must name, when an issuer expectation is configured.
    /// </summary>
    public string? Issuer { get; }

    /// <summary>
    /// Gets the audience the grant must name, when an audience expectation is configured.
    /// </summary>
    /// <remarks>
    /// An audience is required whenever <see cref="RequireProof" /> or <see cref="RequireUseCheck" /> is set.
    /// </remarks>
    public string? Audience { get; }

    /// <summary>
    /// Gets the scopes the grant must contain, trimmed, de-duplicated, and sorted ordinally.
    /// </summary>
    /// <remarks>
    /// The grant may carry additional scopes. An empty list disables the scope check.
    /// </remarks>
    public IReadOnlyList<string> Scopes { get; }

    /// <summary>
    /// Gets the UTC instant used to evaluate the grant validity window, or <see langword="null" /> to use the current
    /// time.
    /// </summary>
    public DateTimeOffset? ValidationUtc { get; }

    /// <summary>
    /// Gets the non-negative tolerance applied to the grant not-before and expiry times.
    /// </summary>
    public TimeSpan AllowedClockSkew { get; }

    /// <summary>
    /// Gets the policy version the grant must carry, when a policy version expectation is configured.
    /// </summary>
    public string? PolicyVersion { get; }

    /// <summary>
    /// Gets the policy hash the grant must carry, when a policy hash expectation is configured.
    /// </summary>
    public string? PolicyHash { get; }

    /// <summary>
    /// Gets the acknowledgment identifier the grant must reference, when an acknowledgment expectation is configured.
    /// </summary>
    public string? AcknowledgmentId { get; }

    /// <summary>
    /// Gets the liability handshake identifier the grant must reference, when a handshake expectation is configured.
    /// </summary>
    public string? HandshakeId { get; }

    /// <summary>
    /// Gets the gateway binding the grant must carry, when a gateway expectation is configured.
    /// </summary>
    public string? GatewayBinding { get; }

    /// <summary>
    /// Gets the resource binding the grant must carry, when a resource expectation is configured.
    /// </summary>
    public string? ResourceBinding { get; }

    /// <summary>
    /// Gets the authenticated subject that the grant must identify, when subject binding is configured.
    /// </summary>
    public string? ExpectedSubjectId { get; }

    /// <summary>
    /// Gets the requested operation that the grant must identify, when operation binding is configured.
    /// </summary>
    public string? ExpectedOperationName { get; }

    /// <summary>
    /// Gets a value indicating whether the grant must carry a signature that verifies and is bound to the grant
    /// content.
    /// </summary>
    /// <remarks>
    /// When set, validation requires an <see cref="IGovernanceSignatureVerificationService" /> and rebuilds the
    /// canonical payload from the grant using <see cref="ProofPayloadOptions" />.
    /// </remarks>
    public bool RequireProof { get; }

    /// <summary>
    /// Gets a value indicating whether the grant must reference a handshake acknowledgment.
    /// </summary>
    /// <remarks>
    /// A grant without an acknowledgment reference fails with <see cref="VerificationPolicyAction.RequireAcknowledgment" />.
    /// </remarks>
    public bool RequireAcknowledgmentReference { get; }

    /// <summary>
    /// Gets a value indicating whether validation must consume a grant use through an
    /// <see cref="ICapabilityGrantUseStore" />.
    /// </summary>
    public bool RequireUseCheck { get; }

    /// <summary>
    /// Gets the maximum use count allowed by this validation context.
    /// </summary>
    /// <remarks>
    /// When the grant carries its own signed use limit, the narrower of the two applies, so local policy can tighten
    /// but never widen what was issued.
    /// </remarks>
    public int MaxUseCount { get; }

    /// <summary>
    /// Gets the signing key identifier the grant proof must use, when configured.
    /// </summary>
    public string? ExpectedProofKeyId { get; }

    /// <summary>
    /// Gets the signing key version the grant proof must use, when configured.
    /// </summary>
    public string? ExpectedProofKeyVersion { get; }

    /// <summary>
    /// Gets the policy version the grant proof must carry, when configured.
    /// </summary>
    public string? ExpectedProofPolicyVersion { get; }

    /// <summary>
    /// Gets the policy hash the grant proof must carry, when configured.
    /// </summary>
    public string? ExpectedProofPolicyHash { get; }

    /// <summary>
    /// Gets the signing provider that must have produced the grant proof, when configured.
    /// </summary>
    public string? RequiredProofProvider { get; }

    /// <summary>
    /// Gets the hash algorithm the grant proof must use, when configured.
    /// </summary>
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

    /// <summary>
    /// Creates a custom validation profile from the supplied expectations.
    /// </summary>
    /// <remarks>
    /// This factory does not bind the grant to an authenticated subject. At an execution boundary, prefer
    /// <see cref="CreateBoundExecutionBoundary" />, or call <see cref="WithExpectedBindings" /> on the result.
    /// </remarks>
    /// <param name="issuer">The issuer the grant must name, or <see langword="null" /> to skip the issuer check.</param>
    /// <param name="audience">The audience the grant must name; required when proof or a use check is required.</param>
    /// <param name="scopes">The scopes the grant must contain; blank entries are ignored.</param>
    /// <param name="validationUtc">The instant used for the validity window; defaults to the current time.</param>
    /// <param name="policyVersion">The policy version the grant must carry, when set.</param>
    /// <param name="policyHash">The policy hash the grant must carry, when set.</param>
    /// <param name="acknowledgmentId">The acknowledgment identifier the grant must reference, when set.</param>
    /// <param name="handshakeId">The handshake identifier the grant must reference, when set.</param>
    /// <param name="gatewayBinding">The gateway binding the grant must carry, when set.</param>
    /// <param name="resourceBinding">The resource binding the grant must carry, when set.</param>
    /// <param name="requireProof">Whether the grant must carry verified proof bound to its content.</param>
    /// <param name="requireAcknowledgmentReference">Whether the grant must reference a handshake acknowledgment.</param>
    /// <param name="requireUseCheck">Whether validation must consume a grant use through a use store.</param>
    /// <param name="maxUseCount">The maximum use count for this validation context; must be at least one.</param>
    /// <param name="allowedClockSkew">The non-negative tolerance applied to the validity window.</param>
    /// <param name="expectedProofKeyId">The signing key identifier the proof must use, when set.</param>
    /// <param name="expectedProofKeyVersion">The signing key version the proof must use, when set.</param>
    /// <param name="expectedProofPolicyVersion">The policy version the proof must carry, when set.</param>
    /// <param name="expectedProofPolicyHash">The policy hash the proof must carry, when set.</param>
    /// <param name="requiredProofProvider">The signing provider that must have produced the proof, when set.</param>
    /// <param name="requiredProofHashAlgorithm">The hash algorithm the proof must use, when set.</param>
    /// <param name="proofPayloadOptions">The canonical payload options the issuer signed with; defaults apply when <see langword="null" />.</param>
    /// <returns>The validation options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="allowedClockSkew" /> is negative, or <paramref name="maxUseCount" /> is less than one.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="requireProof" /> or <paramref name="requireUseCheck" /> is set without an <paramref name="audience" />.
    /// </exception>
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
            expectedSubjectId: null,
            expectedOperationName: null,
            requireSubjectBinding: false,
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

    /// <summary>
    /// Creates a copy with optional host expectations for the authenticated subject and requested operation.
    /// </summary>
    public CapabilityGrantValidationOptions WithExpectedBindings(
        string? expectedSubjectId = null,
        string? expectedOperationName = null)
    {
        string? normalizedSubjectId = NormalizeOptional(expectedSubjectId);
        if (RequireSubjectBinding && normalizedSubjectId is null)
        {
            normalizedSubjectId = ExpectedSubjectId;
        }

        return WithExpectedBindingsCore(
            normalizedSubjectId,
            expectedOperationName,
            RequireSubjectBinding);
    }

    private CapabilityGrantValidationOptions WithExpectedBindingsCore(
        string? expectedSubjectId,
        string? expectedOperationName,
        bool requireSubjectBinding)
    {
        return new CapabilityGrantValidationOptions(
            Issuer,
            Audience,
            Scopes,
            ValidationUtc,
            AllowedClockSkew,
            PolicyVersion,
            PolicyHash,
            AcknowledgmentId,
            HandshakeId,
            GatewayBinding,
            ResourceBinding,
            expectedSubjectId,
            expectedOperationName,
            requireSubjectBinding,
            RequireProof,
            RequireAcknowledgmentReference,
            RequireUseCheck,
            MaxUseCount,
            ExpectedProofKeyId,
            ExpectedProofKeyVersion,
            ExpectedProofPolicyVersion,
            ExpectedProofPolicyHash,
            RequiredProofProvider,
            RequiredProofHashAlgorithm,
            ProofPayloadOptions);
    }

    /// <summary>
    /// Obsolete. Retains the legacy execution-boundary signature for binary compatibility and always fails closed.
    /// </summary>
    /// <remarks>
    /// This method is retained only for compatibility and always throws <see cref="InvalidOperationException" />.
    /// Replace calls with <c>CreateBoundExecutionBoundary(CapabilityGrantBindingExpectations, ...)</c>, whose required
    /// first argument supplies the authoritative subject and optional operation binding. The warning remains non-error
    /// in the 7.x line; removal is planned for the next permitted major version.
    /// </remarks>
    [Obsolete(
        "CreateExecutionBoundary always fails closed and is retained only for binary compatibility. Use CreateBoundExecutionBoundary(CapabilityGrantBindingExpectations, ...) instead. Planned removal in 8.0.",
        DiagnosticId = "ASIB901",
        UrlFormat = "https://asibackbone.github.io/AsiBackbone/articles/asib901-create-execution-boundary.html")]
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
        throw new InvalidOperationException(
            "Execution-boundary validation requires subject binding expectations. Use CreateBoundExecutionBoundary.");
    }

    /// <summary>
    /// Creates a proof-verifying execution-boundary profile with authoritative host binding expectations.
    /// </summary>
    public static CapabilityGrantValidationOptions CreateBoundExecutionBoundary(
        CapabilityGrantBindingExpectations bindingExpectations,
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
        ArgumentNullException.ThrowIfNull(bindingExpectations);

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
            proofPayloadOptions: proofPayloadOptions)
            .WithExpectedBindingsCore(
                bindingExpectations.SubjectId,
                bindingExpectations.OperationName,
                requireSubjectBinding: true);
    }

    /// <summary>
    /// Creates a metadata-only profile that checks grant claims without requiring proof or consuming a use.
    /// </summary>
    /// <remarks>
    /// Because the grant signature is not verified, a valid result only means the grant claims match the expectations.
    /// Do not use this profile to authorize execution; use <see cref="CreateBoundExecutionBoundary" /> instead.
    /// </remarks>
    /// <param name="issuer">The issuer the grant must name, or <see langword="null" /> to skip the issuer check.</param>
    /// <param name="audience">The audience the grant must name, or <see langword="null" /> to skip the audience check.</param>
    /// <param name="scopes">The scopes the grant must contain; blank entries are ignored.</param>
    /// <param name="validationUtc">The instant used for the validity window; defaults to the current time.</param>
    /// <param name="policyVersion">The policy version the grant must carry, when set.</param>
    /// <param name="policyHash">The policy hash the grant must carry, when set.</param>
    /// <param name="acknowledgmentId">The acknowledgment identifier the grant must reference, when set.</param>
    /// <param name="handshakeId">The handshake identifier the grant must reference, when set.</param>
    /// <param name="gatewayBinding">The gateway binding the grant must carry, when set.</param>
    /// <param name="resourceBinding">The resource binding the grant must carry, when set.</param>
    /// <param name="requireAcknowledgmentReference">Whether the grant must reference a handshake acknowledgment.</param>
    /// <param name="allowedClockSkew">The non-negative tolerance applied to the validity window.</param>
    /// <returns>The validation options.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="allowedClockSkew" /> is negative.</exception>
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

    private bool RequireSubjectBinding { get; }
}
