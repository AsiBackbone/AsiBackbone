namespace AsiBackbone.Core.Emissions;

/// <summary>
/// Defines the provider-neutral contract used to emit governance envelopes from Core artifacts, audit/outbox storage, or host adapters.
/// </summary>
/// <remarks>
/// Implementations may write to files, databases, local outbox stores, OpenTelemetry adapters, Azure Monitor, Event Hubs, Purview, SIEM systems, or future providers, but Core does not depend on those provider packages.
/// </remarks>
public interface IGovernanceEmitter
{
    /// <summary>
    /// Emits a provider-neutral governance emission envelope.
    /// </summary>
    /// <param name="envelope">The provider-neutral envelope to emit.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The provider-neutral emission result.</returns>
    ValueTask<GovernanceEmissionResult> EmitAsync(
        GovernanceEmissionEnvelope envelope,
        CancellationToken cancellationToken = default);
}
