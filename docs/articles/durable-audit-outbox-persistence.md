# Durable Audit and Outbox Persistence

This article documents the provider-neutral durable persistence seam for decision receipt, lifecycle events, and governance emission outbox entries.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone remains a governance spine for consequential software decision flow. It is not an AI model host, observability backend, SIEM product, cloud provider, or completed ASI implementation.

## Purpose

External telemetry and governance sinks should be downstream delivery targets, not the first system of record.

The durable persistence seam preserves a local accountability artifact before any optional provider attempts delivery.

```text
Governed decision
  -> DecisionReceipt / DecisionReceiptLifecycleEvent
  -> local audit/lifecycle store
  -> GovernanceEmissionEnvelope
  -> governance outbox
  -> optional provider emitter
```

If provider delivery fails, the local audit or lifecycle record remains available for review, retry, escalation, or dead-letter handling.

## Core-neutral contracts

Core defines provider-neutral contracts and models only:

| Type | Purpose |
| --- | --- |
| `IGovernanceAuditLedgerStore` | Existing neutral audit ledger abstraction for `AuditLedgerRecord` records. |
| `IDecisionReceiptLifecycleStore` | Neutral lifecycle event store for append-only decision receipt lifecycle events. |
| `IGovernanceOutboxStore` | Neutral outbox store for pending governance emission envelopes. |
| `GovernanceOutboxEntry` | Neutral outbox entry state model with status, retry count, last error, next retry time, provider identifiers, and dead-letter reason. |
| `GovernanceOutboxDrain` | Provider-neutral drain path that hands pending or retry-ready outbox entries to an `IGovernanceEmitter` and persists the resulting state transition. |
| `NoOpGovernanceEmitter` | No-op test/dev emitter that acknowledges envelopes as delivered without sending data to an external provider. |

Core does not reference Azure Monitor, Event Hubs, Purview, OpenTelemetry, SIEM SDKs, robotics packages, AI model packages, or cloud-provider SDKs.

## Outbox semantic contract

The governance outbox is a **durable local state record**, not a package-owned distributed queue and not an append-only event stream.

The current contract is:

| Question | Current answer |
| --- | --- |
| Are outbox entries append-only? | No. One `GovernanceOutboxEntry` changes status over its lifecycle. Use decision receipt and lifecycle event stores for append-style evidence. |
| What is the local idempotency key? | `OutboxEntryId`. The EF Core model enforces a unique index for it. |
| What does `SaveAsync` do for an existing id? | It updates the existing row for that `OutboxEntryId` rather than appending another row. |
| What delivery guarantee is provided? | Durable local record plus at-least-once / best-effort provider emission semantics. Exactly-once is not claimed. |
| What ordering is provided? | Deterministic local query ordering for candidates, not global, per-correlation, or per-aggregate ordering. |
| What protects concurrent state updates? | EF Core uses `ConcurrencyStamp` as an optimistic concurrency token. This protects row state; it does not prevent duplicate provider calls before final state is saved. |
| What remains host-owned? | Worker topology, claim/lease behavior, provider idempotency, database migrations, duplicate-key reconciliation, retention, monitoring, and tamper-evidence infrastructure. |

See [Governance Outbox Delivery Semantics](governance-outbox-delivery-semantics.md) for the complete production semantics and host checklist.

## In-memory development stores

`AsiBackbone.Storage.InMemory` includes development and test stores:

| Type | Purpose |
| --- | --- |
| `InMemoryDecisionReceiptLifecycleStore` | Stores lifecycle events in memory for tests, samples, and local development. |
| `InMemoryGovernanceOutboxStore` | Stores governance outbox entries in memory for tests, samples, and local development. |

These stores are intentionally not durable across process restarts. Production hosts should use EF Core or another host-owned durable storage adapter.

The in-memory outbox store is single-process test/sample infrastructure. It should not be used to infer cross-replica claim or lease behavior.

## No-op drain proof path

The no-op drain path exists to prove the outbox handoff before a real provider is added:

```text
GovernanceEmissionEnvelope
  -> IGovernanceOutboxStore
  -> GovernanceOutboxDrain
  -> NoOpGovernanceEmitter
  -> GovernanceEmissionResult.Delivered
  -> delivered outbox state
```

`NoOpGovernanceEmitter` is not a production emission provider. It is a local validation seam for tests, samples, and smoke flows that need to prove pending -> delivered behavior without OpenTelemetry, Azure Monitor, Event Hubs, Purview, SIEM, or another external service.

The drain path also normalizes retryable, deferred, failed, and terminal provider-neutral results back into outbox state so the local accountability record is preserved when provider handoff does not succeed.

## Outbox status model

Outbox entries use the provider-neutral `GovernanceEmissionStatus` vocabulary:

| Status | Meaning |
| --- | --- |
| `Pending` | The envelope is saved locally and ready for delivery. |
| `Delivered` | A downstream provider accepted or delivered the emission. |
| `Deferred` | Delivery is intentionally delayed by host or provider policy. |
| `Failed` | Delivery failed and retryability is not implied by status. |
| `RetryableFailure` | Delivery failed but is expected to be retryable. |
| `DeadLettered` | Delivery reached a terminal state or policy quarantine. |

