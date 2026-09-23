using AsiBackbone.Core.Acknowledgments;
using AsiBackbone.Core.Actors;

namespace AsiBackbone.AspNetCore.Acknowledgments;

internal static class AcknowledgmentActorBinding
{
    public const string FailureCode = "acknowledgment.challenge.actor_unbound";

    public const string FailureMessage =
        "Acknowledgment challenges require a known, authenticated actor with a distinct host-provided identity.";

    public static bool IsSufficient(IGovernanceActorContext actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return actor.IsKnown
            && actor.IsAuthenticated
            && HasDistinctIdentity(actor.ActorId, actor.ActorType);
    }

    public static bool IsSufficient(AcknowledgmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return HasDistinctIdentity(request.ActorId, request.ActorType);
    }

    private static bool HasDistinctIdentity(string? actorId, GovernanceActorType actorType)
    {
        return actorType is not GovernanceActorType.Unknown
            && !string.IsNullOrWhiteSpace(actorId)
            && !string.Equals(
                actorId.Trim(),
                GovernanceActorContext.UnknownActorId,
                StringComparison.Ordinal);
    }
}
