using AsiBackbone.Core.Serialization;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Tests for the <see cref="CanonicalPayload"/> class, specifically focusing on the JSON serialization of supported primitive dictionary and array values, as well as handling of unsupported content value types and non-finite double values.
/// </summary>
public sealed class CanonicalPayloadJsonBranchTests
{
    private static readonly string[] content = ["beta", "alpha"];

    /// <summary>
    /// Locks the canonical JSON v1 bytes used by non-.NET verifiers, including ordinal property ordering and the
    /// escaping behavior of the default <see cref="System.Text.Json.Utf8JsonWriter"/> encoder.
    /// </summary>
    [Fact]
    public void CanonicalJsonV1MatchesGoldenUtf8Bytes()
    {
        var payload = CanonicalPayload.Create(
            "artifact-type",
            "artifact-1",
            "schema-v1",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["nested"] = new Dictionary<string, object?>
                {
                    ["beta"] = 2,
                    ["alpha"] = 1
                },
                ["aString"] = "<>&'+é",
                ["aNumber"] = 0.25d
            });

        const string expectedJson = /*lang=json,strict*/ "{\"artifactId\":\"artifact-1\",\"artifactType\":\"artifact-type\",\"canonicalizationVersion\":\"asibackbone.canonical-json.v1\",\"content\":{\"aNumber\":0.25,\"aString\":\"\\u003C\\u003E\\u0026\\u0027\\u002B\\u00E9\",\"nested\":{\"alpha\":1,\"beta\":2}},\"payloadSchemaVersion\":\"schema-v1\"}";
        const string expectedUtf8Hex = "7B2261727469666163744964223A2261727469666163742D31222C22617274696661637454797065223A2261727469666163742D74797065222C2263616E6F6E6963616C697A6174696F6E56657273696F6E223A226173696261636B626F6E652E63616E6F6E6963616C2D6A736F6E2E7631222C22636F6E74656E74223A7B22614E756D626572223A302E32352C2261537472696E67223A225C75303033435C75303033455C75303032365C75303032375C75303032425C7530304539222C226E6573746564223A7B22616C706861223A312C2262657461223A327D7D2C227061796C6F6164536368656D6156657273696F6E223A22736368656D612D7631227D";

        Assert.Equal(expectedJson, payload.CanonicalJson);
        Assert.Equal(expectedUtf8Hex, Convert.ToHexString(payload.ToUtf8Bytes()));
    }

    /// <summary>
    /// Tests that the <c>CanonicalPayload.Create</c> method correctly serializes supported primitive dictionary and array values into canonical JSON format. The test verifies that the resulting JSON string contains the expected key-value pairs and that the payload can be converted to UTF-8 bytes without errors.
    /// </summary>
    [Fact]
    public void CreateSerializesSupportedPrimitiveDictionaryAndArrayValues()
    {
        var payload = CanonicalPayload.Create(
            CanonicalArtifactTypes.AuditLedgerRecord,
            "record-1",
            AsiBackboneSchemaVersions.StableArtifactsV1,
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["nullValue"] = null,
                ["stringValue"] = "alpha",
                ["boolValue"] = true,
                ["intValue"] = 7,
                ["longValue"] = 8L,
                ["doubleValue"] = 0.25d,
                ["stringDictionary"] = new Dictionary<string, string>
                {
                    ["beta"] = "2",
                    ["alpha"] = "1"
                },
                ["stringArray"] = content,
                ["objectArray"] = new object?[] { "gamma", 9, false, null },
                ["nested"] = new Dictionary<string, object?>
                {
                    ["child"] = "value"
                }
            });

        Assert.Contains("\"boolValue\":true", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"doubleValue\":0.25", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"intValue\":7", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"longValue\":8", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"nullValue\":null", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"objectArray\":[\"gamma\",9,false,null]", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"stringArray\":[\"beta\",\"alpha\"]", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"stringDictionary\":{\"alpha\":\"1\",\"beta\":\"2\"}", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"nested\":{\"child\":\"value\"}", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.NotEmpty(payload.ToUtf8Bytes());
    }

    /// <summary>
    /// Tests that the <c>CanonicalPayload.Create</c> method throws an <see cref="ArgumentOutOfRangeException"/> when attempting to create a payload with non-finite double values (NaN, PositiveInfinity, NegativeInfinity). The test uses the [Theory] attribute to run the test for each of the specified non-finite double values.
    /// </summary>
    /// <param name="value">
    /// The non-finite double value to test (NaN, PositiveInfinity, or NegativeInfinity).
    /// </param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void CreateRejectsNonFiniteDoubleValues(double value)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalPayload.Create(
                CanonicalArtifactTypes.AuditLedgerRecord,
                "record-1",
                AsiBackboneSchemaVersions.StableArtifactsV1,
                CanonicalPayloadOptions.DefaultCanonicalizationVersion,
                new Dictionary<string, object?>
                {
                    ["number"] = value
                }));
    }

    /// <summary>
    /// Tests that the <c>CanonicalPayload.Create</c> method throws a <see cref="NotSupportedException"/> when attempting to create a payload with an unsupported content value type (in this case, a DateTimeOffset). The test verifies that the exception is thrown as expected when an unsupported type is included in the content dictionary.
    /// </summary>
    [Fact]
    public void CreateRejectsUnsupportedContentValueType()
    {
        _ = Assert.Throws<NotSupportedException>(() =>
            CanonicalPayload.Create(
                CanonicalArtifactTypes.AuditLedgerRecord,
                "record-1",
                AsiBackboneSchemaVersions.StableArtifactsV1,
                CanonicalPayloadOptions.DefaultCanonicalizationVersion,
                new Dictionary<string, object?>
                {
                    ["unsupported"] = DateTimeOffset.UtcNow
                }));
    }
}
