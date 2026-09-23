using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.HostIntegration;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Maps enum values used by canonical signed payloads to their stable v1 wire names.
/// </summary>
/// <remarks>
/// These strings are signature protocol constants. They intentionally do not depend on CLR member names so a source
/// rename cannot silently change canonical bytes. Existing values preserve the names emitted by AsiBackbone 6.0.
/// </remarks>
internal static class CanonicalEnumWireNames
{
    public static string ForActorType(GovernanceActorType value)
    {
        return value switch
        {
            GovernanceActorType.Unknown => "Unknown",
            GovernanceActorType.Human => "Human",
            GovernanceActorType.System => "System",
            GovernanceActorType.Service => "Service",
            GovernanceActorType.Agent => "Agent",
            _ => ThrowUndefined(value)
        };
    }

    public static string ForLifecycleStage(DecisionReceiptLifecycleStage value)
    {
        return value switch
        {
            DecisionReceiptLifecycleStage.DecisionEvaluated => "DecisionEvaluated",
            DecisionReceiptLifecycleStage.AcknowledgmentRequested => "AcknowledgmentRequested",
            DecisionReceiptLifecycleStage.AcknowledgmentCompleted => "AcknowledgmentCompleted",
            DecisionReceiptLifecycleStage.CapabilityTokenIssued => "CapabilityTokenIssued",
            DecisionReceiptLifecycleStage.GatewayExecutionStarted => "GatewayExecutionStarted",
            DecisionReceiptLifecycleStage.GatewayExecutionCompleted => "GatewayExecutionCompleted",
            DecisionReceiptLifecycleStage.GatewayExecutionDenied => "GatewayExecutionDenied",
            DecisionReceiptLifecycleStage.ExternalEmissionQueued => "ExternalEmissionQueued",
            DecisionReceiptLifecycleStage.ExternalEmissionDelivered => "ExternalEmissionDelivered",
            DecisionReceiptLifecycleStage.ExternalEmissionFailed => "ExternalEmissionFailed",
            DecisionReceiptLifecycleStage.ExternalEmissionDeadLettered => "ExternalEmissionDeadLettered",
            _ => ThrowUndefined(value)
        };
    }

    public static string ForDecisionOutcome(GovernanceDecisionOutcome value)
    {
        return value switch
        {
            GovernanceDecisionOutcome.Allowed => "Allowed",
            GovernanceDecisionOutcome.Warning => "Warning",
            GovernanceDecisionOutcome.Denied => "Denied",
            GovernanceDecisionOutcome.Deferred => "Deferred",
            GovernanceDecisionOutcome.AcknowledgmentRequired => "AcknowledgmentRequired",
            GovernanceDecisionOutcome.EscalationRecommended => "EscalationRecommended",
            _ => ThrowUndefined(value)
        };
    }

    public static string ForConstraintOutcome(ConstraintEvaluationOutcome value)
    {
        return value switch
        {
            ConstraintEvaluationOutcome.NotApplicable => "NotApplicable",
            ConstraintEvaluationOutcome.Allowed => "Allowed",
            ConstraintEvaluationOutcome.Warning => "Warning",
            ConstraintEvaluationOutcome.Denied => "Denied",
            _ => ThrowUndefined(value)
        };
    }

    public static string ForEmissionEventType(GovernanceEmissionEventType value)
    {
        return value switch
        {
            GovernanceEmissionEventType.Decision => "Decision",
            GovernanceEmissionEventType.Acknowledgment => "Acknowledgment",
            GovernanceEmissionEventType.CapabilityToken => "CapabilityToken",
            GovernanceEmissionEventType.Gateway => "Gateway",
            GovernanceEmissionEventType.DecisionReceipt => "AuditResidue",
            GovernanceEmissionEventType.AuditLifecycle => "AuditLifecycle",
            GovernanceEmissionEventType.Outbox => "Outbox",
            GovernanceEmissionEventType.ProviderEmission => "ProviderEmission",
            _ => ThrowUndefined(value)
        };
    }

    public static string ForEmissionStatus(GovernanceEmissionStatus value)
    {
        return value switch
        {
            GovernanceEmissionStatus.Pending => "Pending",
            GovernanceEmissionStatus.Delivered => "Delivered",
            GovernanceEmissionStatus.Deferred => "Deferred",
            GovernanceEmissionStatus.Failed => "Failed",
            GovernanceEmissionStatus.RetryableFailure => "RetryableFailure",
            GovernanceEmissionStatus.DeadLettered => "DeadLettered",
            _ => ThrowUndefined(value)
        };
    }

    public static string ForPersistenceOutcome(GovernedOperationPersistenceOutcome value)
    {
        return value switch
        {
            GovernedOperationPersistenceOutcome.Committed => "Committed",
            GovernedOperationPersistenceOutcome.Failed => "Failed",
            GovernedOperationPersistenceOutcome.RolledBack => "RolledBack",
            GovernedOperationPersistenceOutcome.CompletedWithoutMutation => "CompletedWithoutMutation",
            _ => ThrowUndefined(value)
        };
    }

    private static string ThrowUndefined<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        throw new ArgumentOutOfRangeException(nameof(value), value, "Canonical enum wire values must be explicitly defined.");
    }
}
