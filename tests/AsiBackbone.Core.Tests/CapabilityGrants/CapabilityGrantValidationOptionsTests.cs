using AsiBackbone.Core.CapabilityGrants;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityGrants;

/// <summary>
/// Unit tests for <see cref="CapabilityGrantValidationOptions"/> class.
/// </summary>
public sealed class CapabilityGrantValidationOptionsTests
{
    /// <summary>
    /// Tests that the <see cref="CapabilityGrantValidationOptions.Create"/> method correctly normalizes optional bindings and scopes, ensuring that whitespace is trimmed and duplicates are removed.
    /// </summary>
    [Fact]
    public void CreateNormalizesOptionalBindingsAndScopes()
    {
        DateTimeOffset localValidationTime = new(2026, 6, 16, 7, 0, 0, TimeSpan.FromHours(-5));

        var options = CapabilityGrantValidationOptions.Create(
            issuer: " issuer-1 ",
            audience: " gateway-1 ",
            scopes: [" robotics.write ", "robotics.read", " ", "robotics.read"],
            validationUtc: localValidationTime,
            policyVersion: " policy-v1 ",
            policyHash: " policy-hash ",
            acknowledgmentId: " ack-1 ",
            handshakeId: " handshake-1 ",
            gatewayBinding: " gateway-binding ",
            resourceBinding: " robot-arm-1 ",
            requireProof: true,
            requireAcknowledgmentReference: true,
            requireUseCheck: true,
            maxUseCount: 3);

        Assert.Equal("issuer-1", options.Issuer);
        Assert.Equal("gateway-1", options.Audience);
        Assert.Collection(
            options.Scopes,
            scope => Assert.Equal("robotics.read", scope),
            scope => Assert.Equal("robotics.write", scope));
        Assert.Equal(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero), options.ValidationUtc);
        Assert.Equal("policy-v1", options.PolicyVersion);
        Assert.Equal("policy-hash", options.PolicyHash);
        Assert.Equal("ack-1", options.AcknowledgmentId);
        Assert.Equal("handshake-1", options.HandshakeId);
        Assert.Equal("gateway-binding", options.GatewayBinding);
        Assert.Equal("robot-arm-1", options.ResourceBinding);
        Assert.True(options.RequireProof);
        Assert.True(options.RequireAcknowledgmentReference);
        Assert.True(options.RequireUseCheck);
        Assert.Equal(3, options.MaxUseCount);
    }

    /// <summary>
    /// Tests that the <see cref="CapabilityGrantValidationOptions.Create"/> method uses empty scopes and null bindings for missing or whitespace-only inputs, ensuring that the options are properly initialized.
    /// </summary>
    [Fact]
    public void CreateUsesEmptyScopesAndNullBindingsForMissingOrWhitespaceInputs()
    {
        var options = CapabilityGrantValidationOptions.Create(
            issuer: " ",
            audience: " ",
            scopes: null,
            policyVersion: " ",
            policyHash: " ",
            acknowledgmentId: " ",
            handshakeId: " ",
            gatewayBinding: " ",
            resourceBinding: " ");

        Assert.Null(options.Issuer);
        Assert.Null(options.Audience);
        Assert.Empty(options.Scopes);
        Assert.Null(options.ValidationUtc);
        Assert.Null(options.PolicyVersion);
        Assert.Null(options.PolicyHash);
        Assert.Null(options.AcknowledgmentId);
        Assert.Null(options.HandshakeId);
        Assert.Null(options.GatewayBinding);
        Assert.Null(options.ResourceBinding);
        Assert.False(options.RequireProof);
        Assert.False(options.RequireAcknowledgmentReference);
        Assert.False(options.RequireUseCheck);
        Assert.Equal(1, options.MaxUseCount);
    }

    /// <summary>
    /// Tests that the <see cref="CapabilityGrantValidationOptions.Create"/> method uses empty scopes when all provided scope entries normalize away to empty strings, ensuring that the options are properly initialized with no scopes.
    /// </summary>
    [Fact]
    public void CreateUsesEmptyScopesWhenAllScopeEntriesNormalizeAway()
    {
        var options = CapabilityGrantValidationOptions.Create(scopes: [" ", ""]);

        Assert.Empty(options.Scopes);
    }

    /// <summary>
    /// Tests that the <see cref="CapabilityGrantValidationOptions.Create"/> method throws an <see cref="ArgumentOutOfRangeException"/> when a non-positive maximum use count is provided, ensuring that the options enforce valid usage constraints.
    /// </summary>
    [Fact]
    public void CreateRejectsNonPositiveMaximumUseCount()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CapabilityGrantValidationOptions.Create(maxUseCount: 0));
    }
}
