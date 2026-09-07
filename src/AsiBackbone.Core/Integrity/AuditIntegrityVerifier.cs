using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.Integrity;

/// <summary>
/// Verifies provider-neutral append-only audit integrity chains.
/// </summary>
public static class AuditIntegrityVerifier
{
    /// <summary>
    /// Verifies that the supplied links form one continuous append-only chain in the supplied order.
    /// </summary>
    /// <param name="links">The links to verify, in chain order.</param>
    /// <param name="expectedChainId">The chain identifier every link must carry. Defaults to the first link's chain identifier.</param>
    /// <param name="requireGenesis">When <see langword="true" />, the first link must be the genesis link at sequence 1.</param>
    /// <param name="expectedPreviousLinkHash">
    /// The link hash the first supplied link must point back to. Required when <paramref name="requireGenesis" /> is
    /// <see langword="false" /> and the first supplied link is not the genesis link, because a partial chain is anchored
    /// only by the hash of the link preceding it.
    /// </param>
    /// <param name="expectedTipLinkHash">When supplied, the final link's hash must equal this value, which detects a truncated chain.</param>
    /// <param name="expectedTipSequence">When supplied, the final link's sequence must equal this value, which detects a truncated chain.</param>
    /// <remarks>
    /// Link metadata is not part of the link hash, so metadata is not authenticated by chain verification and must not be
    /// relied on as tamper-evident.
    /// </remarks>
    public static AuditIntegrityVerificationResult Verify(
        IEnumerable<AuditIntegrityLink> links,
        string? expectedChainId = null,
        bool requireGenesis = true,
        string? expectedPreviousLinkHash = null,
        string? expectedTipLinkHash = null,
        long? expectedTipSequence = null)
    {
        ArgumentNullException.ThrowIfNull(links);

        List<AuditIntegrityLink> orderedLinks = [.. links];

        if (orderedLinks.Count == 0)
        {
            return AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.EmptyChain,
                "integrity.chain-empty",
                "No integrity links were supplied.");
        }

        string chainId = string.IsNullOrWhiteSpace(expectedChainId)
            ? orderedLinks[0].ChainId
            : expectedChainId.Trim();
        HashSet<long> observedSequences = [];
        long expectedSequence = requireGenesis ? 1 : orderedLinks[0].Sequence;

        AuditIntegrityVerificationResult? anchorResult = ResolveExpectedPreviousHash(
            orderedLinks[0],
            requireGenesis,
            expectedPreviousLinkHash,
            out string expectedPreviousHash);

        if (anchorResult is not null)
        {
            return anchorResult;
        }

        foreach (AuditIntegrityLink link in orderedLinks)
        {
            AuditIntegrityVerificationResult? result = VerifyLink(
                link,
                chainId,
                expectedSequence,
                expectedPreviousHash,
                observedSequences,
                requireGenesis);

            if (result is not null)
            {
                return result;
            }

            _ = observedSequences.Add(link.Sequence);
            expectedPreviousHash = link.LinkHash;
            expectedSequence = link.Sequence + 1;
        }

        AuditIntegrityLink tip = orderedLinks[^1];

