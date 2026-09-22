using AsiBackbone.Core.Acknowledgments;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Decisions;
using Xunit;

namespace AsiBackbone.Core.Tests.Acknowledgments;

/// <summary>
/// Unit tests for the <see cref="AcknowledgmentRequest"/> class, which represents a request for a acknowledgment in the ASI Backbone system.
/// </summary>
public sealed class AcknowledgmentRequestTests
{
    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method correctly initializes all required fields and normalizes input values as expected.
    /// </summary>
    [Fact]
    public void CreateStoresRequiredFields()
    {
        var actor = GovernanceActorContext.Human(" user-123 ", " Chris ");

        var request = AcknowledgmentRequest.Create(
            actor,
            " document.approve ",
            " ack.required ",
            " Acknowledgment is required. ",
            " ACK-001 ",
            " I understand this action is consequential. ",
            AcknowledgmentRiskLevel.High,
            riskCategory: " administrative ",
            handshakeId: " handshake-123 ",
            correlationId: " correlation-123 ",
            traceId: " trace-456 ",
            policyVersion: " v1 ",
            policyHash: " hash-abc ");

        Assert.Equal("handshake-123", request.HandshakeId);
        Assert.Equal("user-123", request.ActorId);
        Assert.Equal(GovernanceActorType.Human, request.ActorType);
        Assert.Equal("Chris", request.ActorDisplayName);
        Assert.Equal("document.approve", request.OperationName);
        Assert.Equal("ack.required", request.ReasonCode);
        Assert.Equal("Acknowledgment is required.", request.Message);
        Assert.Equal("ACK-001", request.RequiredAcknowledgmentCode);
        Assert.Equal("I understand this action is consequential.", request.RequiredAcknowledgmentText);
        Assert.Equal(AcknowledgmentRiskLevel.High, request.RiskLevel);
        Assert.Equal("administrative", request.RiskCategory);
        Assert.Equal("correlation-123", request.CorrelationId);
        Assert.Equal("trace-456", request.TraceId);
        Assert.Equal("v1", request.PolicyVersion);
        Assert.Equal("hash-abc", request.PolicyHash);
    }

    /// <summary>
    /// Verifies that if a handshake ID is not provided when creating a <see cref="AcknowledgmentRequest"/>, a new unique ID is generated automatically.
    /// </summary>
    [Fact]
    public void CreateGeneratesHandshakeIdWhenMissing()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.System,
            "system.sync",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand.");

