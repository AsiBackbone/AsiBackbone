using AsiBackbone.Core.CapabilityGrants;

namespace AsiBackbone.AspNetCore.CapabilityGrants;

/// <summary>
/// Defines the stable reason codes <see cref="SignedCapabilityGrantEndpointValidator" /> places on the denied decisions it
/// returns.
/// </summary>
/// <remarks>
/// These values are public contract: they appear in decision receipts, audit ledger records, and problem responses, and
/// they do not change within a major version. Every <see cref="CapabilityGrantValidationCategory" /> other than
/// <see cref="CapabilityGrantValidationCategory.Valid" /> maps to one code through <see cref="ForCategory" />, whatever
/// <see cref="Core.Signing.VerificationPolicyAction" /> the core validator attached to the result.
/// </remarks>
public static class SignedCapabilityGrantReasonCodes
{
    /// <summary>No capability grant token was presented.</summary>
    public const string Missing = "endpoint.capability_grant.missing";

    /// <summary>The capability grant token could not be decoded.</summary>
    public const string Malformed = "endpoint.capability_grant.malformed";

    /// <summary>The request has no known, authenticated actor to bind the grant to.</summary>
    public const string ActorUnresolved = "endpoint.capability_grant.actor_unresolved";

    /// <summary>No request-binding provider is registered, or the provider could not bind the request.</summary>
    public const string BindingUnavailable = "endpoint.capability_grant.binding_unavailable";

    /// <summary>Required proof was missing, or no signature verification service is registered.</summary>
    public const string ProofMissing = "endpoint.capability_grant.proof_missing";

    /// <summary>The grant proof did not verify or is not bound to the grant content.</summary>
    public const string ProofInvalid = "endpoint.capability_grant.proof_invalid";

    /// <summary>The grant issuer did not match the configured issuer.</summary>
    public const string IssuerMismatch = "endpoint.capability_grant.issuer_mismatch";

    /// <summary>The grant audience did not match the configured audience.</summary>
    public const string AudienceMismatch = "endpoint.capability_grant.audience_mismatch";

    /// <summary>The grant subject did not match the authenticated actor.</summary>
    public const string SubjectMismatch = "endpoint.capability_grant.subject_mismatch";

    /// <summary>The grant operation did not match the endpoint operation.</summary>
    public const string OperationMismatch = "endpoint.capability_grant.operation_mismatch";

    /// <summary>The grant has expired.</summary>
    public const string Expired = "endpoint.capability_grant.expired";

    /// <summary>The grant is not yet valid.</summary>
    public const string NotYetValid = "endpoint.capability_grant.not_yet_valid";

    /// <summary>The grant does not carry every scope the endpoint requires.</summary>
    public const string ScopeMissing = "endpoint.capability_grant.scope_missing";

    /// <summary>The grant policy version or hash did not match the current decision.</summary>
    public const string PolicyMismatch = "endpoint.capability_grant.policy_mismatch";

    /// <summary>The grant carries no acknowledgment reference where one is required.</summary>
    public const string AcknowledgmentMissing = "endpoint.capability_grant.acknowledgment_missing";

    /// <summary>The grant acknowledgment reference did not match.</summary>
    public const string AcknowledgmentMismatch = "endpoint.capability_grant.acknowledgment_mismatch";

    /// <summary>The grant handshake reference did not match.</summary>
    public const string HandshakeMismatch = "endpoint.capability_grant.handshake_mismatch";

    /// <summary>The grant gateway binding did not match.</summary>
    public const string GatewayMismatch = "endpoint.capability_grant.gateway_mismatch";

    /// <summary>The grant resource binding did not match the request binding.</summary>
    public const string ResourceMismatch = "endpoint.capability_grant.resource_mismatch";

    /// <summary>The grant has been used as many times as it permits, including a replayed single-use grant.</summary>
    public const string UseLimitExceeded = "endpoint.capability_grant.use_limit_exceeded";

    /// <summary>The grant, or the key that signed it, was revoked or stopped.</summary>
    public const string Revoked = "endpoint.capability_grant.revoked";

    /// <summary>The grant was cancelled.</summary>
    public const string Cancelled = "endpoint.capability_grant.cancelled";

    /// <summary>No use store is registered, or the use store was unavailable.</summary>
    public const string UseStoreUnavailable = "endpoint.capability_grant.use_store_unavailable";

    /// <summary>Validation failed without a more specific category, or failed unexpectedly.</summary>
    public const string Failed = "endpoint.capability_grant.failed";

    /// <summary>
    /// Returns the reason code for a core validation category.
    /// </summary>
    /// <param name="category">The validation category.</param>
    /// <returns>The reason code; <see cref="Failed" /> for <see cref="CapabilityGrantValidationCategory.Valid" /> or an undefined value.</returns>
    public static string ForCategory(CapabilityGrantValidationCategory category)
    {
        return category switch
        {
            CapabilityGrantValidationCategory.MissingProof => ProofMissing,
            CapabilityGrantValidationCategory.InvalidProof => ProofInvalid,
            CapabilityGrantValidationCategory.WrongIssuer => IssuerMismatch,
            CapabilityGrantValidationCategory.WrongAudience => AudienceMismatch,
            CapabilityGrantValidationCategory.Expired => Expired,
            CapabilityGrantValidationCategory.NotYetValid => NotYetValid,
            CapabilityGrantValidationCategory.WrongScope => ScopeMissing,
            CapabilityGrantValidationCategory.PolicyMismatch => PolicyMismatch,
            CapabilityGrantValidationCategory.MissingAcknowledgmentReference => AcknowledgmentMissing,
            CapabilityGrantValidationCategory.AcknowledgmentMismatch => AcknowledgmentMismatch,
            CapabilityGrantValidationCategory.HandshakeMismatch => HandshakeMismatch,
            CapabilityGrantValidationCategory.GatewayMismatch => GatewayMismatch,
            CapabilityGrantValidationCategory.ResourceMismatch => ResourceMismatch,
            CapabilityGrantValidationCategory.ReuseLimitExceeded => UseLimitExceeded,
            CapabilityGrantValidationCategory.Revoked => Revoked,
            CapabilityGrantValidationCategory.Cancelled => Cancelled,
            CapabilityGrantValidationCategory.ReplayStoreUnavailable => UseStoreUnavailable,
            CapabilityGrantValidationCategory.SubjectMismatch => SubjectMismatch,
            CapabilityGrantValidationCategory.OperationMismatch => OperationMismatch,
            CapabilityGrantValidationCategory.Valid => Failed,
            CapabilityGrantValidationCategory.Failed => Failed,
            _ => Failed
        };
    }
}
