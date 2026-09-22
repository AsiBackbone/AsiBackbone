using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Unit tests for the <see cref="DecisionReceipt"/> class, which represents the audit information captured from a governance decision or constraint evaluation.
/// </summary>
public sealed class DecisionReceiptTests
{
    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.Create"/> method correctly stores all required fields and normalizes input values by trimming whitespace.
    /// </summary>
    [Fact]
    public void CreateStoresRequiredFields()
    {
        var actor = GovernanceActorContext.Human(" user-123 ", " Chris ");
        DateTimeOffset occurredUtc = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

        var residue = DecisionReceipt.Create(
            actor,
            " document.approve ",
            " Allowed ",
            eventId: " event-123 ",
            occurredUtc: occurredUtc);

        Assert.Equal("event-123", residue.EventId);
        Assert.Equal(occurredUtc, residue.OccurredUtc);
        Assert.Equal("user-123", residue.ActorId);
        Assert.Equal(GovernanceActorType.Human, residue.ActorType);
        Assert.Equal("Chris", residue.ActorDisplayName);
        Assert.Equal("document.approve", residue.OperationName);
        Assert.Equal("Allowed", residue.Outcome);
        Assert.Empty(residue.ReasonCodes);
        Assert.False(residue.HasReasonCodes);
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.Create"/> method generates a non-empty EventId when one is not provided, and that it is properly normalized if provided with whitespace.
    /// </summary>
    [Fact]
    public void CreateGeneratesEventIdWhenMissing()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed");

        Assert.False(string.IsNullOrWhiteSpace(residue.EventId));
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.Create"/> method generates a non-empty EventId when the supplied event identifier is whitespace.
    /// </summary>
    [Fact]
    public void CreateGeneratesEventIdWhenEventIdIsWhitespace()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed",
            eventId: "   ");

