namespace AsiBackbone.EntityFrameworkCore.CapabilityGrants;

/// <summary>Configures durable capability-grant use storage.</summary>
public sealed class EfCoreCapabilityGrantUseStoreOptions
{
    /// <summary>Gets or sets the retention period after grant expiration.</summary>
    /// <remarks>Set this to at least the greatest clock skew used by any validator sharing this store.</remarks>
    public TimeSpan RetentionGracePeriod { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the number of retries for insert/update contention.</summary>
    public int ContentionRetryCount { get; set; } = 5;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(RetentionGracePeriod, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(ContentionRetryCount, 0);
    }
}
