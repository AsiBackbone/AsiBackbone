# Decision Receipt Observability Schema

This article documents the provider-neutral telemetry, traceability, and operational diagnostics fields added to the decision receipt model for the `1.1.0 - Observability, Outbox, and Governance Emission Providers` milestone.

These fields support observability and governance emission without making `AsiBackbone.Core` depend on OpenTelemetry, Azure Monitor, Event Hubs, Purview, SIEM products, or any provider-specific package.

## Design intent

A decision receipt should remain a framework-neutral governance record. The observability schema adds enough structure for hosts and provider adapters to correlate decisions across logs, traces, outbox records, gateways, dashboards, and governance enrichment systems.

The fields are additive. Existing hosts can keep creating decision receipts with only the original identifiers, actor context, outcome, reason codes, policy fields, and metadata. New hosts can populate telemetry fields when the values are available.

## Construction guidance

`DecisionReceipt.Create`, `DecisionReceipt.FromDecision`, and `DecisionReceipt.FromConstraint` remain supported direct factory paths. They are compact for simple records, but their optional telemetry, policy, outbox, and provider fields can become hard to scan when a host populates many values.

For richer records, prefer `DecisionReceiptBuilder` so each optional field is named at the call site while the resulting `DecisionReceipt` remains immutable:

```csharp
DecisionReceipt receipt = DecisionReceiptBuilder.FromDecision(
    actor,
    "payments.approve",
    decision)
    .WithEventId("event-123")
    .WithDecisionLatencyMs(42)
    .WithConstraintSetHash("constraint-set-hash")
    .WithConstraintCount(3)
    .WithRiskScore(0.75)
    .WithPolicyScope("payments")
    .WithEmitterStatus("queued")
    .WithEmitterProvider("open-telemetry")
    .AddMetadata("region", "us-la")
    .Build();
```

The builder is an ergonomic construction helper, not a mutable audit record. `Build()` delegates to the same normalization and validation path as the direct factories, including reason-code trimming, metadata normalization, timestamp normalization, non-negative numeric checks, and risk-score checks.

## Provider-neutral field reference

| Field | Purpose | PII-safe guidance |
| --- | --- | --- |
| `DecisionReceiptId` | Stable identifier for the decision receipt shape. Defaults to `EventId` when not supplied. | Use an opaque identifier. Do not embed actor names, email addresses, document names, or protected resource values. |
| `SchemaVersion` | Serialized schema version for forward-compatible envelopes. | Safe to emit when it contains only package schema identity. |
| `TraceId` | Links the receipt to host or distributed tracing context. | Use standard trace identifiers. Do not place user or resource information in trace IDs. |
| `SpanId` | Links the receipt to the active span or operation segment. | Use opaque span identifiers only. |
| `ParentSpanId` | Links the receipt to the parent span when available. | Use opaque span identifiers only. |
| `DecisionLatencyMs` | Records elapsed decision/evaluation latency in milliseconds. | Numeric operational diagnostic; safe when not combined with sensitive payload fields. |
| `ConstraintSetHash` | Hash of the evaluated constraint set or policy bundle. | Emit hashes, not raw policy documents, secrets, prompts, or protected content. |
| `ConstraintCount` | Number of constraints evaluated. | Numeric operational diagnostic; safe by default. |
| `RiskScore` | Host-defined risk score for dashboards, routing, or escalation. | Avoid publishing sensitive scoring features. The score itself should be treated as operational metadata. |
| `PolicyScope` | Host-defined scope, jurisdiction, region, tenant group, or policy family. | Prefer coarse scopes or stable codes over names containing personal or protected data. |
| `TenantHash` | Privacy-preserving tenant identifier. | Use a stable salted hash or host-approved opaque identifier. Do not emit raw tenant names by default. |
| `OrganizationHash` | Privacy-preserving organization identifier. | Use a stable salted hash or host-approved opaque identifier. Do not emit raw organization names by default. |
| `EmitterStatus` | Provider-neutral emission status such as `queued`, `delivered`, `failed`, `blocked`, or `dead-lettered`. | Safe when values are controlled status codes. |
| `EmitterProvider` | Provider-neutral provider label such as `open-telemetry`, `log-analytics`, `event-hubs`, `purview`, or `siem`. | Safe when values are provider labels rather than tenant/workspace secrets. |
| `OutboxSequence` | Local sequence value for durable outbox ordering. | Numeric operational diagnostic; safe by default. |
| `GatewayExecutionId` | Identifier linking the receipt to a gateway execution boundary. | Use an opaque identifier. Do not embed command text, prompt text, or resource names. |
| `DecisionStage` | Provider-neutral stage name for the decision or emission lifecycle. | Use controlled values. Do not encode sensitive detail in the stage string. |

