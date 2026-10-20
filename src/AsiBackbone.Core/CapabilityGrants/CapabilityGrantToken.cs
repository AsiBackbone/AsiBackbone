using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text.Json;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.CapabilityGrants;

/// <summary>
/// Encodes and decodes the versioned, size-limited transport form of a signed capability grant.
/// </summary>
/// <remarks>
/// <para>
/// A token is the prefix <see cref="FormatPrefix" /> followed by the unpadded base64url encoding of a UTF-8 JSON
/// document. The document carries format version <see cref="FormatVersion" />, every grant field, and the signing
/// metadata a verifier needs. It does not carry the canonical payload or its hash: <see cref="Decode" /> rebuilds both from
/// the decoded grant fields, so editing any field after signing, including the resource binding, yields a hash the
/// signature does not cover.
/// </para>
/// <para>
/// The token is not encrypted. Every field is readable by anyone who holds it, so a grant must not carry secrets.
/// Decoding is strict: unknown properties, duplicate properties, an unsupported format version, or a token longer
/// than the configured limit fail rather than being ignored. A successful decode is not verification; validate the
/// result with <see cref="CapabilityGrantValidator.ValidateAsync" /> before trusting it.
/// </para>
/// <para>
/// Grant metadata is transported only for keys that the canonical payload options allow, because only those keys are
/// covered by the signature. The issuer and the relying party must use the same <see cref="CanonicalPayloadOptions" />.
/// </para>
/// </remarks>
public static class CapabilityGrantToken
{
    /// <summary>
    /// Gets the prefix that identifies a version 1 capability grant token.
    /// </summary>
    public const string FormatPrefix = "asibcg1.";

    /// <summary>
    /// Gets the token format version written into, and required in, the token document.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// Gets the default maximum token length, in characters, accepted by <see cref="Decode" /> and produced by
    /// <see cref="Encode" />.
    /// </summary>
    public const int DefaultMaxTokenLength = 8 * 1024;

    /// <summary>
    /// The failure code returned when no token was supplied.
    /// </summary>
    public const string MissingFailureCode = "capability.token-missing";

    /// <summary>
    /// The failure code returned when the token exceeds the configured maximum length.
    /// </summary>
    public const string TooLargeFailureCode = "capability.token-too-large";

    /// <summary>
    /// The failure code returned when the token prefix or format version is not supported.
    /// </summary>
    public const string UnsupportedFormatFailureCode = "capability.token-format-unsupported";

    /// <summary>
    /// The failure code returned when the token is not valid base64url, JSON, or token structure.
    /// </summary>
    public const string MalformedFailureCode = "capability.token-malformed";

    /// <summary>
    /// The failure code returned when the token fields do not form a valid capability grant.
    /// </summary>
    public const string InvalidGrantFailureCode = "capability.token-grant-invalid";

    /// <summary>
    /// The failure code returned when the token names a hash algorithm the built-in hasher cannot rebuild.
    /// </summary>
    public const string UnsupportedHashAlgorithmFailureCode = "capability.token-hash-algorithm-unsupported";

    private const int MaxDocumentDepth = 4;
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    // These descriptors are derived from the rebuilt canonical hash. Reading them from the token would let a token
    // describe an artifact other than the one rebuilt from its fields.
    private static readonly HashSet<string> DerivedSigningMetadataKeys = new(StringComparer.Ordinal)
    {
        "artifact_id",
        "artifact_type",
        "canonicalization_version",
        "payload_schema_version"
    };

    private static readonly HashSet<string> DocumentProperties = new(StringComparer.Ordinal) { "v", "grant", "proof" };

    private static readonly HashSet<string> GrantProperties = new(StringComparer.Ordinal)
    {
        "acknowledgmentId",
        "audience",
        "expiresUtc",
        "gatewayBinding",
        "handshakeId",
        "issuedUtc",
        "issuer",
        "maxUseCount",
        "metadata",
        "notBeforeUtc",
        "operationName",
        "policyHash",
        "policyVersion",
        "resourceBinding",
        "schemaVersion",
        "scopes",
        "subjectId",
        "tokenId"
    };

