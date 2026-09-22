namespace AsiBackbone.Core.Audit;

/// <summary>
/// Defines a provider-neutral durable store for decision receipt lifecycle events.
/// </summary>
public interface IDecisionReceiptLifecycleStore
{
    /// <summary>
    /// Appends an decision receipt lifecycle event before optional downstream provider delivery is attempted.
    /// </summary>
    ValueTask<DecisionReceiptLifecycleEvent> AppendAsync(
        DecisionReceiptLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a lifecycle event by its stable event identifier.
    /// </summary>
    ValueTask<DecisionReceiptLifecycleEvent?> FindByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds lifecycle events by correlation identifier.
    /// </summary>
    ValueTask<IReadOnlyList<DecisionReceiptLifecycleEvent>> FindByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds lifecycle events by decision receipt identifier.
    /// </summary>
    ValueTask<IReadOnlyList<DecisionReceiptLifecycleEvent>> FindByDecisionReceiptIdAsync(
        string decisionReceiptId,
        CancellationToken cancellationToken = default);
}
