using AsiBackbone.Core.Results;

namespace AsiBackbone.Core.Audit;

/// <summary>
/// Defines the framework-neutral storage contract for persistent AsiBackbone audit ledger records.
/// </summary>
public interface IGovernanceAuditLedgerStore
{
    /// <summary>
    /// Appends an audit ledger record to durable host-owned storage.
    /// </summary>
    /// <param name="record">The record to append.</param>
    /// <param name="cancellationToken">A token that can cancel the append operation.</param>
    /// <returns>The append operation result.</returns>
    ValueTask<OperationResult<AuditLedgerRecord>> AppendAsync(
        AuditLedgerRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an audit ledger record by its stable record identifier.
    /// </summary>
    /// <param name="recordId">The stable record identifier.</param>
    /// <param name="cancellationToken">A token that can cancel the lookup operation.</param>
    /// <returns>The matching record, or null when no record exists.</returns>
    ValueTask<AuditLedgerRecord?> FindByRecordIdAsync(
        string recordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds audit ledger records associated with a correlation identifier.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="cancellationToken">A token that can cancel the lookup operation.</param>
    /// <returns>The matching audit ledger records.</returns>
    ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds audit ledger records associated with a trace identifier.
    /// </summary>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="cancellationToken">A token that can cancel the lookup operation.</param>
    /// <returns>The matching audit ledger records.</returns>
    ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByTraceIdAsync(
        string traceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds audit ledger records associated with an actor identifier.
    /// </summary>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="cancellationToken">A token that can cancel the lookup operation.</param>
    /// <returns>The matching audit ledger records.</returns>
    ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByActorIdAsync(
        string actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds audit ledger records recorded within the inclusive UTC date range.
    /// </summary>
    /// <param name="recordedFromUtc">The inclusive lower recorded UTC bound.</param>
    /// <param name="recordedToUtc">The inclusive upper recorded UTC bound.</param>
    /// <param name="cancellationToken">A token that can cancel the lookup operation.</param>
    /// <returns>The matching audit ledger records.</returns>
    ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByRecordedUtcRangeAsync(
        DateTimeOffset recordedFromUtc,
        DateTimeOffset recordedToUtc,
        CancellationToken cancellationToken = default);
}
