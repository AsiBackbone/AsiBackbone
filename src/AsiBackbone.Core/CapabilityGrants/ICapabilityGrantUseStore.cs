namespace AsiBackbone.Core.CapabilityGrants;

/// <summary>
/// Defines a provider-neutral boundary for checking and consuming bounded-use capability grants.
/// </summary>
/// <remarks>
/// Hosts provide the storage implementation. Core does not own durable state, distributed locking, cache consistency,
/// database schema, or replay-window guarantees.
/// </remarks>
public interface ICapabilityGrantUseStore
{
    /// <summary>
    /// Checks whether the grant can be used and consumes one use when accepted.
    /// </summary>
    /// <param name="grant">The capability grant being validated.</param>
    /// <param name="maxUseCount">The maximum allowed use count for the validation context.</param>
    /// <param name="usedUtc">The UTC timestamp for this use attempt.</param>
    /// <param name="cancellationToken">A token used to observe cancellation.</param>
    /// <returns>The use-control result.</returns>
    ValueTask<CapabilityGrantUseResult> TryConsumeAsync(
        CapabilityGrant grant,
        int maxUseCount,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken = default);
}
