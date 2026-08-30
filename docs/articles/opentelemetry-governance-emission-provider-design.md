# OpenTelemetry Governance Emission Provider Design

This article documents the design for an OpenTelemetry-first governance emission provider for the `1.1.0 - Observability, Outbox, and Governance Emission Providers` milestone.

Issue: #144.

> [!NOTE]
> **Historical/superseded design record.** This page preserves the pre-implementation design used for issue #144. For current released behavior and package guidance, use [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md). The material below is retained without rewriting the original design history.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine for consequential software decision flow. It is not an AI model host, robot controller, observability backend, SIEM product, cloud governance platform, signing product, or compliance guarantee by itself.

## Purpose

The OpenTelemetry provider gives hosts a vendor-neutral path for projecting AsiBackbone governance events into the host application's configured observability pipeline.

The provider should adapt provider-neutral governance emission envelopes into OpenTelemetry-compatible telemetry without making `AsiBackbone.Core` depend on OpenTelemetry packages, exporters, cloud SDKs, or backend-specific concepts.

```text
Audit residue / lifecycle event / gateway result
  -> GovernanceEmissionEnvelope
  -> durable governance outbox
  -> OpenTelemetry governance emitter
  -> ActivitySource / logger / Meter
  -> host-configured OpenTelemetry exporters
  -> Azure Monitor, Datadog, Grafana, Splunk, Elastic, or other backends
```

OpenTelemetry is the projection path. The durable local audit and outbox records remain the reliability and accountability baseline.

## Package boundary

Planned provider package name:

```text
AsiBackbone.OpenTelemetry
```

Alternative package names that remain acceptable if the package family later groups observability integrations:

```text
AsiBackbone.Observability.OpenTelemetry
AsiBackbone.Telemetry.OpenTelemetry
```

The package should depend on:

* `AsiBackbone.Core`
* OpenTelemetry abstractions needed for activities, metrics, and provider-neutral instrumentation
* `Microsoft.Extensions.Logging.Abstractions` or logging abstractions if required for host logging seams
* `Microsoft.Extensions.Options` only if provider options are needed

The package should not depend on:

* Azure Monitor, Application Insights, Log Analytics, Event Hubs, Purview, Datadog, Grafana, Splunk, Elastic, or SIEM SDKs;
* Azure resource, workspace, tenant, subscription, instrumentation-key, or connection-string concepts;
* host-specific storage providers;
* signing or immutable-storage providers;
* robotics or AI model packages.

Azure Monitor should receive these events through normal host-configured OpenTelemetry exporter configuration, not through an Azure SDK dependency inside this provider.

## Provider placement

Core already owns the provider-neutral contracts:

* `IAsiBackboneGovernanceEmitter`
* `GovernanceEmissionEnvelope`
* `GovernanceEmissionPayload`
* `GovernanceEmissionResult`
* `GovernanceEmissionStatus`
* `GovernanceEmissionError`
* `GovernanceEmissionEventType`
* `IAsiBackboneGovernanceOutboxStore`
* `AsiBackboneGovernanceOutboxDrain`

The OpenTelemetry provider should implement the neutral emitter seam:

```text
IAsiBackboneGovernanceEmitter
  -> OpenTelemetryGovernanceEmitter
```

The outbox drain should remain provider-neutral. It should not know that OpenTelemetry is the downstream emitter.

## Instrumentation primitives

The provider should expose three OpenTelemetry-friendly seams.

| Primitive | Recommended use | Notes |
| --- | --- | --- |
| `ActivitySource` | Traces and span events for decision, acknowledgment, capability, gateway, lifecycle, outbox, and external-emission stages. | Primary correlation surface. |
| `ILogger` | Structured logs for emitted, deferred, failed, retryable, and dead-lettered provider handoff results. | Useful when a host routes logs through OpenTelemetry logging. |
| `Meter` | Counters and histograms for event counts, failures, latency, outbox backlog, and retry/dead-letter pressure. | Avoid high-cardinality labels. |

Suggested names:

