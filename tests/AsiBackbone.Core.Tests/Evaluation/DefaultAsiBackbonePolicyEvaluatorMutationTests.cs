using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Mutation-focused tests for high-value evaluator behavior.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorMutationTests
{
    /// <summary>
    /// Tests that the evaluator aggregates all denial reasons and suppresses warning reasons when at least one constraint denies the operation.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateAggregatesAllDenialReasonsAndSuppressesWarningReasons()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.warning",
                        "The constraint produced a warning.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.denied.first",
                        "The first constraint denied the operation.")),
                new StaticConstraint(ConstraintEvaluationResult.Allow()),
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.denied.second",
                        "The second constraint denied the operation."))
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            ["constraint.denied.first", "constraint.denied.second"],
            decision.ReasonCodes);
        Assert.DoesNotContain("constraint.warning", decision.ReasonCodes);
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);
    }

    /// <summary>
    /// Tests that the evaluator aggregates all warning reasons when no constraints deny the operation, and that the decision is marked as a warning but still allows proceeding.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateAggregatesWarningReasonsWhenNoDenialsExist()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Allow()),
                new StaticConstraint(ConstraintEvaluationResult.NotApplicable()),
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.warning.first",
                        "The first warning was produced.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.warning.second",
                        "The second warning was produced."))
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Equal(
            ["constraint.warning.first", "constraint.warning.second"],
            decision.ReasonCodes);
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);
    }

    /// <summary>
    /// Tests that the evaluator runs all constraints and passes all results to the decision policy after a denial.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateRunsAllConstraintsAndPassesAllResultsToDecisionPolicyAfterDenial()
    {
        TestPolicyContext context = CreateContext();
        int constraintsRun = 0;
        var policy = new PassthroughCapturingDecisionPolicy();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    (_, _) =>
                    {
                        constraintsRun++;
                        return new ValueTask<ConstraintEvaluationResult>(
                            ConstraintEvaluationResult.Deny(
                                "constraint.denied",
                                "The constraint denied the operation."));
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        constraintsRun++;
                        return new ValueTask<ConstraintEvaluationResult>(ConstraintEvaluationResult.Allow());
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        constraintsRun++;
                        return new ValueTask<ConstraintEvaluationResult>(
                            ConstraintEvaluationResult.Warning(
                                "constraint.warning",
                                "The constraint produced a warning."));
                    })
            ],
            decisionPolicy: policy, threatModelContributors: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal(3, constraintsRun);
        Assert.NotNull(policy.ComposedDecision);
        Assert.True(policy.ComposedDecision.IsDenied);
        Assert.Equal("constraint.denied", Assert.Single(policy.ComposedDecision.ReasonCodes));
        Assert.NotNull(policy.ConstraintResults);
        Assert.Equal(
            [
                ConstraintEvaluationOutcome.Denied,
                ConstraintEvaluationOutcome.Allowed,
                ConstraintEvaluationOutcome.Warning
            ],
            policy.ConstraintResults.Select(result => result.Outcome));
    }

    /// <summary>
    /// Tests that the evaluator honors cancellation requests between constraint evaluations, ensuring that subsequent constraints are not evaluated after cancellation.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateHonorsCancellationBetweenConstraints()
    {
        TestPolicyContext context = CreateContext();
        int firstConstraintRuns = 0;
        int secondConstraintRuns = 0;
        using var cancellationTokenSource = new CancellationTokenSource();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    (_, _) =>
                    {
                        firstConstraintRuns++;
                        cancellationTokenSource.Cancel();

                        return new ValueTask<ConstraintEvaluationResult>(ConstraintEvaluationResult.Allow());
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        secondConstraintRuns++;
                        return new ValueTask<ConstraintEvaluationResult>(ConstraintEvaluationResult.Allow());
                    })
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await evaluator.EvaluateAsync(context, cancellationTokenSource.Token));

        Assert.Equal(1, firstConstraintRuns);
        Assert.Equal(0, secondConstraintRuns);
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-mutation-123",
            PolicyVersion = "v-mutation",
            PolicyHash = "hash-mutation"
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

        public string Name => "static-mutation-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<ConstraintEvaluationResult>(result);
        }
    }

    private sealed class DelegateConstraint(
        Func<TestPolicyContext, CancellationToken, ValueTask<ConstraintEvaluationResult>> evaluate) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ValueTask<ConstraintEvaluationResult>> evaluate = evaluate;

        public string Name => "delegate-mutation-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return evaluate(context, cancellationToken);
        }
    }

    private sealed class PassthroughCapturingDecisionPolicy : IAsiBackboneDecisionPolicy<TestPolicyContext>
    {
        public GovernanceDecision? ComposedDecision { get; private set; }

        public IReadOnlyList<ConstraintEvaluationResult>? ConstraintResults { get; private set; }

        public ValueTask<GovernanceDecision> ApplyAsync(
            TestPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            ComposedDecision = composedDecision;
            ConstraintResults = constraintResults;

            return new ValueTask<GovernanceDecision>(composedDecision);
        }
    }
}
