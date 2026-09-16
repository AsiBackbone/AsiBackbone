# Upgrade Guide: 1.0.0 to 1.1.0

This guide covers upgrading from the stable `1.0.0` package family to the `1.1.0 - Observability, Outbox, and Governance Emission Providers` package family.

AsiBackbone remains governance infrastructure for consequential software decision flow. It is not a cloud observability backend, signing product, immutable ledger, or compliance guarantee by itself.

## Compatibility summary

`1.1.0` is an additive minor release.

For existing `1.0.0` consumers that only use policy evaluation, decisions, acknowledgments, audit residue, capability-token references, EF Core audit ledger persistence, or ASP.NET Core request helpers, no source-code change should be required solely because of the version upgrade.

Upgrade pressure appears only when a host chooses to adopt new features such as durable governance outbox persistence, hosted outbox draining, OpenTelemetry emission, DLP/classification failure policy, or signing-ready metadata.

## Package upgrade

Update existing stable package references from `1.0.0` to `1.1.0` when the packages are published.

```xml
<PackageReference Include="AsiBackbone.Core" Version="1.1.0" />
<PackageReference Include="AsiBackbone.Storage.InMemory" Version="1.1.0" />
<PackageReference Include="AsiBackbone.EntityFrameworkCore" Version="1.1.0" />
<PackageReference Include="AsiBackbone.AspNetCore" Version="1.1.0" />
```

Add the OpenTelemetry provider only when the host intends to emit governance envelopes into the host's OpenTelemetry pipeline.

```xml
<PackageReference Include="AsiBackbone.OpenTelemetry" Version="1.1.0" />
```

Do not add provider packages merely because they are documented as future directions. Event Hubs, Purview, Azure-specific, signing-provider, gateway, robotics, and immutable-storage packages are not part of this stable package family unless separately released.

## Progressive upgrade path

Treat `1.1.0` capabilities as optional layers. You can upgrade package versions first, verify the existing `1.0.0`-style flow, and then choose add-ons one at a time.

| Need | Add now? | First document |
| --- | --- | --- |
| Keep existing policy evaluation and audit residue | Yes, if already used | [First 15 Minutes: Standard API Gating](quickstart-api-gating.md) |
| Understand minimum Core-only use | Optional learning path | [Progressive Adoption Ladder](progressive-adoption.md) |
| Durable audit/outbox records | Only when local records must survive restarts or provider outages | [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md) |
| Hosted outbox drain | Only after a durable outbox store exists | [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md) |
| OpenTelemetry projection | Only when the host wants diagnostics projection | [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md) |
| DLP/classification failure policy | Only when host-owned screening can fail, timeout, block, or classify | [DLP and Classification Failure Policy](dlp-classification-failure-policy.md) |
| Signing-ready or managed-key integration | Only when signing and verification responsibilities are defined | [Signing Provider Package Boundary](signing-provider-package-boundary.md) |
| Roslyn analyzers | Optional build-time safety rails | [Roslyn Analyzers](roslyn-analyzers.md) |

## Existing 1.0.0 consumers

Existing consumers can continue to use the 1.0.0-style flow:

```text
Intent or request
  -> Build policy context
  -> Evaluate constraints
  -> Compose decision result
  -> Require acknowledgment when needed
  -> Write audit residue
  -> Issue optional scoped capability token
  -> Host application decides whether and how to execute
```

The 1.1.0 features extend this flow after local audit residue is created. They do not require every host to emit telemetry or adopt an outbox immediately.

## Adopting durable governance outbox persistence

Use durable local/outbox persistence when governance events must survive provider outages, process restarts, network failures, or downstream classification delays.

Recommended sequence:

1. Evaluate the policy and create the audit residue or lifecycle event.
2. Persist the local audit or lifecycle record.
3. Build a `GovernanceEmissionEnvelope`.
4. Enqueue the envelope in `IAsiBackboneGovernanceOutboxStore`.
5. Drain the outbox through an `IAsiBackboneGovernanceEmitter`.
6. Mark the entry delivered, failed, retryable, deferred, or dead-lettered.

The durable local outbox is the reliability baseline. Provider emission should be downstream of that local record.

### EF Core hosts

For EF Core hosts, review the new model configuration and generate host-owned migrations for outbox and lifecycle persistence.

The host remains responsible for:

- calling the appropriate AsiBackbone model-configuration extension;
- generating and reviewing migrations;
- selecting the database provider;
- deploying schema changes;
- defining retention and cleanup policy;
- protecting audit/outbox tables with appropriate access controls;
- validating query performance and indexes under expected workload.

Do not treat the package as owning database lifecycle or production migration deployment.

## In-memory and no-op proof paths

