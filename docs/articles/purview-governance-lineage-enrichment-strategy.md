# Strategy-Only: Purview Governance and Lineage Enrichment

This article documents a **strategy-only** future Microsoft Purview governance and lineage enrichment direction. It is retained as planning guidance after the released `1.1.0 - Observability, Outbox, Signing, and Governance Emission Providers` package-family boundary.

Issue: #146.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine for consequential software decision flow. It is not an AI model host, robot controller, cloud governance platform, Purview catalog provider, SIEM product, signing product, or compliance guarantee by itself.

> [!IMPORTANT]
> This page does **not** document a released NuGet package. No Purview provider package, Purview SDK adapter, catalog-ingestion implementation, or Azure-specific enrichment package is included in the `1.1.0` stable package family.
>
> `AsiBackbone.OpenTelemetry` is the only concrete released governance emission provider package in `1.1.0`. Purview remains a future/strategy-only governance enrichment direction unless a later release separately reviews and ships it.
>
> See [1.1.0 Release Notes - Accepted deferrals](release-notes-110.md#accepted-deferrals) for the current release boundary.

> [!NOTE]
> Purview enrichment is not durable audit storage. It should add classification, lineage, catalog, and compliance context around selected governance events while the host-owned durable audit and outbox records remain the reliability and accountability baseline.

## Purpose

Microsoft Purview can help organizations understand where governed decisions touched sensitive assets, policy domains, classified resources, workflows, or lineage paths.

The Purview strategy should avoid turning every raw AsiBackbone decision into a first-class catalog asset. High-volume raw decision events belong in local audit/outbox storage and operational observability or streaming systems. Purview should receive summarized, classified, and PII-safe governance context when catalog or lineage enrichment adds value.

```text
Decision / acknowledgment / capability token / gateway result
  -> Decision receipt
  -> Durable local store / outbox
  -> Optional observability or streaming emission
  -> Future optional Purview governance and lineage enrichment
  -> Catalog, classification, lineage, compliance, or policy-context views
```

## Selected model

The recommended model is **selective summarized enrichment**.

Under this model, Purview integration should create or update governance enrichment records only for selected event classes, assets, workflows, or policy contexts.

| Pattern | Recommended use | Avoid using it for |
| --- | --- | --- |
| Process lineage event | A governance decision materially affects a data workflow, asset, model pipeline, administrative process, or external execution boundary. | Every allow/deny decision regardless of catalog relevance. |
| Compliance record | A decision is tied to a regulated workflow, retention requirement, approval policy, or high-risk action category. | Routine low-risk operational decisions. |
| Custom governance asset | The host needs a durable catalog object for a policy-governed workflow, gateway, data product, or decision boundary. | One custom asset per raw event. |
| Summarized governance record | The host wants aggregate policy/version/outcome context for a workflow, asset, or operation group. | Raw audit event replacement. |
| Classification or lineage annotation | The decision should enrich an existing asset, lineage edge, data product, process, or policy domain. | Duplicating all local decision receipt fields. |

The first implementation should favor summarized records and lineage annotations over cataloging every individual event.

## What Purview is good for

Purview is appropriate for:

* classification context;
* lineage enrichment;
* data asset and workflow governance context;
* compliance metadata;
* policy-domain mapping;
* discovery of where consequential decisions interact with sensitive resources;
* linking governance decisions to data products, process steps, gateway boundaries, or operational workflows;
* investigation workflows that need catalog context in addition to local audit records.

Purview is not the preferred place for:

* high-volume operational querying;
* raw audit event storage;
* every policy evaluator result;
* outbox retry state;
* detailed provider exception payloads;
* secrets, raw user content, prompts, protected records, or raw capability tokens;
* low-latency alerting or SIEM-style event search.

## Durable audit storage versus governance enrichment

| Concern | Primary location | Purview role |
| --- | --- | --- |
| Authoritative decision receipt | Host-owned audit store | Reference by safe identifiers only. |
| Outbox retry/dead-letter state | Host-owned outbox | Optional enrichment status reference only. |
| Operational query and alerting | OpenTelemetry, Azure Monitor, SIEM, logs, or stream processors | Optional catalog context for investigations. |
| Streaming and replay | Event Hubs or other streaming provider | Optional downstream consumer/enrichment target. |
| Classification and lineage context | Purview or host classifier/catalog | Primary enrichment value. |
| Policy version and schema version context | Decision receipt and envelope | Safe metadata copied into enrichment records. |

This separation keeps Purview useful without making it noisy, expensive, or misleading as the system of record.

## Candidate enrichment records

A Purview enrichment record should be a minimized, catalog-safe view of an AsiBackbone governance event or event group.

Recommended record categories:

| Record category | Description | Example |
| --- | --- | --- |
| Governance decision summary | Summarizes a decision tied to a workflow or asset. | A sensitive export request required acknowledgment and was approved under policy version `2026.06`. |
| Gateway execution lineage | Links a governed external execution to an asset, process, or workflow. | A deployment gateway executed after policy evaluation and capability-token validation. |
| Policy-context annotation | Adds policy version/hash, policy scope, and decision boundary metadata to a cataloged workflow. | A finance report export workflow used policy hash `abc123...`. |
| Compliance checkpoint | Marks that a high-risk action passed a required acknowledgment, escalation, or review checkpoint. | An administrative action required manual acknowledgment before execution. |
| Classification enrichment status | Records whether the governance event was classified, redacted, summarized, blocked, or quarantined before external enrichment. | A payload was blocked by DLP and no Purview enrichment was emitted. |

## Appropriate AsiBackbone fields for Purview

Use stable, minimized, and correlation-safe fields.

| Field | Purview usage | Guidance |
| --- | --- | --- |
| `CorrelationId` | Join investigations across audit, telemetry, stream, and catalog views. | Opaque workflow ID. |
| `AuditResidueId` | Link back to host-owned audit record. | Opaque ID only; no raw audit body. |
| `EventId` / `EnvelopeId` | Deduplication and enrichment tracking. | Opaque event identifiers. |
| `SchemaVersion` | Consumer compatibility and mapping version. | Required for enrichment records. |
| `EventType` | Category mapping. | Controlled vocabulary only. |
| `LifecycleStage` / `LifecycleStageSequence` | Workflow progression. | Useful for lineage and process context. |
| `PolicyVersion` / `PolicyHash` | Governance context. | Hash/version only; never raw policy content. |
| `DecisionStage` / `Outcome` | Summarized decision state. | Controlled values only. |
| `ReasonCode` family | Summarized rationale. | Prefer coarse reason-code family over detailed sensitive reasons. |
| `GatewayExecutionId` | External execution boundary correlation. | Opaque ID. |
| `CapabilityTokenId` | Grant correlation. | Opaque token ID only; never raw token. |
| `AcknowledgmentId` | Human acknowledgment correlation. | Opaque ID only. |
| `TraceId` / `SpanId` | Operational join with observability systems. | Include only if host policy permits. |
| `OperationName` / `PolicyScope` | Catalog context. | Coarse workflow or operation group. |
| `ClassificationState` | Enrichment safety state. | Controlled state such as classified, redacted, blocked, quarantined, failed. |

## PII-safe mapping rules

Purview enrichment must follow host-owned privacy, classification, and DLP policy before any record leaves the local boundary.

Recommended rules:

* Prefer opaque actor identifiers over usernames, emails, employee IDs, or subject IDs.
* Prefer asset, workflow, tenant, region, or operation-group codes over raw resource names when those names are sensitive.
* Prefer policy hashes and versions over raw policy text.
* Prefer reason-code families over detailed sensitive reason strings.
* Prefer summarized status values over raw provider error payloads.
* Use hash, tokenization, generalization, redaction, or omission for fields that could identify people, protected records, sensitive infrastructure, or confidential workflows.
* Do not emit enrichment if classification is unavailable and the host policy requires classification before external provider emission.

## What should not be sent to Purview by default

Do not send these values to Purview by default:

* raw capability tokens;
* secrets, connection strings, API keys, credentials, signing keys, or managed identity details;
* raw prompts, documents, request bodies, protected records, payload bodies, or user-submitted content;
* raw personal data unless a host explicitly opts into that behavior after classification and legal review;
* raw provider exception payloads;
* raw DLP or classification failure payloads;
* high-cardinality event streams where each raw event becomes a first-class Purview asset;
* internal deployment topology, private network names, or sensitive resource identifiers;
* unredacted policy text, constraint definitions, or confidential workflow logic.

When in doubt, preserve the full governance record locally and emit only a summarized enrichment record or no Purview record at all.

## Correlation strategy

Purview records should correlate with local audit, OpenTelemetry/Azure Monitor events, Event Hubs messages, and downstream governance processors through stable opaque identifiers.

```text
Host-owned decision receipt
  CorrelationId
  AuditResidueId
  SchemaVersion
  PolicyVersion / PolicyHash
        |
        +--> OpenTelemetry / Azure Monitor attributes
        |
        +--> Event Hubs message body/properties
        |
        +--> Future Purview summarized enrichment record
```

Recommended correlation fields:

| Correlation field | Local audit | OpenTelemetry / Azure Monitor | Event Hubs | Purview |
| --- | --- | --- | --- | --- |
| `CorrelationId` | Required when available | Attribute/log field | Message property | Enrichment property |
| `AuditResidueId` | Primary local reference | Attribute/log field | Message property | External reference only |
| `SchemaVersion` | Required | Attribute/log field | Message property and body | Enrichment schema field |
| `PolicyVersion` | Required when available | Attribute/log field | Message property | Governance property |
| `PolicyHash` | Required when available | Attribute/log field | Message property | Governance property |
| `EventType` | Required | Attribute/log field | Message property | Record category or property |
| `LifecycleStage` | Optional | Attribute/log field | Message property | Process/lineage stage |
| `GatewayExecutionId` | Optional | Attribute/log field | Message property | Gateway/process reference |

Purview should not need raw audit contents to participate in correlation. It should hold enough context to point an authorized investigator back to the host-owned audit store or observability system.

## Ingestion paths

Possible ingestion paths include:

| Path | Use case | Boundary |
| --- | --- | --- |
| Host-owned batch enrichment job | Periodic summarized enrichment for selected workflows or assets. | Safest first pattern. |
| Event Hubs downstream processor | Near-real-time enrichment from selected stream events. | Processor owns filtering and summarization before Purview. |
| Manual or review-triggered enrichment | High-risk or compliance-reviewed events. | Human or workflow approval controls catalog impact. |
| Future optional Purview provider package | Direct adapter from neutral envelope to Purview enrichment records. | Must depend on Core; Core must not depend on Purview SDKs/APIs. |

The recommended first strategy is not direct high-volume Purview ingestion. Start with a host-owned or optional provider enrichment job that can filter, summarize, classify, and deduplicate before writing catalog or lineage records.

## Catalog-noise and high-cardinality risks

Purview integration can become harmful if it catalogs too much.

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| One asset per event | Catalog becomes noisy and expensive. | Use summarized records, annotations, and workflow-level assets. |
| High-cardinality actor/resource fields | Search, lineage, and governance views become cluttered and privacy-sensitive. | Use opaque or generalized identifiers. |
| Raw decision streams in catalog | Purview becomes a poor operational log store. | Keep operational events in logs, OpenTelemetry, Azure Monitor, Event Hubs, or SIEM. |
| Unclassified payload enrichment | Sensitive data may leave the local boundary. | Require classification/minimization before enrichment. |
| Ambiguous system of record | Investigators may treat Purview as authoritative audit storage. | Store only references and summaries; document local audit as authoritative. |
| Policy leakage | Raw policies or constraints may expose security logic. | Use version/hash/scope only. |

## Failure behavior

Purview enrichment failure must not erase or invalidate the local audit record.

Recommended defaults:

| Failure condition | Recommended behavior |
| --- | --- |
| Purview unavailable | Preserve local audit/outbox record; retry enrichment if policy allows. |
| Classification unavailable | Defer or block enrichment unless host policy allows minimized safe summary emission. |
| DLP violation | Do not enrich Purview; mark outbox/enrichment state blocked or quarantined. |
| Purview rejects record | Normalize error, avoid repeated unsafe retries, and route to review or dead-letter according to host policy. |
| Correlation fields missing | Defer enrichment or emit only if host policy defines a safe fallback. |
| Payload too noisy or high-cardinality | Summarize, aggregate, annotate an existing workflow asset, or skip Purview enrichment. |

## Optional provider boundary

A future Purview integration package may be useful, but it should remain optional and outside Core.

Candidate package name:

```text
AsiBackbone.Governance.Purview
```

Acceptable alternatives:

```text
AsiBackbone.Azure.Purview
AsiBackbone.Purview
AsiBackbone.Lineage.Purview
```

These names are planning candidates only. They are not part of the released `1.1.0` package list.

A future package should depend on:

* `AsiBackbone.Core`;
* Purview SDKs/APIs or host abstractions required for catalog and lineage enrichment;
* Azure Identity support when Managed Identity or token credentials are enabled;
* `Microsoft.Extensions.Options` and logging abstractions if needed.

The package should not depend on:

* OpenTelemetry exporters;
* Azure Monitor or Log Analytics SDKs;
* Event Hubs SDKs unless the package explicitly includes a processor role;
* host-specific storage providers;
* signing or immutable-storage providers;
* robotics or AI model packages.

Core must remain usable with no Purview package installed.

## Test seams

The strategy should be testable without live Purview resources.

Recommended tests for a future provider or mapper:

* mapping creates a summarized enrichment record rather than a raw audit clone;
* mapping preserves `CorrelationId`, `AuditResidueId`, `SchemaVersion`, `PolicyVersion`, `PolicyHash`, event type, lifecycle stage, and gateway/capability/acknowledgment IDs when present;
* raw capability tokens, secrets, payload bodies, prompts, protected records, and raw personal data are excluded by default;
* high-cardinality fields are omitted, hashed, tokenized, or generalized according to mapper options;
* classification-blocked records do not produce Purview enrichment by default;
* Purview API failures normalize into provider-neutral emission/enrichment results;
* Core has no Purview SDK/API dependency;
* live Purview tests are optional, explicitly configured, and excluded from default CI.

## Implementation checklist

Before implementation begins, confirm:

* enrichment pattern: summarized record, process lineage event, compliance record, custom asset, annotation, or batch summary;
* package name and namespace;
* Purview SDK/API boundary;
* options shape and Managed Identity defaults;
* enrichment schema version and content type if serialized;
* required correlation fields;
* classification and DLP preconditions;
* high-cardinality field policy;
* failure mapping and retry/dead-letter behavior;
* outbox interaction expectations;
* tests that require no live Purview resources.

## Relationship to related issues

| Issue | Relationship |
| --- | --- |
| #140 Durable outbox | Purview enrichment should happen after local durable audit/outbox persistence. |
| #141 Lifecycle stages | Lifecycle stage and sequence can inform process lineage and compliance checkpoint records. |
| #142 Decision receipt telemetry | Trace, gateway, outbox, and PII-safe identifiers provide safe correlation fields. |
| #144 OpenTelemetry provider | OpenTelemetry remains the released `1.1.0` governance emission provider; Purview remains governance/catalog enrichment strategy. |
| #145 Event Hubs provider | Event Hubs can feed downstream processors that selectively summarize and enrich Purview records. |
| #149 Observability architecture | This strategy follows the Core-neutral provider package architecture. |
| #187 Governance emission contract | Purview enrichment should consume minimized, versioned governance emission envelopes or summaries. |
| #193 No-op outbox drain | The no-op proof path validates provider-neutral drain sequencing before provider-specific enrichment. |

## Related documentation

- [1.1.0 Release Notes - Accepted deferrals](release-notes-110.md#accepted-deferrals)
- [Released: OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [Design-Only: Event Hubs Governance Emission Provider](event-hubs-governance-emission-provider-design.md)
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [Audit Residue Observability Schema](audit-residue-observability-schema.md)
- [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)

## Non-goals

This strategy-only provider page does not implement:

* Purview ingestion code;
* a Purview NuGet package in the `1.1.0` stable package family;
* Purview catalog asset creation;
* Purview account, collection, classification, or lineage provisioning;
* OpenTelemetry, Azure Monitor, Log Analytics, or Event Hubs emission;
* SIEM-specific payloads;
* signing, timestamping, immutable storage, or tamper-evidence;
* replacement of durable local audit/outbox persistence;
* legal, compliance, retention, or data-residency guarantees.

Purview should remain an optional future governance enrichment layer for selected, classified, summarized, and correlated AsiBackbone governance context unless a later release separately implements and publishes it.
