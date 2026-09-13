using System.Globalization;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using BenchmarkDotNet.Attributes;

namespace AsiBackbone.Benchmarks.BenchmarkDotNet;

/// <summary>
/// Allocation-focused benchmark scenarios for <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}"/>.
/// </summary>
/// <remarks>
/// These scenarios isolate the decision-policy and denied-decision composition paths reviewed in issue #484.
/// They complement <see cref="AsiBackboneHotPathBenchmarks"/> by making constraint-result visibility and
/// first-denial reason composition explicit benchmark dimensions.
/// </remarks>
[MemoryDiagnoser]
[RankColumn]
public class PolicyEvaluatorAllocationBenchmarks
{
    private readonly BdnPolicyContext allAllowWithDecisionPolicyContext = CreateContext("policy_evaluator.all_allow_with_decision_policy_8");
    private readonly BdnPolicyContext mixedWithDecisionPolicyContext = CreateContext("policy_evaluator.warning_and_denial_full_with_decision_policy");
    private readonly BdnPolicyContext firstDenialWithDecisionPolicyContext = CreateContext("policy_evaluator.first_denial_with_decision_policy");
    private readonly BdnPolicyContext firstDenialReasonCompositionContext = CreateContext("policy_evaluator.first_denial_reason_composition");

    private readonly IAsiBackbonePolicyEvaluator<BdnPolicyContext> allAllowWithDecisionPolicyEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnPolicyContext>()
            .AddConstraints(CreateStaticConstraints(8, ConstraintEvaluationResult.Allow()))
            .WithDecisionPolicy(new BdnPassThroughDecisionPolicy())
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnPolicyContext> mixedWithDecisionPolicyEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnPolicyContext>()
            .AddConstraints(CreateMixedConstraints())
            .WithDecisionPolicy(new BdnPassThroughDecisionPolicy())
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnPolicyContext> firstDenialWithDecisionPolicyEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnPolicyContext>()
            .AddConstraints(CreateMixedConstraints())
            .WithDecisionPolicy(new BdnPassThroughDecisionPolicy())
            .WithOptions(new AsiBackbonePolicyEvaluatorOptions { ShortCircuitOnFirstDenial = true })
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnPolicyContext> firstDenialReasonCompositionEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnPolicyContext>()
            .AddConstraints(CreateWarningThenDenialConstraints())
            .WithOptions(new AsiBackbonePolicyEvaluatorOptions { ShortCircuitOnFirstDenial = true })
            .Build();

    /// <summary>
    /// Benchmarks all-allow policy evaluation when a decision policy requires complete constraint-result visibility.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome.</returns>
    [Benchmark(Description = "policy_evaluator.all_allow_with_decision_policy_8")]
    public int PolicyAllAllowWithDecisionPolicy8()
    {
        GovernanceDecision decision = allAllowWithDecisionPolicyEvaluator
            .EvaluateAsync(allAllowWithDecisionPolicyContext)
            .GetAwaiter()
            .GetResult();

        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks full mixed-outcome policy evaluation when a decision policy requires all evaluated constraint results.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome.</returns>
    [Benchmark(Description = "policy_evaluator.warning_and_denial_full_with_decision_policy")]
    public int PolicyWarningAndDenialFullWithDecisionPolicy()
    {
        GovernanceDecision decision = mixedWithDecisionPolicyEvaluator
            .EvaluateAsync(mixedWithDecisionPolicyContext)
            .GetAwaiter()
            .GetResult();

        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks first-denial short-circuiting when a decision policy still receives only evaluated constraint results.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome.</returns>
    [Benchmark(Description = "policy_evaluator.first_denial_with_decision_policy")]
    public int PolicyFirstDenialWithDecisionPolicy()
    {
        GovernanceDecision decision = firstDenialWithDecisionPolicyEvaluator
            .EvaluateAsync(firstDenialWithDecisionPolicyContext)
            .GetAwaiter()
            .GetResult();

        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks denied-decision reason composition when warnings precede the first denial in fast-abort mode.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome.</returns>
    [Benchmark(Description = "policy_evaluator.first_denial_reason_composition")]
    public int PolicyFirstDenialReasonComposition()
    {
        GovernanceDecision decision = firstDenialReasonCompositionEvaluator
            .EvaluateAsync(firstDenialReasonCompositionContext)
            .GetAwaiter()
            .GetResult();

        return Checksum(decision);
    }

    private static int Checksum(GovernanceDecision decision)
    {
        return ((int)decision.Outcome * 397) ^ decision.ReasonCodes.Count;
    }

    private static BdnPolicyContext CreateContext(string scenarioName)
    {
        return new BdnPolicyContext
        {
            CorrelationId = "benchmark-correlation",
            PolicyVersion = "benchmark-policy-v1",
            PolicyHash = "benchmark-policy-hash",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["benchmark"] = scenarioName,
                ["source"] = nameof(PolicyEvaluatorAllocationBenchmarks)
            }
        };
    }

    private static IAsiBackboneConstraint<BdnPolicyContext>[] CreateStaticConstraints(int count, ConstraintEvaluationResult result)
    {
        var constraints = new IAsiBackboneConstraint<BdnPolicyContext>[count];

        for (int index = 0; index < constraints.Length; index++)
        {
            constraints[index] = new BdnStaticConstraint($"constraint-{index.ToString(CultureInfo.InvariantCulture)}", result);
        }

        return constraints;
    }

    private static IAsiBackboneConstraint<BdnPolicyContext>[] CreateMixedConstraints()
    {
        return
        [
            new BdnStaticConstraint("allow-1", ConstraintEvaluationResult.Allow()),
            new BdnStaticConstraint("warning-1", ConstraintEvaluationResult.Warning("policy.warning", "Policy produced a warning.")),
            new BdnStaticConstraint("deny-1", ConstraintEvaluationResult.Deny("policy.denied", "Policy denied the operation.")),
            new BdnStaticConstraint("allow-2", ConstraintEvaluationResult.Allow()),
            new BdnStaticConstraint("warning-2", ConstraintEvaluationResult.Warning("policy.second_warning", "Second policy warning.")),
            new BdnStaticConstraint("deny-2", ConstraintEvaluationResult.Deny("policy.second_denied", "Second policy denial."))
        ];
    }

    private static IAsiBackboneConstraint<BdnPolicyContext>[] CreateWarningThenDenialConstraints()
    {
        return
        [
            new BdnStaticConstraint("warning-1", ConstraintEvaluationResult.Warning("policy.warning", "Policy produced a warning.")),
            new BdnStaticConstraint("deny-1", ConstraintEvaluationResult.Deny("policy.denied", "Policy denied the operation.")),
            new BdnStaticConstraint("skipped-1", ConstraintEvaluationResult.Warning("policy.skipped", "This warning should not be evaluated."))
        ];
    }

    private sealed class BdnStaticConstraint(string name, ConstraintEvaluationResult result) : IAsiBackboneConstraint<BdnPolicyContext>
    {
        public string Name { get; } = name;

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(BdnPolicyContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BdnPassThroughDecisionPolicy : IAsiBackboneDecisionPolicy<BdnPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            BdnPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(composedDecision);
        }
    }

    private sealed class BdnPolicyContext : IAsiBackboneConstraintEvaluationContext
    {
        public string? CorrelationId { get; init; }
        public string? PolicyVersion { get; init; }
        public string? PolicyHash { get; init; }
        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