    private static readonly HashSet<string> ProofProperties = new(StringComparer.Ordinal)
    {
        "hashAlgorithm",
        "keyId",
        "keyVersion",
        "metadata",
        "provider",
        "signature",
        "signatureAlgorithm",
        "signedUtc"
    };

    /// <summary>
    /// Encodes a signed capability grant as a version 1 token.
    /// </summary>
    /// <param name="signedGrant">The signed grant, typically from <see cref="GovernanceArtifactSigner.SignCapabilityGrantAsync" />.</param>
    /// <param name="options">The canonical payload options the grant was signed with; defaults apply when <see langword="null" />.</param>
    /// <param name="maxTokenLength">The maximum token length in characters.</param>
    /// <returns>The encoded token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signedGrant" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTokenLength" /> is less than one.</exception>
    /// <exception cref="ArgumentException">
    /// The artifact is not a capability grant, or its canonical hash does not match the grant rebuilt with
    /// <paramref name="options" />, so the token could never verify.
    /// </exception>
    /// <exception cref="InvalidOperationException">The grant is not signed, or the token would exceed <paramref name="maxTokenLength" />.</exception>
    public static string Encode(
        SignedGovernanceArtifact<CapabilityGrant> signedGrant,
        CanonicalPayloadOptions? options = null,
        int maxTokenLength = DefaultMaxTokenLength)
    {
        ArgumentNullException.ThrowIfNull(signedGrant);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTokenLength, 1);

        CapabilityGrant grant = signedGrant.Artifact;

        if (!string.Equals(signedGrant.ArtifactType, CanonicalArtifactTypes.CapabilityGrant, StringComparison.Ordinal)
            || !string.Equals(signedGrant.ArtifactId, grant.TokenId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The signed artifact does not describe this capability grant.", nameof(signedGrant));
        }

        if (!signedGrant.IsSigned)
        {
            throw new InvalidOperationException("Only a signed capability grant can be encoded as a token.");
        }

        CanonicalPayloadOptions effectiveOptions = options ?? CanonicalPayloadOptions.Default;
        CanonicalPayloadHash rebuiltHash = CanonicalPayloadHasher.ComputeHash(
            CanonicalPayloadBuilder.ForCapabilityGrant(grant, effectiveOptions),
            signedGrant.HashAlgorithm);

        if (!string.Equals(rebuiltHash.HashValue, signedGrant.CanonicalHash.HashValue, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The grant rebuilt with the supplied canonical payload options does not hash to the signed hash. Pass the options the grant was signed with.",
                nameof(options));
        }

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", FormatVersion);
            WriteGrant(writer, grant, effectiveOptions);
            WriteProof(writer, signedGrant);
            writer.WriteEndObject();
        }

        string token = FormatPrefix + Base64Url.EncodeToString(buffer.WrittenSpan);

