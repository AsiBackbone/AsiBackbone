using AsiBackbone.Core.Acknowledgments;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Decisions;
using Microsoft.Extensions.Options;

namespace AsiBackbone.AspNetCore.Acknowledgments;

/// <summary>
/// Provides the default ASP.NET Core-friendly acknowledgment challenge service.
/// </summary>
public sealed class DefaultAcknowledgmentChallengeService : IAcknowledgmentChallengeService
{
    private const string ChallengeMismatchCode = "acknowledgment.challenge.mismatch";
    private const string ChallengeActorMismatchCode = "acknowledgment.challenge.actor_mismatch";
    private const string ChallengeCodeMismatchCode = "acknowledgment.challenge.code_mismatch";

    private readonly AcknowledgmentChallengeOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAcknowledgmentChallengeService" /> class.
    /// </summary>
    /// <param name="options">The acknowledgment challenge options.</param>
    public DefaultAcknowledgmentChallengeService(IOptions<AcknowledgmentChallengeOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    /// <inheritdoc />
    public AcknowledgmentChallenge CreateChallenge(
        IGovernanceActorContext actor,
        string operationName,
        GovernanceDecision decision,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(decision);

        if (!decision.RequiresAcknowledgment)
        {
            throw new InvalidOperationException("Only acknowledgment-required governance decisions can be converted into acknowledgment challenges.");
        }

        var request = AcknowledgmentRequest.FromDecision(
            actor,
            operationName,
            decision,
            options.RequiredAcknowledgmentCode,
            options.RequiredAcknowledgmentText,
            options.RiskLevel,
            options.RiskCategory,
            metadata: metadata);

        return AcknowledgmentChallenge.FromAcknowledgmentRequest(request, options);
    }

    /// <inheritdoc />
    public AcknowledgmentChallengeResult HandleResponse(
        AcknowledgmentChallenge challenge,
        IGovernanceActorContext actor,
        AcknowledgmentChallengeRequest response,
        DateTimeOffset? occurredUtc = null)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(response);

        if (!string.Equals(challenge.HandshakeId, response.HandshakeId?.Trim(), StringComparison.Ordinal))
        {
            return AcknowledgmentChallengeResult.Failure(
                ChallengeMismatchCode,
                "The acknowledgment response did not match the active challenge.");
        }

        // The acknowledgment is the accountability record for the operation, so it must be produced by the actor the
        // challenge was issued to. Previously any actor could satisfy any challenge it could name, and the persisted
        // acknowledgment attributed the liability to whoever answered rather than to whoever was challenged.
        if (!IsChallengedActor(challenge, actor))
        {
            return AcknowledgmentChallengeResult.Failure(
                ChallengeActorMismatchCode,
                "The acknowledgment response was not submitted by the actor the challenge was issued to.");
        }

        if (!string.Equals(challenge.RequiredAcknowledgmentCode, response.AcknowledgmentCode?.Trim(), StringComparison.Ordinal))
        {
            return AcknowledgmentChallengeResult.Failure(
                ChallengeCodeMismatchCode,
                "The acknowledgment response did not contain the required acknowledgment code.");
        }

        var acknowledgment = AcknowledgmentResponse.Create(
            challenge.AcknowledgmentRequest,
            actor,
            response.Acknowledged,
            occurredUtc: occurredUtc,
            metadata: response.Metadata);

        return AcknowledgmentChallengeResult.Success(acknowledgment);
    }

    /// <summary>
    /// Determines whether the responding actor is the actor the challenge was issued to.
    /// </summary>
    /// <remarks>
    /// Both the identifier and the actor type participate, because the same identifier under a different actor type is a
    /// different principal. A single reason code covers both comparisons so a caller cannot use the failure to probe
    /// which component differed.
    /// </remarks>
    /// <param name="challenge">The active acknowledgment challenge.</param>
    /// <param name="actor">The actor that submitted the acknowledgment response.</param>
    /// <returns><see langword="true" /> when the responding actor matches the challenged actor.</returns>
    private static bool IsChallengedActor(
        AcknowledgmentChallenge challenge,
        IGovernanceActorContext actor)
    {
        AcknowledgmentRequest request = challenge.AcknowledgmentRequest;

        return string.Equals(request.ActorId, actor.ActorId?.Trim(), StringComparison.Ordinal)
            && request.ActorType == actor.ActorType;
    }
}
