namespace AsiBackbone.Core.Entities;

/// <summary>
/// Defines the minimum identity contract for ASI Backbone entities.
/// </summary>
public interface IGovernanceEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the entity.
    /// </summary>
    Guid Id { get; set; }
}
