using System.Reflection;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Focused characterization coverage for the claim-merging stage used by the governance outbox drain.
/// </summary>
public sealed class GovernanceOutboxClaimMergeTests
{
    private static readonly DateTimeOffset MergeUtc = new(2026, 7, 11, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedUtc = MergeUtc.AddHours(-1);
    private static readonly MergeClaimsDelegate MergeClaims = CreateMergeClaimsDelegate();

    /// <summary>
    /// Verifies the retry-ready input is returned directly when no pending claims were supplied.
    /// </summary>
    [Fact]
    public void MergeClaimsReturnsRetryReadyInputWhenPendingInputIsEmpty()
    {
        GovernanceOutboxClaim first = CreateActiveClaim("outbox-retry-1", "worker-a", "retry-token-1");
        GovernanceOutboxClaim second = CreateActiveClaim("outbox-retry-2", "worker-a", "retry-token-2");
        IReadOnlyList<GovernanceOutboxClaim> pendingClaims = Array.Empty<GovernanceOutboxClaim>();
        IReadOnlyList<GovernanceOutboxClaim> retryReadyClaims = [first, second];

        IReadOnlyList<GovernanceOutboxClaim> merged = MergeClaims(
            pendingClaims,
            retryReadyClaims,
            maxCount: 2);

        Assert.Same(retryReadyClaims, merged);
        Assert.Collection(
            merged,
            claim => Assert.Same(first, claim),
            claim => Assert.Same(second, claim));
    }

    /// <summary>
    /// Verifies the pending input is returned directly when no retry-ready claims were supplied.
    /// </summary>
    [Fact]
    public void MergeClaimsReturnsPendingInputWhenRetryReadyInputIsEmpty()
    {
        GovernanceOutboxClaim first = CreateActiveClaim("outbox-pending-1", "worker-a", "pending-token-1");
        GovernanceOutboxClaim second = CreateActiveClaim("outbox-pending-2", "worker-a", "pending-token-2");
        IReadOnlyList<GovernanceOutboxClaim> pendingClaims = [first, second];
        IReadOnlyList<GovernanceOutboxClaim> retryReadyClaims = Array.Empty<GovernanceOutboxClaim>();

        IReadOnlyList<GovernanceOutboxClaim> merged = MergeClaims(
            pendingClaims,
            retryReadyClaims,
            maxCount: 2);

        Assert.Same(pendingClaims, merged);
        Assert.Collection(
            merged,
            claim => Assert.Same(first, claim),
            claim => Assert.Same(second, claim));
    }

    /// <summary>
    /// Verifies both empty inputs preserve the established empty fast-path behavior.
    /// </summary>
    [Fact]
    public void MergeClaimsReturnsEmptyRetryInputWhenBothInputsAreEmpty()
    {
        IReadOnlyList<GovernanceOutboxClaim> pendingClaims = Array.Empty<GovernanceOutboxClaim>();
        IReadOnlyList<GovernanceOutboxClaim> retryReadyClaims = Array.Empty<GovernanceOutboxClaim>();

        IReadOnlyList<GovernanceOutboxClaim> merged = MergeClaims(
            pendingClaims,
            retryReadyClaims,
            maxCount: 10);

        Assert.Same(retryReadyClaims, merged);
        Assert.Empty(merged);
    }

    /// <summary>
    /// Verifies pending claims win overlapping entry identities and retry-ready duplicates are removed in stable order.
    /// </summary>
    [Fact]
    public void MergeClaimsPrefersPendingClaimAcrossOverlappingRetrySnapshots()
    {
        GovernanceOutboxClaim pending = CreateActiveClaim("outbox-shared", "worker-a", "current-token");
        GovernanceOutboxClaim stale = CreateClaim(
            "outbox-shared",
            "worker-a",
            "stale-token",
            MergeUtc.AddMinutes(-20),
            MergeUtc.AddMinutes(-15));
        GovernanceOutboxClaim expired = CreateClaim(
            "outbox-shared",
            "worker-a",
            "current-token",
            MergeUtc.AddMinutes(-5),
            MergeUtc.AddMinutes(-1));
        GovernanceOutboxClaim terminal = CreateClaim(
            "outbox-shared",
            "worker-a",
            "terminal-token",
            MergeUtc.AddMinutes(-1),
            MergeUtc.AddMinutes(4),
            GovernanceEmissionStatus.Delivered);
        GovernanceOutboxClaim conflictingOwner = CreateActiveClaim(
            "outbox-shared",
            "worker-b",
            "conflicting-token");
        GovernanceOutboxClaim firstRetry = CreateActiveClaim("outbox-retry-1", "worker-a", "retry-token-1");
        GovernanceOutboxClaim duplicateRetry = CreateActiveClaim("outbox-retry-1", "worker-a", "retry-token-1-copy");
        GovernanceOutboxClaim secondRetry = CreateActiveClaim("outbox-retry-2", "worker-a", "retry-token-2");

        IReadOnlyList<GovernanceOutboxClaim> merged = MergeClaims(
            [pending],
            [stale, expired, terminal, conflictingOwner, firstRetry, duplicateRetry, secondRetry],
            maxCount: 10);

        Assert.Collection(
            merged,
            claim => Assert.Same(pending, claim),
            claim => Assert.Same(firstRetry, claim),
            claim => Assert.Same(secondRetry, claim));
    }

    /// <summary>
    /// Verifies merging remains an identity and ordering operation rather than revalidating store-owned claim eligibility.
    /// </summary>
    [Fact]
    public void MergeClaimsPreservesUniqueClaimsWithoutRevalidatingLeaseOrEntryState()
    {
        GovernanceOutboxClaim pending = CreateActiveClaim("outbox-pending", "worker-a", "pending-token");
        GovernanceOutboxClaim expired = CreateClaim(
            "outbox-expired",
            "worker-a",
            "expired-token",
            MergeUtc.AddMinutes(-5),
            MergeUtc.AddMinutes(-1));
        GovernanceOutboxClaim terminal = CreateClaim(
            "outbox-terminal",
            "worker-a",
            "terminal-token",
            MergeUtc.AddMinutes(-1),
            MergeUtc.AddMinutes(4),
            GovernanceEmissionStatus.DeadLettered);
        GovernanceOutboxClaim conflictingOwner = CreateActiveClaim(
            "outbox-other-owner",
            "worker-b",
            "other-owner-token");

        IReadOnlyList<GovernanceOutboxClaim> merged = MergeClaims(
            [pending],
            [expired, terminal, conflictingOwner],
            maxCount: 4);

        Assert.Collection(
            merged,
            claim => Assert.Same(pending, claim),
            claim => Assert.Same(expired, claim),
            claim => Assert.Same(terminal, claim),
            claim => Assert.Same(conflictingOwner, claim));
    }

    /// <summary>
    /// Verifies overlap does not consume the drain budget and the first unique retry-ready claim fills the remaining slot.
    /// </summary>
    [Fact]
    public void MergeClaimsStopsAtDrainLimitAfterSkippingOverlap()
    {
        GovernanceOutboxClaim firstPending = CreateActiveClaim("outbox-pending-1", "worker-a", "pending-token-1");
        GovernanceOutboxClaim secondPending = CreateActiveClaim("outbox-pending-2", "worker-a", "pending-token-2");
        GovernanceOutboxClaim overlappingRetry = CreateActiveClaim("outbox-pending-1", "worker-a", "overlap-token");
        GovernanceOutboxClaim includedRetry = CreateActiveClaim("outbox-retry-1", "worker-a", "retry-token-1");
        GovernanceOutboxClaim excludedRetry = CreateActiveClaim("outbox-retry-2", "worker-a", "retry-token-2");

        IReadOnlyList<GovernanceOutboxClaim> merged = MergeClaims(
            [firstPending, secondPending],
            [overlappingRetry, includedRetry, excludedRetry],
            maxCount: 3);

        Assert.Collection(
            merged,
            claim => Assert.Same(firstPending, claim),
            claim => Assert.Same(secondPending, claim),
            claim => Assert.Same(includedRetry, claim));
        Assert.DoesNotContain(excludedRetry, merged);
    }

    /// <summary>
    /// Verifies claim identity comparison remains ordinal and therefore case-sensitive.
    /// </summary>
    [Fact]
    public void MergeClaimsUsesOrdinalOutboxEntryIdentity()
    {
        GovernanceOutboxClaim pending = CreateActiveClaim("OUTBOX-CASE", "worker-a", "pending-token");
        GovernanceOutboxClaim retryReady = CreateActiveClaim("outbox-case", "worker-a", "retry-token");

        IReadOnlyList<GovernanceOutboxClaim> merged = MergeClaims(
            [pending],
            [retryReady],
            maxCount: 2);

        Assert.Collection(
            merged,
            claim => Assert.Same(pending, claim),
            claim => Assert.Same(retryReady, claim));
    }

    private static GovernanceOutboxClaim CreateActiveClaim(
        string outboxEntryId,
        string workerId,
        string claimToken)
    {
        return CreateClaim(
            outboxEntryId,
            workerId,
            claimToken,
            MergeUtc.AddMinutes(-1),
            MergeUtc.AddMinutes(4));
    }

    private static GovernanceOutboxClaim CreateClaim(
        string outboxEntryId,
        string workerId,
        string claimToken,
        DateTimeOffset claimedUtc,
        DateTimeOffset claimExpiresUtc,
        GovernanceEmissionStatus status = GovernanceEmissionStatus.Pending)
    {
        var entry = GovernanceOutboxEntry.Restore(
            CreateEnvelope($"{outboxEntryId}-{claimToken}"),
            status,
            outboxEntryId,
            CreatedUtc,
            claimedUtc,
            claimOwner: workerId,
            claimToken: claimToken,
            claimedUtc: claimedUtc,
            claimExpiresUtc: claimExpiresUtc,
            claimAttemptCount: 1);

        return GovernanceOutboxClaim.Create(
            entry,
            workerId,
            claimToken,
            claimedUtc,
            claimExpiresUtc);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string eventId)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Outbox,
            eventId,
            CreatedUtc,
            envelopeId: $"envelope-{eventId}",
            correlationId: "outbox-claim-merge-coverage",
            emitterStatus: GovernanceEmissionStatus.Pending.ToString(),
            emitterProvider: "test-outbox");
    }

    private static MergeClaimsDelegate CreateMergeClaimsDelegate()
    {
        MethodInfo method = typeof(GovernanceOutboxDrain).GetMethod(
            "MergeClaims",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(IReadOnlyList<GovernanceOutboxClaim>),
                typeof(IReadOnlyList<GovernanceOutboxClaim>),
                typeof(int)
            ],
            modifiers: null)
            ?? throw new InvalidOperationException("The outbox drain claim merge method was not found.");

        return method.CreateDelegate<MergeClaimsDelegate>();
    }

    private delegate IReadOnlyList<GovernanceOutboxClaim> MergeClaimsDelegate(
        IReadOnlyList<GovernanceOutboxClaim> pendingClaims,
        IReadOnlyList<GovernanceOutboxClaim> retryReadyClaims,
        int maxCount);
}
