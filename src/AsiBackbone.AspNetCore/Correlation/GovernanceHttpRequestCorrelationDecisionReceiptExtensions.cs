using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Decisions;

namespace AsiBackbone.AspNetCore.Correlation;

/// <summary>
/// Provides helpers for applying ASP.NET Core request correlation data to framework-neutral decision receipt.
/// </summary>
public static class GovernanceHttpRequestCorrelationDecisionReceiptExtensions
{
    /// <summary>
    /// Creates decision receipt from a governance decision and enriches it with safe ASP.NET Core request correlation data.
    /// </summary>
    /// <param name="correlation">The resolved ASP.NET Core request correlation data.</param>
    /// <param name="actor">The actor associated with the operation.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="decision">The governance decision to audit.</param>
    /// <param name="eventId">Optional audit event identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional event timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="metadata">Optional host-provided audit metadata to merge with safe request metadata.</param>
    /// <returns>An enriched decision receipt value.</returns>
    public static DecisionReceipt CreateDecisionReceipt(
        this GovernanceHttpRequestCorrelation correlation,
        IGovernanceActorContext actor,
        string operationName,
        GovernanceDecision decision,
        string? eventId = null,
        DateTimeOffset? occurredUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(decision);

        return DecisionReceiptBuilder.FromDecision(actor, operationName, decision)
            .WithEventId(eventId)
            .WithOccurredUtc(occurredUtc)
            .WithCorrelationId(correlation.CorrelationId ?? decision.CorrelationId)
            .WithTraceId(correlation.TraceId ?? decision.TraceId)
            .WithMetadata(correlation.MergeMetadata(metadata))
            .Build();
    }
}
