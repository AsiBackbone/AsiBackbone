namespace AsiBackbone.Core.Actors;

/// <summary>
/// Identifies the general kind of actor participating in an AsiBackbone operation.
/// </summary>
public enum GovernanceActorType
{
    /// <summary>
    /// The actor is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The actor represents a human participant.
    /// </summary>
    Human = 1,

    /// <summary>
    /// The actor represents a trusted system process.
    /// </summary>
    System = 2,

    /// <summary>
    /// The actor represents a service, integration, or daemon.
    /// </summary>
    Service = 3,

    /// <summary>
    /// The actor represents an autonomous or delegated software agent.
    /// </summary>
    Agent = 4
}
