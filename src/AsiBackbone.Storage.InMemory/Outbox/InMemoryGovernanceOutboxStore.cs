using System.Collections.Concurrent;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;

namespace AsiBackbone.Storage.InMemory.Outbox;

/// <summary>
/// In-memory governance outbox store for tests, samples, and development hosts.
/// </summary>
/// <remarks>
/// This store is not durable across process restarts. Production hosts should use a durable provider such as EF Core or another host-owned storage adapter.
/// Same-entry status transitions use single-process compare-and-swap updates so tests and local validation do not accidentally observe last-write-wins behavior.
/// </remarks>
public sealed class InMemoryGovernanceOutboxStore : IGovernanceOutboxClaimStore
{
    private readonly ConcurrentDictionary<string, GovernanceOutboxEntry> entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> EnqueueAsync(
        GovernanceEmissionEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = GovernanceOutboxEntry.Create(envelope);

        return !entries.TryAdd(entry.OutboxEntryId, entry)
            ? throw new InvalidOperationException($"Outbox entry '{entry.OutboxEntryId}' already exists.")
            : ValueTask.FromResult(entry);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> SaveAsync(
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(SaveEntry(entry, cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(
        string outboxEntryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
        cancellationToken.ThrowIfCancellationRequested();

        _ = entries.TryGetValue(outboxEntryId.Trim(), out GovernanceOutboxEntry? entry);

        return ValueTask.FromResult(entry);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int normalizedMaxCount = NormalizeMaxCount(maxCount);

        IReadOnlyList<GovernanceOutboxEntry> matches = [.. entries.Values
            .Where(entry => entry.Status is GovernanceEmissionStatus.Pending)
            .OrderBy(entry => entry.CreatedUtc)
            .ThenBy(entry => entry.OutboxEntryId)
            .Take(normalizedMaxCount)];

        return ValueTask.FromResult(matches);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
        DateTimeOffset utcNow,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int normalizedMaxCount = NormalizeMaxCount(maxCount);
        DateTimeOffset normalizedUtcNow = utcNow.ToUniversalTime();

        IReadOnlyList<GovernanceOutboxEntry> matches = [.. entries.Values
            .Where(entry => entry.IsRetryReady(normalizedUtcNow))
            .OrderBy(entry => entry.NextRetryUtc ?? entry.UpdatedUtc)
            .ThenBy(entry => entry.OutboxEntryId)
            .Take(normalizedMaxCount)];

        return ValueTask.FromResult(matches);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimPendingAsync(
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<GovernanceOutboxEntry> candidates = [.. entries.Values
            .Where(entry => entry.Status is GovernanceEmissionStatus.Pending)
            .Where(entry => entry.CanBeClaimed(request.UtcNow))
            .OrderBy(entry => entry.CreatedUtc)
            .ThenBy(entry => entry.OutboxEntryId)];

        return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxClaim>>(ClaimEntries(candidates, request, cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimRetryReadyAsync(
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<GovernanceOutboxEntry> candidates = [.. entries.Values
            .Where(entry => entry.IsRetryReady(request.UtcNow))
            .Where(entry => entry.CanBeClaimed(request.UtcNow))
            .OrderBy(entry => entry.NextRetryUtc ?? entry.UpdatedUtc)
            .ThenBy(entry => entry.OutboxEntryId)];

        return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxClaim>>(ClaimEntries(candidates, request, cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(
        string outboxEntryId,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntry updatedEntry = UpdateExistingEntry(
            outboxEntryId,
            entry => IsTerminal(entry) ? entry : entry.MarkDelivered(result),
            cancellationToken);

        return ValueTask.FromResult(updatedEntry);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkClaimDeliveredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntry updatedEntry = UpdateClaimedEntry(
            claim,
            entry => IsTerminal(entry) ? entry : entry.MarkDelivered(result),
            cancellationToken);

        return ValueTask.FromResult(updatedEntry);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkFailedAsync(
        string outboxEntryId,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
        ArgumentNullException.ThrowIfNull(governanceEmissionError);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntry updatedEntry = UpdateExistingEntry(
            outboxEntryId,
            entry => IsTerminal(entry) ? entry : entry.MarkFailed(governanceEmissionError, nextRetryUtc),
            cancellationToken);

        return ValueTask.FromResult(updatedEntry);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkClaimFailedAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(governanceEmissionError);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntry updatedEntry = UpdateClaimedEntry(
            claim,
            entry => IsTerminal(entry) ? entry : entry.MarkFailed(governanceEmissionError, nextRetryUtc),
            cancellationToken);

        return ValueTask.FromResult(updatedEntry);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(
        string outboxEntryId,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
        ArgumentNullException.ThrowIfNull(governanceEmissionError);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntry updatedEntry = UpdateExistingEntry(
            outboxEntryId,
            entry => IsTerminal(entry) ? entry : entry.MarkDeadLettered(governanceEmissionError, deadLetterReason),
            cancellationToken);

        return ValueTask.FromResult(updatedEntry);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkClaimDeadLetteredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(governanceEmissionError);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntry updatedEntry = UpdateClaimedEntry(
            claim,
            entry => IsTerminal(entry) ? entry : entry.MarkDeadLettered(governanceEmissionError, deadLetterReason),
            cancellationToken);

        return ValueTask.FromResult(updatedEntry);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> SaveClaimAsync(
        GovernanceOutboxClaim claim,
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(claim.OutboxEntryId, entry.OutboxEntryId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Claim and entry must reference the same outbox entry ID.", nameof(entry));
        }

        GovernanceOutboxEntry updatedEntry = UpdateClaimedEntry(
            claim,
            currentEntry => IsTerminal(currentEntry) ? currentEntry : entry,
            cancellationToken);

        return ValueTask.FromResult(updatedEntry);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry?> ReleaseClaimAsync(
        GovernanceOutboxClaim claim,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntry? releasedEntry = ReleaseClaim(claim, cancellationToken);

        return ValueTask.FromResult(releasedEntry);
    }

    private List<GovernanceOutboxClaim> ClaimEntries(
        IReadOnlyList<GovernanceOutboxEntry> candidates,
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        List<GovernanceOutboxClaim> claims = new(Math.Min(request.MaxCount, candidates.Count));

        foreach (GovernanceOutboxEntry candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (claims.Count >= request.MaxCount)
            {
                break;
            }

            GovernanceOutboxClaim? claim = TryClaimEntry(candidate.OutboxEntryId, request, cancellationToken);
            if (claim is not null)
            {
                claims.Add(claim);
            }
        }

        return claims;
    }

    private GovernanceOutboxClaim? TryClaimEntry(
        string outboxEntryId,
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!entries.TryGetValue(outboxEntryId, out GovernanceOutboxEntry? currentEntry))
            {
                return null;
            }

            if (!currentEntry.CanBeClaimed(request.UtcNow))
            {
                return null;
            }

            GovernanceOutboxEntry claimedEntry = currentEntry.MarkClaimed(
                request.WorkerId,
                claimedUtc: request.UtcNow,
                leaseDuration: request.LeaseDuration);

            if (entries.TryUpdate(outboxEntryId, claimedEntry, currentEntry))
            {
                return CreateClaim(claimedEntry);
            }
        }
    }

    private GovernanceOutboxEntry SaveEntry(
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!entries.TryGetValue(entry.OutboxEntryId, out GovernanceOutboxEntry? currentEntry))
            {
                if (entries.TryAdd(entry.OutboxEntryId, entry))
                {
                    return entry;
                }

                continue;
            }

            if (IsTerminal(currentEntry))
            {
                return currentEntry;
            }

            if (entries.TryUpdate(entry.OutboxEntryId, entry, currentEntry))
            {
                return entry;
            }
        }
    }

    private GovernanceOutboxEntry UpdateExistingEntry(
        string outboxEntryId,
        Func<GovernanceOutboxEntry, GovernanceOutboxEntry> updateEntry,
        CancellationToken cancellationToken)
    {
        string normalizedOutboxEntryId = outboxEntryId.Trim();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!entries.TryGetValue(normalizedOutboxEntryId, out GovernanceOutboxEntry? currentEntry))
            {
                throw new InvalidOperationException($"Outbox entry '{normalizedOutboxEntryId}' was not found.");
            }

            GovernanceOutboxEntry updatedEntry = updateEntry(currentEntry);

            if (ReferenceEquals(currentEntry, updatedEntry))
            {
                return currentEntry;
            }

            if (entries.TryUpdate(normalizedOutboxEntryId, updatedEntry, currentEntry))
            {
                return updatedEntry;
            }
        }
    }

    private GovernanceOutboxEntry UpdateClaimedEntry(
        GovernanceOutboxClaim claim,
        Func<GovernanceOutboxEntry, GovernanceOutboxEntry> updateEntry,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!entries.TryGetValue(claim.OutboxEntryId, out GovernanceOutboxEntry? currentEntry))
            {
                throw new InvalidOperationException($"Outbox entry '{claim.OutboxEntryId}' was not found.");
            }

            if (!currentEntry.IsClaimedBy(claim) || IsTerminal(currentEntry))
            {
                return currentEntry;
            }

            GovernanceOutboxEntry updatedEntry = updateEntry(currentEntry);

            if (ReferenceEquals(currentEntry, updatedEntry))
            {
                return currentEntry;
            }

            if (entries.TryUpdate(claim.OutboxEntryId, updatedEntry, currentEntry))
            {
                return updatedEntry;
            }
        }
    }

    private GovernanceOutboxEntry? ReleaseClaim(
        GovernanceOutboxClaim claim,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!entries.TryGetValue(claim.OutboxEntryId, out GovernanceOutboxEntry? currentEntry))
            {
                return null;
            }

            if (!currentEntry.IsClaimedBy(claim) || IsTerminal(currentEntry))
            {
                return currentEntry;
            }

            GovernanceOutboxEntry releasedEntry = currentEntry.ReleaseClaim();

            if (entries.TryUpdate(claim.OutboxEntryId, releasedEntry, currentEntry))
            {
                return releasedEntry;
            }
        }
    }

    private static GovernanceOutboxClaim CreateClaim(GovernanceOutboxEntry entry)
    {
        return GovernanceOutboxClaim.Create(
            entry,
            entry.ClaimOwner ?? throw new InvalidOperationException("Claimed entry is missing claim owner."),
            entry.ClaimToken ?? throw new InvalidOperationException("Claimed entry is missing claim token."),
            entry.ClaimedUtc ?? throw new InvalidOperationException("Claimed entry is missing claimed timestamp."),
            entry.ClaimExpiresUtc ?? throw new InvalidOperationException("Claimed entry is missing claim expiration timestamp."));
    }

    private static bool IsTerminal(GovernanceOutboxEntry entry)
    {
        return entry.IsDelivered || entry.IsDeadLettered;
    }

    private static int NormalizeMaxCount(int maxCount)
    {
        return maxCount <= 0
            ? throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "Maximum count must be greater than zero.")
            : maxCount;
    }
}
