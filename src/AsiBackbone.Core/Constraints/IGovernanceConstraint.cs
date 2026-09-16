namespace AsiBackbone.Core.Constraints;

/// <summary>
/// Evaluates whether a supplied context satisfies a governance constraint.
/// </summary>
/// <typeparam name="TContext">The framework-neutral context type evaluated by the constraint.</typeparam>
public interface IGovernanceConstraint<in TContext>
{
    /// <summary>
    /// Gets the stable name of the constraint.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the constraint for the supplied context.
    /// </summary>
    /// <param name="context">The context to evaluate.</param>
    /// <param name="cancellationToken">A token that can cancel asynchronous evaluation.</param>
    /// <returns>The constraint evaluation result.</returns>
    ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        TContext context,
        CancellationToken cancellationToken = default);
}
