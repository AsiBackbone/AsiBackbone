using AsiBackbone.Core.CapabilityGrants;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityGrants;

/// <summary>
/// Tests for the <see cref="CapabilityGrant"/> class, focusing on the branch of the code that creates and validates capability grant grants.
/// </summary>
public sealed class CapabilityGrantBranchTests
{
    private static readonly DateTimeOffset IssuedUtc = new(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresUtc = new(2026, 6, 16, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Tests that the <see cref="CapabilityGrant.Create"/> method correctly normalizes scopes, metadata, and optional bindings, and that the resulting grant has the expected properties.
    /// </summary>
    [Fact]
    public void CreateNormalizesScopesMetadataAndOptionalBindings()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [" region "] = " us-la ",
            [" "] = " ignored ",
            ["nullable"] = null!
        };

        var grant = CapabilityGrant.Create(
            tokenId: " grant-1 ",
            issuer: " issuer-1 ",
            audience: " gateway-1 ",
            scopes: [" robotics.write ", "robotics.read", " ", "robotics.read"],
            issuedUtc: IssuedUtc.ToOffset(TimeSpan.FromHours(-5)),
            expiresUtc: ExpiresUtc.ToOffset(TimeSpan.FromHours(-5)),
            notBeforeUtc: IssuedUtc.AddMinutes(1).ToOffset(TimeSpan.FromHours(-5)),
            subjectId: " subject-1 ",
            operationName: " robotics.execute ",
            policyVersion: " policy-v1 ",
            policyHash: " policy-hash ",
            acknowledgmentId: " ack-1 ",
            handshakeId: " handshake-1 ",
            gatewayBinding: " gateway-binding ",
            resourceBinding: " robot-arm-1 ",
            metadata: metadata,
            schemaVersion: " stable-v1 ");

        Assert.Equal("grant-1", grant.TokenId);
        Assert.Equal("issuer-1", grant.Issuer);
        Assert.Equal("gateway-1", grant.Audience);
        Assert.Collection(
            grant.Scopes,
            scope => Assert.Equal("robotics.read", scope),
            scope => Assert.Equal("robotics.write", scope));
        Assert.Equal(IssuedUtc, grant.IssuedUtc);
        Assert.Equal(IssuedUtc.AddMinutes(1), grant.NotBeforeUtc);
        Assert.Equal(ExpiresUtc, grant.ExpiresUtc);
        Assert.Equal("subject-1", grant.SubjectId);
        Assert.Equal("robotics.execute", grant.OperationName);
        Assert.Equal("policy-v1", grant.PolicyVersion);
        Assert.Equal("policy-hash", grant.PolicyHash);
        Assert.True(grant.HasAcknowledgmentReference);
        Assert.True(grant.HasHandshakeReference);
        Assert.True(grant.HasMetadata);
        Assert.Equal("us-la", grant.Metadata["region"]);
        Assert.Equal(string.Empty, grant.Metadata["nullable"]);
        Assert.False(grant.Metadata.ContainsKey(string.Empty));
    }

    /// <summary>
    /// Tests that the <see cref="CapabilityGrant.Create"/> method allows creating a grant with minimal required parameters and correctly reports missing optional references and metadata.
    /// </summary>
    [Fact]
    public void CreateAllowsMinimalGrantAndReportsMissingOptionalReferences()
    {
        var grant = CapabilityGrant.Create(
            tokenId: "grant-1",
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["robotics.read"],
            issuedUtc: IssuedUtc,
            expiresUtc: ExpiresUtc,
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored"
            });

        Assert.False(grant.HasAcknowledgmentReference);
        Assert.False(grant.HasHandshakeReference);
        Assert.False(grant.HasMetadata);
        Assert.Empty(grant.Metadata);
        Assert.Null(grant.SubjectId);
        Assert.Null(grant.OperationName);
        Assert.Null(grant.PolicyVersion);
        Assert.Null(grant.PolicyHash);
        Assert.Null(grant.GatewayBinding);
        Assert.Null(grant.ResourceBinding);
    }

    /// <summary>
    /// Tests that the <see cref="CapabilityGrant.Create"/> method throws an <see cref="ArgumentNullException"/> when provided with null scopes, ensuring that scopes are required for creating a valid grant.
    /// </summary>
    [Fact]
    public void CreateRejectsNullScopes()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            CapabilityGrant.Create(
                tokenId: "grant-1",
                issuer: "issuer-1",
                audience: "gateway-1",
                scopes: null!,
                issuedUtc: IssuedUtc,
                expiresUtc: ExpiresUtc));
    }

    /// <summary>
    /// Tests that the <see cref="CapabilityGrant.Create"/> method throws an <see cref="ArgumentException"/> when provided with empty or whitespace-only scopes, ensuring that valid scopes are required for creating a grant.
    /// </summary>
    [Fact]
    public void CreateRejectsEmptyNormalizedScopes()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CapabilityGrant.Create(
                tokenId: "grant-1",
                issuer: "issuer-1",
                audience: "gateway-1",
                scopes: [" ", ""],
                issuedUtc: IssuedUtc,
                expiresUtc: ExpiresUtc));
    }

    /// <summary>
    /// Tests that the <see cref="CapabilityGrant.Create"/> method throws an <see cref="ArgumentOutOfRangeException"/> when the not-before timestamp is after the expiration timestamp.
    /// </summary>
    [Fact]
    public void CreateRejectsNotBeforeAfterExpiration()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CapabilityGrant.Create(
                tokenId: "grant-1",
                issuer: "issuer-1",
                audience: "gateway-1",
                scopes: ["robotics.read"],
                issuedUtc: IssuedUtc,
                expiresUtc: ExpiresUtc,
                notBeforeUtc: ExpiresUtc.AddTicks(1)));
    }

    /// <summary>
    /// Tests that the <see cref="CapabilityGrant.Create"/> method throws an <see cref="ArgumentOutOfRangeException"/> when the issued timestamp is after the expiration timestamp.
    /// </summary>
    [Fact]
    public void CreateRejectsIssuedAfterExpiration()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CapabilityGrant.Create(
                tokenId: "grant-1",
                issuer: "issuer-1",
                audience: "gateway-1",
                scopes: ["robotics.read"],
                issuedUtc: ExpiresUtc.AddTicks(1),
                expiresUtc: ExpiresUtc));
    }
}
