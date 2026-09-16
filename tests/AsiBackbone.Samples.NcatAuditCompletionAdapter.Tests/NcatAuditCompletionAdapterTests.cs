using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.HostIntegration;
using AsiBackbone.Storage.InMemory.Audit;
using Xunit;

namespace AsiBackbone.Samples.NcatAuditCompletionAdapter.Tests;

/// <summary>
/// Verifies the optional NCAT completion adapter boundary and delivery semantics.
/// </summary>
public sealed class NcatAuditCompletionAdapterTests
{
    /// <summary>
    /// Verifies committed completion evidence is bound to the mutation batch and appended before acknowledgement.
    /// </summary>
    [Fact]
    public async Task CommittedHandoffAppendsBoundLifecycleEvent()
    {
        InMemoryDecisionReceiptLifecycleStore store = new();
        NcatAuditCompletionAdapter adapter = CreateAdapter(store);

        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(
            CreateCommittedHandoff(),
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Delivered, result.Disposition);
        Assert.True(result.ShouldAcknowledgeSource);
        Assert.NotNull(result.Receipt);
        Assert.NotNull(result.LifecycleEvent);
        Assert.Equal(GovernedOperationPersistenceOutcome.Committed, result.Receipt.PersistenceOutcome);
        Assert.Equal("batch-1", result.Receipt.MutationBatchId);
        Assert.Equal(2, result.Receipt.MutationRecordCount);
        Assert.Equal("abcdef", result.Receipt.MutationManifestHash);
        Assert.Equal("operation-1", result.LifecycleEvent.Metadata[HostAccountabilityMetadataKeys.OperationExecutionId]);
        Assert.Equal("attempt-1", result.LifecycleEvent.Metadata[HostAccountabilityMetadataKeys.ExecutionAttemptId]);
        Assert.Equal("decision-1", result.LifecycleEvent.Metadata[HostAccountabilityMetadataKeys.DecisionAuditRecordId]);
        Assert.Equal("correlation-1", result.LifecycleEvent.CorrelationId);
        Assert.Equal("trace-1", result.LifecycleEvent.TraceId);
        Assert.Equal("completion-1", result.LifecycleEvent.Metadata["ncatCompletionEntryId"]);
        Assert.DoesNotContain(result.LifecycleEvent.Metadata.Keys, key => key.Contains("original", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.LifecycleEvent.Metadata.Keys, key => key.Contains("current", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies source outcome names map to the framework-neutral AsiBackbone persistence outcomes.
    /// </summary>
    [Theory]
    [InlineData("failed", GovernedOperationPersistenceOutcome.Failed)]
    [InlineData("rolled-back", GovernedOperationPersistenceOutcome.RolledBack)]
    [InlineData("no-mutation", GovernedOperationPersistenceOutcome.CompletedWithoutMutation)]
    [InlineData("completed_without_mutation", GovernedOperationPersistenceOutcome.CompletedWithoutMutation)]
    public async Task NonCommittedOutcomesDoNotClaimMutationBinding(
        string sourceOutcome,
        GovernedOperationPersistenceOutcome expectedOutcome)
    {
        NcatAuditCompletionAdapter adapter = CreateAdapter(new InMemoryDecisionReceiptLifecycleStore());
        NcatAuditCompletionHandoff handoff = CreateCommittedHandoff() with
        {
            PersistenceOutcome = sourceOutcome,
            MutationBatchId = null,
            AuditRecordCount = 0,
            MutationManifestHash = null,
            MutationManifestAlgorithm = null
        };

        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(
            handoff,
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Delivered, result.Disposition);
        Assert.Equal(expectedOutcome, result.Receipt!.PersistenceOutcome);
        Assert.False(result.Receipt.HasCommittedMutation);
    }

    /// <summary>
    /// Verifies committed completion cannot omit its canonical manifest binding.
    /// </summary>
    [Fact]
    public async Task CommittedHandoffWithoutManifestIsTerminal()
    {
        NcatAuditCompletionAdapter adapter = CreateAdapter(new InMemoryDecisionReceiptLifecycleStore());
        NcatAuditCompletionHandoff handoff = CreateCommittedHandoff() with
        {
            MutationManifestHash = null,
            MutationManifestAlgorithm = null
        };

        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(
            handoff,
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Terminal, result.Disposition);
        Assert.Equal("invalid-execution-receipt", result.ReasonCode);
        Assert.False(result.ShouldAcknowledgeSource);
    }

    /// <summary>
    /// Verifies missing decision evidence defers rather than losing the source completion entry.
    /// </summary>
    [Fact]
    public async Task MissingDecisionResidueDefersDelivery()
    {
        NcatAuditCompletionAdapter adapter = new(
            new InMemoryDecisionReceiptLifecycleStore(),
            new StubResolver(null));

        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(
            CreateCommittedHandoff(),
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Deferred, result.Disposition);
        Assert.False(result.ShouldAcknowledgeSource);
    }

    /// <summary>
    /// Verifies mismatched correlation cannot join unrelated audit trails.
    /// </summary>
    [Fact]
    public async Task CorrelationMismatchIsTerminal()
    {
        NcatAuditCompletionAdapter adapter = CreateAdapter(new InMemoryDecisionReceiptLifecycleStore());
        NcatAuditCompletionHandoff handoff = CreateCommittedHandoff() with { CorrelationId = "other" };

        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(
            handoff,
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Terminal, result.Disposition);
        Assert.Equal("correlation-id-mismatch", result.ReasonCode);
    }

    /// <summary>
    /// Verifies duplicate source delivery is safely detectable through a deterministic lifecycle event identifier.
    /// </summary>
    [Fact]
    public async Task DuplicateHandoffIsAcknowledgedWithoutSecondAppend()
    {
        InMemoryDecisionReceiptLifecycleStore store = new();
        NcatAuditCompletionAdapter adapter = CreateAdapter(store);
        NcatAuditCompletionHandoff handoff = CreateCommittedHandoff();

        NcatAuditCompletionDeliveryResult first = await adapter.DeliverAsync(
            handoff,
            TestContext.Current.CancellationToken);
        NcatAuditCompletionDeliveryResult second = await adapter.DeliverAsync(
            handoff,
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Delivered, first.Disposition);
        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Duplicate, second.Disposition);
        Assert.Equal(first.LifecycleEventId, second.LifecycleEventId);
        Assert.True(second.ShouldAcknowledgeSource);
    }

    /// <summary>
    /// Verifies lifecycle persistence failure remains retryable and never falsely acknowledges the source entry.
    /// </summary>
    [Fact]
    public async Task LifecyclePersistenceFailureRemainsRetryable()
    {
        NcatAuditCompletionAdapter adapter = CreateAdapter(new ThrowingLifecycleStore());

        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(
            CreateCommittedHandoff(),
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Retryable, result.Disposition);
        Assert.Equal("lifecycle-persistence-failed", result.ReasonCode);
        Assert.False(result.ShouldAcknowledgeSource);
        Assert.Equal(nameof(InvalidOperationException), result.FailureType);
    }

    /// <summary>
    /// Verifies the host may classify exhausted retries as dead-letter without claiming delivery.
    /// </summary>
    [Fact]
    public async Task ExhaustedPersistenceRetriesReturnDeadLetter()
    {
        NcatAuditCompletionAdapter adapter = CreateAdapter(
            new ThrowingLifecycleStore(),
            new NcatAuditCompletionAdapterOptions { DeadLetterAfterAttempts = 3 });
        NcatAuditCompletionHandoff handoff = CreateCommittedHandoff() with { DeliveryAttempt = 3 };

        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(
            handoff,
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.DeadLetter, result.Disposition);
        Assert.False(result.ShouldAcknowledgeSource);
    }

    /// <summary>
    /// Verifies invalid or incomplete source identifiers are rejected before persistence.
    /// </summary>
    [Theory]
    [InlineData("", "operation-1", "decision-1", "completion-entry-id-required")]
    [InlineData("completion-1", "", "decision-1", "operation-execution-id-required")]
    [InlineData("completion-1", "operation-1", "", "decision-audit-record-id-required")]
    public async Task MissingIdentifiersAreTerminal(
        string completionEntryId,
        string operationExecutionId,
        string decisionAuditRecordId,
        string expectedReason)
    {
        NcatAuditCompletionAdapter adapter = CreateAdapter(new InMemoryDecisionReceiptLifecycleStore());
        NcatAuditCompletionHandoff handoff = CreateCommittedHandoff() with
        {
            CompletionEntryId = completionEntryId,
            OperationExecutionId = operationExecutionId,
            DecisionAuditRecordId = decisionAuditRecordId
        };

        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(
            handoff,
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Terminal, result.Disposition);
        Assert.Equal(expectedReason, result.ReasonCode);
    }

    private static NcatAuditCompletionAdapter CreateAdapter(
        IDecisionReceiptLifecycleStore store,
        NcatAuditCompletionAdapterOptions? options = null)
    {
        return new NcatAuditCompletionAdapter(store, new StubResolver(new TestAuditResidue()), options);
    }

    private static NcatAuditCompletionHandoff CreateCommittedHandoff()
    {
        return new NcatAuditCompletionHandoff(
            CompletionEntryId: "completion-1",
            PersistenceOutcome: "committed",
            CompletedUtc: new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero),
            OperationExecutionId: "operation-1",
            ExecutionAttemptId: "attempt-1",
            DecisionAuditRecordId: "decision-1",
            CorrelationId: "correlation-1",
            TraceId: "trace-1",
            MutationBatchId: "batch-1",
            AuditRecordCount: 2,
            MutationManifestHash: "ABCDEF",
            MutationManifestAlgorithm: "SHA-256",
            DeliveryAttempt: 1);
    }

    private sealed class StubResolver(IDecisionReceipt? residue) : INcatDecisionResidueResolver
    {
        public ValueTask<IDecisionReceipt?> ResolveAsync(
            string decisionAuditRecordId,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(residue);
        }
    }

    private sealed class ThrowingLifecycleStore : IDecisionReceiptLifecycleStore
    {
        public ValueTask<DecisionReceiptLifecycleEvent> AppendAsync(
            DecisionReceiptLifecycleEvent lifecycleEvent,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated durable-store failure.");
        }

        public ValueTask<DecisionReceiptLifecycleEvent?> FindByEventIdAsync(
            string eventId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<DecisionReceiptLifecycleEvent?>(null);
        }

        public ValueTask<IReadOnlyList<DecisionReceiptLifecycleEvent>> FindByCorrelationIdAsync(
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<DecisionReceiptLifecycleEvent>>([]);
        }

        public ValueTask<IReadOnlyList<DecisionReceiptLifecycleEvent>> FindByAuditResidueIdAsync(
            string auditResidueId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<DecisionReceiptLifecycleEvent>>([]);
        }
    }

    private sealed class TestAuditResidue : IDecisionReceipt
    {
        public string EventId => "decision-1";
        public string? AuditResidueId => "decision-1";
        public DateTimeOffset OccurredUtc => DateTimeOffset.UtcNow;
        public string ActorId => "actor-1";
        public GovernanceActorType ActorType => GovernanceActorType.Human;
        public string? ActorDisplayName => "Actor";
        public string OperationName => "orders.update";
        public string Outcome => "Allowed";
        public IReadOnlyList<string> ReasonCodes => [];
        public string? CorrelationId => "correlation-1";
        public string? TraceId => "trace-1";
        public string? PolicyVersion => "policy-v1";
        public string? PolicyHash => "policy-hash-1";
        public IReadOnlyDictionary<string, string> Metadata => new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
