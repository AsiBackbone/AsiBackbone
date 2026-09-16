# 1.1.0 Release Notes

These notes describe the released `1.1.0 - Observability, Outbox, Signing, and Governance Emission Providers` package-family boundary.

AsiBackbone is governance infrastructure for accountable software decision flow. It does not certify compliance, or provide production tamper-evidence by itself.

## Release summary

`1.1.0` is the current stable additive minor release over `1.0.0`. Existing `1.0.0` consumers can continue using the stable Core, in-memory storage, EF Core, and ASP.NET Core package surfaces without adopting the new analyzer, observability, outbox, signing, verification, or provider paths.

The release expands the governance spine from local decision/audit records into durable lifecycle, provider-neutral emission, optional OpenTelemetry projection, and signing-provider boundaries:

```text
Decision
  -> acknowledgment when required
  -> capability token when issued
  -> gateway or host execution boundary
  -> audit residue / lifecycle event
  -> durable local audit and outbox persistence
  -> optional signing / verification boundary
  -> optional provider emission
```

The durable local audit/outbox record is the reliability baseline. OpenTelemetry, Azure Monitor, Event Hubs, Purview, SIEMs, dashboards, and other downstream systems should be treated as optional projection or enrichment targets unless the host application explicitly designs them as an authoritative store.

Signing and verification are part of an operational trust model, not proof of tamper-evidence by themselves. Production tamper-evidence requires concrete signing, verification, protected key management, durable append-only or otherwise controlled storage, retention policy, monitoring, and incident response supplied by the host or provider environment.

## Stable package family

The released `1.1.0` package line covers the package family below.

| Package | Stable role |
| --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives, decisions, acknowledgments, capability-token references, audit residue, lifecycle events, provider-neutral emission contracts, durable outbox contracts, DLP/classification failure policy primitives, signing-ready metadata abstractions, canonical hashing/signing seams, and verification-policy primitives. |
| `AsiBackbone.Storage.InMemory` | Non-durable in-memory audit, lifecycle, and outbox helpers for tests, samples, local validation, and no-op proof paths. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and host-owned persistence for audit ledger, audit residue lifecycle, acknowledgment, and durable governance outbox records. |
| `AsiBackbone.AspNetCore` | ASP.NET Core host integration seams for service registration, request correlation, audit enrichment, HTTP result mapping, acknowledgment challenge helpers, endpoint governance, and hosted outbox drain integration. |
| `AsiBackbone.Analyzers` | Roslyn analyzer safety rails for governance persistence and continuation flows. Analyzer diagnostics are development/build-time guidance and do not enforce runtime behavior. |
| `AsiBackbone.OpenTelemetry` | Concrete OpenTelemetry governance emission provider that projects provider-neutral governance envelopes into .NET diagnostics primitives such as `ActivitySource` and `Meter`. |
| `AsiBackbone.Signing.LocalDevelopment` | Local-development RSA signing and verification provider for tests, samples, and host wiring proof paths. Not production key custody, managed-key signing, immutability, non-repudiation, or tamper-evidence. |
| `AsiBackbone.Signing.ManagedKey` | Provider-neutral managed-key signing adapter. The host supplies the actual managed-key client, credentials, key operations, verification path, monitoring, and operational policy. |

Future Event Hubs, Purview, Azure-specific, Azure Key Vault-specific, HSM-specific, gateway, robotics, or immutable-storage packages are not part of the `1.1.0` stable contract unless separately released as stable packages.

## Provider package boundary at a glance

A documentation page can describe a future provider direction without meaning a NuGet package has shipped. Use this table to distinguish released package documentation from design-only or strategy-only guidance.

| Area | `1.1.0` status | Documentation meaning |
| --- | --- | --- |
| OpenTelemetry governance emission | **Released package**: `AsiBackbone.OpenTelemetry`. | Concrete implemented provider package for projecting governance envelopes into .NET diagnostics. |
| Azure Monitor / Log Analytics | **Host-configured exporter guidance.** | Reach Azure Monitor through the host OpenTelemetry SDK/exporter pipeline; no Azure Monitor-specific AsiBackbone package is released. |
| Event Hubs governance emission | **Design-only future provider strategy.** | The Event Hubs page is planning guidance only; no Event Hubs NuGet package or Azure SDK adapter is released in `1.1.0`. |
| Purview governance and lineage enrichment | **Strategy-only future enrichment direction.** | The Purview page is planning guidance only; no Purview NuGet package or SDK adapter is released in `1.1.0`. |
| Signing provider packages | **Released provider boundaries.** | Local-development signing and managed-key adapter packages are released, but production trust guarantees remain host-owned. |

## API compatibility and stable review status

