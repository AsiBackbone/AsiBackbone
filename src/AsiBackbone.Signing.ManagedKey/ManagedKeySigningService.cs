using System.Globalization;
using System.Runtime.ExceptionServices;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Signing.ManagedKey;

/// <summary>
/// Provides managed-key signing for AsiBackbone signing abstractions through a host-owned managed-key client.
/// </summary>
/// <remarks>
/// This service signs precomputed hashes only. It never requests or handles raw private key material.
/// </remarks>
public sealed class ManagedKeySigningService : IAsiBackboneSigningService
{
    private const string ProviderKind = "managed-key";

    private readonly ManagedKeySigningOptions options;
    private readonly IManagedKeySigningClient client;
    private readonly IManagedKeyRetryJitterSource jitterSource;
    private readonly IManagedKeyRetryDelay retryDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedKeySigningService" /> class.
    /// </summary>
    public ManagedKeySigningService(ManagedKeySigningOptions options, IManagedKeySigningClient client)
        : this(
            options,
            client,
            SharedRandomManagedKeyRetryJitterSource.Instance,
            SystemManagedKeyRetryDelay.Instance)
    {
    }

    internal ManagedKeySigningService(
        ManagedKeySigningOptions options,
        IManagedKeySigningClient client,
        IManagedKeyRetryJitterSource jitterSource,
        IManagedKeyRetryDelay retryDelay)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(jitterSource);
        ArgumentNullException.ThrowIfNull(retryDelay);