`AsiBackbone.Storage.InMemory` and the no-op governance emitter are useful for:

- unit tests;
- local development;
- samples;
- smoke tests;
- proof that the outbox drain can call a provider-neutral emitter.

They should not be used as production evidence of durable persistence, external provider delivery, tamper evidence, audit completeness, or compliance.

Use this wording in production documentation:

> The no-op emitter validates wiring only. It does not deliver governance events to an external backend.

## Adopting the hosted outbox drain

`AsiBackbone.AspNetCore` can host the provider-neutral outbox drain in an ASP.NET Core or generic-host application.

Typical adoption steps:

1. Register a durable `IAsiBackboneGovernanceOutboxStore`.
2. Register one concrete `IAsiBackboneGovernanceEmitter`.
3. Configure the hosted drain worker.
4. Choose batch size, polling interval, failure delay, and shutdown behavior.
5. Verify that duplicate workers are not accidentally registered.
6. Confirm provider failures update local outbox state rather than deleting local records.

Operational guidance:

- use conservative polling intervals until production load is understood;
- preserve provider failures for investigation;
- avoid high-cardinality metadata in provider payloads;
- use dead-letter handling for terminal failures;
- keep host retry and alerting policies explicit.

## Adopting OpenTelemetry emission

Add `AsiBackbone.OpenTelemetry` when the host wants to project governance envelopes into OpenTelemetry-compatible diagnostics.

The OpenTelemetry provider:

- implements `IAsiBackboneGovernanceEmitter`;
- emits activity events through `ActivitySource`;
- records low-cardinality metrics through `Meter`;
- exposes stable `asibackbone.*` attribute constants;
- returns provider-neutral emission results and errors.

Recommended flow:

```text
Policy decision / audit residue
  -> GovernanceEmissionEnvelope
  -> durable governance outbox
  -> hosted outbox drain
  -> OpenTelemetryGovernanceEmitter
  -> ActivitySource / Meter
  -> host-configured OpenTelemetry exporters
```

Avoid direct emitter use as the only accountability path in production unless the host has intentionally chosen and documented that tradeoff.

## Azure Monitor exporter guidance

Azure Monitor should be configured through the host's OpenTelemetry pipeline, not inside AsiBackbone Core or the OpenTelemetry provider.

```text
AsiBackbone.OpenTelemetry
  -> ActivitySource / Meter
  -> host OpenTelemetry SDK pipeline
  -> host-configured Azure Monitor exporter
  -> Azure Monitor / Application Insights / Log Analytics
```

The AsiBackbone OpenTelemetry provider should not contain Azure connection strings, instrumentation keys, workspace IDs, tenant IDs, subscription IDs, or Azure SDK types.

Host applications own:

- exporter package references;
- Azure authentication and configuration;
- sampling policy;
- resource attributes;
- retention policy;
- alert rules;
- dashboards;
- operational access controls.

## DLP and classification failure policy

Before sending governance events to any external provider, decide how classification and DLP failures behave.

Recommended defaults:

| Condition | Safer default |
| --- | --- |
| Classifier unavailable | Defer, quarantine, or emit only a minimized safe envelope. |
| Classification timeout | Treat as indeterminate and apply risk-sensitive host policy. |
| DLP violation | Block provider emission and preserve the local outbox record. |
| Provider unavailable | Keep the local record and retry with backoff. |
| Provider rejects payload | Record a normalized error and retry, defer, or dead-letter according to policy. |

Avoid fail-open behavior for high-risk or sensitive payloads unless the host has a documented exception process.

## Signing-ready metadata

`1.1.0` can carry signing-ready metadata and exposes provider-neutral signing and verification seams.

This does not mean records are signed, immutable, non-repudiable, or tamper-evident by default.

Only make tamper-evidence claims when the host or a future package has implemented all required pieces:

- artifact hashing;
- concrete signing provider;
- protected key management;
- signature verification policy;
- durable storage guarantees;
- retention and key-rotation policy;
- operational review procedures.

## Event Hubs and Purview deferrals

Event Hubs and Purview are documented as future provider directions in this milestone.

For `1.1.0`:

- Event Hubs is design documentation only;
- Purview is governance and lineage enrichment strategy documentation only;
- no Event Hubs or Purview SDK dependency is added to Core;
- no stable Event Hubs or Purview implementation package is included;
- provider implementation should happen in future issues with separate API, privacy, and package-boundary review.

## Migration checklist

Before upgrading a production host:

- [ ] Update package references to `1.1.0` in a development branch.
- [ ] Build and test existing 1.0.0 flows without enabling new provider behavior.
- [ ] Decide whether durable outbox persistence is required.
