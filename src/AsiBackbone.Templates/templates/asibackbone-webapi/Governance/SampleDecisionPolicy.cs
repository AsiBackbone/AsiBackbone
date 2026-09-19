using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;

namespace Company.AsibackboneTemplate.Governance;

/// <summary>
/// Sample host-owned decision policy that can raise otherwise-allowed consequential actions into an acknowledgment-required decision.
/// </summary>
public sealed class SampleDecisionPolicy : IGovernanceDecisionPolicy<GovernanceEvaluationContext>
{
    public ValueTask<GovernanceDecision> ApplyAsync(
        GovernanceEvaluationContext context,
        GovernanceDecision composedDecision,
        IReadOnlyList<ConstraintEvaluationResult> constraintResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!composedDecision.CanProceed)
        {
            return ValueTask.FromResult(composedDecision);
        }

        bool isConsequential = context.Metadata.TryGetValue("risk", out string? risk)
            && string.Equals(risk, "consequential", StringComparison.OrdinalIgnoreCase);

        return ValueTask.FromResult(isConsequential
            ? GovernanceDecision.RequireAcknowledgment(
                "template.acknowledgment.required",
                "Consequential actions require host-owned acknowledgment before execution.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)
            : composedDecision);
    }
}