        options.Validate();
        this.options = options;
        this.client = client;
        this.jitterSource = jitterSource;
        this.retryDelay = retryDelay;
    }

    /// <inheritdoc />
    public async ValueTask<SigningResult> SignAsync(
        SigningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ManagedKeySigningAttemptDiagnostics diagnostics = CreateAttemptDiagnostics();
        string? validationFailure = ValidateSigningRequest(request);
        if (validationFailure is not null)
        {
            return HandleValidationFailure(request, validationFailure, diagnostics);
        }

        int attempt = 0;

        while (true)
        {
            try
            {
                ManagedKeySignResult managedResult = await client
                    .SignAsync(CreateManagedKeyRequest(request), cancellationToken)
                    .ConfigureAwait(false);

                ManagedKeyResponseMismatch? mismatch = ValidateSigningResponse(request, managedResult);

                return mismatch is not null
                    ? HandleFailure(
                        request,
                        mismatch.FailureCode,
                        mismatch.Message,
                        attempt,
                        diagnostics,
                        new ManagedKeySigningException(mismatch.FailureCode, mismatch.Message, isRetryable: false))
                    : CreateSignedResult(request, managedResult, attempt, diagnostics);
            }
            catch (ManagedKeySigningException exception) when (exception.IsRetryable && attempt < options.MaxRetryAttempts)
            {
                diagnostics.RecordRetry(exception);
                attempt++;
                await DelayForRetryAsync(attempt, diagnostics, cancellationToken).ConfigureAwait(false);
            }
            catch (ManagedKeySigningException exception)
            {
                return HandleFailure(request, exception.FailureCode, exception.Message, attempt, diagnostics, exception);
            }
            catch (TimeoutException exception)
            {
                return HandleFailure(request, "managedkey.signing.provider-unavailable", exception.GetType().Name, attempt, diagnostics, exception);
            }
            catch (InvalidOperationException exception)
            {
                return HandleFailure(request, "managedkey.signing.failed", exception.GetType().Name, attempt, diagnostics, exception);
            }
            catch (NotSupportedException exception)
            {
                return HandleFailure(request, "managedkey.signing.unsupported", exception.GetType().Name, attempt, diagnostics, exception);
            }
        }
    }

    private SigningResult HandleValidationFailure(
        SigningRequest request,
        string failureCode,
        ManagedKeySigningAttemptDiagnostics diagnostics)
    {
        diagnostics.RecordValidationFailure();

        return !options.ReturnUnsignedOnFailure
            ? throw new ManagedKeySigningException(failureCode, failureCode)
            : CreateUnsignedFailureResult(request, failureCode, failureCode, retryAttempts: 0, diagnostics);
    }

    private SigningResult HandleFailure(
        SigningRequest request,
        string failureCode,
        string failureMessage,
        int retryAttempts,
        ManagedKeySigningAttemptDiagnostics diagnostics,
        Exception exception)
    {
        diagnostics.RecordFailure(exception);

        if (!options.ReturnUnsignedOnFailure)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return CreateUnsignedFailureResult(request, failureCode, failureMessage, retryAttempts, diagnostics);
    }

    private ManagedKeySignRequest CreateManagedKeyRequest(SigningRequest request)
    {
        return new ManagedKeySignRequest(
            request.SigningHash,
            NormalizeHashAlgorithm(request.HashAlgorithm),
            NormalizeRequired(options.SignatureAlgorithm, ManagedKeySigningOptions.DefaultSignatureAlgorithm),
            ResolveKeyId(request),
            ResolveKeyVersion(request),
            request.Purpose,
            request.Metadata);
    }

    /// <summary>
    /// Rejects a provider response whose key identity or signature algorithm differs from what was requested.
    /// </summary>
    /// <remarks>
    /// The signed result copies the provider's key identifier, key version, and signature algorithm into signing metadata.
    /// Without this comparison a provider that signed under a different key, a different key version, or a different
    /// algorithm produced an artifact that recorded the substitution as though it had been requested, and the artifact
    /// could also carry two disagreeing algorithm descriptors because the configured value is written separately.
    /// A key version is only compared when the request or configuration pinned one, so ordinary version resolution by the
    /// provider is still accepted.
    /// </remarks>
    private ManagedKeyResponseMismatch? ValidateSigningResponse(SigningRequest request, ManagedKeySignResult managedResult)
    {
        string requestedKeyId = ResolveKeyId(request);

        if (!string.IsNullOrWhiteSpace(managedResult.KeyId)
            && !string.Equals(managedResult.KeyId, requestedKeyId, StringComparison.Ordinal))
        {
            return new ManagedKeyResponseMismatch(
                "managedkey.signing.key-mismatch",
                "The managed key provider signed with a different key identifier than the one requested.");
        }

        string? requestedKeyVersion = ResolveKeyVersion(request);

        if (requestedKeyVersion is not null
            && !string.IsNullOrWhiteSpace(managedResult.KeyVersion)
            && !string.Equals(managedResult.KeyVersion, requestedKeyVersion, StringComparison.Ordinal))
        {
            return new ManagedKeyResponseMismatch(
                "managedkey.signing.key-version-mismatch",
                "The managed key provider signed with a different key version than the one requested.");
        }

        string configuredAlgorithm = NormalizeRequired(options.SignatureAlgorithm, ManagedKeySigningOptions.DefaultSignatureAlgorithm);

        return !string.IsNullOrWhiteSpace(managedResult.SignatureAlgorithm)
            && !string.Equals(managedResult.SignatureAlgorithm, configuredAlgorithm, StringComparison.Ordinal)
            ? new ManagedKeyResponseMismatch(
                "managedkey.signing.algorithm-mismatch",
                "The managed key provider signed with a different signature algorithm than the configured one.")
            : null;
    }

    private sealed record ManagedKeyResponseMismatch(string FailureCode, string Message);

    private SigningResult CreateSignedResult(
        SigningRequest request,
        ManagedKeySignResult managedResult,
        int retryAttempts,
        ManagedKeySigningAttemptDiagnostics diagnostics)
    {
        string resolvedKeyVersion = managedResult.KeyVersion ?? ResolveKeyVersion(request) ?? string.Empty;
        Dictionary<string, string> metadata = CreateBaseMetadata(request, retryAttempts, providerAttempts: retryAttempts + 1, diagnostics);
        metadata["signing_status"] = "signed";

        if (managedResult.ProviderOperationId is not null)
        {
            metadata["provider_operation_id"] = managedResult.ProviderOperationId;
        }

        foreach (KeyValuePair<string, string> item in managedResult.Metadata)
        {
            if (!IsSafeProviderMetadataKey(item.Key) || IsReservedDiagnosticMetadataKey(item.Key))
            {
                continue;
            }

            metadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        var signingMetadata = SigningMetadata.Create(
            signingHash: request.SigningHash,
            hashAlgorithm: NormalizeHashAlgorithm(request.HashAlgorithm),
            signature: managedResult.Signature,
            signatureAlgorithm: managedResult.SignatureAlgorithm,
            keyId: managedResult.KeyId,
            keyVersion: string.IsNullOrWhiteSpace(resolvedKeyVersion) ? null : resolvedKeyVersion,
            provider: NormalizeRequired(options.ProviderName, ManagedKeySigningOptions.DefaultProviderName),
            signedUtc: managedResult.SignedUtc,
            metadata: metadata);

        return SigningResult.FromMetadata(signingMetadata);
    }

    private SigningResult CreateUnsignedFailureResult(
        SigningRequest request,
        string failureCode,
        string failureMessage,
        int retryAttempts,
        ManagedKeySigningAttemptDiagnostics diagnostics)
    {
        int providerAttempts = diagnostics.HasValidationFailure ? 0 : retryAttempts + 1;
        Dictionary<string, string> metadata = CreateBaseMetadata(request, retryAttempts, providerAttempts, diagnostics);
        metadata["signing_status"] = "failed";
        metadata["failure_code"] = failureCode;
        metadata["failure_message"] = failureMessage;

        var signingMetadata = SigningMetadata.Create(
            signingHash: request.SigningHash,
            hashAlgorithm: NormalizeHashAlgorithm(request.HashAlgorithm),
            keyId: ResolveKeyId(request),
            keyVersion: ResolveKeyVersion(request),
            provider: NormalizeRequired(options.ProviderName, ManagedKeySigningOptions.DefaultProviderName),
            metadata: metadata);

        return SigningResult.FromMetadata(signingMetadata);
    }

    private Dictionary<string, string> CreateBaseMetadata(
        SigningRequest request,
        int retryAttempts,
        int providerAttempts,
        ManagedKeySigningAttemptDiagnostics diagnostics)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in request.Metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || IsReservedDiagnosticMetadataKey(item.Key))
            {
                continue;
            }

            metadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        metadata["provider_kind"] = ProviderKind;
        metadata["remote_key_material"] = "true";
        metadata["raw_private_key_loaded"] = "false";
        metadata["retry_attempts"] = retryAttempts.ToString(CultureInfo.InvariantCulture);
        metadata["provider_attempts"] = providerAttempts.ToString(CultureInfo.InvariantCulture);
        metadata["max_retry_attempts"] = diagnostics.MaxRetryAttempts.ToString(CultureInfo.InvariantCulture);
        metadata["retry_delay_milliseconds"] = diagnostics.BaseRetryDelay.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
        metadata["max_retry_delay_milliseconds"] = diagnostics.MaxRetryDelay.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
        metadata["last_retry_delay_milliseconds"] = diagnostics.LastRetryDelay.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
        metadata["total_retry_delay_milliseconds"] = diagnostics.TotalRetryDelay.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
        metadata["retry_delay_count"] = diagnostics.RetryDelayCount.ToString(CultureInfo.InvariantCulture);
        metadata["retry_backoff_strategy"] = ManagedKeyRetryBackoff.StrategyName;
        metadata["retry_delay_configured"] = ToMetadataBoolean(diagnostics.BaseRetryDelay > TimeSpan.Zero);
        metadata["retry_delay_applied"] = ToMetadataBoolean(diagnostics.RetryDelayCount > 0);
        metadata["signature_algorithm"] = NormalizeRequired(options.SignatureAlgorithm, ManagedKeySigningOptions.DefaultSignatureAlgorithm);

        if (diagnostics.LastRetryFailureCode is not null)
        {
            metadata["last_retry_failure_code"] = diagnostics.LastRetryFailureCode;
        }

        if (diagnostics.LastRetryFailureExceptionType is not null)
        {
            metadata["last_retry_failure_exception_type"] = diagnostics.LastRetryFailureExceptionType;
        }

        if (diagnostics.FailureExceptionType is not null)
        {
            metadata["failure_exception_type"] = diagnostics.FailureExceptionType;
        }

        if (diagnostics.FailureRetryable is not null)
        {
            metadata["failure_retryable"] = ToMetadataBoolean(diagnostics.FailureRetryable.Value);
        }

        if (request.Purpose is not null)
        {
            metadata["purpose"] = request.Purpose;
        }

        return metadata;
    }

    private string? ValidateSigningRequest(SigningRequest request)
    {
        if (!IsSupportedHashAlgorithm(request.HashAlgorithm))
        {
            return "managedkey.signing.hash-algorithm-unsupported";
        }

        string configuredKeyId = NormalizeRequired(options.KeyId, string.Empty);
        if (request.KeyId is not null && !string.Equals(request.KeyId, configuredKeyId, StringComparison.Ordinal))
        {
            return "managedkey.signing.key-mismatch";
        }

        string? configuredKeyVersion = NormalizeOptional(options.KeyVersion);
        return options.RequireKeyVersion && request.KeyVersion is null && configuredKeyVersion is null
            ? "managedkey.signing.key-version-missing"
            : request.KeyVersion is not null
            && configuredKeyVersion is not null
            && !string.Equals(request.KeyVersion, configuredKeyVersion, StringComparison.Ordinal)
            ? "managedkey.signing.key-version-mismatch"
            : null;
    }

    private string ResolveKeyId(SigningRequest request)
    {
        return request.KeyId ?? NormalizeRequired(options.KeyId, string.Empty);
    }

    private string? ResolveKeyVersion(SigningRequest request)
    {
        return request.KeyVersion ?? NormalizeOptional(options.KeyVersion);
    }

    private string NormalizeHashAlgorithm(string? hashAlgorithm)
    {
        string normalized = string.IsNullOrWhiteSpace(hashAlgorithm)
            ? options.HashAlgorithm
            : hashAlgorithm.Trim();

        return normalized.Equals("SHA256", StringComparison.OrdinalIgnoreCase)
            ? ManagedKeySigningOptions.DefaultHashAlgorithm
            : normalized.Equals(ManagedKeySigningOptions.DefaultHashAlgorithm, StringComparison.OrdinalIgnoreCase)
                ? ManagedKeySigningOptions.DefaultHashAlgorithm
                : normalized;
    }

    private bool IsSupportedHashAlgorithm(string? hashAlgorithm)
    {
        return NormalizeHashAlgorithm(hashAlgorithm).Equals(
            NormalizeHashAlgorithm(options.HashAlgorithm),
            StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask DelayForRetryAsync(
        int retryAttempt,
        ManagedKeySigningAttemptDiagnostics diagnostics,
        CancellationToken cancellationToken)
    {
        TimeSpan delay = ManagedKeyRetryBackoff.CalculateDelay(
            options.RetryDelay,
            options.MaxRetryDelay,
            retryAttempt,
            jitterSource.NextDouble(),
            diagnostics.LastRetryDelay);

        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        diagnostics.RecordRetryDelay(delay);
        await retryDelay.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
    }

    private ManagedKeySigningAttemptDiagnostics CreateAttemptDiagnostics()
    {
        return new ManagedKeySigningAttemptDiagnostics(
            options.MaxRetryAttempts,
            options.RetryDelay,
            options.MaxRetryDelay);
    }

    private static bool IsSafeProviderMetadataKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key)
            && !key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            && !key.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !key.Contains("credential", StringComparison.OrdinalIgnoreCase)
            && !key.Contains("private", StringComparison.OrdinalIgnoreCase)
            && !key.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReservedDiagnosticMetadataKey(string key)
    {
        return ManagedKeyDiagnosticMetadataKeyClassifier.IsReserved(key);
    }

    private static string NormalizeRequired(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ToMetadataBoolean(bool value)
    {
        return value ? "true" : "false";
    }

    private sealed class ManagedKeySigningAttemptDiagnostics(
        int maxRetryAttempts,
        TimeSpan baseRetryDelay,
        TimeSpan maxRetryDelay)
    {
        public int MaxRetryAttempts { get; } = maxRetryAttempts;

        public TimeSpan BaseRetryDelay { get; } = baseRetryDelay;

        public TimeSpan MaxRetryDelay { get; } = maxRetryDelay;

        public TimeSpan LastRetryDelay { get; private set; }

        public TimeSpan TotalRetryDelay { get; private set; }

        public int RetryDelayCount { get; private set; }

        public bool HasValidationFailure { get; private set; }

        public string? LastRetryFailureCode { get; private set; }

        public string? LastRetryFailureExceptionType { get; private set; }

        public string? FailureExceptionType { get; private set; }

        public bool? FailureRetryable { get; private set; }

        public void RecordValidationFailure()
        {
            HasValidationFailure = true;
        }

        public void RecordRetry(ManagedKeySigningException exception)
        {
            LastRetryFailureCode = exception.FailureCode;
            LastRetryFailureExceptionType = exception.GetType().Name;
        }

        public void RecordRetryDelay(TimeSpan delay)
        {
            LastRetryDelay = delay;
            TotalRetryDelay += delay;
            RetryDelayCount++;
        }

        public void RecordFailure(Exception exception)
        {
            FailureExceptionType = exception.GetType().Name;
            FailureRetryable = exception is ManagedKeySigningException managedKeyException && managedKeyException.IsRetryable;
        }
    }
}
