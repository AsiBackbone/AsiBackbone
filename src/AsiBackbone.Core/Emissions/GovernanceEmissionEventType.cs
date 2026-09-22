namespace AsiBackbone.Core.Emissions;

/// <summary>
/// Defines provider-neutral governance event categories that can be emitted through an AsiBackbone emission contract.
/// </summary>
/// <remarks>
/// The values are category-level on purpose. Provider-specific adapters may map these values into backend-specific event names without changing Core semantics.
/// </remarks>
public enum GovernanceEmissionEventType
{
    /// <summary>
    /// A governance decision was evaluated.
    /// </summary>
    Decision = 100,

    /// <summary>
    /// An acknowledgment or responsibility-handshake event occurred.
    /// </summary>
    Acknowledgment = 200,

    /// <summary>
    /// A capability grant event occurred.
    /// </summary>
    CapabilityToken = 300,

    /// <summary>
    /// A gateway execution boundary event occurred.
    /// </summary>
    Gateway = 400,

    /// <summary>
    /// Decision receipt or an audit ledger record is being emitted.
    /// </summary>
    DecisionReceipt = 500,

    /// <summary>
    /// An decision receipt lifecycle event is being emitted.
    /// </summary>
    AuditLifecycle = 510,

    /// <summary>
    /// A durable outbox event or handoff is being emitted.
    /// </summary>
    Outbox = 600,

    /// <summary>
    /// A provider emission event, status change, or delivery outcome is being emitted.
    /// </summary>
    ProviderEmission = 700
}