        return VerifyTip(tip, expectedTipLinkHash, expectedTipSequence)
            ?? AuditIntegrityVerificationResult.Valid(chainId, orderedLinks.Count, tip.LinkHash);
    }

    /// <summary>
    /// Determines the link hash the first supplied link must point back to.
    /// </summary>
    /// <remarks>
    /// A partial chain starting at sequence N greater than 1 points back to link N-1, whose hash is non-empty by
    /// construction. Seeding the expected previous hash with an empty string in that case both rejects genuine partial
    /// chains and accepts a forged restart whose links were rewritten to claim no predecessor, so the caller must supply
    /// the anchoring hash instead.
    /// </remarks>
    private static AuditIntegrityVerificationResult? ResolveExpectedPreviousHash(
        AuditIntegrityLink firstLink,
        bool requireGenesis,
        string? expectedPreviousLinkHash,
        out string expectedPreviousHash)
    {
        expectedPreviousHash = string.Empty;

        if (requireGenesis || firstLink.Sequence == 1)
        {
            return string.IsNullOrWhiteSpace(expectedPreviousLinkHash)
                ? null
                : AuditIntegrityVerificationResult.Failed(
                    AuditIntegrityVerificationCategory.MissingAnchor,
                    "integrity.anchor-not-applicable",
                    "An expected previous link hash cannot apply to a chain that starts at the genesis link.",
                    firstLink);
        }

        if (string.IsNullOrWhiteSpace(expectedPreviousLinkHash))
        {
            return AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.MissingAnchor,
                "integrity.expected-previous-hash-required",
                "A partial chain that does not start at the genesis link must supply the expected previous link hash.",
                firstLink);
        }

        expectedPreviousHash = expectedPreviousLinkHash.Trim();
        return null;
    }

    /// <summary>
    /// Rejects a chain that verifies internally but does not reach the tip the caller expected.
    /// </summary>
    private static AuditIntegrityVerificationResult? VerifyTip(
        AuditIntegrityLink tip,
        string? expectedTipLinkHash,
        long? expectedTipSequence)
    {
        return !string.IsNullOrWhiteSpace(expectedTipLinkHash)
            && !string.Equals(tip.LinkHash, expectedTipLinkHash.Trim(), StringComparison.Ordinal)
            ? AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.TruncatedChain,
                "integrity.chain-truncated",
                "The final link does not match the expected tip link hash.",
                tip,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["expected_tip_link_hash"] = expectedTipLinkHash.Trim(),
                    ["actual_tip_link_hash"] = tip.LinkHash
                })
            : expectedTipSequence.HasValue && tip.Sequence != expectedTipSequence.Value
            ? AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.TruncatedChain,
                "integrity.chain-truncated",
                "The final link does not match the expected tip sequence.",
                tip,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["expected_tip_sequence"] = expectedTipSequence.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["actual_tip_sequence"] = tip.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
                })
            : null;
    }

    private static AuditIntegrityVerificationResult? VerifyLink(
        AuditIntegrityLink link,
        string expectedChainId,
        long expectedSequence,
        string expectedPreviousHash,
        HashSet<long> observedSequences,
        bool requireGenesis)
    {
        if (!string.Equals(link.HashAlgorithm, CanonicalPayloadOptions.DefaultHashAlgorithm, StringComparison.Ordinal))
        {
            return AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.UnsupportedAlgorithm,
                "integrity.hash-algorithm-unsupported",
                "The integrity link uses an unsupported hash algorithm.",
                link);
        }

        if (!string.Equals(link.ChainId, expectedChainId, StringComparison.Ordinal))
        {
            return AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.WrongChain,
                "integrity.chain-id-mismatch",
                "The integrity link belongs to a different chain.",
                link);
        }

        if (observedSequences.Contains(link.Sequence))
        {
            return AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.ForkedChain,
                "integrity.sequence-duplicate",
                "Multiple links claim the same chain sequence.",
                link);
        }

        if (link.Sequence != expectedSequence)
        {
            AuditIntegrityVerificationCategory category = link.Sequence > expectedSequence
                ? AuditIntegrityVerificationCategory.MissingRecord
                : AuditIntegrityVerificationCategory.ReorderedRecord;

            return AuditIntegrityVerificationResult.Failed(
                category,
                category is AuditIntegrityVerificationCategory.MissingRecord
                    ? "integrity.sequence-missing"
                    : "integrity.sequence-reordered",
                "The integrity link sequence is not continuous in the supplied order.",
                link,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["expected_sequence"] = expectedSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["actual_sequence"] = link.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
        }

        if (requireGenesis && link.Sequence == 1 && link.PreviousLinkHash.Length != 0)
        {
            return AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.HashMismatch,
                "integrity.genesis-previous-hash-present",
                "The genesis link must not point to a previous link hash.",
                link);
        }

        if (link.Sequence > 1 && link.PreviousLinkHash.Length == 0)
        {
            return AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.HashMismatch,
                "integrity.previous-link-hash-missing",
                "A link after the genesis link must point to a previous link hash.",
                link);
        }

        if (!string.Equals(link.PreviousLinkHash, expectedPreviousHash, StringComparison.Ordinal))
        {
            return AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.HashMismatch,
                "integrity.previous-link-hash-mismatch",
                "The integrity link does not point to the previous link hash.",
                link,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["expected_previous_hash"] = expectedPreviousHash,
                    ["actual_previous_hash"] = link.PreviousLinkHash
                });
        }

        string expectedLinkHash = link.ComputeExpectedLinkHash();
        return !string.Equals(link.LinkHash, expectedLinkHash, StringComparison.Ordinal)
            ? AuditIntegrityVerificationResult.Failed(
                AuditIntegrityVerificationCategory.ModifiedRecord,
                "integrity.link-hash-mismatch",
                "The integrity link hash no longer matches its canonical fields.",
                link,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["expected_link_hash"] = expectedLinkHash,
                    ["actual_link_hash"] = link.LinkHash
                })
            : null;
    }
}
