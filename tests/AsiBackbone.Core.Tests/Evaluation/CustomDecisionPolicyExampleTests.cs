using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Executable examples for host-owned <see cref="IGovernanceDecisionPolicy{TContext}" /> patterns.
/// </summary>
public sealed class CustomDecisionPolicyExampleTests
{
    /// <summary>
    /// Demonstrates a custom decision policy that preserves both warning and deny reasons when a strict deny wins policy is applied.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task StrictDenyWinsPolicyPreservesWarningAndDenyReasons()
    {
        var evaluator = new DefaultGovernancePolicyEvaluator<ExamplePolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Warning(
                    "policy.warning",
                    "The request is allowed only with review visibility.")),
                new StaticConstraint(ConstraintEvaluationResult.Deny(
                    "policy.denied",
                    "The request failed a blocking policy rule."))
            ],
            decisionPolicy: new StrictDenyWinsDecisionPolicy(), threatModelContributors: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            ExamplePolicyContext.Create("US-LA", "routine"),
            TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal(
            ["policy.warning", "policy.denied"],
            decision.ReasonCodes);
    }

    /// <summary>
    /// Demonstrates a custom decision policy that overlays regional requirements on top of global policy decisions, requiring acknowledgment for high-risk actions in supported regions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task RegionalOverlayPolicyCanRequireAcknowledgmentForLocalHighRiskAction()
    {
        var evaluator = new DefaultGovernancePolicyEvaluator<ExamplePolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            decisionPolicy: new RegionalOverlayDecisionPolicy(CreateSupportedRegionSet()), threatModelContributors: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            ExamplePolicyContext.Create("US-LA", "high"),
            TestContext.Current.CancellationToken);

        Assert.True(decision.RequiresAcknowledgment);
        Assert.Equal("regional.acknowledgment_required", Assert.Single(decision.ReasonCodes));
        Assert.Equal("corr-US-LA-high", decision.CorrelationId);
        Assert.Equal("policy-v1", decision.PolicyVersion);
        Assert.Equal("policy-hash-v1", decision.PolicyHash);
    }

    /// <summary>
    /// Demonstrates a custom decision policy that overlays regional requirements on top of global policy decisions, denying actions in unsupported regions even if the global policy allows them.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task RegionalOverlayPolicyNarrowsGlobalAllowForUnsupportedRegion()
    {
        var evaluator = new DefaultGovernancePolicyEvaluator<ExamplePolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            decisionPolicy: new RegionalOverlayDecisionPolicy(CreateSupportedRegionSet()), threatModelContributors: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            ExamplePolicyContext.Create("EU-DE", "routine"),
            TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("regional.unsupported", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Demonstrates a custom decision policy that overlays regional requirements on top of global policy decisions, ensuring that an existing deny decision from a global constraint is not overridden by the regional overlay.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task RegionalOverlayPolicyDoesNotOverrideExistingDenyDecision()
    {
        var evaluator = new DefaultGovernancePolicyEvaluator<ExamplePolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Deny(
                "constraint.blocked",
                "The constraint blocked the request before regional overlay."))],
            decisionPolicy: new RegionalOverlayDecisionPolicy(CreateSupportedRegionSet()), threatModelContributors: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            ExamplePolicyContext.Create("US-LA", "high"),
            TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("constraint.blocked", Assert.Single(decision.ReasonCodes));
    }

    private static HashSet<string> CreateSupportedRegionSet()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "US-LA",
            "US-TX"
        };
    }

    private sealed class StrictDenyWinsDecisionPolicy : IGovernanceDecisionPolicy<ExamplePolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            ExamplePolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            OperationReason[] warnings = [.. constraintResults
                .Where(static result => result.IsWarning)
                .SelectMany(static result => result.Reasons)];
            OperationReason[] denials = [.. constraintResults
                .Where(static result => result.IsDenied)
                .SelectMany(static result => result.Reasons)];

            return denials.Length > 0
                ? ValueTask.FromResult(GovernanceDecision.Deny(
                    warnings.Concat(denials),
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash))
                : warnings.Length > 0
                ? ValueTask.FromResult(GovernanceDecision.Warning(
                    warnings,
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash))
                : ValueTask.FromResult(composedDecision);
        }
    }

    private sealed class RegionalOverlayDecisionPolicy(IReadOnlySet<string> supportedRegions) :
        IGovernanceDecisionPolicy<ExamplePolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            ExamplePolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!composedDecision.CanProceed)
            {
                return ValueTask.FromResult(composedDecision);
            }

            string? region = context.Metadata.GetValueOrDefault("region");
            string? risk = context.Metadata.GetValueOrDefault("risk");

            return string.IsNullOrWhiteSpace(region) || !supportedRegions.Contains(region)
                ? ValueTask.FromResult(GovernanceDecision.Deny(
                    "regional.unsupported",
                    "The requested region is not enabled for this operation.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash))
                : string.Equals(risk, "high", StringComparison.OrdinalIgnoreCase)
                ? ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                    "regional.acknowledgment_required",
                    "The local overlay requires acknowledgment for this high-risk operation.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash))
                : ValueTask.FromResult(composedDecision);
        }
    }

    private sealed class StaticConstraint(ConstraintEvaluationResult result) :
        IGovernanceConstraint<ExamplePolicyContext>
    {
        public string Name => "example.static";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            ExamplePolicyContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ExamplePolicyContext : IGovernanceEvaluationContext
    {
        public required string CorrelationId { get; init; }

        public string PolicyVersion { get; init; } = "policy-v1";

        public string PolicyHash { get; init; } = "policy-hash-v1";

        public required IReadOnlyDictionary<string, string> Metadata { get; init; }

        public static ExamplePolicyContext Create(string region, string risk)
        {
            return new ExamplePolicyContext
            {
                CorrelationId = $"corr-{region}-{risk}",
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["region"] = region,
                    ["risk"] = risk
                }
            };
        }
    }
}
