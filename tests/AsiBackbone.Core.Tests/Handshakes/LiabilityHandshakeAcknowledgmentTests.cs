using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Handshakes;
using Xunit;

namespace AsiBackbone.Core.Tests.Handshakes;

/// <summary>
/// Unit tests for the <see cref="LiabilityHandshakeAcknowledgment"/> class, which represents the acknowledgment response to a liability handshake request.
/// </summary>
public sealed class LiabilityHandshakeAcknowledgmentTests
{
    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Accept"/> method creates an acknowledgment with the expected properties when accepting a handshake request.
    /// </summary>
    [Fact]
    public void AcceptCreatesAcknowledgedResponse()
    {
        var actor = GovernanceActorContext.Human("user-123", "Chris");
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            acknowledgmentId: "acknowledgment-123",
            occurredUtc: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("acknowledgment-123", acknowledgment.AcknowledgmentId);
        Assert.Equal("handshake-123", acknowledgment.HandshakeId);
        Assert.Equal("user-123", acknowledgment.ActorId);
        Assert.Equal(GovernanceActorType.Human, acknowledgment.ActorType);
        Assert.Equal("Chris", acknowledgment.ActorDisplayName);
        Assert.Equal("ACK-001", acknowledgment.AcknowledgmentCode);
        Assert.True(acknowledgment.Acknowledged);
        Assert.False(acknowledgment.Rejected);
        Assert.Equal(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero), acknowledgment.OccurredUtc);
        Assert.Equal("correlation-123", acknowledgment.CorrelationId);
        Assert.Equal("trace-456", acknowledgment.TraceId);
    }

    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Reject"/> method creates an acknowledgment with the expected properties when rejecting a handshake request.
    /// </summary>
    [Fact]
    public void RejectCreatesRejectedResponse()
    {
        var actor = GovernanceActorContext.Human("user-123", "Chris");
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Reject(
            request,
            actor,
            acknowledgmentId: "acknowledgment-123");

        Assert.False(acknowledgment.Acknowledged);
        Assert.True(acknowledgment.Rejected);
        Assert.Equal("ACK-001", acknowledgment.AcknowledgmentCode);
    }

    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Accept"/> method generates a non-empty acknowledgment ID when one is not provided.
    /// </summary>
    [Fact]
    public void CreateGeneratesAcknowledgmentIdWhenMissing()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor);

        Assert.False(string.IsNullOrWhiteSpace(acknowledgment.AcknowledgmentId));
    }

    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Accept"/> method generates a non-empty acknowledgment ID when a whitespace identifier is provided.
    /// </summary>
    [Fact]
    public void CreateGeneratesAcknowledgmentIdWhenIdentifierIsWhitespace()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            acknowledgmentId: "   ");

        Assert.False(string.IsNullOrWhiteSpace(acknowledgment.AcknowledgmentId));
    }

    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Accept"/> method trims a provided acknowledgment identifier.
    /// </summary>
    [Fact]
    public void CreateTrimsAcknowledgmentId()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            acknowledgmentId: " acknowledgment-123 ");

        Assert.Equal("acknowledgment-123", acknowledgment.AcknowledgmentId);
    }

    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Accept"/> method normalizes the provided timestamp to UTC.
    /// </summary>
    [Fact]
    public void CreateNormalizesTimestampToUtc()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            occurredUtc: new DateTimeOffset(2026, 6, 4, 7, 0, 0, TimeSpan.FromHours(-5)));

        Assert.Equal(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero), acknowledgment.OccurredUtc);
    }

    /// <summary>
    /// Verifies that optional actor and request trace values normalize to null when the source values are absent.
    /// </summary>
    [Fact]
    public void CreateNormalizesMissingOptionalValuesToNull()
    {
        var actor = GovernanceActorContext.Human("user-123");
        LiabilityHandshakeRequest request = CreateRequestWithoutTrace(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor);

        Assert.Null(acknowledgment.ActorDisplayName);
        Assert.Null(acknowledgment.CorrelationId);
        Assert.Null(acknowledgment.TraceId);
    }

    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Accept"/> method normalizes metadata keys and values by trimming whitespace and ignoring empty keys.
    /// </summary>
    [Fact]
    public void CreateNormalizesMetadata()
    {
        var actor = GovernanceActorContext.Service("service-123");
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            metadata: new Dictionary<string, string>
            {
                [" source "] = " unit-test ",
                [" "] = "ignored"
            });

        Assert.True(acknowledgment.HasMetadata);
        Assert.Equal("unit-test", acknowledgment.Metadata["source"]);
        Assert.False(acknowledgment.Metadata.ContainsKey(" "));
    }

    /// <summary>
    /// Verifies that null metadata is normalized to an empty metadata collection.
    /// </summary>
    [Fact]
    public void CreateWithNullMetadataReturnsNoMetadata()
    {
        var actor = GovernanceActorContext.Service("service-123");
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            metadata: null);

        Assert.False(acknowledgment.HasMetadata);
        Assert.Empty(acknowledgment.Metadata);
    }

    /// <summary>
    /// Verifies that empty metadata is normalized to an empty metadata collection.
    /// </summary>
    [Fact]
    public void CreateWithEmptyMetadataReturnsNoMetadata()
    {
        var actor = GovernanceActorContext.Service("service-123");
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            metadata: new Dictionary<string, string>());

        Assert.False(acknowledgment.HasMetadata);
        Assert.Empty(acknowledgment.Metadata);
    }

    /// <summary>
    /// Verifies that metadata containing only blank keys is normalized to an empty metadata collection.
    /// </summary>
    [Fact]
    public void CreateWithOnlyBlankMetadataKeysReturnsNoMetadata()
    {
        var actor = GovernanceActorContext.Service("service-123");
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored",
                ["\t"] = "also ignored"
            });

        Assert.False(acknowledgment.HasMetadata);
        Assert.Empty(acknowledgment.Metadata);
    }

    /// <summary>
    /// Verifies that duplicate keys after trimming use the later normalized value.
    /// </summary>
    [Fact]
    public void CreateWithDuplicateTrimmedMetadataKeysUsesLastValue()
    {
        var actor = GovernanceActorContext.Service("service-123");
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            metadata: new Dictionary<string, string>
            {
                [" source "] = " first ",
                ["source"] = " second "
            });

        Assert.True(acknowledgment.HasMetadata);
        _ = Assert.Single(acknowledgment.Metadata);
        Assert.Equal("second", acknowledgment.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that null metadata values are normalized to empty strings while preserving valid keys.
    /// </summary>
    [Fact]
    public void CreateWithNullMetadataValueStoresEmptyString()
    {
        var actor = GovernanceActorContext.Service("service-123");
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            metadata: new Dictionary<string, string>
            {
                [" source "] = null!
            });

        Assert.True(acknowledgment.HasMetadata);
        Assert.Equal(string.Empty, acknowledgment.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Accept"/> method throws an <see cref="ArgumentNullException"/> when the request parameter is null.
    /// </summary>
    [Fact]
    public void CreateThrowsForMissingRequest()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            LiabilityHandshakeAcknowledgment.Accept(
                request: null!,
                actor: GovernanceActorContext.System));
    }

    /// <summary>
    /// Verifies that the <see cref="LiabilityHandshakeAcknowledgment.Accept"/> method throws an <see cref="ArgumentNullException"/> when the actor parameter is null.
    /// </summary>
    [Fact]
    public void CreateThrowsForMissingActor()
    {
        LiabilityHandshakeRequest request = CreateRequest(GovernanceActorContext.System);

        _ = Assert.Throws<ArgumentNullException>(() =>
            LiabilityHandshakeAcknowledgment.Accept(
                request,
                actor: null!));
    }

    /// <summary>
    /// Verifies that the default acknowledgment timestamp is generated in UTC and falls within the creation window.
    /// </summary>
    [Fact]
    public void AcceptWithoutTimestampUsesCurrentUtcTimestamp()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        LiabilityHandshakeRequest request = CreateRequest(actor);
        DateTimeOffset beforeCreate = DateTimeOffset.UtcNow;

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor);

        DateTimeOffset afterCreate = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, acknowledgment.OccurredUtc.Offset);
        Assert.InRange(acknowledgment.OccurredUtc, beforeCreate, afterCreate);
    }

    /// <summary>
    /// Verifies that mutating source metadata after creation does not change acknowledgment metadata.
    /// </summary>
    [Fact]
    public void CreateDoesNotAliasSourceMetadata()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        LiabilityHandshakeRequest request = CreateRequest(actor);
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [" source "] = " original "
        };

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            metadata: metadata);

        metadata[" source "] = " mutated ";
        metadata[" other "] = " added ";

        _ = Assert.Single(acknowledgment.Metadata);
        Assert.Equal("original", acknowledgment.Metadata["source"]);
        Assert.False(acknowledgment.Metadata.ContainsKey("other"));
    }

    /// <summary>
    /// Verifies that metadata cannot be mutated through dictionary casts.
    /// </summary>
    [Fact]
    public void MetadataCannotBeMutatedThroughDictionaryCasts()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor,
            metadata: new Dictionary<string, string>
            {
                [" source "] = " unit-test "
            });

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(acknowledgment.Metadata);

        _ = Assert.Single(acknowledgment.Metadata);
        Assert.Equal("unit-test", acknowledgment.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that empty metadata cannot be mutated through dictionary casts.
    /// </summary>
    [Fact]
    public void EmptyMetadataCannotBeMutatedThroughDictionaryCasts()
    {
        GovernanceActorContext actor = GovernanceActorContext.System;
        LiabilityHandshakeRequest request = CreateRequest(actor);

        var acknowledgment = LiabilityHandshakeAcknowledgment.Accept(
            request,
            actor);

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(acknowledgment.Metadata);

        Assert.False(acknowledgment.HasMetadata);
        Assert.Empty(acknowledgment.Metadata);
    }

    private static LiabilityHandshakeRequest CreateRequest(IGovernanceActorContext actor)
    {
        return LiabilityHandshakeRequest.Create(
            actor,
            "document.approve",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand this action is consequential.",
            LiabilityHandshakeRiskLevel.High,
            handshakeId: "handshake-123",
            correlationId: "correlation-123",
            traceId: "trace-456");
    }

    private static LiabilityHandshakeRequest CreateRequestWithoutTrace(IGovernanceActorContext actor)
    {
        return LiabilityHandshakeRequest.Create(
            actor,
            "document.approve",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand this action is consequential.",
            LiabilityHandshakeRiskLevel.High,
            handshakeId: "handshake-123");
    }
}
