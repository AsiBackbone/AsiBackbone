namespace AsiBackbone.Core.Classification;

/// <summary>
/// Represents provider-neutral policy behavior when DLP or classification screening fails or cannot produce a usable result.
/// </summary>
public enum DlpFailureBehavior
{
    /// <summary>
    /// No behavior was configured. This is the default value and is never a usable policy outcome.
    /// </summary>
    /// <remarks>
    /// The default value occupies the zero slot deliberately so that an unset property, an absent configuration value, or
    /// a deserialized payload that omitted the behavior cannot silently resolve to <see cref="Allow" />. Configuring or
    /// resolving it is rejected rather than defaulted, which keeps an incomplete policy from failing open.
    /// </remarks>
    Unspecified = 0,

    /// <summary>
    /// Allow the operation to proceed without adding a warning decision.
    /// </summary>
    Allow = 1,

    /// <summary>
    /// Allow the operation to proceed while preserving an audit-worthy warning.
    /// </summary>
    WarnAndAllow = 2,

    /// <summary>
    /// Deny the operation. This is the fail-closed behavior.
    /// </summary>
    Deny = 3,

    /// <summary>
    /// Defer the operation for later evaluation, retry, or review.
    /// </summary>
    Defer = 4,

    /// <summary>
    /// Require user or system acknowledgment before the operation can proceed.
    /// </summary>
    RequireAcknowledgment = 5,

    /// <summary>
    /// Recommend escalation before execution or provider emission.
    /// </summary>
    Escalate = 6
}