`1.1.0` is a backward-compatible minor release in the stable `1.x` line. The original `1.0.0` stable package family remains part of the compatible contract, and `1.1.0` adds stable package surfaces for analyzers, OpenTelemetry, local-development signing, and managed-key signing adapter boundaries.

The stable API review remains historical for the initial `1.0.0` package-family baseline, with a `1.1.0` addendum documenting the expanded stable package family. See [API Compatibility and SemVer](api-compatibility-and-semver.md) and [Historical Stable API Review](stable-api-review.md).

SemVer interpretation for this release:

- existing `1.0.0` consumers can upgrade without required source-code changes when they do not adopt new features;
- additive public APIs and new stable packages are allowed in a compatible minor release;
- stable provider packages released in `1.1.0` are part of the compatible `1.x` contract for those packages;
- future/design-only Event Hubs, Purview, Azure-specific SDK adapters, robotics, immutable-storage, and additional provider packages remain outside the stable contract unless separately reviewed and released;
- compatible patch releases such as `1.1.1` should keep `AssemblyVersion` at `1.0.0.0` while updating package `Version`, `FileVersion`, and `InformationalVersion`.

## What changed since 1.0.0

### Provider-neutral governance emission

Core now includes a provider-neutral governance emission contract so audit residue, lifecycle, gateway, and decision artifacts can be converted into stable governance emission envelopes without binding Core to OpenTelemetry, Azure Monitor, Event Hubs, Purview, SIEM, robotics, or cloud-provider SDK dependencies.

The key boundary is:

```text
IAsiBackboneGovernanceEmitter
  -> GovernanceEmissionEnvelope
  -> GovernanceEmissionResult
  -> GovernanceEmissionError
```

Providers adapt this contract into downstream systems. Core keeps the vocabulary neutral.

### Durable audit lifecycle and outbox persistence

`1.1.0` adds durable lifecycle and outbox concepts for preserving local accountability records before provider delivery is attempted.

The recommended sequence is:

1. Save the audit residue or lifecycle event locally.
2. Build a `GovernanceEmissionEnvelope`.
3. Enqueue the envelope into `IAsiBackboneGovernanceOutboxStore`.
4. Drain the outbox through an `IAsiBackboneGovernanceEmitter`.
5. Mark the outbox entry delivered, failed, retryable, deferred, or dead-lettered.

This avoids losing governance records when downstream providers are unavailable, rate-limited, misconfigured, or blocked by classification policy.

### In-memory proof paths

`AsiBackbone.Storage.InMemory` includes non-durable development and test helpers for lifecycle and outbox validation.

The no-op governance emitter and in-memory outbox path are intended for tests, samples, local smoke checks, and proof-of-wiring only. They are not durable production storage, not evidence of provider delivery, and not a substitute for EF Core or another host-owned durable store.

### EF Core durable adapter

`AsiBackbone.EntityFrameworkCore` adds host-owned durable persistence for governance outbox entries and audit residue lifecycle events.

The host application still owns:

- the `DbContext`;
- database provider;
- connection string;
- migrations;
- deployment;
- schema lifecycle;
- retention policy;
- backup and recovery;
- access controls.

The EF Core package contributes model configuration and storage adapters. It does not own the consuming application's database.

### Hosted governance outbox drain

`AsiBackbone.AspNetCore` adds hosted outbox drain integration for ASP.NET Core and generic-host applications.

The hosted worker can drain pending outbox entries through a registered provider-neutral emitter. Hosts configure enablement, batch size, polling interval, failure delay, shutdown behavior, stores, and concrete providers.

Hosts should avoid duplicate workers unless they intentionally design for multi-worker behavior and understand the storage/concurrency consequences.

### Endpoint governance

`AsiBackbone.AspNetCore` includes endpoint governance metadata and validation seams so hosts can attach governance intent to endpoints and validate endpoint-level policy metadata explicitly.

Endpoint governance remains a host adapter. It does not replace authentication, authorization, routing, middleware enforcement, UI, persistence, or execution controls.

### OpenTelemetry provider

`AsiBackbone.OpenTelemetry` is the first concrete governance emission provider package and the only concrete released governance emission provider package in `1.1.0`.

It implements `IAsiBackboneGovernanceEmitter` and projects governance envelopes into OpenTelemetry-friendly .NET diagnostics:

- `ActivitySource` activity events and tags;
- `Meter` counters and latency histograms;
- stable `asibackbone.*` attribute constants;
- provider-neutral delivered, failed, retryable, deferred, and dead-letter result behavior.

The provider does not configure exporters. It does not depend on Azure Monitor, Application Insights, Log Analytics, Event Hubs, Purview, Datadog, Grafana, Splunk, Elastic, SIEM, robotics, AI model, or cloud-provider SDK packages.

### Azure Monitor guidance

