using AsiBackbone.Core.CapabilityGrants;

namespace AsiBackbone.Storage.InMemory.CapabilityGrants;

/// <summary>
/// Provides a non-durable, in-process capability grant use store for tests, samples, and local validation.
/// </summary>
/// <remarks>
/// <para>
/// This store is thread-safe within a single process, but it is not durable, distributed, replicated, or suitable for
/// production replay protection. Hosts that require production single-use or bounded-use guarantees should provide a
/// durable implementation of <see cref="ICapabilityGrantUseStore" /> with documented transaction, locking, retention,
/// and failure semantics.
/// </para>
/// <para>
/// Use records are retained until a grant has been expired for longer than <see cref="EvictionGracePeriod" />, measured
/// against the latest use time this store has observed. A grant past that retention horizon is refused with
/// <c>capability.use-retention-elapsed</c> rather than given a fresh count, because its earlier uses may already have
/// been evicted. Set <see cref="EvictionGracePeriod" /> to at least the largest
/// <see cref="CapabilityGrantValidationOptions.AllowedClockSkew" /> any validator uses with this store; otherwise grants
/// that are expired but still inside the validator's skew are denied (fail closed) instead of accepted.
/// </para>
/// </remarks>
public sealed class InMemoryCapabilityGrantUseStore : ICapabilityGrantUseStore
{
    /// <summary>
    /// Separates issuer from token identifier in a use-record key, using a control character that cannot occur in either part.
    /// </summary>
    private const string KeySeparator = "\u001F";

    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, GrantUseEntry> useCounts = new(StringComparer.Ordinal);
    private readonly HashSet<string> stoppedGrantKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> cancelledGrantKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> stoppedGrantIdsForAllIssuers = new(StringComparer.Ordinal);
    private readonly HashSet<string> cancelledGrantIdsForAllIssuers = new(StringComparer.Ordinal);
    private TimeSpan evictionGracePeriod = TimeSpan.FromMinutes(5);
    private DateTimeOffset? latestObservedUseUtc;

