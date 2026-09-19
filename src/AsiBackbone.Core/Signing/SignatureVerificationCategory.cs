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
    /// <remarks>
    /// Defaults to <see cref="VerificationPolicyAction.Deny" />. An artifact whose signature was stripped carries no proof,
    /// so treating it as awaiting acknowledgment would invite a host to proceed on unproven content. Hosts that
    /// deliberately accept unsigned artifacts on a lower-assurance path can map this category to another action through
    /// <see cref="VerificationPolicyOptions.Create" />.
    /// </remarks>
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
    /// <remarks>
    /// This category describes a transient operational condition only. A signature produced by a provider the
    /// verification policy does not require is a trust decision and is reported as
    /// <see cref="UntrustedSigningContext" />.
    /// </remarks>
    ProviderUnavailable = 6,

    /// <summary>
    /// Canonical payload descriptors did not match the artifact being verified.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="VerificationPolicyAction.Deny" />. Signing metadata that describes a different artifact
    /// identifier, artifact type, canonicalization version, or payload schema version than the artifact presented means
    /// the signature cannot be treated as evidence for that artifact.
    /// </remarks>
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
    UntrustedKey = 10,

    /// <summary>
    /// The signature was produced under a signing context the verification policy does not trust, such as a provider other
    /// than the required provider or a policy version or policy hash other than the expected one.
    /// </summary>
    /// <remarks>
    /// Like <see cref="UntrustedKey" />, this is a trust decision rather than an operational failure, so it defaults to
    /// <see cref="VerificationPolicyAction.Deny" /> instead of <see cref="VerificationPolicyAction.Defer" /> or
    /// <see cref="VerificationPolicyAction.Escalate" />. A retry or a human approval cannot make an artifact signed under
    /// the wrong provider or policy context trustworthy.
    /// </remarks>
    UntrustedSigningContext = 12
}