Azure Monitor should be reached through host-owned OpenTelemetry exporter configuration:

```text
AsiBackbone.OpenTelemetry
  -> ActivitySource / Meter
  -> host OpenTelemetry SDK pipeline
  -> host-configured Azure Monitor exporter
  -> Azure Monitor / Application Insights / Log Analytics
```

The OpenTelemetry provider should not hold Azure connection strings, instrumentation keys, workspace IDs, tenant IDs, or Azure SDK types. No Azure Monitor-specific AsiBackbone provider package is released in `1.1.0`.

### Design-only provider strategy pages

The documentation set includes design-only and strategy-only pages for future provider directions:

- [Design-Only: Event Hubs Governance Emission Provider](event-hubs-governance-emission-provider-design.md)
- [Strategy-Only: Purview Governance and Lineage Enrichment](purview-governance-lineage-enrichment-strategy.md)

Those pages preserve architectural direction and provider-boundary thinking, but they do not imply released NuGet packages, Azure SDK adapters, production support, or stable implementation contracts. Event Hubs and Purview remain accepted deferrals for `1.1.0` unless a future release separately reviews and ships them.

### Roslyn analyzer safety rails

`AsiBackbone.Analyzers` adds build-time analyzer safety rails for governance persistence and continuation flows.

Analyzer diagnostics should be treated as development-time feedback. They do not execute governance decisions at runtime, do not prove compliance, and do not replace tests, code review, runtime policy evaluation, or host-owned operational controls.

### DLP and classification failure behavior

Core includes provider-neutral DLP/classification failure policy primitives so hosts can decide how governance emission should behave when classification is unavailable, times out, returns indeterminate results, blocks a payload, or reports a classified result.

Hosts should explicitly choose risk-sensitive behavior such as fail-open, fail-closed, defer, require acknowledgment, or escalate. Sensitive or unclassified payloads should not be emitted to external providers merely because a classifier is unavailable.

### Signing-ready receipts and verification seams

Core includes signing-ready abstractions, canonical hashing/signing seams, signing metadata fields, and verification-policy primitives for audit receipts and downstream verification flows.

Accurate wording:

- records can carry signing-ready metadata;
- artifact hashes can be signed through a configured provider package or host-owned signing service;
- verification can classify signatures as valid, invalid, missing, unavailable, unsupported, revoked, or otherwise policy-relevant according to the host's verification policy;
- signed does not mean verified;
- verified does not mean tamper-evident unless the deployed storage, retention, key-management, chain/anchor, monitoring, and operational controls support that claim.

### Local-development signing provider

`AsiBackbone.Signing.LocalDevelopment` provides a local-development RSA signing and verification provider.

It is intended for:

- tests;
- samples;
- deterministic local validation;
- host wiring proof paths;
- documentation examples.

It is not a production managed-key provider and does not provide protected key custody, immutable storage, legal non-repudiation, compliance certification, or production tamper-evidence by itself.

### Managed-key signing adapter

`AsiBackbone.Signing.ManagedKey` provides a provider-neutral managed-key signing adapter.

The package supplies the adapter boundary and registration shape. The host supplies the actual managed-key client. That host-owned client may call Azure Key Vault, Managed HSM, cloud KMS, HSM appliances, or organization-owned signing services, but those concrete integrations are not included by default.

The managed-key adapter must not return private keys, symmetric keys, connection strings, or raw credential material to Core.

## Quality and coverage posture

The `1.1.0` release is validated through normal tests, release-validation workflows, generated package checks, external consumer smoke tests, repository-wide line coverage, Core-only branch coverage, and targeted mutation reports.

Post-`1.1.0` / `1.1.1` quality hardening focuses on raising meaningful Core line and branch coverage around the expanded Core surface, including capability grant validation, signing and verification policy, canonical payload building, governance emission, durable outbox objects, and DLP/classification policy behavior.

That coverage-hardening work is separate from mutation-testing scope expansion. Mutation testing remains a targeted quality signal for selected high-value governance behavior, not a full-repository certification. See [Quality Reports](../quality/index.md) and [Mutation Coverage Scope and Deferrals](../quality/mutation-coverage-scope.md) for the current distinction between coverage gates, branch hardening, and mutation scope.

## SemVer and compatibility

`1.1.0` is SemVer-compatible with `1.0.0` consumers and expands the stable package family for the compatible `1.x` line.

Compatibility expectations:

- existing `1.0.0` package references can be upgraded to `1.1.0` without required source-code changes for consumers that do not use new features;
- new public APIs are additive;
- new stable packages in `1.1.0` are covered by their own package-specific stable contracts;
- analyzer adoption is optional and should not be required for runtime use;
- signing-provider adoption is optional;
- new persisted fields and schema additions should be treated as additive migration work owned by the host;
- preview, design-only, strategy-only, or future provider packages remain outside the stable compatibility promise until separately released as stable.

