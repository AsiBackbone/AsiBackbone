namespace AsiBackbone.Core.Entities;

/// <summary>
/// Provides a base implementation for ASI Backbone entities.
/// </summary>
public abstract class GovernanceEntity : IGovernanceEntity, IConcurrencyTrackedEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the entity.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the concurrency token used to detect conflicting updates.
    /// </summary>
    public string ConcurrencyStamp { get; set; } = NewConcurrencyStamp();

    /// <summary>
    /// Creates a new concurrency stamp value.
    /// </summary>
    /// <returns>A new normalized concurrency stamp value.</returns>
    public static string NewConcurrencyStamp()
    {
        return Guid.NewGuid().ToString("N");
    }
}
