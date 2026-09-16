using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Decisions;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Focused tests for allocation-sensitive audit residue decision-record creation.
/// </summary>
public sealed class AuditResidueHotPathTests
{
    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.FromDecision"/> method correctly copies the decision outcome, trace, and reason codes into the resulting audit residue.
    /// </summary>
    /// <param name="scenario">The scenario under test.</param>
    /// <param name="expectedOutcome">The expected outcome of the audit residue.</param>
    /// <param name="expectedReasonCode">The expected reason code for the audit residue.</param>
    [Theory]
    [InlineData("allow", "Allowed", null)]
    [InlineData("warning", "Warning", "policy.warning")]
    [InlineData("deny", "Denied", "policy.denied")]
    [InlineData("defer", "Deferred", "policy.deferred")]
    [InlineData("acknowledgment", "AcknowledgmentRequired", "policy.acknowledgment_required")]
    [InlineData("escalation", "EscalationRecommended", "policy.escalation_recommended")]
    public void FromDecisionCopiesDecisionOutcomeTraceAndReasonCodes(
        string scenario,
        string expectedOutcome,
        string? expectedReasonCode)
    {
        var actor = GovernanceActorContext.Service("benchmark-service");
        GovernanceDecision decision = CreateDecision(scenario);

        var residue = DecisionReceipt.FromDecision(
            actor,
            "benchmark.operation",
            decision,
            eventId: "benchmark-event",
            occurredUtc: new DateTimeOffset(2026, 6, 30, 18, 0, 0, TimeSpan.Zero));

        Assert.Equal("benchmark-event", residue.EventId);
        Assert.Equal(expectedOutcome, residue.Outcome);
        Assert.Equal(CreateExpectedReasonCodes(expectedReasonCode), residue.ReasonCodes);
        Assert.Equal(decision.CorrelationId, residue.CorrelationId);
        Assert.Equal(decision.TraceId, residue.TraceId);
        Assert.Equal(decision.PolicyVersion, residue.PolicyVersion);
        Assert.Equal(decision.PolicyHash, residue.PolicyHash);
    }

    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.FromDecision"/> method reuses the immutable reason codes from the decision to ensure audit fidelity and avoid unnecessary allocations.
    /// </summary>
    [Fact]
    public void FromDecisionReusesImmutableDecisionReasonCodesForAuditFidelity()
    {
        var decision = GovernanceDecision.Deny(
            "policy.denied",
            "Policy denied the benchmark operation.",
            correlationId: "benchmark-correlation",
            traceId: "benchmark-trace",
            policyVersion: "benchmark-policy-v1",
            policyHash: "benchmark-policy-hash");

        var residue = DecisionReceipt.FromDecision(
            GovernanceActorContext.Service("benchmark-service"),
            "benchmark.operation",
            decision,
            eventId: "benchmark-event");

        Assert.Same(decision.ReasonCodes, residue.ReasonCodes);
        Assert.Equal("policy.denied", Assert.Single(residue.ReasonCodes));
    }

    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.FromDecision"/> method defensively copies the metadata to ensure audit fidelity and avoid unnecessary allocations.
    /// </summary>
    [Fact]
    public void FromDecisionStillDefensivelyCopiesMetadata()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" source "] = " original ",
            [" "] = "ignored"
        };
        var decision = GovernanceDecision.Warning(
            "policy.warning",
            "Policy produced a warning.");

        var residue = DecisionReceipt.FromDecision(
            GovernanceActorContext.Service("benchmark-service"),
            "benchmark.operation",
            decision,
            metadata: metadata,
            eventId: "benchmark-event");

        metadata[" source "] = " mutated ";
        metadata[" added "] = " later ";

        _ = Assert.Single(residue.Metadata);
        Assert.Equal("original", residue.Metadata["source"]);
        Assert.False(residue.Metadata.ContainsKey("added"));
        ReadOnlyMetadataAssert.CannotMutateThroughCasts(residue.Metadata);
    }

    private static GovernanceDecision CreateDecision(string scenario)
    {
        return scenario switch
        {
            "allow" => GovernanceDecision.Allow(
                correlationId: "corr-allow",
                traceId: "trace-allow",
                policyVersion: "policy-v1",
                policyHash: "policy-hash"),
            "warning" => GovernanceDecision.Warning(
                "policy.warning",
                "Policy produced a warning.",
                correlationId: "corr-warning",
                traceId: "trace-warning",
                policyVersion: "policy-v1",
                policyHash: "policy-hash"),
            "deny" => GovernanceDecision.Deny(
                "policy.denied",
                "Policy denied the operation.",
                correlationId: "corr-denied",
                traceId: "trace-denied",
                policyVersion: "policy-v1",
                policyHash: "policy-hash"),
            "defer" => GovernanceDecision.Defer(
                "policy.deferred",
                "Policy deferred the operation.",
                correlationId: "corr-deferred",
                traceId: "trace-deferred",
                policyVersion: "policy-v1",
                policyHash: "policy-hash"),
            "acknowledgment" => GovernanceDecision.RequireAcknowledgment(
                "policy.acknowledgment_required",
                "Acknowledgment is required.",
                correlationId: "corr-ack",
                traceId: "trace-ack",
                policyVersion: "policy-v1",
                policyHash: "policy-hash"),
            "escalation" => GovernanceDecision.Escalate(
                "policy.escalation_recommended",
                "Escalation is recommended.",
                correlationId: "corr-escalate",
                traceId: "trace-escalate",
                policyVersion: "policy-v1",
                policyHash: "policy-hash"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported decision scenario.")
        };
    }

    private static string[] CreateExpectedReasonCodes(string? expectedReasonCode)
    {
        return expectedReasonCode is null ? [] : [expectedReasonCode];
    }
}
