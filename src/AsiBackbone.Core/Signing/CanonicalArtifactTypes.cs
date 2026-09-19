namespace AsiBackbone.Core.Signing;

/// <summary>
/// Provides stable artifact type identifiers for canonical AsiBackbone signing payloads.
/// </summary>
public static class CanonicalArtifactTypes
{
    /// <summary>
    /// Identifies a framework-neutral decision receipt payload.
    /// </summary>
    public const string AuditResidue = "asibackbone.audit-residue";

    /// <summary>
    /// Identifies a persistence-ready audit ledger record payload.
    /// </summary>
    public const string AuditLedgerRecord = "asibackbone.audit-ledger-record";

    /// <summary>
    /// Identifies an decision receipt lifecycle event payload.
    /// </summary>
    public const string AuditResidueLifecycleEvent = "asibackbone.audit-residue-lifecycle-event";

    /// <summary>
    /// Identifies a provider-neutral governance emission envelope payload.
    /// </summary>
    public const string GovernanceEmissionEnvelope = "asibackbone.governance-emission-envelope";

    /// <summary>
    /// Identifies a durable outbox entry payload.
    /// </summary>
    public const string GovernanceOutboxEntry = "asibackbone.governance-outbox-entry";

    /// <summary>
    /// Identifies a provider-neutral capability-token grant payload.
    /// </summary>
    public const string CapabilityTokenGrant = "asibackbone.capability-token-grant";

    /// <summary>
    /// Identifies a provider-neutral audit integrity chain link payload.
    /// </summary>
    public const string AuditIntegrityLink = "asibackbone.audit-integrity-link";

    /// <summary>
    /// Identifies a framework-neutral governed operation execution receipt payload.
    /// </summary>
    public const string GovernedOperationExecutionReceipt = "asibackbone.governed-operation-execution-receipt";
}
