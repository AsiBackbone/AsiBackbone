using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Emissions;

/// <summary>
/// Provider-neutral no-op governance emitter for tests, samples, local validation, and outbox proof-of-composition flows.
/// </summary>
/// <remarks>
/// This emitter does not send data to an external provider. It acknowledges the envelope as delivered so hosts can validate outbox drain behavior before wiring a real provider such as OpenTelemetry.
/// </remarks>
public sealed class NoOpGovernanceEmitter : IGovernanceEmitter
{
    /// <summary>
    /// Gets the provider name used by the no-op emitter.
    /// </summary>
    public const string ProviderName = "noop";

    private static readonly IReadOnlyDictionary<string, string> DeliveredMetadata = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["emitter.kind"] = "noop",
            ["emitter.purpose"] = "outbox-drain-validation"
        });

    /// <summary>
    /// Gets a reusable no-op governance emitter instance.
    /// </summary>
    public static NoOpGovernanceEmitter Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<GovernanceEmissionResult> EmitAsync(
        GovernanceEmissionEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var result = GovernanceEmissionResult.DeliveredFromNormalizedMetadata(
            ProviderName,
            providerRecordId: envelope.EnvelopeId,
            DeliveredMetadata);

        return ValueTask.FromResult(result);
    }
}
