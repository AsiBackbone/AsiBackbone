using AsiBackbone.Core.Decisions;
using Xunit;

namespace AsiBackbone.Core.Tests.Decisions;

/// <summary>
/// Unit tests for GovernanceDecision telemetry identifier normalization boundaries.
/// </summary>
public sealed class GovernanceDecisionTelemetryIdentifierTests
{
    /// <summary>
    /// Verifies that null and blank telemetry identifiers remain normalized to null.
    /// </summary>
    [Fact]
    public void AllowConvertsNullAndBlankTelemetryIdentifiersToNull()
    {
        var nullDecision = GovernanceDecision.Allow(
            correlationId: null,
            traceId: null);

        var blankDecision = GovernanceDecision.Allow(
            correlationId: "   ",
            traceId: "\t");

        Assert.Null(nullDecision.CorrelationId);
        Assert.Null(nullDecision.TraceId);
        Assert.Null(blankDecision.CorrelationId);
        Assert.Null(blankDecision.TraceId);
    }

    /// <summary>
    /// Verifies that normal telemetry identifiers are trimmed without changing their values.
    /// </summary>
    [Fact]
    public void AllowTrimsAndPreservesNormalTelemetryIdentifiers()
    {
        var decision = GovernanceDecision.Allow(
            correlationId: " correlation-123 ",
            traceId: " trace-456 ");

        Assert.Equal("correlation-123", decision.CorrelationId);
        Assert.Equal("trace-456", decision.TraceId);
    }

    /// <summary>
    /// Verifies that overlong telemetry identifiers are truncated after trimming so external values cannot flow into decisions unbounded.
    /// </summary>
    [Fact]
    public void AllowTruncatesOverlongTelemetryIdentifiersAfterTrimming()
    {
        string overlongCorrelationId = $" {new string('c', GovernanceDecision.MaxCorrelationIdLength + 20)} ";
        string overlongTraceId = $" {new string('t', GovernanceDecision.MaxTraceIdLength + 20)} ";

        var decision = GovernanceDecision.Allow(
            correlationId: overlongCorrelationId,
            traceId: overlongTraceId);

        string expectedCorrelationId = new('c', GovernanceDecision.MaxCorrelationIdLength);
        string expectedTraceId = new('t', GovernanceDecision.MaxTraceIdLength);

        Assert.Equal(GovernanceDecision.MaxCorrelationIdLength, decision.CorrelationId!.Length);
        Assert.Equal(GovernanceDecision.MaxTraceIdLength, decision.TraceId!.Length);
        Assert.Equal(expectedCorrelationId, decision.CorrelationId);
        Assert.Equal(expectedTraceId, decision.TraceId);
    }

    /// <summary>
    /// Verifies replacing a trace identifier preserves all decision semantics and normalizes the new identifier.
    /// </summary>
    [Fact]
    public void WithTraceIdPreservesDecisionAndNormalizesTraceIdentifier()
    {
        var decision = GovernanceDecision.RequireAcknowledgment(
            "acknowledgment.required",
            "Approval is required.",
            correlationId: "correlation-123",
            traceId: "original-trace",
            policyVersion: "v1",
            policyHash: "hash-123");

        GovernanceDecision tracedDecision = decision.WithTraceId(" replacement-trace ");

        Assert.NotSame(decision, tracedDecision);
        Assert.Equal(decision.Outcome, tracedDecision.Outcome);
        Assert.Equal(decision.Reasons, tracedDecision.Reasons);
        Assert.Equal(decision.ReasonCodes, tracedDecision.ReasonCodes);
        Assert.Equal(decision.CorrelationId, tracedDecision.CorrelationId);
        Assert.Equal("replacement-trace", tracedDecision.TraceId);
        Assert.Equal(decision.PolicyVersion, tracedDecision.PolicyVersion);
        Assert.Equal(decision.PolicyHash, tracedDecision.PolicyHash);
    }
}
