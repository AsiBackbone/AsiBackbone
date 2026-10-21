namespace AsiBackbone.Signing.ManagedKey;

/// <summary>Resolves trusted public verification keys by stable identifier and version.</summary>
public interface IGovernanceVerificationKeyResolver
{
    /// <summary>Determines whether the key identifier is known independently of a particular version.</summary>
    ValueTask<bool> IsKnownKeyAsync(string keyId, CancellationToken cancellationToken = default);

    /// <summary>Resolves a trusted key version, or returns <see langword="null" /> when it is not trusted.</summary>
    ValueTask<GovernanceVerificationKey?> ResolveAsync(
        string keyId,
        string keyVersion,
        CancellationToken cancellationToken = default);
}