Hosts using EF Core should still review generated migrations before deployment because durable outbox and lifecycle records add storage surfaces that the host owns.

See [API Compatibility and SemVer](api-compatibility-and-semver.md) and [Historical Stable API Review](stable-api-review.md) for the stable `1.x` compatibility policy, package-family expansion, and `AssemblyVersion` guidance.

## Accepted deferrals

The following work is intentionally deferred or documentation/design-only for this released boundary:

| Area | `1.1.0` status |
| --- | --- |
| Event Hubs | Design documentation only. No Event Hubs SDK dependency, Event Hubs NuGet package, Azure Event Hubs adapter, or implementation package is included in the stable package family. |
| Purview | Governance and lineage enrichment strategy documentation only. No Purview SDK dependency, Purview NuGet package, catalog-ingestion adapter, or implementation package is included in the stable package family. |
| Azure Monitor | Supported through host-configured OpenTelemetry exporter guidance. No Azure Monitor-specific package is included. |
| Azure Key Vault / Managed HSM / cloud KMS | Not implemented directly. The managed-key adapter requires a host-owned client and does not ship live cloud SDK integration by default. |
| Production tamper evidence | Not claimed by default. Requires concrete signing, verification, storage, retention, key-management, monitoring, and operational controls implemented by the host or a future provider package. |
| Immutable storage / external anchoring | Not implemented. Hash-chain or signing metadata should not be described as immutable or externally anchored unless a concrete storage/anchoring design is deployed. |
| Robotics / physical execution | Not implemented. Robotics remains a later gateway/scenario area and does not change the software package boundary. |

## Upgrade guidance

See [Upgrade Guide: 1.0.0 to 1.1.0](upgrade-100-to-110.md) for step-by-step guidance.

At a high level:

1. Upgrade existing stable packages from `1.0.0` to `1.1.0`.
2. Add `AsiBackbone.Analyzers` only when build-time diagnostics are desired.
3. Add `AsiBackbone.OpenTelemetry` only if provider emission is needed.
4. Prefer durable EF Core or another durable host-owned outbox store before provider emission.
5. Use in-memory stores and no-op emitters only for tests, samples, and local validation.
6. Use `AsiBackbone.Signing.LocalDevelopment` only for local development, tests, samples, or proof paths.
7. Use `AsiBackbone.Signing.ManagedKey` only when the host supplies a managed-key client, credentials, key identity, failure policy, monitoring, and verification plan.
8. Configure Azure Monitor through the host OpenTelemetry pipeline if Azure Monitor is the selected backend.
9. Treat Event Hubs and Purview pages as future design/strategy guidance only unless a later release publishes concrete provider packages.
10. Do not claim signing, immutability, non-repudiation, or tamper-evidence unless a concrete signing, verification, storage, retention, and key-management design is actually implemented.

## Validation record and reusable commands

The `1.1.0` release boundary is documented by this release note, the historical release readiness record, and the reusable [Stable Release Validation](release-validation.md) process.

For maintenance validation, follow-up release candidates, or package-shape checks, run validation from a clean working tree and capture results in the relevant release PR or maintenance checklist.

Recommended commands:

```powershell
dotnet restore AsiBackbone.slnx
dotnet format AsiBackbone.slnx --verify-no-changes --verbosity minimal
dotnet build AsiBackbone.slnx -c Release
dotnet test AsiBackbone.slnx -c Release --no-build --no-restore
dotnet tool restore
dotnet tool run docfx -- docs/docfx.json
```

Package validation should also pack and validate the expected package artifacts, including Core, Storage.InMemory, EntityFrameworkCore, AspNetCore, Analyzers, OpenTelemetry, Signing.LocalDevelopment, and Signing.ManagedKey.

For future releases, rerun validation against the final release candidate before tagging. Do not rely on older milestone test counts once signing, analyzer, endpoint-governance, or package-boundary changes have landed.

## Related documentation

- [Upgrade Guide: 1.0.0 to 1.1.0](upgrade-100-to-110.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Historical Stable API Review](stable-api-review.md)
- [Quality Reports](../quality/index.md)
- [Mutation Coverage Scope and Deferrals](../quality/mutation-coverage-scope.md)
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [Released: OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [Design-Only: Event Hubs Governance Emission Provider](event-hubs-governance-emission-provider-design.md)
- [Strategy-Only: Purview Governance and Lineage Enrichment](purview-governance-lineage-enrichment-strategy.md)
- [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
- [Key Rotation and Retired-Key Verification](key-rotation-and-retired-key-verification.md)
- [Capability Grant Hardening](capability-grant-hardening.md)
