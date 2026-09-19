using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Decisions;

namespace AsiBackbone.AspNetCore.Handshakes;

/// <summary>
/// Builds and handles ASP.NET Core-friendly acknowledgment challenges without assuming a specific UI framework.
/// </summary>
public interface IAcknowledgmentChallengeService
{
    /// <summary>
    /// Creates an acknowledgment challenge from an acknowledgment-required governance decision.
    /// </summary>
    /// <param name="actor">The actor associated with the challenge.</param>
    /// <param name="operationName">The operation name requiring acknowledgment.</param>
    /// <param name="decision">The governance decision requiring acknowledgment.</param>
    /// <param name="metadata">Optional host-provided challenge metadata.</param>
    /// <returns>A host-friendly acknowledgment challenge.</returns>
    AcknowledgmentChallenge CreateChallenge(
        IGovernanceActorContext actor,
        string operationName,
        GovernanceDecision decision,
        IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Handles a host-submitted acknowledgment challenge response.
    /// </summary>
    /// <param name="challenge">The original acknowledgment challenge.</param>
    /// <param name="actor">The actor responding to the challenge.</param>
    /// <param name="response">The submitted response.</param>
    /// <param name="occurredUtc">Optional response timestamp.</param>
    /// <returns>The result of handling the response.</returns>
    AcknowledgmentChallengeResult HandleResponse(
        AcknowledgmentChallenge challenge,
        IGovernanceActorContext actor,
        AcknowledgmentChallengeRequest response,
        DateTimeOffset? occurredUtc = null);
}
