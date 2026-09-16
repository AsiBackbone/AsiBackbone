using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Results;
using Xunit;

namespace AsiBackbone.Testing.Tests;

/// <summary>
/// Focused coverage for deterministic telemetry projection in the test-harness decision factory.
/// </summary>
public sealed class AsiBackboneTestHarnessDecisionFactoryTests
{
    /// <summary>
    /// Verifies every supported decision outcome is reconstructed without changing its classification.
    /// </summary>
    [Theory]
    [InlineData(GovernanceDecisionOutcome.Allowed)]
    [InlineData(GovernanceDecisionOutcome.Warning)]
    [InlineData(GovernanceDecisionOutcome.Denied)]
    [InlineData(GovernanceDecisionOutcome.Deferred)]
    [InlineData(GovernanceDecisionOutcome.AcknowledgmentRequired)]
    [InlineData(GovernanceDecisionOutcome.EscalationRecommended)]
    public void WithTelemetryPreservesEverySupportedOutcome(GovernanceDecisionOutcome outcome)
    {
        GovernanceDecision source = CreateDecision(outcome);

        GovernanceDecision projected = AsiBackboneTestHarnessDecisionFactory.WithTelemetry(
            source,
            "correlation-from-context",
            "trace-from-context",
            "policy-v2",
            "hash-v2");

        Assert.Equal(outcome, projected.Outcome);
        Assert.Equal("correlation-from-context", projected.CorrelationId);
        Assert.Equal("trace-from-context", projected.TraceId);
        Assert.Equal("policy-v2", projected.PolicyVersion);
        Assert.Equal("hash-v2", projected.PolicyHash);

        if (source.Reasons.Count == 0)
        {
            Assert.Empty(projected.Reasons);
        }
        else
        {
            Assert.Equal(source.Reasons[0].Code, projected.Reasons[0].Code);
            Assert.Equal(source.Reasons[0].Message, projected.Reasons[0].Message);
        }
    }

    /// <summary>
    /// Verifies telemetry already attached to a decision takes precedence over supplied fallback values.
    /// </summary>
    [Fact]
    public void WithTelemetryPreservesDecisionOwnedTelemetry()
    {
        var source = GovernanceDecision.Deny(
            "test.denied",
            "Denied by the test harness.",
            correlationId: "decision-correlation",
            traceId: "decision-trace",
            policyVersion: "decision-policy",
            policyHash: "decision-hash");

        GovernanceDecision projected = AsiBackboneTestHarnessDecisionFactory.WithTelemetry(
            source,
            "fallback-correlation",
            "fallback-trace",
            "fallback-policy",
            "fallback-hash");

        Assert.Equal("decision-correlation", projected.CorrelationId);
        Assert.Equal("decision-trace", projected.TraceId);
        Assert.Equal("decision-policy", projected.PolicyVersion);
        Assert.Equal("decision-hash", projected.PolicyHash);
        Assert.Equal("test.denied", Assert.Single(projected.Reasons).Code);
    }

    /// <summary>
    /// Verifies the context overload uses correlation and policy metadata while leaving trace fallback unset.
    /// </summary>
    [Fact]
    public void ContextOverloadProjectsContextTelemetry()
    {
        var context = new GovernanceEvaluationContext(
            correlationId: "context-correlation",
            policyVersion: "context-policy",
            policyHash: "context-hash");

        GovernanceDecision projected = AsiBackboneTestHarnessDecisionFactory.WithTelemetry(
            GovernanceDecision.Allow(),
            context);

        Assert.True(projected.IsAllowed);
        Assert.Equal("context-correlation", projected.CorrelationId);
        Assert.Null(projected.TraceId);
        Assert.Equal("context-policy", projected.PolicyVersion);
        Assert.Equal("context-hash", projected.PolicyHash);
    }

