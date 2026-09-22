using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Tests for the <see cref="GovernanceOutboxClaimRequest"/> and <see cref="GovernanceOutboxClaim"/> models, including validation and normalization of values, as well as lease state evaluation for outbox entries.
/// </summary>
public sealed class GovernanceOutboxClaimModelTests
{
    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxClaimRequest.Create"/> method normalizes input values correctly, including trimming whitespace from the worker ID and converting the current time to UTC.
    /// </summary>
    [Fact]
    public void ClaimRequestCreateNormalizesValues()
    {
        DateTimeOffset now = new(2026, 7, 8, 12, 0, 0, TimeSpan.FromHours(-4));

        var request = GovernanceOutboxClaimRequest.Create(
            " worker-1 ",
            now,
            TimeSpan.FromMinutes(3),
            maxCount: 7);

        Assert.Equal("worker-1", request.WorkerId);
        Assert.Equal(now.ToUniversalTime(), request.UtcNow);
        Assert.Equal(TimeSpan.FromMinutes(3), request.LeaseDuration);
        Assert.Equal(7, request.MaxCount);
        Assert.Equal(now.ToUniversalTime().AddMinutes(3), request.ClaimExpiresUtc);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxClaimRequest.Create"/> method rejects invalid input values.
    /// </summary>
    [Fact]
    public void ClaimRequestCreateRejectsInvalidValues()
    {
        _ = Assert.Throws<ArgumentException>(() => GovernanceOutboxClaimRequest.Create(" "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxClaimRequest.Create("worker-1", leaseDuration: TimeSpan.Zero));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxClaimRequest.Create("worker-1", maxCount: 0));
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxClaim.Create"/> method normalizes input values correctly, including trimming whitespace from the worker ID and claim token, and evaluates the expiration state of the claim.
    /// </summary>
    [Fact]
    public void ClaimCreateNormalizesAndEvaluatesExpiration()
    {
        GovernanceOutboxEntry entry = CreateEntry();
        DateTimeOffset claimedUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresUtc = claimedUtc.AddMinutes(5);

        var claim = GovernanceOutboxClaim.Create(
            entry,
            " worker-1 ",
            " token-1 ",
            claimedUtc,
            expiresUtc);

        Assert.Equal("worker-1", claim.WorkerId);
        Assert.Equal("token-1", claim.ClaimToken);
        Assert.False(claim.IsExpired(expiresUtc.AddTicks(-1)));
        Assert.True(claim.IsExpired(expiresUtc));
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxClaim.Create"/> method rejects invalid expiration values, such as when the expiration time is before the claimed time.
    /// </summary>
    [Fact]
    public void ClaimCreateRejectsInvalidExpiration()
    {
        GovernanceOutboxEntry entry = CreateEntry();
        DateTimeOffset claimedUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceOutboxClaim.Create(
            entry,
            "worker-1",
            "token-1",
            claimedUtc,
            claimedUtc));
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxEntry"/> methods for claiming and releasing entries correctly evaluate the lease state, including whether an entry has an active claim, can be claimed, and whether it is claimed by a specific claim.
    /// </summary>
    [Fact]
    public void OutboxEntryClaimHelpersEvaluateLeaseState()
    {
        GovernanceOutboxEntry entry = CreateEntry();
        DateTimeOffset claimedUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

        GovernanceOutboxEntry claimedEntry = entry.MarkClaimed(
            "worker-1",
            "token-1",
            claimedUtc,
            TimeSpan.FromMinutes(5));
        var matchingClaim = GovernanceOutboxClaim.Create(
            claimedEntry,
            "worker-1",
            "token-1",
            claimedUtc,
            claimedUtc.AddMinutes(5));
        var otherClaim = GovernanceOutboxClaim.Create(
            claimedEntry,
            "worker-1",
            "token-2",
            claimedUtc,
            claimedUtc.AddMinutes(5));

        Assert.True(claimedEntry.HasClaim);
        Assert.True(claimedEntry.HasActiveClaim(claimedUtc.AddMinutes(1)));
        Assert.False(claimedEntry.CanBeClaimed(claimedUtc.AddMinutes(1)));
        Assert.False(claimedEntry.HasActiveClaim(claimedUtc.AddMinutes(5)));
        Assert.True(claimedEntry.CanBeClaimed(claimedUtc.AddMinutes(5)));
        Assert.True(claimedEntry.IsClaimedBy(matchingClaim));
        Assert.False(claimedEntry.IsClaimedBy(otherClaim));
        Assert.Equal(1, claimedEntry.ClaimAttemptCount);

        GovernanceOutboxEntry releasedEntry = claimedEntry.ReleaseClaim(claimedUtc.AddMinutes(2));
        Assert.False(releasedEntry.HasClaim);
        Assert.True(releasedEntry.CanBeClaimed(claimedUtc.AddMinutes(2)));
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxEntry.MarkClaimed"/> method rejects invalid lease durations, such as a zero or negative duration.
    /// </summary>
    [Fact]
    public void OutboxEntryMarkClaimedRejectsInvalidLeaseDuration()
    {
        GovernanceOutboxEntry entry = CreateEntry();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => entry.MarkClaimed(
            "worker-1",
            leaseDuration: TimeSpan.Zero));
    }

    private static GovernanceOutboxEntry CreateEntry()
    {
        return GovernanceOutboxEntry.Create(GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.DecisionReceipt,
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
            envelopeId: "envelope-1"));
    }
}
