using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.CapabilityTokens;

public static class CapabilityGrantValidator
{
    public static async ValueTask<CapabilityGrantValidationResult> ValidateAsync(
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant,
        CapabilityGrantValidationOptions? options = null,
        IAsiBackboneSignatureVerificationService? verificationService = null,
        ICapabilityGrantUseStore? useStore = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signedGrant);
        cancellationToken.ThrowIfCancellationRequested();

        CapabilityTokenGrant grant = signedGrant.Artifact;

        // The previous default built permissive options: no proof, no use check, and no issuer, audience, or scope
        // expectations, so the simplest call was the least safe one and returned Valid for anything unexpired. Validation
        // now requires the caller to state what it is validating against.
        if (options is null)
        {
            return CapabilityGrantValidationResult.Failed(
                grant,
                CapabilityTokenValidationCategory.Failed,
                VerificationPolicyAction.Deny,
                "capability.validation-options-required",
                "Capability grant validation requires explicit options describing what the grant is validated against.");
        }

        CapabilityGrantValidationOptions effectiveOptions = options;
        DateTimeOffset validationUtc = (effectiveOptions.ValidationUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();

        if (effectiveOptions.RequireProof)
        {
            CapabilityGrantValidationResult? proofResult = await ValidateProofAsync(
                signedGrant,
                grant,
                effectiveOptions,
                verificationService,
                cancellationToken).ConfigureAwait(false);

            if (proofResult is not null)
            {
                return proofResult;
            }
        }

        CapabilityGrantValidationResult? metadataResult = ValidateMetadata(grant, effectiveOptions, validationUtc);

        if (metadataResult is not null)
        {
            return metadataResult;
        }

        if (effectiveOptions.RequireUseCheck)
        {
            CapabilityGrantValidationResult? useResult = await ValidateUseAsync(
                grant,
                effectiveOptions,
                useStore,
                validationUtc,
                cancellationToken).ConfigureAwait(false);

            if (useResult is not null)
            {
                return useResult;
            }
        }

        return CapabilityGrantValidationResult.Valid(grant);
    }

    private static async ValueTask<CapabilityGrantValidationResult?> ValidateProofAsync(
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant,
        CapabilityTokenGrant grant,
        CapabilityGrantValidationOptions options,
        IAsiBackboneSignatureVerificationService? verificationService,
        CancellationToken cancellationToken)
    {
        if (verificationService is null)
        {
            return CapabilityGrantValidationResult.Failed(
                grant,
                CapabilityTokenValidationCategory.MissingProof,
                VerificationPolicyAction.Deny,
                "capability.proof-verifier-missing",
                "A proof verifier is required for this validation context.");
        }

        CapabilityGrantValidationResult? bindingResult = ValidateProofBinding(signedGrant, grant, options);

        if (bindingResult is not null)
        {
            return bindingResult;
        }

        var verificationContext = VerificationPolicyContext.Create(
            purpose: CanonicalArtifactTypes.CapabilityTokenGrant,
            expectedKeyId: options.ExpectedProofKeyId,
            expectedKeyVersion: options.ExpectedProofKeyVersion,
            expectedPolicyVersion: options.ExpectedProofPolicyVersion,
            expectedPolicyHash: options.ExpectedProofPolicyHash,
            requiredProvider: options.RequiredProofProvider,
            requiredHashAlgorithm: options.RequiredProofHashAlgorithm);

        VerificationPolicyOutcome verificationOutcome = await GovernanceArtifactVerifier.VerifyAsync(
            signedGrant,
            verificationService,
            context: verificationContext,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (verificationOutcome.ShouldAllow)
        {
            return null;
        }

        // A grant whose signature was stripped is not a grant awaiting acknowledgment. Where proof is required, absent
        // proof denies, rather than inviting a host that treats RequireAcknowledgment as "proceed after a click" to
        // continue on a grant carrying no proof at all.
        VerificationPolicyAction action = verificationOutcome.Category is SignatureVerificationCategory.MissingSignature
            ? VerificationPolicyAction.Deny
            : verificationOutcome.Action;

        return CapabilityGrantValidationResult.Failed(
            grant,
            MapVerificationCategory(verificationOutcome.Category),
            action,
            verificationOutcome.FailureCode ?? "capability.proof-invalid",
            verificationOutcome.FailureMessage);
    }

    /// <summary>
    /// Binds the signed proof to the grant being validated by rebuilding the canonical payload from the grant itself.
    /// </summary>
    /// <remarks>
    /// Signature verification establishes that a signature covers a hash. It does not establish that the hash describes the
    /// grant whose fields are about to be evaluated. Rebuilding the payload from <see cref="SignedGovernanceArtifact{TArtifact}.Artifact" />
    /// and comparing hashes closes that gap, and the artifact descriptors are asserted so a proof issued for another artifact
    /// type or token identifier cannot be presented alongside this grant.
    /// </remarks>
    private static CapabilityGrantValidationResult? ValidateProofBinding(
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant,
        CapabilityTokenGrant grant,
        CapabilityGrantValidationOptions options)
    {
        if (!string.Equals(signedGrant.ArtifactType, CanonicalArtifactTypes.CapabilityTokenGrant, StringComparison.Ordinal))
        {
            return CapabilityGrantValidationResult.Failed(
                grant,
                CapabilityTokenValidationCategory.InvalidProof,
                VerificationPolicyAction.Deny,
                "capability.proof-artifact-type-mismatch",
                "The signed artifact type is not a capability token grant.");
        }

        if (!string.Equals(signedGrant.ArtifactId, grant.TokenId, StringComparison.Ordinal))
        {
            return CapabilityGrantValidationResult.Failed(
                grant,
                CapabilityTokenValidationCategory.InvalidProof,
                VerificationPolicyAction.Deny,
                "capability.proof-artifact-id-mismatch",
                "The signed artifact identifier does not match the grant token identifier.");
        }

        CanonicalPayloadHash recomputedHash;

        try
        {
            recomputedHash = CanonicalPayloadHasher.ComputeHash(
                CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant, options.ProofPayloadOptions),
                signedGrant.HashAlgorithm);
        }
        catch (NotSupportedException)
        {
            return CapabilityGrantValidationResult.Failed(
                grant,
                CapabilityTokenValidationCategory.InvalidProof,
                VerificationPolicyAction.Deny,
                "capability.proof-hash-algorithm-unsupported",
                "The grant canonical payload cannot be rebuilt with the built-in hasher, so the proof is not bound to the grant content.");
        }

        return string.Equals(recomputedHash.HashValue, signedGrant.CanonicalHash.HashValue, StringComparison.Ordinal)
            ? null
            : CapabilityGrantValidationResult.Failed(
                grant,
                CapabilityTokenValidationCategory.InvalidProof,
                VerificationPolicyAction.Deny,
                "capability.proof-content-mismatch",
                "The grant does not hash to the signed canonical hash value.");
    }

