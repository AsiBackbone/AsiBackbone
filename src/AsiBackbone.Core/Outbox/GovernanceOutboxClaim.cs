namespace AsiBackbone.Core.Outbox;

/// <summary>
/// Represents a successful claim over an outbox entry.
/// </summary>
/// <remarks>
/// A claim proves that a worker acquired a lease before provider emission. It reduces duplicate selection risk for cooperating workers, but it does not provide exactly-once delivery.
/// </remarks>
public sealed class GovernanceOutboxClaim
{
    private GovernanceOutboxClaim(
        GovernanceOutboxEntry entry,
        string workerId,
        string claimToken,
        DateTimeOffset claimedUtc,
        DateTimeOffset claimExpiresUtc)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

        if (claimExpiresUtc.ToUniversalTime() <= claimedUtc.ToUniversalTime())
        {
            throw new ArgumentOutOfRangeException(nameof(claimExpiresUtc), claimExpiresUtc, "Claim expiration must be after the claimed timestamp.");
        }

        Entry = entry;
        WorkerId = workerId.Trim();
        ClaimToken = claimToken.Trim();
        ClaimedUtc = claimedUtc.ToUniversalTime();
        ClaimExpiresUtc = claimExpiresUtc.ToUniversalTime();
    }

    /// <summary>
    /// Gets the claimed outbox entry snapshot.
    /// </summary>
    public GovernanceOutboxEntry Entry { get; }

    /// <summary>
    /// Gets the stable outbox entry identifier.
    /// </summary>
    public string OutboxEntryId => Entry.OutboxEntryId;

    /// <summary>
    /// Gets the worker, process, node, or partition owner that acquired the claim.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the opaque token that identifies this lease instance.
    /// </summary>
    public string ClaimToken { get; }

    /// <summary>
    /// Gets the UTC timestamp when the claim was acquired.
    /// </summary>
    public DateTimeOffset ClaimedUtc { get; }

    /// <summary>
    /// Gets the UTC timestamp when the claim lease expires.
    /// </summary>
    public DateTimeOffset ClaimExpiresUtc { get; }

    /// <summary>
    /// Determines whether the claim lease is expired at the supplied UTC timestamp.
    /// </summary>
    public bool IsExpired(DateTimeOffset utcNow)
    {
        return ClaimExpiresUtc <= utcNow.ToUniversalTime();
    }

    /// <summary>
    /// Creates a claim from a claimed entry snapshot.
    /// </summary>
    public static GovernanceOutboxClaim Create(
        GovernanceOutboxEntry entry,
        string workerId,
        string claimToken,
        DateTimeOffset claimedUtc,
        DateTimeOffset claimExpiresUtc)
    {
        return new GovernanceOutboxClaim(entry, workerId, claimToken, claimedUtc, claimExpiresUtc);
    }
}