```text
ActivitySource: AsiBackbone.OpenTelemetry
Meter:          AsiBackbone.OpenTelemetry
Logger source:  AsiBackbone.OpenTelemetry
```

The provider should also expose constants for source names and stable attribute names so hosts and tests can assert mappings without copying strings.

## Event categories

`GovernanceEmissionEventType` should map into stable OpenTelemetry event names.

| Governance event type | Activity event name | Metric/log role |
| --- | --- | --- |
| `Decision` | `asibackbone.decision.evaluated` | Count by outcome, reason code family, and policy scope. |
| `Acknowledgment` | `asibackbone.acknowledgment.recorded` | Count requested/completed acknowledgment stages. |
| `CapabilityToken` | `asibackbone.capability_token.issued` | Count token issuance without raw token values. |
| `Gateway` | `asibackbone.gateway.completed` | Count gateway allow/deny/error outcomes. |
| `AuditResidue` | `asibackbone.audit_residue.created` | Count durable residue creation. |
| `Lifecycle` | `asibackbone.lifecycle.recorded` | Track stage progression. |
| `Outbox` | `asibackbone.outbox.updated` | Track pending, delivered, failed, deferred, and dead-letter state. |
| `ProviderEmission` | `asibackbone.emission.delivered` or `asibackbone.emission.failed` | Track provider handoff result. |
| unknown/future | `asibackbone.governance.event` | Preserve event with generic category attribute. |

The implementation can start with one emitted `ActivityEvent` per envelope and expand to child activities later if needed.

## Attribute naming convention

Attribute names should be stable, lowercase, dotted, and namespaced. Use `asibackbone.*` to avoid collision with OpenTelemetry semantic conventions while making the attributes searchable.

### Core correlation attributes

| Attribute | Source field | Guidance |
| --- | --- | --- |
| `asibackbone.correlation_id` | `CorrelationId` | Opaque host workflow join key. |
| `asibackbone.audit_residue_id` | `AuditResidueId` | Opaque audit residue identifier. |
| `asibackbone.event_id` | `EventId` | Opaque governance event identifier. |
| `asibackbone.envelope_id` | `EnvelopeId` | Opaque emission envelope identifier. |
| `asibackbone.schema_version` | `SchemaVersion` | Safe schema identity. |
| `asibackbone.trace_id` | `TraceId` | Preserve only if host supplied. |
| `asibackbone.span_id` | `SpanId` | Preserve only if host supplied. |
| `asibackbone.parent_span_id` | `ParentSpanId` | Preserve only if host supplied. |

### Decision and policy attributes

| Attribute | Source field | Guidance |
| --- | --- | --- |
| `asibackbone.event_type` | `EventType` | Controlled event type. |
| `asibackbone.decision.outcome` | `Outcome` | Controlled outcome string. |
| `asibackbone.decision.stage` | `DecisionStage` | Controlled stage string. |
| `asibackbone.reason_codes` | metadata or source envelope | Prefer low-cardinality reason-code family where possible. |
| `asibackbone.policy.version` | `PolicyVersion` | Stable policy version. |
| `asibackbone.policy.hash` | `PolicyHash` | Hash only; never raw policy content. |
| `asibackbone.policy.scope` | metadata or audit residue | Coarse scope only. |
| `asibackbone.constraint_set.hash` | metadata or audit residue | Hash only. |
| `asibackbone.constraint.count` | metadata or audit residue | Numeric count. |
| `asibackbone.risk.score` | metadata or audit residue | Numeric host-defined score. |

### Lifecycle and gateway attributes

| Attribute | Source field | Guidance |
| --- | --- | --- |
| `asibackbone.lifecycle.stage` | `LifecycleStage` | Controlled lifecycle stage. |
| `asibackbone.lifecycle.stage_sequence` | `LifecycleStageSequence` | Stable sequence value. |
| `asibackbone.gateway.execution_id` | `GatewayExecutionId` | Opaque gateway execution ID. |
| `asibackbone.capability_token.id` | metadata or payload descriptor | Opaque token identifier only; never raw token. |
| `asibackbone.acknowledgment.id` | metadata or payload descriptor | Opaque acknowledgment identifier. |

