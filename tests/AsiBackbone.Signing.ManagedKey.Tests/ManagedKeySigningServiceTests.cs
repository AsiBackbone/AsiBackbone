using System.Text;
using AsiBackbone.Core.Signing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Signing.ManagedKey.Tests;

/// <summary>
/// Unit tests for the <see cref="ManagedKeySigningService"/> class.
/// </summary>
public sealed class ManagedKeySigningServiceTests
{
    private static readonly DateTimeOffset SignedUtc = new(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningService.SignAsync(SigningRequest, CancellationToken)"/> method returns a <see cref="SigningResult"/> with provider-neutral managed key metadata when signing is successful.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SignAsyncReturnsProviderNeutralManagedKeyMetadata()
    {
        var client = new FakeManagedKeySigningClient();
        var service = new ManagedKeySigningService(CreateOptions(), client);
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-256",
            purpose: CanonicalArtifactTypes.AuditLedgerRecord,
            keyId: "managed-key-1",
            keyVersion: "v1",
            metadata: new Dictionary<string, string>
            {
                ["artifact_type"] = CanonicalArtifactTypes.AuditLedgerRecord,
                ["artifact_id"] = "record-1",
                ["retry_attempts"] = "caller-spoof",
                ["signing_status"] = "caller-spoof"
            });

        SigningResult result = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal("abc123", result.Metadata.SigningHash);
        Assert.Equal("SHA-256", result.Metadata.HashAlgorithm);
        Assert.NotNull(result.Metadata.Signature);
        Assert.Equal(ManagedKeySigningOptions.DefaultSignatureAlgorithm, result.Metadata.SignatureAlgorithm);
        Assert.Equal("managed-key-1", result.Metadata.KeyId);
        Assert.Equal("v1", result.Metadata.KeyVersion);
        Assert.Equal("managed-key-test", result.Metadata.Provider);
        Assert.Equal(SignedUtc, result.Metadata.SignedUtc);
        Assert.Equal("managed-key", result.Metadata.Metadata["provider_kind"]);
        Assert.Equal("true", result.Metadata.Metadata["remote_key_material"]);
        Assert.Equal("false", result.Metadata.Metadata["raw_private_key_loaded"]);
        Assert.Equal("signed", result.Metadata.Metadata["signing_status"]);
        Assert.Equal("operation-1", result.Metadata.Metadata["provider_operation_id"]);
        Assert.Equal("0", result.Metadata.Metadata["retry_attempts"]);
        Assert.Equal("1", result.Metadata.Metadata["provider_attempts"]);
        Assert.Equal("2", result.Metadata.Metadata["max_retry_attempts"]);
        Assert.Equal("0", result.Metadata.Metadata["retry_delay_milliseconds"]);
        Assert.Equal("false", result.Metadata.Metadata["retry_delay_configured"]);
        Assert.Equal("false", result.Metadata.Metadata["retry_delay_applied"]);
        Assert.Equal(CanonicalArtifactTypes.AuditLedgerRecord, result.Metadata.Metadata["artifact_type"]);
        Assert.False(result.Metadata.Metadata.ContainsKey("access_token"));
    }

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningService.SignAsync(SigningRequest, CancellationToken)"/> method throws a <see cref="ManagedKeySigningException"/> with the expected failure code when an unsupported hash algorithm is provided and the service is configured to fail closed.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SignAsyncThrowsForUnsupportedHashAlgorithmByDefault()
    {
        var service = new ManagedKeySigningService(CreateOptions(), new FakeManagedKeySigningClient());
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-512",
            purpose: CanonicalArtifactTypes.AuditLedgerRecord,
            keyId: "managed-key-1",
            keyVersion: "v1");

        ManagedKeySigningException exception = await Assert.ThrowsAsync<ManagedKeySigningException>(async () =>
            await service.SignAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal("managedkey.signing.hash-algorithm-unsupported", exception.FailureCode);
    }

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningService.SignAsync(SigningRequest, CancellationToken)"/> method throws a <see cref="ManagedKeySigningException"/> with the expected failure code when the managed key provider fails and the service is configured to fail closed.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SignAsyncThrowsForProviderFailureByDefault()
    {
        var client = new FakeManagedKeySigningClient(retryableFailuresBeforeSuccess: 1);
        ManagedKeySigningOptions options = CreateOptions(maxRetryAttempts: 0);
        var service = new ManagedKeySigningService(options, client);
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-256",
            purpose: CanonicalArtifactTypes.AuditLedgerRecord,
            keyId: "managed-key-1",
            keyVersion: "v1");

