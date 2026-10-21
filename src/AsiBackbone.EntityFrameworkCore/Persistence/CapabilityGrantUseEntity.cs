using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Entities;

namespace AsiBackbone.EntityFrameworkCore.Persistence;

/// <summary>Represents the durable, issuer-scoped use state for a capability grant.</summary>
public sealed class CapabilityGrantUseEntity : GovernanceEntity
{
    /// <summary>Gets or sets the grant issuer.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Gets or sets the stable grant token identifier.</summary>
    public string TokenId { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of accepted uses.</summary>
    public int UseCount { get; set; }

    /// <summary>Gets or sets the current use-control state.</summary>
    public GrantUseState State { get; set; } = GrantUseState.Accepted;

    /// <summary>Gets or sets the most recent accepted-use timestamp.</summary>
    public DateTimeOffset LastUsedUtc { get; set; }

    /// <summary>Gets or sets the earliest time at which the record may be purged.</summary>
    public DateTimeOffset RetainUntilUtc { get; set; }
}
