using AsiBackbone.Core.Signing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Signing.ManagedKey.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="ManagedKeySigningServiceCollectionExtensions" /> registration overloads.
/// </summary>
public sealed class ManagedKeySigningServiceCollectionExtensionsTests
{
    /// <summary>
    /// Verifies production registration with a client factory.
    /// </summary>
    [Fact]
    public void ProductionFactoryRegistrationUsesSingletonServicesAndFailsClosed()
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<IGovernanceSignatureVerificationService>(new StubVerificationService());
        var client = new StubManagedKeySigningClient();
        int factoryCalls = 0;

        IServiceCollection result = services.AddAsiBackboneManagedKeySigning(
            ConfigureValidOptions,
            _ =>
            {
                factoryCalls++;
                return client;
            });

        Assert.Same(services, result);
        AssertSingletonRegistrations(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        ManagedKeySigningService concrete = provider.GetRequiredService<ManagedKeySigningService>();
        IGovernanceSigningService abstraction = provider.GetRequiredService<IGovernanceSigningService>();

        Assert.Same(concrete, abstraction);
        Assert.Same(concrete, provider.GetRequiredService<ManagedKeySigningService>());
        Assert.Same(client, provider.GetRequiredService<IManagedKeySigningClient>());
        Assert.Same(client, provider.GetRequiredService<IManagedKeySigningClient>());
        Assert.Equal(1, factoryCalls);
        Assert.False(provider.GetRequiredService<ManagedKeySigningOptions>().ReturnUnsignedOnFailure);
    }

    /// <summary>
    /// Verifies production registration with an existing client.
    /// </summary>
    [Fact]
    public void ProductionRegisteredClientRegistrationUsesHostSingletonAndFailsClosed()
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<IGovernanceSignatureVerificationService>(new StubVerificationService());
        var client = new StubManagedKeySigningClient();
        _ = services.AddSingleton<IManagedKeySigningClient>(client);

        IServiceCollection result = services.AddAsiBackboneManagedKeySigning(ConfigureValidOptions);

        Assert.Same(services, result);
        AssertSingletonRegistrations(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        ManagedKeySigningService concrete = provider.GetRequiredService<ManagedKeySigningService>();

        Assert.Same(concrete, provider.GetRequiredService<IGovernanceSigningService>());
        Assert.Same(client, provider.GetRequiredService<IManagedKeySigningClient>());
        Assert.False(provider.GetRequiredService<ManagedKeySigningOptions>().ReturnUnsignedOnFailure);
    }

    /// <summary>
    /// Verifies production registration fails when no verification service is available.
    /// </summary>
    [Fact]
    public void ProductionRegistrationRequiresVerificationService()
    {
        string? originalDotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        string? originalAspNetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

            ServiceCollection services = new();
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddAsiBackboneManagedKeySigning(ConfigureValidOptions));

