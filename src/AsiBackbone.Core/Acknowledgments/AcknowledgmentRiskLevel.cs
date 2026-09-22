namespace AsiBackbone.Core.Acknowledgments;

/// <summary>
/// Represents the risk level associated with a liability or responsibility handshake.
/// </summary>
public enum AcknowledgmentRiskLevel
{
    /// <summary>
    /// No risk level was specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The operation is low risk.
    /// </summary>
    Low = 1,

    /// <summary>
    /// The operation is medium risk.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// The operation is high risk.
    /// </summary>
    High = 3,

    /// <summary>
    /// The operation is critical risk.
    /// </summary>
    Critical = 4
}
