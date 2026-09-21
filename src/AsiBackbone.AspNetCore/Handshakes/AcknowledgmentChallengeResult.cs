using AsiBackbone.Core.Handshakes;
using AsiBackbone.Core.Results;

namespace AsiBackbone.AspNetCore.Handshakes;

/// <summary>
/// Represents the result of handling a host-submitted acknowledgment challenge response.
/// </summary>
public sealed class AcknowledgmentChallengeResult
{
    private AcknowledgmentChallengeResult(
        OperationResult result,
        LiabilityHandshakeAcknowledgment? acknowledgment)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
        Acknowledgment = acknowledgment;
    }

    /// <summary>
    /// Gets the operation result describing response handling success or failure.
    /// </summary>
    public OperationResult Result { get; }

    /// <summary>
    /// Gets the Core acknowledgment response when one was created.
    /// </summary>
    public LiabilityHandshakeAcknowledgment? Acknowledgment { get; }

    /// <summary>
    /// Gets a value indicating whether the response was handled successfully.
    /// </summary>
    /// <remarks>
    /// This reports only that the response was valid and produced an acknowledgment record. It is <see langword="true" />
    /// when the actor explicitly declined, because a refusal is recorded as a <see cref="LiabilityHandshakeAcknowledgment" />
    /// just like an acceptance. Do not use it to decide whether the operation may continue; use
    /// <see cref="CanProceed" /> instead.
    /// </remarks>
    public bool Succeeded => Result.Succeeded;

    /// <summary>
    /// Gets a value indicating whether the response was accepted by the actor.
    /// </summary>
    public bool Acknowledged => Acknowledgment?.Acknowledged == true;

    /// <summary>
    /// Gets a value indicating whether the response was rejected by the actor.
    /// </summary>
    public bool Rejected => Acknowledgment?.Rejected == true;

    /// <summary>
    /// Gets a value indicating whether the operation that required acknowledgment may continue.
    /// </summary>
    /// <remarks>
    /// This is <see langword="true" /> only when the response was handled successfully and the actor accepted. A handled
    /// refusal and every handling failure yield <see langword="false" />. It is the single check a host should gate the
    /// consequential operation on, matching <see cref="Core.Decisions.GovernanceDecision.CanProceed" />.
    /// </remarks>
    public bool CanProceed => Succeeded && Acknowledged;

    /// <summary>
    /// Creates a successful acknowledgment challenge result.
    /// </summary>
    /// <param name="acknowledgment">The Core acknowledgment response.</param>
    /// <returns>A successful challenge result.</returns>
    public static AcknowledgmentChallengeResult Success(LiabilityHandshakeAcknowledgment acknowledgment)
    {
        ArgumentNullException.ThrowIfNull(acknowledgment);

        return new AcknowledgmentChallengeResult(OperationResult.Success(), acknowledgment);
    }

    /// <summary>
    /// Creates a failed acknowledgment challenge result.
    /// </summary>
    /// <param name="code">The failure reason code.</param>
    /// <param name="message">The failure reason message.</param>
    /// <returns>A failed challenge result.</returns>
    public static AcknowledgmentChallengeResult Failure(string code, string message)
    {
        return new AcknowledgmentChallengeResult(OperationResult.Failure(code, message), null);
    }
}
