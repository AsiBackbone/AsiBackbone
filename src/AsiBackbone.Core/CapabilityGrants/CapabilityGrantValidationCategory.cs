namespace AsiBackbone.Core.CapabilityGrants;

/// <summary>
/// Describes the provider-neutral validation category assigned to a capability grant.
/// </summary>
public enum CapabilityGrantValidationCategory
{
    /// <summary>
    /// The grant satisfied all configured validation checks.
    /// </summary>
    Valid = 0,

    /// <summary>
    /// Required proof was missing.
    /// </summary>
    MissingProof = 1,

    /// <summary>
    /// Proof was present but did not verify.
    /// </summary>
    InvalidProof = 2,

    /// <summary>
    /// The grant issuer did not match the configured expectation.
    /// </summary>
    WrongIssuer = 3,

    /// <summary>
    /// The grant audience did not match the configured expectation.
    /// </summary>
    WrongAudience = 4,

    /// <summary>
    /// The grant expired before validation.
    /// </summary>
    Expired = 5,

    /// <summary>
    /// The grant is not yet valid.
    /// </summary>
    NotYetValid = 6,

    /// <summary>
    /// Required scope was missing.
    /// </summary>
    WrongScope = 7,

    /// <summary>
    /// Policy version or policy hash did not match the configured expectation.
    /// </summary>
    PolicyMismatch = 8,

    /// <summary>
    /// An acknowledgment reference was required but missing.
    /// </summary>
    MissingAcknowledgmentReference = 9,

    /// <summary>
    /// The acknowledgment reference did not match the configured expectation.
    /// </summary>
    AcknowledgmentMismatch = 10,

    /// <summary>
    /// The handshake reference did not match the configured expectation.
    /// </summary>
    HandshakeMismatch = 11,

    /// <summary>
    /// The gateway binding did not match the configured expectation.
    /// </summary>
    GatewayMismatch = 12,

    /// <summary>
    /// The resource binding did not match the configured expectation.
    /// </summary>
    ResourceMismatch = 13,

    /// <summary>
    /// The grant exceeded its configured use limit.
    /// </summary>
    ReuseLimitExceeded = 14,

    /// <summary>
    /// The grant was revoked.
    /// </summary>
    Revoked = 15,

    /// <summary>
    /// The grant was cancelled.
    /// </summary>
    Cancelled = 16,

    /// <summary>
    /// Reuse-state storage was unavailable or not configured when required.
    /// </summary>
    ReplayStoreUnavailable = 17,

    /// <summary>
    /// Validation failed but no more specific category could be inferred safely.
    /// </summary>
    Failed = 18
}
