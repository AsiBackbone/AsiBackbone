using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;

namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Applies domain- or host-specific decision rules after constraint composition.
/// </summary>
/// <remarks>
/// Constraints produce allow, deny, warning, or not-applicable results. Outcomes such as deferred,
/// acknowledgment-required, and escalation-recommended depend on risk, policy, and broader context
/// rather than any single constraint, so they are produced by this policy boundary.
/// </remarks>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
public interface IGovernanceDecisionPolicy<in TContext>
    where TContext : IGovernanceEvaluationContext
{
    /// <summary>
    /// Applies decision rules to the decision composed from constraint results.
    /// </summary>
    /// <param name="context">The framework-neutral evaluation context.</param>
    /// <param name="composedDecision">The decision composed from constraint results.</param>
    /// <param name="constraintResults">The individual constraint results.</param>
    /// <param name="cancellationToken">A token that can cancel asynchronous evaluation.</param>
    /// <returns>The final governance decision.</returns>
    ValueTask<GovernanceDecision> ApplyAsync(
        TContext context,
        GovernanceDecision composedDecision,
        IReadOnlyList<ConstraintEvaluationResult> constraintResults,
        CancellationToken cancellationToken = default);
}
