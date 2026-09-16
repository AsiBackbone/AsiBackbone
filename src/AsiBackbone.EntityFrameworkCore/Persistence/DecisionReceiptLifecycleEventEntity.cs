using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Entities;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>
/// Represents the Entity Framework Core persistence shape for an decision receipt lifecycle event.
/// </summary>
public sealed class DecisionReceiptLifecycleEventEntity : GovernanceEntity
{
    /// <summary>
    /// Gets or sets the stable lifecycle event identifier.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lifecycle stage represented by this event.
    /// </summary>
    public DecisionReceiptLifecycleStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the stable lifecycle stage sequence value.
    /// </summary>
    public int StageSequence { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the lifecycle event occurred.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier that links the lifecycle event to the original decision context.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the related decision receipt identifier, when available.
    /// </summary>
    public string? AuditResidueId { get; set; }

    /// <summary>
    /// Gets or sets the trace identifier associated with the lifecycle event, when available.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the operation name associated with the lifecycle event, when available.
    /// </summary>
    public string? OperationName { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle outcome associated with the event, when available.
    /// </summary>
    public string? Outcome { get; set; }

    /// <summary>
    /// Gets or sets serialized framework-neutral lifecycle metadata supplied by the host.
    /// </summary>
    public string MetadataJson { get; set; } = "{}";
}
