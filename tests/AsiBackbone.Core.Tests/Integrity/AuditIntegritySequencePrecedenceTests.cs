#pragma warning disable CS1591

using AsiBackbone.Core.Integrity;
using AsiBackbone.Core.Serialization;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Integrity;

public sealed class AuditIntegritySequencePrecedenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VerifyClassifiesAdjacentDuplicateAsForkedChain()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        AuditIntegrityLink duplicate = RehydrateWithRecordId(second, "record-2-duplicate");

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first, second, duplicate], "audit-ledger");

        AssertFailure(result, AuditIntegrityVerificationCategory.ForkedChain, "integrity.sequence-duplicate");
    }

    [Fact]
    public void VerifyClassifiesNonAdjacentDuplicateAsForkedChainBeforeReorderedRecord()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        var third = AuditIntegrityLink.Append(second, CreateRecordHash("record-3"), Now.AddSeconds(2));
        AuditIntegrityLink duplicate = RehydrateWithRecordId(second, "record-2-late-duplicate");

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first, second, third, duplicate], "audit-ledger");

        AssertFailure(result, AuditIntegrityVerificationCategory.ForkedChain, "integrity.sequence-duplicate");
    }

    [Fact]
    public void VerifyClassifiesUniqueBackwardSequenceAsReorderedRecord()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        var third = AuditIntegrityLink.Append(second, CreateRecordHash("record-3"), Now.AddSeconds(2));
        var sequenceFour = AuditIntegrityLink.Append(third, CreateRecordHash("record-4"), Now.AddSeconds(3));
        var sequenceFive = AuditIntegrityLink.Append(sequenceFour, CreateRecordHash("record-5"), Now.AddSeconds(4));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
            [sequenceFive, sequenceFour],
            "audit-ledger",
            requireGenesis: false,
            expectedPreviousLinkHash: sequenceFour.LinkHash);

        AssertFailure(result, AuditIntegrityVerificationCategory.ReorderedRecord, "integrity.sequence-reordered");
        Assert.Equal("6", result.SafeMetadata["expected_sequence"]);
        Assert.Equal("4", result.SafeMetadata["actual_sequence"]);
    }

    [Fact]
    public void VerifyClassifiesForwardSequenceGapAsMissingRecord()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        AuditIntegrityLink third = CreateComputedLink(3, first.LinkHash, "record-3");

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first, third], "audit-ledger");

        AssertFailure(result, AuditIntegrityVerificationCategory.MissingRecord, "integrity.sequence-missing");
        Assert.Equal("2", result.SafeMetadata["expected_sequence"]);
        Assert.Equal("3", result.SafeMetadata["actual_sequence"]);
    }

    [Fact]
    public void VerifyClassifiesConflictingLinksClaimingSameSequenceAsForkedChain()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var accepted = AuditIntegrityLink.Append(first, CreateRecordHash("record-2a"), Now.AddSeconds(1));
        AuditIntegrityLink conflicting = CreateComputedLink(2, first.LinkHash, "record-2b");

        Assert.NotEqual(accepted.LinkHash, conflicting.LinkHash);

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first, accepted, conflicting], "audit-ledger");

        AssertFailure(result, AuditIntegrityVerificationCategory.ForkedChain, "integrity.sequence-duplicate");
    }

    [Fact]
    public void VerifyDoesNotRecordRejectedSequenceAsAccepted()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        AuditIntegrityLink invalidSecond = CreateComputedLink(2, "wrong-previous-hash", "record-2");
        AuditIntegrityLink validSecond = CreateComputedLink(2, first.LinkHash, "record-2-valid");

        AuditIntegrityVerificationResult firstAttempt = AuditIntegrityVerifier.Verify([first, invalidSecond, validSecond], "audit-ledger");

        AssertFailure(firstAttempt, AuditIntegrityVerificationCategory.HashMismatch, "integrity.previous-link-hash-mismatch");
    }

    private static AuditIntegrityLink RehydrateWithRecordId(AuditIntegrityLink source, string recordId)
    {
        return AuditIntegrityLink.Rehydrate(
            source.ChainId,
            source.Sequence,
            recordId,
            source.RecordType,
            source.RecordHash,
            source.PreviousLinkHash,
            source.LinkHash,
            source.HashAlgorithm,
            source.CanonicalizationVersion,
            source.SchemaVersion,
            source.CreatedUtc);
    }

    private static AuditIntegrityLink CreateComputedLink(long sequence, string previousLinkHash, string recordId)
    {
        CanonicalPayloadHash recordHash = CreateRecordHash(recordId);
        var draft = AuditIntegrityLink.Rehydrate(
            "audit-ledger",
            sequence,
            recordId,
            recordHash.ArtifactType,
            recordHash.HashValue,
            previousLinkHash,
            recordHash.HashValue,
            recordHash.HashAlgorithm,
            recordHash.CanonicalizationVersion,
            AsiBackboneSchemaVersions.StableArtifactsV1,
            Now.AddSeconds(sequence));

        return AuditIntegrityLink.Rehydrate(
            draft.ChainId,
            draft.Sequence,
            draft.RecordId,
            draft.RecordType,
            draft.RecordHash,
            draft.PreviousLinkHash,
            draft.ComputeExpectedLinkHash(),
            draft.HashAlgorithm,
            draft.CanonicalizationVersion,
            draft.SchemaVersion,
            draft.CreatedUtc);
    }

    private static CanonicalPayloadHash CreateRecordHash(string recordId)
    {
        var payload = CanonicalPayload.Create(
            CanonicalArtifactTypes.AuditLedgerRecord,
            recordId,
            AsiBackboneSchemaVersions.StableArtifactsV1,
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["recordId"] = recordId,
                ["outcome"] = "Allowed"
            });

        return CanonicalPayloadHasher.ComputeHash(payload);
    }

    private static void AssertFailure(
        AuditIntegrityVerificationResult result,
        AuditIntegrityVerificationCategory category,
        string failureCode)
    {
        Assert.False(result.IsValid);
        Assert.Equal(category, result.Category);
        Assert.Equal(failureCode, result.FailureCode);
    }
}

#pragma warning restore CS1591
