namespace AsiBackbone.Core.Actors;

/// <summary>
/// Defines a framework-neutral description of the actor participating in an AsiBackbone operation.
/// </summary>
public interface IGovernanceActorContext
{
    /// <summary>
    /// Gets the stable actor identifier.
    /// </summary>
    string ActorId { get; }

    /// <summary>
    /// Gets the actor type.
    /// </summary>
    GovernanceActorType ActorType { get; }

    /// <summary>
    /// Gets the optional display name or label for the actor.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether the actor is known to the host or framework.
    /// </summary>
    bool IsKnown { get; }

    /// <summary>
    /// Gets a value indicating whether the actor has been authenticated by the host.
    /// </summary>
    bool IsAuthenticated { get; }
}
