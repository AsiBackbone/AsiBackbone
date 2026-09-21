using AsiBackbone.AspNetCore.Handshakes;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Handshakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Handshakes;

/// <summary>
/// Unit tests for the <see cref="DefaultAcknowledgmentChallengeService"/> class, which handles the creation and processing of acknowledgment challenges in the AsiBackbone framework.
/// </summary>
public sealed class AsiBackboneAcknowledgmentChallengeServiceTests
{
    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.CreateChallenge"/> method correctly builds a host-friendly acknowledgment challenge from a given acknowledgment decision, including all relevant metadata and options.
    /// </summary>
    [Fact]
    public void CreateChallengeBuildsHostFriendlyChallengeFromAcknowledgmentDecision()
    {
        var actor = GovernanceActorContext.Human(" user-123 ", " Test User ");
        var decision = GovernanceDecision.RequireAcknowledgment(
            "risk.high",
            "Manual acknowledgment is required.",
            correlationId: " correlation-123 ",
            traceId: " trace-123 ",
            policyVersion: " v1 ",
            policyHash: " hash-123 ");
        var options = new AcknowledgmentChallengeOptions
        {
            RequiredAcknowledgmentCode = "CONFIRM",
            RequiredAcknowledgmentText = "Confirm responsibility before continuing.",
            RiskLevel = LiabilityHandshakeRiskLevel.High,
            RiskCategory = "content-risk",
            IncludeTraceId = true,
            IncludePolicyMetadata = true,
        };
        DefaultAcknowledgmentChallengeService service = CreateService(options);

        AcknowledgmentChallenge challenge = service.CreateChallenge(
            actor,
            " PublishEpisode ",
            decision,
            new Dictionary<string, string>
            {
                [" source "] = " web ",
            });

        Assert.Equal("PublishEpisode", challenge.OperationName);
        Assert.Equal("risk.high", challenge.ReasonCode);
        Assert.Equal("Manual acknowledgment is required.", challenge.ReasonMessage);
        Assert.Equal("CONFIRM", challenge.RequiredAcknowledgmentCode);
        Assert.Equal("Confirm responsibility before continuing.", challenge.RequiredAcknowledgmentText);
        Assert.Equal(LiabilityHandshakeRiskLevel.High, challenge.RiskLevel);
        Assert.Equal("content-risk", challenge.RiskCategory);
        Assert.Equal("correlation-123", challenge.CorrelationId);
        Assert.Equal("trace-123", challenge.TraceId);
        Assert.Equal("v1", challenge.PolicyVersion);
        Assert.Equal("hash-123", challenge.PolicyHash);
        Assert.Equal("web", challenge.Metadata["source"]);
        Assert.Equal(challenge.HandshakeId, challenge.HandshakeRequest.HandshakeId);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.CreateChallenge"/> method hides optional diagnostic fields (TraceId, PolicyVersion, PolicyHash) by default when creating an acknowledgment challenge.
    /// </summary>
    [Fact]
    public void CreateChallengeHidesOptionalDiagnosticFieldsByDefault()
    {
        var actor = GovernanceActorContext.Human("user-123");
        var decision = GovernanceDecision.RequireAcknowledgment(
            "ack.required",
            "Acknowledgment required.",
            correlationId: "correlation-123",
            traceId: "trace-123",
            policyVersion: "v1",
            policyHash: "hash-123");
        DefaultAcknowledgmentChallengeService service = CreateService();

        AcknowledgmentChallenge challenge = service.CreateChallenge(actor, "RunOperation", decision);

        Assert.Equal("Acknowledgment required.", challenge.ReasonMessage);
        Assert.Equal("correlation-123", challenge.CorrelationId);
        Assert.Null(challenge.TraceId);
        Assert.Null(challenge.PolicyVersion);
        Assert.Null(challenge.PolicyHash);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.CreateChallenge"/> method can be configured to hide the reason message in the acknowledgment challenge when the <see cref="AcknowledgmentChallengeOptions.IncludeReasonMessage"/> option is set to false.
    /// </summary>
    [Fact]
    public void CreateChallengeCanHideReasonMessage()
    {
        var actor = GovernanceActorContext.Human("user-123");
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Do not expose this.");
        DefaultAcknowledgmentChallengeService service = CreateService(new AcknowledgmentChallengeOptions
        {
            IncludeReasonMessage = false,
        });

        AcknowledgmentChallenge challenge = service.CreateChallenge(actor, "RunOperation", decision);

        Assert.Null(challenge.ReasonMessage);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.CreateChallenge"/> method throws an <see cref="InvalidOperationException"/> when attempting to create a challenge for a decision that does not require acknowledgment, ensuring that only valid acknowledgment decisions are processed.
    /// </summary>
    [Fact]
    public void CreateChallengeRejectsDecisionThatDoesNotRequireAcknowledgment()
    {
        var actor = GovernanceActorContext.Human("user-123");
        var decision = GovernanceDecision.Allow();
        DefaultAcknowledgmentChallengeService service = CreateService();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            service.CreateChallenge(actor, "RunOperation", decision));

        Assert.Contains("acknowledgment-required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.HandleResponse"/> method correctly processes a valid acknowledgment response that matches the challenge, resulting in an accepted acknowledgment with the expected properties.
    /// </summary>
    [Fact]
    public void HandleResponseCreatesAcceptedAcknowledgmentWhenResponseMatchesChallenge()
    {
        var actor = GovernanceActorContext.Human("user-123", "Test User");
        var decision = GovernanceDecision.RequireAcknowledgment(
            "ack.required",
            "Acknowledgment required.",
            correlationId: "correlation-123",
            traceId: "trace-123");
        DefaultAcknowledgmentChallengeService service = CreateService(new AcknowledgmentChallengeOptions
        {
            RequiredAcknowledgmentCode = "CONFIRM",
        });
        AcknowledgmentChallenge challenge = service.CreateChallenge(actor, "RunOperation", decision);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = $" {challenge.HandshakeId} ",
            AcknowledgmentCode = " CONFIRM ",
            Acknowledged = true,
            Metadata = new Dictionary<string, string>
            {
                ["transport"] = "minimal-api",
            },
        };
        DateTimeOffset occurredUtc = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, actor, response, occurredUtc);

        Assert.True(result.Succeeded);
        Assert.True(result.Acknowledged);
        Assert.False(result.Rejected);
        Assert.NotNull(result.Acknowledgment);
        Assert.Equal(challenge.HandshakeId, result.Acknowledgment.HandshakeId);
        Assert.Equal("CONFIRM", result.Acknowledgment.AcknowledgmentCode);
        Assert.Equal("correlation-123", result.Acknowledgment.CorrelationId);
        Assert.Equal("trace-123", result.Acknowledgment.TraceId);
        Assert.Equal("minimal-api", result.Acknowledgment.Metadata["transport"]);
        Assert.Equal(occurredUtc, result.Acknowledgment.OccurredUtc);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.HandleResponse"/> method correctly processes a response where the actor declines to acknowledge, resulting in a rejected acknowledgment with the expected properties.
    /// </summary>
    [Fact]
    public void HandleResponseCreatesRejectedAcknowledgmentWhenActorDeclines()
    {
        var actor = GovernanceActorContext.Human("user-123");
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");
        DefaultAcknowledgmentChallengeService service = CreateService();
        AcknowledgmentChallenge challenge = service.CreateChallenge(actor, "RunOperation", decision);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = challenge.HandshakeId,
            AcknowledgmentCode = challenge.RequiredAcknowledgmentCode,
            Acknowledged = false,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, actor, response);

        Assert.True(result.Succeeded);
        Assert.False(result.Acknowledged);
        Assert.True(result.Rejected);
        Assert.NotNull(result.Acknowledgment);
        Assert.True(result.Acknowledgment.Rejected);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.HandleResponse"/> method fails when the handshake ID in the response does not match the expected handshake ID from the challenge, resulting in a failure with the appropriate reason code.
    /// </summary>
    [Fact]
    public void HandleResponseFailsWhenHandshakeIdDoesNotMatch()
    {
        var actor = GovernanceActorContext.Human("user-123");
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");
        DefaultAcknowledgmentChallengeService service = CreateService();
        AcknowledgmentChallenge challenge = service.CreateChallenge(actor, "RunOperation", decision);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = "different-handshake",
            AcknowledgmentCode = challenge.RequiredAcknowledgmentCode,
            Acknowledged = true,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, actor, response);

        Assert.False(result.Succeeded);
        Assert.Null(result.Acknowledgment);
        Assert.Contains("acknowledgment.challenge.mismatch", result.Result.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.HandleResponse"/> method fails when the acknowledgment code in the response does not match the required acknowledgment code from the challenge, resulting in a failure with the appropriate reason code.
    /// </summary>
    [Fact]
    public void HandleResponseFailsWhenAcknowledgmentCodeDoesNotMatch()
    {
        var actor = GovernanceActorContext.Human("user-123");
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");
        DefaultAcknowledgmentChallengeService service = CreateService();
        AcknowledgmentChallenge challenge = service.CreateChallenge(actor, "RunOperation", decision);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = challenge.HandshakeId,
            AcknowledgmentCode = "wrong-code",
            Acknowledged = true,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, actor, response);

        Assert.False(result.Succeeded);
        Assert.Null(result.Acknowledgment);
        Assert.Contains("acknowledgment.challenge.code_mismatch", result.Result.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.HandleResponse"/> method fails when a different actor submits an otherwise valid response, so that one actor cannot accept the liability recorded against another.
    /// </summary>
    [Fact]
    public void HandleResponseFailsWhenRespondingActorIsNotTheChallengedActor()
    {
        var challengedActor = GovernanceActorContext.Human("user-123");
        var otherActor = GovernanceActorContext.Human("user-456");
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");
        DefaultAcknowledgmentChallengeService service = CreateService();
        AcknowledgmentChallenge challenge = service.CreateChallenge(challengedActor, "RunOperation", decision);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = challenge.HandshakeId,
            AcknowledgmentCode = challenge.RequiredAcknowledgmentCode,
            Acknowledged = true,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, otherActor, response);

        Assert.False(result.Succeeded);
        Assert.Null(result.Acknowledgment);
        Assert.Contains("acknowledgment.challenge.actor_mismatch", result.Result.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.HandleResponse"/> method prioritizes actor binding failures over acknowledgment code failures.
    /// </summary>
    [Fact]
    public void HandleResponseFailsWithActorMismatchWhenActorAndCodeDoNotMatch()
    {
        var challengedActor = GovernanceActorContext.Human("user-123");
        var otherActor = GovernanceActorContext.Human("user-456");
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");
        DefaultAcknowledgmentChallengeService service = CreateService();
        AcknowledgmentChallenge challenge = service.CreateChallenge(challengedActor, "RunOperation", decision);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = challenge.HandshakeId,
            AcknowledgmentCode = "wrong-code",
            Acknowledged = true,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, otherActor, response);

        Assert.False(result.Succeeded);
        Assert.Null(result.Acknowledgment);
        Assert.Contains("acknowledgment.challenge.actor_mismatch", result.Result.ReasonCodes);
        Assert.DoesNotContain("acknowledgment.challenge.code_mismatch", result.Result.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.HandleResponse"/> method fails when the responding actor reuses the challenged actor identifier under a different actor type, because that is a different principal.
    /// </summary>
    [Fact]
    public void HandleResponseFailsWhenRespondingActorTypeDiffers()
    {
        var challengedActor = GovernanceActorContext.Human("shared-id");
        var impersonatingActor = GovernanceActorContext.Service("shared-id");
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");
        DefaultAcknowledgmentChallengeService service = CreateService();
        AcknowledgmentChallenge challenge = service.CreateChallenge(challengedActor, "RunOperation", decision);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = challenge.HandshakeId,
            AcknowledgmentCode = challenge.RequiredAcknowledgmentCode,
            Acknowledged = true,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, impersonatingActor, response);

        Assert.False(result.Succeeded);
        Assert.Null(result.Acknowledgment);
        Assert.Contains("acknowledgment.challenge.actor_mismatch", result.Result.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <see cref="DefaultAcknowledgmentChallengeService.HandleResponse"/> method accepts a response from an equivalent actor context whose identifier carries surrounding whitespace, so the actor binding check does not reject a legitimately re-resolved actor.
    /// </summary>
    [Fact]
    public void HandleResponseAcceptsEquivalentActorWithUntrimmedIdentifier()
    {
        var challengedActor = GovernanceActorContext.Human("user-123");
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledgment required.");
        DefaultAcknowledgmentChallengeService service = CreateService();
        AcknowledgmentChallenge challenge = service.CreateChallenge(challengedActor, "RunOperation", decision);
        IGovernanceActorContext reresolvedActor = new TestActorContext(" user-123 ", GovernanceActorType.Human);
        var response = new AcknowledgmentChallengeRequest
        {
            HandshakeId = challenge.HandshakeId,
            AcknowledgmentCode = challenge.RequiredAcknowledgmentCode,
            Acknowledged = true,
        };

        AcknowledgmentChallengeResult result = service.HandleResponse(challenge, reresolvedActor, response);

        Assert.True(result.Succeeded);
        Assert.True(result.Acknowledged);
    }

    /// <summary>
    /// Tests that the <see cref="AcknowledgmentChallengeOptions.Validate"/> method throws an <see cref="InvalidOperationException"/> when the required acknowledgment code is null, empty, or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChallengeOptionsRejectMissingAcknowledgmentCode(string? code)
    {
        var options = new AcknowledgmentChallengeOptions
        {
            RequiredAcknowledgmentCode = code!,
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("acknowledgment code", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DefaultAcknowledgmentChallengeService CreateService(
        AcknowledgmentChallengeOptions? options = null)
    {
        return new DefaultAcknowledgmentChallengeService(
            Options.Create(options ?? new AcknowledgmentChallengeOptions()));
    }

    private sealed record TestActorContext(string ActorId, GovernanceActorType ActorType) : IGovernanceActorContext
    {
        public string? DisplayName => null;

        public bool IsKnown => true;

        public bool IsAuthenticated => true;
    }
}
