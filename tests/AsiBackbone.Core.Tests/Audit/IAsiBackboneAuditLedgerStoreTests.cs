using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Unit tests for <see cref="IGovernanceAuditLedgerStore"/>. These tests verify the contract of the interface, but do not test any specific implementation. Each implementation should have its own set of unit tests to verify its behavior and correctness.
/// </summary>
public sealed class IAsiBackboneAuditLedgerStoreTests
{
    /// <summary>
    /// Verifies that an implementation of <see cref="IGovernanceAuditLedgerStore"/> allows appending a record and then looking it up by its record ID. This test uses a simple in-memory implementation of the interface to validate the expected behavior of the contract.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous test operation. The test will pass if the record can be successfully appended and retrieved by its record ID, and will fail if any of these operations do not behave as expected.
    /// </returns>
    [Fact]
    public async Task ContractAllowsAppendAndLookupByRecordId()
    {
        IGovernanceAuditLedgerStore store = new TestAuditLedgerStore();

        var residue = DecisionReceipt.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed",
            eventId: "event-123",
            correlationId: "correlation-123");

        var record = AuditLedgerRecord.FromResidue(
            residue,
            recordId: "record-123");

        OperationResult<AuditLedgerRecord> appendResult = await store.AppendAsync(record, TestContext.Current.CancellationToken);
        AuditLedgerRecord? found = await store.FindByRecordIdAsync("record-123", TestContext.Current.CancellationToken);

