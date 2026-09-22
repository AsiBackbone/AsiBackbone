using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.HostIntegration;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.HostIntegration;

/// <summary>
/// Verifies framework-neutral governed execution receipts, lifecycle correlation, and canonical signing behavior.
/// </summary>
public sealed class GovernedOperationExecutionReceiptTests
{
    /// <summary>
    /// Verifies committed mutation evidence is normalized and exposed through stable lifecycle metadata.
    /// </summary>
    [Fact]
    public void CommittedReceiptCarriesOpaqueMutationBinding()
    {
        var receipt = GovernedOperationExecutionReceipt.Create(
            " operation-1 ",
            GovernedOperationPersistenceOutcome.Committed,
            executionAttemptId: " attempt-2 ",
            mutationBatchId: " batch-7 ",
            mutationRecordCount: 3,
            mutationManifestHash: " ABCDEF ",
            mutationManifestAlgorithm: " SHA-256 ",
            persistenceProvider: " relational ",
            decisionAuditRecordId: " record-1 ");

        Assert.Equal("operation-1", receipt.OperationExecutionId);
        Assert.Equal("attempt-2", receipt.ExecutionAttemptId);
        Assert.Equal("batch-7", receipt.MutationBatchId);
        Assert.Equal(3, receipt.MutationRecordCount);
        Assert.Equal("abcdef", receipt.MutationManifestHash);
        Assert.True(receipt.HasCommittedMutation);
        Assert.False(receipt.CompletedWithoutMutation);

        IReadOnlyDictionary<string, string> metadata = receipt.ToLifecycleMetadata();
        Assert.Equal("operation-1", metadata[HostAccountabilityMetadataKeys.OperationExecutionId]);
        Assert.Equal("attempt-2", metadata[HostAccountabilityMetadataKeys.ExecutionAttemptId]);
        Assert.Equal("batch-7", metadata[HostAccountabilityMetadataKeys.MutationBatchId]);
        Assert.Equal("3", metadata[HostAccountabilityMetadataKeys.MutationRecordCount]);
        Assert.Equal("false", metadata[HostAccountabilityMetadataKeys.CompletedWithoutMutation]);
    }

    /// <summary>
    /// Verifies no-mutation completion cannot claim a host mutation batch.
    /// </summary>
    [Fact]
    public void CompletedWithoutMutationHasNoMutationBinding()
    {
        var receipt = GovernedOperationExecutionReceipt.Create(
            "operation-1",
            GovernedOperationPersistenceOutcome.CompletedWithoutMutation,
            executionAttemptId: "attempt-1");

        Assert.True(receipt.CompletedWithoutMutation);
        Assert.False(receipt.HasCommittedMutation);
        Assert.Null(receipt.MutationBatchId);
        Assert.Equal(0, receipt.MutationRecordCount);
    }

    /// <summary>
    /// Verifies failed and rolled-back attempts remain distinguishable while sharing one logical operation identity.
    /// </summary>
    [Theory]
    [InlineData(GovernedOperationPersistenceOutcome.Failed, "attempt-1")]
    [InlineData(GovernedOperationPersistenceOutcome.RolledBack, "attempt-2")]
    public void RetryAttemptsPreserveLogicalOperationIdentity(
        GovernedOperationPersistenceOutcome outcome,
        string attemptId)
    {
        var receipt = GovernedOperationExecutionReceipt.Create(
            "operation-1",
            outcome,
            executionAttemptId: attemptId);

        Assert.Equal("operation-1", receipt.OperationExecutionId);
        Assert.Equal(attemptId, receipt.ExecutionAttemptId);
        Assert.Equal(outcome, receipt.PersistenceOutcome);
        Assert.False(receipt.HasCommittedMutation);
    }

    /// <summary>
    /// Verifies non-committed outcomes cannot falsely claim committed mutation evidence.
    /// </summary>
    [Fact]
    public void FailedReceiptRejectsMutationBinding()
    {
        _ = Assert.Throws<ArgumentException>(() => GovernedOperationExecutionReceipt.Create(
            "operation-1",
            GovernedOperationPersistenceOutcome.Failed,
            mutationBatchId: "batch-1",
            mutationRecordCount: 1,
            mutationManifestHash: "hash",
            mutationManifestAlgorithm: "SHA-256"));
    }

