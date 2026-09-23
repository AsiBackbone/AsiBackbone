using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.HostIntegration;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Locks the <c>asibackbone.canonical-json.v1</c> bytes and metadata-normalization equivalences documented in
/// <c>docs/articles/canonical-json-v1.md</c> for implementers outside .NET. Every expected value in this class is
/// published in that specification; a change to any of them requires a new canonicalization version rather than an
/// update to these tests.
/// </summary>
public sealed class CanonicalJsonV1InteroperabilityTests
{
    private const string EscapingVectorJson = /*lang=json,strict*/ "{\"artifactId\":\"artifact-2\",\"artifactType\":\"artifact-type\",\"canonicalizationVersion\":\"asibackbone.canonical-json.v1\",\"content\":{\"big\":9007199254740993,\"count\":-7,\"flag\":false,\"items\":[\"b\",\"a\",null],\"text\":\"tab\\tline\\nquote\\u0022slash\\\\ctl\\u0001emoji\\uD83D\\uDE00\"},\"payloadSchemaVersion\":\"schema-v1\"}";

    private const string EscapingVectorUtf8Hex = "7B2261727469666163744964223A2261727469666163742D32222C22617274696661637454797065223A2261727469666163742D74797065222C2263616E6F6E6963616C697A6174696F6E56657273696F6E223A226173696261636B626F6E652E63616E6F6E6963616C2D6A736F6E2E7631222C22636F6E74656E74223A7B22626967223A393030373139393235343734303939332C22636F756E74223A2D372C22666C6167223A66616C73652C226974656D73223A5B2262222C2261222C6E756C6C5D2C2274657874223A227461625C746C696E655C6E71756F74655C7530303232736C6173685C5C63746C5C7530303031656D6F6A695C75443833445C7544453030227D2C227061796C6F6164536368656D6156657273696F6E223A22736368656D612D7631227D";

    private const string EscapingVectorSha256 = "33affa1ac1fd0c5571054c4fdb280611afcc15902fe66b8b37ab4ae1bb032b13";

    private const string DecisionReceiptVectorJson = /*lang=json,strict*/ "{\"artifactId\":\"receipt-1\",\"artifactType\":\"asibackbone.audit-residue\",\"canonicalizationVersion\":\"asibackbone.canonical-json.v1\",\"content\":{\"actorDisplayName\":null,\"actorId\":\"actor-1\",\"actorType\":\"Human\",\"auditResidueId\":\"receipt-1\",\"constraintCount\":null,\"constraintSetHash\":null,\"correlationId\":\"correlation-1\",\"decisionLatencyMs\":42,\"decisionStage\":null,\"emitterProvider\":null,\"emitterStatus\":null,\"eventId\":\"receipt-1\",\"gatewayExecutionId\":null,\"metadata\":{\"blank\":\"\",\"note\":\"two  words\",\"region\":\"us-east\",\"tier\":\"\"},\"occurredUtc\":\"2026-09-22T12:34:56.1234567Z\",\"operationName\":\"orders.approve\",\"organizationHash\":null,\"outboxSequence\":null,\"outcome\":\"Allowed\",\"parentSpanId\":null,\"policyHash\":null,\"policyScope\":null,\"policyVersion\":\"policy-v1\",\"reasonCodes\":[\"policy.a\",\"policy.b\"],\"riskScore\":0.5,\"schemaVersion\":\"1.0.0\",\"spanId\":null,\"tenantHash\":null,\"traceId\":null},\"payloadSchemaVersion\":\"1.0.0\"}";

    private const string DecisionReceiptVectorSha256 = "9636918eea3a4d7e121499e4def36588950427de5250739df5d4fe9c4e01a6b5";

    private const string ExecutionReceiptVectorJson = /*lang=json,strict*/ "{\"artifactId\":\"operation-1:attempt-1\",\"artifactType\":\"asibackbone.governed-operation-execution-receipt\",\"canonicalizationVersion\":\"asibackbone.canonical-json.v1\",\"content\":{\"completedUtc\":\"2026-09-22T12:00:00.0000000Z\",\"completedWithoutMutation\":false,\"decisionAuditRecordId\":\"record-1\",\"executionAttemptId\":\"attempt-1\",\"metadata\":{\"safe\":\"included\"},\"mutationBatchId\":\"batch-1\",\"mutationManifestAlgorithm\":\"SHA-256\",\"mutationManifestHash\":\"abcdef\",\"mutationRecordCount\":2,\"operationExecutionId\":\"operation-1\",\"persistenceOutcome\":\"Committed\",\"persistenceProvider\":\"efcore\"},\"payloadSchemaVersion\":\"1.0.0\"}";

    private const string ExecutionReceiptVectorSha256 = "9f9614b15817b0728fdb7f81f311e4d1578a5173d1cd1d52d5ae3cacb2b15d11";

    private static readonly string[] SafeAllowList = ["safe"];

    private static readonly string[] VectorMetadataAllowList = ["region", "tier", "blank", "note"];

    private static readonly string[] RegionAllowList = ["region"];

