using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Signing.ManagedKey.Tests;

/// <summary>
/// Tests that the managed key signing service rejects a provider response whose key identity or algorithm differs from
/// the request.
/// </summary>
/// <remarks>
/// The signed result copies the provider's key identifier, key version, and signature algorithm into signing metadata, so
/// an unchecked substitution would be recorded as though it had been requested.
/// </remarks>
public sealed class ManagedKeySigningResponseBindingTests
{
    private static readonly DateTimeOffset SignedUtc = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that a response signed under a different key version than the pinned one is rejected.
    /// </summary>
    [Fact]
    public async Task SignAsyncRejectsADifferentKeyVersionThanRequested()
    {
        SigningResult result = await SignWithProviderResultAsync(
            ManagedKeySignResult.Create("signature", "TEST-SIGNATURE", "managed-key-1", "resolved-v9", SignedUtc));

        Assert.False(result.IsSigned);
        Assert.Equal("managedkey.signing.key-version-mismatch", result.Metadata.Metadata["failure_code"]);
    }

    /// <summary>
    /// Verifies that a response signed under a different key identifier is rejected.
    /// </summary>
    [Fact]
    public async Task SignAsyncRejectsADifferentKeyIdentifierThanRequested()
    {
        SigningResult result = await SignWithProviderResultAsync(
            ManagedKeySignResult.Create("signature", "TEST-SIGNATURE", "other-key", "v1", SignedUtc));

        Assert.False(result.IsSigned);
        Assert.Equal("managedkey.signing.key-mismatch", result.Metadata.Metadata["failure_code"]);
    }

    /// <summary>
    /// Verifies that a response signed with a different algorithm than the configured one is rejected.
    /// </summary>
    [Fact]
    public async Task SignAsyncRejectsADifferentSignatureAlgorithmThanConfigured()
    {
        SigningResult result = await SignWithProviderResultAsync(
            ManagedKeySignResult.Create("signature", "OTHER-SIGNATURE", "managed-key-1", "v1", SignedUtc));

        Assert.False(result.IsSigned);
        Assert.Equal("managedkey.signing.algorithm-mismatch", result.Metadata.Metadata["failure_code"]);
    }

    /// <summary>
    /// Verifies that a matching response still signs.
    /// </summary>
    [Fact]
    public async Task SignAsyncAcceptsAResponseMatchingTheRequest()
    {
        SigningResult result = await SignWithProviderResultAsync(
            ManagedKeySignResult.Create("signature", "TEST-SIGNATURE", "managed-key-1", "v1", SignedUtc));

        Assert.True(result.IsSigned);
        Assert.Equal("v1", result.Metadata.KeyVersion);
    }

    private static async Task<SigningResult> SignWithProviderResultAsync(ManagedKeySignResult managedResult)
    {
        var options = ManagedKeySigningOptions.Create(
            keyId: "managed-key-1",
            keyVersion: "v1",
            providerName: "managed-key-test",
            signatureAlgorithm: "TEST-SIGNATURE",
            requireKeyVersion: true,
            returnUnsignedOnFailure: true,
            maxRetryAttempts: 0,
            retryDelay: TimeSpan.Zero);

        var service = new ManagedKeySigningService(options, new StubClient(managedResult));

        return await service.SignAsync(
            new SigningRequest("abc123", "SHA-256", purpose: "audit", keyId: "managed-key-1", keyVersion: "v1"),
            TestContext.Current.CancellationToken);
    }

    private sealed class StubClient(ManagedKeySignResult result) : IManagedKeySigningClient
    {
        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(result);
        }
    }
}
