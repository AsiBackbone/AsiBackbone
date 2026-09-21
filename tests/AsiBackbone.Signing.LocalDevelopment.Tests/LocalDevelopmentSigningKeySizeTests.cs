using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Signing.LocalDevelopment.Tests;

/// <summary>
/// Covers local-development signing key-size validation and lifecycle behavior.
/// </summary>
public sealed class LocalDevelopmentSigningKeySizeTests
{
    /// <summary>
    /// Verifies omitted key-size configuration uses the documented secure default.
    /// </summary>
    [Fact]
    public async Task OmittedKeySizeUsesDefault()
    {
        var options = LocalDevelopmentSigningOptions.Create();
        using var service = new LocalDevelopmentSigningService(options);

        SigningResult result = await service.SignAsync(
            new SigningRequest("default-key-size", hashAlgorithm: "SHA-256"),
            TestContext.Current.CancellationToken);

        Assert.Equal(LocalDevelopmentSigningOptions.DefaultKeySizeBits, options.KeySizeBits);
        Assert.Equal(
            LocalDevelopmentSigningOptions.DefaultKeySizeBits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.Metadata.Metadata["key_size_bits"]);
    }

    /// <summary>
    /// Verifies supported key sizes produce the requested RSA key size.
    /// </summary>
    /// <param name="keySizeBits">The requested RSA key size.</param>
    [Theory]
    [InlineData(LocalDevelopmentSigningOptions.MinimumKeySizeBits)]
    [InlineData(3072)]
    [InlineData(4096)]
    public async Task SupportedKeySizeProducesRequestedKeySize(int keySizeBits)
    {
        using var service = new LocalDevelopmentSigningService(
            LocalDevelopmentSigningOptions.Create(keySizeBits: keySizeBits));

        SigningResult result = await service.SignAsync(
            new SigningRequest($"key-size-{keySizeBits}", hashAlgorithm: "SHA-256"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal(
            keySizeBits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.Metadata.Metadata["key_size_bits"]);
    }

    /// <summary>
    /// Verifies a key size that passes option validation but that the platform RSA provider cannot generate fails with
    /// the documented exception type instead of a provider exception or a silently different key.
    /// </summary>
    /// <param name="keySizeBits">A key size above the minimum that is not a legal RSA provider key size.</param>
    [Theory]
    [InlineData(2049)]
    [InlineData(2050)]
    public void UnsupportedProviderKeySizeFailsExplicitly(int keySizeBits)
    {
        var options = LocalDevelopmentSigningOptions.Create(keySizeBits: keySizeBits);
        options.Validate();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new LocalDevelopmentSigningService(options));

        Assert.Contains(
            keySizeBits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("not supported", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies below-minimum and nonsensical key sizes fail explicitly.
    /// </summary>
    /// <param name="keySizeBits">The invalid configured key size.</param>
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(2047)]
    public void InvalidKeySizeFailsValidation(int keySizeBits)
    {
        var options = LocalDevelopmentSigningOptions.Create(keySizeBits: keySizeBits);

        InvalidOperationException validationException = Assert.Throws<InvalidOperationException>(options.Validate);
        InvalidOperationException constructionException = Assert.Throws<InvalidOperationException>(
            () => new LocalDevelopmentSigningService(options));

        Assert.Contains("at least 2048 bits", validationException.Message, StringComparison.Ordinal);
        Assert.Contains(keySizeBits.ToString(System.Globalization.CultureInfo.InvariantCulture), validationException.Message, StringComparison.Ordinal);
        Assert.Equal(validationException.Message, constructionException.Message);
    }

    /// <summary>
    /// Verifies concurrent disposal remains idempotent and signing does not race into an unhandled use-after-dispose failure.
    /// </summary>
    [Fact]
    public async Task ConcurrentDisposeAndSignRemainSafe()
    {
        var service = new LocalDevelopmentSigningService();
        Task[] operations = [.. Enumerable.Range(0, 32)
            .Select(index => index % 2 == 0
                ? Task.Run(service.Dispose)
                : Task.Run(async () =>
                {
                    SigningResult result = await service.SignAsync(
                        new SigningRequest($"concurrent-{index}", hashAlgorithm: "SHA-256"),
                        TestContext.Current.CancellationToken);

                    Assert.True(
                        result.IsSigned
                        || (result.Metadata.Metadata.TryGetValue("failure_code", out string? failureCode)
                            && failureCode is "localdev.signing.disposed" or "localdev.signing.failed"));
                }))];

        await Task.WhenAll(operations);

        service.Dispose();
        service.Dispose();
    }
}
