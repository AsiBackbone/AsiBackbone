# Observability and Governance Emission Architecture

This article documents the released `3.1.0` observability, durable outbox, signing, and governance-emission architecture boundary together with provider directions that remain design-only.

AsiBackbone is a governance spine for consequential software decision flow. It is not a compliance product, signing product, or cloud-governance platform by itself.

> [!IMPORTANT]
> Provider-specific integrations depend on Core. Core must never depend on provider-specific integrations.
>
> `AsiBackbone.Core` remains framework-neutral and vendor-neutral. Observability platforms, streaming systems, governance catalogs, signing systems, storage providers, and cloud-specific enrichment belong in optional packages or host applications.
>
> `AsiBackbone.OpenTelemetry` is the concrete released governance-emission provider package in `3.1.0`. Event Hubs, Purview, Azure Monitor-specific SDK adapters, immutable-storage providers, and additional cloud-specific packages remain design-only or host-owned guidance unless a later release separately reviews and ships them.

## Purpose

The `3.1.0` integration direction preserves AsiBackbone's neutral policy decision pipeline while allowing host applications to emit structured decision records into operational and governance systems.

The primary architecture path is:

```text
Decision
  -> Acknowledgment
  -> Capability grant
  -> Gateway execution boundary
  -> Decision receipt
  -> Durable local/outbox record
  -> Optional provider emission
```

The source-of-truth governance record should exist locally and durably before downstream emission. External systems may enrich, search, alert, stream, classify, or catalog the event, but they should not become the only place where the governance decision exists.

## Released versus design-only provider boundary

| Provider path | `3.1.0` status | Appropriate responsibility | Not appropriate responsibility |
| --- | --- | --- | --- |
| OpenTelemetry | **Released package:** `AsiBackbone.OpenTelemetry`. | Convert neutral governance-emission envelopes into .NET diagnostics through `ActivitySource`, activity events, tags, and `Meter` metrics. | Configure exporters, redefine Core decision semantics, or require Core to reference OpenTelemetry packages. |
| Azure Monitor / Log Analytics | **Host-configured exporter guidance** through OpenTelemetry. No Azure Monitor-specific AsiBackbone package is released. | Receive telemetry through the host's OpenTelemetry exporter pipeline. | Become the only audit store or force Azure SDK dependencies into Core. |
| Event Hubs | **Design-only future provider strategy.** No Event Hubs package is released in `3.1.0`. | Future optional streaming adapter for minimized governance events after durable outbox persistence. | Replace local durability or imply a current Event Hubs NuGet package exists. |
| Purview | **Strategy-only future enrichment direction.** No Purview package is released in `3.1.0`. | Future optional governance, catalog, or lineage enrichment for selected and classified events. | Store raw audit records by default, become the primary audit ledger, or imply a current Purview package exists. |
| Signing providers | **Released package boundaries** for local-development and managed-key adapter packages. | Attach signing or verification behavior through provider packages and host-owned key operations. | Claim tamper-evidence without deployed signing, verification, key management, storage, and retention controls. |

Provider-specific packages must depend on Core. Core must not depend on provider packages.

## High-level architecture

```text
Host application
  |
  | builds actor context, policy context, metadata, and host policy inputs
  v
AsiBackbone.Core
  |
  | evaluates policy and returns governance decision
  | creates acknowledgment challenge when required
  | creates scoped capability grant when allowed
  | creates decision receipt / decision receipt shape
  v
Durable local store / outbox
  |
  | persists decision, acknowledgment, token, gateway, and emission state
  | retries provider emission safely
  | records provider failure and classification state
  v
Released provider packages or host adapters
  |
  | OpenTelemetry projection through AsiBackbone.OpenTelemetry
  | host-configured Azure Monitor / Log Analytics exporter
  v
Design-only or future provider strategy
  |
  | Event Hubs streaming provider design
  | Purview governance / lineage enrichment strategy
  | future cloud-specific or immutable-storage providers
```

The local outbox is the reliability boundary. Provider emission is downstream of that boundary.

## Core-neutral rule placement

Core may define:

- decision outcomes and reason codes;
- actor, policy, operation, acknowledgment, capability, gateway, and audit identifiers;
- correlation, trace, event, record, schema, policy-version, and policy-hash fields;
- acknowledgment and capability result shapes;
- decision receipt and decision-receipt contracts;
- governance-emission envelopes and provider-neutral results;
- durable outbox contracts and drain primitives; and
- signing-ready and verification-policy abstractions.

Core must not contain:

- Azure Monitor, Application Insights, Log Analytics, Event Hubs, or Purview SDK dependencies;
- OpenTelemetry exporter configuration;
- cloud workspace, tenant, subscription, resource-group, namespace, topic, table, or catalog identifiers;
- provider-specific retry or payload-mapping logic;
- provider-specific DLP, classification, or lineage rules; or
- concrete signing-provider implementation or key-management logic.

## Durable outbox baseline

External emission should follow an outbox-style pattern so host applications do not lose governance records when downstream providers are unavailable.

