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
