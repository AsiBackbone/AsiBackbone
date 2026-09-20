using AsiBackbone.Core.Signing;
using AsiBackbone.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Signing.ManagedKey.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="ManagedKeySigningBuilderExtensions" /> class.
/// </summary>
public sealed class ManagedKeySigningBuilderExtensionsTests
{
    /// <summary>
    /// Verifies that the factory overload registers configured managed-key signing services and returns the same builder.
    /// </summary>
    [Fact]
    public void UseManagedKeySigningWithFactoryRegistersServicesAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<IGovernanceSignatureVerificationService>(new StubVerificationService());
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);
        var client = new StubManagedKeySigningClient();
        bool configureInvoked = false;

        IAsiBackboneBuilder result = builder.UseManagedKeySigning(
            options =>
            {
                configureInvoked = true;
                ConfigureValidOptions(options);
                options.ProviderName = "managed-key-test";
                options.ReturnUnsignedOnFailure = true;
                options.MaxRetryAttempts = 0;
                options.RetryDelay = TimeSpan.Zero;
            },
            _ => client);

        Assert.Same(builder, result);
        Assert.True(configureInvoked);
        AssertSingletonRegistrations(services, expectsClientFactory: true);

        using ServiceProvider provider = services.BuildServiceProvider();
        ManagedKeySigningOptions options = provider.GetRequiredService<ManagedKeySigningOptions>();
        ManagedKeySigningService concrete = provider.GetRequiredService<ManagedKeySigningService>();
        IGovernanceSigningService signing = provider.GetRequiredService<IGovernanceSigningService>();
        IManagedKeySigningClient resolvedClient = provider.GetRequiredService<IManagedKeySigningClient>();

        Assert.Equal("managed-key-test", options.ProviderName);
        Assert.Equal("managed-key-1", options.KeyId);
        Assert.Equal("v1", options.KeyVersion);
        Assert.True(options.ReturnUnsignedOnFailure);
        Assert.Equal(0, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.Zero, options.RetryDelay);
        Assert.Same(concrete, signing);
        Assert.Same(client, resolvedClient);
    }

    /// <summary>
    /// Verifies that the existing-client overload uses the host-registered managed-key client.
    /// </summary>
    [Fact]
    public void UseManagedKeySigningWithRegisteredClientUsesHostClientAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<IGovernanceSignatureVerificationService>(new StubVerificationService());
        var client = new StubManagedKeySigningClient();
        _ = services.AddSingleton<IManagedKeySigningClient>(client);
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseManagedKeySigning(ConfigureValidOptions);

        Assert.Same(builder, result);
        AssertSingletonRegistrations(services, expectsClientFactory: false);

        using ServiceProvider provider = services.BuildServiceProvider();
        ManagedKeySigningOptions options = provider.GetRequiredService<ManagedKeySigningOptions>();
        ManagedKeySigningService concrete = provider.GetRequiredService<ManagedKeySigningService>();
        IGovernanceSigningService signing = provider.GetRequiredService<IGovernanceSigningService>();

        Assert.Equal("managed-key-1", options.KeyId);
        Assert.Equal("v1", options.KeyVersion);
        Assert.False(options.ReturnUnsignedOnFailure);
        Assert.Same(concrete, signing);
        Assert.Same(client, provider.GetRequiredService<IManagedKeySigningClient>());
    }

    /// <summary>
    /// Verifies that the factory overload rejects a null builder.
    /// </summary>
    [Fact]
    public void UseManagedKeySigningFactoryOverloadRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.UseManagedKeySigning(ConfigureValidOptions, _ => new StubManagedKeySigningClient()));

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that the registered-client overload rejects a null builder.
    /// </summary>
    [Fact]
    public void UseManagedKeySigningRegisteredClientOverloadRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.UseManagedKeySigning(ConfigureValidOptions));

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that managed-key registration rejects a null configuration callback.
    /// </summary>
    [Fact]
    public void UseManagedKeySigningRejectsNullConfigureCallback()
    {
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(new ServiceCollection());
        Action<ManagedKeySigningOptions>? configure = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            builder.UseManagedKeySigning(configure!, _ => new StubManagedKeySigningClient()));

        Assert.Equal("configure", exception.ParamName);
    }

    /// <summary>
    /// Verifies that the factory overload rejects a null client factory.
    /// </summary>
    [Fact]
    public void UseManagedKeySigningRejectsNullClientFactory()
    {
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(new ServiceCollection());
        Func<IServiceProvider, IManagedKeySigningClient>? clientFactory = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            builder.UseManagedKeySigning(ConfigureValidOptions, clientFactory!));

        Assert.Equal("clientFactory", exception.ParamName);
    }

    /// <summary>
    /// Verifies that invalid managed-key options fail before signing services are registered.
    /// </summary>
    [Fact]
    public void UseManagedKeySigningRejectsInvalidConfigurationBeforeRegistration()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            builder.UseManagedKeySigning(static options => options.KeyId = string.Empty));

        Assert.Equal("Managed-key signing key ID is required.", exception.Message);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(ManagedKeySigningOptions)
                || descriptor.ServiceType == typeof(ManagedKeySigningService)
                || descriptor.ServiceType == typeof(IGovernanceSigningService));
    }

    private static void ConfigureValidOptions(ManagedKeySigningOptions options)
    {
        options.KeyId = "managed-key-1";
        options.KeyVersion = "v1";
        options.RetryDelay = TimeSpan.Zero;
    }

    private static void AssertSingletonRegistrations(IServiceCollection services, bool expectsClientFactory)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ManagedKeySigningOptions)
                && descriptor.Lifetime == ServiceLifetime.Singleton
                && descriptor.ImplementationInstance is ManagedKeySigningOptions);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ManagedKeySigningService)
                && descriptor.ImplementationType == typeof(ManagedKeySigningService)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IGovernanceSigningService)
                && descriptor.ImplementationFactory is not null
                && descriptor.Lifetime == ServiceLifetime.Singleton);

        if (expectsClientFactory)
        {
            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType == typeof(IManagedKeySigningClient)
                    && descriptor.ImplementationFactory is not null
                    && descriptor.Lifetime == ServiceLifetime.Singleton);
        }
    }

    private sealed class StubManagedKeySigningClient : IManagedKeySigningClient
    {
        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The builder tests do not invoke managed-key signing.");
        }
    }

    private sealed class StubVerificationService : IGovernanceSignatureVerificationService
    {
        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(SignatureVerificationResult.Verified());
        }
    }
}
