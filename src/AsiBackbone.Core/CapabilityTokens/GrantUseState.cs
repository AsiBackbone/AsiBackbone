namespace AsiBackbone.Core.CapabilityTokens;

public enum GrantUseState
{
    /// <summary>
    /// No use state was assigned. Zero is a rejected sentinel so a default value cannot mean "accepted".
    /// </summary>
    Unspecified = 0,

    Accepted = 5,
    UseLimitExceeded = 1,
    Stopped = 2,
    Cancelled = 3,
    Unavailable = 4
}
