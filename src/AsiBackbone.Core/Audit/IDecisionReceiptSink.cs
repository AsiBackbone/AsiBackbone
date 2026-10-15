namespace AsiBackbone.Core.Audit;

/// <summary>
/// Receives decision receipt produced by the policy decision pipeline.
/// </summary>
/// <remarks>
/// Core defines the contract only. Storage providers, hosts, or future integration packages own how and where receipts are recorded.
/// </remarks>
public interface IDecisionReceiptSink
{
    /// <summary>
    /// Records a single decision receipt value.
    /// </summary>
    /// <param name="receipt">The decision receipt to record.</param>
    /// <param name="cancellationToken">A token that can cancel the write.</param>
    /// <returns>A task that completes when the receipt has been recorded.</returns>
    ValueTask WriteAsync(
        IDecisionReceipt receipt,
        CancellationToken cancellationToken = default);
}
