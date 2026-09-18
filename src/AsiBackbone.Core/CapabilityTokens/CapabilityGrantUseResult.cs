namespace AsiBackbone.Core.CapabilityTokens;

/// <summary>
/// Represents the outcome returned by a capability grant use-control store.
/// </summary>
public sealed class CapabilityGrantUseResult
{
    private CapabilityGrantUseResult(GrantUseState state, int useCount, string? failureCode, string? failureMessage)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Use state must be defined.");
        }

        State = state;
        UseCount = useCount < 0
            ? throw new ArgumentOutOfRangeException(nameof(useCount), useCount, "Use count must be greater than or equal to zero.")
            : useCount;
        FailureCode = NormalizeOptional(failureCode);
        FailureMessage = NormalizeOptional(failureMessage);
    }

    /// <summary>
    /// Gets the use-control state.
    /// </summary>
    public GrantUseState State { get; }

    /// <summary>
    /// Gets the observed use count after the store checked or consumed the grant.
    /// </summary>
    public int UseCount { get; }

    /// <summary>
    /// Gets the provider-neutral failure code when the use-control check did not accept the grant.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets the provider-neutral failure message when the use-control check did not accept the grant.
    /// </summary>
    public string? FailureMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the grant was accepted for use.
    /// </summary>
    public bool IsAccepted => State is GrantUseState.Accepted;

    /// <summary>
    /// Creates an accepted use result.
    /// </summary>
    public static CapabilityGrantUseResult Accepted(int useCount)
    {
        return new CapabilityGrantUseResult(GrantUseState.Accepted, useCount, null, null);
    }

    /// <summary>
    /// Creates a result indicating the configured use limit was exceeded.
    /// </summary>
    public static CapabilityGrantUseResult UseLimitExceeded(int useCount, string? failureMessage = null)
    {
        return new CapabilityGrantUseResult(GrantUseState.UseLimitExceeded, useCount, "capability.use-limit-exceeded", failureMessage);
    }

    /// <summary>
    /// Creates a result indicating the store can no longer prove the grant is within its use limit, because the grant is
    /// past the store's use-record retention horizon and its record may already have been evicted.
    /// </summary>
    /// <remarks>
    /// The state is <see cref="GrantUseState.UseLimitExceeded" />, so validation denies. Accepting the use instead would
    /// start a fresh count for a grant whose earlier uses the store has discarded, which is a replay.
    /// </remarks>
    public static CapabilityGrantUseResult RetentionElapsed(string? failureMessage = null)
    {
        return new CapabilityGrantUseResult(GrantUseState.UseLimitExceeded, 0, "capability.use-retention-elapsed", failureMessage);
    }

    /// <summary>
    /// Creates a result indicating the grant was administratively stopped.
    /// </summary>
    public static CapabilityGrantUseResult Stopped(string? failureMessage = null)
    {
        return new CapabilityGrantUseResult(GrantUseState.Stopped, 0, "capability.grant-stopped", failureMessage);
    }

    /// <summary>
    /// Creates a result indicating the grant was cancelled.
    /// </summary>
    public static CapabilityGrantUseResult Cancelled(string? failureMessage = null)
    {
        return new CapabilityGrantUseResult(GrantUseState.Cancelled, 0, "capability.grant-cancelled", failureMessage);
    }

    /// <summary>
    /// Creates a result indicating the use-control store was unavailable.
    /// </summary>
    public static CapabilityGrantUseResult Unavailable(string? failureMessage = null)
    {
        return new CapabilityGrantUseResult(GrantUseState.Unavailable, 0, "capability.use-store-unavailable", failureMessage);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
