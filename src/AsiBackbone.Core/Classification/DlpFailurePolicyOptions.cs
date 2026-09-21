namespace AsiBackbone.Core.Classification;

/// <summary>
/// Defines provider-neutral policy options for resolving DLP or classification failure behavior.
/// </summary>
public sealed class DlpFailurePolicyOptions
{
    /// <summary>
    /// Gets or sets the default behavior for low-risk intents.
    /// </summary>
    public DlpFailureBehavior LowRiskBehavior { get; set; } = DlpFailureBehavior.WarnAndAllow;

    /// <summary>
    /// Gets or sets the default behavior for medium-risk intents.
    /// </summary>
    public DlpFailureBehavior MediumRiskBehavior { get; set; } = DlpFailureBehavior.RequireAcknowledgment;

    /// <summary>
    /// Gets or sets the default behavior for high-risk or regulated intents.
    /// </summary>
    public DlpFailureBehavior HighRiskBehavior { get; set; } = DlpFailureBehavior.Deny;

    /// <summary>
    /// Gets risk- and failure-specific behavior overrides.
    /// </summary>
    public IDictionary<DlpFailurePolicyKey, DlpFailureBehavior> BehaviorOverrides { get; } =
        new Dictionary<DlpFailurePolicyKey, DlpFailureBehavior>();

    /// <summary>
    /// Resolves the configured behavior for the supplied failure context.
    /// </summary>
    /// <param name="context">The DLP failure policy context.</param>
    /// <returns>The configured behavior.</returns>
    public DlpFailureBehavior GetBehavior(DlpFailurePolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var key = new DlpFailurePolicyKey(context.RiskLevel, context.FailureKind);

        if (BehaviorOverrides.TryGetValue(key, out DlpFailureBehavior overrideBehavior))
        {
            return ValidateBehavior(overrideBehavior, nameof(BehaviorOverrides));
        }

        DlpFailureBehavior behavior = context.RiskLevel switch
        {
            DlpIntentRiskLevel.Low => LowRiskBehavior,
            DlpIntentRiskLevel.Medium => MediumRiskBehavior,
            DlpIntentRiskLevel.High => HighRiskBehavior,
            // DlpFailurePolicyContext rejects an unspecified risk level at construction, so this arm is unreachable through
            // a validly constructed context. It stays explicit so an unassigned tier can never fall through to a default.
            DlpIntentRiskLevel.Unspecified => throw new ArgumentOutOfRangeException(nameof(context), context.RiskLevel, "DLP intent risk level must be assigned before a behavior can be resolved."),
            _ => throw new ArgumentOutOfRangeException(nameof(context), context.RiskLevel, "DLP intent risk level must be defined.")
        };

        return ValidateBehavior(behavior, nameof(context));
    }

    /// <summary>
    /// Validates a resolved or configured failure behavior.
    /// </summary>
    /// <remarks>
    /// This is the single choke point for both the risk-tier properties and <see cref="BehaviorOverrides" />, so rejecting
    /// <see cref="DlpFailureBehavior.Unspecified" /> here keeps a partially configured policy from resolving a screening
    /// failure into an allow. An incomplete policy raises instead of quietly proceeding.
    /// </remarks>
    private static DlpFailureBehavior ValidateBehavior(
        DlpFailureBehavior behavior,
        string parameterName)
    {
        return !Enum.IsDefined(behavior)
            ? throw new ArgumentOutOfRangeException(parameterName, behavior, "DLP failure behavior must be defined.")
            : behavior == DlpFailureBehavior.Unspecified
                ? throw new ArgumentOutOfRangeException(
                    parameterName,
                    behavior,
                    "DLP failure behavior must be configured. Set an explicit behavior rather than leaving it unspecified.")
                : behavior;
    }
}
