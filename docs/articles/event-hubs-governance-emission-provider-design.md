# Design-Only: Event Hubs Governance Emission Provider

This article documents a **design-only** future provider strategy for an optional Event Hubs governance emission provider. It is retained as planning guidance after the released `1.1.0 - Observability, Outbox, Signing, and Governance Emission Providers` package-family boundary.

Issue: #145.

AsiBackbone is a governance spine for consequential software decision flow. It is not a streaming platform, SIEM product, cloud governance platform, signing product, or compliance guarantee by itself.

> [!IMPORTANT]
> This page does **not** document a released NuGet package. No Event Hubs provider package, Azure Event Hubs SDK adapter, Azure-specific emission package, or Event Hubs publishing implementation is included in the `1.1.0` stable package family.
>
> `AsiBackbone.OpenTelemetry` is the only concrete released governance emission provider package in `1.1.0`. Event Hubs remains a future/design-only provider direction unless a later release separately reviews and ships it.
>
> See [1.1.0 Release Notes - Accepted deferrals](release-notes-110.md#accepted-deferrals) for the current release boundary.

> [!NOTE]
> Event Hubs is a downstream streaming boundary. It must not replace durable local decision receipt, durable outbox persistence, policy evaluation, acknowledgment, capability-token, or gateway records.

## Purpose

The Event Hubs provider design gives hosts a possible future Azure streaming path for replayable governance event delivery to downstream monitoring, compliance, lineage, SIEM, enrichment, or analytics consumers.

A future provider should adapt provider-neutral governance emission envelopes into Event Hubs messages without making `AsiBackbone.Core` depend on Azure SDKs, Azure resource concepts, Event Hubs namespaces, Purview, SIEM products, or provider-specific retry clients.

```text
Decision receipt / lifecycle event / gateway result
  -> GovernanceEmissionEnvelope
  -> durable outbox
  -> future Event Hubs governance emitter
  -> Azure Event Hubs namespace / event hub
  -> downstream monitoring, compliance, lineage, SIEM, enrichment, or analytics consumers
```

The durable local audit and outbox records remain the reliability and accountability baseline. Event Hubs is the replayable stream, not the authoritative audit ledger.

## Package boundary

Candidate provider package name:

```text
AsiBackbone.Streaming.EventHubs
```

Acceptable alternative package names if the package family later groups Azure integrations differently:

```text
AsiBackbone.AzureEventHubs
AsiBackbone.Azure.EventHubs
AsiBackbone.Observability.EventHubs
```

These names are planning candidates only. They are not part of the released `1.1.0` package list.

A future package should depend on:

* `AsiBackbone.Core`;
* Event Hubs client abstractions needed to publish to Azure Event Hubs;
* Azure Identity support when Managed Identity or token credentials are enabled;
* `Microsoft.Extensions.Options` for provider configuration;
* `Microsoft.Extensions.Logging.Abstractions` for safe provider diagnostics if needed.

The future package should not depend on:

* OpenTelemetry exporters;
* Azure Monitor, Application Insights, Log Analytics, Purview, Sentinel, or SIEM SDKs;
* host-specific storage providers;
* signing or immutable-storage providers;
* robotics or AI model packages.

Downstream consumers such as Purview enrichment jobs, SIEM processors, or analytics workers should subscribe to the stream outside this provider. The Event Hubs provider should only publish minimized governance envelopes to the configured event hub.

## Provider placement

Core already owns the provider-neutral contracts:

* `IGovernanceEmitter`
* `GovernanceEmissionEnvelope`
* `GovernanceEmissionPayload`
* `GovernanceEmissionResult`
* `GovernanceEmissionStatus`
* `GovernanceEmissionError`
* `GovernanceEmissionEventType`
* `IGovernanceOutboxStore`
* `GovernanceOutboxDrain`

A future Event Hubs provider should implement the neutral emitter seam:

```text
IGovernanceEmitter
  -> EventHubsGovernanceEmitter
```

The outbox drain should remain provider-neutral. It should not know that Event Hubs is the downstream emitter.

## Event envelope

The Event Hubs message body should contain the serialized `GovernanceEmissionEnvelope` or a stable Event Hubs projection of that envelope.

Recommended envelope-level fields:

| Field | Purpose |
| --- | --- |
| `EnvelopeId` | Stable envelope identifier for idempotency and diagnostics. |
| `SchemaVersion` | Stable envelope schema version. |
| `Source` | Package or host source name, such as `AsiBackbone`. |
| `SourceVersion` | AsiBackbone or provider package version when known. |
| `EventType` | Controlled governance event type. |
| `EventId` | Stable governance event identifier. |
| `OccurredUtc` / `CreatedUtc` | Event occurrence and envelope creation timestamps. |
| `CorrelationId` | Host workflow or request join key. |
| `AuditResidueId` | Opaque identifier for the durable decision receipt record. |
| `LifecycleStage` / `LifecycleStageSequence` | Lifecycle stage and stable sequence when available. |
| `PolicyVersion` / `PolicyHash` | Policy version and hash that shaped the decision. |
| `TraceId`, `SpanId`, `ParentSpanId` | Distributed tracing join fields when supplied by the host. |
| `OperationName`, `Outcome`, `ActorId` | Minimized operation and actor context. |
| `EmitterStatus`, `EmitterProvider`, `OutboxSequence` | Provider and outbox status hints. |
| `GatewayExecutionId`, `DecisionStage` | Gateway and decision-boundary correlation fields. |
| `Payload` | Optional minimized payload descriptor, not raw protected content. |
| `Metadata` | Provider-neutral safe metadata. |

Recommended envelope schema identity:

```text
SchemaVersion: 1.0
ContentType: application/vnd.AsiBackbone.governance-emission+json;v=1
Source: AsiBackbone
EmitterProvider: azure-event-hubs
```

The first implementation should keep the envelope shape stable and versioned. Breaking envelope changes should require a new schema version and compatibility guidance for downstream consumers.

## Event Hubs message mapping

A future provider should map stable envelope fields to Event Hubs message properties so downstream processors can route and correlate without parsing the full message body first.

| Event Hubs field/property | Source | Guidance |
| --- | --- | --- |
| `MessageId` | `EventId` or `EnvelopeId` | Prefer stable event identity for idempotency. |
| `CorrelationId` | `CorrelationId` | Preserve host workflow correlation. |
| `ContentType` | envelope content type | Use the versioned content type. |
| `Properties["asibackbone.event_type"]` | `EventType` | Controlled event type only. |
| `Properties["asibackbone.schema_version"]` | `SchemaVersion` | Safe schema identity. |
| `Properties["asibackbone.audit_residue_id"]` | `AuditResidueId` | Opaque decision receipt identifier. |
| `Properties["asibackbone.envelope_id"]` | `EnvelopeId` | Opaque envelope identifier. |
| `Properties["asibackbone.policy.version"]` | `PolicyVersion` | Stable policy version. |
| `Properties["asibackbone.policy.hash"]` | `PolicyHash` | Hash only; never raw policy content. |
| `Properties["asibackbone.lifecycle.stage"]` | `LifecycleStage` | Controlled lifecycle stage. |
| `Properties["asibackbone.gateway.execution_id"]` | `GatewayExecutionId` | Opaque gateway execution identifier. |
| `Properties["asibackbone.emitter.provider"]` | provider name | Use `azure-event-hubs`. |
| `Properties["asibackbone.outbox.sequence"]` | `OutboxSequence` | Include only when available. |

Do not place raw payload bodies, raw prompts, personal data, secrets, tokens, or protected records into message properties.

## Partitioning guidance

Partitioning should favor stable, low-sensitivity, operationally useful keys.

Candidate partition keys:

| Partition key | When appropriate | Notes |
| --- | --- | --- |
| Tenant or region code | Multi-tenant or regional hosts. | Use coarse, non-sensitive values. |
| Operation group | Workflow-focused stream processing. | Avoid high-cardinality operation instances. |
| Event type | Consumers process categories independently. | May create uneven partitions if one event type dominates. |
| Correlation group | Hosts need workflow-local ordering. | Use a coarse derived key rather than raw identifiers when possible. |

Avoid actor IDs, raw user IDs, decision receipt IDs, event IDs, envelope IDs, trace IDs, capability-token IDs, and raw resource IDs as default partition keys unless host policy explicitly permits the cardinality and privacy tradeoff.

## Managed Identity and configuration

A future provider should support Managed Identity or Azure token credentials where appropriate. Connection-string configuration may be useful for local development or legacy hosts, but Managed Identity should be the preferred production path when the hosting environment supports it.

Recommended options shape:

```text
EventHubsGovernanceEmitterOptions
  NamespaceFullyQualifiedName
  EventHubName
  ConnectionString               optional; development or legacy path
  CredentialMode                 ManagedIdentity | DefaultAzureCredential | ConnectionString | Custom
  ManagedIdentityClientId        optional user-assigned identity hint
  ProducerIdentifier             optional source identifier
  ContentType
  SchemaVersion
  Source
  SourceVersion
  PartitionKeyStrategy
  MaxBatchSize
  SendTimeout
  IncludeMessageProperties
  ValidateMinimizedPayload
```

Configuration should not require Core to understand Azure tenants, subscriptions, resource groups, namespaces, or Event Hubs SDK types.

## Emission sequence

The provider should be downstream of durable outbox persistence.

Recommended host sequence:

1. Evaluate policy and produce the neutral decision/decision receipt.
2. Persist decision receipt or lifecycle event locally.
3. Build a `GovernanceEmissionEnvelope`.
4. Enqueue the envelope in `IGovernanceOutboxStore`.
5. Drain the outbox through `GovernanceOutboxDrain` using the future `EventHubsGovernanceEmitter`.
6. The emitter serializes the envelope, maps safe message properties, and sends to Event Hubs.
7. The emitter returns `GovernanceEmissionResult.Delivered` when Event Hubs accepts the event or batch.
8. The outbox marks the entry delivered, deferred, failed, retryable, or dead-lettered according to the result.

If a host bypasses the outbox by calling the emitter directly, documentation should describe that as an advanced host-owned choice rather than the recommended accountability path.

## Result behavior

The provider should return provider-neutral results.

| Condition | Recommended result |
| --- | --- |
| Event accepted by Event Hubs | `Delivered` with provider `azure-event-hubs` and provider message/batch identifier when safely available. |
| Event is intentionally skipped by provider options | `Deferred` or `Delivered` depending on whether the host considers skip a successful no-op. |
| Payload blocked by minimization or DLP rule | `DeadLettered` or `Failed` with `emission.blocked` according to host policy. |
| Event Hubs namespace, event hub, identity, or network is unavailable | `RetryableFailure` when a later attempt may succeed. |
| Event Hubs rejects the event as too large or invalid | `Failed` or `DeadLettered` depending on whether the event can be repaired. |
| Serialization fails because the envelope is invalid | `Failed` or `DeadLettered` with a normalized safe error code. |
| Cancellation requested | Throw `OperationCanceledException`; the outbox drain preserves cancellation semantics. |

Provider-specific exceptions, SDK error objects, HTTP/AMQP details, and backend payloads should be normalized into `GovernanceEmissionError` before crossing the Core boundary.

## Retry and dead-letter behavior

The provider should not own the durable retry policy by itself. Durable retry state belongs to the outbox or host-owned worker.

Recommended split:

| Layer | Responsibility |
| --- | --- |
| Event Hubs provider | Attempt send, normalize result/error, preserve cancellation, avoid unsafe payload expansion. |
| Outbox drain | Interpret result, increment attempt counters, schedule retry, defer, or dead-letter. |
| Host policy | Decide max attempts, backoff, quarantine, alerting, and manual review. |

Failed Event Hubs emission must not lose local audit records. The local decision receipt and outbox entry should remain available for replay, investigation, or manual remediation.

## Privacy and minimization

The provider must not put these values into Event Hubs message bodies, properties, partition keys, or logs by default:

* raw capability tokens;
* secrets, connection strings, API keys, credentials, signing keys, or managed identity details;
* raw prompts, documents, request bodies, protected records, payload bodies, or user-submitted content;
* raw personal data unless a host explicitly opts into that behavior;
* provider namespace secrets or deployment-sensitive topology;
* raw DLP or classification failure payloads.

Use opaque IDs, hashes, coarse policy scopes, controlled status values, numeric counts, minimized payload descriptors, and safe metadata. When in doubt, preserve the full governance record locally and emit only a minimized envelope.

## Purview and downstream consumers

Purview is one possible downstream consumer or enrichment target. It should not be the sole reason for this provider.

Possible downstream consumers include:

* Azure Functions or worker services that enrich governance events;
* SIEM pipelines;
* monitoring and alerting processors;
* compliance review queues;
* lineage or catalog enrichment jobs;
* anomaly detection and trend analysis jobs;
* archival or lakehouse ingestion pipelines.

Downstream consumers should treat the Event Hubs event as a minimized transport event. They should join back to durable local records only through authorized, host-owned workflows.

## Security and operational guardrails

Recommended guardrails:

* prefer Managed Identity or token credentials in production;
* do not log connection strings, credentials, namespace secrets, or raw SDK payloads;
* keep batch size and payload size configurable;
* validate envelope schema version before send;
* enforce content-type and schema-version properties;
* support deterministic serialization for tests and replay comparisons;
* support low-cardinality provider diagnostics;
* expose safe counters for delivered, failed, retryable, deferred, and dead-lettered sends if metrics are later added;
* document that Event Hubs retention and replay are streaming features, not durable local audit guarantees.

## Test seams

The design should be testable without live Azure resources.

Recommended tests:

* envelope serialization produces stable JSON for schema version `1.0`;
* message mapping preserves `CorrelationId`, `AuditResidueId`, `EventId`, `EnvelopeId`, `SchemaVersion`, `EventType`, `PolicyVersion`, `PolicyHash`, lifecycle stage, gateway ID, outcome, and outbox sequence when present;
* content type is versioned and stable;
* message properties do not contain raw tokens, secrets, payload bodies, or protected content;
* partition-key strategy avoids high-cardinality sensitive defaults;
* provider returns delivered result when a fake Event Hubs producer accepts the message;
* provider normalizes send failure into provider-neutral result/error details;
* provider respects cancellation;
* Core has no Azure SDK dependency;
* provider package has no Purview, Azure Monitor, OpenTelemetry exporter, SIEM, robotics, or AI model dependencies;
* outbox drain can use the Event Hubs emitter through `IGovernanceEmitter` without knowing about Event Hubs types.

Live Event Hubs integration tests should be optional, explicitly configured, and excluded from default CI.

## Implementation checklist

Before implementation begins, confirm:

* package name and namespace;
* Azure SDK dependency boundary;
* options shape and Managed Identity defaults;
* event envelope schema version and content type;
* deterministic JSON serialization settings;
* message property names;
* partition-key strategy defaults;
* batch behavior and payload-size guardrails;
* failure mapping and retryability rules;
* outbox interaction expectations;
* minimization/DLP failure behavior;
* tests that require no live Azure resources.

## Relationship to related issues

| Issue | Relationship |
| --- | --- |
| #140 Durable outbox | Event Hubs emission should happen after local durable outbox persistence. |
| #141 Lifecycle stages | Lifecycle stage and sequence should be present in the envelope and safe message properties. |
| #142 Decision receipt telemetry | Trace, latency, gateway, outbox, and PII-safe identifiers provide provider mapping fields. |
| #144 OpenTelemetry provider | OpenTelemetry is the released `1.1.0` governance emission provider; Event Hubs remains a future Azure streaming provider design. |
| #149 Observability architecture | This provider design follows the Core-neutral provider package architecture. |
| #187 Governance emission contract | The provider implements the neutral `IGovernanceEmitter` seam. |
| #193 No-op outbox drain | The no-op proof path validates the drain sequence before this real provider is added. |

## Related documentation

- [1.1.0 Release Notes - Accepted deferrals](release-notes-110.md#accepted-deferrals)
- [Released: OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [Decision Receipt Observability Schema](decision-receipt-observability-schema.md)
- [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)

## Non-goals

This design-only provider page does not implement:

* Event Hubs publishing code;
* an Event Hubs NuGet package in the `1.1.0` stable package family;
* Event Hubs namespace or event hub provisioning;
* Azure Monitor direct emission;
* OpenTelemetry export configuration;
* Purview classification or lineage enrichment;
* Sentinel, Splunk, Elastic, or other SIEM-specific payloads;
* signing, timestamping, immutable storage, or tamper-evidence;
* replacement of durable local audit/outbox persistence;
* legal, compliance, retention, or data-residency guarantees.

Event Hubs should remain an optional future streaming adapter for host-owned governance emission pipelines unless a later release separately implements and publishes it.
