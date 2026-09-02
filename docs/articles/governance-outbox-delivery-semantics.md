# Governance Outbox Delivery Semantics

This article defines the production semantics for governance outbox entries: identity, persistence, retry, idempotency, ordering, and host responsibilities.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone remains a governance and policy spine. It provides durable local outbox records and provider-neutral drain primitives; it is not an AI model host, distributed queue, SIEM product, immutable ledger, or exactly-once delivery system.

## Summary contract

| Area | Package behavior | Host responsibility |
| --- | --- | --- |
| Local persistence | Save a provider-neutral `GovernanceOutboxEntry` before optional downstream emission. | Choose a durable store, migration strategy, retention policy, backup policy, and operational monitoring. |
| Entry identity | `OutboxEntryId` is the stable outbox record identifier. The EF Core model enforces a unique index for this value. | Treat `OutboxEntryId` as an idempotency boundary when replaying, reconciling, or manually recovering records. |
| Append vs update | Outbox entries are **state records**, not an append-only event stream. The same outbox entry moves through pending, delivered, failed, retryable, deferred, or dead-lettered state. | Use audit residue and lifecycle event stores when an append-only evidence trail is required. |
| Delivery | Provider emission is **at-least-once / best-effort** unless the host and provider add stronger idempotency or transaction semantics. | Do not assume exactly-once provider delivery. Supply provider idempotency keys where supported. |
| Selection | `FindPendingAsync` and `FindRetryReadyAsync` return ordered delivery candidates. | Add claim/lease, partitioning, singleton workers, or provider-side idempotency before scaling workers against the same rows. |
| Ordering | Pending rows are ordered by `CreatedUtc` then `OutboxEntryId`; retry-ready rows are ordered by retry timestamp then `OutboxEntryId`. | Do not treat this as a global, per-correlation, or per-aggregate ordering guarantee unless the host enforces partitioned processing. |
| Concurrency | EF Core uses `ConcurrencyStamp` as an optimistic concurrency token and a unique `OutboxEntryId` index. | Handle `DbUpdateConcurrencyException`, duplicate-key races, retries, and recovery according to the host's provider and operational policy. |
| Tamper evidence | The outbox stores policy hashes, payload hashes, identifiers, timestamps, and metadata useful for traceability. | Do not describe records as cryptographically tamper-evident unless the host has enabled signing, immutable storage, or an external evidence chain. |

## Chosen semantics

The current outbox is a durable state table for local accountability evidence. It is intentionally smaller than a distributed queue and more honest than an overclaimed audit ledger.

The chosen semantics are:

1. **Durable-before-provider**: the local outbox entry should be saved before optional provider delivery is attempted.
2. **Stateful, not append-only**: one outbox entry changes status over its lifecycle; lifecycle/audit stores carry append-style evidence where needed.
3. **At-least-once provider posture**: provider delivery may be attempted more than once during retry, replay, manual drain, scale-out, or crash recovery.
4. **Stable idempotency keys**: the envelope and outbox identifiers exist so hosts and providers can collapse duplicate attempts where supported.
5. **Host-owned scale-out safety**: the package does not silently claim rows, lease rows, lock rows, or create a distributed singleton.
6. **Provider-neutral failure vocabulary**: retries, deferrals, failures, and dead letters are represented without binding Core to a provider SDK.

This fits AsiBackbone's role as a governance spine: it preserves local decision and emission state, but it does not pretend to own the entire distributed delivery path.

## Identity and idempotency fields

Use these identifiers when designing provider delivery and recovery:

| Field | Use |
| --- | --- |
| `GovernanceOutboxEntry.OutboxEntryId` | Stable local outbox record identifier and primary package-level idempotency boundary. |
| `GovernanceEmissionEnvelope.EnvelopeId` | Stable provider-neutral emission envelope identifier. Useful as a provider idempotency key when accepted by the provider. |
| `GovernanceEmissionEnvelope.EventId` | Source governance event identifier, when available. Useful for reconciling source events to outbox records. |
| `GovernanceEmissionEnvelope.AuditResidueId` | Links provider emission back to local audit residue. |
| `GovernanceEmissionEnvelope.CorrelationId` | Cross-request or operation correlation. Useful for diagnostics, not by itself a uniqueness guarantee. |
| `GovernanceEmissionEnvelope.OutboxSequence` | Optional host-supplied sequence hint. Useful for diagnostics or host-owned ordering, but not a package-owned ordering guarantee. |
| `GovernanceOutboxEntry.ProviderRecordId` | Provider-returned delivery identifier, when safe to store. Useful for reconciliation after provider acceptance. |