    /// <summary>
    /// Verifies lifecycle helpers preserve original decision correlation and add receipt evidence.
    /// </summary>
    [Fact]
    public void CompletionLifecycleEventPreservesDecisionCorrelation()
    {
        var residue = new TestDecisionReceipt();
        var receipt = GovernedOperationExecutionReceipt.Create(
            "operation-1",
            GovernedOperationPersistenceOutcome.Committed,
            executionAttemptId: "attempt-1",
            mutationBatchId: "batch-1",
            mutationRecordCount: 2,
            mutationManifestHash: "abcdef",
            mutationManifestAlgorithm: "SHA-256",
            completedUtc: new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-5)),
            decisionAuditRecordId: "decision-record-1");

        DecisionReceiptLifecycleEvent lifecycleEvent = HostAccountabilityLifecycleEvent.FromExecutionReceipt(
            residue,
            receipt,
            eventId: "lifecycle-1");

        Assert.Equal(DecisionReceiptLifecycleStage.GatewayExecutionCompleted, lifecycleEvent.Stage);
        Assert.Equal("correlation-1", lifecycleEvent.CorrelationId);
        Assert.Equal("trace-1", lifecycleEvent.TraceId);
        Assert.Equal("audit-1", lifecycleEvent.DecisionReceiptId);
        Assert.Equal("orders.approve", lifecycleEvent.OperationName);
        Assert.Equal("Committed", lifecycleEvent.Outcome);
        Assert.Equal("2026-07-17T17:00:00.0000000+00:00", lifecycleEvent.OccurredUtc.ToString("O"));
        Assert.Equal("operation-1", lifecycleEvent.Metadata[HostAccountabilityMetadataKeys.OperationExecutionId]);
        Assert.Equal("decision-record-1", lifecycleEvent.Metadata[HostAccountabilityMetadataKeys.DecisionAuditRecordId]);
    }

    /// <summary>
    /// Verifies receipt canonicalization is deterministic and compatible with the existing payload hasher.
    /// </summary>
    [Fact]
    public void EquivalentReceiptsProduceStableCanonicalHash()
    {
        GovernedOperationExecutionReceipt first = CreateCommittedReceipt(new Dictionary<string, string>
        {
            ["safe"] = "included",
            ["ignored"] = "one"
        });
        GovernedOperationExecutionReceipt second = CreateCommittedReceipt(new Dictionary<string, string>
        {
            ["ignored"] = "two",
            ["safe"] = "included"
        });
        var options = CanonicalPayloadOptions.Create(["safe"]);

        CanonicalPayload firstPayload = GovernedOperationExecutionReceiptCanonicalPayload.Create(first, options);
        CanonicalPayload secondPayload = GovernedOperationExecutionReceiptCanonicalPayload.Create(second, options);
        CanonicalPayloadHash firstHash = CanonicalPayloadHasher.ComputeHash(firstPayload);
        CanonicalPayloadHash secondHash = CanonicalPayloadHasher.ComputeHash(secondPayload);

        Assert.Equal(CanonicalArtifactTypes.GovernedOperationExecutionReceipt, firstPayload.ArtifactType);
        Assert.Equal("operation-1:attempt-1", firstPayload.ArtifactId);
        Assert.Equal(firstPayload.CanonicalJson, secondPayload.CanonicalJson);
        Assert.Equal(firstHash.HashValue, secondHash.HashValue);

        using var document = JsonDocument.Parse(firstPayload.CanonicalJson);
        JsonElement content = document.RootElement.GetProperty("content");
        Assert.Equal("Committed", content.GetProperty("persistenceOutcome").GetString());
        Assert.Equal("included", content.GetProperty("metadata").GetProperty("safe").GetString());
        Assert.False(content.GetProperty("metadata").TryGetProperty("ignored", out _));
    }

    private static GovernedOperationExecutionReceipt CreateCommittedReceipt(IReadOnlyDictionary<string, string> metadata)
    {
        return GovernedOperationExecutionReceipt.Create(
            "operation-1",
            GovernedOperationPersistenceOutcome.Committed,
            executionAttemptId: "attempt-1",
            mutationBatchId: "batch-1",
            mutationRecordCount: 2,
            mutationManifestHash: "abcdef",
            mutationManifestAlgorithm: "SHA-256",
            completedUtc: new DateTimeOffset(2026, 7, 17, 17, 0, 0, TimeSpan.Zero),
            persistenceProvider: "relational",
            decisionAuditRecordId: "decision-record-1",
            metadata: metadata);
    }

    private sealed class TestDecisionReceipt : IDecisionReceipt
    {
        public string EventId => "audit-1";
        public string? DecisionReceiptId => "audit-1";
        public DateTimeOffset OccurredUtc => DateTimeOffset.UtcNow;
        public string ActorId => "actor-1";
        public GovernanceActorType ActorType => GovernanceActorType.Human;
        public string? ActorDisplayName => "Actor";
        public string OperationName => "orders.approve";
        public string Outcome => "Allowed";
        public IReadOnlyList<string> ReasonCodes => Array.Empty<string>();
        public string? CorrelationId => "correlation-1";
        public string? TraceId => "trace-1";
        public string? PolicyVersion => "policy-v1";
        public string? PolicyHash => "policy-hash-1";
        public IReadOnlyDictionary<string, string> Metadata => new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
