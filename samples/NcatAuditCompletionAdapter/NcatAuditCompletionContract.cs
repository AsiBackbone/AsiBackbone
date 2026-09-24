using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AsiBackbone.Samples.NcatAuditCompletionAdapter;

/// <summary>
/// Applies version 1 of NCAT's published audit-completion contract to a received completion message.
/// </summary>
/// <remarks>
/// The rules are modeled independently from NCAT and are checked against NCAT's pinned contract vectors in
/// the sample tests. Validation rejects anything the contract does not define instead of guessing a mapping.
/// </remarks>
public static class NcatAuditCompletionContract
{
    /// <summary>Gets the major contract version this adapter understands.</summary>
    public const int SupportedContractMajorVersion = 1;

    /// <summary>Gets the supported completion message schema version.</summary>
    public const string MessageSchemaVersion = "1.0";

    /// <summary>Gets the supported canonical mutation manifest schema version.</summary>
    public const string ManifestSchemaVersion = "1.0";

    /// <summary>Gets the supported canonical mutation manifest digest algorithm.</summary>
    public const string ManifestAlgorithm = "SHA-256";

    /// <summary>Gets the only persistence outcome carried by an NCAT completion message.</summary>
    public const string CommittedOutcome = "Committed";

    /// <summary>Gets the prefix of every NCAT completion idempotency key.</summary>
    public const string IdempotencyKeyPrefix = "ncat-audit-completion:";

    /// <summary>Gets the maximum destination length accepted by NCAT staging.</summary>
    public const int MaximumDestinationLength = 128;

    private const int Sha256HexLength = 64;

