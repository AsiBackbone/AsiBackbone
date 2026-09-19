using AsiBackbone.Core.Audit;

namespace AsiBackbone.Testing.Contracts;

/// <summary>
/// Reusable contract fixture for <see cref="IDecisionReceiptSink" /> implementations.
/// </summary>
public abstract class DecisionReceiptSinkContract
{
    /// <summary>
    /// Creates the decision receipt sink implementation under test.
    /// </summary>
    /// <returns>The decision receipt sink implementation to validate.</returns>
    protected abstract IDecisionReceiptSink CreateDecisionReceiptSink();

    /// <summary>
    /// Creates the decision receipt supplied to the sink implementation under test.
    /// </summary>
    /// <returns>The decision receipt to write.</returns>
    protected abstract IDecisionReceipt CreateDecisionReceipt();

    /// <summary>
    /// Verifies that a decision receipt sink accepts a valid decision receipt value without weakening its required shape.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the contract validation.</param>
    /// <returns>A task that completes when the contract validation succeeds.</returns>
    public async ValueTask VerifyDecisionReceiptSinkAcceptsValidReceiptAsync(CancellationToken cancellationToken = default)
    {
        IDecisionReceiptSink receiptSink = CreateDecisionReceiptSink()
            ?? throw new GovernanceContractViolationException("Decision receipt sink contract must provide a sink instance.");
        IDecisionReceipt receipt = CreateDecisionReceipt()
            ?? throw new GovernanceContractViolationException("Decision receipt sink contract must provide a decision receipt.");

        _ = GovernanceDecisionContract.VerifyDecisionReceipt(receipt, "Decision receipt sink receipt");

        try
        {
            await receiptSink.WriteAsync(receipt, cancellationToken).ConfigureAwait(false);
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
                "Decision receipt sink implementations must accept a valid decision receipt during normal contract validation or document a fail-closed/degraded behavior.",
                exception);
        }
    }
}
