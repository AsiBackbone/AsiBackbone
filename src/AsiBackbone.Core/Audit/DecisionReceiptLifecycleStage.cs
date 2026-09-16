namespace AsiBackbone.Core.Audit;

/// <summary>
/// Defines framework-neutral lifecycle stages for decision receipt emitted across a governed decision flow.
/// </summary>
/// <remarks>
/// The numeric values are intentionally spaced so later additive stages can be inserted without renumbering the stable sequence.
/// </remarks>
public enum DecisionReceiptLifecycleStage
{
    /// <summary>
    /// The governance decision was evaluated.
    /// </summary>
    DecisionEvaluated = 100,

    /// <summary>
    /// Acknowledgment was requested before the operation could continue.
    /// </summary>
    AcknowledgmentRequested = 200,

    /// <summary>
    /// The requested acknowledgment was completed.
    /// </summary>
    AcknowledgmentCompleted = 210,

    /// <summary>
    /// A scoped capability token was issued for a permitted operation.
    /// </summary>
    CapabilityTokenIssued = 300,

    /// <summary>
    /// Gateway execution started for a governed operation.
    /// </summary>
    GatewayExecutionStarted = 400,

    /// <summary>
    /// Gateway execution completed for a governed operation.
    /// </summary>
    GatewayExecutionCompleted = 410,

    /// <summary>
    /// Gateway execution was denied before the operation completed.
    /// </summary>
    GatewayExecutionDenied = 420,

    /// <summary>
    /// External provider emission was queued for downstream delivery.
    /// </summary>
    ExternalEmissionQueued = 500,

    /// <summary>
    /// External provider emission was delivered successfully.
    /// </summary>
    ExternalEmissionDelivered = 510,

    /// <summary>
    /// External provider emission failed and may require retry, escalation, or host handling.
    /// </summary>
    ExternalEmissionFailed = 520,

    /// <summary>
    /// External provider emission was dead-lettered after terminal failure or policy-based quarantine.
    /// </summary>
    ExternalEmissionDeadLettered = 530
}
