using System.Globalization;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Signing.ManagedKey.Tests;

/// <summary>
/// Focused branch coverage for <see cref="ManagedKeySigningService" />.
/// </summary>
public sealed class ManagedKeySigningServiceBranchTests
{
    private static readonly DateTimeOffset SignedUtc = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that both supported SHA-256 spellings are normalized consistently.
    /// </summary>
    [Theory]
    [InlineData("SHA256")]
    [InlineData("sha-256")]
    public async Task SignAsyncNormalizesSupportedSha256Spellings(string hashAlgorithm)
    {
        var client = new RecordingClient(CreateSuccessfulResult());
        var service = new ManagedKeySigningService(CreateOptions(), client);

        SigningResult result = await service.SignAsync(
            CreateRequest(hashAlgorithm: hashAlgorithm),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal("SHA-256", result.Metadata.HashAlgorithm);
        Assert.Equal("SHA-256", Assert.Single(client.Requests).HashAlgorithm);
    }

    /// <summary>
    /// Verifies key identifier and key-version validation branches.
    /// </summary>
    [Theory]
    [InlineData("other-key", "v1", "managedkey.signing.key-mismatch")]
    [InlineData("managed-key-1", "v2", "managedkey.signing.key-version-mismatch")]
    public async Task SignAsyncReturnsExpectedValidationFailure(
        string keyId,
        string keyVersion,
        string expectedFailureCode)
    {
        var client = new RecordingClient(CreateSuccessfulResult());
        var service = new ManagedKeySigningService(CreateOptions(returnUnsignedOnFailure: true), client);

        SigningResult result = await service.SignAsync(
            CreateRequest(keyId: keyId, keyVersion: keyVersion),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
        Assert.Equal(expectedFailureCode, result.Metadata.Metadata["failure_code"]);
        Assert.Equal("0", result.Metadata.Metadata["provider_attempts"]);
        Assert.Empty(client.Requests);
    }

    /// <summary>
    /// Verifies required and optional key-version behavior.
    /// </summary>
    [Fact]
    public async Task SignAsyncEnforcesRequiredKeyVersionAndAllowsOptionalVersion()
    {
        var requiredClient = new RecordingClient(CreateSuccessfulResult(keyVersion: null));
        var requiredService = new ManagedKeySigningService(
            CreateOptions(keyVersion: null, requireKeyVersion: true, returnUnsignedOnFailure: true),
            requiredClient);

        SigningResult requiredResult = await requiredService.SignAsync(
            CreateRequest(keyVersion: null),
            TestContext.Current.CancellationToken);

        Assert.False(requiredResult.IsSigned);
        Assert.Equal("managedkey.signing.key-version-missing", requiredResult.Metadata.Metadata["failure_code"]);
        Assert.Empty(requiredClient.Requests);

        var optionalClient = new RecordingClient(CreateSuccessfulResult(keyVersion: null));
        var optionalService = new ManagedKeySigningService(
            CreateOptions(keyVersion: null, requireKeyVersion: false),
            optionalClient);

        SigningResult optionalResult = await optionalService.SignAsync(
            CreateRequest(keyVersion: null),
            TestContext.Current.CancellationToken);

        Assert.True(optionalResult.IsSigned);
        Assert.Null(optionalResult.Metadata.KeyVersion);
        _ = Assert.Single(optionalClient.Requests);
    }

    /// <summary>
    /// Verifies explicitly handled provider exception categories in unsigned-failure mode.
    /// </summary>
    [Theory]
    [InlineData(typeof(TimeoutException), "managedkey.signing.provider-unavailable")]
    [InlineData(typeof(InvalidOperationException), "managedkey.signing.failed")]
    [InlineData(typeof(NotSupportedException), "managedkey.signing.unsupported")]
    public async Task SignAsyncMapsHandledProviderExceptions(Type exceptionType, string expectedFailureCode)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "provider failure")!;
        var service = new ManagedKeySigningService(
            CreateOptions(returnUnsignedOnFailure: true, maxRetryAttempts: 0),
            new ThrowingClient(exception));

        SigningResult result = await service.SignAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
        Assert.Equal(expectedFailureCode, result.Metadata.Metadata["failure_code"]);
        Assert.Equal(exceptionType.Name, result.Metadata.Metadata["failure_message"]);
        Assert.Equal(exceptionType.Name, result.Metadata.Metadata["failure_exception_type"]);
        Assert.Equal("false", result.Metadata.Metadata["failure_retryable"]);
        Assert.Equal("1", result.Metadata.Metadata["provider_attempts"]);
    }

