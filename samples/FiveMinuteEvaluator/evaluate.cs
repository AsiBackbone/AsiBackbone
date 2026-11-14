#:package AsiBackbone.Core@7.5.4
#:property ManagePackageVersionsCentrally=false

// Five-minute evaluator: one host-owned rule, two requests, one allowed and one blocked.
// Run from the repository root with: dotnet run samples/FiveMinuteEvaluator/evaluate.cs
//
// The package pin above must equal the repository release version. Validate-VersionConsistency.ps1 enforces that,
// and the external consumer smoke test runs this file against locally packed packages before they are published.

using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;

DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator =
    DefaultGovernancePolicyEvaluator.CreateBuilder<GovernanceEvaluationContext>()
        .AddConstraint(new AllowedRegionConstraint("US-LA", "US-TX"))
        .WithOptions(new GovernancePolicyOptions())
        .Build();

int executions = 0;

foreach (string region in new[] { "US-LA", "EU-DE" })
{
    var context = new GovernanceEvaluationContext(
        correlationId: $"eval-{region}",
        policyVersion: "policy-v1",
        policyHash: "policy-hash-v1",
        metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["operation"] = "orders.approve",
            ["region"] = region
        });

    GovernanceDecision decision = await evaluator.EvaluateAsync(context);

    DecisionReceipt receipt = DecisionReceipt.FromDecision(
        GovernanceActorContext.Human("evaluator", "Evaluator"),
        operationName: "orders.approve",
        decision,
        metadata: context.Metadata);

    Console.WriteLine($"orders.approve in {region}");
    Console.WriteLine($"  Outcome:        {decision.Outcome}");
    Console.WriteLine($"  Reasons:        {(decision.ReasonCodes.Count == 0 ? "(none)" : string.Join(", ", decision.ReasonCodes))}");
    Console.WriteLine($"  Receipt:        {receipt.EventId}");

    if (decision.CanProceed)
    {
        // Host-owned execution: AsiBackbone returns the decision; the host performs the operation.
        executions++;
        Console.WriteLine("  Host executed:  yes");
    }
    else
    {
        Console.WriteLine("  Host executed:  no");
    }

    Console.WriteLine();
}

Console.WriteLine($"Host executions: {executions} of 2. The blocked request never reached host code.");

internal sealed class AllowedRegionConstraint(params string[] allowedRegions) : IGovernanceConstraint<GovernanceEvaluationContext>
{
    public string Name => "orders.allowed-region";

    public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        GovernanceEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        bool allowed = context.Metadata.TryGetValue("region", out string? region)
            && allowedRegions.Contains(region, StringComparer.Ordinal);

        return ValueTask.FromResult(allowed
            ? ConstraintEvaluationResult.Allow()
            : ConstraintEvaluationResult.Deny(
                "orders.region.not-allowed",
                "Orders can be approved only in configured regions."));
    }
}