A practical provider call should prefer a stable idempotency key such as `EnvelopeId` or `OutboxEntryId` rather than generating a fresh key on each retry.

## Duplicate writes

`SaveAsync` persists the current state for an outbox entry by stable `OutboxEntryId`. For an already-persisted entry, saving another entry with the same `OutboxEntryId` updates the existing row state instead of creating a second row.

That behavior supports idempotent recovery flows such as:

```text
load or reconstruct entry with known OutboxEntryId
  -> save pending / delivered / failed state
  -> later replay same OutboxEntryId
  -> update existing row rather than append duplicate row
```

This does not mean all duplicate races are hidden. If two host contexts both decide that the same `OutboxEntryId` is new and insert concurrently, the EF Core unique index is the final storage boundary. Depending on timing and database provider behavior, one insert may succeed while the other receives a duplicate-key or update exception. The host should catch, reload, and reconcile according to its retry policy when it intentionally uses externally supplied outbox identifiers.

## Retry and poison-message handling

The outbox state model uses `GovernanceEmissionStatus`:

| Status | Retry meaning |
| --- | --- |
| `Pending` | Ready for first delivery attempt. |
| `Delivered` | Terminal success from the outbox perspective. |
| `Deferred` | Delivery intentionally delayed by host or provider policy. |
| `Failed` | Delivery failed; retryability is not implied by the status alone. |
| `RetryableFailure` | Delivery failed and is expected to be retryable. |
| `DeadLettered` | Terminal failure/quarantine state. |

`RetryCount` and `NextRetryUtc` provide provider-neutral retry scheduling. `MaxRetryCount` is retained for persisted-record compatibility; the built-in drain uses `AsiBackboneGovernanceOutboxOptions.MaxRetryAttempts` as its authoritative threshold. `LastError`, `ProviderName`, `ProviderRecordId`, and `DeadLetterReason` preserve safe diagnostic state for operations and recovery.

Poison-message handling remains host policy. A host may dead-letter immediately for known permanent failures, use the drain's configured maximum retry attempts, disable automatic threshold dead-lettering, or route records to a manual review process before retrying. Dead-lettering should not erase the local audit context.

## Concurrency semantics

The EF Core outbox model uses two complementary protections:

1. `OutboxEntryId` has a unique index so a stable outbox identifier maps to one durable row.
2. `ConcurrencyStamp` is an optimistic concurrency token so stale state transitions can be detected during save.

These protections preserve row integrity; they do not guarantee duplicate-free provider calls. Provider emission occurs outside the database transaction used to save the final state. Two workers can therefore select the same candidate and both call the provider before one final-state save loses an optimistic concurrency race.

For scaled hosts, use one of the documented patterns:

- one active drain worker per durable outbox partition;
- partitioned workers with disjoint selection scopes;
- host-owned claim/lease behavior before provider emission;
- provider-side idempotency keys for replay and duplicate attempts.

## Ordering semantics

The built-in selection order is deterministic for query results, not a distributed ordering guarantee.

Pending delivery candidates are ordered by:

```text
CreatedUtc -> OutboxEntryId
```

Retry-ready delivery candidates are ordered by:

```text
NextRetryUtc or UpdatedUtc -> OutboxEntryId
```

This provides stable local ordering for a single worker reading from one store. It does not prove global ordering across replicas, database partitions, tenants, correlations, aggregates, regions, or providers. Hosts that require per-correlation or per-aggregate order should partition and drain by that key.

## Host application checklist

Before enabling durable outbox persistence in production, decide:

- which database provider and migration strategy owns the outbox table;
- whether one active worker, partitioned workers, or a claim/lease adapter drains rows;
- which stable identifier will be sent as the provider idempotency key;
- how duplicate-key and optimistic concurrency exceptions are retried or reconciled;
- when retryable failures become dead-lettered;
- how dead-lettered records are reviewed, replayed, or retained;
- what backlog, retry, dead-letter, and provider-failure metrics trigger alerts;
- which metadata fields are safe to persist and export;
- whether signing, immutable storage, or an external ledger is required before making tamper-evidence claims.

## Related documentation

- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Outbox Multi-Worker Concurrency Guidance](outbox-multi-worker-concurrency.md)
- [Outbox Claim and Lease Design Record](outbox-claim-lease-design.md)
- [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md)
