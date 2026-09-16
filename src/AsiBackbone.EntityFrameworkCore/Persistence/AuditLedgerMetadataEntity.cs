using AsiBackbone.Core.Entities;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>
/// Represents a normalized metadata row associated with an AsiBackbone audit ledger record.
/// </summary>
public sealed class AuditLedgerMetadataEntity : GovernanceEntity
{
    /// <summary>
    /// Gets or sets the identifier of the parent audit ledger record.
    /// </summary>
    public Guid AuditLedgerRecordId { get; set; }

    /// <summary>
    /// Gets or sets the metadata key.
    /// </summary>
    public string MetadataKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the metadata value.
    /// </summary>
    public string MetadataValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent audit ledger record.
    /// </summary>
    public AuditLedgerRecordEntity AuditLedgerRecord { get; set; } = null!;
}
