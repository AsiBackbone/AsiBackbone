# Production Hardening: Evaluator and Outbox Configuration

This article gives production-oriented guidance for two areas that can otherwise be easy to misread:

- evaluator exception handling, especially `TreatConstraintExceptionAsDenial`;
- EF Core governance outbox persistence, especially durable local writes, metadata storage, and host-owned delivery responsibilities.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance and policy spine for accountable software decision flow. It is not an AI model host, a SIEM product, a distributed queue, a compliance product, an immutable ledger, or an artificial superintelligence implementation.

## Recommended production posture

A governed production host should start from this posture and then intentionally relax only where the host has a reason:

| Area | Recommended posture | Why |
| --- | --- | --- |
| Empty policy surface | `DenyWhenNoConstraints = true` for governed production endpoints. | An empty constraint set may indicate dependency-injection, configuration, feature-flag, database, or policy-discovery failure. |
| Constraint exceptions | Keep the `3.x` default `TreatConstraintExceptionAsDenial = true` for governed production surfaces unless the host explicitly needs fail-fast exception propagation. | Eligible ordinary constraint exceptions produce stable denied decisions, reason codes, and auditable policy metadata instead of unstructured host exceptions. |
| Exception propagation opt-out | Set `TreatConstraintExceptionAsDenial = false` only when the host has an intentional exception, retry, transaction, telemetry, or incident boundary that must observe the original exception directly. | This preserves fail-fast behavior for hosts that already convert exceptions into reliable audit and operational evidence. |
| Threat contributor exceptions | Keep `TreatThreatContributorExceptionAsDenial = true` when contributors are registered. | Threat-model extension failures should not quietly continue execution. |
| Critical failures | Never treat critical host/runtime failures as ordinary policy denials. | Process corruption, cancellation, infrastructure failure, and runtime incidents belong to the host error boundary. |
| Outbox persistence | Save a local durable outbox row before optional downstream emission. | External telemetry or governance providers should be downstream delivery targets, not the first system of record. |
| Outbox delivery | Treat provider emission as at-least-once / best-effort unless the host adds stronger semantics. | The package does not claim exactly-once delivery, distributed locking, global ordering, or provider receipt guarantees. |
| Metadata storage | Keep metadata minimized and provider-neutral. | The EF Core package uses string-backed JSON metadata for portability; query stable first-class columns first. |

A typical explicit evaluator configuration for a governed production surface is:

```csharp
var evaluatorOptions = new GovernancePolicyOptions
{
    // Production governance surfaces should fail closed if expected constraints are missing.
    DenyWhenNoConstraints = true,

    // 3.x defaults eligible ordinary constraint failures to denied governance decisions
    // with stable reason codes and auditable policy metadata.
    TreatConstraintExceptionAsDenial = true,

    // Keep the full reason set for audit visibility unless a latency-sensitive host
    // intentionally prefers first-denial fast abort behavior.
    ShortCircuitOnFirstDenial = false
};
```

When the host intentionally opts out of exception-as-denial behavior, document the reason and make sure the host still records the failed governed attempt through its central failure path:

```csharp
var evaluatorOptions = new GovernancePolicyOptions
{
    DenyWhenNoConstraints = true,
    TreatConstraintExceptionAsDenial = false
};
```

## Evaluator exception handling

`TreatConstraintExceptionAsDenial` changes one narrow behavior: eligible non-cancellation, non-critical exceptions thrown by policy constraints become denied `GovernanceDecision` results.

It does not make the application safe by itself, and it does not replace host exception handling. The host still owns:

- dependency health checks;
- startup validation;
- transaction boundaries;
- retry and circuit-breaker policy;
- structured logging and redaction;
- incident response;
- HTTP error handling;
- durable audit persistence;
- monitoring and alerting.

### Policy/constraint failure versus critical host failure

Use this distinction when deciding whether to keep the fail-closed default or opt out:

