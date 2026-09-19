using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.DependencyInjection;

/// <summary>
/// Verifies strict-profile development-diagnostics behavior.
/// </summary>
public sealed class StrictGovernanceDiagnosticsTests
{
    /// <summary>
    /// Verifies that the strict profile disables metadata values in development diagnostics.
    /// </summary>
    [Fact]
    public void StrictProfileDisablesDevelopmentDiagnosticsMetadataValues()
    {
        ServiceCollection services = new();
        _ = services.AddAsiBackboneStrictGovernance();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        EndpointGovernanceOptions options = provider
            .GetRequiredService<IOptions<EndpointGovernanceOptions>>()
            .Value;

        Assert.False(options.IncludeDevelopmentDiagnosticsMetadataValues);
    }
}
