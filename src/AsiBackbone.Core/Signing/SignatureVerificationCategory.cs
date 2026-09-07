namespace AsiBackbone.Core.Signing;

/// <summary>
/// Describes the provider-neutral category assigned to a signature verification result.
/// </summary>
public enum SignatureVerificationCategory
{
    /// <summary>
    /// No verification category was assigned.
    /// </summary>
    /// <remarks>
    /// Zero is a rejected sentinel so that a default-constructed value, or a persisted column that yields zero for
    /// unrecognized input, cannot mean "valid".
    /// </remarks>
    Unspecified = 0,

    /// <summary>
    /// The signature verified against the expected artifact hash and metadata.
    /// </summary>
    Valid = 11,

    /// <summary>
    /// The signature value was present but did not verify.
    /// </summary>
    InvalidSignature = 1,

    /// <summary>
    /// The verification hash did not match the hash recorded in signing metadata.
    /// </summary>
    HashMismatch = 2,

    /// <summary>
    /// Required signature metadata was missing.
    /// </summary>
    MissingSignature = 3,

    /// <summary>
    /// The signing key identifier or key version could not be resolved or did not match policy expectations.
    /// </summary>
    UnknownKeyVersion = 4,

    /// <summary>
    /// The signing key was revoked, disabled, or otherwise no longer trusted.
    /// </summary>
    RevokedKey = 5,

    /// <summary>
    /// The verification provider was unavailable or could not complete verification.
    /// </summary>
    ProviderUnavailable = 6,

    /// <summary>
    /// Canonical payload descriptors did not match the artifact being verified.
    /// </summary>
    CanonicalizationMismatch = 7,

    /// <summary>
    /// The hash or signature algorithm is unsupported by the configured verifier or policy.
    /// </summary>
    UnsupportedAlgorithm = 8,

    /// <summary>
    /// Verification failed but no more specific category could be inferred safely.
    /// </summary>
    Failed = 9,

    /// <summary>
    /// The signature was produced under a key the verification policy does not trust for this purpose.
    /// </summary>
    /// <remarks>
    /// This is distinct from <see cref="UnknownKeyVersion" />, which describes a key the verifier could not resolve. An
    /// artifact signed under a resolvable but unpinned key is a trust decision rather than a lookup failure, and defaults
    /// to <see cref="VerificationPolicyAction.Deny" />.
    /// </remarks>
    UntrustedKey = 10
}
