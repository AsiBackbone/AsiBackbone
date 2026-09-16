using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Results;

namespace AsiBackbone.Testing.Contracts;

/// <summary>
/// Reusable contract fixture for <see cref="IGovernanceConstraint{TContext}" /> implementations.
/// </summary>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
public abstract class GovernanceConstraintContract<TContext>
{
    /// <summary>
    /// Creates the constraint implementation under test.
    /// </summary>
    /// <returns>The constraint implementation to validate.</returns>
    protected abstract IGovernanceConstraint<TContext> CreateConstraint();

    /// <summary>
    /// Creates the context supplied to the constraint implementation under test.
    /// </summary>
    /// <returns>The evaluation context to validate with.</returns>
    protected abstract TContext CreateEvaluationContext();

    /// <summary>
    /// Verifies that the constraint returns a safe, non-null result shape.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the contract validation.</param>
    /// <returns>The verified constraint result.</returns>
    public async ValueTask<ConstraintEvaluationResult> VerifyConstraintReturnsSafeResultAsync(CancellationToken cancellationToken = default)
    {
        IGovernanceConstraint<TContext> constraint = CreateConstraint()
            ?? throw new GovernanceContractViolationException("Constraint contract must provide a constraint instance.");
        TContext context = CreateEvaluationContext()
            ?? throw new GovernanceContractViolationException("Constraint contract must provide an evaluation context.");

        if (string.IsNullOrWhiteSpace(constraint.Name))
        {
            throw new GovernanceContractViolationException("Constraint implementations must expose a stable, non-empty constraint name.");
        }

        try
        {
            ConstraintEvaluationResult result = await constraint.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            return VerifyConstraintResult(result, constraint.Name);
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
                $"Constraint '{constraint.Name}' must not throw during normal contract validation; return Deny, Warning, or NotApplicable instead.",
                exception);
        }
    }

    private static ConstraintEvaluationResult VerifyConstraintResult(ConstraintEvaluationResult? result, string constraintName)
    {
        if (result is null)
        {
            throw new GovernanceContractViolationException($"Constraint '{constraintName}' must return a result and must never return null.");
        }

        if (result.IsDenied || result.IsWarning)
        {
            if (result.Reasons.Count == 0)
            {
                throw new GovernanceContractViolationException($"Constraint '{constraintName}' denied or warned without a reason code.");
            }

            for (int index = 0; index < result.Reasons.Count; index++)
            {
                OperationReason reason = result.Reasons[index] ?? throw new GovernanceContractViolationException($"Constraint '{constraintName}' returned a null reason at index {index}.");

                if (string.IsNullOrWhiteSpace(reason.Code))
                {
                    throw new GovernanceContractViolationException($"Constraint '{constraintName}' returned an empty reason code at index {index}.");
                }

                if (string.IsNullOrWhiteSpace(reason.Message))
                {
                    throw new GovernanceContractViolationException($"Constraint '{constraintName}' returned an empty reason message at index {index}.");
                }
            }
        }

        return result;
    }
}
