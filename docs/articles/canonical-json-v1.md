# Canonical JSON v1 Format

`asibackbone.canonical-json.v1` defines the exact bytes produced by `CanonicalPayload` for hashing and signing. This page is the normative specification of that format. Implementations outside .NET must reproduce these rules byte-for-byte before verifying a hash or signature.

> [!IMPORTANT]
> This is an AsiBackbone-defined format. It is **not** RFC 8785 JSON Canonicalization Scheme (JCS), and neither a JCS serializer nor a generic "canonical JSON" library will necessarily produce the same bytes. Escaping, number formatting, and the normalization performed by the artifact builders all differ from JCS.

The same byte rules also produce the version 1 signature input (`asibackbone.signature-input.v1`) described in [What the signature covers](cryptographic-security-posture.md#what-the-signature-covers).

Canonicalization happens in two layers:

1. **Artifact builders** (`CanonicalPayloadBuilder`) select the fields of an artifact, format timestamps and enumerations as strings, and filter and normalize metadata and string sets. The result is a tree of supported values.
2. **The canonical writer** (`CanonicalPayload.Create`) wraps that tree in the envelope below and serializes it to UTF-8.

A verifier that recomputes a hash from an artifact must reproduce both layers. A verifier that only checks a hash over bytes it already holds needs only the byte rules.

## Encoding and envelope

The result is UTF-8 without a byte-order mark, indentation, insignificant whitespace, or a trailing newline. The root object contains exactly these properties, in this order:

1. `artifactId`
2. `artifactType`
3. `canonicalizationVersion`
4. `content`
5. `payloadSchemaVersion`

The four string descriptors are trimmed of leading and trailing white space (as defined under [Metadata filtering and normalization](#metadata-filtering-and-normalization)) and must not be empty after trimming. The `content` object is not trimmed or otherwise rewritten by the writer.

## Objects and property order

Object properties, including nested content and metadata properties, are ordered by ordinal comparison of their UTF-16 code units. For property names made only of ASCII characters, which is every name the built-in builders emit, this is the same as ordering by byte value. Property names are escaped with the same rules as string values. Each property name appears once.

## Strings

Strings are written with the default `System.Text.Json.Utf8JsonWriter` encoder:

| Input | Output |
| --- | --- |
| U+0008, U+0009, U+000A, U+000C, U+000D | `\b`, `\t`, `\n`, `\f`, `\r` |
| `\` (U+005C) | `\\` |
| `"` (U+0022) | `\u0022` |
| Other control characters U+0000 to U+001F | `\uXXXX` |
| HTML-sensitive ASCII: `<`, `>`, `&`, `'`, `+`, and `` ` `` | `\uXXXX` |
| Any character outside printable ASCII (U+0020 to U+007E), including U+007F | `\uXXXX` |
| Supplementary characters (above U+FFFF) | Two `\uXXXX` escapes, one per UTF-16 surrogate code unit |
| Other printable ASCII | The character itself |

`XXXX` is four uppercase hexadecimal digits. As a result, canonical JSON v1 output contains only ASCII bytes. This escaping is intentionally different from JCS, which emits most Unicode characters directly and escapes `"` as `\"`.

Strings containing unpaired UTF-16 surrogates are not valid canonical input, and their encoding is not defined by v1. Hosts must not sign such values.

## Numbers and values

The supported values are null, Boolean, string, signed 32-bit integer, signed 64-bit integer, finite IEEE 754 binary64 (`double`), objects with string keys, and arrays containing supported values.

- Integers are emitted as base-10 JSON numbers without leading zeroes, a plus sign, a fraction, or an exponent. The full signed 64-bit range is exact; verifiers in languages whose default number type is binary64 must not parse and re-emit integers larger than 2^53.
- Finite doubles are emitted with the exact algorithm in [Double formatting](#double-formatting). Every finite binary64 value has one defined representation.
- NaN and positive or negative infinity are rejected.
- No implicit conversion is performed for decimal, date/time, enum, or arbitrary object values; the writer rejects them. Artifact builders convert their supported domain values before serialization, as described below.
- Null values are emitted as `null`, and properties whose value is null are retained rather than omitted.

### Double formatting

A finite binary64 value is formatted as follows. This is the invariant-culture general format that `Utf8JsonWriter.WriteNumberValue(double)` produces on .NET Core 3.0 and later, including the `net10.0` target, and it is part of the v1 contract rather than a property of any particular runtime.

1. **Zero.** Positive zero is `0`. Negative zero is `-0`; the sign of zero is preserved and is significant.
2. **Sign.** A negative value is written as `-` followed by the formatting of its magnitude.
3. **Digits.** Compute the shortest decimal significand `d` (digits `d1 d2 ... dn`, with no leading or trailing zeroes) that round-trips to the same binary64 value. When more than one shortest significand round-trips, use the one closest to the exact binary value. These are the same digits produced by ECMAScript `Number.prototype.toString`, Python `repr`, and Ryu-style shortest formatters. Let `k` be the decimal exponent such that the value equals `0.d1d2...dn × 10^k`.
4. **Notation.** Let `m = max(n, 15)`. Use scientific notation when `k > m` or `k < -3`; otherwise use fixed notation.
5. **Fixed notation.** When `k <= 0`, write `0.`, then `-k` zeroes, then the digits. When `0 < k < n`, write the first `k` digits, `.`, and the remaining digits. When `k >= n`, write the digits followed by `k - n` zeroes, with no decimal point.
6. **Scientific notation.** Write `d1`; if `n > 1`, write `.` and `d2...dn`. Then write `E`, the exponent sign (`+` or `-`, always present), and the absolute value of `k - 1` with at least two digits.

The layout differs from both JCS and ECMAScript, which use fixed notation for magnitudes from `1e-6` (inclusive) up to `1e21` (exclusive). Verifiers must implement the layout above rather than reuse a JavaScript or JCS number serializer; only the digit generation in step 3 can be shared.

| Value | Canonical v1 text |
| --- | --- |
| 0.25 | `0.25` |
| 1.0 | `1` |
| -1.5 | `-1.5` |
| 1.0 / 3.0 | `0.3333333333333333` |
| 0.0001 | `0.0001` |
| 0.00001 | `1E-05` |
| 1e14 | `100000000000000` |
| 1e15 | `1E+15` |
| 2^53 (9007199254740992) | `9007199254740992` |
| 12345678901234568 | `12345678901234568` |
| 123456789012345680 | `1.2345678901234568E+17` |
| 1e21 | `1E+21` |
| Largest finite value | `1.7976931348623157E+308` |
| Smallest subnormal value | `5E-324` |
| Negative zero | `-0` |

These values are locked by `CanonicalJsonV1InteroperabilityTests`. The only double among the built-in builder fields is the decision receipt and audit ledger record `riskScore`; host-supplied content may contain others.

## Arrays and string sets

The writer preserves array element order. Arrays are therefore ordered data unless a builder normalizes them first.

The built-in builders treat these arrays as unordered string sets: decision receipt and audit ledger record `reasonCodes`, and capability grant `scopes`. Before serialization a string set is normalized by:

1. removing null, empty, and white-space-only entries;
2. trimming leading and trailing white space from each remaining entry;
3. removing duplicates by ordinal comparison;
4. sorting ordinally.

Two sets that differ only in order, duplicates, blank entries, or surrounding white space therefore canonicalize identically.

## Timestamps

Builders convert `DateTimeOffset` values to UTC and format them as strings with exactly seven fractional-second digits and a literal `Z`, using the .NET custom format `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'` with the invariant culture. For example, 14:34:56.1234567 at UTC+02:00 on 22 September 2026 is emitted as `"2026-09-22T12:34:56.1234567Z"`. An absent optional timestamp is emitted as `null`.

The offset is not preserved: two timestamps that denote the same instant in different offsets canonicalize identically.

## Enumerations

Builders emit enumeration values as fixed protocol strings from `CanonicalEnumWireNames`, never as numeric values or as CLR `Enum.ToString()` output. Those strings are part of the v1 contract and are retained across public renames; for example, `GovernanceEmissionEventType.DecisionReceipt` is emitted as `"AuditResidue"`. An undefined enumeration value is rejected.

## Metadata filtering and normalization

Metadata dictionaries on decision receipts, audit ledger records, lifecycle events, emission envelopes and payloads, outbox entries, capability grants, and governed operation execution receipts pass through the same filter before hashing. Every built-in canonical payload builder, including `GovernedOperationExecutionReceiptCanonicalPayload`, uses this one implementation:

1. **Allow-list.** An entry is included only when its key, trimmed, is in `CanonicalPayloadOptions.MetadataKeyAllowList` by ordinal comparison. The default allow-list is empty, so with default options no metadata is hashed.
2. **Key trimming.** Included keys are trimmed of leading and trailing white space.
3. **Collision rejection.** If two included keys are identical after trimming, canonicalization fails with an `ArgumentException` instead of letting enumeration order choose a value.
4. **Value normalization.** Values are trimmed of leading and trailing white space. A runtime null value is converted to the empty string.
5. **Emission.** The result is emitted as an object whose property values are all strings, ordered as described above. The `metadata` property is always present; when no entry survives filtering it is the empty object `{}`.

"White space" means every character for which .NET `char.IsWhiteSpace` returns true, which is what `string.Trim()` removes: the Unicode `White_Space` characters, including U+0009 to U+000D, U+0020, U+0085, U+00A0, U+1680, U+2000 to U+200A, U+2028, U+2029, U+202F, U+205F, and U+3000. Zero-width characters such as U+200B and U+FEFF are not white space and are preserved.

Several artifact types also normalize their own metadata when the artifact is created, for example by dropping blank keys. That creation-time normalization changes the artifact before it is hashed; the rules above are what the canonical builder applies to whatever metadata the artifact carries.

### Equivalences by contract

The following distinctions are **intentionally not covered by a v1 signature**. Hosts and verifiers must treat them as semantically insignificant:

| Distinction | Canonical v1 treatment |
| --- | --- |
| Leading or trailing white space in an allow-listed metadata key | Insignificant. `" region "` and `"region"` are the same key. |
| Leading or trailing white space in a metadata value | Insignificant. `"  us-east  "` and `"us-east"` are the same value. |
| Runtime null, empty, and white-space-only metadata values | Equivalent. All are emitted as `""`. |
| Metadata keys outside the allow-list | Not covered. They can be added, removed, or changed without affecting the hash. |
| Order, duplicates, blank entries, and surrounding white space in string sets | Insignificant, as described under [Arrays and string sets](#arrays-and-string-sets). |
| The offset of a timestamp | Insignificant; only the UTC instant is covered. |

Interior white space, letter case, and every other character remain significant: `"us east"`, `"us  east"`, and `"US EAST"` hash differently.

A host that needs one of these distinctions to be authenticated must encode it in the value before signing, for example by storing a quoted or escaped form, rather than relying on the canonical format to preserve it. Changing any of these equivalences alters the bytes for existing artifacts, so it requires a new canonicalization version, not a change to v1.

## Golden interoperability vectors

The vectors below are the compatibility authority for v1. Each one is locked by tests in `AsiBackbone.Core.Tests` (`CanonicalPayloadJsonBranchTests` and `CanonicalJsonV1InteroperabilityTests`). The JSON is shown as ASCII text, so every backslash is a literal byte. Hashes are lowercase hexadecimal SHA-256 over the UTF-8 bytes.

### Vector 1: ordering and HTML-sensitive escaping

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

Canonical JSON:

```json
{"artifactId":"artifact-1","artifactType":"artifact-type","canonicalizationVersion":"asibackbone.canonical-json.v1","content":{"aNumber":0.25,"aString":"\u003C\u003E\u0026\u0027\u002B\u00E9","nested":{"alpha":1,"beta":2}},"payloadSchemaVersion":"schema-v1"}
```

Uppercase hexadecimal UTF-8 bytes:

```text
7B2261727469666163744964223A2261727469666163742D31222C22617274696661637454797065223A2261727469666163742D74797065222C2263616E6F6E6963616C697A6174696F6E56657273696F6E223A226173696261636B626F6E652E63616E6F6E6963616C2D6A736F6E2E7631222C22636F6E74656E74223A7B22614E756D626572223A302E32352C2261537472696E67223A225C75303033435C75303033455C75303032365C75303032375C75303032425C7530304539222C226E6573746564223A7B22616C706861223A312C2262657461223A327D7D2C227061796C6F6164536368656D6156657273696F6E223A22736368656D612D7631227D
```

### Vector 2: control characters, quotes, surrogate pairs, integers, and array order

Inputs, with descriptor values shown before trimming:

```text
artifactId: "  artifact-2  "
artifactType: "  artifact-type  "
canonicalizationVersion: asibackbone.canonical-json.v1
payloadSchemaVersion: " schema-v1 "
content:
  text: "tab" U+0009 "line" U+000A "quote" U+0022 "slash" U+005C "ctl" U+0001 "emoji" U+1F600
  items: ["b", "a", null]
  flag: false
  count: -7 (32-bit integer)
  big: 9007199254740993 (64-bit integer)
```

Canonical JSON:

```json
{"artifactId":"artifact-2","artifactType":"artifact-type","canonicalizationVersion":"asibackbone.canonical-json.v1","content":{"big":9007199254740993,"count":-7,"flag":false,"items":["b","a",null],"text":"tab\tline\nquote\u0022slash\\ctl\u0001emoji\uD83D\uDE00"},"payloadSchemaVersion":"schema-v1"}
```

Uppercase hexadecimal UTF-8 bytes:

```text
7B2261727469666163744964223A2261727469666163742D32222C22617274696661637454797065223A2261727469666163742D74797065222C2263616E6F6E6963616C697A6174696F6E56657273696F6E223A226173696261636B626F6E652E63616E6F6E6963616C2D6A736F6E2E7631222C22636F6E74656E74223A7B22626967223A393030373139393235343734303939332C22636F756E74223A2D372C22666C6167223A66616C73652C226974656D73223A5B2262222C2261222C6E756C6C5D2C2274657874223A227461625C746C696E655C6E71756F74655C7530303232736C6173685C5C63746C5C7530303031656D6F6A695C75443833445C7544453030227D2C227061796C6F6164536368656D6156657273696F6E223A22736368656D612D7631227D
```

SHA-256:

```text
33affa1ac1fd0c5571054c4fdb280611afcc15902fe66b8b37ab4ae1bb032b13
```

### Vector 3: decision receipt builder normalization

This vector exercises the builder layer through `CanonicalPayloadBuilder.ForDecisionReceipt` with the metadata allow-list `region`, `tier`, `blank`, `note`.

Inputs:

```text
eventId: receipt-1 (decision receipt identifier defaults to the event identifier)
schemaVersion: 1.0.0
occurredUtc: 2026-09-22T14:34:56.1234567+02:00
actorId: actor-1
actorType: Human
operationName: orders.approve
outcome: Allowed
correlationId: correlation-1
decisionLatencyMs: 42
riskScore: 0.5
policyVersion: policy-v1
reasonCodes: [" policy.b ", "policy.a", "policy.b", "", "   ", null]
metadata:
  " region ": "  us-east  "
  "tier": null
  "blank": "   "
  "note": "two  words"
  "ignored": "not-allow-listed"
all other optional fields: absent
```

Canonical JSON:

```json
{"artifactId":"receipt-1","artifactType":"asibackbone.audit-residue","canonicalizationVersion":"asibackbone.canonical-json.v1","content":{"actorDisplayName":null,"actorId":"actor-1","actorType":"Human","auditResidueId":"receipt-1","constraintCount":null,"constraintSetHash":null,"correlationId":"correlation-1","decisionLatencyMs":42,"decisionStage":null,"emitterProvider":null,"emitterStatus":null,"eventId":"receipt-1","gatewayExecutionId":null,"metadata":{"blank":"","note":"two  words","region":"us-east","tier":""},"occurredUtc":"2026-09-22T12:34:56.1234567Z","operationName":"orders.approve","organizationHash":null,"outboxSequence":null,"outcome":"Allowed","parentSpanId":null,"policyHash":null,"policyScope":null,"policyVersion":"policy-v1","reasonCodes":["policy.a","policy.b"],"riskScore":0.5,"schemaVersion":"1.0.0","spanId":null,"tenantHash":null,"traceId":null},"payloadSchemaVersion":"1.0.0"}
```

SHA-256:

```text
9636918eea3a4d7e121499e4def36588950427de5250739df5d4fe9c4e01a6b5
```

### Vector 4: governed operation execution receipt

This vector exercises `GovernedOperationExecutionReceiptCanonicalPayload.Create` with the metadata allow-list `safe`. `GovernedOperationExecutionReceipt.Create` trims metadata when the receipt is created, and the canonical filter then applies the rules above.

Inputs:

```text
operationExecutionId: operation-1
executionAttemptId: attempt-1 (artifact identifier is operationExecutionId:executionAttemptId)
persistenceOutcome: Committed
mutationBatchId: batch-1
mutationRecordCount: 2
mutationManifestHash: ABCDEF (the receipt stores it in lowercase)
mutationManifestAlgorithm: SHA-256
completedUtc: 2026-09-22T12:00:00Z
persistenceProvider: efcore
decisionAuditRecordId: record-1
metadata:
  " safe ": "  included  "
  "ignored": "x"
```

Canonical JSON:

```json
{"artifactId":"operation-1:attempt-1","artifactType":"asibackbone.governed-operation-execution-receipt","canonicalizationVersion":"asibackbone.canonical-json.v1","content":{"completedUtc":"2026-09-22T12:00:00.0000000Z","completedWithoutMutation":false,"decisionAuditRecordId":"record-1","executionAttemptId":"attempt-1","metadata":{"safe":"included"},"mutationBatchId":"batch-1","mutationManifestAlgorithm":"SHA-256","mutationManifestHash":"abcdef","mutationRecordCount":2,"operationExecutionId":"operation-1","persistenceOutcome":"Committed","persistenceProvider":"efcore"},"payloadSchemaVersion":"1.0.0"}
```

SHA-256:

```text
9f9614b15817b0728fdb7f81f311e4d1578a5173d1cd1d52d5ae3cacb2b15d11
```

## Hash selection

`CanonicalPayloadOptions.HashAlgorithm` is carried by the payload and used when `CanonicalPayloadHasher.ComputeHash(payload)` is called. The built-in hasher accepts `SHA256` or `SHA-256` and normalizes the descriptor to `SHA-256`; it accepts `SHA512` or `SHA-512` and normalizes the descriptor to `SHA-512`. Any other value produces `NotSupportedException` rather than silently falling back. Hash values are lowercase hexadecimal.

The hash algorithm is not part of the canonical JSON envelope. The same canonical bytes may therefore be hashed with a different algorithm, and verifiers must use the algorithm descriptor carried with the hash or signing metadata.

## Versioning rule

Bug fixes that do not alter canonical bytes may retain `asibackbone.canonical-json.v1`. Any change that can alter the bytes for an existing artifact requires a new canonicalization version and new golden vectors. That includes property ordering, escaping, number formatting (including the double layout rule), value support, null handling, UTF-8 encoding, timestamp formatting, enumeration wire strings, string-set normalization, and the metadata filtering and normalization rules above.

## Related documentation

- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Cryptographic Security Posture](cryptographic-security-posture.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Production Managed-Key Integration Guide](production-managed-key-integration.md)
