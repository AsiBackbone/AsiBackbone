using AsiBackbone.Core.Signing;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.Signing.ManagedKey;

/// <summary>Registers public-key governance signature verification.</summary>
public static class KeySignatureVerificationServiceCollectionExtensions
{
    /// <summary>Registers configured public keys and the public-key verifier.</summary>
    public static IServiceCollection AddAsiBackboneKeySignatureVerification(
        this IServiceCollection services,
        Action<KeySignatureVerificationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        KeySignatureVerificationOptions options = new();
        configure(options);
        options.Validate();
        _ = services.AddSingleton(options);
        _ = services.AddSingleton<IGovernanceVerificationKeyResolver, ConfiguredGovernanceVerificationKeyResolver>();
        return AddVerifier(services);
    }

    /// <summary>Registers a host key resolver and the public-key verifier.</summary>
    public static IServiceCollection AddAsiBackboneKeySignatureVerification<TResolver>(
        this IServiceCollection services,
        Action<KeySignatureVerificationOptions>? configure = null)
        where TResolver : class, IGovernanceVerificationKeyResolver
    {
        ArgumentNullException.ThrowIfNull(services);
        KeySignatureVerificationOptions options = new();
        configure?.Invoke(options);
        options.Validate();
        _ = services.AddSingleton(options);
        _ = services.AddSingleton<IGovernanceVerificationKeyResolver, TResolver>();
        return AddVerifier(services);
    }

    private static IServiceCollection AddVerifier(IServiceCollection services)
    {
        _ = services.AddSingleton<PublicKeySignatureVerificationService>();
        _ = services.AddSingleton<IGovernanceSignatureVerificationService>(provider =>
            provider.GetRequiredService<PublicKeySignatureVerificationService>());
        return services;
    }

    private sealed class ConfiguredGovernanceVerificationKeyResolver(KeySignatureVerificationOptions options)
        : IGovernanceVerificationKeyResolver
    {
        public ValueTask<bool> IsKnownKeyAsync(string keyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(options.Keys.Any(candidate =>
                string.Equals(candidate.KeyId, keyId, StringComparison.Ordinal)));
        }

        public ValueTask<GovernanceVerificationKey?> ResolveAsync(
            string keyId,
            string keyVersion,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GovernanceVerificationKey? key = options.Keys.SingleOrDefault(candidate =>
                string.Equals(candidate.KeyId, keyId, StringComparison.Ordinal)
                && string.Equals(candidate.KeyVersion, keyVersion, StringComparison.Ordinal));
            return ValueTask.FromResult(key);
        }
    }
}
