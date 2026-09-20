namespace AsiBackbone.Core.Classification;

/// <summary>
/// Represents the host-assigned risk level for an intent that depends on DLP or classification screening.
/// </summary>
public enum DlpIntentRiskLevel
{
    /// <summary>
    /// No risk level was assigned. This is the default value and is never a usable policy input.
    /// </summary>
    /// <remarks>
    /// The default value occupies the zero slot deliberately so that an unset field, an absent configuration value, or a
    /// deserialized payload that omitted the risk level cannot silently resolve to the most permissive tier. Supplying it
    /// to <see cref="DlpFailurePolicyContext" /> is rejected rather than defaulted.
    /// </remarks>
    Unspecified = 0,

    /// <summary>
    /// The intent is low risk and may be allowed with warning when screening is unavailable, depending on policy.
    /// </summary>
    Low = 1,

    /// <summary>
    /// The intent is medium risk and may require acknowledgment, deferral, or escalation when screening is unavailable, depending on policy.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// The intent is high risk, regulated, or consequential enough to fail closed or escalate when screening is unavailable, depending on policy.
    /// </summary>
    High = 3
}
