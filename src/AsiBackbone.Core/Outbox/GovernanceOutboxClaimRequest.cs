namespace AsiBackbone.Core.Outbox;

/// <summary>
/// Describes an opt-in outbox claim and lease request.
/// </summary>
/// <remarks>
/// Claim requests are used by claim-capable stores to coordinate scaled workers before provider emission. They do not create exactly-once delivery guarantees by themselves.
/// </remarks>
public sealed class GovernanceOutboxClaimRequest
{
    /// <summary>
    /// Gets the default lease duration for claimed outbox work.
    /// </summary>
    public static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private GovernanceOutboxClaimRequest(
        string workerId,
        DateTimeOffset utcNow,
        TimeSpan leaseDuration,
        int maxCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be greater than TimeSpan.Zero.");
        }

        if (maxCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "Maximum count must be greater than zero.");
        }

        WorkerId = workerId.Trim();
        UtcNow = utcNow.ToUniversalTime();
        LeaseDuration = leaseDuration;
        MaxCount = maxCount;
    }

    /// <summary>
    /// Gets the stable worker, process, node, or partition owner identifier requesting the claim.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the UTC timestamp used for eligibility and lease expiration calculations.
    /// </summary>
    public DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Gets the requested claim lease duration.
    /// </summary>
    public TimeSpan LeaseDuration { get; }

    /// <summary>
    /// Gets the maximum number of entries to claim.
    /// </summary>
    public int MaxCount { get; }

    /// <summary>
    /// Gets the UTC timestamp at which the requested lease expires.
    /// </summary>
    public DateTimeOffset ClaimExpiresUtc => UtcNow.Add(LeaseDuration);

    /// <summary>
    /// Creates a claim request with normalized worker and timing values.
    /// </summary>
    public static GovernanceOutboxClaimRequest Create(
        string workerId,
        DateTimeOffset? utcNow = null,
        TimeSpan? leaseDuration = null,
        int maxCount = 100)
    {
        return new GovernanceOutboxClaimRequest(
            workerId,
            utcNow ?? DateTimeOffset.UtcNow,
            leaseDuration ?? DefaultLeaseDuration,
            maxCount);
    }
}
