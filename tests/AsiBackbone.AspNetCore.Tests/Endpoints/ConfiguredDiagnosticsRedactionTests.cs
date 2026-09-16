using System.Reflection;
using AsiBackbone.AspNetCore.Endpoints;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Verifies configured and built-in development-diagnostics metadata redaction.
/// </summary>
public sealed class ConfiguredDiagnosticsRedactionTests
{
    private static readonly MethodInfo ShouldRedactMethod = typeof(AsiBackboneEndpointGovernanceDevelopmentDiagnostics)
        .GetMethod("ShouldRedactMetadataValue", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Verifies that configured metadata keys are matched case-insensitively.
    /// </summary>
    [Fact]
    public void ConfiguredMetadataKeyIsRedactedCaseInsensitively()
    {
        var options = new EndpointGovernanceOptions();
        _ = options.DevelopmentDiagnosticsRedactedMetadataKeys.Add("tenant.reference");

        Assert.True(ShouldRedact(options, "TENANT.REFERENCE"));
    }

    /// <summary>
    /// Verifies that built-in sensitive-name fragments cause metadata values to be redacted.
    /// </summary>
    /// <param name="key">The metadata key to evaluate.</param>
    [Theory]
    [InlineData("clientSecret")]
    [InlineData("api_token")]
    [InlineData("database.password")]
    [InlineData("auth.credential")]
    [InlineData("request.cookie")]
    [InlineData("authorization.context")]
    [InlineData("signing.key")]
    public void SensitiveNameMetadataKeyIsRedacted(string key)
    {
        Assert.True(ShouldRedact(new EndpointGovernanceOptions(), key));
    }

    private static bool ShouldRedact(EndpointGovernanceOptions options, string key)
    {
        return (bool)ShouldRedactMethod.Invoke(null, [options, key])!;
    }
}