| Failure type | Example | Recommended handling |
| --- | --- | --- |
| Policy/constraint failure | A regional rule service returns an unexpected policy value, a host-owned constraint throws during a recoverable policy lookup, or a custom rule cannot evaluate safely. | Keep the `3.x` default exception-as-denial posture when a denied decision artifact is operationally preferred. Opt out only when fail-fast host exception handling is required. |
| Host/runtime failure | Cancellation, process-corruption-style runtime failure, broken dependency startup, database outage, memory exhaustion, invalid runtime state, or other systemic incident. | Let the host error boundary, health checks, restart policy, and incident handling see the failure. Do not hide it as an ordinary governance denial. |

`OperationCanceledException` remains cancellation and continues to propagate. Critical host/runtime exceptions also continue to propagate instead of becoming denied governance decisions.

### What is logged when exceptions become denials

When `TreatConstraintExceptionAsDenial = true` and an eligible constraint exception is converted, the public decision receives the safe reason code:

```text
asibackbone.policy.constraint_exception
```

The public decision message is intentionally curated and must not include stack traces, connection strings, raw request bodies, secrets, tokens, protected data, raw prompts, or arbitrary user input.

When the evaluator has an `ILogger<DefaultGovernancePolicyEvaluator<TContext>>`, the exception-as-denial path writes an error-level log entry with:

- event id `4120`;
- event name `ConstraintExceptionDeniedError`;
- constraint name;
- exception type;
- correlation ID;
- policy version;
- policy hash;
- the exception object attached to the log record for host-owned telemetry.

That log record is operational telemetry, not public decision output. The host remains responsible for redaction, retention, access control, SIEM routing, and incident policy.

### Monitoring hooks for evaluator hardening

Production hosts should alert or review trends for:

- denied decisions grouped by reason code;
- escalation-recommended or acknowledgment-required outcomes;
- `asibackbone.policy.constraint_exception` occurrences;
- evaluator error event id `4120`;
- explicit opt-out registrations that set `TreatConstraintExceptionAsDenial = false`;
- empty-policy warnings when permissive empty-policy behavior is intentionally left enabled;
- sudden changes in deny, warning, defer, acknowledgment, or escalation rates after policy deployment;
- missing or stale policy version/hash values in emitted decisions and audit records.

## EF Core governance outbox persistence

The EF Core outbox adapter is a durable local storage adapter for provider-neutral governance emission envelopes. It is intentionally not a distributed queue, workflow engine, cloud emitter, SIEM adapter, immutable ledger, or exactly-once delivery mechanism.

### Insert and update path

The current EF Core outbox store uses ordinary asynchronous EF Core database I/O at the store boundary:

| Operation | Database I/O path |
| --- | --- |
| `EnqueueAsync` | Creates a `GovernanceOutboxEntry`, adds the mapped EF entity to the host-owned `DbContext`, then calls `SaveChangesAsync`. |
| `SaveAsync` for a new entry | Looks for an existing row by `OutboxEntryId` using `SingleOrDefaultAsync`; if no row exists, adds a new entity and calls `SaveChangesAsync`. |
| `SaveAsync` for an existing entry | Looks for an existing row by `OutboxEntryId`, updates current values, refreshes `ConcurrencyStamp`, then calls `SaveChangesAsync`. |
| `FindPendingAsync` | Applies status filtering, deterministic ordering, and `Take(maxCount)` in the database query before `ToListAsync`. |
| `FindRetryReadyAsync` | Applies retry-ready filtering, deterministic ordering, and `Take(maxCount)` in the database query before `ToListAsync`. |
| `MarkDeliveredAsync`, `MarkFailedAsync`, `MarkDeadLetteredAsync` | Load the existing entry, create the updated outbox state, then save the updated row. |

A row is durable only after the host-owned database provider successfully completes the relevant `SaveChangesAsync` call and the host transaction, if any, commits.

### JSON metadata storage strategy

The EF Core package stores minimized metadata dictionaries in string-backed JSON columns:

- `MetadataJson`
- `EnvelopeMetadataJson`
- `EnvelopePayloadMetadataJson`

This is a provider-neutral compatibility choice. It keeps the package portable across relational EF Core providers and avoids requiring native JSON type support, provider-specific migrations, compatibility-level settings, or generated columns as a package default.

