using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Focused coverage for audit-warning visibility when evaluation also produces denied results.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorAuditMessageBehaviorTests
{
    /// <summary>
    /// This test ensures that when a full evaluation is performed, and multiple constraints produce denials, the audit warnings are suppressed in the final decision, and all denials are aggregated into the decision's reason codes.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task FullEvaluationDeniedDecisionSuppressesAuditWarningsAndAggregatesDenials()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();

        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    "pre-warning-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("pre-warning");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.pre_warning",
                            "The first constraint produced an audit warning.");
                    }),
                new DelegateConstraint(
                    "first-denial-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("first-denial");
                        return ConstraintEvaluationResult.Deny(
                            "constraint.denied.one",
                            "The first denial blocked the operation.");
                    }),
                new DelegateConstraint(
                    "second-denial-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("second-denial");
                        return ConstraintEvaluationResult.Deny(
                            "constraint.denied.two",
                            "The second denial also blocked the operation.");
                    })
            ],
            [CreateWarningContributor()], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(["pre-warning", "first-denial", "second-denial"], observedOrder);
        Assert.True(decision.IsDenied);
        Assert.Contains("constraint.denied.one", decision.ReasonCodes);
        Assert.Contains("constraint.denied.two", decision.ReasonCodes);
        Assert.DoesNotContain("constraint.pre_warning", decision.ReasonCodes);
        Assert.DoesNotContain("threat.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// This test ensures that when short-circuit evaluation is enabled, and a denial is encountered after an audit warning, the evaluation stops at the first denial, preserving the audit warning in the final decision, and skipping any subsequent constraints.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ShortCircuitDeniedDecisionPreservesEvaluatedAuditWarningsAndSkipsLaterConstraints()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();

        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    "pre-warning-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("pre-warning");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.pre_warning",
                            "The first constraint produced an audit warning.");
                    }),
                new DelegateConstraint(
                    "denial-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("denial");
                        return ConstraintEvaluationResult.Deny(
                            "constraint.denied",
                            "The first denial blocked the operation.");
                    }),
                new DelegateConstraint(
                    "skipped-denial-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("skipped-denial");
                        return ConstraintEvaluationResult.Deny(
                            "constraint.skipped_denied",
                            "This denial should not be evaluated in fast-abort mode.");
                    })
            ],
            [CreateWarningContributor()],
            decisionPolicy: null,
            options: new GovernancePolicyOptions
            {
                ShortCircuitOnFirstDenial = true
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(["pre-warning", "denial"], observedOrder);
        Assert.True(decision.IsDenied);
        Assert.Contains("threat.warning", decision.ReasonCodes);
        Assert.Contains("constraint.pre_warning", decision.ReasonCodes);
        Assert.Contains("constraint.denied", decision.ReasonCodes);
        Assert.DoesNotContain("constraint.skipped_denied", decision.ReasonCodes);
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-audit-message-123",
            PolicyVersion = "v-audit-message",
            PolicyHash = "hash-audit-message"
        };
    }

    private static StaticThreatContributor CreateWarningContributor()
    {
        return new StaticThreatContributor(
            "warning-threat-contributor",
            ThreatAssessment.Create(
                ThreatSeverity.Low,
                ThreatCategories.InputMalformed,
                "threat.warning",
                "Warning threat indicator was reported.",
                GovernanceDecisionOutcome.Warning));
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
        string name,
        Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate) : IGovernanceConstraint<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate = evaluate;

        public string Name { get; } = name;

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(evaluate(context, cancellationToken));
        }
    }

    private sealed class StaticThreatContributor(
        string name,
        ThreatAssessment assessment) : IThreatModelContributor<TestPolicyContext>
    {
        private readonly ThreatAssessment assessment = assessment;

        public string Name { get; } = name;

        public ValueTask<ThreatAssessment> AssessAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(assessment);
        }
    }
}
