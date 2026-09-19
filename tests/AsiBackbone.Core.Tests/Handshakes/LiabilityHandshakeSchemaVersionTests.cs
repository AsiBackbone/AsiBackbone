using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Handshakes;
using AsiBackbone.Core.Serialization;
using Xunit;

namespace AsiBackbone.Core.Tests.Handshakes;

/// <summary>
/// Unit tests for stable liability handshake schema version serialization.
/// </summary>
public sealed class LiabilityHandshakeSchemaVersionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Verifies that handshake requests default to the initial stable schema version and serialize it explicitly.
    /// </summary>
    [Fact]
    public void RequestDefaultsAndSerializesStableSchemaVersion()
    {
        LiabilityHandshakeRequest request = CreateRequest();

        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, request.SchemaVersion);
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, ReadSerializedSchemaVersion(request));
    }

    /// <summary>
    /// Verifies that handshake requests preserve an explicit schema version and serialize it explicitly.
    /// </summary>
    [Fact]
    public void RequestPreservesAndSerializesExplicitSchemaVersion()
    {
        LiabilityHandshakeRequest request = CreateRequest(schemaVersion: " 1.1-test ");

        Assert.Equal("1.1-test", request.SchemaVersion);
        Assert.Equal("1.1-test", ReadSerializedSchemaVersion(request));
    }

    /// <summary>
    /// Verifies that handshake acknowledgments default to the initial stable schema version and serialize it explicitly.
    /// </summary>
    [Fact]
    public void AcknowledgmentDefaultsAndSerializesStableSchemaVersion()
    {
        LiabilityHandshakeAcknowledgment acknowledgment = CreateAcknowledgment();

        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, acknowledgment.SchemaVersion);
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, ReadSerializedSchemaVersion(acknowledgment));
    }

    /// <summary>
    /// Verifies that handshake acknowledgments preserve an explicit schema version and serialize it explicitly.
    /// </summary>
    [Fact]
    public void AcknowledgmentPreservesAndSerializesExplicitSchemaVersion()
    {
        LiabilityHandshakeAcknowledgment acknowledgment = CreateAcknowledgment(schemaVersion: " 1.1-test ");

        Assert.Equal("1.1-test", acknowledgment.SchemaVersion);
        Assert.Equal("1.1-test", ReadSerializedSchemaVersion(acknowledgment));
    }

    private static LiabilityHandshakeRequest CreateRequest(string? schemaVersion = null)
    {
        return LiabilityHandshakeRequest.Create(
            GovernanceActorContext.Human("actor-123", "Test Actor"),
            "document.approve",
            "ack.required",
            "Acknowledgment is required.",
            "ACK-001",
            "I understand this action is consequential.",
            LiabilityHandshakeRiskLevel.High,
            riskCategory: "administrative",
            handshakeId: "handshake-123",
            correlationId: "correlation-123",
            traceId: "trace-456",
            policyVersion: "v1",
            policyHash: "hash-abc",
            schemaVersion: schemaVersion);
    }

    private static LiabilityHandshakeAcknowledgment CreateAcknowledgment(string? schemaVersion = null)
    {
        LiabilityHandshakeRequest request = CreateRequest();

        return LiabilityHandshakeAcknowledgment.Accept(
            request,
            GovernanceActorContext.Human("actor-123", "Test Actor"),
            acknowledgmentId: "ack-123",
            occurredUtc: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            schemaVersion: schemaVersion);
    }

    private static string ReadSerializedSchemaVersion<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);

        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty("schemaVersion").GetString()!;
    }
}