    /// <summary>
    /// Gets or sets the grace period retained after a grant expires before its use record may be evicted.
    /// </summary>
    /// <remarks>
    /// Defaults to five minutes. This is also the retention horizon: a grant expired for longer than this period is refused
    /// rather than consumed. Set it to at least the largest <see cref="CapabilityGrantValidationOptions.AllowedClockSkew" />
    /// used with this store.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan EvictionGracePeriod
    {
        get
        {
            lock (syncRoot)
            {
                return evictionGracePeriod;
            }
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);

            lock (syncRoot)
            {
                evictionGracePeriod = value;
            }
        }
    }

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
    /// Marks a grant identifier as stopped for every issuer, for subsequent local validation attempts.
    /// </summary>
    /// <param name="grantId">The stable capability grant identifier.</param>
    /// <remarks>
    /// Use records are keyed by issuer and token identifier, so this overload stops every issuer's grant that uses the
    /// identifier. Call <see cref="StopGrant(string, string)" /> to stop one issuer's grant.
    /// </remarks>
    public void StopGrant(string grantId)
    {
        string normalizedGrantId = NormalizeGrantId(grantId);

        lock (syncRoot)
        {
            _ = stoppedGrantIdsForAllIssuers.Add(normalizedGrantId);
            _ = cancelledGrantIdsForAllIssuers.Remove(normalizedGrantId);
        }
    }

    /// <summary>
    /// Marks one issuer's grant as stopped for subsequent local validation attempts.
    /// </summary>
    /// <param name="issuer">The grant issuer.</param>
    /// <param name="grantId">The stable capability grant identifier.</param>
    public void StopGrant(string issuer, string grantId)
    {
        string key = CreateKey(issuer, NormalizeGrantId(grantId));

        lock (syncRoot)
        {
            _ = stoppedGrantKeys.Add(key);
            _ = cancelledGrantKeys.Remove(key);
        }
    }

    /// <summary>
    /// Marks a grant identifier as cancelled for every issuer, for subsequent local validation attempts.
    /// </summary>
    /// <param name="grantId">The stable capability grant identifier.</param>
    /// <remarks>
    /// Use records are keyed by issuer and token identifier, so this overload cancels every issuer's grant that uses the
    /// identifier. Call <see cref="CancelGrant(string, string)" /> to cancel one issuer's grant.
    /// </remarks>
    public void CancelGrant(string grantId)
    {
        string normalizedGrantId = NormalizeGrantId(grantId);

        lock (syncRoot)
        {
            _ = cancelledGrantIdsForAllIssuers.Add(normalizedGrantId);
            _ = stoppedGrantIdsForAllIssuers.Remove(normalizedGrantId);
        }
    }

    /// <summary>
    /// Marks one issuer's grant as cancelled for subsequent local validation attempts.
    /// </summary>
    /// <param name="issuer">The grant issuer.</param>
    /// <param name="grantId">The stable capability grant identifier.</param>
    public void CancelGrant(string issuer, string grantId)
    {
        string key = CreateKey(issuer, NormalizeGrantId(grantId));

        lock (syncRoot)
        {
            _ = cancelledGrantKeys.Add(key);
            _ = stoppedGrantKeys.Remove(key);
        }
    }

    /// <summary>
    /// Clears use-count, stopped/cancelled, and observed-time state from this in-memory store instance.
    /// </summary>
    public void Clear()
    {
        lock (syncRoot)
        {
            useCounts.Clear();
            stoppedGrantKeys.Clear();
            cancelledGrantKeys.Clear();
            stoppedGrantIdsForAllIssuers.Clear();
            cancelledGrantIdsForAllIssuers.Clear();
            latestObservedUseUtc = null;
        }
    }

    /// <inheritdoc />
    public ValueTask<CapabilityGrantUseResult> TryConsumeAsync(
        CapabilityGrant grant,
        int maxUseCount,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxUseCount, 1);
        cancellationToken.ThrowIfCancellationRequested();

        // Stop and cancel state was keyed by token identifier alone while use counts were keyed by issuer and token, so
        // stopping one issuer's grant stopped every issuer's grant that shared the identifier.
        string key = CreateKey(grant.Issuer, grant.TokenId);

        lock (syncRoot)
        {
            if (stoppedGrantKeys.Contains(key) || stoppedGrantIdsForAllIssuers.Contains(grant.TokenId))
            {
                return ValueTask.FromResult(CapabilityGrantUseResult.Stopped("The in-memory capability grant use store marked this grant as stopped."));
            }

            if (cancelledGrantKeys.Contains(key) || cancelledGrantIdsForAllIssuers.Contains(grant.TokenId))
            {
                return ValueTask.FromResult(CapabilityGrantUseResult.Cancelled("The in-memory capability grant use store marked this grant as cancelled."));
            }

            DateTimeOffset retentionThreshold = AdvanceRetentionThreshold(usedUtc);
            EvictExpiredEntries(retentionThreshold);

            // Eviction previously ran against the grace period alone, while the validator accepts an expired grant for as
            // long as its clock skew allows. With skew above the grace period, a grant's record was evicted while the grant
            // still validated, so the next use started a fresh count: a replay. A grant past the retention horizon may
            // already have lost its record, so it is refused instead. The horizon only moves forward, so a record is never
            // evicted while a grant it describes can still be accepted.
            if (grant.ExpiresUtc < retentionThreshold)
            {
                return ValueTask.FromResult(CapabilityGrantUseResult.RetentionElapsed(
                    "The grant is past the in-memory use store's retention horizon, so its earlier uses can no longer be proven."));
            }

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
    /// Records the use time and returns the retention threshold measured from the latest use time observed so far.
    /// </summary>
    /// <remarks>
    /// Use times come from the caller, and validators may supply a fixed validation time. Measuring from each call's own
    /// time let a later call with an earlier time find a record that an earlier call had already evicted, and start a fresh
    /// count. Measuring from the latest observed time keeps the threshold monotonic. Must be called while holding the lock.
    /// </remarks>
    private DateTimeOffset AdvanceRetentionThreshold(DateTimeOffset usedUtc)
    {
        DateTimeOffset normalizedUsedUtc = usedUtc.ToUniversalTime();
        DateTimeOffset latest = latestObservedUseUtc is { } observed && observed > normalizedUsedUtc
            ? observed
            : normalizedUsedUtc;

        latestObservedUseUtc = latest;
        return latest - evictionGracePeriod;
    }

    /// <summary>
    /// Removes use records for grants that expired before the retention threshold.
    /// </summary>
    /// <remarks>
    /// Every consumed token identifier was previously retained for the lifetime of the process, so a long-running host
    /// accumulated a record per grant it ever validated with no way to reclaim the memory short of clearing the store.
    /// Must be called while holding the lock.
    /// </remarks>
    private void EvictExpiredEntries(DateTimeOffset retentionThreshold)
    {
        List<string>? expiredKeys = null;

        foreach (KeyValuePair<string, GrantUseEntry> item in useCounts)
        {
            if (item.Value.ExpiresUtc < retentionThreshold)
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