            Assert.Contains("IGovernanceSignatureVerificationService", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalDotnetEnvironment);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalAspNetEnvironment);
        }
    }

    /// <summary>
    /// Verifies production registration allows an explicit verification implementation.
    /// </summary>
    [Fact]
    public void ProductionRegistrationAllowsVerificationService()
    {
        string? originalDotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        string? originalAspNetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

            ServiceCollection services = new();
            _ = services.AddSingleton<IGovernanceSignatureVerificationService>(new StubVerificationService());

            IServiceCollection result = services.AddAsiBackboneManagedKeySigning(ConfigureValidOptions);

            Assert.Same(services, result);
            using ServiceProvider provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetRequiredService<IGovernanceSignatureVerificationService>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalDotnetEnvironment);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalAspNetEnvironment);
        }
    }

    /// <summary>
    /// Verifies local-validation registration with a client factory.
    /// </summary>
    [Fact]
    public void LocalValidationFactoryRegistrationForcesUnsignedFailures()
    {
        ServiceCollection services = new();
        var client = new StubManagedKeySigningClient();

        IServiceCollection result = services.AddAsiBackboneManagedKeySigningForLocalValidation(
            options =>
            {
                ConfigureValidOptions(options);
                options.ReturnUnsignedOnFailure = false;
            },
            _ => client);

        Assert.Same(services, result);
        AssertSingletonRegistrations(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        ManagedKeySigningService concrete = provider.GetRequiredService<ManagedKeySigningService>();

        Assert.Same(concrete, provider.GetRequiredService<IGovernanceSigningService>());
        Assert.Same(client, provider.GetRequiredService<IManagedKeySigningClient>());
        Assert.True(provider.GetRequiredService<ManagedKeySigningOptions>().ReturnUnsignedOnFailure);
    }

    /// <summary>
    /// Verifies local-validation registration with an existing client.
    /// </summary>
    [Fact]
    public void LocalValidationRegisteredClientRegistrationForcesUnsignedFailures()
    {
        ServiceCollection services = new();
        var client = new StubManagedKeySigningClient();
        _ = services.AddSingleton<IManagedKeySigningClient>(client);

        IServiceCollection result = services.AddAsiBackboneManagedKeySigningForLocalValidation(options =>
        {
            ConfigureValidOptions(options);
            options.ReturnUnsignedOnFailure = false;
        });

        Assert.Same(services, result);
        AssertSingletonRegistrations(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        ManagedKeySigningService concrete = provider.GetRequiredService<ManagedKeySigningService>();

        Assert.Same(concrete, provider.GetRequiredService<IGovernanceSigningService>());
        Assert.Same(client, provider.GetRequiredService<IManagedKeySigningClient>());
        Assert.True(provider.GetRequiredService<ManagedKeySigningOptions>().ReturnUnsignedOnFailure);
    }

    /// <summary>
    /// Verifies null argument guards for all registration forms.
    /// </summary>
    [Fact]
    public void RegistrationOverloadsRejectNullArguments()
    {
        ServiceCollection services = new();
        static IManagedKeySigningClient factory(IServiceProvider _)
        {
            return new StubManagedKeySigningClient();
        }

        Assert.Equal(
            "services",
            Assert.Throws<ArgumentNullException>(() =>
                ManagedKeySigningServiceCollectionExtensions.AddAsiBackboneManagedKeySigning(
                    null!, ConfigureValidOptions, factory)).ParamName);
        Assert.Equal(
            "configure",
            Assert.Throws<ArgumentNullException>(() =>
                services.AddAsiBackboneManagedKeySigning(null!, factory)).ParamName);
        Assert.Equal(
            "clientFactory",
            Assert.Throws<ArgumentNullException>(() =>
                services.AddAsiBackboneManagedKeySigning(ConfigureValidOptions, null!)).ParamName);

        Assert.Equal(
            "services",
            Assert.Throws<ArgumentNullException>(() =>
                ManagedKeySigningServiceCollectionExtensions.AddAsiBackboneManagedKeySigning(
                    null!, ConfigureValidOptions)).ParamName);
        Assert.Equal(
            "configure",
            Assert.Throws<ArgumentNullException>(() =>
                services.AddAsiBackboneManagedKeySigning(null!)).ParamName);

        Assert.Equal(
            "services",
            Assert.Throws<ArgumentNullException>(() =>
                ManagedKeySigningServiceCollectionExtensions.AddAsiBackboneManagedKeySigningForLocalValidation(
                    null!, ConfigureValidOptions, factory)).ParamName);
        Assert.Equal(
            "configure",
            Assert.Throws<ArgumentNullException>(() =>
                services.AddAsiBackboneManagedKeySigningForLocalValidation(null!, factory)).ParamName);
        Assert.Equal(
            "clientFactory",
            Assert.Throws<ArgumentNullException>(() =>
                services.AddAsiBackboneManagedKeySigningForLocalValidation(ConfigureValidOptions, null!)).ParamName);

        Assert.Equal(
            "services",
            Assert.Throws<ArgumentNullException>(() =>
                ManagedKeySigningServiceCollectionExtensions.AddAsiBackboneManagedKeySigningForLocalValidation(
                    null!, ConfigureValidOptions)).ParamName);
        Assert.Equal(
            "configure",
            Assert.Throws<ArgumentNullException>(() =>
                services.AddAsiBackboneManagedKeySigningForLocalValidation(null!)).ParamName);
    }

    private static void ConfigureValidOptions(ManagedKeySigningOptions options)
    {
        options.ProviderName = "managed-key-test";
        options.KeyId = "managed-key-1";
        options.KeyVersion = "v1";
        options.MaxRetryAttempts = 0;
        options.RetryDelay = TimeSpan.Zero;
    }

    private static void AssertSingletonRegistrations(IServiceCollection services)
    {
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ManagedKeySigningOptions)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ManagedKeySigningService)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IGovernanceSigningService)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IManagedKeySigningClient)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    private sealed class StubManagedKeySigningClient : IManagedKeySigningClient
    {
        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Registration tests do not invoke signing.");
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
