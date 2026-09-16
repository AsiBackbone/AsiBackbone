using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Tests for the optional latency-optimized fast-abort evaluation path.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorFastAbortTests
{
    private static readonly string[] ExpectedDefaultEvaluationOrder = ["first", "second", "third"];

    /// <summary>
    /// Tests that the default evaluation behavior of the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}"/> runs all constraints even after the first denied result is encountered.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task DefaultEvaluationRunsAllConstraintsAfterFirstDeniedResult()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("first");
                        return ConstraintEvaluationResult.Deny(
                            "constraint.first_denied",
                            "The first constraint denied the operation.");
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("second");
                        return ConstraintEvaluationResult.Deny(
                            "constraint.second_denied",
                            "The second constraint denied the operation.");
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("third");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.third_warning",
                            "The third constraint produced a warning.");
                    })
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedDefaultEvaluationOrder, observedOrder);
        Assert.True(decision.IsDenied);
        Assert.Contains("constraint.first_denied", decision.ReasonCodes);
        Assert.Contains("constraint.second_denied", decision.ReasonCodes);
        Assert.DoesNotContain("constraint.third_warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Tests that when the <see cref="AsiBackbonePolicyEvaluatorOptions.ShortCircuitOnFirstDenial"/> option is enabled, the evaluation stops after the first denied result is encountered.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ShortCircuitOnFirstDenialStopsAfterFirstDeniedResult()
    {
        TestPolicyContext context = CreateContext();
        int skippedEvaluationCount = 0;

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.first_denied",
                        "The first constraint denied the operation.")),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        skippedEvaluationCount++;
                        return ConstraintEvaluationResult.Deny(
                            "constraint.second_denied",
                            "This constraint should not run.");
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
        Assert.Equal("constraint.first_denied", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Tests that when the <see cref="AsiBackbonePolicyEvaluatorOptions.ShortCircuitOnFirstDenial"/> option is enabled, any warnings produced before the first denied result are preserved in the final decision.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ShortCircuitOnFirstDenialPreservesWarningsProducedBeforeDeniedResult()
    {
        TestPolicyContext context = CreateContext();
        int skippedEvaluationCount = 0;

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.pre_warning",
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
            ["constraint.pre_warning", "constraint.denied"],
            decision.ReasonCodes);
    }

    /// <summary>
    /// Tests that when the <see cref="AsiBackbonePolicyEvaluatorOptions.ShortCircuitOnFirstDenial"/> option is enabled, only the evaluated constraint results are passed to the decision policy, and any skipped constraints are not included in the results.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ShortCircuitOnFirstDenialPassesOnlyEvaluatedConstraintResultsToDecisionPolicy()
    {
        TestPolicyContext context = CreateContext();
        var policy = new CapturingDecisionPolicy();
        int skippedEvaluationCount = 0;

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.pre_warning",
                        "The first constraint produced a warning.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.denied",
                        "The second constraint denied the operation.")),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        skippedEvaluationCount++;
                        return ConstraintEvaluationResult.Allow();
                    })
            ],
            decisionPolicy: policy,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                ShortCircuitOnFirstDenial = true
            }, threatModelContributors: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(0, skippedEvaluationCount);
        Assert.True(decision.IsDeferred);
        Assert.Equal(1, policy.ApplyCount);
        Assert.NotNull(policy.ComposedDecision);
        Assert.True(policy.ComposedDecision.IsDenied);
        Assert.Contains("constraint.pre_warning", policy.ComposedDecision.ReasonCodes);
        Assert.Contains("constraint.denied", policy.ComposedDecision.ReasonCodes);
        Assert.NotNull(policy.ConstraintResults);
        Assert.Equal(2, policy.ConstraintResults.Count);
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-fast-abort-123",
            PolicyVersion = "v-fast-abort",
            PolicyHash = "hash-fast-abort"
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
            return new ValueTask<ConstraintEvaluationResult>(
                evaluate(context, cancellationToken));
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
