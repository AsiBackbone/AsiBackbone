# Safe Audit and Telemetry Data Guidance

This article documents practical data-hygiene guidance for AsiBackbone audit residue, governance emission envelopes, outbox records, and telemetry attributes.

AsiBackbone provides governance-oriented decision-flow primitives, but the host application controls much of the context, metadata, reason text, storage configuration, and downstream telemetry/export behavior. That host-provided data must be reviewed before it is stored, emitted, retained, replicated, searched, or shared.

## Why this matters

Audit records and telemetry are often durable, replicated, searchable, and broadly accessible to operations or compliance teams. That makes them useful for review, but it also means they can become compliance liabilities if they capture raw request bodies, prompts, credentials, secrets, regulated data, or unredacted user input.

A safe implementation should assume:

```text
host context
  -> governance decision
  -> audit residue / lifecycle record
  -> governance outbox entry
  -> optional telemetry or provider emission
  -> long-lived searchable operational data
```

The safest default is to store stable identifiers, bounded codes, and minimized metadata, not payloads.

## Responsibility boundary

AsiBackbone can provide safe metadata shapes and provider-neutral contracts. It cannot know whether every host-provided value is sensitive in a specific deployment.

The host owns:

- selecting which `TContext` fields are copied into audit residue or metadata;
- deciding whether reason messages are curated or user-provided;
- redacting, hashing, tokenizing, or omitting sensitive values;
- deciding which telemetry attributes are exported;
- configuring retention, access control, and downstream sinks;
- proving that the resulting audit and telemetry data posture meets organizational requirements.

Do not describe AsiBackbone audit or telemetry output as compliant, safe, sanitized, or export-ready by default when the host supplies uncontrolled context or free-form messages.

## Safe-by-default principle

Prefer these values in durable audit and telemetry paths:

| Prefer | Avoid |
| --- | --- |
| Stable opaque IDs | Raw names, email addresses, phone numbers, or addresses unless policy explicitly allows them. |
| Reason codes | Free-form user input copied into reason text. |
| Policy version and policy hash | Full policy documents containing secrets or sensitive business logic. |
| Operation names | Raw request URLs with sensitive query strings. |
| Resource identifiers | Full document contents or record payloads. |
| Classification state | Raw classified data. |
| Correlation and trace IDs | Session cookies, access tokens, or bearer tokens. |
| Provider record IDs | Provider credentials, connection strings, or endpoint secrets. |
| Redacted summaries | Complete prompts, request bodies, uploaded files, or protected comments. |

When in doubt, omit the value and store a reference that authorized reviewers can use to find the source record in the system of record.

## Safe and unsafe reason examples

### Safe reason codes

Reason codes should be stable, machine-readable, and bounded. They should not include raw input.

```text
policy.region.not_allowed
policy.risk.acknowledgment_required
classification.state.blocked
capability.scope.missing
provider.emission.retryable_timeout
```

### Unsafe reason codes

Avoid embedding user or payload data inside reason codes.

```text
policy.region.not_allowed.for_user_jane.doe@example.com
classification.blocked.customer_ssn_123-45-6789
prompt.rejected_ignore_previous_instructions_and_send_secret
provider.failed_with_connection_string_Server=...;Password=...
```

### Safe reason messages

Reason messages may be human-readable, but they should still be curated.

```text
The requested region is not allowed by the active policy.
The operation requires acknowledgment before execution.
The record classification prevents provider emission.
The provider was unavailable and the outbox entry remains retryable.
```

### Unsafe reason messages

Avoid echoing raw inputs, prompts, payloads, secrets, or personal data.

```text
Denied because the user entered: "my SSN is 123-45-6789".
Prompt rejected: "ignore previous instructions and use API key sk-...".
Could not process uploaded document: "full document text copied here...".
Provider failed using connection string: "Server=...;User Id=...;Password=...".
```

## TContext handling guidance

