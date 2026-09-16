using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Verifies fail-closed handling for undefined threat assessment enum values.
/// </summary>
public sealed class ThreatAssessmentUndefinedEnumTests
{
    /// <summary>
    /// Verifies that an undefined threat severity is rejected during assessment construction.
    /// </summary>
    [Fact]
    public void ThreatAssessmentRejectsUndefinedSeverity()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => ThreatAssessment.Create(
            (ThreatSeverity)999,
            ThreatCategories.AuditIntegrityRisk,
            "threat.undefined_severity",
            "Undefined threat severity should be rejected.",
            GovernanceDecisionOutcome.Denied));

        Assert.Equal("severity", exception.ParamName);
    }

    /// <summary>
    /// Verifies that an undefined recommended outcome is rejected during assessment construction.
    /// </summary>
    [Fact]
    public void ThreatAssessmentRejectsUndefinedRecommendedOutcome()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => ThreatAssessment.Create(
            ThreatSeverity.Medium,
            ThreatCategories.AuditIntegrityRisk,
            "threat.undefined_outcome",
            "Undefined threat outcome should be rejected.",
            (GovernanceDecisionOutcome)999));

        Assert.Equal("recommendedOutcome", exception.ParamName);
    }

    /// <summary>
    /// Verifies that malformed contributor output follows the controlled exception-as-denial path and prevents normal constraint execution.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateUndefinedOutcomeContributorFailsClosedAndSkipsConstraints()
    {
        var context = new TestPolicyContext
        {
            CorrelationId = "corr-undefined-threat-enum",
            PolicyVersion = "v-undefined-threat-enum",
            PolicyHash = "hash-undefined-threat-enum"
        };
        bool constraintRan = false;
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new DelegateConstraint(
                (_, _) =>
                {
                    constraintRan = true;
                    return ConstraintEvaluationResult.Allow();
                })],
            [new DelegateThreatContributor(
                "undefined-outcome-threat-contributor",
                (_, _) => ThreatAssessment.Create(
                    ThreatSeverity.Medium,
                    ThreatCategories.AuditIntegrityRisk,
                    "threat.undefined_outcome",
                    "Undefined threat outcome should fail closed.",
                    (GovernanceDecisionOutcome)999))], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.IsAllowed);
        Assert.False(decision.CanProceed);
        Assert.False(constraintRan);
        Assert.Equal(
            GovernancePolicyOptions.DefaultThreatContributorExceptionReasonCode,
            Assert.Single(decision.ReasonCodes));

        OperationReason reason = Assert.Single(decision.Reasons);
        Assert.Equal("undefined-outcome-threat-contributor", reason.Metadata["threat.contributor"]);
        Assert.Equal(nameof(ArgumentOutOfRangeException), reason.Metadata["threat.failure"]);
    }

    private sealed class TestPolicyContext : IGovernanceEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class DelegateConstraint(
        Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate) :
        IGovernanceConstraint<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate = evaluate;

        public string Name => "undefined-threat-enum-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(evaluate(context, cancellationToken));
        }
    }

    private sealed class DelegateThreatContributor(
        string name,
        Func<TestPolicyContext, CancellationToken, ThreatAssessment> assess) :
        IThreatModelContributor<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ThreatAssessment> assess = assess;

        public string Name { get; } = name;

        public ValueTask<ThreatAssessment> AssessAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(assess(context, cancellationToken));
        }
    }
}
