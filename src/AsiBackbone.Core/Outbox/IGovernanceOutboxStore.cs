using AsiBackbone.Core.Emissions;

namespace AsiBackbone.Core.Outbox;

/// <summary>
/// Defines a provider-neutral durable outbox store for governance emission envelopes.
/// </summary>
/// <remarks>
/// Outbox entries are durable local state records keyed by <see cref="GovernanceOutboxEntry.OutboxEntryId" />.
/// They are not an append-only event stream and they do not imply cross-process row claiming, leasing,
/// skip-locked selection, or exactly-once provider delivery. Hosts that run multiple drain workers against
/// the same durable store must add host-owned claiming, partitioning, or provider-side idempotency.
/// </remarks>
public interface IGovernanceOutboxStore
{
    /// <summary>
    /// Enqueues a provider-neutral governance emission envelope before optional downstream provider delivery is attempted.
    /// </summary>
    /// <remarks>
    /// Implementations should persist a new pending state record with a stable outbox entry identifier. Provider emission
    /// remains downstream and should be treated as at-least-once unless the host and provider supply stronger guarantees.
    /// </remarks>
    ValueTask<GovernanceOutboxEntry> EnqueueAsync(
        GovernanceEmissionEnvelope envelope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an updated outbox entry state.
    /// </summary>
    /// <remarks>
    /// Implementations should store the latest state for the entry's stable identifier. For durable providers that support it,
    /// saving an already-known <see cref="GovernanceOutboxEntry.OutboxEntryId" /> should update the existing row rather than
    /// append a second logical outbox entry. Concurrent duplicate inserts may still surface provider-specific duplicate-key or
    /// concurrency exceptions that the host must reconcile.
    ///
    /// This is a non-claim mutation. Implementations may propagate storage-provider optimistic-concurrency exceptions without
    /// retrying or translating another writer's state into caller success. The caller owns conflict detection, reload, retry,
    /// merge, or abandonment according to its idempotency and transaction model.
    /// </remarks>
    ValueTask<GovernanceOutboxEntry> SaveAsync(
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an outbox entry by its stable identifier.
    /// </summary>
    ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(
        string outboxEntryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds pending outbox entries ordered for delivery.
    /// </summary>
    /// <remarks>
    /// Returned entries are candidates, not claimed work items. A multi-worker host should use a storage adapter or
    /// deployment pattern that claims or partitions work before provider emission.
    /// </remarks>
    ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds retry-ready outbox entries ordered for delivery.
    /// </summary>
    /// <remarks>
    /// Returned entries are candidates, not claimed work items. A multi-worker host should use a storage adapter or
    /// deployment pattern that claims or partitions work before provider emission.
    /// </remarks>
    ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
        DateTimeOffset utcNow,
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox entry as delivered using a provider-neutral emission result.
    /// </summary>
    /// <remarks>
    /// This is a non-claim mutation. Implementations may propagate a storage-provider optimistic-concurrency exception when
    /// another writer changes the same durable entry before this transition commits. No hidden retry or winner-state recovery
    /// is implied; the caller owns reload and conflict resolution. Competing workers should prefer a claim-capable store.
    /// </remarks>
    ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(
        string outboxEntryId,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox entry as failed or retryable failed using provider-neutral error information.
    /// </summary>
    /// <remarks>
    /// This is a non-claim mutation. Implementations may propagate a storage-provider optimistic-concurrency exception when
    /// another writer changes the same durable entry before this transition commits. No hidden retry or winner-state recovery
    /// is implied; the caller owns reload and conflict resolution. Competing workers should prefer a claim-capable store.
    /// </remarks>
    ValueTask<GovernanceOutboxEntry> MarkFailedAsync(
        string outboxEntryId,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox entry as dead-lettered.
    /// </summary>
    /// <remarks>
    /// This is a non-claim mutation. Implementations may propagate a storage-provider optimistic-concurrency exception when
    /// another writer changes the same durable entry before this transition commits. No hidden retry or winner-state recovery
    /// is implied; the caller owns reload and conflict resolution. Competing workers should prefer a claim-capable store.
    /// </remarks>
    ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(
        string outboxEntryId,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default);
}
