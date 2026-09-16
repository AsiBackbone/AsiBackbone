namespace AsiBackbone.Core.Actors;

/// <summary>
/// Provides a default framework-neutral actor context implementation.
/// </summary>
public sealed record GovernanceActorContext : IGovernanceActorContext
{
    /// <summary>
    /// The stable identifier used for unknown actors.
    /// </summary>
    public const string UnknownActorId = "unknown";

    /// <summary>
    /// The stable identifier used for system actors.
    /// </summary>
    public const string SystemActorId = "system";

    private GovernanceActorContext(
        string actorId,
        GovernanceActorType actorType,
        string? displayName,
        bool isKnown,
        bool isAuthenticated)
    {
        ActorId = NormalizeActorId(actorId);
        ActorType = actorType;
        DisplayName = NormalizeDisplayName(displayName);
        IsKnown = isKnown;
        IsAuthenticated = isAuthenticated;
    }

    /// <inheritdoc />
    public string ActorId { get; }

    /// <inheritdoc />
    public GovernanceActorType ActorType { get; }

    /// <inheritdoc />
    public string? DisplayName { get; }

    /// <inheritdoc />
    public bool IsKnown { get; }

    /// <inheritdoc />
    public bool IsAuthenticated { get; }

    /// <summary>
    /// Gets a shared actor context for unknown or unauthenticated operations.
    /// </summary>
    public static GovernanceActorContext Unknown { get; } =
        new(UnknownActorId, GovernanceActorType.Unknown, null, isKnown: false, isAuthenticated: false);

    /// <summary>
    /// Gets a shared actor context for trusted system operations.
    /// </summary>
    public static GovernanceActorContext System { get; } =
        new(SystemActorId, GovernanceActorType.System, "System", isKnown: true, isAuthenticated: true);

    /// <summary>
    /// Creates an actor context for a human participant.
    /// </summary>
    /// <param name="actorId">The stable actor identifier.</param>
    /// <param name="displayName">The optional display name.</param>
    /// <param name="isAuthenticated">Whether the host has authenticated the actor.</param>
    /// <returns>A human actor context.</returns>
    public static GovernanceActorContext Human(
        string actorId,
        string? displayName = null,
        bool isAuthenticated = true)
    {
        return new GovernanceActorContext(
            actorId,
            GovernanceActorType.Human,
            displayName,
            isKnown: true,
            isAuthenticated: isAuthenticated);
    }

    /// <summary>
    /// Creates an actor context for a service participant.
    /// </summary>
    /// <param name="actorId">The stable service actor identifier.</param>
    /// <param name="displayName">The optional display name.</param>
    /// <returns>A service actor context.</returns>
    public static GovernanceActorContext Service(string actorId, string? displayName = null)
    {
        return new GovernanceActorContext(
            actorId,
            GovernanceActorType.Service,
            displayName,
            isKnown: true,
            isAuthenticated: true);
    }

    /// <summary>
    /// Creates an actor context for a delegated or autonomous software agent.
    /// </summary>
    /// <param name="actorId">The stable agent actor identifier.</param>
    /// <param name="displayName">The optional display name.</param>
    /// <param name="isAuthenticated">Whether the host has authenticated the agent.</param>
    /// <returns>An agent actor context.</returns>
    public static GovernanceActorContext Agent(
        string actorId,
        string? displayName = null,
        bool isAuthenticated = true)
    {
        return new GovernanceActorContext(
            actorId,
            GovernanceActorType.Agent,
            displayName,
            isKnown: true,
            isAuthenticated: isAuthenticated);
    }

    private static string NormalizeActorId(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        return actorId.Trim();
    }

    private static string? NormalizeDisplayName(string? displayName)
    {
        string? normalizedDisplayName = displayName?.Trim();

        return string.IsNullOrWhiteSpace(normalizedDisplayName)
            ? null
            : normalizedDisplayName;
    }
}
