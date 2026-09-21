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

        // Registration holds a snapshot, not the caller's mutable instance.
        Assert.NotSame(configured, resolved);
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

    /// <summary>
    /// Verifies that changing the caller's options instance after registration does not change the registered
    /// options or the registered provider.
    /// </summary>
    [Fact]
    public async Task MutatingOptionsAfterRegistrationDoesNotChangeRegisteredProvider()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);
        var configured = LocalDevelopmentSigningOptions.Create(
            signatureAlgorithm: "LOCAL-REGISTERED-ALGORITHM",
            environmentName: "Development");

        _ = builder.UseLocalDevelopmentSigning(configured);

        configured.SignatureAlgorithm = "LOCAL-MUTATED-ALGORITHM";
        configured.KeyId = "mutated-key";
        configured.AllowInProduction = true;
        configured.EnvironmentName = "Production";

        using ServiceProvider provider = services.BuildServiceProvider();
        LocalDevelopmentSigningOptions resolved = provider.GetRequiredService<LocalDevelopmentSigningOptions>();
        IGovernanceSigningService signing = provider.GetRequiredService<IGovernanceSigningService>();

        Assert.Equal("LOCAL-REGISTERED-ALGORITHM", resolved.SignatureAlgorithm);
        Assert.Equal(LocalDevelopmentSigningOptions.DefaultKeyId, resolved.KeyId);
        Assert.False(resolved.AllowInProduction);
        Assert.Equal("Development", resolved.EnvironmentName);

        SigningResult result = await signing.SignAsync(
            new SigningRequest("snapshot-hash", hashAlgorithm: "SHA-256"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal("LOCAL-REGISTERED-ALGORITHM", result.Metadata.SignatureAlgorithm);
        Assert.Equal(LocalDevelopmentSigningOptions.DefaultKeyId, result.Metadata.KeyId);
    }

    /// <summary>
    /// Verifies that a <see cref="TimeProvider" /> registered by the host supplies the signing time.
    /// </summary>
    [Fact]
    public async Task RegisteredTimeProviderSuppliesSigningTime()
    {
        DateTimeOffset fixedUtc = new(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);
        ServiceCollection services = new();
        _ = services.AddSingleton<TimeProvider>(new FixedTimeProvider(fixedUtc));
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        _ = builder.UseLocalDevelopmentSigning(LocalDevelopmentSigningOptions.Create(environmentName: "Development"));

        using ServiceProvider provider = services.BuildServiceProvider();
        IGovernanceSigningService signing = provider.GetRequiredService<IGovernanceSigningService>();

        SigningResult result = await signing.SignAsync(
            new SigningRequest("clock-hash", hashAlgorithm: "SHA-256"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal(fixedUtc, result.Metadata.SignedUtc);
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
                && descriptor.ImplementationFactory is not null
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
