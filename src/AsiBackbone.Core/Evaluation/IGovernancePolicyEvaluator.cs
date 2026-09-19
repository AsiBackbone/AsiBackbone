using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;

namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Evaluates a framework-neutral context against the active policy structure and composes the result into a governance decision.
/// </summary>
/// <remarks>
/// This is the decision step of the policy decision pipeline. The host supplies the constraints and context; the evaluator owns the composition loop.
/// </remarks>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
public interface IGovernancePolicyEvaluator<in TContext>
    where TContext : IGovernanceEvaluationContext
{
    /// <summary>
    /// Evaluates the supplied context and returns the composed governance decision.
    /// </summary>
    /// <param name="context">The framework-neutral evaluation context.</param>
    /// <param name="cancellationToken">A token that can cancel asynchronous evaluation.</param>
    /// <returns>The composed governance decision.</returns>
    ValueTask<GovernanceDecision> EvaluateAsync(
        TContext context,
        CancellationToken cancellationToken = default);
}
