# Governance Emission Contract

This article documents the provider-neutral governance emission contract introduced for the `1.1.0 - Observability, Outbox, and Governance Emission Providers` milestone.

AsiBackbone remains a policy decision pipeline for consequential software decision flow. It is not an AI model host, cloud governance platform, observability backend, SIEM product, streaming system, signing product, or provider SDK wrapper.

## Purpose

The governance emission contract is the seam between Core governance artifacts, durable local audit/outbox persistence, and optional downstream providers.

```text
Core governance artifacts
  -> GovernanceEmissionEnvelope
  -> IGovernanceEmitter
  -> GovernanceEmissionResult
  -> durable outbox or optional provider adapter
```

The goal is to define the handoff shape before implementing concrete providers such as OpenTelemetry, Azure Monitor, Event Hubs, Purview, SIEM, file, database, or future integrations.

## Core-neutral placement

The contract lives in `AsiBackbone.Core` under the provider-neutral emission language.

Core owns:

* `IGovernanceEmitter`
* `GovernanceEmissionEnvelope`
* `GovernanceEmissionPayload`
* `GovernanceEmissionResult`
* `GovernanceEmissionStatus`
* `GovernanceEmissionError`
* `GovernanceEmissionEventType`

Core does not own:

* OpenTelemetry exporter configuration;
* Azure Monitor, Log Analytics, Event Hubs, or Purview SDK dependencies;
* SIEM-specific payload formats;
* provider-specific retry clients;
* cloud workspace, subscription, tenant, resource group, namespace, topic, table, or catalog identifiers;
* signing-provider implementation or key-management logic.

Provider packages and host-owned adapters depend on Core. Core must not depend on provider packages.

## Contract roles

| Type | Role |
| --- | --- |
| `IGovernanceEmitter` | Provider-neutral async emission boundary. Durable outbox workers and provider adapters can target this interface. |
| `GovernanceEmissionEnvelope` | Versioned neutral envelope carrying event identity, correlation, decision receipt ID, lifecycle stage, policy version/hash, trace fields, gateway/outbox hints, payload descriptor, and safe metadata. |
| `GovernanceEmissionPayload` | Minimized payload descriptor. It captures payload type, schema version, content type, content hash, size, and safe metadata without requiring raw protected content. |
| `GovernanceEmissionResult` | Provider-neutral result shape for delivered, pending, deferred, failed, retryable, and dead-letter outcomes. |
| `GovernanceEmissionStatus` | Stable status vocabulary for local/outbox/provider handoff. |
| `GovernanceEmissionError` | Provider-neutral error code, message, retryability, provider name, and safe provider error code. |
| `GovernanceEmissionEventType` | Stable event category vocabulary for decision, acknowledgment, capability token, gateway, decision receipt, lifecycle, outbox, and provider emission events. |

## Envelope guidance

`GovernanceEmissionEnvelope` should be treated as a minimized transport contract, not as the authoritative audit store.

The envelope can carry:

* `EnvelopeId`
* `SchemaVersion`
* `EventType`
* `EventId`
* `OccurredUtc` / `CreatedUtc`
* `CorrelationId`
* `AuditResidueId`
* `LifecycleStage` / `LifecycleStageSequence`
* `PolicyVersion` / `PolicyHash`
* `TraceId`, `SpanId`, `ParentSpanId`
* `OperationName`, `Outcome`, and opaque `ActorId`
* `EmitterStatus`, `EmitterProvider`, and `OutboxSequence`
* `GatewayExecutionId` and `DecisionStage`
* optional minimized `GovernanceEmissionPayload`
* provider-neutral metadata

Provider adapters may map these fields into backend-specific records, spans, logs, events, streams, or catalog metadata, but the neutral envelope should remain backend-independent.

## Result and failure behavior

`GovernanceEmissionResult` supports these neutral outcomes:

| Status | Meaning | Typical next step |
| --- | --- | --- |
| `Pending` | Accepted locally or staged for future emission. | Persist or continue outbox processing. |
| `Delivered` | Emission succeeded. | Record provider-side identifier when available. |
| `Deferred` | Emission intentionally delayed. | Retry after a host-defined delay or review. |
| `Failed` | Emission failed and retryability is not implied by status. | Apply host policy. |
| `RetryableFailure` | Failure is expected to be retryable. | Retry with backoff and failure counters. |
| `DeadLettered` | Terminal failure or policy quarantine. | Stop automatic retry and preserve diagnostics. |

Provider-specific exception types, HTTP codes, SDK error objects, and backend payloads should be normalized into `GovernanceEmissionError` before crossing the Core boundary.

## Relationship to related issues

| Issue | Relationship |
| --- | --- |
| #140 Durable outbox | The outbox can store `GovernanceEmissionEnvelope` values and hand them to `IGovernanceEmitter` when ready. |
| #141 Lifecycle stages | `GovernanceEmissionEnvelope` can carry `DecisionReceiptLifecycleStage` and stable stage sequence values. |
| #142 Decision receipt telemetry | `GovernanceEmissionEnvelope.FromResidue` copies neutral telemetry, trace, outbox, gateway, and policy fields from decision receipt. |
| #144 OpenTelemetry provider | The OpenTelemetry provider should adapt this contract into spans, events, logs, metrics, and attributes without changing Core semantics. |
| #145 Event Hubs provider | The Event Hubs provider should adapt this contract into versioned stream messages with stable message properties, outbox-safe retry behavior, and no Core Azure dependency. |
| #146 Purview enrichment | Purview enrichment should consume minimized envelopes or summaries as optional catalog, classification, lineage, and compliance context without becoming the raw audit store. |
| #147 Signing-ready receipts | Signing-ready metadata can be carried by audit receipts and downstream emissions without requiring Core to depend on a concrete signing provider. |
| #149 Observability architecture docs | This contract is the first implementation seam described by the observability and governance emission architecture direction. |

## Privacy and minimization rules

Emission envelopes should not contain:

* raw capability tokens;
* secrets, connection strings, API keys, or credentials;
* raw prompts, documents, payload bodies, protected records, or sensitive content;
* raw personal data unless host policy explicitly permits it;
* provider workspace secrets or deployment topology details;
* unredacted DLP/classification failure payloads.

Use opaque IDs, hashes, coarse policy scopes, controlled stage/status values, and minimized metadata. When provider emission is blocked by DLP or classification policy, preserve the local governance record and return or persist a provider-neutral failure result.

## Provider implementation sequence

Recommended implementation sequence:

1. Core defines this neutral contract.
2. Durable outbox stores envelopes and status/result state.
3. Optional providers adapt envelopes into backend-specific payloads.
4. Provider failures normalize back into `GovernanceEmissionResult` and `GovernanceEmissionError`.
5. Host policy decides retry, defer, quarantine, dead-letter, or escalation behavior.

This keeps AsiBackbone from starting at a vendor provider and working backward into Core.

## Related documentation

- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Decision Receipt Observability Schema](decision-receipt-observability-schema.md)
- [Core Domain Language](core-domain-language.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
