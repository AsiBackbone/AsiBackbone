using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;

namespace AsiBackbone.Core.ThreatModeling;

/// <summary>
/// Contributes host-defined threat-aware assessment data to policy evaluation.
/// </summary>
/// <typeparam name="TContext">The framework-neutral context type evaluated by the contributor.</typeparam>
/// <remarks>
/// Contributors are extension points. AsiBackbone does not claim to detect threats by itself; hosts provide the checks that are appropriate for their domain.
/// Return <see cref="ThreatAssessment.NoThreat" /> when no finding is present. Actionable assessments must not recommend <see cref="GovernanceDecisionOutcome.Allowed" />; use <see cref="GovernanceDecisionOutcome.Warning" />, <see cref="GovernanceDecisionOutcome.Denied" />, <see cref="GovernanceDecisionOutcome.Deferred" />, <see cref="GovernanceDecisionOutcome.AcknowledgmentRequired" />, or <see cref="GovernanceDecisionOutcome.EscalationRecommended" /> instead.
/// </remarks>
public interface IThreatModelContributor<in TContext>
    where TContext : IGovernanceEvaluationContext
{
    /// <summary>
    /// Gets the stable name of the contributor for audit metadata.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Assesses the supplied context and returns a threat assessment.
    /// </summary>
    /// <param name="context">The context to assess.</param>
    /// <param name="cancellationToken">A token that can cancel asynchronous assessment.</param>
    /// <returns>The threat assessment reported by the contributor.</returns>
    ValueTask<ThreatAssessment> AssessAsync(
        TContext context,
        CancellationToken cancellationToken = default);
}