    private static readonly string[] UnnormalizedReasonCodes = [" policy.b ", "policy.a", "policy.b", "", "   ", null!];

    private static readonly object?[] UnorderedItems = ["b", "a", null];

    /// <summary>
    /// Locks string escaping, integer, Boolean, null, array-order, and envelope-descriptor trimming bytes.
    /// </summary>
    [Fact]
    public void EscapingVectorMatchesPublishedBytesAndHash()
    {
        var payload = CanonicalPayload.Create(
            "  artifact-type  ",
            "  artifact-2  ",
            " schema-v1 ",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["text"] = "tab\tline\nquote\"slash\\ctl\u0001emoji\U0001F600",
                ["items"] = UnorderedItems,
                ["flag"] = false,
                ["count"] = -7,
                ["big"] = 9007199254740993L
            });

        Assert.Equal(EscapingVectorJson, payload.CanonicalJson);
        Assert.Equal(EscapingVectorUtf8Hex, Convert.ToHexString(payload.ToUtf8Bytes()));
        Assert.Equal(EscapingVectorSha256, CanonicalPayloadHasher.ComputeHash(payload).HashValue);
    }

    /// <summary>
    /// Locks the remaining escaping rules listed in the specification table: the backtick, U+007F, and the short
    /// escapes for backspace, form feed, and carriage return.
    /// </summary>
    [Fact]
    public void SpecifiedEscapesOutsideTheGoldenVectorsAreLocked()
    {
        var payload = CanonicalPayload.Create(
            "artifact-type",
            "artifact-3",
            "schema-v1",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["text"] = "`\u007F\b\f\r"
            });

        Assert.Contains("\"text\":\"\\u0060\\u007F\\b\\f\\r\"", payload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Locks the double formatting table in the specification, including the fixed/scientific notation boundaries.
    /// </summary>
    /// <param name="value">The double to serialize.</param>
    /// <param name="expected">The published canonical text.</param>
    [Theory]
    [InlineData(0.25d, "0.25")]
    [InlineData(1.0d, "1")]
    [InlineData(-1.5d, "-1.5")]
    [InlineData(1.0d / 3.0d, "0.3333333333333333")]
    [InlineData(0.0001d, "0.0001")]
    [InlineData(0.00001d, "1E-05")]
    [InlineData(1e14d, "100000000000000")]
    [InlineData(1e15d, "1E+15")]
    [InlineData(9007199254740992d, "9007199254740992")]
    [InlineData(12345678901234568d, "12345678901234568")]
    [InlineData(123456789012345680d, "1.2345678901234568E+17")]
    [InlineData(1e21d, "1E+21")]
    [InlineData(double.MaxValue, "1.7976931348623157E+308")]
    [InlineData(double.Epsilon, "5E-324")]
    [InlineData(0.0d, "0")]
    public void DoubleFormattingMatchesPublishedTable(double value, string expected)
    {
        Assert.Equal(expected, SerializeNumber(value));
    }

    /// <summary>
    /// Negative zero keeps its sign. The value is built from its bit pattern so the compiler cannot fold it to zero.
    /// </summary>
    [Fact]
    public void NegativeZeroIsEmittedWithSign()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(long.MinValue);

        Assert.Equal("-0", SerializeNumber(negativeZero));
    }

    /// <summary>
    /// Locks a builder-level vector covering timestamp formatting, enum wire names, string-set normalization, and
    /// metadata filtering, trimming, and runtime-null handling.
    /// </summary>
    [Fact]
    public void DecisionReceiptVectorMatchesPublishedBytesAndHash()
    {
        VectorReceipt receipt = new()
        {
            OccurredUtc = new DateTimeOffset(2026, 9, 22, 14, 34, 56, TimeSpan.FromHours(2)).AddTicks(1_234_567),
            ReasonCodes = UnnormalizedReasonCodes,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [" region "] = "  us-east  ",
                ["tier"] = null!,
                ["blank"] = "   ",
                ["note"] = "two  words",
                ["ignored"] = "not-allow-listed"
            }
        };

        CanonicalPayload payload = CanonicalPayloadBuilder.ForDecisionReceipt(
            receipt,
            CanonicalPayloadOptions.Create(VectorMetadataAllowList));

        Assert.Equal(DecisionReceiptVectorJson, payload.CanonicalJson);
        Assert.Equal(DecisionReceiptVectorSha256, CanonicalPayloadHasher.ComputeHash(payload).HashValue);
    }

    /// <summary>
    /// Locks the governed operation execution receipt vector, which uses the shared metadata filter.
    /// </summary>
    [Fact]
    public void ExecutionReceiptVectorMatchesPublishedBytesAndHash()
    {
        var receipt = GovernedOperationExecutionReceipt.Create(
            "operation-1",
            GovernedOperationPersistenceOutcome.Committed,
            executionAttemptId: "attempt-1",
            mutationBatchId: "batch-1",
            mutationRecordCount: 2,
            mutationManifestHash: "ABCDEF",
            mutationManifestAlgorithm: "SHA-256",
            completedUtc: new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
            persistenceProvider: "efcore",
            decisionAuditRecordId: "record-1",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [" safe "] = "  included  ",
                ["ignored"] = "x"
            });

        CanonicalPayload payload = GovernedOperationExecutionReceiptCanonicalPayload.Create(
            receipt,
            CanonicalPayloadOptions.Create(SafeAllowList));

        Assert.Equal(ExecutionReceiptVectorJson, payload.CanonicalJson);
        Assert.Equal(ExecutionReceiptVectorSha256, CanonicalPayloadHasher.ComputeHash(payload).HashValue);
    }

    /// <summary>
    /// Leading and trailing white space in allow-listed metadata keys and values is insignificant by contract, and
    /// runtime null, empty, and white-space-only values are equivalent.
    /// </summary>
    /// <param name="key">The metadata key variant.</param>
    /// <param name="value">The metadata value variant.</param>
    /// <param name="canonicalValue">The value the variant must be equivalent to.</param>
    [Theory]
    [InlineData("region", "us-east", "us-east")]
    [InlineData(" region ", "us-east", "us-east")]
    [InlineData("region", "  us-east  ", "us-east")]
    [InlineData("\tregion\n", "\u00A0us-east\u3000", "us-east")]
    [InlineData("region", null, "")]
    [InlineData("region", "", "")]
    [InlineData("region", " \t ", "")]
    public void MetadataNormalizationCollapsesDocumentedDistinctions(string key, string? value, string canonicalValue)
    {
        string variantHash = HashReceiptWithRegion(key, value);
        string canonicalHash = HashReceiptWithRegion("region", canonicalValue);

        Assert.Equal(canonicalHash, variantHash);
    }

    /// <summary>
    /// Interior white space and case remain significant in metadata values.
    /// </summary>
    /// <param name="left">The first metadata value.</param>
    /// <param name="right">A value that must hash differently.</param>
    [Theory]
    [InlineData("us east", "us  east")]
    [InlineData("us east", "useast")]
    [InlineData("us-east", "US-EAST")]
    public void MetadataNormalizationPreservesInteriorWhiteSpaceAndCase(string left, string right)
    {
        Assert.NotEqual(HashReceiptWithRegion("region", left), HashReceiptWithRegion("region", right));
    }

    /// <summary>
    /// Keys that become identical after trimming are rejected instead of letting enumeration order choose the value.
    /// </summary>
    [Fact]
    public void MetadataKeysThatCollideAfterTrimmingAreRejected()
    {
        VectorReceipt receipt = new()
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = "us-east",
                [" region"] = "us-west"
            }
        };

        _ = Assert.Throws<ArgumentException>(() =>
            CanonicalPayloadBuilder.ForDecisionReceipt(receipt, CanonicalPayloadOptions.Create(RegionAllowList)));
    }

    /// <summary>
    /// With default options no metadata reaches the payload, and the metadata property is emitted as an empty object.
    /// </summary>
    [Fact]
    public void DefaultOptionsEmitEmptyMetadataObject()
    {
        VectorReceipt receipt = new()
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = "us-east"
            }
        };

        CanonicalPayload payload = CanonicalPayloadBuilder.ForDecisionReceipt(receipt);

        Assert.Contains("\"metadata\":{}", payload.CanonicalJson, StringComparison.Ordinal);
    }

    private static string SerializeNumber(double value)
    {
        const string prefix = "\"content\":{\"n\":";

        var payload = CanonicalPayload.Create(
            "artifact-type",
            "artifact-number",
            "schema-v1",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["n"] = value
            });

        int start = payload.CanonicalJson.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        int end = payload.CanonicalJson.IndexOf('}', start);

        return payload.CanonicalJson[start..end];
    }

    private static string HashReceiptWithRegion(string key, string? value)
    {
        VectorReceipt receipt = new()
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [key] = value!
            }
        };

        CanonicalPayload payload = CanonicalPayloadBuilder.ForDecisionReceipt(
            receipt,
            CanonicalPayloadOptions.Create(RegionAllowList));

        return CanonicalPayloadHasher.ComputeHash(payload).HashValue;
    }

    private sealed class VectorReceipt : IDecisionReceipt
    {
        public string EventId { get; init; } = "receipt-1";

        public DateTimeOffset OccurredUtc { get; init; } = new(2026, 9, 22, 12, 34, 56, TimeSpan.Zero);

        public string ActorId { get; init; } = "actor-1";

        public GovernanceActorType ActorType { get; init; } = GovernanceActorType.Human;

        public string? ActorDisplayName { get; init; }

        public string OperationName { get; init; } = "orders.approve";

        public string Outcome { get; init; } = "Allowed";

        public IReadOnlyList<string> ReasonCodes { get; init; } = [];

        public string? CorrelationId { get; init; } = "correlation-1";

        public string? TraceId { get; init; }

        public long? DecisionLatencyMs { get; init; } = 42;

        public double? RiskScore { get; init; } = 0.5;

        public string? PolicyVersion { get; init; } = "policy-v1";

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    }
}
