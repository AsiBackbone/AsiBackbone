using AsiBackbone.Core.Integrity;
using AsiBackbone.Core.Serialization;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Integrity;

/// <summary>
/// Unit tests for the <see cref="AuditIntegrityVerifier"/> class, which verifies the integrity of an audit ledger chain of links.
/// </summary>
public sealed class AuditIntegrityVerifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly validates a continuous chain of audit integrity links.
    /// </summary>
    [Fact]
    public void VerifyAcceptsContinuousChain()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        var third = AuditIntegrityLink.Append(second, CreateRecordHash("record-3"), Now.AddSeconds(2));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first, second, third], "audit-ledger");

        Assert.True(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.Valid, result.Category);
        Assert.Equal("audit-ledger", result.ChainId);
        Assert.Equal("3", result.SafeMetadata["link_count"]);
        Assert.Equal(third.LinkHash, result.SafeMetadata["tip_hash"]);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityLink.Append"/> method correctly binds the previous link hash and increments the sequence number.
    /// </summary>
    [Fact]
    public void AppendBindsPreviousHashAndNextSequence()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));

        Assert.True(first.IsGenesis);
        Assert.False(second.IsGenesis);
        Assert.Equal(2, second.Sequence);
        Assert.Equal(first.LinkHash, second.PreviousLinkHash);
        Assert.NotEqual(first.LinkHash, second.LinkHash);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly detects modified link fields.
    /// </summary>
    [Fact]
    public void VerifyDetectsModifiedLinkFields()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var modified = AuditIntegrityLink.Rehydrate(
            first.ChainId,
            first.Sequence,
            first.RecordId,
            first.RecordType,
            MutateHash(first.RecordHash),
            first.PreviousLinkHash,
            first.LinkHash,
            first.HashAlgorithm,
            first.CanonicalizationVersion,
            first.SchemaVersion,
            first.CreatedUtc);

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([modified], "audit-ledger");

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.ModifiedRecord, result.Category);
        Assert.Equal("integrity.link-hash-mismatch", result.FailureCode);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly detects a missing sequence in the chain of links.
    /// </summary>
    [Fact]
    public void VerifyDetectsMissingSequence()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        var third = AuditIntegrityLink.Append(second, CreateRecordHash("record-3"), Now.AddSeconds(2));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first, third], "audit-ledger");

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.MissingRecord, result.Category);
        Assert.Equal("integrity.sequence-missing", result.FailureCode);
        Assert.Equal("2", result.SafeMetadata["expected_sequence"]);
        Assert.Equal("3", result.SafeMetadata["actual_sequence"]);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly detects a forked sequence in the chain of links.
    /// </summary>
    [Fact]
    public void VerifyDetectsForkedSequence()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        var fork = AuditIntegrityLink.Rehydrate(
            second.ChainId,
            second.Sequence,
            "record-2b",
            second.RecordType,
            second.RecordHash,
            second.PreviousLinkHash,
            second.LinkHash,
            second.HashAlgorithm,
            second.CanonicalizationVersion,
            second.SchemaVersion,
            second.CreatedUtc);

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first, second, fork], "audit-ledger");

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.ForkedChain, result.Category);
        Assert.Equal("integrity.sequence-duplicate", result.FailureCode);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly detects a mismatch in the previous link hash of a link in the chain.
    /// </summary>
    [Fact]
    public void VerifyDetectsPreviousHashMismatch()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        var broken = AuditIntegrityLink.Rehydrate(
            second.ChainId,
            second.Sequence,
            second.RecordId,
            second.RecordType,
            second.RecordHash,
            MutateHash(second.PreviousLinkHash),
            second.LinkHash,
            second.HashAlgorithm,
            second.CanonicalizationVersion,
            second.SchemaVersion,
            second.CreatedUtc);

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first, broken], "audit-ledger");

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.HashMismatch, result.Category);
        Assert.Equal("integrity.previous-link-hash-mismatch", result.FailureCode);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly rejects an empty chain of links.
    /// </summary>
    [Fact]
    public void VerifyRejectsEmptyChain()
    {
        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([]);

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.EmptyChain, result.Category);
        Assert.Equal("integrity.chain-empty", result.FailureCode);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly detects a mismatch in the chain ID of a link in the chain.
    /// </summary>  
    [Fact]
    public void VerifyDetectsWrongChain()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([first], "other-ledger");

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.WrongChain, result.Category);
        Assert.Equal("integrity.chain-id-mismatch", result.FailureCode);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly detects an unsupported hash algorithm in a link in the chain.
    /// </summary>
    [Fact]
    public void VerifyDetectsUnsupportedHashAlgorithm()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var unsupported = AuditIntegrityLink.Rehydrate(
            first.ChainId,
            first.Sequence,
            first.RecordId,
            first.RecordType,
            first.RecordHash,
            first.PreviousLinkHash,
            first.LinkHash,
            "SHA-512",
            first.CanonicalizationVersion,
            first.SchemaVersion,
            first.CreatedUtc);

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([unsupported], "audit-ledger");

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.UnsupportedAlgorithm, result.Category);
        Assert.Equal("integrity.hash-algorithm-unsupported", result.FailureCode);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityVerifier.Verify"/> method correctly detects a previous link hash present in a genesis link, which is not allowed.
    /// </summary>
    [Fact]
    public void VerifyDetectsGenesisPreviousHash()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var broken = AuditIntegrityLink.Rehydrate(
            first.ChainId,
            first.Sequence,
            first.RecordId,
            first.RecordType,
            first.RecordHash,
            "unexpected-previous-hash",
            first.LinkHash,
            first.HashAlgorithm,
            first.CanonicalizationVersion,
            first.SchemaVersion,
            first.CreatedUtc);

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify([broken], "audit-ledger");

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.HashMismatch, result.Category);
        Assert.Equal("integrity.genesis-previous-hash-present", result.FailureCode);
    }

    /// <summary>
    /// Tests that a genuine chain suffix verifies when anchored by the hash of the link preceding it.
    /// </summary>
    [Fact]
    public void VerifyAcceptsGenuinePartialChainAnchoredByPreviousLinkHash()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        var third = AuditIntegrityLink.Append(second, CreateRecordHash("record-3"), Now.AddSeconds(2));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
            [second, third],
            "audit-ledger",
            requireGenesis: false,
            expectedPreviousLinkHash: first.LinkHash);

        Assert.True(result.IsValid);
        Assert.Equal("2", result.SafeMetadata["link_count"]);
        Assert.Equal(third.LinkHash, result.SafeMetadata["tip_hash"]);
    }

    /// <summary>
    /// Tests that a partial chain is rejected when the caller supplies no anchoring previous link hash.
    /// </summary>
    /// <remarks>
    /// Without an anchor there is nothing to distinguish a genuine suffix from a rewritten one, so the verifier fails closed
    /// rather than assuming the suffix begins the chain.
    /// </remarks>
    [Fact]
    public void VerifyRejectsPartialChainWithoutAnAnchor()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
            [second],
            "audit-ledger",
            requireGenesis: false);

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.MissingAnchor, result.Category);
        Assert.Equal("integrity.expected-previous-hash-required", result.FailureCode);
    }

    /// <summary>
    /// Tests that a forged restart claiming no predecessor is rejected.
    /// </summary>
    /// <remarks>
    /// An attacker who rewrote links from sequence N onward with an empty previous hash, recomputing each link hash, used to
    /// produce a chain the verifier accepted while it rejected the genuine one.
    /// </remarks>
    [Fact]
    public void VerifyRejectsForgedRestartThatClaimsNoPredecessor()
    {
        AuditIntegrityLink forged = CreateComputedLink(5, CreateRecordHash("record-5"));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
            [forged],
            "audit-ledger",
            requireGenesis: false,
            expectedPreviousLinkHash: CreateRecordHash("record-4").HashValue);

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.HashMismatch, result.Category);
        Assert.Equal("integrity.previous-link-hash-missing", result.FailureCode);
    }

    /// <summary>
    /// Tests that a truncated chain is rejected when the caller states the tip link hash it expects.
    /// </summary>
    [Fact]
    public void VerifyRejectsTruncatedChainAgainstExpectedTip()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));
        var third = AuditIntegrityLink.Append(second, CreateRecordHash("record-3"), Now.AddSeconds(2));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
            [first, second],
            "audit-ledger",
            expectedTipLinkHash: third.LinkHash);

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.TruncatedChain, result.Category);
        Assert.Equal("integrity.chain-truncated", result.FailureCode);
    }

    /// <summary>
    /// Tests that a truncated chain is rejected when the caller states the tip sequence it expects.
    /// </summary>
    [Fact]
    public void VerifyRejectsTruncatedChainAgainstExpectedTipSequence()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
            [first, second],
            "audit-ledger",
            expectedTipSequence: 3);

        Assert.False(result.IsValid);
        Assert.Equal(AuditIntegrityVerificationCategory.TruncatedChain, result.Category);
        Assert.Equal("integrity.chain-truncated", result.FailureCode);
    }

    /// <summary>
    /// Tests that a complete chain verifying against its own tip is accepted.
    /// </summary>
    [Fact]
    public void VerifyAcceptsCompleteChainAgainstExpectedTip()
    {
        var first = AuditIntegrityLink.CreateGenesis("audit-ledger", CreateRecordHash("record-1"), Now);
        var second = AuditIntegrityLink.Append(first, CreateRecordHash("record-2"), Now.AddSeconds(1));

        AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
            [first, second],
            "audit-ledger",
            expectedTipLinkHash: second.LinkHash,
            expectedTipSequence: 2);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityLink.CreateGenesis"/> method correctly normalizes metadata keys and values, trimming whitespace and handling null values.
    /// </summary>
    [Fact]
    public void CreateGenesisNormalizesMetadata()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [" region "] = " us-la ",
            [" "] = " ignored ",
            ["nullable"] = null!
        };

        var link = AuditIntegrityLink.CreateGenesis(
            " audit-ledger ",
            CreateRecordHash("record-1"),
            Now.ToOffset(TimeSpan.FromHours(-5)),
            metadata);

        Assert.Equal("audit-ledger", link.ChainId);
        Assert.True(link.IsGenesis);
        Assert.Equal(Now, link.CreatedUtc);
        Assert.Equal("us-la", link.Metadata["region"]);
        Assert.Equal(string.Empty, link.Metadata["nullable"]);
        Assert.False(link.Metadata.ContainsKey(string.Empty));
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityLink.CreateGenesis"/> method correctly uses empty metadata when all entries normalize away (e.g., whitespace keys).
    /// </summary>
    [Fact]
    public void CreateGenesisUsesEmptyMetadataWhenEntriesNormalizeAway()
    {
        var link = AuditIntegrityLink.CreateGenesis(
            "audit-ledger",
            CreateRecordHash("record-1"),
            Now,
            new Dictionary<string, string>
            {
                [" "] = "ignored"
            });

        Assert.False(link.Metadata.ContainsKey(string.Empty));
        Assert.Empty(link.Metadata);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityLink.Rehydrate"/> method correctly normalizes the chain ID, record ID, previous link hash, and metadata, trimming whitespace and handling case sensitivity.
    /// </summary>
    [Fact]
    public void RehydrateNormalizesHashesPreviousHashAndMetadata()
    {
        CanonicalPayloadHash recordHash = CreateRecordHash("record-1");

        var link = AuditIntegrityLink.Rehydrate(
            " audit-ledger ",
            1,
            " record-1 ",
            recordHash.ArtifactType,
            recordHash.HashValue.ToUpperInvariant(),
            " PREVIOUS-HASH ",
            recordHash.HashValue.ToUpperInvariant(),
            recordHash.HashAlgorithm.ToLowerInvariant(),
            recordHash.CanonicalizationVersion,
            AsiBackboneSchemaVersions.StableArtifactsV1,
            Now,
            new Dictionary<string, string>
            {
                [" source "] = " rehydrate-test "
            });

        Assert.Equal("audit-ledger", link.ChainId);
        Assert.Equal("record-1", link.RecordId);
        Assert.Equal(recordHash.HashValue, link.RecordHash);
        Assert.Equal("previous-hash", link.PreviousLinkHash);
        Assert.False(link.IsGenesis);
        Assert.Equal("rehydrate-test", link.Metadata["source"]);
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityLink.Rehydrate"/> method throws an exception when given an invalid sequence number (e.g., zero or negative), as sequences must be positive integers.
    /// </summary>
    [Fact]
    public void RehydrateRejectsInvalidSequence()
    {
        CanonicalPayloadHash recordHash = CreateRecordHash("record-1");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuditIntegrityLink.Rehydrate(
                "audit-ledger",
                0,
                "record-1",
                recordHash.ArtifactType,
                recordHash.HashValue,
                string.Empty,
                recordHash.HashValue,
                recordHash.HashAlgorithm,
                recordHash.CanonicalizationVersion,
                AsiBackboneSchemaVersions.StableArtifactsV1,
                Now));
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityLink.Append"/> method throws an exception when given a null previous link, as appending requires a valid previous link to maintain the integrity of the chain.
    /// </summary>
    [Fact]
    public void AppendRejectsNullPreviousLink()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            AuditIntegrityLink.Append(null!, CreateRecordHash("record-2"), Now));
    }

    /// <summary>
    /// Tests that the <see cref="AuditIntegrityLink.CreateGenesis"/> method throws an exception when given a null record hash, as a genesis link must have a valid record hash to establish the integrity of the chain.
    /// </summary>
    [Fact]
    public void CreateGenesisRejectsNullRecordHash()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            AuditIntegrityLink.CreateGenesis("audit-ledger", null!, Now));
    }

    private static AuditIntegrityLink CreateComputedLink(long sequence, CanonicalPayloadHash recordHash)
    {
        var draft = AuditIntegrityLink.Rehydrate(
            "audit-ledger",
            sequence,
            recordHash.ArtifactId,
            recordHash.ArtifactType,
            recordHash.HashValue,
            string.Empty,
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

    private static string MutateHash(string hash)
    {
        return hash.StartsWith('a') ? $"b{hash[1..]}" : $"a{hash[1..]}";
    }
}