The current status model does not include a provider-neutral `Claimed` or `InProgress` state. `FindPendingAsync` and `FindRetryReadyAsync` return candidates for delivery, not claimed work items. Hosts that run multiple workers against the same durable store need host-owned claiming, partitioning, or downstream idempotency.

`GovernanceOutboxEntry` also tracks:

* `RetryCount`
* `MaxRetryCount`
* `NextRetryUtc`
* `LastError`
* `ProviderName`
* `ProviderRecordId`
* `DeadLetterReason`
* minimized metadata

## EF Core outbox selection and indexing

The EF Core outbox store pushes common drain selection work into the provider query before materialization:

* `FindPendingAsync` filters to `Pending`, orders by `CreatedUtc` and `OutboxEntryId`, and applies `Take(maxCount)` in the database query.
* `FindRetryReadyAsync` filters to deferred, failed, or retryable-failure rows that have not exhausted retry count and have no future `NextRetryUtc`, orders by retry timestamp and `OutboxEntryId`, and applies `Take(maxCount)` in the database query.

The built-in EF Core model includes provider-neutral indexes for common drain paths, including status, retry timestamp, created/updated timestamps, deterministic outbox identifiers, correlation identifiers, decision receipt identifiers, and envelope identifiers. Hosts remain responsible for reviewing the generated model against their database provider, migration strategy, workload, retention policy, and horizontal-worker pattern.

Provider-specific filtered indexes, partial indexes, table partitioning, claim/lease columns, lock hints, or queue-specific SQL are intentionally host-owned migration decisions. AsiBackbone supplies the portable model and selection semantics; production hosts decide whether to add provider-specific optimization beyond that portable baseline.

## EF Core metadata JSON storage

The EF Core outbox store currently serializes minimized metadata dictionaries into string-backed JSON columns:

* `MetadataJson`
* `EnvelopeMetadataJson`
* `EnvelopePayloadMetadataJson`

These columns remain text-backed JSON in the provider-neutral package model. Native EF Core JSON column mapping is not adopted as the default outbox metadata strategy because the metadata surfaces are open-ended dictionaries and the package avoids provider-specific JSON column assumptions.

Hosts that need provider-native JSON filtering can add host-owned migrations, generated/computed columns, JSON indexes, views, or provider-specific SQL over the existing columns.

See [EF Core JSON Metadata Storage Strategy](ef-core-json-metadata-storage.md) for the decision record and future adoption criteria.

## Failure handling

Provider failures should be normalized into `GovernanceEmissionError` before updating the outbox. The host can then decide whether to retry, defer, dead-letter, or escalate.

Recommended sequence:

1. Save the decision receipt or lifecycle event locally.
2. Create a `GovernanceEmissionEnvelope`.
3. Enqueue the envelope into `IGovernanceOutboxStore`.
4. Attempt optional provider emission through `GovernanceOutboxDrain`.
5. Mark the outbox entry delivered, failed, retryable, deferred, or dead-lettered.

This avoids losing the local accountability record when external sinks are unavailable.

The sequence above is not an exactly-once delivery guarantee. A scaled host should either run one active worker per partition or add a claim-before-emit strategy before increasing worker count for the same durable outbox.

## Operational visibility

Durable outbox persistence reduces event-loss risk, but it does not automatically guarantee that centralized monitoring, governance catalogs, SIEM systems, or compliance ledgers received the event. Hosts should monitor the drain path and alert on sustained backlog or repeated emission failure.

See [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md) for provider-neutral operational controls, metrics, alert thresholds, dead-letter guidance, and recovery runbook expectations.

See [Outbox Multi-Worker Concurrency](outbox-multi-worker-concurrency.md) for guidance on horizontally scaled drain workers, EF Core optimistic concurrency limits, provider-specific SQL claiming patterns, and idempotent provider delivery.

## Privacy and provider boundaries

Outbox entries should contain minimized provider-neutral metadata only. Do not store raw capability tokens, secrets, connection strings, provider credentials, raw prompts, protected document bodies, or unredacted sensitive records in the outbox payload or metadata.

Use opaque identifiers, hashes, policy versions, policy hashes, lifecycle stage values, correlation identifiers, and trace identifiers that are safe under host policy.

See [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md) for practical safe-to-store and safe-to-export guidance before enabling durable audit persistence or provider emission.

## Related documentation

- [Governance Emission Contract](governance-emission-contract.md)
- [Governance Outbox Delivery Semantics](governance-outbox-delivery-semantics.md)
- [EF Core JSON Metadata Storage Strategy](ef-core-json-metadata-storage.md)
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)
- [Outbox Multi-Worker Concurrency](outbox-multi-worker-concurrency.md)
- [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md)
- [Audit Residue Observability Schema](audit-residue-observability-schema.md)
- [EF Core Integration Boundary](ef-core-integration-boundary.md)
