using AsiBackbone.AspNetCore.Acknowledgments;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Decisions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Acknowledgments;

/// <summary>
/// Unit tests for the <see cref="AcknowledgmentChallenge"/> and related classes.
/// </summary>
public sealed class AsiBackboneAcknowledgmentChallengeBranchTests
{
    /// <summary>
    /// Tests that the <c>AcknowledgmentChallengeResult.Success(AcknowledgmentResponse)</c> method throws an <see cref="ArgumentNullException"/> when a null acknowledgment is provided.
    /// </summary>
    [Fact]
    public void ChallengeResultSuccessRejectsNullAcknowledgment()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            AcknowledgmentChallengeResult.Success(null!));
    }

    /// <summary>
    /// Tests that the <see cref="AcknowledgmentChallengeResult.Failure(string, string)"/> method correctly exposes the unacknowledged state without an acknowledgment.
    /// </summary>
    [Fact]
    public void ChallengeResultFailureExposesUnacknowledgedStateWithoutAcknowledgment()
    {
        var result = AcknowledgmentChallengeResult.Failure(
            "ack.failed",
            "Acknowledgment failed.");

        Assert.False(result.Succeeded);
        Assert.False(result.Acknowledged);
        Assert.False(result.Rejected);
        Assert.Null(result.Acknowledgment);
        Assert.Contains("ack.failed", result.Result.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <c>DefaultAcknowledgmentChallengeService.CreateChallenge(IGovernanceActorContext, string, GovernanceDecision)</c> method throws an <see cref="ArgumentNullException"/> when a null actor is provided.
    /// </summary>
    [Fact]
    public void CreateChallengeRejectsNullActor()
    {
        DefaultAcknowledgmentChallengeService service = CreateService();
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");

        _ = Assert.Throws<ArgumentNullException>(() => service.CreateChallenge(null!, "RunOperation", decision));
    }

    /// <summary>
    /// Tests that the <c>DefaultAcknowledgmentChallengeService.CreateChallenge(IGovernanceActorContext, string, GovernanceDecision)</c> method throws an <see cref="ArgumentNullException"/> when a null decision is provided.
    /// </summary>
    [Fact]
    public void CreateChallengeRejectsNullDecision()
    {
        DefaultAcknowledgmentChallengeService service = CreateService();
        IGovernanceActorContext actor = GovernanceActorContext.Human("user-123");

        _ = Assert.Throws<ArgumentNullException>(() => service.CreateChallenge(actor, "RunOperation", null!));
    }

    /// <summary>
    /// Tests that the <c>DefaultAcknowledgmentChallengeService.HandleResponse(AcknowledgmentChallenge, IGovernanceActorContext, AcknowledgmentChallengeRequest)</c> method throws an <see cref="ArgumentNullException"/> when a null challenge is provided.
    /// </summary>
    [Fact]
    public void HandleResponseRejectsNullChallenge()
    {
        DefaultAcknowledgmentChallengeService service = CreateService();
        IGovernanceActorContext actor = GovernanceActorContext.Human("user-123");
        var response = new AcknowledgmentChallengeRequest();

        _ = Assert.Throws<ArgumentNullException>(() => service.HandleResponse(null!, actor, response));
    }

    /// <summary>
    /// Tests that the <c>DefaultAcknowledgmentChallengeService.HandleResponse(AcknowledgmentChallenge, IGovernanceActorContext, AcknowledgmentChallengeRequest)</c> method throws an <see cref="ArgumentNullException"/> when a null actor is provided.
    /// </summary>
    [Fact]
    public void HandleResponseRejectsNullActor()
    {
        DefaultAcknowledgmentChallengeService service = CreateService();
        IGovernanceActorContext actor = GovernanceActorContext.Human("user-123");
        AcknowledgmentChallenge challenge = CreateChallenge(service, actor);
        var response = new AcknowledgmentChallengeRequest();

        _ = Assert.Throws<ArgumentNullException>(() => service.HandleResponse(challenge, null!, response));
    }

    /// <summary>
    /// Tests that the <c>DefaultAcknowledgmentChallengeService.HandleResponse(AcknowledgmentChallenge, IGovernanceActorContext, AcknowledgmentChallengeRequest)</c> method throws an <see cref="ArgumentNullException"/> when a null response is provided.
    /// </summary>
    [Fact]
    public void HandleResponseRejectsNullResponse()
    {
        DefaultAcknowledgmentChallengeService service = CreateService();
        IGovernanceActorContext actor = GovernanceActorContext.Human("user-123");
        AcknowledgmentChallenge challenge = CreateChallenge(service, actor);

        _ = Assert.Throws<ArgumentNullException>(() => service.HandleResponse(challenge, actor, null!));
    }

    /// <summary>
    /// Tests that the <c>DefaultAcknowledgmentChallengeService.HandleResponse(AcknowledgmentChallenge, IGovernanceActorContext, AcknowledgmentChallengeRequest)</c> method fails when the handshake ID is missing or blank in the response.
    /// </summary>
    /// <param name="handshakeId">
    /// The handshake ID to test, which can be null, empty, or whitespace.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HandleResponseFailsWhenHandshakeIdIsMissingOrBlank(string? handshakeId)
    {
        DefaultAcknowledgmentChallengeService service = CreateService();
        IGovernanceActorContext actor = GovernanceActorContext.Human("user-123");
        AcknowledgmentChallenge challenge = CreateChallenge(service, actor);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = handshakeId,
            AcknowledgmentCode = challenge.RequiredAcknowledgmentCode,
            Acknowledged = true,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, actor, response);

        Assert.False(result.Succeeded);
        Assert.Contains("acknowledgment.challenge.mismatch", result.Result.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <c>DefaultAcknowledgmentChallengeService.HandleResponse(AcknowledgmentChallenge, IGovernanceActorContext, AcknowledgmentChallengeRequest)</c> method fails when the acknowledgment code is missing or blank in the response.
    /// </summary>
    /// <param name="acknowledgmentCode">
    /// The acknowledgment code to test, which can be null, empty, or whitespace.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HandleResponseFailsWhenAcknowledgmentCodeIsMissingOrBlank(string? acknowledgmentCode)
    {
        DefaultAcknowledgmentChallengeService service = CreateService();
        IGovernanceActorContext actor = GovernanceActorContext.Human("user-123");
        AcknowledgmentChallenge challenge = CreateChallenge(service, actor);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = challenge.HandshakeId,
            AcknowledgmentCode = acknowledgmentCode,
            Acknowledged = true,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, actor, response);

        Assert.False(result.Succeeded);
        Assert.Contains("acknowledgment.challenge.code_mismatch", result.Result.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <see cref="AcknowledgmentChallengeOptions.Validate"/> method throws an <see cref="InvalidOperationException"/> when the required acknowledgment text is missing or blank.
    /// </summary>
    /// <param name="text">
    /// The acknowledgment text to test, which can be null, empty, or whitespace.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChallengeOptionsRejectMissingAcknowledgmentText(string? text)
    {
        var options = new AcknowledgmentChallengeOptions
        {
            RequiredAcknowledgmentText = text!,
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("acknowledgment text", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService"/> constructor throws an <see cref="ArgumentNullException"/> when null options are provided.
    /// </summary>
    [Fact]
    public void ServiceConstructorRejectsNullOptions()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new DefaultAcknowledgmentChallengeService(null!));
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService"/> constructor throws an <see cref="InvalidOperationException"/> when invalid options are provided (e.g., missing required acknowledgment code).
    /// </summary>
    [Fact]
    public void ServiceConstructorRejectsInvalidOptions()
    {
        var options = new AcknowledgmentChallengeOptions
        {
            RequiredAcknowledgmentCode = " ",
        };

        _ = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAcknowledgmentChallengeService(Options.Create(options)));
    }

    private static AcknowledgmentChallenge CreateChallenge(
        DefaultAcknowledgmentChallengeService service,
        IGovernanceActorContext actor)
    {
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");

        return service.CreateChallenge(actor, "RunOperation", decision);
    }

    private static DefaultAcknowledgmentChallengeService CreateService(
        AcknowledgmentChallengeOptions? options = null)
    {
        return new DefaultAcknowledgmentChallengeService(
            Options.Create(options ?? new AcknowledgmentChallengeOptions()));
    }
}
