using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Signing.ManagedKey.Tests;

/// <summary>
/// Covers bounded exponential retry backoff and deterministic service integration.
/// </summary>
public sealed class ManagedKeyRetryBackoffTests
{
    private static readonly DateTimeOffset SignedUtc = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies exponential windows, jitter bounds, monotonic growth, and the configured cap.
    /// </summary>
    [Fact]
    public void CalculateDelayGrowsWithinBoundsAndCaps()
    {
        var baseDelay = TimeSpan.FromMilliseconds(100);
        var maxDelay = TimeSpan.FromMilliseconds(500);

        TimeSpan first = ManagedKeyRetryBackoff.CalculateDelay(baseDelay, maxDelay, 1, 0d, TimeSpan.Zero);
        TimeSpan second = ManagedKeyRetryBackoff.CalculateDelay(baseDelay, maxDelay, 2, 1d, first);
        TimeSpan third = ManagedKeyRetryBackoff.CalculateDelay(baseDelay, maxDelay, 3, 1d, second);
        TimeSpan fourth = ManagedKeyRetryBackoff.CalculateDelay(baseDelay, maxDelay, 4, 1d, third);

        Assert.Equal(TimeSpan.FromMilliseconds(50), first);
        Assert.Equal(TimeSpan.FromMilliseconds(200), second);
        Assert.Equal(TimeSpan.FromMilliseconds(400), third);
        Assert.Equal(TimeSpan.FromMilliseconds(500), fourth);
    }

