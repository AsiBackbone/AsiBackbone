using AsiBackbone.Core.Emissions;

namespace AsiBackbone.OpenTelemetry;

/// <summary>
/// Maps provider-neutral governance emission categories and provider statuses to stable OpenTelemetry event names.
/// </summary>
internal static class OpenTelemetryGovernanceEventNameMapper
{
    private static readonly Dictionary<GovernanceEmissionEventType, string> EventNames =
        new()
        {
            [GovernanceEmissionEventType.Decision] = OpenTelemetryGovernanceInstrumentation.DecisionEvaluatedEventName,
            [GovernanceEmissionEventType.Acknowledgment] = OpenTelemetryGovernanceInstrumentation.AcknowledgmentRecordedEventName,
            [GovernanceEmissionEventType.CapabilityToken] = OpenTelemetryGovernanceInstrumentation.CapabilityTokenIssuedEventName,
            [GovernanceEmissionEventType.Gateway] = OpenTelemetryGovernanceInstrumentation.GatewayCompletedEventName,
            [GovernanceEmissionEventType.DecisionReceipt] = OpenTelemetryGovernanceInstrumentation.DecisionReceiptCreatedEventName,
            [GovernanceEmissionEventType.AuditLifecycle] = OpenTelemetryGovernanceInstrumentation.LifecycleRecordedEventName,
            [GovernanceEmissionEventType.Outbox] = OpenTelemetryGovernanceInstrumentation.OutboxUpdatedEventName
        };

    private static readonly string[] FailureStatusMarkers = ["fail", "dead", "retry"];

    internal static string GetEventName(
        GovernanceEmissionEventType eventType,
        string? emitterStatus)
    {
        return eventType is GovernanceEmissionEventType.ProviderEmission
            ? IsFailureStatus(emitterStatus)
                ? OpenTelemetryGovernanceInstrumentation.EmissionFailedEventName
                : OpenTelemetryGovernanceInstrumentation.EmissionDeliveredEventName
            : EventNames.TryGetValue(eventType, out string? eventName)
            ? eventName
            : OpenTelemetryGovernanceInstrumentation.GenericGovernanceEventName;
    }

    private static bool IsFailureStatus(string? status)
    {
        return status is not null
            && FailureStatusMarkers.Any(marker => status.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
