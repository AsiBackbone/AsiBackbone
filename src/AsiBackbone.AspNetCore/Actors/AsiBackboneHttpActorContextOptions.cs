using System.Security.Claims;
using AsiBackbone.Core.Actors;

namespace AsiBackbone.AspNetCore.Actors;

/// <summary>
/// Configures how ASP.NET Core claims are mapped into framework-neutral actor contexts.
/// </summary>
public sealed class AsiBackboneHttpActorContextOptions
{
    /// <summary>
    /// Gets the default stable identifier claim types checked for authenticated actors.
    /// </summary>
    /// <remarks>
    /// Email claims are deliberately absent. Actor identifiers are persisted verbatim and indexed in durable audit rows,
    /// and an email address is both personal data and a mutable identifier, so a host that wants one must add it
    /// explicitly and accept the retention consequences.
    /// </remarks>
    public static IReadOnlyList<string> DefaultActorIdClaimTypes { get; } =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "oid",
        "client_id",
        "azp",
    ];

    /// <summary>
    /// Gets the default display-name claim types checked for authenticated actors.
    /// </summary>
    /// <remarks>
    /// Email claims are deliberately absent, for the retention reason described on <see cref="DefaultActorIdClaimTypes" />.
    /// </remarks>
    public static IReadOnlyList<string> DefaultDisplayNameClaimTypes { get; } =
    [
        ClaimTypes.Name,
        "name",
        "preferred_username",
    ];

    /// <summary>
    /// Gets the default actor types that may be accepted from an HTTP claim.
    /// </summary>
    /// <remarks>
    /// Privileged software actor types require explicit host opt-in because claim trust is established by the host identity boundary, not by AsiBackbone.
    /// </remarks>
    public static IReadOnlyList<AsiBackboneActorType> DefaultAllowedActorTypesFromClaims { get; } =
    [
        AsiBackboneActorType.Human,
    ];

    /// <summary>
    /// Gets or sets the claim types used to resolve a stable actor identifier.
    /// </summary>
    public IList<string> ActorIdClaimTypes { get; set; } = [.. DefaultActorIdClaimTypes];

    /// <summary>
    /// Gets or sets the claim types used to resolve an optional actor display name.
    /// </summary>
    public IList<string> DisplayNameClaimTypes { get; set; } = [.. DefaultDisplayNameClaimTypes];

    /// <summary>
    /// Gets or sets the claim type used to resolve an actor type.
    /// </summary>
    /// <remarks>
    /// This must identify a trusted identity-provider-issued or host-generated claim. It must not be populated from user-controlled request, scope, profile, or application data.
    /// </remarks>
    public string ActorTypeClaimType { get; set; } = "actor_type";

    /// <summary>
    /// Gets or sets the actor types that may be accepted from <see cref="ActorTypeClaimType" />.
    /// </summary>
    /// <remarks>
    /// The conservative default accepts only <see cref="AsiBackboneActorType.Human" />. A host must explicitly add <see cref="AsiBackboneActorType.System" />, <see cref="AsiBackboneActorType.Service" />, or <see cref="AsiBackboneActorType.Agent" /> after establishing that the configured claim is protected by a trusted identity boundary. An empty list disables actor-type claim mapping.
    /// </remarks>
    public IList<AsiBackboneActorType> AllowedActorTypesFromClaims { get; set; } = [.. DefaultAllowedActorTypesFromClaims];

    /// <summary>
    /// Gets or sets the actor type used when an authenticated principal does not provide a valid or allowed actor type claim.
    /// </summary>
    public AsiBackboneActorType DefaultAuthenticatedActorType { get; set; } = AsiBackboneActorType.Human;

    /// <summary>
    /// Gets or sets the display name used for unauthenticated actors.
    /// </summary>
    public string? UnauthenticatedDisplayName { get; set; }

    /// <summary>
    /// Validates the options.
    /// </summary>
    public void Validate()
    {
        if (ActorIdClaimTypes is null || ActorIdClaimTypes.Count == 0 || ActorIdClaimTypes.All(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("At least one actor identifier claim type must be configured.");
        }

        if (DisplayNameClaimTypes is null)
        {
            throw new InvalidOperationException("DisplayNameClaimTypes must be configured.");
        }

        if (string.IsNullOrWhiteSpace(ActorTypeClaimType))
        {
            throw new InvalidOperationException("ActorTypeClaimType must be configured.");
        }

        if (AllowedActorTypesFromClaims is null)
        {
            throw new InvalidOperationException("AllowedActorTypesFromClaims must be configured.");
        }

        if (AllowedActorTypesFromClaims.Any(static actorType => !Enum.IsDefined(actorType)))
        {
            throw new InvalidOperationException("AllowedActorTypesFromClaims must contain only defined actor types.");
        }

        if (!Enum.IsDefined(DefaultAuthenticatedActorType))
        {
            throw new InvalidOperationException("DefaultAuthenticatedActorType must be a defined actor type.");
        }
    }
}