    /// <summary>
    /// Verifies zero base delay keeps retries immediate regardless of attempt or jitter.
    /// </summary>
    [Fact]
    public void CalculateDelayReturnsZeroWhenBaseDelayIsZero()
    {
        TimeSpan result = ManagedKeyRetryBackoff.CalculateDelay(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5),
            retryAttempt: 4,
            jitterSample: 0.75d,
            previousDelay: TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, result);
    }

    /// <summary>
    /// Verifies different jitter samples produce different schedules inside the same retry window.
    /// </summary>
    [Fact]
    public void CalculateDelayVariesByJitterSample()
    {
        TimeSpan low = ManagedKeyRetryBackoff.CalculateDelay(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(1),
            retryAttempt: 2,
            jitterSample: 0.1d,
            previousDelay: TimeSpan.Zero);
        TimeSpan high = ManagedKeyRetryBackoff.CalculateDelay(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(1),
            retryAttempt: 2,
            jitterSample: 0.9d,
            previousDelay: TimeSpan.Zero);

        Assert.InRange(low, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200));
        Assert.InRange(high, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200));
        Assert.NotEqual(low, high);
    }

    /// <summary>
    /// Verifies deterministic retry delays, attempt counts, and safe delay diagnostics.
    /// </summary>
    [Fact]
    public async Task SignAsyncUsesDeterministicBackoffAndReportsDiagnostics()
    {
        var client = new FailThenSucceedClient(failuresBeforeSuccess: 3);
        var jitter = new SequenceJitterSource(0d, 1d, 1d);
        var delay = new RecordingRetryDelay();
        var service = new ManagedKeySigningService(
            CreateOptions(maxRetryAttempts: 3),
            client,
            jitter,
            delay);

        SigningResult result = await service.SignAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal(4, client.CallCount);
        Assert.Equal(
            new[]
            {
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(400)
            },
            delay.Delays);
        Assert.Equal("3", result.Metadata.Metadata["retry_attempts"]);
        Assert.Equal("3", result.Metadata.Metadata["retry_delay_count"]);
        Assert.Equal("400", result.Metadata.Metadata["last_retry_delay_milliseconds"]);
        Assert.Equal("650", result.Metadata.Metadata["total_retry_delay_milliseconds"]);
        Assert.Equal("500", result.Metadata.Metadata["max_retry_delay_milliseconds"]);
        Assert.Equal(ManagedKeyRetryBackoff.StrategyName, result.Metadata.Metadata["retry_backoff_strategy"]);
        Assert.Equal("true", result.Metadata.Metadata["retry_delay_applied"]);
    }

    /// <summary>
    /// Verifies terminal failures are neither retried nor delayed.
    /// </summary>
    [Fact]
    public async Task SignAsyncDoesNotDelayOrRetryTerminalFailure()
    {
        var client = new TerminalFailureClient();
        var delay = new RecordingRetryDelay();
        var service = new ManagedKeySigningService(
            CreateOptions(maxRetryAttempts: 3, returnUnsignedOnFailure: true),
            client,
            new SequenceJitterSource(1d),
            delay);

        SigningResult result = await service.SignAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
        Assert.Equal(1, client.CallCount);
        Assert.Empty(delay.Delays);
        Assert.Equal("0", result.Metadata.Metadata["retry_attempts"]);
        Assert.Equal("0", result.Metadata.Metadata["retry_delay_count"]);
        Assert.Equal("false", result.Metadata.Metadata["retry_delay_applied"]);
    }

    /// <summary>
    /// Verifies cancellation interrupts the real delay implementation.
    /// </summary>
    [Fact]
    public async Task SystemRetryDelayHonorsCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await SystemManagedKeyRetryDelay.Instance.DelayAsync(
                TimeSpan.FromSeconds(5),
                cancellationTokenSource.Token));
    }

    /// <summary>
    /// Verifies invalid delay windows, attempts, jitter samples, and previous delays are rejected.
    /// </summary>
    /// <param name="baseDelayMilliseconds">The base delay in milliseconds.</param>
    /// <param name="maxDelayMilliseconds">The maximum delay in milliseconds.</param>
    /// <param name="retryAttempt">The retry attempt number.</param>
    /// <param name="jitterSample">The jitter sample.</param>
    /// <param name="previousDelayMilliseconds">The previous delay in milliseconds.</param>
    /// <param name="expectedParameterName">The expected rejected parameter name.</param>
    [Theory]
    [InlineData(-1, 500, 1, 0.5d, 0, "baseDelay")]
    [InlineData(100, -1, 1, 0.5d, 0, "maxDelay")]
    [InlineData(100, 50, 1, 0.5d, 0, "maxDelay")]
    [InlineData(100, 500, 0, 0.5d, 0, "retryAttempt")]
    [InlineData(100, 500, 1, double.NaN, 0, "jitterSample")]
    [InlineData(100, 500, 1, -0.1d, 0, "jitterSample")]
    [InlineData(100, 500, 1, 1.1d, 0, "jitterSample")]
    [InlineData(100, 500, 1, 0.5d, -1, "previousDelay")]
    [InlineData(100, 500, 1, 0.5d, 501, "previousDelay")]
    public void CalculateDelayRejectsInvalidArguments(
        int baseDelayMilliseconds,
        int maxDelayMilliseconds,
        int retryAttempt,
        double jitterSample,
        int previousDelayMilliseconds,
        string expectedParameterName)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => ManagedKeyRetryBackoff.CalculateDelay(
            TimeSpan.FromMilliseconds(baseDelayMilliseconds),
            TimeSpan.FromMilliseconds(maxDelayMilliseconds),
            retryAttempt,
            jitterSample,
            TimeSpan.FromMilliseconds(previousDelayMilliseconds)));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    /// <summary>
    /// Verifies a previous delay already at the window cap keeps the next delay at that cap.
    /// </summary>
    [Fact]
    public void CalculateDelayHoldsAtCapWhenPreviousDelayReachedUpperBound()
    {
        TimeSpan result = ManagedKeyRetryBackoff.CalculateDelay(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(500),
            retryAttempt: 1,
            jitterSample: 0d,
            previousDelay: TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(100), result);
    }

    /// <summary>
    /// Verifies the real delay implementation completes immediately for a non-positive delay.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task SystemRetryDelayCompletesImmediatelyForZeroDelay()
    {
        ValueTask delay = SystemManagedKeyRetryDelay.Instance.DelayAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.True(delay.IsCompletedSuccessfully);
        await delay;
    }

    /// <summary>
    /// Verifies the signing exception falls back to the failure code when no message is supplied.
    /// </summary>
    /// <param name="message">The optional exception message.</param>
    /// <param name="expectedMessage">The expected exception message.</param>
    [Theory]
    [InlineData(null, "managed-key.failed")]
    [InlineData(" ", "managed-key.failed")]
    [InlineData("Provider failed.", "Provider failed.")]
    public void SigningExceptionUsesFailureCodeWhenMessageIsMissing(string? message, string expectedMessage)
    {
        var exception = new ManagedKeySigningException(" managed-key.failed ", message, isRetryable: true);

        Assert.Equal(expectedMessage, exception.Message.Trim());
        Assert.Equal("managed-key.failed", exception.FailureCode);
        Assert.True(exception.IsRetryable);
    }

    private static ManagedKeySigningOptions CreateOptions(
        int maxRetryAttempts,
        bool returnUnsignedOnFailure = false)
    {
        var options = ManagedKeySigningOptions.Create(
            keyId: "managed-key-1",
            keyVersion: "v1",
            providerName: "managed-key-test",
            signatureAlgorithm: "TEST-SIGNATURE",
            returnUnsignedOnFailure: returnUnsignedOnFailure,
            maxRetryAttempts: maxRetryAttempts,
            retryDelay: TimeSpan.FromMilliseconds(100));
        options.MaxRetryDelay = TimeSpan.FromMilliseconds(500);
        options.Validate();
        return options;
    }

    private static SigningRequest CreateRequest()
    {
        return new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-256",
            keyId: "managed-key-1",
            keyVersion: "v1");
    }

    private sealed class SequenceJitterSource(params double[] samples) : IManagedKeyRetryJitterSource
    {
        private int index;

        public double NextDouble()
        {
            return samples[Math.Min(index++, samples.Length - 1)];
        }
    }

    private sealed class RecordingRetryDelay : IManagedKeyRetryDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailThenSucceedClient(int failuresBeforeSuccess) : IManagedKeySigningClient
    {
        public int CallCount { get; private set; }

        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            return CallCount <= failuresBeforeSuccess
                ? throw new ManagedKeySigningException(
                    "managedkey.signing.provider-unavailable",
                    "provider unavailable",
                    isRetryable: true)
                : ValueTask.FromResult(ManagedKeySignResult.Create(
                "signature",
                request.SignatureAlgorithm,
                request.KeyId,
                request.KeyVersion,
                SignedUtc));
        }
    }

    private sealed class TerminalFailureClient : IManagedKeySigningClient
    {
        public int CallCount { get; private set; }

        public ValueTask<ManagedKeySignResult> SignAsync(
            ManagedKeySignRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new ManagedKeySigningException(
                "managedkey.signing.failed",
                "terminal failure",
                isRetryable: false);
        }
    }
}