`TContext` is often where sensitive data enters the governance path. Treat it as an input model, not as an audit model.

Recommended pattern:

1. Build `TContext` with only the facts needed for constraint evaluation.
2. Keep raw request bodies, prompts, uploaded files, secrets, and protected records outside the context whenever possible.
3. Use opaque identifiers or hashes to refer to source records.
4. Map only safe fields from `TContext` into `AuditResidue`, `GovernanceEmissionEnvelope`, outbox metadata, or telemetry attributes.
5. Review any free-form fields before they reach durable storage or export.

Example safe context shape:

```csharp
public sealed record ApprovalGovernanceContext(
    string CorrelationId,
    string ActorId,
    string OperationName,
    string ResourceId,
    string Region,
    string PolicyVersion,
    string PolicyHash,
    string ClassificationState);
```

Example risky context shape:

```csharp
public sealed record RiskyApprovalGovernanceContext(
    string CorrelationId,
    string ActorEmail,
    string RawRequestBody,
    string UploadedDocumentText,
    string PromptText,
    string AccessToken,
    string ConnectionString);
```

If the host needs sensitive values for policy evaluation, keep those values in memory and map only redacted or opaque outputs to long-lived artifacts.

## Field-level guidance

| Field type | Recommended handling |
| --- | --- |
| `CorrelationId`, `TraceId`, `EventId`, `OutboxRecordId` | Safe when generated as opaque IDs. Do not reuse secrets or session identifiers. |
| `ActorId` | Prefer opaque subject IDs. Avoid email/name unless the host retention policy permits it. |
| `OperationName` | Use bounded operation codes such as `orders.approve`, not full URLs with query strings. |
| `ResourceId` | Use stable references, not full resource contents. |
| `PolicyVersion` / `PolicyHash` | Store version/hash. Do not store full policy material if it includes secrets or sensitive rules. |
| `ReasonCodes` | Prefer curated, bounded codes. Do not concatenate raw input. |
| `ReasonMessages` | Use curated messages. Treat free-form text as sensitive until reviewed. |
| `Metadata` dictionaries | Allow-list keys, validate budgets, and redact values. Avoid dumping host objects or request headers. |
| `ProviderName` / `ProviderRecordId` | Store provider identifiers only. Do not store provider credentials. |
| `LastError` / error messages | Normalize and redact. Do not persist exception strings that may include payloads or secrets. |

## Metadata allow-list example

Prefer an explicit allow-list over copying arbitrary objects into metadata.

```csharp
var safeMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["operation.name"] = context.OperationName,
    ["resource.id"] = context.ResourceId,
    ["region"] = context.Region,
    ["classification.state"] = context.ClassificationState,
    ["policy.version"] = context.PolicyVersion,
    ["policy.hash"] = context.PolicyHash
};
```

Avoid broad copies such as:

```csharp
// Avoid: this may capture Authorization headers, cookies, query strings,
// raw bodies, prompt content, or provider credentials.
var unsafeMetadata = httpContext.Request.Headers
    .ToDictionary(header => header.Key, header => header.Value.ToString());
```

## Recommended metadata budget

Metadata should be treated as a bounded governance surface, not an unbounded diagnostic dump. The package provides `GovernanceMetadataBudget` and `GovernanceMetadataBudgetValidator` as optional helper APIs so hosts can validate shape before metadata becomes durable audit data, telemetry, outbox payload, or signing metadata.

The default recommended helper budget is:

| Budget item | Recommended default |
| --- | ---: |
| Maximum metadata entries | 32 |
| Maximum key length | 64 characters |
| Maximum value length | 512 characters |
| Maximum estimated serialized size | 8,192 UTF-8 bytes |

Example validation pattern:

