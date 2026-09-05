using AsiBackbone.Core.Outbox;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Unit tests for the <see cref="AsiBackboneGovernanceOutboxOptions"/> class.
/// </summary>
public sealed class AsiBackboneGovernanceOutboxOptionsTests
{
    /// <summary>
    /// Validates that the default options are accepted without throwing exceptions.
    /// </summary>
    [Fact]
    public void ValidateAcceptsDefaultOptions()
    {
        var options = new AsiBackboneGovernanceOutboxOptions();

        options.Validate();

        Assert.True(options.UseClaimLeases);
        Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultClaimWorkerId, options.ClaimWorkerId);
        Assert.True(options.ClaimLeaseDuration > TimeSpan.Zero);
        Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultClaimPageSize, options.ClaimPageSize);
        Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultMaxClaimAttempts, options.MaxClaimAttempts);
        Assert.True(options.DeadLetterOnMaxClaimAttempts);
    }

    /// <summary>
    /// Validates that the default claim worker identifier distinguishes the machine and process so replicas do not share a claim owner.
    /// </summary>
    [Fact]
    public void DefaultClaimWorkerIdCombinesMachineNameAndProcessId()
    {
        Assert.Equal(
            $"{Environment.MachineName}:{Environment.ProcessId}",
            AsiBackboneGovernanceOutboxOptions.DefaultClaimWorkerId);
    }

    /// <summary>
    /// Validates that a non-positive claim page size is rejected.
    /// </summary>
    [Fact]
    public void ValidateRejectsNonPositiveClaimPageSize()
    {
        var options = new AsiBackboneGovernanceOutboxOptions { ClaimPageSize = 0 };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(AsiBackboneGovernanceOutboxOptions.ClaimPageSize), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates that a non-positive maximum claim attempt threshold is rejected.
    /// </summary>
    [Fact]
    public void ValidateRejectsNonPositiveMaxClaimAttempts()
    {
        var options = new AsiBackboneGovernanceOutboxOptions { MaxClaimAttempts = 0 };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(AsiBackboneGovernanceOutboxOptions.MaxClaimAttempts), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates that clearing the worker identifier while claim leases remain enabled is rejected.
    /// </summary>
    [Fact]
    public void ValidateRejectsClearedClaimWorkerIdWhileClaimLeasesRemainEnabled()
    {
        var options = new AsiBackboneGovernanceOutboxOptions { ClaimWorkerId = null };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(AsiBackboneGovernanceOutboxOptions.ClaimWorkerId), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates that the options with claim leases and a valid worker ID are accepted without throwing exceptions.
    /// </summary>
    [Fact]
    public void ValidateAcceptsClaimLeaseOptionsWithWorkerId()
    {
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            UseClaimLeases = true,
            ClaimWorkerId = "worker-1",
            ClaimLeaseDuration = TimeSpan.FromMinutes(2)
        };

        options.Validate();
    }

    /// <summary>
    /// Validates that the options with invalid timing values are rejected.
    /// </summary>
    [Fact]
    public void ValidateRejectsInvalidTimingOptions()
    {
        _ = Assert.Throws<InvalidOperationException>(() => new AsiBackboneGovernanceOutboxOptions
        {
            RetryDelay = TimeSpan.FromTicks(-1)
        }.Validate());

        _ = Assert.Throws<InvalidOperationException>(() => new AsiBackboneGovernanceOutboxOptions
        {
            DeferredDelay = TimeSpan.FromTicks(-1)
        }.Validate());

        _ = Assert.Throws<InvalidOperationException>(() => new AsiBackboneGovernanceOutboxOptions
        {
            ClaimLeaseDuration = TimeSpan.Zero
        }.Validate());
    }

    /// <summary>
    /// Validates that the options require a worker ID when claim leases are enabled.
    /// </summary>
    [Fact]
    public void ValidateRequiresWorkerIdWhenClaimLeasesAreEnabled()
    {
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            UseClaimLeases = true,
            ClaimWorkerId = " "
        };

        _ = Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
