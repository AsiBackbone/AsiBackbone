using AsiBackbone.Core.CapabilityTokens;

namespace AsiBackbone.Storage.InMemory.CapabilityTokens;

/// <summary>
/// Provides a non-durable, in-process capability grant use store for tests, samples, and local validation.
/// </summary>
/// <remarks>
/// This store is thread-safe within a single process, but it is not durable, distributed, replicated, or suitable for
/// production replay protection. Hosts that require production single-use or bounded-use guarantees should provide a
/// durable implementation of <see cref="ICapabilityGrantUseStore" /> with documented transaction, locking, retention,
/// and failure semantics.
/// </remarks>
public sealed class InMemoryCapabilityGrantUseStore : ICapabilityGrantUseStore
{
    /// <summary>
    /// Separates issuer from token identifier in a use-record key, using a control character that cannot occur in either part.
    /// </summary>
    private const string KeySeparator = "\u001F";

    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, GrantUseEntry> useCounts = new(StringComparer.Ordinal);
    private readonly HashSet<string> stoppedGrantIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> cancelledGrantIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the grace period retained after a grant expires before its use record may be evicted.
    /// </summary>
    public TimeSpan EvictionGracePeriod { get; set; } = TimeSpan.FromMinutes(5);

    private sealed record GrantUseEntry(int Count, DateTimeOffset ExpiresUtc);

    /// <summary>
    /// Gets the observed use count for a grant identifier, across every issuer that used it.
    /// </summary>
    /// <param name="grantId">The stable capability grant identifier.</param>
    /// <returns>The observed use count, or zero when the grant has not been consumed by this store instance.</returns>
    /// <remarks>
    /// Use records are keyed by issuer and token identifier, so this overload sums the issuers that used the identifier.
    /// Call <see cref="GetUseCount(string, string)" /> to read one issuer's count.
    /// </remarks>
    public int GetUseCount(string grantId)
    {
        string normalizedGrantId = NormalizeGrantId(grantId);

        lock (syncRoot)
        {
            int total = 0;

            foreach (KeyValuePair<string, GrantUseEntry> item in useCounts)
            {
                if (string.Equals(SplitTokenId(item.Key), normalizedGrantId, StringComparison.Ordinal))
                {
                    total += item.Value.Count;
                }
            }

            return total;
        }
    }

    /// <summary>
    /// Gets the observed use count for one issuer's grant identifier.
    /// </summary>
    /// <param name="issuer">The grant issuer.</param>
    /// <param name="grantId">The stable capability grant identifier.</param>
    /// <returns>The observed use count, or zero when that issuer's grant has not been consumed by this store instance.</returns>
    public int GetUseCount(string issuer, string grantId)
    {
        string key = CreateKey(issuer, NormalizeGrantId(grantId));

        lock (syncRoot)
        {
            return useCounts.TryGetValue(key, out GrantUseEntry? entry) ? entry.Count : 0;
        }
    }

    /// <summary>
    /// Marks a grant as stopped for subsequent local validation attempts.
    /// </summary>
    /// <param name="grantId">The stable capability grant identifier.</param>
    public void StopGrant(string grantId)
    {
        string normalizedGrantId = NormalizeGrantId(grantId);

        lock (syncRoot)
        {
            _ = stoppedGrantIds.Add(normalizedGrantId);
            _ = cancelledGrantIds.Remove(normalizedGrantId);
        }
    }

    /// <summary>
    /// Marks a grant as cancelled for subsequent local validation attempts.
    /// </summary>
    /// <param name="grantId">The stable capability grant identifier.</param>
    public void CancelGrant(string grantId)
    {
        string normalizedGrantId = NormalizeGrantId(grantId);

        lock (syncRoot)
        {
            _ = cancelledGrantIds.Add(normalizedGrantId);
            _ = stoppedGrantIds.Remove(normalizedGrantId);
        }
    }

    /// <summary>
    /// Clears use-count and stopped/cancelled state from this in-memory store instance.
    /// </summary>
    public void Clear()
    {
        lock (syncRoot)
        {
            useCounts.Clear();
            stoppedGrantIds.Clear();
            cancelledGrantIds.Clear();
        }
    }

    /// <inheritdoc />
    public ValueTask<CapabilityGrantUseResult> TryConsumeAsync(
        CapabilityTokenGrant grant,
        int maxUseCount,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxUseCount, 1);
        _ = usedUtc.ToUniversalTime();
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            if (stoppedGrantIds.Contains(grant.TokenId))
            {
                return ValueTask.FromResult(CapabilityGrantUseResult.Stopped("The in-memory capability grant use store marked this grant as stopped."));
            }

            if (cancelledGrantIds.Contains(grant.TokenId))
            {
                return ValueTask.FromResult(CapabilityGrantUseResult.Cancelled("The in-memory capability grant use store marked this grant as cancelled."));
            }

            EvictExpiredEntries(usedUtc);

            // Keying by token identifier alone let two issuers that happen to use the same identifier share one use
            // budget, so one issuer's grant could exhaust another's.
            string key = CreateKey(grant.Issuer, grant.TokenId);
            _ = useCounts.TryGetValue(key, out GrantUseEntry? entry);
            int currentCount = entry?.Count ?? 0;

            if (currentCount >= maxUseCount)
            {
                return ValueTask.FromResult(CapabilityGrantUseResult.UseLimitExceeded(
                    currentCount,
                    "The in-memory capability grant use limit was exceeded."));
            }

            int nextCount = currentCount + 1;
            useCounts[key] = new GrantUseEntry(nextCount, grant.ExpiresUtc);
            return ValueTask.FromResult(CapabilityGrantUseResult.Accepted(nextCount));
        }
    }

    private static string NormalizeGrantId(string grantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grantId);
        return grantId.Trim();
    }

    /// <summary>
    /// Builds the issuer-scoped key for a grant use record.
    /// </summary>
    private static string CreateKey(string issuer, string grantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        return string.Concat(issuer.Trim(), KeySeparator, grantId);
    }

    private static string SplitTokenId(string key)
    {
        int separatorIndex = key.IndexOf(KeySeparator, StringComparison.Ordinal);

        return separatorIndex < 0 ? key : key[(separatorIndex + 1)..];
    }

    /// <summary>
    /// Removes use records for grants that expired longer ago than the configured grace period.
    /// </summary>
    /// <remarks>
    /// Every consumed token identifier was previously retained for the lifetime of the process, so a long-running host
    /// accumulated a record per grant it ever validated with no way to reclaim the memory short of clearing the store.
    /// </remarks>
    private void EvictExpiredEntries(DateTimeOffset usedUtc)
    {
        DateTimeOffset threshold = usedUtc.ToUniversalTime() - EvictionGracePeriod;
        List<string>? expiredKeys = null;

        foreach (KeyValuePair<string, GrantUseEntry> item in useCounts)
        {
            if (item.Value.ExpiresUtc < threshold)
            {
                (expiredKeys ??= []).Add(item.Key);
            }
        }

        if (expiredKeys is null)
        {
            return;
        }

        foreach (string key in expiredKeys)
        {
            _ = useCounts.Remove(key);
        }
    }
}
