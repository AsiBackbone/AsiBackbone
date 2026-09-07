using AsiBackbone.Core.Signing;
using AsiBackbone.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Signing.LocalDevelopment.Tests;

/// <summary>
/// Tests the guards that keep the ephemeral local-development signer out of production and stop it from silently
/// returning unsigned artifacts.
/// </summary>
public sealed class LocalDevelopmentSigningProductionGuardTests
{
    /// <summary>
    /// Verifies that registering the provider in a production environment throws.
    /// </summary>
    /// <remarks>
    /// The analyzer only reports a call it can read inside an environment branch, so an unconditional registration
    /// reaching production needed a runtime guard as well.
    /// </remarks>
    [Fact]
    public void UseLocalDevelopmentSigningThrowsInProduction()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateBuilder().UseLocalDevelopmentSigning(
                LocalDevelopmentSigningOptions.Create(environmentName: "Production")));

        Assert.Contains("Production", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the production guard is case-insensitive.
    /// </summary>
    [Fact]
    public void UseLocalDevelopmentSigningThrowsInProductionRegardlessOfCasing()
    {
        _ = Assert.Throws<InvalidOperationException>(
            () => CreateBuilder().UseLocalDevelopmentSigning(
                LocalDevelopmentSigningOptions.Create(environmentName: "PRODUCTION")));
    }

    /// <summary>
    /// Verifies that a host can opt in deliberately.
    /// </summary>
    [Fact]
    public void UseLocalDevelopmentSigningAllowsProductionWhenExplicitlyPermitted()
    {
        IAsiBackboneBuilder builder = CreateBuilder().UseLocalDevelopmentSigning(
            LocalDevelopmentSigningOptions.Create(environmentName: "Production", allowInProduction: true));

        Assert.NotNull(builder);
    }

    /// <summary>
    /// Verifies that non-production environments register normally.
    /// </summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void UseLocalDevelopmentSigningAllowsNonProductionEnvironments(string environmentName)
    {
        IAsiBackboneBuilder builder = CreateBuilder().UseLocalDevelopmentSigning(
            LocalDevelopmentSigningOptions.Create(environmentName: environmentName));

        Assert.NotNull(builder);
    }

    /// <summary>
    /// Verifies that a rejected signing request throws when the host asked not to receive unsigned results.
    /// </summary>
    /// <remarks>
    /// Request-validation failures previously returned unsigned metadata even with ReturnUnsignedOnFailure set to false,
    /// so a caller that asked to be told about signing failures was not told about this one.
    /// </remarks>
    [Fact]
    public async Task SignAsyncThrowsOnRequestValidationFailureWhenUnsignedResultsAreRefused()
    {
        var options = LocalDevelopmentSigningOptions.Create(
            environmentName: "Development",
            returnUnsignedOnFailure: false);

        using var service = new LocalDevelopmentSigningService(options);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.SignAsync(
                new SigningRequest("abc123", "SHA-999", purpose: "audit"),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that the unsigned result is still available to hosts that asked for it.
    /// </summary>
    [Fact]
    public async Task SignAsyncReturnsUnsignedResultOnRequestValidationFailureWhenPermitted()
    {
        var options = LocalDevelopmentSigningOptions.Create(
            environmentName: "Development",
            returnUnsignedOnFailure: true);

        using var service = new LocalDevelopmentSigningService(options);

        SigningResult result = await service.SignAsync(
            new SigningRequest("abc123", "SHA-999", purpose: "audit"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
    }

    private static AsiBackboneBuilder CreateBuilder()
    {
        return new AsiBackboneBuilder(new ServiceCollection());
    }
}
