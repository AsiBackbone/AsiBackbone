using System.Collections.ObjectModel;
using AsiBackbone.Core.Audit;

namespace AsiBackbone.Core.HostIntegration;

/// <summary>
/// Creates append-only audit lifecycle events that bind a governed decision to host execution evidence.
/// </summary>
public static class HostAccountabilityLifecycleEvent
{
    /// <summary>
    /// Creates a gateway-execution-started lifecycle event for one logical operation and attempt.
    /// </summary>
    public static DecisionReceiptLifecycleEvent ExecutionStarted(
        IDecisionReceipt residue,
        string operationExecutionId,
        string? executionAttemptId = null,
        string? decisionAuditRecordId = null,
        string? eventId = null,
        DateTimeOffset? occurredUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(residue);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationExecutionId);

        SortedDictionary<string, string> lifecycleMetadata = new(StringComparer.Ordinal);
        AddMetadata(lifecycleMetadata, metadata);
        lifecycleMetadata[HostAccountabilityMetadataKeys.OperationExecutionId] = operationExecutionId.Trim();
        AddOptional(lifecycleMetadata, HostAccountabilityMetadataKeys.ExecutionAttemptId, executionAttemptId);
        AddOptional(lifecycleMetadata, HostAccountabilityMetadataKeys.DecisionAuditRecordId, decisionAuditRecordId);

        return DecisionReceiptLifecycleEvent.FromResidue(
            DecisionReceiptLifecycleStage.GatewayExecutionStarted,
            residue,
            eventId: eventId,
            occurredUtc: occurredUtc,
            outcome: "Started",
            metadata: new ReadOnlyDictionary<string, string>(lifecycleMetadata));
    }

    /// <summary>
    /// Creates a gateway-execution-completed lifecycle event from a typed host execution receipt.
    /// </summary>
    /// <remarks>
    /// The existing completion stage remains stable. Committed, failed, rolled-back, and no-mutation
    /// distinctions are carried by the typed receipt outcome and stable metadata keys rather than by
    /// expanding the lifecycle-stage enum.
    /// </remarks>
    public static DecisionReceiptLifecycleEvent FromExecutionReceipt(
        IDecisionReceipt residue,
        GovernedOperationExecutionReceipt receipt,
        string? eventId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(residue);
        ArgumentNullException.ThrowIfNull(receipt);

        return DecisionReceiptLifecycleEvent.FromResidue(
            DecisionReceiptLifecycleStage.GatewayExecutionCompleted,
            residue,
            eventId: eventId,
            occurredUtc: receipt.CompletedUtc,
            outcome: receipt.PersistenceOutcome.ToString(),
            metadata: receipt.ToLifecycleMetadata(metadata));
    }

    private static void AddMetadata(SortedDictionary<string, string> destination, IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return;
        }

        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
            {
                destination[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
            }
        }
    }

    private static void AddOptional(SortedDictionary<string, string> destination, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            destination[key] = value.Trim();
        }
    }
}
