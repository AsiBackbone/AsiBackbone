using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Handshakes;
using Microsoft.Extensions.Options;

namespace AsiBackbone.AspNetCore.Handshakes;

/// <summary>
/// Provides the default ASP.NET Core-friendly acknowledgment challenge service.
/// </summary>
public sealed class DefaultAcknowledgmentChallengeService : IAcknowledgmentChallengeService
{
    private const string ChallengeMismatchCode = "acknowledgment.challenge.mismatch";
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

        var request = LiabilityHandshakeRequest.FromDecision(
            actor,
            operationName,
            decision,
            options.RequiredAcknowledgmentCode,
            options.RequiredAcknowledgmentText,
            options.RiskLevel,
            options.RiskCategory,
            metadata: metadata);

        return AcknowledgmentChallenge.FromHandshakeRequest(request, options);
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

        if (!string.Equals(challenge.RequiredAcknowledgmentCode, response.AcknowledgmentCode?.Trim(), StringComparison.Ordinal))
        {
            return AcknowledgmentChallengeResult.Failure(
                ChallengeCodeMismatchCode,
                "The acknowledgment response did not contain the required acknowledgment code.");
        }

        var acknowledgment = LiabilityHandshakeAcknowledgment.Create(
            challenge.HandshakeRequest,
            actor,
            response.Acknowledged,
            occurredUtc: occurredUtc,
            metadata: response.Metadata);

        return AcknowledgmentChallengeResult.Success(acknowledgment);
    }
}
