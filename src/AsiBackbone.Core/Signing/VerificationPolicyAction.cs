namespace AsiBackbone.Core.Signing;

/// <summary>
/// Describes the host-facing action selected by verification policy.
/// </summary>
public enum VerificationPolicyAction
{
    /// <summary>
    /// No action was assigned.
    /// </summary>
    /// <remarks>
    /// Zero is a rejected sentinel so that a default-constructed value, or a persisted column that yields zero for
    /// unrecognized input, cannot mean "allow".
    /// </remarks>
    Unspecified = 0,

    /// <summary>
    /// Allow the governed operation or high-assurance emission to proceed.
    /// </summary>
    Allow = 7,

    /// <summary>
    /// Deny the governed operation or high-assurance emission.
    /// </summary>
    Deny = 1,

    /// <summary>
    /// Defer the workflow until verification can be completed later.
    /// </summary>
    Defer = 2,

    /// <summary>
    /// Require an explicit acknowledgment before proceeding.
    /// </summary>
    RequireAcknowledgment = 3,

    /// <summary>
    /// Escalate to an operator, reviewer, or host-defined governance process.
    /// </summary>
    Escalate = 4,

    /// <summary>
    /// Retry verification or downstream handling according to host retry policy.
    /// </summary>
    Retry = 5,

    /// <summary>
    /// Move the record or emission request to dead-letter handling.
    /// </summary>
    DeadLetter = 6
}