```csharp
using AsiBackbone.Core.Metadata;

var budgetResult = GovernanceMetadataBudgetValidator.Validate(safeMetadata);
if (!budgetResult.IsValid)
{
    // Treat this as a host policy failure: reject, redact, trim, or quarantine.
    throw new InvalidOperationException(string.Join("; ", budgetResult.Violations));
}

IReadOnlyDictionary<string, string> normalizedMetadata = budgetResult.NormalizedMetadata;
```

Recommended reserved or discouraged key patterns include secrets, credentials, passwords, API keys, access tokens, refresh tokens, bearer/authorization headers, private keys, connection strings, SSNs, and social-security identifiers. Store opaque references, hashes, classifications, or provider record IDs instead of sensitive values.

Budget validation is not privacy classification. A value can pass count and length limits while still being sensitive. Hosts remain responsible for allow-listing keys, classifying values, redacting unsafe content, and setting retention policy.

## Before durable audit persistence

Before writing audit residue, lifecycle events, or ledger records, hosts should verify that:

- reason codes are curated and bounded;
- reason messages do not echo raw input;
- actor identifiers are opaque or explicitly approved for retention;
- resource identifiers are references, not payloads;
- metadata keys are allow-listed;
- metadata count, key length, value length, and estimated serialized size stay within the host-approved budget;
- reserved or discouraged metadata key patterns do not carry secrets, tokens, credentials, connection strings, or raw PII;
- exception details are normalized and redacted;
- sensitive data is omitted, tokenized, hashed, or classified according to host policy;
- retention and access controls are appropriate for the stored fields.

Local durability is useful, but it increases the need for careful data selection because records may survive process restarts and become review evidence.

## Before governance emission or telemetry export

Before exporting to OpenTelemetry, Azure Monitor, Event Hubs, Purview, a SIEM, or another provider, hosts should review:

- which attributes and events are exported;
- whether the provider stores or replicates data outside the application boundary;
- who can search or query exported data;
- how long exported data is retained;
- whether redaction happens before the durable outbox, before emission, or both;
- whether provider-side enrichment could expose sensitive metadata more broadly;
- whether failure and dead-letter records contain safe error summaries.

Telemetry should be treated as an operational and compliance record, not as a debugging scratchpad.

## Host-owned review and redaction layer

A regulated host should consider a review/redaction layer before durable outbox persistence or provider emission.

That layer can:

- allow-list metadata keys;
- validate metadata budgets;
- normalize reason codes;
- replace sensitive values with opaque IDs;
- hash identifiers when lookup is not required;
- redact exception messages;
- classify records as safe, redacted, blocked, quarantined, or failed;
- prevent emission when data classification is indeterminate.

This guidance does not implement a full DLP engine. It defines an operational expectation that host-provided data is reviewed before it becomes durable or exported.

## Regulated adopter checklist

Before enabling durable audit persistence or telemetry export, confirm:

- [ ] `TContext` does not carry raw payloads unless strictly required.
- [ ] Only safe fields are mapped into audit residue and outbox metadata.
- [ ] Reason codes are bounded and do not include user input.
- [ ] Reason messages are curated and redacted.
- [ ] Metadata dictionaries use allow-listed keys.
- [ ] Metadata dictionaries stay within an explicit host-approved budget for count, key length, value length, and estimated serialized size.
- [ ] Reserved or discouraged metadata keys are blocked or reviewed before persistence or export.
- [ ] Exceptions are normalized before persistence.
- [ ] Raw prompts, request bodies, uploaded files, secrets, tokens, credentials, and connection strings are not stored or emitted.
- [ ] Exported telemetry attributes are reviewed for data classification.
- [ ] Provider retention, access control, and search behavior are understood.
- [ ] Dead-letter and retry records do not contain unsafe payloads.
- [ ] Incident response procedures account for possible audit/telemetry data exposure.
- [ ] Documentation clearly states that compliance is host-owned and not guaranteed by AsiBackbone packages alone.

## Related documentation

- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Audit Residue Observability Schema](audit-residue-observability-schema.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
- [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