        Assert.False(string.IsNullOrWhiteSpace(request.HandshakeId));
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method normalizes metadata keys and values by trimming whitespace and ignoring empty keys, ensuring consistent handling of metadata entries.
    /// </summary>
    [Fact]
    public void CreateNormalizesMetadata()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.Service("service-123"),
            "external.call",
            "risk.high",
            "High risk operation.",
            "ACK-001",
            "I accept responsibility.",
            metadata: new Dictionary<string, string>
            {
                [" region "] = " us-la ",
                [" "] = "ignored",
                ["risk"] = " high "
            });

        Assert.True(request.HasMetadata);
        Assert.Equal("us-la", request.Metadata["region"]);
        Assert.Equal("high", request.Metadata["risk"]);
        Assert.False(request.Metadata.ContainsKey(" "));
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.FromDecision"/> method correctly copies relevant trace data from a given <see cref="GovernanceDecision"/> instance, ensuring that the resulting handshake request contains consistent correlation and tracing information for end-to-end visibility.
    /// </summary>
    [Fact]
    public void FromDecisionCopiesDecisionTraceData()
    {
        var actor = GovernanceActorContext.Human("user-123", "Chris");
        var decision = GovernanceDecision.RequireAcknowledgment(
            "ack.required",
            "Acknowledgment is required.",
            correlationId: "correlation-123",
            traceId: "trace-456",
            policyVersion: "v1",
            policyHash: "hash-abc");

        var request = AcknowledgmentRequest.FromDecision(
            actor,
            "document.approve",
            decision,
            "ACK-001",
            "I understand this action is consequential.",
            AcknowledgmentRiskLevel.Medium,
            riskCategory: "administrative",
            handshakeId: "handshake-123");

        Assert.Equal("handshake-123", request.HandshakeId);
        Assert.Equal("ack.required", request.ReasonCode);
        Assert.Equal("Acknowledgment is required.", request.Message);
        Assert.Equal("correlation-123", request.CorrelationId);
        Assert.Equal("trace-456", request.TraceId);
        Assert.Equal("v1", request.PolicyVersion);
        Assert.Equal("hash-abc", request.PolicyHash);
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method throws an <see cref="ArgumentNullException"/> when a null actor context is provided, ensuring that the method enforces the requirement for a valid actor to be associated with each handshake request.
    /// </summary>
    [Fact]
    public void CreateThrowsForMissingActor()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            AcknowledgmentRequest.Create(
                actor: null!,
                operationName: "document.approve",
                reasonCode: "ack.required",
                message: "Acknowledgment is required.",
                requiredAcknowledgmentCode: "ACK-001",
                requiredAcknowledgmentText: "I understand."));
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method throws an <see cref="ArgumentException"/> when an empty or whitespace-only operation name is provided, ensuring that the method enforces the requirement for a valid operation name to be specified for each handshake request.
    /// </summary>
    /// <param name="operationName">
    /// The operation name to validate.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateThrowsForMissingOperationName(string operationName)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            AcknowledgmentRequest.Create(
                GovernanceActorContext.System,
                operationName,
                "ack.required",
                "Acknowledgment is required.",
                "ACK-001",
                "I understand."));
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method throws an <see cref="ArgumentException"/> when an empty or whitespace-only reason code is provided, ensuring that the method enforces the requirement for a valid reason code to be specified for each handshake request.
    /// </summary>
    /// <param name="reasonCode">
    /// The reason code to validate.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateThrowsForMissingReasonCode(string reasonCode)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            AcknowledgmentRequest.Create(
                GovernanceActorContext.System,
                "system.sync",
                reasonCode,
                "Acknowledgment is required.",
                "ACK-001",
                "I understand."));
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method throws an <see cref="ArgumentException"/> when an empty or whitespace-only required acknowledgment code is provided, ensuring that the method enforces the requirement for a valid required acknowledgment code to be specified for each handshake request.
    /// </summary>
    /// <param name="acknowledgmentCode">
    /// The required acknowledgment code to validate.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateThrowsForMissingRequiredAcknowledgmentCode(string acknowledgmentCode)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            AcknowledgmentRequest.Create(
                GovernanceActorContext.System,
                "system.sync",
                "ack.required",
                "Acknowledgment is required.",
                acknowledgmentCode,
                "I understand."));
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.FromDecision"/> method uses default reason code and message values when the provided <see cref="GovernanceDecision"/> does not specify any reasons, ensuring that the resulting handshake request contains meaningful information even in cases where the decision lacks explicit reasoning details.
    /// </summary>
    [Fact]
    public void FromDecision_WithNoReasons_UsesDefaultReasonCodeAndMessage()
    {
        var request = AcknowledgmentRequest.FromDecision(
            GovernanceActorContext.System,
            "system.sync",
            GovernanceDecision.Allow(),
            "ACK-001",
            "I understand this action is consequential.",
            handshakeId: "handshake-123");

        Assert.Equal("handshake.required", request.ReasonCode);
        Assert.Equal("Acknowledgment is required before proceeding.", request.Message);
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method normalizes optional string fields by treating null, empty, or whitespace-only values as null, ensuring that the resulting handshake request has consistent handling of optional data and avoids storing meaningless or invalid values for these fields.
    /// </summary>
    [Fact]
    public void Create_WithWhitespaceOptionalFields_NormalizesToNull()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.System,
            "system.sync",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand.",
            riskCategory: " ",
            correlationId: " ",
            traceId: "",
            policyVersion: "\t",
            policyHash: " ");

        Assert.Null(request.RiskCategory);
        Assert.Null(request.CorrelationId);
        Assert.Null(request.TraceId);
        Assert.Null(request.PolicyVersion);
        Assert.Null(request.PolicyHash);
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method correctly handles null or empty metadata by treating it as having no metadata, ensuring that the resulting handshake request has consistent handling of metadata and does not contain any entries when null or empty metadata is provided.
    /// </summary>
    [Fact]
    public void Create_WithNullMetadata_HasNoMetadata()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.System,
            "system.sync",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand.",
            metadata: null);

        Assert.False(request.HasMetadata);
        Assert.Empty(request.Metadata);
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method correctly handles explicitly empty metadata by treating it as having no metadata, ensuring that the resulting handshake request has consistent handling of metadata and does not contain any entries when an empty metadata dictionary is provided.
    /// </summary>
    [Fact]
    public void Create_WithEmptyMetadata_HasNoMetadata()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.System,
            "system.sync",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand.",
            metadata: new Dictionary<string, string>());

        Assert.False(request.HasMetadata);
        Assert.Empty(request.Metadata);
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method correctly ignores metadata entries with blank keys by treating them as invalid and excluding them from the resulting handshake request, ensuring that the resulting handshake request has consistent handling of metadata and does not contain any entries with blank keys.
    /// </summary>
    [Fact]
    public void Create_WithOnlyBlankMetadataKeys_HasNoMetadata()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.System,
            "system.sync",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand.",
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored",
                ["\t"] = "also ignored"
            });

        Assert.False(request.HasMetadata);
        Assert.Empty(request.Metadata);
    }

    /// <summary>
    /// Verifies that the <see cref="AcknowledgmentRequest.Create"/> method correctly handles null metadata values by storing an empty string, ensuring that the resulting handshake request has consistent handling of metadata and avoids storing null values.
    /// </summary>
    [Fact]
    public void Create_WithNullMetadataValue_StoresEmptyString()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.System,
            "system.sync",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand.",
            metadata: new Dictionary<string, string>
            {
                [" source "] = null!
            });

        Assert.True(request.HasMetadata);
        Assert.Equal(string.Empty, request.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that metadata cannot be mutated through dictionary casts.
    /// </summary>
    [Fact]
    public void MetadataCannotBeMutatedThroughDictionaryCasts()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.System,
            "system.sync",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand.",
            metadata: new Dictionary<string, string>
            {
                [" source "] = " unit-test "
            });

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(request.Metadata);

        _ = Assert.Single(request.Metadata);
        Assert.Equal("unit-test", request.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that empty metadata cannot be mutated through dictionary casts.
    /// </summary>
    [Fact]
    public void EmptyMetadataCannotBeMutatedThroughDictionaryCasts()
    {
        var request = AcknowledgmentRequest.Create(
            GovernanceActorContext.System,
            "system.sync",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand.");

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(request.Metadata);

        Assert.False(request.HasMetadata);
        Assert.Empty(request.Metadata);
    }
}