        return token.Length > maxTokenLength
            ? throw new InvalidOperationException(
                $"The encoded capability grant token is {token.Length} characters, which exceeds the {maxTokenLength}-character limit.")
            : token;
    }

    /// <summary>
    /// Decodes a version 1 token into a signed capability grant whose canonical payload and hash are rebuilt from the
    /// decoded fields.
    /// </summary>
    /// <remarks>
    /// Decoding never throws for token content. Every malformed, oversized, or unsupported token produces a failed
    /// result with a stable failure code.
    /// </remarks>
    /// <param name="token">The token to decode.</param>
    /// <param name="options">The canonical payload options the issuer signed with; defaults apply when <see langword="null" />.</param>
    /// <param name="maxTokenLength">The maximum accepted token length in characters.</param>
    /// <returns>The decode result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTokenLength" /> is less than one.</exception>
    public static CapabilityGrantTokenDecodeResult Decode(
        string? token,
        CanonicalPayloadOptions? options = null,
        int maxTokenLength = DefaultMaxTokenLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTokenLength, 1);

        if (string.IsNullOrWhiteSpace(token))
        {
            return CapabilityGrantTokenDecodeResult.Failure(MissingFailureCode, "No capability grant token was supplied.");
        }

        string trimmed = token.Trim();

        if (trimmed.Length > maxTokenLength)
        {
            return CapabilityGrantTokenDecodeResult.Failure(
                TooLargeFailureCode,
                $"The capability grant token exceeds the {maxTokenLength}-character limit.");
        }

        if (!trimmed.StartsWith(FormatPrefix, StringComparison.Ordinal))
        {
            return CapabilityGrantTokenDecodeResult.Failure(
                UnsupportedFormatFailureCode,
                "The capability grant token does not use a supported format prefix.");
        }

        ReadOnlySpan<char> encoded = trimmed.AsSpan(FormatPrefix.Length);

        if (encoded.IsEmpty || !Base64Url.IsValid(encoded, out int decodedLength))
        {
            return Malformed("The capability grant token body is not valid base64url.");
        }

        byte[] documentBytes = new byte[decodedLength];

        if (!Base64Url.TryDecodeFromChars(encoded, documentBytes, out int bytesWritten) || bytesWritten != decodedLength)
        {
            return Malformed("The capability grant token body is not valid base64url.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                documentBytes,
                new JsonDocumentOptions
                {
                    MaxDepth = MaxDocumentDepth,
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    AllowDuplicateProperties = false
                });

            return DecodeDocument(document.RootElement, options ?? CanonicalPayloadOptions.Default);
        }
        catch (JsonException)
        {
            return Malformed("The capability grant token body is not a valid token document.");
        }
        catch (TokenStructureException exception)
        {
            return Malformed(exception.Message);
        }
    }

    private static CapabilityGrantTokenDecodeResult DecodeDocument(JsonElement root, CanonicalPayloadOptions options)
    {
        RequireObject(root, "token document");
        RejectUnknownProperties(root, DocumentProperties, "token document");

        if (!root.TryGetProperty("v", out JsonElement versionElement)
            || versionElement.ValueKind is not JsonValueKind.Number
            || !versionElement.TryGetInt32(out int version))
        {
            throw new TokenStructureException("The capability grant token document has no format version.");
        }

        if (version != FormatVersion)
        {
            return CapabilityGrantTokenDecodeResult.Failure(
                UnsupportedFormatFailureCode,
                "The capability grant token format version is not supported.");
        }

        JsonElement grantElement = GetRequiredObject(root, "grant", "token document");
        JsonElement proofElement = GetRequiredObject(root, "proof", "token document");
        RejectUnknownProperties(grantElement, GrantProperties, "grant");
        RejectUnknownProperties(proofElement, ProofProperties, "proof");

        CapabilityGrant grant;

        try
        {
            grant = CapabilityGrant.Create(
                tokenId: GetRequiredString(grantElement, "tokenId"),
                issuer: GetRequiredString(grantElement, "issuer"),
                audience: GetRequiredString(grantElement, "audience"),
                scopes: GetRequiredStringArray(grantElement, "scopes"),
                issuedUtc: GetRequiredTimestamp(grantElement, "issuedUtc"),
                expiresUtc: GetRequiredTimestamp(grantElement, "expiresUtc"),
                notBeforeUtc: GetOptionalTimestamp(grantElement, "notBeforeUtc"),
                subjectId: GetOptionalString(grantElement, "subjectId"),
                operationName: GetOptionalString(grantElement, "operationName"),
                policyVersion: GetOptionalString(grantElement, "policyVersion"),
                policyHash: GetOptionalString(grantElement, "policyHash"),
                acknowledgmentId: GetOptionalString(grantElement, "acknowledgmentId"),
                handshakeId: GetOptionalString(grantElement, "handshakeId"),
                gatewayBinding: GetOptionalString(grantElement, "gatewayBinding"),
                resourceBinding: GetOptionalString(grantElement, "resourceBinding"),
                metadata: FilterGrantMetadata(GetOptionalStringMap(grantElement, "metadata"), options),
                maxUseCount: GetOptionalInt32(grantElement, "maxUseCount"),
                schemaVersion: GetRequiredString(grantElement, "schemaVersion"));
        }
        catch (ArgumentException)
        {
            return CapabilityGrantTokenDecodeResult.Failure(
                InvalidGrantFailureCode,
                "The capability grant token fields do not form a valid capability grant.");
        }

        string hashAlgorithm = GetRequiredString(proofElement, "hashAlgorithm");
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityGrant(grant, options);
        CanonicalPayloadHash hash;

        try
        {
            hash = CanonicalPayloadHasher.ComputeHash(payload, hashAlgorithm);
        }
        catch (NotSupportedException)
        {
            return CapabilityGrantTokenDecodeResult.Failure(
                UnsupportedHashAlgorithmFailureCode,
                "The capability grant token names a hash algorithm the built-in hasher does not support.");
        }

        Dictionary<string, string> signingMetadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in GetOptionalStringMap(proofElement, "metadata"))
        {
            if (!DerivedSigningMetadataKeys.Contains(item.Key.Trim()))
            {
                signingMetadata[item.Key] = item.Value;
            }
        }

        var metadata = SigningMetadata.Create(
            signingHash: hash.HashValue,
            hashAlgorithm: hash.HashAlgorithm,
            signature: GetOptionalString(proofElement, "signature"),
            signatureAlgorithm: GetOptionalString(proofElement, "signatureAlgorithm"),
            keyId: GetOptionalString(proofElement, "keyId"),
            keyVersion: GetOptionalString(proofElement, "keyVersion"),
            provider: GetOptionalString(proofElement, "provider"),
            signedUtc: GetOptionalTimestamp(proofElement, "signedUtc"),
            metadata: signingMetadata);

        return CapabilityGrantTokenDecodeResult.Success(
            SignedGovernanceArtifacts.FromSigningMetadata(grant, payload, hash, metadata));
    }

    private static void WriteGrant(Utf8JsonWriter writer, CapabilityGrant grant, CanonicalPayloadOptions options)
    {
        writer.WriteStartObject("grant");
        WriteOptionalString(writer, "acknowledgmentId", grant.AcknowledgmentId);
        writer.WriteString("audience", grant.Audience);
        writer.WriteString("expiresUtc", FormatTimestamp(grant.ExpiresUtc));
        WriteOptionalString(writer, "gatewayBinding", grant.GatewayBinding);
        WriteOptionalString(writer, "handshakeId", grant.HandshakeId);
        writer.WriteString("issuedUtc", FormatTimestamp(grant.IssuedUtc));
        writer.WriteString("issuer", grant.Issuer);

        if (grant.MaxUseCount.HasValue)
        {
            writer.WriteNumber("maxUseCount", grant.MaxUseCount.Value);
        }

        Dictionary<string, string> boundMetadata = FilterGrantMetadata(grant.Metadata, options);
        if (boundMetadata.Count > 0)
        {
            writer.WriteStartObject("metadata");
            foreach (KeyValuePair<string, string> item in boundMetadata.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                writer.WriteString(item.Key, item.Value);
            }

            writer.WriteEndObject();
        }

        if (grant.NotBeforeUtc.HasValue)
        {
            writer.WriteString("notBeforeUtc", FormatTimestamp(grant.NotBeforeUtc.Value));
        }

        WriteOptionalString(writer, "operationName", grant.OperationName);
        WriteOptionalString(writer, "policyHash", grant.PolicyHash);
        WriteOptionalString(writer, "policyVersion", grant.PolicyVersion);
        WriteOptionalString(writer, "resourceBinding", grant.ResourceBinding);
        writer.WriteString("schemaVersion", grant.SchemaVersion);
        writer.WriteStartArray("scopes");
        foreach (string scope in grant.Scopes)
        {
            writer.WriteStringValue(scope);
        }

        writer.WriteEndArray();
        WriteOptionalString(writer, "subjectId", grant.SubjectId);
        writer.WriteString("tokenId", grant.TokenId);
        writer.WriteEndObject();
    }

    private static void WriteProof(Utf8JsonWriter writer, SignedGovernanceArtifact<CapabilityGrant> signedGrant)
    {
        SigningMetadata signing = signedGrant.SigningMetadata;

        writer.WriteStartObject("proof");
        writer.WriteString("hashAlgorithm", signedGrant.HashAlgorithm);
        WriteOptionalString(writer, "keyId", signing.KeyId);
        WriteOptionalString(writer, "keyVersion", signing.KeyVersion);

        KeyValuePair<string, string>[] transportedMetadata = [.. signing.Metadata
            .Where(item => !DerivedSigningMetadataKeys.Contains(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)];

        if (transportedMetadata.Length > 0)
        {
            writer.WriteStartObject("metadata");
            foreach (KeyValuePair<string, string> item in transportedMetadata)
            {
                writer.WriteString(item.Key, item.Value);
            }

            writer.WriteEndObject();
        }

        WriteOptionalString(writer, "provider", signing.Provider);
        WriteOptionalString(writer, "signature", signing.Signature);
        WriteOptionalString(writer, "signatureAlgorithm", signing.SignatureAlgorithm);

        if (signing.SignedUtc.HasValue)
        {
            writer.WriteString("signedUtc", FormatTimestamp(signing.SignedUtc.Value));
        }

        writer.WriteEndObject();
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static Dictionary<string, string> FilterGrantMetadata(
        IReadOnlyDictionary<string, string> metadata,
        CanonicalPayloadOptions options)
    {
        Dictionary<string, string> filtered = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (options.AllowsMetadataKey(item.Key))
            {
                filtered[item.Key] = item.Value;
            }
        }

        return filtered;
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }

    private static CapabilityGrantTokenDecodeResult Malformed(string message)
    {
        return CapabilityGrantTokenDecodeResult.Failure(MalformedFailureCode, message);
    }

    private static void RequireObject(JsonElement element, string description)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            throw new TokenStructureException($"The capability grant {description} must be a JSON object.");
        }
    }

    private static void RejectUnknownProperties(JsonElement element, HashSet<string> knownProperties, string description)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!knownProperties.Contains(property.Name))
            {
                throw new TokenStructureException($"The capability grant {description} contains an unsupported property.");
            }
        }
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName, string description)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind is JsonValueKind.Object
            ? value
            : throw new TokenStructureException($"The capability grant {description} requires a '{propertyName}' object.");
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        return GetOptionalString(element, propertyName)
            ?? throw new TokenStructureException($"The capability grant token requires a '{propertyName}' value.");
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return !element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind is JsonValueKind.Null
            ? null
            : value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : throw new TokenStructureException($"The capability grant token '{propertyName}' value must be a string.");
    }

    private static string[] GetRequiredStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind is not JsonValueKind.Array)
        {
            throw new TokenStructureException($"The capability grant token requires a '{propertyName}' array.");
        }

        List<string> values = [];

        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind is not JsonValueKind.String)
            {
                throw new TokenStructureException($"The capability grant token '{propertyName}' array must contain only strings.");
            }

            values.Add(item.GetString()!);
        }

        return [.. values];
    }

    private static Dictionary<string, string> GetOptionalStringMap(JsonElement element, string propertyName)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);

        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind is JsonValueKind.Null)
        {
            return values;
        }

        if (value.ValueKind is not JsonValueKind.Object)
        {
            throw new TokenStructureException($"The capability grant token '{propertyName}' value must be an object.");
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Value.ValueKind is not JsonValueKind.String)
            {
                throw new TokenStructureException($"The capability grant token '{propertyName}' values must be strings.");
            }

            values[property.Name] = property.Value.GetString()!;
        }

        return values;
    }

    private static int? GetOptionalInt32(JsonElement element, string propertyName)
    {
        return !element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind is JsonValueKind.Null
            ? null
            : value.ValueKind is JsonValueKind.Number && value.TryGetInt32(out int result)
            ? result
            : throw new TokenStructureException($"The capability grant token '{propertyName}' value must be a 32-bit integer.");
    }

    private static DateTimeOffset GetRequiredTimestamp(JsonElement element, string propertyName)
    {
        return GetOptionalTimestamp(element, propertyName)
            ?? throw new TokenStructureException($"The capability grant token requires a '{propertyName}' timestamp.");
    }

    private static DateTimeOffset? GetOptionalTimestamp(JsonElement element, string propertyName)
    {
        string? value = GetOptionalString(element, propertyName);

        return value is null
            ? null
            : DateTimeOffset.TryParseExact(
            value,
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset parsed)
            ? parsed
            : throw new TokenStructureException($"The capability grant token '{propertyName}' value must be a UTC timestamp in the token format.");
    }

    private sealed class TokenStructureException(string message) : Exception(message);
}