        Assert.False(string.IsNullOrWhiteSpace(residue.EventId));
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.Create"/> method normalizes reason codes by trimming whitespace and removing empty entries, and that it normalizes trace fields and
    /// </summary>
    [Fact]
    public void CreateNormalizesReasonCodesTraceFieldsAndMetadata()
    {
        var actor = GovernanceActorContext.Service(" service-123 ");

        var residue = DecisionReceipt.Create(
            actor,
            "external.call",
            "Warning",
            reasonCodes: [" risk.high ", "", " policy.warning "],
            eventId: " event-123 ",
            occurredUtc: new DateTimeOffset(2026, 6, 4, 7, 0, 0, TimeSpan.FromHours(-5)),
            correlationId: " correlation-123 ",
            traceId: " trace-456 ",
            policyVersion: " v1 ",
            policyHash: " hash-abc ",
            metadata: new Dictionary<string, string>
            {
                [" region "] = " us-la ",
                [" "] = "ignored",
                ["risk"] = " high "
            });

        Assert.Equal(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero), residue.OccurredUtc);
        Assert.Equal(["risk.high", "policy.warning"], residue.ReasonCodes);
        Assert.True(residue.HasReasonCodes);
        Assert.Equal("correlation-123", residue.CorrelationId);
        Assert.Equal("trace-456", residue.TraceId);
        Assert.Equal("v1", residue.PolicyVersion);
        Assert.Equal("hash-abc", residue.PolicyHash);
        Assert.True(residue.HasMetadata);
        Assert.Equal("us-la", residue.Metadata["region"]);
        Assert.Equal("high", residue.Metadata["risk"]);
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.Create"/> method converts whitespace-only trace fields (CorrelationId, TraceId, PolicyVersion, PolicyHash) to null, as they are considered optional and should not be stored as empty strings.
    /// </summary>
    [Fact]
    public void CreateConvertsWhitespaceTraceFieldsToNull()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed",
            correlationId: " ",
            traceId: "",
            policyVersion: null,
            policyHash: " ");

        Assert.Null(residue.CorrelationId);
        Assert.Null(residue.TraceId);
        Assert.Null(residue.PolicyVersion);
        Assert.Null(residue.PolicyHash);
    }

    /// <summary>
    /// Verifies that null reason codes are filtered and blank reason codes do not create decision receipt entries.
    /// </summary>
    [Fact]
    public void CreateFiltersNullAndBlankReasonCodes()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        string[] reasonCodes = [" policy.warning ", null!, "", "   "];

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Warning",
            reasonCodes: reasonCodes);

        Assert.True(residue.HasReasonCodes);
        Assert.Equal("policy.warning", Assert.Single(residue.ReasonCodes));
    }

    /// <summary>
    /// Verifies that null metadata values are normalized to empty strings.
    /// </summary>
    [Fact]
    public void CreateWithNullMetadataValueStoresEmptyString()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed",
            metadata: new Dictionary<string, string>
            {
                [" source "] = null!
            });

        Assert.True(residue.HasMetadata);
        Assert.Equal(string.Empty, residue.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that empty metadata returns the shared empty metadata representation.
    /// </summary>
    [Fact]
    public void CreateWithEmptyMetadataReturnsNoMetadata()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed",
            metadata: new Dictionary<string, string>());

        Assert.False(residue.HasMetadata);
        Assert.Empty(residue.Metadata);
    }

    /// <summary>
    /// Verifies that metadata containing only blank keys returns no metadata.
    /// </summary>
    [Fact]
    public void CreateWithOnlyBlankMetadataKeysReturnsNoMetadata()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed",
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored",
                ["\t"] = "also ignored"
            });

        Assert.False(residue.HasMetadata);
        Assert.Empty(residue.Metadata);
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.Create"/> method throws an <see cref="ArgumentNullException"/> when the required <c>actor</c> parameter is null.
    /// </summary>
    [Fact]
    public void CreateThrowsForMissingActor()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            DecisionReceipt.Create(
                actor: null!,
                operationName: "document.approve",
                outcome: "Allowed"));
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.Create"/> method throws an <see cref="ArgumentException"/> when the required <paramref name="operationName"/> parameter is null, empty, or whitespace, as an decision receipt must have a valid operation name to be meaningful.
    /// </summary>
    /// <param name="operationName">
    /// The invalid operation name value to test, which can be an empty string or a whitespace string. The test will verify that both cases are properly handled by the method and result in an exception being thrown.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateThrowsForMissingOperationName(string? operationName)
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            DecisionReceipt.Create(
                actor,
                operationName!,
                "Allowed"));
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.Create"/> method throws an <see cref="ArgumentException"/> when the required <paramref name="outcome"/> parameter is null, empty, or whitespace, as an decision receipt must have a valid outcome to indicate the result of the operation being audited.
    /// </summary>
    /// <param name="outcome">
    /// The invalid outcome value to test, which can be an empty string or a whitespace string. The test will verify that both cases are properly handled by the method and result in an exception being thrown.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateThrowsForMissingOutcome(string? outcome)
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            DecisionReceipt.Create(
                actor,
                "document.approve",
                outcome!));
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.FromDecision"/> method correctly copies the outcome, reason codes, correlation ID, trace ID, policy version, and policy hash from the provided <see cref="GovernanceDecision"/> when creating an decision receipt, ensuring that all relevant information from the decision is captured in the audit record.
    /// </summary>
    [Fact]
    public void FromDecisionCopiesDecisionOutcomeAndTraceData()
    {
        var actor = GovernanceActorContext.Human("user-123", "Chris");
        var decision = GovernanceDecision.Deny(
            "policy.denied",
            "Policy denied the operation.",
            correlationId: "correlation-123",
            traceId: "trace-456",
            policyVersion: "v1",
            policyHash: "hash-abc");

        var residue = DecisionReceipt.FromDecision(
            actor,
            "document.approve",
            decision,
            eventId: "event-123",
            occurredUtc: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("event-123", residue.EventId);
        Assert.Equal("Denied", residue.Outcome);
        Assert.Equal("policy.denied", Assert.Single(residue.ReasonCodes));
        Assert.Equal("correlation-123", residue.CorrelationId);
        Assert.Equal("trace-456", residue.TraceId);
        Assert.Equal("v1", residue.PolicyVersion);
        Assert.Equal("hash-abc", residue.PolicyHash);
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.FromDecision"/> method throws when the decision is missing.
    /// </summary>
    [Fact]
    public void FromDecisionThrowsForMissingDecision()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        _ = Assert.Throws<ArgumentNullException>(() =>
            DecisionReceipt.FromDecision(
                actor,
                "document.approve",
                decision: null!));
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.FromConstraint"/> method correctly copies the outcome, reason codes, correlation ID, and policy version from the provided <see cref="ConstraintEvaluationResult"/> when creating an decision receipt, ensuring that all relevant information from the constraint evaluation is captured in the audit record.
    /// </summary>
    [Fact]
    public void FromConstraintCopiesConstraintOutcomeAndReasonCodes()
    {
        var actor = GovernanceActorContext.Service("service-123");
        var constraintResult = ConstraintEvaluationResult.Warning(
            "constraint.high_risk",
            "Constraint produced a high-risk warning.");

        var residue = DecisionReceipt.FromConstraint(
            actor,
            "external.call",
            constraintResult,
            eventId: "event-123",
            occurredUtc: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
            correlationId: "correlation-123",
            policyVersion: "v1");

        Assert.Equal("event-123", residue.EventId);
        Assert.Equal("Warning", residue.Outcome);
        Assert.Equal("constraint.high_risk", Assert.Single(residue.ReasonCodes));
        Assert.Equal("correlation-123", residue.CorrelationId);
        Assert.Equal("v1", residue.PolicyVersion);
    }

    /// <summary>
    /// Verifies that the <see cref="DecisionReceipt.FromConstraint"/> method throws when the constraint result is missing.
    /// </summary>
    [Fact]
    public void FromConstraintThrowsForMissingConstraintResult()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;

        _ = Assert.Throws<ArgumentNullException>(() =>
            DecisionReceipt.FromConstraint(
                actor,
                "external.call",
                constraintResult: null!));
    }

    /// <summary>
    /// Verifies that the default timestamp is generated in UTC and falls within the creation window.
    /// </summary>
    [Fact]
    public void CreateWithoutTimestampUsesCurrentUtcTimestamp()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        DateTimeOffset beforeCreate = DateTimeOffset.UtcNow;

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed");

        DateTimeOffset afterCreate = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, residue.OccurredUtc.Offset);
        Assert.InRange(residue.OccurredUtc, beforeCreate, afterCreate);
    }

    /// <summary>
    /// Verifies that mutating source reason codes and metadata after creation does not change the decision receipt.
    /// </summary>
    [Fact]
    public void CreateDoesNotAliasSourceCollections()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        List<string> reasonCodes = [" policy.warning "];
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [" source "] = " original "
        };

        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Warning",
            reasonCodes: reasonCodes,
            metadata: metadata);

        reasonCodes.Add("policy.added");
        metadata[" source "] = " mutated ";
        metadata[" other "] = " added ";

        Assert.Equal("policy.warning", Assert.Single(residue.ReasonCodes));
        _ = Assert.Single(residue.Metadata);
        Assert.Equal("original", residue.Metadata["source"]);
        Assert.False(residue.Metadata.ContainsKey("other"));
    }

    /// <summary>
    /// Verifies that metadata cannot be mutated through dictionary casts.
    /// </summary>
    [Fact]
    public void MetadataCannotBeMutatedThroughDictionaryCasts()
    {
        var residue = DecisionReceipt.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed",
            metadata: new Dictionary<string, string>
            {
                [" source "] = " unit-test "
            });

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(residue.Metadata);

        _ = Assert.Single(residue.Metadata);
        Assert.Equal("unit-test", residue.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that empty metadata cannot be mutated through dictionary casts.
    /// </summary>
    [Fact]
    public void EmptyMetadataCannotBeMutatedThroughDictionaryCasts()
    {
        var residue = DecisionReceipt.Create(
            GovernanceActorContext.System,
            "system.sync",
            "Allowed");

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(residue.Metadata);

        Assert.False(residue.HasMetadata);
        Assert.Empty(residue.Metadata);
    }
}
