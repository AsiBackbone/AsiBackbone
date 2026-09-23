using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Correlation;

/// <summary>
/// Unit tests for the <see cref="GovernanceHttpRequestCorrelationDecisionReceiptExtensions"/> class.
/// </summary>
public sealed class GovernanceHttpRequestCorrelationDecisionReceiptExtensionsTests
{
    /// <summary>
    /// Tests that the <c>GovernanceHttpRequestCorrelation.CreateDecisionReceipt"</c> method uses the request correlation ID and trace ID before falling back to the decision correlation ID and trace ID.
    /// </summary>
    [Fact]
    public void CreateDecisionReceiptUsesRequestCorrelationBeforeDecisionCorrelation()
    {
        GovernanceHttpRequestCorrelation correlation = new(
            correlationId: "request-correlation",
            traceId: "request-trace");
        var decision = GovernanceDecision.Allow(
            correlationId: "decision-correlation",
            traceId: "decision-trace",
            policyVersion: "v1",
            policyHash: "hash-1");

        DecisionReceipt residue = correlation.CreateDecisionReceipt(
            GovernanceActorContext.System,
            "operate",
            decision);

        Assert.Equal("request-correlation", residue.CorrelationId);
        Assert.Equal("request-trace", residue.TraceId);
        Assert.Equal("v1", residue.PolicyVersion);
        Assert.Equal("hash-1", residue.PolicyHash);
    }

    /// <summary>
    /// Tests that the <c>GovernanceHttpRequestCorrelation.CreateDecisionReceipt</c> method falls back to the decision correlation ID and trace ID when the request correlation ID and trace ID are missing.
    /// </summary>
    [Fact]
    public void CreateDecisionReceiptFallsBackToDecisionCorrelationWhenRequestCorrelationIsMissing()
    {
        GovernanceHttpRequestCorrelation correlation = new();
        var decision = GovernanceDecision.Allow(
            correlationId: "decision-correlation",
            traceId: "decision-trace");

        DecisionReceipt residue = correlation.CreateDecisionReceipt(
            GovernanceActorContext.System,
            "operate",
            decision);

        Assert.Equal("decision-correlation", residue.CorrelationId);
        Assert.Equal("decision-trace", residue.TraceId);
    }

    /// <summary>
    /// Tests that the <c>GovernanceHttpRequestCorrelation.CreateDecisionReceipt</c> method merges safe request metadata with host metadata.
    /// </summary>
    [Fact]
    public void CreateDecisionReceiptMergesSafeRequestMetadataWithHostMetadata()
    {
        GovernanceHttpRequestCorrelation correlation = new(
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GovernanceHttpRequestMetadataKeys.Method] = "POST",
                [GovernanceHttpRequestMetadataKeys.RoutePattern] = "/api/widgets/{id}",
            });
        var decision = GovernanceDecision.Allow();
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["operation.scope"] = "test",
        };

        DecisionReceipt residue = correlation.CreateDecisionReceipt(
            GovernanceActorContext.System,
            "operate",
            decision,
            metadata: metadata);

        Assert.Equal("POST", residue.Metadata[GovernanceHttpRequestMetadataKeys.Method]);
        Assert.Equal("/api/widgets/{id}", residue.Metadata[GovernanceHttpRequestMetadataKeys.RoutePattern]);
        Assert.Equal("test", residue.Metadata["operation.scope"]);
    }

    /// <summary>
    /// Verifies that correlation enrichment preserves the stable signed wire name for every decision outcome.
    /// </summary>
    [Theory]
    [InlineData(GovernanceDecisionOutcome.Allowed, "Allowed")]
    [InlineData(GovernanceDecisionOutcome.Warning, "Warning")]
    [InlineData(GovernanceDecisionOutcome.Denied, "Denied")]
    [InlineData(GovernanceDecisionOutcome.Deferred, "Deferred")]
    [InlineData(GovernanceDecisionOutcome.AcknowledgmentRequired, "AcknowledgmentRequired")]
    [InlineData(GovernanceDecisionOutcome.EscalationRecommended, "EscalationRecommended")]
    public void CreateDecisionReceiptUsesStableOutcomeWireNames(
        GovernanceDecisionOutcome value,
        string expectedWireName)
    {
        GovernanceHttpRequestCorrelation correlation = new(correlationId: "request-correlation");
        GovernanceDecision decision = CreateDecision(value);

        DecisionReceipt residue = correlation.CreateDecisionReceipt(
            GovernanceActorContext.System,
            "operate",
            decision);

        Assert.Equal(expectedWireName, residue.Outcome);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceHttpRequestCorrelation.ToEvaluationContext"/> method propagates the correlation ID, policy version, policy hash, and safe request metadata to the evaluation context.
    /// </summary>
    [Fact]
    public void ToEvaluationContextPropagatesCorrelationAndSafeMetadata()
    {
        GovernanceHttpRequestCorrelation correlation = new(
            correlationId: "request-correlation",
            traceId: "request-trace",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GovernanceHttpRequestMetadataKeys.Method] = "GET",
            });

        GovernanceEvaluationContext context = correlation.ToEvaluationContext(
            policyVersion: "v2",
            policyHash: "hash-2",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["operation.scope"] = "policy",
            });

        Assert.Equal("request-correlation", context.CorrelationId);
        Assert.Equal("v2", context.PolicyVersion);
        Assert.Equal("hash-2", context.PolicyHash);
        Assert.Equal("GET", context.Metadata[GovernanceHttpRequestMetadataKeys.Method]);
        Assert.Equal("policy", context.Metadata["operation.scope"]);
    }

    private static GovernanceDecision CreateDecision(GovernanceDecisionOutcome value)
    {
        return value switch
        {
            GovernanceDecisionOutcome.Allowed => GovernanceDecision.Allow(),
            GovernanceDecisionOutcome.Warning => GovernanceDecision.Warning("wire.warning", "Wire-name fixture."),
            GovernanceDecisionOutcome.Denied => GovernanceDecision.Deny("wire.denied", "Wire-name fixture."),
            GovernanceDecisionOutcome.Deferred => GovernanceDecision.Defer("wire.deferred", "Wire-name fixture."),
            GovernanceDecisionOutcome.AcknowledgmentRequired => GovernanceDecision.RequireAcknowledgment(
                "wire.acknowledgment-required",
                "Wire-name fixture."),
            GovernanceDecisionOutcome.EscalationRecommended => GovernanceDecision.Escalate(
                "wire.escalation-recommended",
                "Wire-name fixture."),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Test decision outcome must be defined.")
        };
    }
}
