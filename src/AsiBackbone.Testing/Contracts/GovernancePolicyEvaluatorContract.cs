using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;

namespace AsiBackbone.Testing.Contracts;

/// <summary>
/// Reusable contract fixture for <see cref="IGovernancePolicyEvaluator{TContext}" /> implementations.
/// </summary>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
public abstract class GovernancePolicyEvaluatorContract<TContext>
    where TContext : IGovernanceEvaluationContext
{
    /// <summary>
    /// Creates the evaluator implementation under test.
    /// </summary>
    /// <returns>The evaluator implementation to validate.</returns>
    protected abstract IGovernancePolicyEvaluator<TContext> CreateEvaluator();

    /// <summary>
    /// Creates the context supplied to the evaluator implementation under test.
    /// </summary>
    /// <returns>The evaluation context to validate with.</returns>
    protected abstract TContext CreateEvaluationContext();

    /// <summary>
    /// Verifies that the evaluator returns a safe, non-null decision and preserves supplied decision telemetry.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the contract validation.</param>
    /// <returns>The verified governance decision.</returns>
    public async ValueTask<GovernanceDecision> VerifyEvaluatorReturnsSafeDecisionAsync(CancellationToken cancellationToken = default)
    {
        IGovernancePolicyEvaluator<TContext> evaluator = CreateEvaluator()
            ?? throw new GovernanceContractViolationException("Policy evaluator contract must provide an evaluator instance.");
        TContext context = CreateEvaluationContext()
            ?? throw new GovernanceContractViolationException("Policy evaluator contract must provide an evaluation context.");

        try
        {
            GovernanceDecision decision = await evaluator.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            return GovernanceDecisionContract.VerifyTelemetryFromContext(decision, context, "Policy evaluator decision");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GovernanceContractViolationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new GovernanceContractViolationException(
                "Policy evaluator implementations must not fail open or bypass the policy pipeline by throwing during normal contract validation.",
                exception);
        }
    }
}