Production query guidance:

- prefer first-class indexed columns such as status, outbox id, envelope id, correlation id, decision receipt id, policy version, policy hash, trace id, timestamps, provider name, provider record id, and error code;
- do not build primary operational behavior around parsing arbitrary metadata JSON;
- add host-owned computed columns, generated columns, JSON indexes, views, or provider-specific SQL only when the host has a documented provider and migration strategy;
- keep JSON metadata minimized and safe to store.

### Durability guarantees and non-guarantees

The package provides:

- provider-neutral outbox contracts and state models;
- durable local EF Core row persistence through a host-owned `DbContext`;
- stable local identifiers such as `OutboxEntryId` and `EnvelopeId`;
- optimistic concurrency through the configured `ConcurrencyStamp` token;
- deterministic local candidate ordering for pending and retry-ready queries;
- provider-neutral status, retry, error, and dead-letter fields.

The host application still owns:

- database provider selection;
- connection strings and credential handling;
- migration creation and deployment;
- transaction boundaries around source audit records and outbox records;
- backup and restore;
- retention, archival, deletion, and privacy policy;
- encryption and database access control;
- dead-letter review and replay procedures;
- claim/lease behavior for multi-worker drain patterns;
- provider idempotency keys and duplicate delivery handling;
- provider-specific SQL optimization;
- monitoring and alerting;
- signing, immutable storage, external evidence chains, and any tamper-evidence claims.

Do not claim that the outbox provides exactly-once delivery, global ordering, distributed locking, immutable audit evidence, cryptographic tamper-evidence, cloud-provider delivery, compliance certification, or SIEM coverage unless the host has implemented and validated those external pieces.

### Monitoring hooks for outbox hardening

Production hosts should monitor:

- pending outbox backlog age and count;
- retry-ready backlog age and count;
- repeated `RetryableFailure` transitions;
- `Failed` and `DeadLettered` counts by provider and error code;
- provider delivery latency;
- optimistic concurrency exception rate;
- duplicate-key reconciliation events;
- drain worker liveness;
- host transaction failures before outbox persistence;
- rows missing expected policy version, policy hash, correlation ID, or trace ID;
- records containing metadata keys that violate host safe-data policy.

## Package guarantees versus host responsibilities

| Concern | AsiBackbone package guarantee | Host responsibility |
| --- | --- | --- |
| Policy evaluation result | Returns a structured `GovernanceDecision` when evaluation completes normally or eligible exceptions are converted by the `3.x` default policy. | Decide whether to continue execution, return an error response, require acknowledgment, escalate, or retry. |
| Exception-as-denial | Converts eligible constraint exceptions into denied decisions by default unless the host explicitly sets `TreatConstraintExceptionAsDenial = false`. | Decide whether conversion is appropriate for each host and monitor both decisions and logs. |
| Public reason safety | Default exception-as-denial reason text is curated and stable. | Keep custom reason messages safe and prevent sensitive data from entering logs, metadata, or audit payloads. |
| Local outbox persistence | EF Core adapter persists provider-neutral outbox rows through a host-owned `DbContext`. | Configure the provider, migrations, transactions, credentials, access control, backup, retention, and operational policy. |
| Metadata portability | String-backed JSON metadata works as ordinary text columns across providers. | Add provider-specific JSON features only through explicit host-owned migrations and tests. |
| Provider emission | Core and EF Core provide contracts and local state for later drain. | Choose emitters, exporters, idempotency keys, retry policy, dead-letter policy, and recovery workflow. |
| Tamper evidence | Package records include identifiers, policy hashes, payload hashes, timestamps, and signing-ready seams. | Enable signing, key management, immutable storage, external evidence chains, and verification before making tamper-evidence claims. |

## Related documentation

- [Core Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Constraint Exception Policy](constraint-exception-policy.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Governance Outbox Delivery Semantics](governance-outbox-delivery-semantics.md)
- [EF Core JSON Metadata Storage Strategy](ef-core-json-metadata-storage.md)
- [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)
- [Outbox Multi-Worker Concurrency](outbox-multi-worker-concurrency.md)
- [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