    /// <summary>
    /// Derives the idempotency key NCAT assigns to a destination and mutation batch.
    /// </summary>
    /// <remarks>
    /// The key hashes the trimmed destination and the trimmed mutation batch identifier. The message itself keeps
    /// the batch identifier exactly as NCAT recorded it.
    /// </remarks>
    public static string ComputeIdempotencyKey(string destination, string mutationBatchId)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(mutationBatchId);

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(destination.Trim() + "\n" + mutationBatchId.Trim()));
        return IdempotencyKeyPrefix + Convert.ToHexString(digest);
    }

    /// <summary>
    /// Validates a completion message against the contract and translates it into an adapter handoff.
    /// </summary>
    /// <returns><see langword="true" /> when the message satisfies the contract; otherwise <see langword="false" />.</returns>
    public static bool TryCreateHandoff(
        NcatAuditCompletionMessage message,
        int deliveryAttempt,
        [NotNullWhen(true)] out NcatAuditCompletionHandoff? handoff,
        [NotNullWhen(false)] out string? reasonCode)
    {
        ArgumentNullException.ThrowIfNull(message);
        handoff = null;

        reasonCode = Validate(message);
        if (reasonCode is not null)
        {
            return false;
        }

        handoff = new NcatAuditCompletionHandoff(
            CompletionEntryId: message.IdempotencyKey,
            PersistenceOutcome: message.PersistenceOutcome,
            CompletedUtc: message.ReceiptCompletedUtc.ToUniversalTime(),
            OperationExecutionId: message.OperationExecutionId,
            ExecutionAttemptId: message.ExecutionAttemptId,
            DecisionAuditRecordId: message.DecisionAuditRecordId,
            CorrelationId: message.CorrelationId,
            TraceId: message.TraceId,
            MutationBatchId: message.MutationBatchId,
            AuditRecordCount: message.AuditRecordCount,
            MutationManifestHash: message.MutationManifestHash,
            MutationManifestAlgorithm: message.MutationManifestAlgorithm,
            DeliveryAttempt: deliveryAttempt);
        return true;
    }

    /// <summary>
    /// Verifies a message against retained canonical manifest bytes, such as an archive's copy of the manifest.
    /// </summary>
    /// <param name="message">The completion message to verify.</param>
    /// <param name="canonicalManifestUtf8">The exact canonical manifest text, encoded as UTF-8 without a byte order mark.</param>
    /// <param name="reasonCode">The rejection reason when verification fails.</param>
    /// <returns><see langword="true" /> when the manifest corresponds to the message; otherwise <see langword="false" />.</returns>
    public static bool TryVerifyCanonicalManifest(
        NcatAuditCompletionMessage message,
        ReadOnlySpan<byte> canonicalManifestUtf8,
        [NotNullWhen(false)] out string? reasonCode)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!string.Equals(message.MutationManifestAlgorithm, ManifestAlgorithm, StringComparison.Ordinal))
        {
            reasonCode = "unsupported-manifest-algorithm";
            return false;
        }

        if (!TryReadManifestHeader(canonicalManifestUtf8, out ManifestHeader? header))
        {
            reasonCode = "manifest-malformed";
            return false;
        }

        reasonCode =
            !string.Equals(header.SchemaVersion, message.MutationManifestSchemaVersion, StringComparison.Ordinal)
                ? "manifest-schema-version-mismatch"
            : !string.Equals(header.MutationBatchId, message.MutationBatchId, StringComparison.Ordinal)
                ? "manifest-batch-id-mismatch"
            : header.AuditRecordCount != message.AuditRecordCount
                ? "manifest-record-count-mismatch"
            : !DigestMatches(message.MutationManifestHash, SHA256.HashData(canonicalManifestUtf8))
                ? "manifest-digest-mismatch"
            : null;

        return reasonCode is null;
    }

    private static string? Validate(NcatAuditCompletionMessage message)
    {
        return !string.Equals(message.SchemaVersion, MessageSchemaVersion, StringComparison.Ordinal)
                ? "unsupported-message-schema-version"
            : !string.Equals(message.MutationManifestSchemaVersion, ManifestSchemaVersion, StringComparison.Ordinal)
                ? "unsupported-manifest-schema-version"
            : !string.Equals(message.MutationManifestAlgorithm, ManifestAlgorithm, StringComparison.Ordinal)
                ? "unsupported-manifest-algorithm"
            : !string.Equals(message.PersistenceOutcome, CommittedOutcome, StringComparison.Ordinal)
                ? "unsupported-persistence-outcome"
            : !IsSha256Hex(message.MutationManifestHash)
                ? "invalid-manifest-hash"
            : string.IsNullOrWhiteSpace(message.MutationBatchId)
                ? "mutation-batch-id-required"
            : message.AuditRecordCount < 1
                ? "audit-record-count-invalid"
            : !IsValidDestination(message.Destination)
                ? "invalid-destination"
            : HasWhitespaceOnlyIdentifier(message)
                ? "malformed-optional-identifier"
            : !string.Equals(
                message.IdempotencyKey,
                ComputeIdempotencyKey(message.Destination, message.MutationBatchId),
                StringComparison.Ordinal)
                ? "idempotency-key-mismatch"
            : null;
    }

    private static bool IsSha256Hex(string? value)
    {
        return value is { Length: Sha256HexLength } && value.All(char.IsAsciiHexDigit);
    }

    private static bool IsValidDestination(string? destination)
    {
        return !string.IsNullOrWhiteSpace(destination) &&
            destination.Length <= MaximumDestinationLength &&
            string.Equals(destination, destination.Trim(), StringComparison.Ordinal);
    }

    private static bool HasWhitespaceOnlyIdentifier(NcatAuditCompletionMessage message)
    {
        string?[] identifiers =
        [
            message.OperationExecutionId,
            message.ExecutionAttemptId,
            message.DecisionAuditRecordId,
            message.CorrelationId,
            message.TraceId
        ];

        return identifiers.Any(identifier => identifier is not null && string.IsNullOrWhiteSpace(identifier));
    }

    private static bool DigestMatches(string expectedHex, byte[] actualDigest)
    {
        if (!IsSha256Hex(expectedHex))
        {
            return false;
        }

        byte[] expected = Convert.FromHexString(expectedHex);
        return CryptographicOperations.FixedTimeEquals(expected, actualDigest);
    }

    private static bool TryReadManifestHeader(
        ReadOnlySpan<byte> canonicalManifestUtf8,
        [NotNullWhen(true)] out ManifestHeader? header)
    {
        header = null;

        try
        {
            Utf8JsonReader reader = new(canonicalManifestUtf8);
            using var document = JsonDocument.ParseValue(ref reader);

            // A canonical manifest is exactly one JSON value. Trailing whitespace is skipped by the reader;
            // any further token means the digest covers content other than the manifest.
            if (reader.Read())
            {
                return false;
            }

            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out JsonElement schemaVersion) ||
                schemaVersion.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("mutationBatchId", out JsonElement mutationBatchId) ||
                mutationBatchId.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("auditRecordCount", out JsonElement auditRecordCount) ||
                !auditRecordCount.TryGetInt32(out int count))
            {
                return false;
            }

            header = new ManifestHeader(schemaVersion.GetString()!, mutationBatchId.GetString()!, count);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record ManifestHeader(string SchemaVersion, string MutationBatchId, int AuditRecordCount);
}