## Correlation behavior

`CorrelationId` remains the primary host workflow join key. `DecisionReceiptId` provides a stable receipt reference. `TraceId`, `SpanId`, and `ParentSpanId` let host applications or optional adapters project the same governance record into logs, traces, spans, events, and metrics.

A typical host path is:

```text
Host request / workflow
  -> CorrelationId
  -> TraceId / SpanId
  -> Governance decision
  -> DecisionReceiptId
  -> AuditLedgerRecord / outbox row
  -> Optional provider emission
```

The correlation fields should be stable and opaque. They should not contain raw personal data, protected resource names, secrets, prompt bodies, document contents, authorization tokens, or provider connection details.

## Version awareness

`SchemaVersion` is included on each decision receipt so serialized records can be interpreted safely as the package family evolves. The field is additive and defaults to the stable artifact schema version when the host does not provide one.

Hosts and provider adapters should treat unknown schema versions conservatively. They may store the record locally, skip unsupported provider enrichment, or emit a minimized safe envelope according to host policy.

## Provider boundaries

The schema is intentionally usable by multiple provider paths:

| Provider path | Use of neutral fields |
| --- | --- |
| File or local logs | Store the same field names in structured log records. |
| Database persistence | Persist columns or JSON fields without binding to a cloud provider. |
| OpenTelemetry | Map `TraceId`, `SpanId`, latency, outcome, policy fields, and stage fields to span events, logs, metrics, or attributes. |
| SIEM | Index status, provider, policy, risk, stage, and correlation fields for investigation. |
| Azure Monitor / Log Analytics | Emit minimized operational records from an optional adapter or host-owned pipeline. |
| Event Hubs | Stream minimized governance envelopes after durable local/outbox persistence. |
| Purview | Enrich catalog, classification, or lineage context without making Purview the raw audit store by default. |

`AsiBackbone.Core` owns the neutral field names and value-shape guidance. Provider-specific transformation, retry behavior, workspace IDs, cloud SDKs, exporters, and provider-specific payloads belong outside Core.

## Recommended controlled values

The Core model intentionally stores `EmitterStatus`, `EmitterProvider`, and `DecisionStage` as provider-neutral strings so hosts can evolve their own controlled vocabularies without taking a dependency on a specific provider package.

Recommended `EmitterStatus` examples:

* `pending`
* `queued`
* `delivered`
* `failed`
* `blocked`
* `quarantined`
* `dead-lettered`

Recommended `EmitterProvider` examples:

* `file`
* `database`
* `open-telemetry`
* `log-analytics`
* `event-hubs`
* `purview`
* `siem`

Recommended `DecisionStage` examples:

* `DecisionEvaluated`
* `AcknowledgmentRequested`
* `AcknowledgmentCompleted`
* `CapabilityTokenIssued`
* `GatewayExecutionStarted`
* `GatewayExecutionCompleted`
* `ExternalEmissionQueued`
* `ExternalEmissionDelivered`
* `ExternalEmissionFailed`

Hosts should prefer controlled values that are stable, short, and free of sensitive detail.

## Privacy and minimization checklist

Before emitting decision receipt outside the host boundary, verify that:

* actor, tenant, organization, subject, and resource identifiers are opaque, hashed, tokenized, or minimized according to host policy;
* raw tokens, secrets, prompts, document contents, protected records, and payload bodies are not placed in telemetry fields;
* `TenantHash` and `OrganizationHash` are not reversible without host-controlled secrets or lookup tables;
* `PolicyScope` and `DecisionStage` use controlled vocabulary rather than free-form sensitive descriptions;
* provider identifiers do not expose workspace secrets, connection strings, resource IDs that policy forbids, or deployment-sensitive topology;
* DLP/classification failures are recorded locally and handled according to policy before external emission.

## Related documentation

- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [Core Domain Language](core-domain-language.md)
- [EF Core Host Ownership and Migrations](ef-core-host-ownership-and-migrations.md)
