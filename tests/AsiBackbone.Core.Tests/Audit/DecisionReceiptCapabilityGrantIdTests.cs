using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Tests that a validated capability grant identifier travels from the decision receipt to the audit ledger record.
/// </summary>
public sealed class DecisionReceiptCapabilityGrantIdTests
{
    private static readonly DateTimeOffset OccurredUtc = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies the copy carries the grant identifier and preserves every other receipt value.
    /// </summary>
    [Fact]
    public void WithCapabilityGrantIdPreservesReceiptValues()
    {
        DecisionReceipt receipt = CreateReceipt();

        DecisionReceipt withGrant = receipt.WithCapabilityGrantId(" grant-1 ");

        Assert.Null(receipt.CapabilityGrantId);
        Assert.Equal("grant-1", withGrant.CapabilityGrantId);
        Assert.Equal(receipt.EventId, withGrant.EventId);
        Assert.Equal(receipt.DecisionReceiptId, withGrant.DecisionReceiptId);
        Assert.Equal(receipt.OccurredUtc, withGrant.OccurredUtc);
        Assert.Equal(receipt.ActorId, withGrant.ActorId);
        Assert.Equal(receipt.Outcome, withGrant.Outcome);
        Assert.Equal(receipt.ReasonCodes, withGrant.ReasonCodes);
        Assert.Equal(receipt.PolicyVersion, withGrant.PolicyVersion);
        Assert.Equal(receipt.DecisionStage, withGrant.DecisionStage);
        Assert.Equal(receipt.Metadata, withGrant.Metadata);
        Assert.Null(withGrant.WithCapabilityGrantId("  ").CapabilityGrantId);
    }

    /// <summary>
    /// Verifies a receipt without a grant identifier hashes exactly as before, and naming a grant changes the hash.
    /// </summary>
    [Fact]
    public void ReceiptPayloadBindsGrantIdentifierOnlyWhenPresent()
    {
        DecisionReceipt receipt = CreateReceipt();

        string withoutGrant = CanonicalPayloadBuilder.ForDecisionReceipt(receipt).CanonicalJson;
        string clearedGrant = CanonicalPayloadBuilder.ForDecisionReceipt(receipt.WithCapabilityGrantId(null)).CanonicalJson;
        string withGrant = CanonicalPayloadBuilder.ForDecisionReceipt(receipt.WithCapabilityGrantId("grant-1")).CanonicalJson;

        Assert.DoesNotContain("capabilityGrantId", withoutGrant, StringComparison.Ordinal);
        Assert.Equal(withoutGrant, clearedGrant);
        Assert.Contains("\"capabilityGrantId\":\"grant-1\"", withGrant, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the ledger record takes the receipt grant identifier, and an explicit identifier takes precedence.
    /// </summary>
    [Fact]
    public void AuditLedgerRecordUsesReceiptGrantIdentifier()
    {
        DecisionReceipt receipt = CreateReceipt().WithCapabilityGrantId("grant-1");

        var fromReceipt = AuditLedgerRecord.FromDecisionReceipt(receipt, recordId: "record-1", recordedUtc: OccurredUtc);
        var explicitId = AuditLedgerRecord.FromDecisionReceipt(receipt, capabilityTokenId: "grant-explicit");
        var blankExplicit = AuditLedgerRecord.FromDecisionReceipt(receipt, capabilityTokenId: " ");

        Assert.Equal("grant-1", fromReceipt.CapabilityTokenId);
        Assert.Equal("grant-1", ((IDecisionReceipt)fromReceipt).CapabilityGrantId);
        Assert.Equal("grant-explicit", explicitId.CapabilityTokenId);
        Assert.Equal("grant-1", blankExplicit.CapabilityTokenId);
        Assert.Contains(
            "\"capabilityGrantId\":\"grant-1\"",
            CanonicalPayloadBuilder.ForAuditLedgerRecord(fromReceipt).CanonicalJson,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the ledger record hash binds the grant identifier and rehydrates from the persisted value.
    /// </summary>
    [Fact]
    public void AuditLedgerRecordHashBindsGrantIdentifier()
    {
        DecisionReceipt receipt = CreateReceipt().WithCapabilityGrantId("grant-1");
        var record = AuditLedgerRecord.FromDecisionReceipt(receipt, recordId: "record-1", recordedUtc: OccurredUtc);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(record));

        var rehydrated = AuditLedgerRecord.FromDecisionReceipt(
            CreateReceipt(),
            recordId: "record-1",
            recordedUtc: OccurredUtc,
            capabilityTokenId: record.CapabilityTokenId);
        var withoutGrant = AuditLedgerRecord.FromDecisionReceipt(CreateReceipt(), recordId: "record-1", recordedUtc: OccurredUtc);

        Assert.Equal(
            hash.HashValue,
            CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(rehydrated)).HashValue);
        Assert.NotEqual(
            hash.HashValue,
            CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(withoutGrant)).HashValue);
    }

    /// <summary>
    /// Verifies the receipt builder carries the grant identifier.
    /// </summary>
    [Fact]
    public void DecisionReceiptBuilderCarriesGrantIdentifier()
    {
        DecisionReceipt receipt = DecisionReceiptBuilder
            .FromDecision(CreateActor(), "orders.approve", GovernanceDecision.Allow())
            .WithCapabilityGrantId("grant-1")
            .Build();
        DecisionReceipt withoutGrant = DecisionReceiptBuilder
            .FromDecision(CreateActor(), "orders.approve", GovernanceDecision.Allow())
            .Build();

        Assert.Equal("grant-1", receipt.CapabilityGrantId);
        Assert.Null(withoutGrant.CapabilityGrantId);
    }

    private static DecisionReceipt CreateReceipt()
    {
        return DecisionReceipt.FromDecision(
            CreateActor(),
            "orders.approve",
            GovernanceDecision.Allow(correlationId: "correlation-1", policyVersion: "policy-v1", policyHash: "policy-hash-1"),
            eventId: "event-1",
            occurredUtc: OccurredUtc,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "test" },
            decisionStage: "test.stage");
    }

    private static GovernanceActorContext CreateActor()
    {
        return GovernanceActorContext.Human(actorId: "user-1", displayName: "User One");
    }
}
