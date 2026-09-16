namespace AsiBackbone.Core;

/// <summary>
/// Defines stable length limits for identifiers that flow through governance records and persistence adapters.
/// </summary>
public static class GovernanceIdentifierLimits
{
    /// <summary>
    /// Gets the maximum supported length for a governance identifier.
    /// </summary>
    /// <remarks>
    /// Persistence adapters use a 128-character identifier boundary for correlation identifiers and other stable IDs.
    /// Host adapters should apply this limit before untrusted values enter logging, governance records, or persistence.
    /// </remarks>
    public const int MaximumLength = 128;
}