    /// <summary>
    /// Verifies that fail-closed mode rethrows the original provider exception instance.
    /// </summary>
    [Fact]
    public async Task SignAsyncFailClosedPreservesOriginalExceptionInstance()
    {
        var expected = new InvalidOperationException("original provider failure");
        var service = new ManagedKeySigningService(
            CreateOptions(returnUnsignedOnFailure: false, maxRetryAttempts: 0),
            new ThrowingClient(expected));

        Exception? actual = await Record.ExceptionAsync(async () =>
            await service.SignAsync(CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Contains(nameof(SignAsyncFailClosedPreservesOriginalExceptionInstance), actual!.StackTrace);
    }

    /// <summary>
    /// Verifies terminal and exhausted retryable managed-key failures.
    /// </summary>
    [Theory]
    [InlineData(false, 0, 1, "false")]
    [InlineData(true, 1, 2, "true")]
    public async Task SignAsyncReportsTerminalManagedKeyFailures(
        bool retryable,
        int maxRetryAttempts,
        int expectedProviderAttempts,
        string expectedRetryable)
    {
        var client = new ThrowingClient(new ManagedKeySigningException(
            "provider.test.failure",
            "safe provider failure",
            retryable));
        var service = new ManagedKeySigningService(
            CreateOptions(returnUnsignedOnFailure: true, maxRetryAttempts: maxRetryAttempts),
            client);

        SigningResult result = await service.SignAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
        Assert.Equal("provider.test.failure", result.Metadata.Metadata["failure_code"]);
        Assert.Equal("safe provider failure", result.Metadata.Metadata["failure_message"]);
        Assert.Equal(
            expectedProviderAttempts.ToString(CultureInfo.InvariantCulture),
            result.Metadata.Metadata["provider_attempts"]);
        Assert.Equal(
            maxRetryAttempts.ToString(CultureInfo.InvariantCulture),
            result.Metadata.Metadata["retry_attempts"]);
        Assert.Equal(expectedRetryable, result.Metadata.Metadata["failure_retryable"]);
        Assert.Equal(expectedProviderAttempts, client.CallCount);
    }

    /// <summary>
    /// Verifies bounded diagnostics, optional fields, and unsafe provider metadata filtering.
    /// </summary>
    [Fact]
    public async Task SignAsyncEmitsSafeOptionalMetadataAndResolvedKeyVersion()
    {
        var providerMetadata = new Dictionary<string, string>
        {
            [" provider_region "] = " east ",
            ["access_token"] = "secret",
            ["private_key"] = "secret",
            ["connection_string"] = "secret",
            ["signing_status"] = "spoofed"
        };
        var managedResult = ManagedKeySignResult.Create(
            "signature",
            "TEST-SIGNATURE",
            "managed-key-1",
            keyVersion: " resolved-v9 ",
            SignedUtc,
            providerOperationId: " operation-42 ",
            metadata: providerMetadata);
        var service = new ManagedKeySigningService(
            CreateOptions(keyVersion: null, requireKeyVersion: false),
            new RecordingClient(managedResult));

        SigningResult result = await service.SignAsync(
            CreateRequest(purpose: " audit-purpose ", keyVersion: null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal("resolved-v9", result.Metadata.KeyVersion);
        Assert.Equal("operation-42", result.Metadata.Metadata["provider_operation_id"]);
        Assert.Equal("east", result.Metadata.Metadata["provider_region"]);
        Assert.Equal("audit-purpose", result.Metadata.Metadata["purpose"]);
        Assert.Equal("signed", result.Metadata.Metadata["signing_status"]);
        Assert.DoesNotContain("access_token", result.Metadata.Metadata.Keys);
        Assert.DoesNotContain("private_key", result.Metadata.Metadata.Keys);
        Assert.DoesNotContain("connection_string", result.Metadata.Metadata.Keys);
    }

    /// <summary>
    /// Verifies retry-delay diagnostics for zero and configured delays.
    /// </summary>
    [Theory]
    [InlineData(0, "false", "false")]
    [InlineData(1, "true", "true")]
    public async Task SignAsyncRecordsRetryDelayDiagnostics(
        int retryDelayMilliseconds,
        string expectedConfigured,
        string expectedApplied)
    {
        var client = new FailOnceClient(CreateSuccessfulResult());
        var service = new ManagedKeySigningService(
            CreateOptions(
                maxRetryAttempts: 1,
                retryDelay: TimeSpan.FromMilliseconds(retryDelayMilliseconds)),
            client);

        SigningResult result = await service.SignAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal("1", result.Metadata.Metadata["retry_attempts"]);
        Assert.Equal("2", result.Metadata.Metadata["provider_attempts"]);
        Assert.Equal(expectedConfigured, result.Metadata.Metadata["retry_delay_configured"]);
        Assert.Equal(expectedApplied, result.Metadata.Metadata["retry_delay_applied"]);
        Assert.Equal("provider.retry", result.Metadata.Metadata["last_retry_failure_code"]);
    }

    private static ManagedKeySigningOptions CreateOptions(
        string? keyVersion = "v1",
        bool requireKeyVersion = true,
        bool returnUnsignedOnFailure = false,
        int maxRetryAttempts = 0,
        TimeSpan? retryDelay = null)
    {
        return ManagedKeySigningOptions.Create(
            keyId: "managed-key-1",
            keyVersion: keyVersion,
            providerName: "managed-key-test",
            signatureAlgorithm: "TEST-SIGNATURE",
            requireKeyVersion: requireKeyVersion,
            returnUnsignedOnFailure: returnUnsignedOnFailure,
            maxRetryAttempts: maxRetryAttempts,
            retryDelay: retryDelay ?? TimeSpan.Zero);
    }

    private static SigningRequest CreateRequest(
        string hashAlgorithm = "SHA-256",
        string? purpose = null,
        string? keyId = "managed-key-1",
        string? keyVersion = "v1")
    {
        return new SigningRequest(
            "abc123",
            hashAlgorithm,
            purpose,
            keyId,
            keyVersion);
    }

    private static ManagedKeySignResult CreateSuccessfulResult(string? keyVersion = "v1")
    {
        return ManagedKeySignResult.Create(
            "signature",
            "TEST-SIGNATURE",
            "managed-key-1",
            keyVersion,
            SignedUtc);
    }

    private sealed class RecordingClient(ManagedKeySignResult result) : IManagedKeySigningClient
    {
        public List<ManagedKeySignRequest> Requests { get; } = [];

        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingClient(Exception exception) : IManagedKeySigningClient
    {
        public int CallCount { get; private set; }

        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw exception;
        }
    }

    private sealed class FailOnceClient(ManagedKeySignResult result) : IManagedKeySigningClient
    {
        private int callCount;

        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            callCount++;
            return callCount == 1
                ? throw new ManagedKeySigningException(
                    "provider.retry",
                    "retryable provider failure",
                    isRetryable: true)
                : ValueTask.FromResult(result);
        }
    }
}
