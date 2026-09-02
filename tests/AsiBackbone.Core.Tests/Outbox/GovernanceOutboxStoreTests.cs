using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Audit;
using AsiBackbone.Storage.InMemory.Outbox;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Tests for provider-neutral governance outbox persistence and lifecycle durability.
/// </summary>
public sealed class GovernanceOutboxStoreTests
{
    /// <summary>
    /// Verifies that a governance emission envelope can be saved before any external provider delivery is attempted.
    /// </summary>
    [Fact]
    public async Task EnqueueAsyncPersistsPendingGovernanceEmissionEnvelope()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceEmissionEnvelope envelope = CreateEnvelope("event-1", "correlation-1");

        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(envelope, TestContext.Current.CancellationToken);

        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> pendingEntries = await outboxStore.FindPendingAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(storedEntry);
        Assert.Same(envelope, storedEntry.Envelope);
        Assert.Equal(GovernanceEmissionStatus.Pending, storedEntry.Status);
        Assert.Contains(pendingEntries, pendingEntry => pendingEntry.OutboxEntryId == entry.OutboxEntryId);
    }

    /// <summary>
    /// Verifies that local lifecycle audit records remain available when a downstream provider emission fails.
    /// </summary>
    [Fact]
    public async Task FailedEmissionDoesNotLoseLocalLifecycleAuditRecord()
    {
        var lifecycleStore = new InMemoryAuditResidueLifecycleStore();
        var outboxStore = new InMemoryGovernanceOutboxStore();

        var lifecycleEvent = AuditResidueLifecycleEvent.Create(
            AuditResidueLifecycleStage.ExternalEmissionQueued,
            "correlation-1",
            auditResidueId: "residue-1",
            eventId: "lifecycle-1",
            traceId: "trace-1",
            operationName: "governance.emit");

        AuditResidueLifecycleEvent savedLifecycleEvent = await lifecycleStore.AppendAsync(
            lifecycleEvent,
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            GovernanceEmissionEnvelope.FromLifecycleEvent(savedLifecycleEvent),
            TestContext.Current.CancellationToken);

        var error = GovernanceEmissionError.Create(
            "provider.unavailable",
            "Provider was unavailable.",
            isRetryable: true,
            providerName: "test-provider");

        GovernanceOutboxEntry failedEntry = await outboxStore.MarkFailedAsync(
            entry.OutboxEntryId,
            error,
            nextRetryUtc: DateTimeOffset.UtcNow.AddMinutes(5),
            cancellationToken: TestContext.Current.CancellationToken);

        AuditResidueLifecycleEvent? storedLifecycleEvent = await lifecycleStore.FindByEventIdAsync(
            "lifecycle-1",
            TestContext.Current.CancellationToken);

        Assert.NotNull(storedLifecycleEvent);
        Assert.Equal("correlation-1", storedLifecycleEvent.CorrelationId);
        Assert.Equal("residue-1", storedLifecycleEvent.AuditResidueId);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, failedEntry.Status);
        Assert.Equal(1, failedEntry.RetryCount);
        Assert.Equal("test-provider", failedEntry.ProviderName);
        Assert.Equal("provider.unavailable", failedEntry.LastError?.Code);
    }

    /// <summary>
    /// Verifies that retry-ready entries are returned only after their retry time is reached.
    /// </summary>
    [Fact]
    public async Task FindRetryReadyAsyncReturnsEntriesAfterNextRetryTime()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1", "correlation-1"),
            TestContext.Current.CancellationToken);

        DateTimeOffset retryUtc = new(2026, 6, 15, 15, 0, 0, TimeSpan.Zero);
        var error = GovernanceEmissionError.Create(
            "provider.timeout",
            "Provider timed out.",
            isRetryable: true);

        GovernanceOutboxEntry failedEntry = await outboxStore.MarkFailedAsync(
            entry.OutboxEntryId,
            error,
            retryUtc,
            TestContext.Current.CancellationToken);

        IReadOnlyList<GovernanceOutboxEntry> beforeRetry = await outboxStore.FindRetryReadyAsync(
            retryUtc.AddTicks(-1),
            cancellationToken: TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> afterRetry = await outboxStore.FindRetryReadyAsync(
            retryUtc,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, failedEntry.Status);
        Assert.DoesNotContain(beforeRetry, retryReadyEntry => retryReadyEntry.OutboxEntryId == entry.OutboxEntryId);
        Assert.Contains(afterRetry, retryReadyEntry => retryReadyEntry.OutboxEntryId == entry.OutboxEntryId);
    }

    /// <summary>
    /// Verifies that entries can transition to delivered and are removed from pending work.
    /// </summary>
    [Fact]
    public async Task MarkDeliveredAsyncTransitionsEntryToDelivered()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1", "correlation-1"),
            TestContext.Current.CancellationToken);

        GovernanceOutboxEntry deliveredEntry = await outboxStore.MarkDeliveredAsync(
            entry.OutboxEntryId,
            GovernanceEmissionResult.Delivered(
                providerName: "test-provider",
                providerRecordId: "record-1"),
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> pendingEntries = await outboxStore.FindPendingAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(deliveredEntry.IsDelivered);
        Assert.Equal(GovernanceEmissionStatus.Delivered, deliveredEntry.Status);
        Assert.Equal("test-provider", deliveredEntry.ProviderName);
        Assert.Equal("record-1", deliveredEntry.ProviderRecordId);
        Assert.DoesNotContain(pendingEntries, pendingEntry => pendingEntry.OutboxEntryId == entry.OutboxEntryId);
    }

    /// <summary>
    /// Verifies that concurrent retryable failure updates on the same in-memory entry do not collapse into accidental last-write-wins state.
    /// </summary>
    [Fact]
    public async Task ConcurrentMarkFailedAsyncUpdatesSameEntryWithoutLosingRetryCount()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1", "correlation-1"),
            TestContext.Current.CancellationToken);
        var error = GovernanceEmissionError.Create(
            "provider.timeout",
            "Provider timed out.",
            isRetryable: true,
            providerName: "test-provider");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        const int attemptCount = 4;

        Task<GovernanceOutboxEntry>[] updateTasks = [.. Enumerable.Range(0, attemptCount).Select(_ =>
            Task.Run(async () =>
            {
                await startSignal.Task.WaitAsync(cancellationToken);

                return await outboxStore.MarkFailedAsync(
                    entry.OutboxEntryId,
                    error,
                    DateTimeOffset.UtcNow.AddMinutes(1),
                    cancellationToken);
            }, cancellationToken))];

        startSignal.SetResult();
        _ = await Task.WhenAll(updateTasks);

        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            cancellationToken);

        Assert.NotNull(storedEntry);
        Assert.Equal(attemptCount, storedEntry.RetryCount);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, storedEntry.Status);
        Assert.Equal("provider.timeout", storedEntry.LastError?.Code);
    }

    /// <summary>
    /// Verifies that a delivered same-entry transition remains terminal when it races with a retryable failure transition.
    /// </summary>
    [Fact]
    public async Task ConcurrentDeliveredAndFailedTransitionsKeepDeliveredTerminalState()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1", "correlation-1"),
            TestContext.Current.CancellationToken);
        var error = GovernanceEmissionError.Create(
            "provider.timeout",
            "Provider timed out.",
            isRetryable: true,
            providerName: "test-provider");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<GovernanceOutboxEntry> deliveredTask = Task.Run(async () =>
        {
            await startSignal.Task.WaitAsync(cancellationToken);

            return await outboxStore.MarkDeliveredAsync(
                entry.OutboxEntryId,
                GovernanceEmissionResult.Delivered(
                    providerName: "test-provider",
                    providerRecordId: "record-1"),
                cancellationToken);
        }, cancellationToken);

        Task<GovernanceOutboxEntry> failedTask = Task.Run(async () =>
        {
            await startSignal.Task.WaitAsync(cancellationToken);

            return await outboxStore.MarkFailedAsync(
                entry.OutboxEntryId,
                error,
                DateTimeOffset.UtcNow.AddMinutes(1),
                cancellationToken);
        }, cancellationToken);

        startSignal.SetResult();
        _ = await Task.WhenAll(deliveredTask, failedTask);

        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            cancellationToken);

        Assert.NotNull(storedEntry);
        Assert.True(storedEntry.IsDelivered);
        Assert.Equal(GovernanceEmissionStatus.Delivered, storedEntry.Status);
        Assert.Equal("record-1", storedEntry.ProviderRecordId);
    }

    /// <summary>
    /// Verifies that a stale deferred save cannot overwrite a terminal delivered state in the in-memory store.
    /// </summary>
    [Fact]
    public async Task SaveAsyncDoesNotOverwriteTerminalDeliveredEntryWithDeferredSnapshot()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1", "correlation-1"),
            TestContext.Current.CancellationToken);
        var error = GovernanceEmissionError.Create(
            "provider.deferred",
            "Provider asked the host to retry later.",
            isRetryable: true,
            providerName: "test-provider");
        GovernanceOutboxEntry deferredSnapshot = entry.MarkDeferred(
            error,
            DateTimeOffset.UtcNow.AddMinutes(1));

        GovernanceOutboxEntry deliveredEntry = await outboxStore.MarkDeliveredAsync(
            entry.OutboxEntryId,
            GovernanceEmissionResult.Delivered(
                providerName: "test-provider",
                providerRecordId: "record-1"),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry savedEntry = await outboxStore.SaveAsync(
            deferredSnapshot,
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);

        Assert.True(deliveredEntry.IsDelivered);
        Assert.True(savedEntry.IsDelivered);
        Assert.NotNull(storedEntry);
        Assert.True(storedEntry.IsDelivered);
        Assert.Equal("record-1", storedEntry.ProviderRecordId);
    }

    /// <summary>
    /// Verifies that entries can transition to a terminal dead-letter state.
    /// </summary>
    [Fact]
    public async Task MarkDeadLetteredAsyncTransitionsEntryToTerminalDeadLetterState()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1", "correlation-1"),
            TestContext.Current.CancellationToken);
        var error = GovernanceEmissionError.Create(
            "provider.rejected",
            "Provider rejected the minimized envelope.",
            providerName: "test-provider");

        GovernanceOutboxEntry deadLetteredEntry = await outboxStore.MarkDeadLetteredAsync(
            entry.OutboxEntryId,
            error,
            "policy-quarantine",
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> retryReadyEntries = await outboxStore.FindRetryReadyAsync(
            DateTimeOffset.UtcNow.AddDays(1),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(deadLetteredEntry.IsDeadLettered);
        Assert.Equal(GovernanceEmissionStatus.DeadLettered, deadLetteredEntry.Status);
        Assert.Equal("policy-quarantine", deadLetteredEntry.DeadLetterReason);
        Assert.Equal("provider.rejected", deadLetteredEntry.LastError?.Code);
        Assert.DoesNotContain(retryReadyEntries, retryReadyEntry => retryReadyEntry.OutboxEntryId == entry.OutboxEntryId);
    }

    /// <summary>
    /// Verifies that the semantics of the GovernanceEmissionResult branches are covered in the compiled outbox tests.
    /// </summary>
    [Fact]
    public void GovernanceEmissionResultBranchSemanticsAreCoveredInCompiledOutboxTests()
    {
        var normalizedDelivered = GovernanceEmissionResult.Delivered(
            " provider ",
            " record-1 ",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [" "] = "ignored",
                [" key "] = " value "
            });
        var pending = GovernanceEmissionResult.Pending(" ", new Dictionary<string, string>(StringComparer.Ordinal) { [" "] = "ignored" });
        var retryableError = GovernanceEmissionError.Create(" retry ", " retry message ", isRetryable: true, providerName: " provider ", providerErrorCode: " code ");
        var deferred = GovernanceEmissionResult.Deferred(retryableError, new DateTimeOffset(2026, 7, 8, 8, 30, 0, TimeSpan.FromHours(-4)));
        var failedRetryable = GovernanceEmissionResult.Failed(retryableError);
        var deadLettered = GovernanceEmissionResult.DeadLettered(GovernanceEmissionError.Create("dead", "dead message"));

        Assert.True(normalizedDelivered.IsSuccess);
        Assert.True(normalizedDelivered.IsTerminal);
        Assert.False(normalizedDelivered.ShouldRetry);
        Assert.True(normalizedDelivered.HasMetadata);
        Assert.Equal("provider", normalizedDelivered.ProviderName);
        Assert.Equal("record-1", normalizedDelivered.ProviderRecordId);
        Assert.Equal("value", normalizedDelivered.Metadata["key"]);
        Assert.False(pending.HasMetadata);
        Assert.Null(pending.ProviderName);
        Assert.True(deferred.ShouldRetry);
        Assert.Equal("provider", deferred.ProviderName);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero), deferred.RetryAfterUtc);
        Assert.True(failedRetryable.ShouldRetry);
        Assert.False(deadLettered.ShouldRetry);
        Assert.True(deadLettered.IsTerminal);
        Assert.Equal("retry", retryableError.Code);
        Assert.Equal("retry message", retryableError.Message);
        Assert.Equal("code", retryableError.ProviderErrorCode);
        _ = Assert.Throws<ArgumentException>(() => GovernanceEmissionError.Create(" ", "message"));
        _ = Assert.Throws<ArgumentException>(() => GovernanceEmissionError.Create("code", " "));
        _ = Assert.Throws<ArgumentNullException>(() => GovernanceEmissionResult.Failed(null!));
        _ = Assert.Throws<ArgumentNullException>(() => GovernanceEmissionResult.RetryableFailure(null!));
        _ = Assert.Throws<ArgumentNullException>(() => GovernanceEmissionResult.DeadLettered(null!));
    }

    /// <summary>
    /// Verifies that the semantics of the GovernanceOutboxClaim branches are covered in the compiled outbox tests.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ClaimLeaseBranchesAreCoveredInCompiledOutboxTests()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-claim", "correlation-claim"),
            TestContext.Current.CancellationToken);
        DateTimeOffset claimedUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

        IReadOnlyList<GovernanceOutboxClaim> claims = await outboxStore.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create(" worker-a ", claimedUtc, TimeSpan.FromMinutes(5), maxCount: 1),
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxClaim> blockedClaims = await outboxStore.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("worker-b", claimedUtc.AddMinutes(1), TimeSpan.FromMinutes(5), maxCount: 1),
            TestContext.Current.CancellationToken);
        GovernanceOutboxClaim claim = Assert.Single(claims);
        var mismatchedClaim = GovernanceOutboxClaim.Create(
            claim.Entry,
            claim.WorkerId,
            "mismatched-token",
            claimedUtc,
            claimedUtc.AddMinutes(5));

        GovernanceOutboxEntry mismatchedResult = await outboxStore.MarkClaimDeliveredAsync(
            mismatchedClaim,
            GovernanceEmissionResult.Delivered("provider", "wrong-record"),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry deliveredResult = await outboxStore.MarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("provider", "record-1"),
            TestContext.Current.CancellationToken);

        Assert.Equal("worker-a", claim.WorkerId);
        Assert.False(claim.IsExpired(claimedUtc.AddMinutes(4)));
        Assert.True(claim.IsExpired(claimedUtc.AddMinutes(5)));
        Assert.Empty(blockedClaims);
        Assert.False(mismatchedResult.IsDelivered);
        Assert.True(deliveredResult.IsDelivered);
        Assert.False(deliveredResult.HasClaim);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxClaimRequest.Create("worker-a", leaseDuration: TimeSpan.Zero));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxClaimRequest.Create("worker-a", maxCount: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxClaim.Create(entry, "worker-a", "token", claimedUtc, claimedUtc));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => entry.MarkClaimed("worker-a", leaseDuration: TimeSpan.Zero));
    }

    /// <summary>
    /// Verifies that the semantics of the GovernanceOutboxEntry validation and transition branches are covered in the compiled outbox tests.
    /// </summary>
    [Fact]
    public void GovernanceOutboxEntryValidationAndTransitionBranchesAreCovered()
    {
        GovernanceEmissionEnvelope envelope = CreateEnvelope("event-validation", "correlation-validation");
        DateTimeOffset now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var nonRetryableError = GovernanceEmissionError.Create("provider.failed", "Provider failed.", isRetryable: false, providerName: " provider ");

        _ = Assert.Throws<ArgumentException>(() => GovernanceOutboxEntry.Restore(envelope, GovernanceEmissionStatus.Pending, " ", now, now));
        _ = Assert.Throws<ArgumentNullException>(() => GovernanceOutboxEntry.Restore(null!, GovernanceEmissionStatus.Pending, "entry-1", now, now));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxEntry.Restore(envelope, (GovernanceEmissionStatus)999, "entry-1", now, now));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxEntry.Restore(envelope, GovernanceEmissionStatus.Pending, "entry-1", now, now, retryCount: -1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxEntry.Restore(envelope, GovernanceEmissionStatus.Pending, "entry-1", now, now, maxRetryCount: -1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxEntry.Restore(envelope, GovernanceEmissionStatus.Pending, "entry-1", now, now, claimAttemptCount: -1));

        var entry = GovernanceOutboxEntry.Create(
            envelope,
            " entry-1 ",
            now,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [" "] = "ignored",
                [" key "] = " value "
            });
        GovernanceOutboxEntry failedEntry = entry.MarkFailed(nonRetryableError, now.AddMinutes(1), now.AddSeconds(1));
        GovernanceOutboxEntry legacyMaxRetryEntry = GovernanceOutboxEntry.Create(envelope, "entry-2", now, maxRetryCount: 1)
            .MarkFailed(nonRetryableError, now.AddMinutes(1), now.AddSeconds(1));
        var restoredWithoutFullClaim = GovernanceOutboxEntry.Restore(
            envelope,
            GovernanceEmissionStatus.Pending,
            "entry-3",
            now,
            now,
            claimOwner: " ",
            claimToken: "token",
            claimedUtc: now,
            claimExpiresUtc: now.AddMinutes(5));

        Assert.Equal("entry-1", entry.OutboxEntryId);
        Assert.True(entry.HasMetadata);
        Assert.Equal("value", entry.Metadata["key"]);
        Assert.Equal(GovernanceEmissionStatus.Failed, failedEntry.Status);
        Assert.Equal(now.AddMinutes(1), failedEntry.NextRetryUtc);
        Assert.Equal("provider", failedEntry.ProviderName);
        Assert.Equal(GovernanceEmissionStatus.Failed, legacyMaxRetryEntry.Status);
        Assert.Equal(now.AddMinutes(1), legacyMaxRetryEntry.NextRetryUtc);
        Assert.Null(legacyMaxRetryEntry.DeadLetterReason);
        Assert.False(restoredWithoutFullClaim.HasClaim);
        Assert.True(restoredWithoutFullClaim.CanBeClaimed(now));
        _ = Assert.Throws<ArgumentException>(() => entry.MarkDelivered(GovernanceEmissionResult.Failed(nonRetryableError)));
    }

    /// <summary>
    /// Verifies that the semantics of the GovernanceOutboxOptions validation branches are covered in the compiled outbox tests.
    /// </summary>
    [Fact]
    public void OutboxOptionValidationBranchesAreCoveredInCompiledOutboxTests()
    {
        new AsiBackboneGovernanceOutboxOptions().Validate();

        new AsiBackboneGovernanceOutboxOptions
        {
            UseClaimLeases = true,
            ClaimWorkerId = "worker-1",
            ClaimLeaseDuration = TimeSpan.FromMinutes(2)
        }.Validate();

        _ = Assert.Throws<InvalidOperationException>(() => new AsiBackboneGovernanceOutboxOptions
        {
            RetryDelay = TimeSpan.FromTicks(-1)
        }.Validate());
        _ = Assert.Throws<InvalidOperationException>(() => new AsiBackboneGovernanceOutboxOptions
        {
            DeferredDelay = TimeSpan.FromTicks(-1)
        }.Validate());
        _ = Assert.Throws<InvalidOperationException>(() => new AsiBackboneGovernanceOutboxOptions
        {
            ClaimLeaseDuration = TimeSpan.Zero
        }.Validate());
        _ = Assert.Throws<InvalidOperationException>(() => new AsiBackboneGovernanceOutboxOptions
        {
            UseClaimLeases = true,
            ClaimWorkerId = " "
        }.Validate());
    }

    /// <summary>
    /// Verifies that the semantics of the claimed drain branches are covered in the compiled outbox tests.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ClaimedDrainBranchesAreCoveredInCompiledOutboxTests()
    {
        DateTimeOffset drainUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var selectionOnlyDrain = new AsiBackboneGovernanceOutboxDrain(
            new SelectionOnlyOutboxStore(),
            new ResultEmitter(GovernanceEmissionResult.Delivered("provider", "record")),
            outboxOptions: Options.Create(new AsiBackboneGovernanceOutboxOptions
            {
                UseClaimLeases = true,
                ClaimWorkerId = "worker-1"
            }));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await selectionOnlyDrain.DrainAsync(drainUtc, cancellationToken: TestContext.Current.CancellationToken));

        var emptyStore = new InMemoryGovernanceOutboxStore();
        IReadOnlyList<GovernanceOutboxEntry> emptyDrain = await CreateClaimDrain(
            emptyStore,
            new ResultEmitter(GovernanceEmissionResult.Delivered("provider", "record-empty"))).DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(emptyDrain);

        var deliveredStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry deliveredSource = await deliveredStore.EnqueueAsync(
            CreateEnvelope("event-delivered", "correlation-delivered"),
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> deliveredDrain = await CreateClaimDrain(
            deliveredStore,
            new ResultEmitter(GovernanceEmissionResult.Delivered("provider", "record-delivered"))).DrainAsync(
            drainUtc,
            maxCount: 1,
            cancellationToken: TestContext.Current.CancellationToken);
        GovernanceOutboxEntry deliveredEntry = Assert.Single(deliveredDrain);
        Assert.Equal(deliveredSource.OutboxEntryId, deliveredEntry.OutboxEntryId);
        Assert.True(deliveredEntry.IsDelivered);
        Assert.False(deliveredEntry.HasClaim);

        var retryStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry retrySource = await retryStore.EnqueueAsync(
            CreateEnvelope("event-retry", "correlation-retry"),
            TestContext.Current.CancellationToken);
        _ = await retryStore.MarkFailedAsync(
            retrySource.OutboxEntryId,
            GovernanceEmissionError.Create("provider.transient", "Transient failure.", isRetryable: true),
            drainUtc.AddMinutes(-1),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry retryEntry = Assert.Single(await CreateClaimDrain(
            retryStore,
            new ResultEmitter(GovernanceEmissionResult.Delivered("provider", "record-retry"))).DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(retryEntry.IsDelivered);

        var deferredStore = new InMemoryGovernanceOutboxStore();
        _ = await deferredStore.EnqueueAsync(CreateEnvelope("event-deferred", "correlation-deferred"), TestContext.Current.CancellationToken);
        GovernanceOutboxEntry deferredEntry = Assert.Single(await CreateClaimDrain(
            deferredStore,
            new ResultEmitter(GovernanceEmissionResult.Deferred(
                GovernanceEmissionError.Create("provider.deferred", "Deferred.", isRetryable: true),
                drainUtc.AddMinutes(10),
                "provider"))).DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(GovernanceEmissionStatus.Deferred, deferredEntry.Status);
        Assert.Equal(drainUtc.AddMinutes(10), deferredEntry.NextRetryUtc);
        Assert.False(deferredEntry.HasClaim);

        var pendingStore = new InMemoryGovernanceOutboxStore();
        _ = await pendingStore.EnqueueAsync(CreateEnvelope("event-pending", "correlation-pending"), TestContext.Current.CancellationToken);
        GovernanceOutboxEntry pendingEntry = Assert.Single(await CreateClaimDrain(
            pendingStore,
            new ResultEmitter(GovernanceEmissionResult.Pending("provider"))).DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(GovernanceEmissionStatus.Deferred, pendingEntry.Status);
        Assert.Equal("emission.pending", pendingEntry.LastError?.Code);

        var deadLetterStore = new InMemoryGovernanceOutboxStore();
        _ = await deadLetterStore.EnqueueAsync(CreateEnvelope("event-dead", "correlation-dead"), TestContext.Current.CancellationToken);
        GovernanceOutboxEntry deadLetterEntry = Assert.Single(await CreateClaimDrain(
            deadLetterStore,
            new ResultEmitter(GovernanceEmissionResult.DeadLettered(
                GovernanceEmissionError.Create("provider.dead", "Dead letter.", providerName: "provider")))).DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(deadLetterEntry.IsDeadLettered);
        Assert.False(deadLetterEntry.HasClaim);

        var failedStore = new InMemoryGovernanceOutboxStore();
        _ = await failedStore.EnqueueAsync(CreateEnvelope("event-failed", "correlation-failed"), TestContext.Current.CancellationToken);
        GovernanceOutboxEntry failedEntry = Assert.Single(await CreateClaimDrain(
            failedStore,
            new ResultEmitter(GovernanceEmissionResult.Failed(
                GovernanceEmissionError.Create("provider.failed", "Failed.", isRetryable: false, providerName: "provider")))).DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(GovernanceEmissionStatus.Failed, failedEntry.Status);
        Assert.False(failedEntry.HasClaim);

        var throwingStore = new InMemoryGovernanceOutboxStore();
        _ = await throwingStore.EnqueueAsync(CreateEnvelope("event-throw", "correlation-throw"), TestContext.Current.CancellationToken);
        GovernanceOutboxEntry thrownEntry = Assert.Single(await CreateClaimDrain(
            throwingStore,
            new ThrowingEmitter()).DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, thrownEntry.Status);
        Assert.Equal("emission.exception", thrownEntry.LastError?.Code);
        Assert.Equal(drainUtc.AddMinutes(1), thrownEntry.NextRetryUtc);
    }

    private static AsiBackboneGovernanceOutboxDrain CreateClaimDrain(
        InMemoryGovernanceOutboxStore outboxStore,
        IAsiBackboneGovernanceEmitter emitter)
    {
        return new AsiBackboneGovernanceOutboxDrain(
            outboxStore,
            emitter,
            outboxOptions: Options.Create(new AsiBackboneGovernanceOutboxOptions
            {
                UseClaimLeases = true,
                ClaimWorkerId = "worker-1",
                ClaimLeaseDuration = TimeSpan.FromMinutes(5)
            }));
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string eventId, string correlationId)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditResidue,
            eventId: eventId,
            occurredUtc: new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}",
            correlationId: correlationId,
            auditResidueId: $"residue-{eventId}",
            traceId: $"trace-{eventId}",
            operationName: "governance.emit",
            emitterStatus: "pending",
            emitterProvider: "outbox");
    }

    private sealed class ResultEmitter(GovernanceEmissionResult result) : IAsiBackboneGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingEmitter : IAsiBackboneGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Emitter failed.");
        }
    }

    private sealed class SelectionOnlyOutboxStore : IAsiBackboneGovernanceOutboxStore
    {
        public ValueTask<GovernanceOutboxEntry> EnqueueAsync(GovernanceEmissionEnvelope envelope, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceOutboxEntry.Create(envelope));
        }

        public ValueTask<GovernanceOutboxEntry> SaveAsync(GovernanceOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(entry);
        }

        public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(string outboxEntryId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<GovernanceOutboxEntry?>(null);
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(int maxCount = 100, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(Array.Empty<GovernanceOutboxEntry>());
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(DateTimeOffset utcNow, int maxCount = 100, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(Array.Empty<GovernanceOutboxEntry>());
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(string outboxEntryId, GovernanceEmissionResult result, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkFailedAsync(string outboxEntryId, GovernanceEmissionError governanceEmissionError, DateTimeOffset? nextRetryUtc = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(string outboxEntryId, GovernanceEmissionError governanceEmissionError, string? deadLetterReason = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
