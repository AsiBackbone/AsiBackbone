using AsiBackbone.AspNetCore.Outbox;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Outbox;

/// <summary>
/// Tests validation branches for <see cref="GovernanceOutboxDrainWorkerOptions"/>.
/// </summary>
public sealed class AsiBackboneGovernanceOutboxDrainWorkerOptionsTests
{
    /// <summary>
    /// Verifies that non-positive batch sizes are rejected.
    /// </summary>
    /// <param name="batchSize">The invalid batch size.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateRejectsNonPositiveBatchSize(int batchSize)
    {
        GovernanceOutboxDrainWorkerOptions options = CreateValidOptions();
        options.BatchSize = batchSize;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("batch size", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that zero and negative polling intervals are rejected.
    /// </summary>
    /// <param name="ticks">The invalid interval in ticks.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ValidateRejectsNonPositivePollingInterval(long ticks)
    {
        GovernanceOutboxDrainWorkerOptions options = CreateValidOptions();
        options.PollingInterval = TimeSpan.FromTicks(ticks);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("polling interval", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that zero and negative failure delays are rejected.
    /// </summary>
    /// <param name="ticks">The invalid delay in ticks.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ValidateRejectsNonPositiveFailureDelay(long ticks)
    {
        GovernanceOutboxDrainWorkerOptions options = CreateValidOptions();
        options.FailureDelay = TimeSpan.FromTicks(ticks);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("failure delay", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that zero and negative shutdown-drain timeouts are rejected.
    /// </summary>
    /// <param name="ticks">The invalid timeout in ticks.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ValidateRejectsNonPositiveShutdownDrainTimeout(long ticks)
    {
        GovernanceOutboxDrainWorkerOptions options = CreateValidOptions();
        options.ShutdownDrainTimeout = TimeSpan.FromTicks(ticks);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("shutdown timeout", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a null retry clock is rejected.
    /// </summary>
    [Fact]
    public void ValidateRejectsNullRetryClock()
    {
        GovernanceOutboxDrainWorkerOptions options = CreateValidOptions();
        options.RetryClock = null!;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("retry clock", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the smallest positive boundary values are accepted.
    /// </summary>
    [Fact]
    public void ValidateAcceptsBoundaryValidValues()
    {
        GovernanceOutboxDrainWorkerOptions options = CreateValidOptions();

        options.Validate();
    }

    private static GovernanceOutboxDrainWorkerOptions CreateValidOptions()
    {
        return new GovernanceOutboxDrainWorkerOptions
        {
            BatchSize = 1,
            PollingInterval = TimeSpan.FromTicks(1),
            FailureDelay = TimeSpan.FromTicks(1),
            ShutdownDrainTimeout = TimeSpan.FromTicks(1),
            RetryClock = static () => DateTimeOffset.UnixEpoch,
        };
    }
}