        Assert.True(appendResult.Succeeded);
        Assert.Same(record, appendResult.Value);
        Assert.Same(record, found);
    }

    /// <summary>
    /// Verifies that an implementation of <see cref="IGovernanceAuditLedgerStore"/> allows looking up records by their correlation ID.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ContractAllowsLookupByCorrelationId()
    {
        IGovernanceAuditLedgerStore store = new TestAuditLedgerStore();
        AuditLedgerRecord firstRecord = CreateRecord("record-1", "correlation-123", "trace-1", "actor-1");
        AuditLedgerRecord secondRecord = CreateRecord("record-2", "correlation-123", "trace-2", "actor-2");
        AuditLedgerRecord thirdRecord = CreateRecord("record-3", "correlation-456", "trace-3", "actor-3");

        _ = await store.AppendAsync(firstRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(secondRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(thirdRecord, TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLedgerRecord> found = await store.FindByCorrelationIdAsync(
            "correlation-123",
            TestContext.Current.CancellationToken);

        AuditLedgerRecord[] expected = [firstRecord, secondRecord];

        Assert.Equal(expected, found);
    }

    /// <summary>
    /// Verifies that an implementation of <see cref="IGovernanceAuditLedgerStore"/> allows looking up records by their trace ID.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ContractAllowsLookupByTraceId()
    {
        IGovernanceAuditLedgerStore store = new TestAuditLedgerStore();
        AuditLedgerRecord firstRecord = CreateRecord("record-1", "correlation-1", "trace-123", "actor-1");
        AuditLedgerRecord secondRecord = CreateRecord("record-2", "correlation-2", "trace-123", "actor-2");
        AuditLedgerRecord thirdRecord = CreateRecord("record-3", "correlation-3", "trace-456", "actor-3");

        _ = await store.AppendAsync(firstRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(secondRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(thirdRecord, TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLedgerRecord> found = await store.FindByTraceIdAsync(
            "trace-123",
            TestContext.Current.CancellationToken);

        AuditLedgerRecord[] expected = [firstRecord, secondRecord];

        Assert.Equal(expected, found);
    }

    /// <summary>
    /// Verifies that an implementation of <see cref="IGovernanceAuditLedgerStore"/> allows looking up records by their actor ID.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ContractAllowsLookupByActorId()
    {
        IGovernanceAuditLedgerStore store = new TestAuditLedgerStore();
        AuditLedgerRecord firstRecord = CreateRecord("record-1", "correlation-1", "trace-1", "actor-123");
        AuditLedgerRecord secondRecord = CreateRecord("record-2", "correlation-2", "trace-2", "actor-123");
        AuditLedgerRecord thirdRecord = CreateRecord("record-3", "correlation-3", "trace-3", "actor-456");

        _ = await store.AppendAsync(firstRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(secondRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(thirdRecord, TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLedgerRecord> found = await store.FindByActorIdAsync(
            "actor-123",
            TestContext.Current.CancellationToken);

        AuditLedgerRecord[] expected = [firstRecord, secondRecord];

        Assert.Equal(expected, found);
    }

    /// <summary>
    /// Verifies that an implementation of <see cref="IGovernanceAuditLedgerStore"/> allows looking up records by recorded UTC range.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ContractAllowsLookupByRecordedUtcRange()
    {
        IGovernanceAuditLedgerStore store = new TestAuditLedgerStore();
        AuditLedgerRecord firstRecord = CreateRecord(
            "record-1",
            "correlation-1",
            "trace-1",
            "actor-1",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        AuditLedgerRecord secondRecord = CreateRecord(
            "record-2",
            "correlation-2",
            "trace-2",
            "actor-2",
            new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero));
        AuditLedgerRecord thirdRecord = CreateRecord(
            "record-3",
            "correlation-3",
            "trace-3",
            "actor-3",
            new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero));

        _ = await store.AppendAsync(firstRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(secondRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(thirdRecord, TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLedgerRecord> found = await store.FindByRecordedUtcRangeAsync(
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        AuditLedgerRecord[] expected = [secondRecord];

        Assert.Equal(expected, found);
    }

    private static AuditLedgerRecord CreateRecord(
        string recordId,
        string correlationId,
        string traceId,
        string actorId,
        DateTimeOffset? recordedUtc = null)
    {
        var actor = GovernanceActorContext.Human(actorId);
        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed",
            eventId: $"event-{recordId}",
            correlationId: correlationId,
            traceId: traceId);

        return AuditLedgerRecord.FromResidue(
            residue,
            recordId: recordId,
            recordedUtc: recordedUtc);
    }

    private sealed class TestAuditLedgerStore : IGovernanceAuditLedgerStore
    {
        private readonly Dictionary<string, AuditLedgerRecord> records = new(StringComparer.Ordinal);

        public ValueTask<OperationResult<AuditLedgerRecord>> AppendAsync(
            AuditLedgerRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            records[record.RecordId] = record;

            return ValueTask.FromResult(OperationResult.Success(record));
        }

        public ValueTask<AuditLedgerRecord?> FindByRecordIdAsync(
            string recordId,
            CancellationToken cancellationToken = default)
        {
            _ = records.TryGetValue(recordId, out AuditLedgerRecord? record);

            return ValueTask.FromResult(record);
        }

        public ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByCorrelationIdAsync(
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            return FindAsync(record => string.Equals(record.CorrelationId, correlationId, StringComparison.Ordinal));
        }

        public ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByTraceIdAsync(
            string traceId,
            CancellationToken cancellationToken = default)
        {
            return FindAsync(record => string.Equals(record.TraceId, traceId, StringComparison.Ordinal));
        }

        public ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByActorIdAsync(
            string actorId,
            CancellationToken cancellationToken = default)
        {
            return FindAsync(record => string.Equals(record.ActorId, actorId, StringComparison.Ordinal));
        }

        public ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByRecordedUtcRangeAsync(
            DateTimeOffset recordedFromUtc,
            DateTimeOffset recordedToUtc,
            CancellationToken cancellationToken = default)
        {
            return FindAsync(record => record.RecordedUtc >= recordedFromUtc && record.RecordedUtc <= recordedToUtc);
        }

        private ValueTask<IReadOnlyList<AuditLedgerRecord>> FindAsync(Func<AuditLedgerRecord, bool> predicate)
        {
            IReadOnlyList<AuditLedgerRecord> matches = records.Values
                .Where(predicate)
                .OrderBy(record => record.RecordedUtc)
                .ThenBy(record => record.RecordId)
                .ToArray();

            return ValueTask.FromResult(matches);
        }
    }
}
