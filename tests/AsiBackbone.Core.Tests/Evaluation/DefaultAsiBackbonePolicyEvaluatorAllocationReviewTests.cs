using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Regression coverage for allocation-review-sensitive policy evaluator semantics.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorAllocationReviewTests
{
    /// <summary>
    /// Verifies that decision-policy result visibility is preserved when first-denial short-circuiting evaluates only part of the constraint set.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task DecisionPolicyReceivesOnlyEvaluatedConstraintResultsWhenShortCircuiting()
    {
        TestPolicyContext context = CreateContext();
        int skippedEvaluationCount = 0;
        var policy = new CapturingDecisionPolicy();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.warning",
                        "The first constraint produced a warning.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.denied",
                        "The second constraint denied the operation.")),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        skippedEvaluationCount++;
                        return ConstraintEvaluationResult.Warning(
                            "constraint.skipped",
                            "This warning should not be evaluated.");
                    })
            ],
            decisionPolicy: policy,
            options: new GovernancePolicyOptions
            {
                ShortCircuitOnFirstDenial = true
            }, threatModelContributors: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(0, skippedEvaluationCount);
        Assert.True(decision.IsDenied);
        Assert.Equal(1, policy.ApplyCount);
        Assert.NotNull(policy.ComposedDecision);
        Assert.True(policy.ComposedDecision.IsDenied);
        Assert.Equal(
            ["constraint.warning", "constraint.denied"],
            policy.ComposedDecision.ReasonCodes);

        IReadOnlyList<ConstraintEvaluationResult> constraintResults = Assert.IsAssignableFrom<IReadOnlyList<ConstraintEvaluationResult>>(policy.ConstraintResults);
        Assert.Equal(2, constraintResults.Count);
        Assert.True(constraintResults[0].IsWarning);
        Assert.True(constraintResults[1].IsDenied);
    }

    /// <summary>
    /// Verifies that decision-policy constraint results remain read-only from the policy consumer's perspective.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task DecisionPolicyConstraintResultsRemainReadOnlyForPolicyConsumers()
    {
        TestPolicyContext context = CreateContext();
        var policy = new CapturingDecisionPolicy();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Allow()),
                new StaticConstraint(ConstraintEvaluationResult.NotApplicable())
            ],
            decisionPolicy: policy, threatModelContributors: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(1, policy.ApplyCount);
        Assert.NotNull(policy.ConstraintResults);
        Assert.Equal(2, policy.ConstraintResults.Count);

        IList<ConstraintEvaluationResult> listView = Assert.IsAssignableFrom<IList<ConstraintEvaluationResult>>(policy.ConstraintResults);
        _ = Assert.Throws<NotSupportedException>(() => listView.Add(ConstraintEvaluationResult.Allow()));
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-allocation-review-123",
            PolicyVersion = "v-allocation-review",
            PolicyHash = "hash-allocation-review"
        };
    }

    private sealed class TestPolicyContext : IGovernanceEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class StaticConstraint(ConstraintEvaluationResult result) : IGovernanceConstraint<TestPolicyContext>
    {
        private readonly ConstraintEvaluationResult result = result;

        public string Name => "static-allocation-review-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DelegateConstraint(
        Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate) : IGovernanceConstraint<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate = evaluate;

        public string Name => "delegate-allocation-review-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(evaluate(context, cancellationToken));
        }
    }

    private sealed class CapturingDecisionPolicy : IGovernanceDecisionPolicy<TestPolicyContext>
    {
        public int ApplyCount { get; private set; }

        public GovernanceDecision? ComposedDecision { get; private set; }

        public IReadOnlyList<ConstraintEvaluationResult>? ConstraintResults { get; private set; }

        public ValueTask<GovernanceDecision> ApplyAsync(
            TestPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            ComposedDecision = composedDecision;
            ConstraintResults = constraintResults;

            return ValueTask.FromResult(composedDecision);
        }
    }
}
