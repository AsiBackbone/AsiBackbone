using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using BenchmarkDotNet.Attributes;

namespace AsiBackbone.Benchmarks.BenchmarkDotNet;

/// <summary>
/// BenchmarkDotNet scenarios that make first-denial short-circuit savings visible when skipped constraints are meaningful work.
/// </summary>
/// <remarks>
/// These benchmarks complement the static-constraint hot-path baselines by placing CPU-bound tail constraints after the
/// first denial. The short-circuit path should skip that tail work, while the full-evaluation path intentionally keeps
/// running to preserve complete constraint visibility.
/// </remarks>
[MemoryDiagnoser]
[RankColumn]
public class FirstDenialShortCircuitBenchmarks
{
    private readonly BdnBenchmarkPolicyContext fullEvaluationContext = CreateContext("policy_evaluator.first_denial_expensive_tail_full");
    private readonly BdnBenchmarkPolicyContext shortCircuitContext = CreateContext("policy_evaluator.first_denial_expensive_tail_short_circuit");

    private readonly IGovernancePolicyEvaluator<BdnBenchmarkPolicyContext> fullEvaluationEvaluator =
        DefaultGovernancePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .AddConstraints(CreateExpensiveTailConstraints())
            .Build();

    private readonly IGovernancePolicyEvaluator<BdnBenchmarkPolicyContext> shortCircuitEvaluator =
        DefaultGovernancePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .AddConstraints(CreateExpensiveTailConstraints())
            .WithOptions(new GovernancePolicyOptions { ShortCircuitOnFirstDenial = true })
            .Build();

    /// <summary>
    /// Benchmarks full evaluation when expensive tail constraints remain after the first denial.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome.</returns>
    [Benchmark(Description = "policy_evaluator.first_denial_expensive_tail_full")]
    public int PolicyFirstDenialExpensiveTailFull()
    {
        GovernanceDecision decision = fullEvaluationEvaluator
            .EvaluateAsync(fullEvaluationContext)
            .GetAwaiter()
            .GetResult();

        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks first-denial short-circuiting when expensive tail constraints are skipped.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome.</returns>
    [Benchmark(Description = "policy_evaluator.first_denial_expensive_tail_short_circuit")]
    public int PolicyFirstDenialExpensiveTailShortCircuit()
    {
        GovernanceDecision decision = shortCircuitEvaluator
            .EvaluateAsync(shortCircuitContext)
            .GetAwaiter()
            .GetResult();

        return Checksum(decision);
    }

    private static int Checksum(GovernanceDecision decision)
    {
        return ((int)decision.Outcome * 397) ^ decision.ReasonCodes.Count;
    }

    private static BdnBenchmarkPolicyContext CreateContext(string scenarioName)
    {
        return new BdnBenchmarkPolicyContext
        {
            CorrelationId = "benchmark-correlation",
            PolicyVersion = "benchmark-policy-v1",
            PolicyHash = "benchmark-policy-hash",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["benchmark"] = scenarioName,
                ["source"] = nameof(FirstDenialShortCircuitBenchmarks)
            }
        };
    }

    private static IGovernanceConstraint<BdnBenchmarkPolicyContext>[] CreateExpensiveTailConstraints()
    {
        return
        [
            new BdnStaticConstraint("allow-before-denial", ConstraintEvaluationResult.Allow()),
            new BdnStaticConstraint("first-denial", ConstraintEvaluationResult.Deny("policy.denied", "Policy denied the operation.")),
            new BdnCpuBoundConstraint("expensive-tail-1", 512, ConstraintEvaluationResult.Allow()),
            new BdnCpuBoundConstraint("expensive-tail-2", 512, ConstraintEvaluationResult.Allow()),
            new BdnCpuBoundConstraint("expensive-tail-3", 512, ConstraintEvaluationResult.Allow()),
            new BdnCpuBoundConstraint("expensive-tail-4", 512, ConstraintEvaluationResult.Allow())
        ];
    }

    private sealed class BdnStaticConstraint(string name, ConstraintEvaluationResult result) : IGovernanceConstraint<BdnBenchmarkPolicyContext>
    {
        public string Name { get; } = name;

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(BdnBenchmarkPolicyContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BdnCpuBoundConstraint(string name, int iterations, ConstraintEvaluationResult result) : IGovernanceConstraint<BdnBenchmarkPolicyContext>
    {
        public string Name { get; } = name;

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(BdnBenchmarkPolicyContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int checksum = Name.Length;
            for (int index = 0; index < iterations; index++)
            {
                checksum = unchecked((checksum * 397) ^ index);
            }

            return checksum == int.MinValue
                ? ValueTask.FromResult(ConstraintEvaluationResult.Deny("policy.unreachable", "Unreachable benchmark guard."))
                : ValueTask.FromResult(result);
        }
    }

    private sealed class BdnBenchmarkPolicyContext : IGovernanceEvaluationContext
    {
        public string? CorrelationId { get; init; }
        public string? PolicyVersion { get; init; }
        public string? PolicyHash { get; init; }
        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
