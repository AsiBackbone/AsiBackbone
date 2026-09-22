namespace AsiBackbone.Core.ThreatModeling;

/// <summary>
/// Provides conventional threat assessment category names for host-defined contributors.
/// </summary>
/// <remarks>
/// Hosts may supply their own category strings. These constants are intentionally descriptive rather than product claims.
/// </remarks>
public static class ThreatCategories
{
    /// <summary>
    /// Indicates that no threat category applies.
    /// </summary>
    public const string None = "None";

    /// <summary>
    /// Indicates malformed input or request shape.
    /// </summary>
    public const string InputMalformed = "InputMalformed";

    /// <summary>
    /// Indicates input that exceeds host-defined size or shape limits.
    /// </summary>
    public const string InputOversized = "InputOversized";

    /// <summary>
    /// Indicates an apparent attempt to bypass policy evaluation.
    /// </summary>
    public const string PolicyBypassAttempt = "PolicyBypassAttempt";

    /// <summary>
    /// Indicates prompt-injection-like content as determined by the host contributor.
    /// </summary>
    public const string PromptInjectionLikeInput = "PromptInjectionLikeInput";

    /// <summary>
    /// Indicates a possible replay attempt.
    /// </summary>
    public const string ReplayAttempt = "ReplayAttempt";

    /// <summary>
    /// Indicates mismatch between the request and a capability grant.
    /// </summary>
    public const string CapabilityTokenMismatch = "CapabilityTokenMismatch";

    /// <summary>
    /// Indicates mismatch with regional or local policy expectations.
    /// </summary>
    public const string RegionPolicyMismatch = "RegionPolicyMismatch";

    /// <summary>
    /// Indicates a potentially unsafe external command or execution request.
    /// </summary>
    public const string UnsafeExternalCommand = "UnsafeExternalCommand";

    /// <summary>
    /// Indicates a possible audit integrity or provenance risk.
    /// </summary>
    public const string AuditIntegrityRisk = "AuditIntegrityRisk";
}
