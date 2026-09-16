using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Audit;

/// <summary>
/// Represents a framework-neutral lifecycle event linked to a governed decision receipt flow.
/// </summary>
/// <remarks>
/// Lifecycle events are append-only progress records. They allow acknowledgment, capability token, gateway, outbox, and provider delivery activity to be recorded without rewriting the original decision residue.
/// </remarks>
public sealed class DecisionReceiptLifecycleEvent
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private DecisionReceiptLifecycleEvent(
        string eventId,
        DecisionReceiptLifecycleStage stage,
        DateTimeOffset occurredUtc,
        string correlationId,
        string? auditResidueId,
        string? traceId,
        string? operationName,
        string? outcome,
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "Lifecycle stage must be a defined decision receipt lifecycle stage.");
        }

        EventId = eventId.Trim();
        Stage = stage;
        OccurredUtc = occurredUtc.ToUniversalTime();
        CorrelationId = correlationId.Trim();
        AuditResidueId = NormalizeOptional(auditResidueId);
        TraceId = NormalizeOptional(traceId);
        OperationName = NormalizeOptional(operationName);
        Outcome = NormalizeOptional(outcome);
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the stable identifier for this lifecycle event.
    /// </summary>
    public string EventId { get; }

    /// <summary>
    /// Gets the lifecycle stage represented by this event.
    /// </summary>
    public DecisionReceiptLifecycleStage Stage { get; }

    /// <summary>
    /// Gets the stable sequence value for this lifecycle stage.
    /// </summary>
    public int StageSequence => (int)Stage;

    /// <summary>
    /// Gets the UTC timestamp when the lifecycle event occurred.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; }

    /// <summary>
    /// Gets the correlation identifier that links this lifecycle event to the original decision context.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets the related decision receipt identifier when the original decision residue is available.
    /// </summary>
    public string? AuditResidueId { get; }

    /// <summary>
    /// Gets the trace identifier associated with the lifecycle event, when supplied by the host or original residue.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// Gets the operation name associated with the lifecycle event, when supplied by the host or original residue.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    /// Gets the decision, gateway, emission, or host-defined outcome associated with this lifecycle event, when supplied.
    /// </summary>
    public string? Outcome { get; }

    /// <summary>
    /// Gets additional framework-neutral lifecycle metadata supplied by the host.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether this lifecycle event is linked to an decision receipt identifier.
    /// </summary>
    public bool HasAuditResidueId => AuditResidueId is not null;

    /// <summary>
    /// Gets a value indicating whether this lifecycle event contains metadata.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Creates an decision receipt lifecycle event.
    /// </summary>
    /// <param name="stage">The lifecycle stage represented by this event.</param>
    /// <param name="correlationId">The correlation identifier linking the event to the original decision context.</param>
    /// <param name="auditResidueId">Optional decision receipt identifier when the original decision residue is available.</param>
    /// <param name="eventId">Optional lifecycle event identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional lifecycle timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <param name="operationName">Optional operation name.</param>
    /// <param name="outcome">Optional lifecycle or host-defined outcome.</param>
    /// <param name="metadata">Optional host-provided lifecycle metadata.</param>
    /// <returns>An decision receipt lifecycle event.</returns>
    public static DecisionReceiptLifecycleEvent Create(
        DecisionReceiptLifecycleStage stage,
        string correlationId,
        string? auditResidueId = null,
        string? eventId = null,
        DateTimeOffset? occurredUtc = null,
        string? traceId = null,
        string? operationName = null,
        string? outcome = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new DecisionReceiptLifecycleEvent(
            NormalizeIdentifier(eventId),
            stage,
            occurredUtc ?? DateTimeOffset.UtcNow,
            correlationId,
            auditResidueId,
            traceId,
            operationName,
            outcome,
            NormalizeMetadata(metadata));
    }

    /// <summary>
    /// Creates an decision receipt lifecycle event by copying correlation context from existing decision receipt.
    /// </summary>
    /// <param name="stage">The lifecycle stage represented by this event.</param>
    /// <param name="residue">The original decision receipt to correlate with the lifecycle event.</param>
    /// <param name="correlationId">Optional correlation identifier override. When omitted, the residue correlation identifier is used.</param>
    /// <param name="auditResidueId">Optional decision receipt identifier override. When omitted, the residue event identifier is used.</param>
    /// <param name="eventId">Optional lifecycle event identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional lifecycle timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="outcome">Optional lifecycle or host-defined outcome. When omitted, the residue outcome is used.</param>
    /// <param name="metadata">Optional host-provided lifecycle metadata merged after residue metadata.</param>
    /// <returns>An decision receipt lifecycle event.</returns>
    public static DecisionReceiptLifecycleEvent FromResidue(
        DecisionReceiptLifecycleStage stage,
        IDecisionReceipt residue,
        string? correlationId = null,
        string? auditResidueId = null,
        string? eventId = null,
        DateTimeOffset? occurredUtc = null,
        string? outcome = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(residue);

        string? effectiveCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? residue.CorrelationId
            : correlationId;

        return new DecisionReceiptLifecycleEvent(
            NormalizeIdentifier(eventId),
            stage,
            occurredUtc ?? DateTimeOffset.UtcNow,
            effectiveCorrelationId ?? throw new ArgumentException("A lifecycle event requires a correlation identifier from the residue or an explicit override.", nameof(correlationId)),
            string.IsNullOrWhiteSpace(auditResidueId) ? residue.EventId : auditResidueId,
            residue.TraceId,
            residue.OperationName,
            string.IsNullOrWhiteSpace(outcome) ? residue.Outcome : outcome,
            NormalizeMetadata(residue.Metadata, metadata));
    }

    private static string NormalizeIdentifier(string? identifier)
    {
        return string.IsNullOrWhiteSpace(identifier)
            ? Guid.NewGuid().ToString("N")
            : identifier.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        params IReadOnlyDictionary<string, string>?[] metadataSets)
    {
        Dictionary<string, string> normalizedMetadata = new(StringComparer.Ordinal);

        foreach (IReadOnlyDictionary<string, string>? metadata in metadataSets)
        {
            if (metadata is null || metadata.Count == 0)
            {
                continue;
            }

            foreach (KeyValuePair<string, string> item in metadata)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                normalizedMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
            }
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }
}
