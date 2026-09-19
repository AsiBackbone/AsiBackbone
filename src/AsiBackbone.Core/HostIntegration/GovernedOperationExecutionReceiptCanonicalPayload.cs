using System.Globalization;
using AsiBackbone.Core.Serialization;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.HostIntegration;

/// <summary>
/// Builds deterministic signing payloads for governed operation execution receipts.
/// </summary>
public static class GovernedOperationExecutionReceiptCanonicalPayload
{
    /// <summary>
    /// Builds a provider-neutral canonical payload for an execution receipt.
    /// </summary>
    public static CanonicalPayload Create(
        GovernedOperationExecutionReceipt receipt,
        CanonicalPayloadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        CanonicalPayloadOptions effectiveOptions = options ?? CanonicalPayloadOptions.Default;

        SortedDictionary<string, object?> content = new(StringComparer.Ordinal)
        {
            ["completedUtc"] = receipt.CompletedUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture),
            ["completedWithoutMutation"] = receipt.CompletedWithoutMutation,
            ["decisionAuditRecordId"] = receipt.DecisionAuditRecordId,
            ["executionAttemptId"] = receipt.ExecutionAttemptId,
            ["metadata"] = FilterMetadata(receipt.Metadata, effectiveOptions),
            ["mutationBatchId"] = receipt.MutationBatchId,
            ["mutationManifestAlgorithm"] = receipt.MutationManifestAlgorithm,
            ["mutationManifestHash"] = receipt.MutationManifestHash,
            ["mutationRecordCount"] = receipt.MutationRecordCount,
            ["operationExecutionId"] = receipt.OperationExecutionId,
            ["persistenceOutcome"] = receipt.PersistenceOutcome.ToString(),
            ["persistenceProvider"] = receipt.PersistenceProvider
        };

        return CanonicalPayload.Create(
            CanonicalArtifactTypes.GovernedOperationExecutionReceipt,
            BuildArtifactId(receipt),
            GovernanceSchemaVersions.StableArtifactsV1,
            effectiveOptions.CanonicalizationVersion,
            content);
    }

    private static string BuildArtifactId(GovernedOperationExecutionReceipt receipt)
    {
        return receipt.ExecutionAttemptId is null
            ? receipt.OperationExecutionId
            : $"{receipt.OperationExecutionId}:{receipt.ExecutionAttemptId}";
    }

    private static SortedDictionary<string, object?> FilterMetadata(
        IReadOnlyDictionary<string, string> metadata,
        CanonicalPayloadOptions options)
    {
        SortedDictionary<string, object?> filtered = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (options.AllowsMetadataKey(item.Key))
            {
                filtered[item.Key] = item.Value;
            }
        }

        return filtered;
    }
}