    /// <summary>
    /// Verifies deferred, acknowledgment, and escalation decisions use stable fallback reasons
    /// when an internally constructed decision contains no reasons.
    /// </summary>
    [Theory]
    [InlineData(GovernanceDecisionOutcome.Deferred, "test_harness.deferred", "Deferred by the AsiBackbone test harness.")]
    [InlineData(GovernanceDecisionOutcome.AcknowledgmentRequired, "test_harness.acknowledgment_required", "Acknowledgment required by the AsiBackbone test harness.")]
    [InlineData(GovernanceDecisionOutcome.EscalationRecommended, "test_harness.escalation_recommended", "Escalation recommended by the AsiBackbone test harness.")]
    public void WithTelemetryUsesStableFallbackReasons(
        GovernanceDecisionOutcome outcome,
        string expectedCode,
        string expectedMessage)
    {
        GovernanceDecision source = CreateDecisionWithNoReasons(outcome);

        GovernanceDecision projected = AsiBackboneTestHarnessDecisionFactory.WithTelemetry(
            source,
            correlationId: null,
            traceId: null,
            policyVersion: null,
            policyHash: null);

        OperationReason reason = Assert.Single(projected.Reasons);
        Assert.Equal(expectedCode, reason.Code);
        Assert.Equal(expectedMessage, reason.Message);
    }

    /// <summary>
    /// Verifies unknown outcome values preserve the original decision through the defensive fallback branch.
    /// </summary>
    [Fact]
    public void WithTelemetryReturnsOriginalDecisionForUnknownOutcome()
    {
        GovernanceDecision source = CreateDecisionWithNoReasons((GovernanceDecisionOutcome)int.MaxValue);

        GovernanceDecision projected = AsiBackboneTestHarnessDecisionFactory.WithTelemetry(
            source,
            "fallback-correlation",
            "fallback-trace",
            "fallback-policy",
            "fallback-hash");

        Assert.Same(source, projected);
    }

    /// <summary>
    /// Verifies null decisions and contexts are rejected before projection.
    /// </summary>
    [Fact]
    public void WithTelemetryRejectsNullArguments()
    {
        _ = Assert.Throws<ArgumentNullException>(() => AsiBackboneTestHarnessDecisionFactory.WithTelemetry(
            null!,
            correlationId: null,
            traceId: null,
            policyVersion: null,
            policyHash: null));
        _ = Assert.Throws<ArgumentNullException>(() => AsiBackboneTestHarnessDecisionFactory.WithTelemetry(
            GovernanceDecision.Allow(),
            null!));
    }

    private static GovernanceDecision CreateDecision(GovernanceDecisionOutcome outcome)
    {
        return outcome switch
        {
            GovernanceDecisionOutcome.Allowed => GovernanceDecision.Allow(),
            GovernanceDecisionOutcome.Warning => GovernanceDecision.Warning("test.warning", "Warning from test harness."),
            GovernanceDecisionOutcome.Denied => GovernanceDecision.Deny("test.denied", "Denied by test harness."),
            GovernanceDecisionOutcome.Deferred => GovernanceDecision.Defer("test.deferred", "Deferred by test harness."),
            GovernanceDecisionOutcome.AcknowledgmentRequired => GovernanceDecision.RequireAcknowledgment(
                "test.acknowledgment",
                "Acknowledgment required by test harness."),
            GovernanceDecisionOutcome.EscalationRecommended => GovernanceDecision.Escalate(
                "test.escalation",
                "Escalation recommended by test harness."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Outcome must be supported by the test harness.")
        };
    }

    private static GovernanceDecision CreateDecisionWithNoReasons(GovernanceDecisionOutcome outcome)
    {
        ConstructorInfo constructor = typeof(GovernanceDecision).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(GovernanceDecisionOutcome),
                typeof(IReadOnlyList<OperationReason>),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string)
            ],
            modifiers: null)
            ?? throw new InvalidOperationException("The governance decision constructor could not be located.");

        return (GovernanceDecision)constructor.Invoke(
        [
            outcome,
            Array.Empty<OperationReason>(),
            null,
            null,
            null,
            null
        ]);
    }
}
