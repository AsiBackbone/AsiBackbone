using AsiBackbone.Core.Constraints;

namespace Company.AsibackboneTemplate.Governance;

/// <summary>
/// Sample host-owned constraint that requires a region for manually evaluated operations.
/// Endpoint-governance middleware also passes endpoint metadata, so the template allows endpoints that carry explicit policy metadata.
/// </summary>
public sealed class SampleRegionConstraint : IGovernanceConstraint<GovernanceEvaluationContext>
{
    public string Name => "sample.region";

    public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        GovernanceEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool hasRegion = context.Metadata.TryGetValue("region", out string? region)
            && !string.IsNullOrWhiteSpace(region);
        bool hasEndpointPolicyMetadata = context.Metadata.ContainsKey("endpoint.policy_types");

        return ValueTask.FromResult(hasRegion || hasEndpointPolicyMetadata
            ? ConstraintEvaluationResult.Allow()
            : ConstraintEvaluationResult.Deny(
                "template.region.missing",
                "A region is required before this host allows the operation to continue."));
    }
}
