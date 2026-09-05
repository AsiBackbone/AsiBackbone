using System.Buffers;
using System.Text;
using System.Text.Json;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Represents a deterministic, provider-neutral payload that can be hashed and later signed by a host or provider package.
/// </summary>
public sealed class CanonicalPayload
{
    private CanonicalPayload(
        string artifactType,
        string artifactId,
        string payloadSchemaVersion,
        string canonicalizationVersion,
        string hashAlgorithm,
        string canonicalJson)
    {
        ArtifactType = artifactType;
        ArtifactId = artifactId;
        PayloadSchemaVersion = payloadSchemaVersion;
        CanonicalizationVersion = canonicalizationVersion;
        HashAlgorithm = hashAlgorithm;
        CanonicalJson = canonicalJson;
    }

    /// <summary>
    /// Gets the stable artifact type bound into the canonical payload.
    /// </summary>
    public string ArtifactType { get; }

    /// <summary>
    /// Gets the stable artifact identifier bound into the canonical payload.
    /// </summary>
    public string ArtifactId { get; }

    /// <summary>
    /// Gets the payload schema version bound into the canonical payload.
    /// </summary>
    public string PayloadSchemaVersion { get; }

    /// <summary>
    /// Gets the canonicalization version used to create the payload bytes.
    /// </summary>
    public string CanonicalizationVersion { get; }

    /// <summary>
    /// Gets the hash algorithm selected for the canonical payload bytes.
    /// </summary>
    public string HashAlgorithm { get; }

    /// <summary>
    /// Gets the deterministic JSON payload to hash or sign.
    /// </summary>
    public string CanonicalJson { get; }

    /// <summary>
    /// Creates a canonical payload envelope around deterministic artifact content.
    /// </summary>
    public static CanonicalPayload Create(
        string artifactType,
        string artifactId,
        string payloadSchemaVersion,
        string canonicalizationVersion,
        IReadOnlyDictionary<string, object?> content)
    {
        return Create(
            artifactType,
            artifactId,
            payloadSchemaVersion,
            canonicalizationVersion,
            CanonicalPayloadOptions.DefaultHashAlgorithm,
            content);
    }

    /// <summary>
    /// Creates a canonical payload envelope around deterministic artifact content with a selected hash algorithm.
    /// </summary>
    public static CanonicalPayload Create(
        string artifactType,
        string artifactId,
        string payloadSchemaVersion,
        string canonicalizationVersion,
        string hashAlgorithm,
        IReadOnlyDictionary<string, object?> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactType);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalizationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);
        ArgumentNullException.ThrowIfNull(content);

        string normalizedArtifactType = artifactType.Trim();
        string normalizedArtifactId = artifactId.Trim();
        string normalizedPayloadSchemaVersion = payloadSchemaVersion.Trim();
        string normalizedCanonicalizationVersion = canonicalizationVersion.Trim();
        string normalizedHashAlgorithm = CanonicalPayloadHash.NormalizeHashAlgorithm(hashAlgorithm);

        SortedDictionary<string, object?> envelope = new(StringComparer.Ordinal)
        {
            ["artifactId"] = normalizedArtifactId,
            ["artifactType"] = normalizedArtifactType,
            ["canonicalizationVersion"] = normalizedCanonicalizationVersion,
            ["content"] = content,
            ["payloadSchemaVersion"] = normalizedPayloadSchemaVersion
        };

        return new CanonicalPayload(
            normalizedArtifactType,
            normalizedArtifactId,
            normalizedPayloadSchemaVersion,
            normalizedCanonicalizationVersion,
            normalizedHashAlgorithm,
            CanonicalPayloadJson.Serialize(envelope));
    }

    /// <summary>
    /// Gets the canonical JSON payload as UTF-8 bytes.
    /// </summary>
    public byte[] ToUtf8Bytes()
    {
        return Encoding.UTF8.GetBytes(CanonicalJson);
    }
}

internal static class CanonicalPayloadJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = false
    };

    public static string Serialize(IReadOnlyDictionary<string, object?> value)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, WriterOptions))
        {
            WriteDictionary(writer, value);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case double doubleValue:
                if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Canonical payload numbers must be finite.");
                }

                writer.WriteNumberValue(doubleValue);
                break;
            case IReadOnlyDictionary<string, object?> dictionaryValue:
                WriteDictionary(writer, dictionaryValue);
                break;
            case IReadOnlyDictionary<string, string> stringDictionaryValue:
                WriteStringDictionary(writer, stringDictionaryValue);
                break;
            case IEnumerable<string> stringValues:
                writer.WriteStartArray();
                foreach (string item in stringValues)
                {
                    writer.WriteStringValue(item);
                }

                writer.WriteEndArray();
                break;
            case IEnumerable<object?> objectValues:
                writer.WriteStartArray();
                foreach (object? item in objectValues)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new NotSupportedException($"Canonical payload value type '{value.GetType().FullName}' is not supported.");
        }
    }

    private static void WriteDictionary(Utf8JsonWriter writer, IReadOnlyDictionary<string, object?> dictionary)
    {
        writer.WriteStartObject();

        foreach (KeyValuePair<string, object?> item in dictionary.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(item.Key);
            WriteValue(writer, item.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteStringDictionary(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> dictionary)
    {
        writer.WriteStartObject();

        foreach (KeyValuePair<string, string> item in dictionary.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WriteString(item.Key, item.Value);
        }

        writer.WriteEndObject();
    }
}