    private static CapabilityGrantValidationResult? ValidateMetadata(
        CapabilityTokenGrant grant,
        CapabilityGrantValidationOptions options,
        DateTimeOffset validationUtc)
    {
        return options.Issuer is not null && !string.Equals(options.Issuer, grant.Issuer, StringComparison.Ordinal)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.WrongIssuer, VerificationPolicyAction.Deny, "capability.issuer-mismatch")
            : options.Audience is not null && !string.Equals(options.Audience, grant.Audience, StringComparison.Ordinal)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.WrongAudience, VerificationPolicyAction.Deny, "capability.audience-mismatch")
            : IsNotYetValid(grant, validationUtc, options.AllowedClockSkew)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.NotYetValid, VerificationPolicyAction.Defer, "capability.not-yet-valid")
            : IsExpired(grant, validationUtc, options.AllowedClockSkew)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.Expired, VerificationPolicyAction.Deny, "capability.expired")
            : options.Scopes.Count > 0 && !ContainsRequiredScopes(grant.Scopes, options.Scopes)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.WrongScope, VerificationPolicyAction.Deny, "capability.scope-missing")
            : (options.PolicyVersion is not null && !string.Equals(options.PolicyVersion, grant.PolicyVersion, StringComparison.Ordinal))
            || (options.PolicyHash is not null && !string.Equals(options.PolicyHash, grant.PolicyHash, StringComparison.Ordinal))
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.PolicyMismatch, VerificationPolicyAction.Deny, "capability.policy-mismatch")
            : options.RequireAcknowledgmentReference && !grant.HasAcknowledgmentReference
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.MissingAcknowledgmentReference, VerificationPolicyAction.RequireAcknowledgment, "capability.acknowledgment-missing")
            : options.AcknowledgmentId is not null && !string.Equals(options.AcknowledgmentId, grant.AcknowledgmentId, StringComparison.Ordinal)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.AcknowledgmentMismatch, VerificationPolicyAction.Deny, "capability.acknowledgment-mismatch")
            : options.HandshakeId is not null && !string.Equals(options.HandshakeId, grant.HandshakeId, StringComparison.Ordinal)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.HandshakeMismatch, VerificationPolicyAction.Deny, "capability.handshake-mismatch")
            : options.GatewayBinding is not null && !string.Equals(options.GatewayBinding, grant.GatewayBinding, StringComparison.Ordinal)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.GatewayMismatch, VerificationPolicyAction.Deny, "capability.gateway-mismatch")
            : options.ResourceBinding is not null && !string.Equals(options.ResourceBinding, grant.ResourceBinding, StringComparison.Ordinal)
            ? CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.ResourceMismatch, VerificationPolicyAction.Deny, "capability.resource-mismatch")
            : null;
    }

    private static bool IsNotYetValid(CapabilityTokenGrant grant, DateTimeOffset validationUtc, TimeSpan allowedClockSkew)
    {
        return grant.NotBeforeUtc.HasValue
            && grant.NotBeforeUtc.Value > validationUtc
            && grant.NotBeforeUtc.Value - validationUtc > allowedClockSkew;
    }

    private static bool IsExpired(CapabilityTokenGrant grant, DateTimeOffset validationUtc, TimeSpan allowedClockSkew)
    {
        return validationUtc >= grant.ExpiresUtc
            && validationUtc - grant.ExpiresUtc >= allowedClockSkew;
    }

    private static async ValueTask<CapabilityGrantValidationResult?> ValidateUseAsync(
        CapabilityTokenGrant grant,
        CapabilityGrantValidationOptions options,
        ICapabilityGrantUseStore? useStore,
        DateTimeOffset validationUtc,
        CancellationToken cancellationToken)
    {
        if (useStore is null)
        {
            return CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.ReplayStoreUnavailable, VerificationPolicyAction.Defer, "capability.use-store-missing");
        }

        CapabilityGrantUseResult result = await useStore
            .TryConsumeAsync(grant, options.MaxUseCount, validationUtc, cancellationToken)
            .ConfigureAwait(false);

        return result.State switch
        {
            GrantUseState.Accepted => null,
            GrantUseState.UseLimitExceeded => CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.ReuseLimitExceeded, VerificationPolicyAction.Deny, result.FailureCode ?? "capability.use-limit-exceeded", result.FailureMessage),
            GrantUseState.Stopped => CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.Revoked, VerificationPolicyAction.Deny, result.FailureCode ?? "capability.grant-stopped", result.FailureMessage),
            GrantUseState.Cancelled => CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.Cancelled, VerificationPolicyAction.Deny, result.FailureCode ?? "capability.grant-cancelled", result.FailureMessage),
            GrantUseState.Unavailable => CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.ReplayStoreUnavailable, VerificationPolicyAction.Defer, result.FailureCode ?? "capability.use-store-unavailable", result.FailureMessage),
            _ => CapabilityGrantValidationResult.Failed(grant, CapabilityTokenValidationCategory.Failed, VerificationPolicyAction.Escalate, "capability.validation-failed")
        };
    }

    private static bool ContainsRequiredScopes(IReadOnlyList<string> actualScopes, IReadOnlyList<string> requiredScopes)
    {
        HashSet<string> actualScopeSet = new(actualScopes, StringComparer.Ordinal);

        foreach (string requiredScope in requiredScopes)
        {
            if (!actualScopeSet.Contains(requiredScope))
            {
                return false;
            }
        }

        return true;
    }

    private static CapabilityTokenValidationCategory MapVerificationCategory(SignatureVerificationCategory category)
    {
        return category switch
        {
            SignatureVerificationCategory.MissingSignature => CapabilityTokenValidationCategory.MissingProof,
            SignatureVerificationCategory.Valid => CapabilityTokenValidationCategory.Valid,
            SignatureVerificationCategory.InvalidSignature => CapabilityTokenValidationCategory.InvalidProof,
            SignatureVerificationCategory.HashMismatch => CapabilityTokenValidationCategory.InvalidProof,
            SignatureVerificationCategory.RevokedKey => CapabilityTokenValidationCategory.Revoked,
            SignatureVerificationCategory.UntrustedKey => CapabilityTokenValidationCategory.InvalidProof,
            SignatureVerificationCategory.ProviderUnavailable => CapabilityTokenValidationCategory.Failed,
            SignatureVerificationCategory.UnknownKeyVersion => CapabilityTokenValidationCategory.Failed,
            SignatureVerificationCategory.CanonicalizationMismatch => CapabilityTokenValidationCategory.Failed,
            SignatureVerificationCategory.UnsupportedAlgorithm => CapabilityTokenValidationCategory.InvalidProof,
            SignatureVerificationCategory.Failed => CapabilityTokenValidationCategory.Failed,
            _ => CapabilityTokenValidationCategory.Failed
        };
    }
}