        ManagedKeySigningException exception = await Assert.ThrowsAsync<ManagedKeySigningException>(async () =>
            await service.SignAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal("managedkey.signing.provider-unavailable", exception.FailureCode);
        Assert.Equal(1, client.CallCount);
    }

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningService.SignAsync(SigningRequest, CancellationToken)"/> method returns an unsigned <see cref="SigningResult"/> with the expected failure code when an unsupported hash algorithm is provided and the service is configured to fail open.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SignAsyncReturnsUnsignedFailureForUnsupportedHashAlgorithmInLocalValidationMode()
    {
        var service = new ManagedKeySigningService(CreateLocalValidationOptions(), new FakeManagedKeySigningClient());
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-512",
            purpose: CanonicalArtifactTypes.AuditLedgerRecord,
            keyId: "managed-key-1",
            keyVersion: "v1");

        SigningResult result = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
        Assert.Null(result.Metadata.Signature);
        Assert.Equal("failed", result.Metadata.Metadata["signing_status"]);
        Assert.Equal("managedkey.signing.hash-algorithm-unsupported", result.Metadata.Metadata["failure_code"]);
        Assert.Equal("0", result.Metadata.Metadata["provider_attempts"]);
        Assert.Equal("2", result.Metadata.Metadata["max_retry_attempts"]);
    }

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningService.SignAsync(SigningRequest, CancellationToken)"/> method returns an unsigned <see cref="SigningResult"/> with the expected failure code when the key version is missing and required in local validation mode.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SignAsyncReturnsUnsignedFailureForMissingKeyVersionWhenRequiredInLocalValidationMode()
    {
        ManagedKeySigningOptions options = CreateLocalValidationOptions(keyVersion: null, requireKeyVersion: true);
        var service = new ManagedKeySigningService(options, new FakeManagedKeySigningClient());
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-256",
            purpose: CanonicalArtifactTypes.AuditLedgerRecord,
            keyId: "managed-key-1");

        SigningResult result = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
        Assert.Equal("managedkey.signing.key-version-missing", result.Metadata.Metadata["failure_code"]);
        Assert.Equal("0", result.Metadata.Metadata["provider_attempts"]);
    }

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningService.SignAsync(SigningRequest, CancellationToken)"/> method returns an unsigned <see cref="SigningResult"/> with the expected failure code when the managed key provider fails and the service is configured to fail open.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SignAsyncReturnsUnsignedFailureForProviderFailureInLocalValidationMode()
    {
        var client = new FakeManagedKeySigningClient(retryableFailuresBeforeSuccess: 1);
        ManagedKeySigningOptions options = CreateLocalValidationOptions(maxRetryAttempts: 0);
        var service = new ManagedKeySigningService(options, client);
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-256",
            purpose: CanonicalArtifactTypes.AuditLedgerRecord,
            keyId: "managed-key-1",
            keyVersion: "v1");

        SigningResult result = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
        Assert.Equal("failed", result.Metadata.Metadata["signing_status"]);
        Assert.Equal("managedkey.signing.provider-unavailable", result.Metadata.Metadata["failure_code"]);
        Assert.Equal("0", result.Metadata.Metadata["retry_attempts"]);
        Assert.Equal("1", result.Metadata.Metadata["provider_attempts"]);
        Assert.Equal("0", result.Metadata.Metadata["max_retry_attempts"]);
        Assert.Equal("ManagedKeySigningException", result.Metadata.Metadata["failure_exception_type"]);
        Assert.Equal("true", result.Metadata.Metadata["failure_retryable"]);
        Assert.Equal(1, client.CallCount);
    }

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningService.SignAsync(SigningRequest, CancellationToken)"/> method retries on retryable failures and eventually succeeds.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SignAsyncRetriesRetryableClientFailureThenSigns()
    {
        var client = new FakeManagedKeySigningClient(retryableFailuresBeforeSuccess: 1);
        ManagedKeySigningOptions options = CreateOptions(retryDelay: TimeSpan.Zero);
        var service = new ManagedKeySigningService(options, client);
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-256",
            purpose: CanonicalArtifactTypes.AuditLedgerRecord,
            keyId: "managed-key-1",
            keyVersion: "v1");

        SigningResult result = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal(2, client.CallCount);
        Assert.Equal("1", result.Metadata.Metadata["retry_attempts"]);
        Assert.Equal("2", result.Metadata.Metadata["provider_attempts"]);
        Assert.Equal("2", result.Metadata.Metadata["max_retry_attempts"]);
        Assert.Equal("0", result.Metadata.Metadata["retry_delay_milliseconds"]);
        Assert.Equal("false", result.Metadata.Metadata["retry_delay_configured"]);
        Assert.Equal("false", result.Metadata.Metadata["retry_delay_applied"]);
        Assert.Equal("managedkey.signing.provider-unavailable", result.Metadata.Metadata["last_retry_failure_code"]);
        Assert.Equal("ManagedKeySigningException", result.Metadata.Metadata["last_retry_failure_exception_type"]);
    }

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningService.SignAsync(SigningRequest, CancellationToken)"/> method records the applied retry delay in the signing result metadata when a retry delay is configured.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SignAsyncRecordsAppliedRetryDelayWhenConfigured()
    {
        var client = new FakeManagedKeySigningClient(retryableFailuresBeforeSuccess: 1);
        ManagedKeySigningOptions options = CreateOptions(retryDelay: TimeSpan.FromMilliseconds(1));
        var service = new ManagedKeySigningService(options, client);
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-256",
            purpose: CanonicalArtifactTypes.AuditLedgerRecord,
            keyId: "managed-key-1",
            keyVersion: "v1");

        SigningResult result = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal("1", result.Metadata.Metadata["retry_delay_milliseconds"]);
        Assert.Equal("true", result.Metadata.Metadata["retry_delay_configured"]);
        Assert.Equal("true", result.Metadata.Metadata["retry_delay_applied"]);
    }

    /// <summary>
    /// Tests that the <see cref="ManagedKeySigningOptions"/> class defaults to fail-closed behavior when the <see cref="ManagedKeySigningOptions.ReturnUnsignedOnFailure"/> property is not explicitly set.
    /// </summary>
    [Fact]
    public void ManagedKeySigningOptionsDefaultToFailClosed()
    {
        var options = new ManagedKeySigningOptions
        {
            KeyId = "managed-key-1",
            KeyVersion = "v1"
        };

        Assert.False(options.ReturnUnsignedOnFailure);
    }

    /// <summary>
    /// Tests that the <c>ServiceCollectionExtensions.AddAsiBackboneManagedKeySigning(IServiceCollection, Action{ManagedKeySigningOptions}, Func{IServiceProvider, IManagedKeySigningClient})</c> method registers a fail-closed signing service by default.
    /// </summary>
    [Fact]
    public void AddAsiBackboneManagedKeySigningRegistersFailClosedSigningServiceByDefault()
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<IGovernanceSignatureVerificationService>(new StubVerificationService());
        _ = services.AddAsiBackboneManagedKeySigning(
            options =>
            {
                options.ProviderName = "managed-key-test";
                options.KeyId = "managed-key-1";
                options.KeyVersion = "v1";
                options.RetryDelay = TimeSpan.Zero;
            },
            _ => new FakeManagedKeySigningClient());

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<ManagedKeySigningService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IGovernanceSigningService>());
        Assert.False(serviceProvider.GetRequiredService<ManagedKeySigningOptions>().ReturnUnsignedOnFailure);
    }

    /// <summary>
    /// Tests that the <c>ServiceCollectionExtensions.AddAsiBackboneManagedKeySigningForLocalValidation(IServiceCollection, Action{ManagedKeySigningOptions}, Func{IServiceProvider, IManagedKeySigningClient})</c> method registers a fail-open signing service.
    /// </summary>
    [Fact]
    public void AddAsiBackboneManagedKeySigningForLocalValidationRegistersFailOpenSigningService()
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<IGovernanceSignatureVerificationService>(new StubVerificationService());
        _ = services.AddAsiBackboneManagedKeySigningForLocalValidation(
            options =>
            {
                options.ProviderName = "managed-key-test";
                options.KeyId = "managed-key-1";
                options.KeyVersion = "v1";
                options.RetryDelay = TimeSpan.Zero;
            },
            _ => new FakeManagedKeySigningClient());

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<ManagedKeySigningService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IGovernanceSigningService>());
        Assert.True(serviceProvider.GetRequiredService<ManagedKeySigningOptions>().ReturnUnsignedOnFailure);
    }

    private static ManagedKeySigningOptions CreateOptions(
        string? keyVersion = "v1",
        bool requireKeyVersion = true,
        bool returnUnsignedOnFailure = false,
        int maxRetryAttempts = 2,
        TimeSpan? retryDelay = null)
    {
        return ManagedKeySigningOptions.Create(
            keyId: "managed-key-1",
            keyVersion: keyVersion,
            providerName: "managed-key-test",
            requireKeyVersion: requireKeyVersion,
            returnUnsignedOnFailure: returnUnsignedOnFailure,
            maxRetryAttempts: maxRetryAttempts,
            retryDelay: retryDelay ?? TimeSpan.Zero);
    }

    /// <summary>
    /// Verifies that the managed-key client receives the exact signature input Core supplied, so a host client that signs
    /// it binds the signing policy context rather than the hash text alone.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SignAsyncPassesSignatureInputToClient()
    {
        var client = new FakeManagedKeySigningClient();
        var service = new ManagedKeySigningService(CreateOptions(), client);
        var hash = CanonicalPayloadHash.Create(
            CanonicalArtifactTypes.AuditLedgerRecord,
            "record-1",
            "schema-v1",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            CanonicalPayloadOptions.DefaultHashAlgorithm,
            "abc123");
        SigningRequest request = GovernanceArtifactSigner.CreateSigningRequest(
            hash,
            keyId: "managed-key-1",
            keyVersion: "v1",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GovernanceSignatureInput.PolicyVersionMetadataKey] = "policy-v1"
            });

        _ = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.False(request.UsesLegacySignatureInput);
        Assert.True(client.LastSignatureInput.Span.SequenceEqual(request.SignatureInput.Span));
    }

    private static ManagedKeySigningOptions CreateLocalValidationOptions(
        string? keyVersion = "v1",
        bool requireKeyVersion = true,
        int maxRetryAttempts = 2,
        TimeSpan? retryDelay = null)
    {
        return ManagedKeySigningOptions.CreateLocalValidation(
            keyId: "managed-key-1",
            keyVersion: keyVersion,
            providerName: "managed-key-test",
            requireKeyVersion: requireKeyVersion,
            maxRetryAttempts: maxRetryAttempts,
            retryDelay: retryDelay ?? TimeSpan.Zero);
    }

    private sealed class FakeManagedKeySigningClient(int retryableFailuresBeforeSuccess = 0) : IManagedKeySigningClient
    {
        public int CallCount { get; private set; }

        public ReadOnlyMemory<byte> LastSignatureInput { get; private set; }

        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastSignatureInput = request.SignatureInput;

            if (CallCount <= retryableFailuresBeforeSuccess)
            {
                throw new ManagedKeySigningException(
                    "managedkey.signing.provider-unavailable",
                    "The managed-key provider is temporarily unavailable.",
                    isRetryable: true);
            }

            string signature = Convert.ToBase64String(Encoding.UTF8.GetBytes($"managed-signature:{request.SigningHash}"));

            return ValueTask.FromResult(ManagedKeySignResult.Create(
                signature,
                request.SignatureAlgorithm,
                request.KeyId,
                request.KeyVersion,
                SignedUtc,
                providerOperationId: "operation-1",
                metadata: new Dictionary<string, string>
                {
                    ["provider_region"] = "test-region",
                    ["retry_attempts"] = "provider-spoof",
                    ["signing_status"] = "provider-spoof",
                    ["provider_operation_id"] = "provider-spoof",
                    ["access_token"] = "should-not-be-preserved"
                }));
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
