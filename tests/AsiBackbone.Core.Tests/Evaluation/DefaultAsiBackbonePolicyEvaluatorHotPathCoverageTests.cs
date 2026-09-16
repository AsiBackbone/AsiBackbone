using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Focused branch-coverage tests for allocation-optimized policy evaluator hot paths.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorHotPathCoverageTests
{
    /// <summary>
    /// Verifies that a policy evaluation with all pass-through constraints produces an allowed decision with no reason codes.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWithAllPassThroughConstraintsProducesAllowedDecision()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Allow()),
                new StaticConstraint(ConstraintEvaluationResult.NotApplicable()),
                new StaticConstraint(ConstraintEvaluationResult.Allow())
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.True(decision.CanProceed);
        Assert.Empty(decision.ReasonCodes);
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);
    }

    /// <summary>
    /// Verifies that a policy evaluation with multiple warning-producing constraints aggregates the warning reason codes into the final decision.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWithMultipleWarningsAggregatesWarningReasons()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.first_warning",
                        "The first constraint produced a warning.")),
                new StaticConstraint(ConstraintEvaluationResult.NotApplicable()),
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.second_warning",
                        "The second constraint produced a warning."))
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Equal(
            ["constraint.first_warning", "constraint.second_warning"],
            decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a policy evaluation with multiple denied constraints aggregates the denied reason codes into the final decision and drops any prior warnings by default.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWithMultipleDenialsAggregatesDeniedReasonsAndDropsPriorWarningsByDefault()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.prior_warning",
                        "The prior warning should not govern the denied decision.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.first_denied",
                        "The first constraint denied the operation.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.second_denied",
                        "The second constraint denied the operation."))
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            ["constraint.first_denied", "constraint.second_denied"],
            decision.ReasonCodes);
        Assert.DoesNotContain("constraint.prior_warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a policy evaluation with short-circuiting on the first denial preserves only the evaluated reason codes, skipping any subsequent constraints.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ShortCircuitWithPriorWarningsPreservesOnlyEvaluatedReasons()
    {
        TestPolicyContext context = CreateContext();
        int skippedEvaluationCount = 0;
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.first_warning",
                        "The first constraint produced a warning.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.second_warning",
                        "The second constraint produced a warning.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.denied",
                        "The constraint denied the operation.")),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        skippedEvaluationCount++;
                        return ConstraintEvaluationResult.Warning(
                            "constraint.skipped_warning",
                            "This warning should not be evaluated.");
                    })
            ],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                ShortCircuitOnFirstDenial = true
            }, threatModelContributors: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(0, skippedEvaluationCount);
        Assert.True(decision.IsDenied);
        Assert.Equal(
            ["constraint.first_warning", "constraint.second_warning", "constraint.denied"],
            decision.ReasonCodes);
        Assert.DoesNotContain("constraint.skipped_warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a decision policy receives read-only results for all pass-through constraints, and that attempts to modify the list of constraint results throw a NotSupportedException.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task DecisionPolicyReceivesReadOnlyResultsForAllPassThroughConstraints()
    {
        TestPolicyContext context = CreateContext();
        var policy = new CapturingDecisionPolicy();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Allow()),
                new StaticConstraint(ConstraintEvaluationResult.NotApplicable())
            ],
            decisionPolicy: policy, threatModelContributors: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDeferred);
        Assert.Equal(1, policy.ApplyCount);
        Assert.NotNull(policy.ComposedDecision);
        Assert.True(policy.ComposedDecision.IsAllowed);

        IReadOnlyList<ConstraintEvaluationResult> constraintResults = Assert.IsAssignableFrom<IReadOnlyList<ConstraintEvaluationResult>>(policy.ConstraintResults);
        Assert.Equal(2, constraintResults.Count);

        IList<ConstraintEvaluationResult> listView = Assert.IsAssignableFrom<IList<ConstraintEvaluationResult>>(policy.ConstraintResults);
        _ = Assert.Throws<NotSupportedException>(() => listView.Add(ConstraintEvaluationResult.Allow()));
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-hot-path-123",
            PolicyVersion = "v-hot-path",
            PolicyHash = "hash-hot-path"
        };
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

        public string Name => "static-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<ConstraintEvaluationResult>(result);
        }
    }

    private sealed class DelegateConstraint(
        Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate = evaluate;

        public string Name => "delegate-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<ConstraintEvaluationResult>(evaluate(context, cancellationToken));
        }
    }

    private sealed class CapturingDecisionPolicy : IAsiBackboneDecisionPolicy<TestPolicyContext>
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

            return new ValueTask<GovernanceDecision>(
                GovernanceDecision.Defer(
                    "policy.deferred",
                    "The capturing policy deferred the operation.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash));
        }
    }
}