### Outbox and emitter attributes

| Attribute | Source field | Guidance |
| --- | --- | --- |
| `asibackbone.outbox.sequence` | `OutboxSequence` | Numeric sequence when available. |
| `asibackbone.emitter.provider` | `EmitterProvider` or provider name | Use `open-telemetry` for this provider. |
| `asibackbone.emitter.status` | `EmitterStatus` or result status | Controlled status. |
| `asibackbone.emitter.result` | `GovernanceEmissionResult.Status` | Delivered, deferred, failed, retryable, or dead-lettered. |
| `asibackbone.emitter.failure.code` | `GovernanceEmissionError.Code` | Normalized safe code. |
| `asibackbone.emitter.failure.retryable` | `GovernanceEmissionError.IsRetryable` | Boolean. |
| `asibackbone.emitter.failure.provider_code` | safe provider error code | Safe code only; no raw provider payload. |

### Latency attributes and metrics

| Attribute or metric | Source | Guidance |
| --- | --- | --- |
| `asibackbone.decision.latency_ms` | `DecisionLatencyMs` | Attribute when present; histogram when measured by provider. |
| `asibackbone.emission.latency_ms` | provider measurement | Histogram. |
| `asibackbone.outbox.retry_count` | outbox entry | Attribute or metric label only when low cardinality. |

## Metrics

The first provider version should keep metrics low-cardinality.

Recommended counters:

| Metric | Type | Suggested dimensions |
| --- | --- | --- |
| `asibackbone.governance.emissions` | Counter | `event_type`, `result`, `provider` |
| `asibackbone.governance.emission_failures` | Counter | `event_type`, `failure_code`, `retryable` |
| `asibackbone.governance.outbox.dead_letters` | Counter | `event_type`, `provider` |
| `asibackbone.governance.outbox.deferred` | Counter | `event_type`, `provider` |

Recommended histograms:

| Metric | Type | Suggested dimensions |
| --- | --- | --- |
| `asibackbone.governance.emission_latency_ms` | Histogram | `event_type`, `provider` |
| `asibackbone.governance.decision_latency_ms` | Histogram | `event_type`, `outcome` |

Avoid actor IDs, correlation IDs, envelope IDs, audit residue IDs, trace IDs, policy hashes, and gateway execution IDs as metric labels. Those values are too high-cardinality for metrics.

## Emission sequence

The provider should be downstream of durable outbox persistence.

Recommended host sequence:

1. Evaluate policy and produce the neutral decision/audit residue.
2. Persist audit residue or lifecycle event locally.
3. Build a `GovernanceEmissionEnvelope`.
4. Enqueue the envelope in `IAsiBackboneGovernanceOutboxStore`.
5. Drain the outbox through `AsiBackboneGovernanceOutboxDrain` using `OpenTelemetryGovernanceEmitter`.
6. The emitter records an OpenTelemetry activity event, structured log, and optional metrics.
7. The emitter returns `GovernanceEmissionResult.Delivered` when local OpenTelemetry instrumentation accepted the event.
8. The outbox marks the entry delivered, deferred, failed, retryable, or dead-lettered according to the result.

The provider should not be the only durable record. If the host bypasses the outbox by calling the emitter directly, documentation should describe that as an advanced host-owned choice rather than the recommended accountability path.

## Result behavior

The provider should return provider-neutral results.

| Condition | Recommended result |
| --- | --- |
| Activity/log/metric emission completes without exception | `Delivered` with provider `open-telemetry`. |
| Event is intentionally skipped by provider options | `Deferred` or `Delivered` depending on whether the host considers skip a successful no-op. |
| Payload blocked by provider-side minimization rule | `DeadLettered` or `Failed` with `emission.blocked` according to host policy. |
| OpenTelemetry instrumentation throws unexpectedly | `RetryableFailure` when a later attempt may succeed; otherwise `Failed`. |
| Cancellation requested | Throw `OperationCanceledException`; the outbox drain preserves cancellation semantics. |

The provider should normalize unexpected failures into `GovernanceEmissionError` values and let the outbox preserve retry/dead-letter state.

