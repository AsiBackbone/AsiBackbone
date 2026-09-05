# Canonical JSON v1 Format

`asibackbone.canonical-json.v1` defines the exact bytes produced by `CanonicalPayload` for hashing and signing. Implementations outside .NET must reproduce these rules byte-for-byte before verifying a hash or signature.

> [!IMPORTANT]
> This is an AsiBackbone-specific format. It is **not** RFC 8785 JSON Canonicalization Scheme (JCS), and a JCS serializer will not necessarily produce the same bytes.

## Encoding and envelope

The result is UTF-8 without a byte-order mark, indentation, insignificant whitespace, or a trailing newline. The root object contains these properties in ordinal order:

1. `artifactId`
2. `artifactType`
3. `canonicalizationVersion`
4. `content`
5. `payloadSchemaVersion`

All object properties, including nested content and metadata properties, are ordered by their UTF-16 property-name code units using ordinal comparison. Array element order is preserved. Builders normalize fields documented as unordered sets before serialization.

## Strings

Strings use the default `System.Text.Json.Utf8JsonWriter` encoder. In addition to JSON control characters, non-ASCII characters and HTML-sensitive ASCII characters are escaped with uppercase four-digit `\uXXXX` sequences. This includes `<`, `>`, `&`, `'`, and `+`. Supplementary Unicode scalar values are represented as two escaped UTF-16 surrogate code units.

This escaping is intentionally different from JCS, which generally emits Unicode characters directly.

## Numbers and values

The supported values are null, Boolean, string, signed 32-bit integer, signed 64-bit integer, finite IEEE 754 binary64 (`double`), objects with string keys, and arrays containing supported values.

- Integers are emitted as base-10 JSON numbers without leading zeroes.
- Finite doubles use the shortest round-trippable representation emitted by `Utf8JsonWriter.WriteNumberValue(double)` on the supported .NET runtime.
- NaN and positive or negative infinity are rejected.
- No implicit conversion is performed for decimal, date/time, enum, or arbitrary object values. Artifact builders convert their supported domain values before serialization.
- Null object properties are retained.

The golden vector below is the compatibility authority for v1 behavior. A change to these bytes requires a new canonicalization version.

## Golden interoperability vector

Inputs:

```text
artifactId: artifact-1
artifactType: artifact-type
canonicalizationVersion: asibackbone.canonical-json.v1
payloadSchemaVersion: schema-v1
content:
  aNumber: 0.25
  aString: <>&'+é
  nested:
    beta: 2
    alpha: 1
```

Canonical JSON (shown as ASCII text, so every backslash is a literal byte):

```json
{"artifactId":"artifact-1","artifactType":"artifact-type","canonicalizationVersion":"asibackbone.canonical-json.v1","content":{"aNumber":0.25,"aString":"\u003C\u003E\u0026\u0027\u002B\u00E9","nested":{"alpha":1,"beta":2}},"payloadSchemaVersion":"schema-v1"}
```

Uppercase hexadecimal UTF-8 bytes:

```text
7B2261727469666163744964223A2261727469666163742D31222C22617274696661637454797065223A2261727469666163742D74797065222C2263616E6F6E6963616C697A6174696F6E56657273696F6E223A226173696261636B626F6E652E63616E6F6E6963616C2D6A736F6E2E7631222C22636F6E74656E74223A7B22614E756D626572223A302E32352C2261537472696E67223A225C75303033435C75303033455C75303032365C75303032375C75303032425C7530304539222C226E6573746564223A7B22616C706861223A312C2262657461223A327D7D2C227061796C6F6164536368656D6156657273696F6E223A22736368656D612D7631227D
```

## Hash selection

`CanonicalPayloadOptions.HashAlgorithm` is carried by the payload and used when `CanonicalPayloadHasher.ComputeHash(payload)` is called. The built-in hasher accepts `SHA256` or `SHA-256` and normalizes the descriptor to `SHA-256`; it accepts `SHA512` or `SHA-512` and normalizes the descriptor to `SHA-512`. Any other value produces `NotSupportedException` rather than silently falling back.

The hash algorithm is not part of the canonical JSON envelope. The same canonical bytes may therefore be hashed with a different algorithm, and verifiers must use the algorithm descriptor carried with the hash or signing metadata.

## Versioning rule

Bug fixes that do not alter canonical bytes may retain `asibackbone.canonical-json.v1`. Any change to property ordering, escaping, number formatting, value support, null handling, or UTF-8 encoding that can alter bytes requires a new canonicalization version and new golden vectors.
