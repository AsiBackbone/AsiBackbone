using AsiBackbone.Core.Audit;

namespace AsiBackbone.Testing.Contracts;

/// <summary>
/// Reusable contract fixture for <see cref="IDecisionReceiptSink" /> implementations.
/// </summary>
public abstract class DecisionReceiptSinkContract
{
    /// <summary>
    /// Creates the audit sink implementation under test.
    /// </summary>
    /// <returns>The audit sink implementation to validate.</returns>
    protected abstract IDecisionReceiptSink CreateAuditSink();

    /// <summary>
    /// Creates the decision receipt supplied to the audit sink implementation under test.
    /// </summary>
    /// <returns>The decision receipt to write.</returns>
    protected abstract IDecisionReceipt CreateDecisionReceipt();

    /// <summary>
    /// Verifies that an audit sink accepts a valid decision receipt value without weakening the residue shape.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the contract validation.</param>
    /// <returns>A task that completes when the contract validation succeeds.</returns>
    public async ValueTask VerifyAuditSinkAcceptsValidResidueAsync(CancellationToken cancellationToken = default)
    {
        IDecisionReceiptSink auditSink = CreateAuditSink()
            ?? throw new GovernanceContractViolationException("Audit sink contract must provide an audit sink instance.");
        IDecisionReceipt residue = CreateDecisionReceipt()
            ?? throw new GovernanceContractViolationException("Audit sink contract must provide decision receipt.");

        _ = GovernanceDecisionContract.VerifyDecisionReceipt(residue, "Audit sink residue");

        try
        {
            await auditSink.WriteAsync(residue, cancellationToken).ConfigureAwait(false);
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
                "Audit sink implementations must accept valid decision receipt during normal contract validation or document a fail-closed/degraded behavior.",
                exception);
        }
    }
}