## Privacy and minimization

The provider must not put these values into OpenTelemetry attributes, events, logs, metrics, baggage, or resource attributes:

* raw capability tokens;
* secrets, connection strings, API keys, credentials, or signing keys;
* raw prompts, documents, request bodies, protected records, payload bodies, or user-submitted content;
* raw personal data unless a host explicitly opts into that behavior;
* provider workspace secrets or deployment-sensitive topology;
* raw DLP or classification failure payloads.

Use opaque IDs, hashes, coarse policy scopes, controlled status values, numeric counts, and safe metadata. When in doubt, emit a minimized envelope and preserve the full governance record locally under host policy.

## Azure Monitor through OpenTelemetry

Azure Monitor can receive the provider's events when the host configures an Azure Monitor OpenTelemetry exporter in the application. That exporter configuration belongs to the host or to a future Azure-specific adapter package, not to this OpenTelemetry provider.

Conceptual host flow:

```text
AsiBackbone.OpenTelemetry
  -> OpenTelemetry SDK pipeline configured by the host
  -> Azure Monitor exporter configured by the host
  -> Azure Monitor / Application Insights / Log Analytics backend
```

The OpenTelemetry provider should document the attributes that Azure Monitor users can query, but it should not reference Azure SDK types, require Azure configuration, or claim Azure Monitor is the authoritative audit ledger.

## Test seams

The design should be testable without live Azure, Datadog, Grafana, Splunk, Elastic, SIEM, or third-party observability resources.

Recommended tests:

* provider emits an activity event for a decision envelope;
* provider preserves correlation, audit residue ID, event ID, schema version, policy version/hash, trace fields, lifecycle stage, gateway ID, outcome, and status attributes;
* provider records low-cardinality counters and histograms without high-cardinality labels;
* provider returns delivered result when instrumentation succeeds;
* provider normalizes instrumentation failure into provider-neutral result/error details;
* provider respects cancellation;
* Core has no OpenTelemetry dependency;
* provider package has no Azure SDK dependencies;
* outbox drain can use the OpenTelemetry emitter through `IAsiBackboneGovernanceEmitter` without knowing about OpenTelemetry types.

Tests can use in-memory activity listeners, fake loggers, meter listeners, and in-memory outbox stores. Live exporter tests should be optional and excluded from default CI.

## Implementation checklist

Before implementation begins, confirm:

* package name and namespace;
* source names for `ActivitySource`, `Meter`, and logger;
* stable attribute constant names;
* event-name constants;
* which telemetry primitives are required for the first version;
* whether logs are emitted directly or left to host logger scope enrichment;
* option defaults for including metrics, logs, and activity events;
* failure behavior for instrumentation exceptions;
* high-cardinality guardrails;
* documentation for Azure Monitor via host OpenTelemetry exporter;
* tests that require no live observability backend.

## Relationship to related issues

| Issue | Relationship |
| --- | --- |
| #140 Durable outbox | OpenTelemetry emission should happen after local durable outbox persistence. |
| #141 Lifecycle stages | Lifecycle stage and sequence should map to activity attributes/events. |
| #142 Audit residue telemetry | Trace, latency, gateway, outbox, and PII-safe identifiers provide provider mapping fields. |
| #149 Observability architecture | This provider design follows the Core-neutral provider package architecture. |
| #187 Governance emission contract | The provider implements the neutral `IAsiBackboneGovernanceEmitter` seam. |
| #193 No-op outbox drain | The no-op proof path validates the drain sequence before this real provider is added. |

## Related documentation

- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Audit Residue Observability Schema](audit-residue-observability-schema.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)

## Non-goals

This provider design does not implement:

* Azure Monitor direct emission;
* Application Insights direct SDK wiring;
* Log Analytics custom table management;
* Event Hubs streaming;
* Purview classification or lineage enrichment;
* signing, timestamping, or immutable storage;
* SIEM-specific payloads;
* replacement of durable local audit/outbox persistence;
* legal, compliance, or tamper-evidence guarantees.

The provider should remain an OpenTelemetry-first projection adapter for host-owned observability pipelines.
