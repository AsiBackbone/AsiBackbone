# OpenTelemetry Governance Emission Provider

Issue #197 adds the first concrete governance emission provider package: `AsiBackbone.OpenTelemetry`.

The provider keeps the Core package provider-neutral. `AsiBackbone.Core` continues to own the governance envelope, result, error, and outbox-drain contracts, while the OpenTelemetry package adapts those contracts into .NET diagnostics primitives that OpenTelemetry SDKs and exporters can observe.

## Package role

`AsiBackbone.OpenTelemetry` implements `IGovernanceEmitter` through `OpenTelemetryGovernanceEmitter`.

The provider emits:

- an `ActivitySource` activity with a governance activity event;
- stable `asibackbone.*` activity tags for correlation, decision receipt, lifecycle, policy, trace, latency, gateway, outbox, schema, and emitter fields;
- low-cardinality `Meter` counters and histograms for emission count, failure count, and emission latency;
- provider-neutral `GovernanceEmissionResult` and `GovernanceEmissionError` values.

The provider does **not** configure exporters and does **not** depend on Azure Monitor, Application Insights, Log Analytics, Event Hubs, Purview, Datadog, Grafana, Splunk, Elastic, SIEM, robotics, AI model, or cloud-provider SDK packages.

## Diagnostics names

The implementation exposes stable constants for tests and host configuration:

```csharp
OpenTelemetryGovernanceInstrumentation.ActivitySourceName
OpenTelemetryGovernanceInstrumentation.MeterName
OpenTelemetryGovernanceInstrumentation.ProviderName
```

Current source and meter name:

```text
AsiBackbone.OpenTelemetry
```

Provider result name:

```text
open-telemetry
```

## Recommended decision -> outbox -> drain -> OpenTelemetry flow

```text
Policy decision / decision receipt
  -> GovernanceEmissionEnvelope
  -> IGovernanceOutboxStore
  -> GovernanceOutboxDrain
  -> OpenTelemetryGovernanceEmitter
  -> ActivitySource / Meter
  -> host-configured OpenTelemetry exporters
```

This preserves the local durable audit/outbox record before any external observability path is attempted.

## Minimal direct emitter usage

Direct emitter usage is useful for tests, smoke checks, or advanced host-owned flows:

```csharp
IGovernanceEmitter emitter = new OpenTelemetryGovernanceEmitter();

GovernanceEmissionEnvelope envelope = GovernanceEmissionEnvelope.Create(
    GovernanceEmissionEventType.Decision,
    eventId: "event-001",
    correlationId: "correlation-001",
    auditResidueId: "audit-residue-001",
    policyVersion: "policy-v1",
    policyHash: "sha256:policy",
    traceId: "trace-001",
    operationName: "ApproveSensitiveAction",
    outcome: "RequireAcknowledgment",
    emitterStatus: GovernanceEmissionStatus.Pending.ToString());

GovernanceEmissionResult result = await emitter.EmitAsync(envelope, cancellationToken);
```

For production accountability, prefer the durable outbox flow rather than direct provider emission alone.

## Durable outbox drain usage

When paired with a configured outbox store, the hosted drain can deliver queued envelopes through the OpenTelemetry provider:

```csharp
builder.Services.AddScoped<IGovernanceEmitter, OpenTelemetryGovernanceEmitter>();

builder.Services.AddAsiBackboneGovernanceOutboxDrainWorker(options =>
{
    options.BatchSize = 100;
    options.PollingInterval = TimeSpan.FromSeconds(30);
    options.FailureDelay = TimeSpan.FromMinutes(1);
});
```

The outbox store may be in-memory for local validation or EF Core for durable host-owned persistence. Exporter configuration remains outside AsiBackbone.

## Azure Monitor through host OpenTelemetry configuration

Azure Monitor should receive governance emission telemetry through normal host-owned OpenTelemetry exporter wiring:

```text
AsiBackbone.OpenTelemetry
  -> ActivitySource / Meter
  -> host OpenTelemetry SDK pipeline
  -> host-configured Azure Monitor exporter
  -> Azure Monitor / Application Insights / Log Analytics
```

The AsiBackbone OpenTelemetry provider should not hold Azure connection strings, instrumentation keys, workspace IDs, tenant IDs, or Azure SDK types.

## Attribute coverage

The provider maps the following envelope fields when present:

| Field | Attribute |
| --- | --- |
| `CorrelationId` | `asibackbone.correlation_id` |
| `DecisionReceiptId` | `asibackbone.audit_residue_id` |
| `EventId` | `asibackbone.event_id` |
| `EnvelopeId` | `asibackbone.envelope_id` |
| `SchemaVersion` | `asibackbone.schema_version` |
| `TraceId` | `asibackbone.trace_id` |
| `SpanId` | `asibackbone.span_id` |
| `ParentSpanId` | `asibackbone.parent_span_id` |
| `EventType` | `asibackbone.event_type` |
| `Outcome` | `asibackbone.decision.outcome` |
| `DecisionStage` | `asibackbone.decision.stage` |
| `PolicyVersion` | `asibackbone.policy.version` |
| `PolicyHash` | `asibackbone.policy.hash` |
| `LifecycleStage` | `asibackbone.lifecycle.stage` |
| `LifecycleStageSequence` | `asibackbone.lifecycle.stage_sequence` |
| `GatewayExecutionId` | `asibackbone.gateway.execution_id` |
| `OutboxSequence` | `asibackbone.outbox.sequence` |
| `EmitterStatus` | `asibackbone.emitter.status` |
| provider result | `asibackbone.emitter.result` |
| local latency | `asibackbone.emission.latency_ms` |

Payload descriptor fields are emitted only as minimized descriptor tags, such as payload type, schema version, content type, content hash, and size. Raw protected content, raw prompts, raw tokens, secrets, and provider payload bodies are not emitted by this provider.

## Failure behavior

Unexpected provider-side instrumentation exceptions are normalized into:

- `GovernanceEmissionResult.RetryableFailure(...)`
- `GovernanceEmissionError` with code `opentelemetry.emission.exception`
- provider name `open-telemetry`

Cancellation still follows normal .NET cancellation semantics and is not converted into a failed governance result.

## Test seams

The provider is covered without live Azure, Datadog, Grafana, Splunk, Elastic, or SIEM resources by using:

- `ActivityListener` for activity/event/attribute assertions;
- `MeterListener` for counter/histogram assertions;
- an options-level `BeforeEmitAsync` hook for deterministic failure normalization tests.

Live exporter validation should remain optional host-owned testing, not default CI behavior.
