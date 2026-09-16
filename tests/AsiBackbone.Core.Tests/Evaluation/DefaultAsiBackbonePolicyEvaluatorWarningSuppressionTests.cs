using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Focused coverage for warning-only reason handling when denied decisions are composed.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorWarningSuppressionTests
{
    /// <summary>
    /// Verifies that full evaluation continues after denial, aggregates denial reasons, and clears warning-only reasons from the final denied decision.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task FullEvaluationDeniedDecisionClearsWarningOnlyReasonsAndAggregatesDenials()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    "pre-warning-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("pre-warning");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.warning.before_denial",
                            "The first constraint produced a warning before any denial.");
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
                    "post-denial-warning-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("post-denial-warning");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.warning.after_denial",
                            "The later warning should not be included in the denied decision.");
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

        Assert.Equal(["pre-warning", "first-denial", "post-denial-warning", "second-denial"], observedOrder);
        Assert.True(decision.IsDenied);
        Assert.Equal(["constraint.denied.one", "constraint.denied.two"], decision.ReasonCodes);
        Assert.DoesNotContain("constraint.warning.before_denial", decision.ReasonCodes);
        Assert.DoesNotContain("constraint.warning.after_denial", decision.ReasonCodes);
        Assert.DoesNotContain("threat.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that first-denial fast-abort preserves warnings already produced because later constraints are intentionally skipped.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ShortCircuitDeniedDecisionPreservesEvaluatedWarningsBecauseLaterConstraintsAreSkipped()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    "pre-warning-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("pre-warning");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.warning.before_denial",
                            "The first constraint produced a warning before any denial.");
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
                    "skipped-warning-constraint",
                    (_, _) =>
                    {
                        observedOrder.Add("skipped-warning");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.warning.skipped",
                            "This warning should not be evaluated in fast-abort mode.");
                    })
            ],
            [CreateWarningContributor()],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                ShortCircuitOnFirstDenial = true
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(["pre-warning", "first-denial"], observedOrder);
        Assert.True(decision.IsDenied);
        Assert.Contains("threat.warning", decision.ReasonCodes);
        Assert.Contains("constraint.warning.before_denial", decision.ReasonCodes);
        Assert.Contains("constraint.denied.one", decision.ReasonCodes);
        Assert.DoesNotContain("constraint.warning.skipped", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that actionable threat warnings remain protected from allow-downgrade when no denial is composed.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ThreatWarningRemainsProtectedFromAllowDowngradeWhenEvaluationCanProceed()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [CreateWarningContributor()],
            new AlwaysAllowDecisionPolicy(), options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Equal("threat.warning", Assert.Single(decision.ReasonCodes));
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-warning-suppression-123",
            PolicyVersion = "v-warning-suppression",
            PolicyHash = "hash-warning-suppression"
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

    private sealed class TestPolicyContext : IAsiBackboneConstraintEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class StaticConstraint(ConstraintEvaluationResult result) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly ConstraintEvaluationResult result = result;

        public string Name => "static-warning-suppression-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DelegateConstraint(
        string name,
        Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate) : IAsiBackboneConstraint<TestPolicyContext>
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

    private sealed class AlwaysAllowDecisionPolicy : IAsiBackboneDecisionPolicy<TestPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            TestPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceDecision.Allow(
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }
}
