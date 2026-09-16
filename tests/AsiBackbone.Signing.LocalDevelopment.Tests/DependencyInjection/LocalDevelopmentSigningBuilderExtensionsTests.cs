using AsiBackbone.Core.Signing;
using AsiBackbone.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Signing.LocalDevelopment.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="LocalDevelopmentSigningBuilderExtensions" /> class.
/// </summary>
public sealed class LocalDevelopmentSigningBuilderExtensionsTests
{
    /// <summary>
    /// Verifies that default local-development signing registration adds signing and verification services.
    /// </summary>
    [Fact]
    public void UseLocalDevelopmentSigningRegistersDefaultsAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseLocalDevelopmentSigning();

        Assert.Same(builder, result);
        AssertSingletonRegistrations(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        LocalDevelopmentSigningOptions options = provider.GetRequiredService<LocalDevelopmentSigningOptions>();
        LocalDevelopmentSigningService concrete = provider.GetRequiredService<LocalDevelopmentSigningService>();
        IGovernanceSigningService signing = provider.GetRequiredService<IGovernanceSigningService>();
        IGovernanceSignatureVerificationService verification =
            provider.GetRequiredService<IGovernanceSignatureVerificationService>();

        Assert.Equal(LocalDevelopmentSigningOptions.DefaultProviderName, options.ProviderName);
        Assert.Equal(LocalDevelopmentSigningOptions.DefaultKeyId, options.KeyId);
        Assert.Equal(LocalDevelopmentSigningOptions.DefaultKeyVersion, options.KeyVersion);
        Assert.Equal(LocalDevelopmentSigningOptions.DefaultSignatureAlgorithm, options.SignatureAlgorithm);
        Assert.Equal(LocalDevelopmentSigningOptions.DefaultKeySizeBits, options.KeySizeBits);
        Assert.True(options.ReturnUnsignedOnFailure);
        Assert.Same(concrete, signing);
        Assert.Same(concrete, verification);
    }

    /// <summary>
    /// Verifies that configured local-development options flow into dependency injection.
    /// </summary>
    [Fact]
    public void UseLocalDevelopmentSigningAppliesOptionsAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);
        var configured = LocalDevelopmentSigningOptions.Create(
            providerName: "local-test-provider",
            keyId: "local-test-key",
            keyVersion: "v2",
            signatureAlgorithm: "LOCAL-TEST-ALGORITHM",
            keySizeBits: 3072,
            returnUnsignedOnFailure: false);

        IAsiBackboneBuilder result = builder.UseLocalDevelopmentSigning(configured);

        Assert.Same(builder, result);
        AssertSingletonRegistrations(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        LocalDevelopmentSigningOptions resolved = provider.GetRequiredService<LocalDevelopmentSigningOptions>();
        LocalDevelopmentSigningService concrete = provider.GetRequiredService<LocalDevelopmentSigningService>();
        IGovernanceSigningService signing = provider.GetRequiredService<IGovernanceSigningService>();
        IGovernanceSignatureVerificationService verification =
            provider.GetRequiredService<IGovernanceSignatureVerificationService>();

        Assert.Same(configured, resolved);
        Assert.Equal("local-test-provider", resolved.ProviderName);
        Assert.Equal("local-test-key", resolved.KeyId);
        Assert.Equal("v2", resolved.KeyVersion);
        Assert.Equal("LOCAL-TEST-ALGORITHM", resolved.SignatureAlgorithm);
        Assert.Equal(3072, resolved.KeySizeBits);
        Assert.False(resolved.ReturnUnsignedOnFailure);
        Assert.Same(concrete, signing);
        Assert.Same(concrete, verification);
    }

    /// <summary>
    /// Verifies that the default overload rejects a null builder.
    /// </summary>
    [Fact]
    public void UseLocalDevelopmentSigningDefaultOverloadRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseLocalDevelopmentSigning());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that the configured overload rejects a null builder.
    /// </summary>
    [Fact]
    public void UseLocalDevelopmentSigningConfiguredOverloadRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;
        var options = LocalDevelopmentSigningOptions.Create();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseLocalDevelopmentSigning(options));

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that the configured overload rejects null options.
    /// </summary>
    [Fact]
    public void UseLocalDevelopmentSigningRejectsNullOptions()
    {
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(new ServiceCollection());
        LocalDevelopmentSigningOptions? options = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder.UseLocalDevelopmentSigning(options!));

        Assert.Equal("options", exception.ParamName);
    }

    private static void AssertSingletonRegistrations(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(LocalDevelopmentSigningOptions)
                && descriptor.Lifetime == ServiceLifetime.Singleton
                && descriptor.ImplementationInstance is LocalDevelopmentSigningOptions);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(LocalDevelopmentSigningService)
                && descriptor.ImplementationType == typeof(LocalDevelopmentSigningService)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IGovernanceSigningService)
                && descriptor.ImplementationFactory is not null
                && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IGovernanceSignatureVerificationService)
                && descriptor.ImplementationFactory is not null
                && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
