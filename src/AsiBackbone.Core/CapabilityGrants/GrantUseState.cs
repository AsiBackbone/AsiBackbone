namespace AsiBackbone.Core.CapabilityGrants;

/// <summary>
/// Identifies the outcome reported by an <see cref="ICapabilityGrantUseStore" /> when a capability grant use is
/// checked and consumed.
/// </summary>
/// <remarks>
/// Only <see cref="Accepted" /> lets validation continue. Every other state fails validation, and an unrecognized value
/// escalates.
/// </remarks>
public enum GrantUseState
{
    /// <summary>
    /// No use state was assigned. Zero is a rejected sentinel so a default value cannot mean "accepted".
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The use was within the allowed use count and has been consumed.
    /// </summary>
    Accepted = 5,

    /// <summary>
    /// The grant has already reached its allowed use count, or its use-tracking retention has elapsed, so validation
    /// denies.
    /// </summary>
    UseLimitExceeded = 1,

    /// <summary>
    /// The grant has been stopped by the host, so validation treats it as revoked and denies.
    /// </summary>
    Stopped = 2,

    /// <summary>
    /// The grant has been cancelled, so validation denies.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// The use store could not determine the use state, so validation defers rather than allowing.
    /// </summary>
    Unavailable = 4
}
