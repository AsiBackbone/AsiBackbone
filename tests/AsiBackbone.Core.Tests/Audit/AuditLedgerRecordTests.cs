using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Unit tests for <see cref="AuditLedgerRecord"/> focusing on the behavior of the <see cref="AuditLedgerRecord.FromResidue"/> factory method, including field copying, normalization, metadata handling, and error conditions.
/// </summary>
public sealed class AuditLedgerRecordTests
{
    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> correctly copies all relevant fields from an <see cref="IDecisionReceipt"/> instance, including normalization of string fields and proper handling of timestamps and metadata. This test ensures that the resulting <see cref="AuditLedgerRecord"/> accurately reflects the information contained in the source residue while also applying any necessary transformations or defaults for missing optional fields.
    /// </summary>
    [Fact]
    public void FromResidueCopiesAuditResidueFieldsAndLedgerReferences()
    {
        var actor = GovernanceActorContext.Human(" user-123 ", " Chris ");
        DateTimeOffset occurredUtc = new(2026, 6, 4, 7, 0, 0, TimeSpan.FromHours(-5));
        DateTimeOffset recordedUtc = new(2026, 6, 4, 8, 0, 0, TimeSpan.FromHours(-5));

        var residue = DecisionReceipt.Create(
            actor,
            " document.approve ",
            " Allowed ",
            reasonCodes: [" policy.allowed "],
            eventId: " event-123 ",
            occurredUtc: occurredUtc,
            correlationId: " correlation-123 ",
            traceId: " trace-456 ",
            policyVersion: " v1 ",
            policyHash: " hash-abc ",
            metadata: new Dictionary<string, string>
            {
                [" source "] = " residue "
            });

        var record = AuditLedgerRecord.FromResidue(
            residue,
            recordId: " record-123 ",
            recordedUtc: recordedUtc,
            handshakeId: " handshake-123 ",
            acknowledgmentId: " ack-123 ",
            capabilityTokenId: " token-123 ",
            previousRecordHash: " previous-hash ",
            recordHash: " record-hash ",
            signatureKeyId: " key-1 ",
            signatureAlgorithm: " HMACSHA256 ",
            signatureValue: " signature-value ");

        Assert.Equal("record-123", record.RecordId);
        Assert.Equal("event-123", record.EventId);
        Assert.Equal(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero), record.OccurredUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 4, 13, 0, 0, TimeSpan.Zero), record.RecordedUtc);
        Assert.Equal("user-123", record.ActorId);
        Assert.Equal(GovernanceActorType.Human, record.ActorType);
        Assert.Equal("Chris", record.ActorDisplayName);
        Assert.Equal("document.approve", record.OperationName);
        Assert.Equal("Allowed", record.Outcome);
        Assert.Equal("policy.allowed", Assert.Single(record.ReasonCodes));
        Assert.True(record.HasReasonCodes);
        Assert.Equal("correlation-123", record.CorrelationId);
        Assert.Equal("trace-456", record.TraceId);
        Assert.Equal("v1", record.PolicyVersion);
        Assert.Equal("hash-abc", record.PolicyHash);
        Assert.Equal("handshake-123", record.HandshakeId);
        Assert.Equal("ack-123", record.AcknowledgmentId);
        Assert.Equal("token-123", record.CapabilityTokenId);
        Assert.Equal("previous-hash", record.PreviousRecordHash);
        Assert.Equal("record-hash", record.RecordHash);
        Assert.Equal("key-1", record.SignatureKeyId);
        Assert.Equal("HMACSHA256", record.SignatureAlgorithm);
        Assert.Equal("signature-value", record.SignatureValue);
        Assert.True(record.HasMetadata);
        Assert.Equal("residue", record.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that the object returned by <see cref="AuditLedgerRecord.FromResidue"/> implements the <see cref="IDecisionReceipt"/> interface, ensuring that it can be used interchangeably with other audit residue implementations in contexts where the interface is expected. This test confirms that the factory method produces an object that adheres to the required contract for audit residues, allowing it to be seamlessly integrated into existing systems and workflows that rely on the <see cref="IDecisionReceipt"/> abstraction.
    /// </summary>
    [Fact]
    public void FromResidueImplementsAuditResidueContract()
    {
        var record = AuditLedgerRecord.FromResidue(CreateValidResidue());

        _ = Assert.IsType<IDecisionReceipt>(record, exactMatch: false);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> generates a valid <see cref="AuditLedgerRecord.RecordId"/> when the optional <c>recordId</c> parameter is not provided. The generated record ID should be a non-empty, 32-character string that does not contain hyphens, ensuring it meets typical requirements for unique identifiers in audit logging contexts. This test confirms that the factory method correctly handles cases where a record ID is not supplied, providing a suitable default value to maintain the integrity and traceability of audit records.
    /// </summary>
    [Fact]
    public void FromResidueGeneratesRecordIdWhenMissing()
    {
        var record = AuditLedgerRecord.FromResidue(CreateValidResidue());

        Assert.False(string.IsNullOrWhiteSpace(record.RecordId));
        Assert.Equal(32, record.RecordId.Length);
        Assert.DoesNotContain("-", record.RecordId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> generates a valid <see cref="AuditLedgerRecord.RecordId"/> when the optional <c>recordId</c> parameter is provided but consists only of whitespace. The generated record ID should be a non-empty, 32-character string that does not contain hyphens, ensuring it meets typical requirements for unique identifiers in audit logging contexts. This test confirms that the factory method correctly handles cases where a record ID is supplied but is not valid, providing a suitable default value to maintain the integrity and traceability of audit records.
    /// </summary>
    [Fact]
    public void FromResidueGeneratesRecordIdWhenWhitespace()
    {
        var record = AuditLedgerRecord.FromResidue(
            CreateValidResidue(),
            recordId: "   ");

        Assert.False(string.IsNullOrWhiteSpace(record.RecordId));
        Assert.Equal(32, record.RecordId.Length);
        Assert.DoesNotContain("-", record.RecordId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> uses the current UTC timestamp for the <see cref="AuditLedgerRecord.RecordedUtc"/> property when the optional <c>recordedUtc</c> parameter is not provided. The test captures the time immediately before and after the factory method call to ensure that the recorded timestamp falls within this range, confirming that it reflects the actual time of record creation. This test ensures that audit records have accurate timestamps even when a specific recorded time is not supplied, which is critical for maintaining the integrity and reliability of audit logs.
    /// </summary>
    [Fact]
    public void FromResidueUsesCurrentUtcRecordedTimestampWhenMissing()
    {
        DateTimeOffset beforeCreate = DateTimeOffset.UtcNow;

        var record = AuditLedgerRecord.FromResidue(CreateValidResidue());

        DateTimeOffset afterCreate = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, record.RecordedUtc.Offset);
        Assert.InRange(record.RecordedUtc, beforeCreate, afterCreate);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> normalizes optional ledger fields to null when they are provided as whitespace or empty strings. This test ensures that fields such as <see cref="AuditLedgerRecord.HandshakeId"/>, <see cref="AuditLedgerRecord.AcknowledgmentId"/>, <see cref="AuditLedgerRecord.CapabilityTokenId"/>, <see cref="AuditLedgerRecord.PreviousRecordHash"/>, <see cref="AuditLedgerRecord.RecordHash"/>, <see cref="AuditLedgerRecord.SignatureKeyId"/>, <see cref="AuditLedgerRecord.SignatureAlgorithm"/>, and <see cref="AuditLedgerRecord.SignatureValue"/> are correctly set to null when the input values are not meaningful, maintaining consistency in how optional fields are represented in the resulting audit ledger record.
    /// </summary>
    [Fact]
    public void FromResidueNormalizesOptionalLedgerFieldsToNull()
    {
        var record = AuditLedgerRecord.FromResidue(
            CreateValidResidue(),
            handshakeId: " ",
            acknowledgmentId: "",
            capabilityTokenId: "\t",
            previousRecordHash: " ",
            recordHash: "",
            signatureKeyId: "\t",
            signatureAlgorithm: " ",
            signatureValue: "");

        Assert.Null(record.HandshakeId);
        Assert.Null(record.AcknowledgmentId);
        Assert.Null(record.CapabilityTokenId);
        Assert.Null(record.PreviousRecordHash);
        Assert.Null(record.RecordHash);
        Assert.Null(record.SignatureKeyId);
        Assert.Null(record.SignatureAlgorithm);
        Assert.Null(record.SignatureValue);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> normalizes reason codes by trimming whitespace and filtering out null or blank entries. This test ensures that the resulting <see cref="AuditLedgerRecord.ReasonCodes"/> collection contains only meaningful reason codes without leading or trailing whitespace, and that the <see cref="AuditLedgerRecord.HasReasonCodes"/> property accurately reflects whether any valid reason codes are present. This normalization is important for maintaining the clarity and usefulness of reason codes in audit records, allowing them to be reliably used for filtering, analysis, and reporting purposes.
    /// </summary>
    [Fact]
    public void FromResidueFiltersNullAndBlankReasonCodesFromCustomResidue()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.ReasonCodes = [" risk.high ", null!, "", "   ", " policy.warning "];

        var record = AuditLedgerRecord.FromResidue(residue);

        Assert.True(record.HasReasonCodes);
        Assert.Equal(["risk.high", "policy.warning"], record.ReasonCodes);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> correctly handles cases where the source <see cref="IDecisionReceipt"/> has a null or empty collection of reason codes. The resulting <see cref="AuditLedgerRecord"/> should have an empty collection for <see cref="AuditLedgerRecord.ReasonCodes"/> and the <see cref="AuditLedgerRecord.HasReasonCodes"/> property should return false, indicating that there are no valid reason codes associated with the record. This test ensures that the factory method gracefully handles cases where reason codes are not provided, without throwing exceptions or producing invalid state in the resulting audit ledger record.
    /// </summary>
    [Fact]
    public void FromResidueWithNoReasonCodesHasNoReasonCodes()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.ReasonCodes = [];

        var record = AuditLedgerRecord.FromResidue(residue);

        Assert.False(record.HasReasonCodes);
        Assert.Empty(record.ReasonCodes);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> correctly merges metadata from both the source <see cref="IDecisionReceipt"/> and the optional metadata parameter, with the ledger metadata taking precedence in cases of duplicate keys. This test ensures that the resulting <see cref="AuditLedgerRecord.Metadata"/> contains a combined set of key-value pairs from both sources, with any keys present in both collections being overridden by the values from the ledger metadata. This behavior is important for allowing callers to provide additional context or override existing metadata when creating audit ledger records, while still preserving relevant information from the original residue.
    /// </summary>
    [Fact]
    public void FromResidueMergesResidueAndLedgerMetadataAndLedgerOverridesDuplicateKeys()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.Metadata = new Dictionary<string, string>
        {
            [" source "] = " residue ",
            [" duplicate "] = " residue-value "
        };

        var record = AuditLedgerRecord.FromResidue(
            residue,
            metadata: new Dictionary<string, string>
            {
                [" ledger "] = " record ",
                [" duplicate "] = " ledger-value "
            });

        Assert.True(record.HasMetadata);
        Assert.Equal("residue", record.Metadata["source"]);
        Assert.Equal("record", record.Metadata["ledger"]);
        Assert.Equal("ledger-value", record.Metadata["duplicate"]);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> ignores metadata entries with blank keys and treats null metadata values as empty strings. This test ensures that any metadata entries in either the source residue or the optional ledger metadata that have keys consisting solely of whitespace are not included in the resulting <see cref="AuditLedgerRecord.Metadata"/>, and that any values that are null are normalized to empty strings. This behavior is important for maintaining the integrity and usability of metadata in audit records, preventing invalid or meaningless entries from being included while still preserving the presence of keys with null values in a consistent manner.
    /// </summary>
    [Fact]
    public void FromResidueIgnoresBlankMetadataKeysAndStoresNullMetadataValuesAsEmptyStrings()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.Metadata = new Dictionary<string, string>
        {
            [" "] = "ignored",
            ["\t"] = "also ignored",
            [" source "] = null!
        };

        var record = AuditLedgerRecord.FromResidue(
            residue,
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored",
                [" ledger "] = null!
            });

        Assert.True(record.HasMetadata);
        Assert.Equal(string.Empty, record.Metadata["source"]);
        Assert.Equal(string.Empty, record.Metadata["ledger"]);
        Assert.False(record.Metadata.ContainsKey(" "));
        Assert.False(record.Metadata.ContainsKey("\t"));
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> correctly handles cases where the source <see cref="IDecisionReceipt"/> has a null or empty metadata collection. The resulting <see cref="AuditLedgerRecord"/> should have an empty collection for <see cref="AuditLedgerRecord.Metadata"/> and the <see cref="AuditLedgerRecord.HasMetadata"/> property should return false, indicating that there is no metadata associated with the record. This test ensures that the factory method gracefully handles cases where metadata is not provided in the source residue, without throwing exceptions or producing invalid state in the resulting audit ledger record.
    /// </summary>
    [Fact]
    public void FromResidueWithNullMetadataSourcesHasNoMetadata()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.Metadata = null!;

        var record = AuditLedgerRecord.FromResidue(residue);

        Assert.False(record.HasMetadata);
        Assert.Empty(record.Metadata);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> correctly handles cases where the source <see cref="IDecisionReceipt"/> has an empty metadata collection. The resulting <see cref="AuditLedgerRecord"/> should have an empty collection for <see cref="AuditLedgerRecord.Metadata"/> and the <see cref="AuditLedgerRecord.HasMetadata"/> property should return false, indicating that there is no metadata associated with the record. This test ensures that the factory method gracefully handles cases where metadata is provided as an empty collection in the source residue, without throwing exceptions or producing invalid state in the resulting audit ledger record.
    /// </summary>
    [Fact]
    public void FromResidueWithEmptyMetadataSourcesHasNoMetadata()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.Metadata = new Dictionary<string, string>();

        var record = AuditLedgerRecord.FromResidue(
            residue,
            metadata: new Dictionary<string, string>());

        Assert.False(record.HasMetadata);
        Assert.Empty(record.Metadata);
    }

    /// <summary>
    /// Verifies that the collections for reason codes and metadata in the object returned by <see cref="AuditLedgerRecord.FromResidue"/> do not alias the source collections from the input <see cref="IDecisionReceipt"/> or the optional metadata parameter. Modifying the original collections after creating the audit ledger record should not affect the contents of the record's reason codes or metadata, confirming that defensive copying is performed to maintain immutability and prevent unintended side effects. This test ensures that the integrity of the audit ledger record is preserved regardless of changes to the source collections after its creation.
    /// </summary>
    [Fact]
    public void FromResidueDoesNotAliasSourceCollections()
    {
        List<string> reasonCodes = [" policy.warning "];

        Dictionary<string, string> residueMetadata = new(StringComparer.Ordinal)
        {
            [" source "] = " residue "
        };

        Dictionary<string, string> ledgerMetadata = new(StringComparer.Ordinal)
        {
            [" ledger "] = " record "
        };

        TestAuditResidue residue = CreateValidResidue();
        residue.ReasonCodes = reasonCodes;
        residue.Metadata = residueMetadata;

        var record = AuditLedgerRecord.FromResidue(
            residue,
            metadata: ledgerMetadata);

        reasonCodes.Add("policy.added");
        residueMetadata[" source "] = " mutated ";
        residueMetadata[" other "] = " added ";
        ledgerMetadata[" ledger "] = " mutated ";
        ledgerMetadata[" extra "] = " added ";

        Assert.Equal("policy.warning", Assert.Single(record.ReasonCodes));
        Assert.Equal(2, record.Metadata.Count);
        Assert.Equal("residue", record.Metadata["source"]);
        Assert.Equal("record", record.Metadata["ledger"]);
        Assert.False(record.Metadata.ContainsKey("other"));
        Assert.False(record.Metadata.ContainsKey("extra"));
    }

    /// <summary>
    /// Verifies that the metadata collection in the object returned by <see cref="AuditLedgerRecord.FromResidue"/> cannot be mutated through casts to mutable dictionary types. Attempting to cast the <see cref="AuditLedgerRecord.Metadata"/> property to either a generic <see cref="IDictionary{TKey, TValue}"/> or a non-generic <c>IDictionary</c> and modify it should result in an exception, confirming that the metadata is exposed as a read-only collection and cannot be altered after the audit ledger record has been created. This test ensures that the immutability of the metadata is enforced at runtime, preventing any modifications that could compromise the integrity of the audit record.
    /// </summary>
    [Fact]
    public void MetadataCannotBeMutatedThroughDictionaryCasts()
    {
        var record = AuditLedgerRecord.FromResidue(
            CreateValidResidue(),
            metadata: new Dictionary<string, string>
            {
                [" source "] = " unit-test "
            });

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(record.Metadata);

        Assert.True(record.HasMetadata);
        Assert.Equal("unit-test", record.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that the metadata collection in the object returned by <see cref="AuditLedgerRecord.FromResidue"/> cannot be mutated through casts to mutable dictionary types even when the source <see cref="IDecisionReceipt"/> has an empty metadata collection. Attempting to cast the <see cref="AuditLedgerRecord.Metadata"/> property to either a generic <see cref="IDictionary{TKey, TValue}"/> or a non-generic <c>IDictionary</c> and modify it should result in an exception, confirming that the metadata is exposed as a read-only collection and cannot be altered after the audit ledger record has been created, regardless of the initial state of the source metadata. This test ensures that the immutability of the metadata is consistently enforced at runtime, preventing any modifications that could compromise the integrity of the audit record even when starting with no metadata.
    /// </summary>
    [Fact]
    public void EmptyMetadataCannotBeMutatedThroughDictionaryCasts()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.Metadata = new Dictionary<string, string>();

        var record = AuditLedgerRecord.FromResidue(residue);

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(record.Metadata);

        Assert.False(record.HasMetadata);
        Assert.Empty(record.Metadata);
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> throws an <see cref="ArgumentNullException"/> when the input <see cref="IDecisionReceipt"/> parameter is null. This test ensures that the factory method enforces the requirement for a valid audit residue to create an audit ledger record, and that it provides a clear and specific exception when this precondition is not met. Proper handling of null inputs is critical for preventing unexpected errors and maintaining the robustness of the audit logging system.
    /// </summary>
    [Fact]
    public void FromResidueThrowsForMissingResidue()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            AuditLedgerRecord.FromResidue(null!));
    }

    /// <summary>
    /// Verifies that <see cref="AuditLedgerRecord.FromResidue"/> throws an appropriate exception when the input <see cref="IDecisionReceipt"/> contains invalid values for required fields such as <see cref="IDecisionReceipt.EventId"/>, <see cref="IDecisionReceipt.ActorId"/>, <see cref="IDecisionReceipt.OperationName"/>, or <see cref="IDecisionReceipt.Outcome"/>. The test iterates through each of these fields, setting them to invalid values (e.g., null, empty, or whitespace) and asserting that the factory method throws an exception, confirming that it properly validates the input residue and enforces the presence of essential information needed to create a valid audit ledger record. This validation is crucial for ensuring the integrity and usefulness of audit records generated from residues.
    /// </summary>
    /// <param name="fieldName">
    /// The name of the required field in the <see cref="IDecisionReceipt"/> to be set to an invalid value for testing. This parameter is used to identify which field is being tested for validation, allowing the test to systematically verify that each required field is properly checked by the <see cref="AuditLedgerRecord.FromResidue"/> method. The test will cover fields such as "EventId", "ActorId", "OperationName", and "Outcome", which are critical for the creation of a valid audit ledger record.
    /// </param>
    [Theory]
    [InlineData("EventId")]
    [InlineData("ActorId")]
    [InlineData("OperationName")]
    [InlineData("Outcome")]
    public void FromResidueThrowsForInvalidRequiredResidueFields(string fieldName)
    {
        TestAuditResidue residue = CreateValidResidue();

        switch (fieldName)
        {
            case "EventId":
                residue.EventId = " ";
                break;

            case "ActorId":
                residue.ActorId = "";
                break;

            case "OperationName":
                residue.OperationName = "\t";
                break;

            case "Outcome":
                residue.Outcome = null!;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unknown test field.");
        }

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            AuditLedgerRecord.FromResidue(residue));
    }

    /// <summary>
    /// Verifies that null reason-code collections are normalized to an empty read-only collection.
    /// </summary>
    [Fact]
    public void FromResidueWithNullReasonCodesHasNoReasonCodes()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.ReasonCodes = null!;

        var record = AuditLedgerRecord.FromResidue(residue);

        Assert.False(record.HasReasonCodes);
        Assert.Empty(record.ReasonCodes);
    }

    /// <summary>
    /// Verifies that ledger-supplied metadata is retained when residue metadata is null.
    /// </summary>
    [Fact]
    public void FromResidueUsesLedgerMetadataWhenResidueMetadataIsNull()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.Metadata = null!;

        var record = AuditLedgerRecord.FromResidue(
            residue,
            metadata: new Dictionary<string, string>
            {
                [" ledger-key "] = " ledger-value "
            });

        Assert.True(record.HasMetadata);
        _ = Assert.Single(record.Metadata);
        Assert.Equal("ledger-value", record.Metadata["ledger-key"]);
    }

    /// <summary>
    /// Verifies that optional residue fields normalize whitespace to null.
    /// </summary>
    [Fact]
    public void FromResidueNormalizesOptionalResidueFieldsToNull()
    {
        TestAuditResidue residue = CreateValidResidue();
        residue.ActorDisplayName = " ";
        residue.CorrelationId = "";
        residue.TraceId = "\t";
        residue.PolicyVersion = " ";
        residue.PolicyHash = "";

        var record = AuditLedgerRecord.FromResidue(residue);

        Assert.Null(record.ActorDisplayName);
        Assert.Null(record.CorrelationId);
        Assert.Null(record.TraceId);
        Assert.Null(record.PolicyVersion);
        Assert.Null(record.PolicyHash);
    }

    private static TestAuditResidue CreateValidResidue()
    {
        return new TestAuditResidue
        {
            EventId = "event-123",
            OccurredUtc = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
            ActorId = "actor-123",
            ActorType = GovernanceActorType.System,
            ActorDisplayName = "System",
            OperationName = "system.sync",
            Outcome = "Allowed",
            ReasonCodes = ["policy.allowed"],
            CorrelationId = "correlation-123",
            TraceId = "trace-456",
            PolicyVersion = "v1",
            PolicyHash = "hash-abc",
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "test"
            }
        };
    }

    private sealed class TestAuditResidue : IDecisionReceipt
    {
        public string EventId { get; set; } = "event-123";

        public DateTimeOffset OccurredUtc { get; set; } =
            new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

        public string ActorId { get; set; } = "actor-123";

        public GovernanceActorType ActorType { get; set; } = GovernanceActorType.System;

        public string? ActorDisplayName { get; set; } = "System";

        public string OperationName { get; set; } = "system.sync";

        public string Outcome { get; set; } = "Allowed";

        public IReadOnlyList<string> ReasonCodes { get; set; } = ["policy.allowed"];

        public string? CorrelationId { get; set; } = "correlation-123";

        public string? TraceId { get; set; } = "trace-456";

        public string? PolicyVersion { get; set; } = "v1";

        public string? PolicyHash { get; set; } = "hash-abc";

        public IReadOnlyDictionary<string, string> Metadata { get; set; } =
            new Dictionary<string, string>
            {
                ["source"] = "test"
            };
    }
}