```text
Decision receipt / lifecycle event
  -> host-owned durable store
  -> GovernanceEmissionEnvelope
  -> IGovernanceOutboxStore
  -> GovernanceOutboxDrain
  -> IGovernanceEmitter
  -> provider result
  -> delivered / failed / retryable / deferred / dead-letter state
```

A process crash, provider outage, network failure, rate limit, or DLP failure should not erase the original decision receipt.

## OpenTelemetry as the released provider

`AsiBackbone.OpenTelemetry` is the concrete released governance-emission provider package for `3.1.0`.

```text
Core neutral governance emission envelope
  -> AsiBackbone.OpenTelemetry
  -> ActivitySource / Meter
  -> host OpenTelemetry SDK pipeline
  -> selected exporter/backend
```

The host remains responsible for exporter selection, Azure Monitor connection settings, sampling, retention, backend routing, and operational alerting.

Telemetry payloads should remain metadata-minimized. Do not place secrets, raw tokens, raw personal data, protected records, prompt bodies, document contents, or sensitive payloads into telemetry attributes.

## Azure Monitor / Log Analytics guidance

Azure Monitor and Log Analytics are backend targets reached through the host-owned OpenTelemetry exporter pipeline. No Azure Monitor-specific AsiBackbone package is included in `3.1.0`.

```text
AsiBackbone.OpenTelemetry
  -> ActivitySource / Meter
  -> host OpenTelemetry SDK pipeline
  -> host-configured Azure Monitor exporter
  -> Azure Monitor / Application Insights / Log Analytics
```

Provider emission must not replace durable local or outbox persistence. Azure dependencies belong in host exporter configuration or a future optional provider package, not in Core.

## Design-only: Event Hubs

Event Hubs remains a design-only future provider strategy. It is not a released AsiBackbone NuGet package in `3.1.0`.

A future adapter may stream minimized governance envelopes after local outbox persistence, but Event Hubs must not become the local source of truth or bypass acknowledgment, capability-token, or gateway boundaries.

## Strategy-only: Purview

Purview remains a strategy-only future governance and lineage enrichment direction. It is not a released AsiBackbone NuGet package in `3.1.0`.

A future integration may map selected governance events to classified assets, data products, workflows, or lineage nodes. Raw audit records should remain in the host-owned durable store unless the host deliberately chooses another authoritative store.

## DLP and classification failure policy

Provider emission must pass through host-owned classification and DLP rules before leaving the local boundary. The host should define:

- which fields may leave the local store;
- which values require redaction, hashing, tokenization, generalization, or omission;
- whether actor identifiers must be opaque;
- what happens when classification is unavailable or inconclusive;
- which event types require signing before external emission; and
- which provider failures are retryable, terminal, or escalation-worthy.

DLP and classification remain host-owned responsibilities unless a future package explicitly implements a classifier integration boundary.

## Signing-ready and current limitations

The `3.1.0` package family includes signing-ready abstractions and provider signing boundaries, but signing alone does not prove tamper-evidence.

Do not describe records as tamper-proof, immutable, non-repudiable, legally certified, compliance-approved, or externally anchored by default.

Production tamper-evidence requires deployed signing, verification, protected key management, durable append-only or otherwise controlled storage, retention policy, monitoring, and incident response.

## Candidate and released package boundaries

| Boundary | Current status | Role |
| --- | --- | --- |
| `AsiBackbone.Core` | Released | Neutral governance primitives, decision contracts, acknowledgment, capability, decision receipt, emission contracts, outbox contracts, signing-ready seams, and verification policy. |
| `AsiBackbone.Storage.InMemory` | Released | Development, test, sample, and local-validation storage. |
| `AsiBackbone.EntityFrameworkCore` | Released | Host-owned EF Core persistence and durable local storage support. |
| `AsiBackbone.AspNetCore` | Released | ASP.NET Core host integration and hosted outbox drain support. |
| `AsiBackbone.Analyzers` | Released | Build-time governance safety rails. |
| `AsiBackbone.OpenTelemetry` | Released | Concrete OpenTelemetry governance-emission provider. |
| `AsiBackbone.Signing.LocalDevelopment` | Released | Local-development signing and verification proof path. |
| `AsiBackbone.Signing.ManagedKey` | Released | Managed-key signing adapter boundary with host-owned managed-key client. |
| `AsiBackbone.Streaming.EventHubs` | Design-only candidate | Future Event Hubs streaming adapter package. |
| `AsiBackbone.Governance.Purview` | Strategy-only candidate | Future Purview catalog, classification, and lineage enrichment adapter package. |
| `AsiBackbone.Observability.AzureMonitor` | Future or host-owned guidance | Azure Monitor is currently reached through host OpenTelemetry exporter configuration. |

Future package names may change. The architectural rule should not: provider packages depend on Core; Core never depends on provider packages.

## Related documentation

- [Released: OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [Design-Only: Event Hubs Governance Emission Provider](event-hubs-governance-emission-provider-design.md)
- [Strategy-Only: Purview Governance and Lineage Enrichment](purview-governance-lineage-enrichment-strategy.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Core Domain Language](core-domain-language.md)
- [EF Core Host Ownership and Migrations](ef-core-host-ownership-and-migrations.md)
- [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
- [API Baseline and Architecture Boundary Checks](api-baseline-and-boundary-checks.md)
