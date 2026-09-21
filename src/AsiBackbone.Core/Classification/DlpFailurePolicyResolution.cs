using System.Collections.ObjectModel;
using System.Globalization;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Results;

namespace AsiBackbone.Core.Classification;

/// <summary>
/// Represents the resolved provider-neutral policy response to a DLP or classification failure.
/// </summary>
public sealed class DlpFailurePolicyResolution
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private DlpFailurePolicyResolution(
        DlpFailurePolicyContext context,
        DlpFailureBehavior behavior,
        OperationReason reason,
        GovernanceDecision decision)
    {
        Context = context;
        Behavior = behavior;
        Reason = reason;
        Decision = decision;
    }

    /// <summary>
    /// Gets the original failure context.
    /// </summary>
    public DlpFailurePolicyContext Context { get; }

    /// <summary>
    /// Gets the resolved provider-neutral failure behavior.
    /// </summary>
    public DlpFailureBehavior Behavior { get; }

    /// <summary>
    /// Gets the machine-readable reason associated with the failure behavior.
    /// </summary>
    public OperationReason Reason { get; }

    /// <summary>
    /// Gets the governance decision produced by the resolved behavior.
    /// </summary>
    public GovernanceDecision Decision { get; }

    /// <summary>
    /// Gets a value indicating whether the resolved decision allows immediate execution.
    /// </summary>
    public bool CanProceed => Decision.CanProceed;

    /// <summary>
    /// Gets a value indicating whether the resolution uses a fail-open behavior.
    /// </summary>
    public bool IsFailOpen => Behavior is DlpFailureBehavior.Allow or DlpFailureBehavior.WarnAndAllow;

    /// <summary>
    /// Gets a value indicating whether the resolution uses a fail-closed behavior.
    /// </summary>
    public bool IsFailClosed => Behavior is DlpFailureBehavior.Deny;

    /// <summary>
    /// Creates a policy resolution for the supplied context and behavior.
    /// </summary>
    /// <param name="context">The original DLP or classification failure context.</param>
    /// <param name="behavior">The resolved failure behavior.</param>
    /// <returns>The policy resolution.</returns>
    public static DlpFailurePolicyResolution Create(
        DlpFailurePolicyContext context,
        DlpFailureBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!Enum.IsDefined(behavior))
        {
            throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "DLP failure behavior must be defined.");
        }

        // A resolution carries the governance decision a host acts on, so an unconfigured behavior cannot be materialized
        // into one. Rejecting it here rather than in the switch below names the actual problem instead of reporting the
        // value as merely undefined.
        if (behavior == DlpFailureBehavior.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(behavior),
                behavior,
                "DLP failure behavior must be resolved before a policy resolution is created. Unspecified is not an actionable outcome.");
        }

        var reason = OperationReason.Create(
            DlpFailureReasonCodes.GetFor(context.FailureKind),
            BuildMessage(context),
            BuildReasonMetadata(context, behavior));

        GovernanceDecision decision = behavior switch
        {
            DlpFailureBehavior.Allow => GovernanceDecision.Allow(
                correlationId: context.CorrelationId,
                traceId: context.TraceId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash),
            DlpFailureBehavior.WarnAndAllow => GovernanceDecision.Warning(
                reason,
                correlationId: context.CorrelationId,
                traceId: context.TraceId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash),
            DlpFailureBehavior.Deny => GovernanceDecision.Deny(
                reason,
                correlationId: context.CorrelationId,
                traceId: context.TraceId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash),
            DlpFailureBehavior.Defer => GovernanceDecision.Defer(
                reason.Code,
                reason.Message,
                correlationId: context.CorrelationId,
                traceId: context.TraceId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash),
            DlpFailureBehavior.RequireAcknowledgment => GovernanceDecision.RequireAcknowledgment(
                reason.Code,
                reason.Message,
                correlationId: context.CorrelationId,
                traceId: context.TraceId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash),
            DlpFailureBehavior.Escalate => GovernanceDecision.Escalate(
                reason.Code,
                reason.Message,
                correlationId: context.CorrelationId,
                traceId: context.TraceId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash),
            // Rejected above, so this arm is unreachable. It stays explicit so that an unconfigured behavior can never
            // acquire a governance decision by falling through to a default.
            DlpFailureBehavior.Unspecified => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "DLP failure behavior must be resolved before a policy resolution is created."),
            _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "DLP failure behavior must be defined.")
        };

        return new DlpFailurePolicyResolution(context, behavior, reason, decision);
    }

    private static string BuildMessage(DlpFailurePolicyContext context)
    {
        return context.FailureKind switch
        {
            DlpClassificationFailureKind.ServiceUnavailable => "DLP/classification screening service was unavailable.",
            DlpClassificationFailureKind.Timeout => context.Timeout.HasValue
                ? $"DLP/classification screening timed out after {context.Timeout.Value.TotalMilliseconds:0} ms."
                : "DLP/classification screening timed out.",
            DlpClassificationFailureKind.IndeterminateResult => "DLP/classification screening returned an indeterminate result.",
            DlpClassificationFailureKind.BlockedResult => "DLP/classification screening returned a blocked result.",
            DlpClassificationFailureKind.ClassifiedResult => "DLP/classification screening returned a classified result that requires configured policy handling.",
            _ => throw new ArgumentOutOfRangeException(nameof(context), context.FailureKind, "DLP failure kind must be defined.")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildReasonMetadata(
        DlpFailurePolicyContext context,
        DlpFailureBehavior behavior)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in context.Metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            metadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        metadata["dlp.failure_kind"] = ToMetadataValue(context.FailureKind);
        metadata["dlp.risk_level"] = ToMetadataValue(context.RiskLevel);
        metadata["dlp.behavior"] = ToMetadataValue(behavior);

        if (!string.IsNullOrWhiteSpace(context.IntentCategory))
        {
            metadata["dlp.intent_category"] = context.IntentCategory;
        }

        if (!string.IsNullOrWhiteSpace(context.Environment))
        {
            metadata["dlp.environment"] = context.Environment;
        }

        if (context.Timeout.HasValue)
        {
            metadata["dlp.timeout_ms"] = context.Timeout.Value.TotalMilliseconds
                .ToString("0", CultureInfo.InvariantCulture);
        }

        return metadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(metadata);
    }

    private static string ToMetadataValue<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();

        return string.Concat(
            text.Select((character, index) =>
                index > 0 && char.IsUpper(character)
                    ? "_" + char.ToLowerInvariant(character)
                    : char.ToLowerInvariant(character).ToString()));
    }
}
