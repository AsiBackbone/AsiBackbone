using AsiBackbone.Core.Audit;
using AsiBackbone.Core.HostIntegration;

namespace AsiBackbone.Samples.NcatAuditCompletionAdapter;

/// <summary>
/// Identifies how the NCAT source dispatcher should treat one completion handoff attempt.
/// </summary>
public enum NcatAuditCompletionDeliveryDisposition
{
    /// <summary>The lifecycle event was durably appended and the source entry may be acknowledged.</summary>
    Delivered = 100,

    /// <summary>An equivalent lifecycle event already exists and the source entry may be acknowledged.</summary>
    Duplicate = 200,

    /// <summary>The handoff should be attempted again because lifecycle persistence did not complete.</summary>
    Retryable = 300,

    /// <summary>The handoff is valid but required decision evidence is not available yet.</summary>
    Deferred = 400,

    /// <summary>The handoff is invalid or conflicts with previously persisted idempotency evidence.</summary>
    Terminal = 500,

    /// <summary>The configured retry threshold was reached without a successful lifecycle append.</summary>
    DeadLetter = 600
}

/// <summary>
/// Reports the normalized result of one NCAT audit-completion handoff attempt.
/// </summary>
public sealed record NcatAuditCompletionDeliveryResult(
    NcatAuditCompletionDeliveryDisposition Disposition,
    string ReasonCode,
    string? LifecycleEventId = null,
    GovernedOperationExecutionReceipt? Receipt = null,
    DecisionReceiptLifecycleEvent? LifecycleEvent = null,
    string? FailureType = null)
{
    /// <summary>
    /// Gets a value indicating whether the source completion entry may be marked delivered.
    /// </summary>
    public bool ShouldAcknowledgeSource =>
        Disposition is NcatAuditCompletionDeliveryDisposition.Delivered or NcatAuditCompletionDeliveryDisposition.Duplicate;
}
