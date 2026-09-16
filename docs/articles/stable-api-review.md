# Historical Stable API Review

This review records the public API review for the initial stable AsiBackbone package family before the `1.0.0` surface was established. It is retained as a historical API-shape baseline for the original compatible `1.x` line and the `1.1.0` additive package-family expansion.

Current package status is documented in [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md), [3.0.0 Release Notes](release-notes-300.md), and the current release-readiness record. This page is intentionally historical and should not be read as the current stable package-family index.

In this software project, **ASI** means **Accountable Systems Infrastructure**. This review is limited to implemented .NET package APIs and does not treat AsiBackbone as an artificial superintelligence implementation, AI model host, legal/compliance guarantee, or robot controller.

## Review status

| Item | Status |
| --- | --- |
| Public API review documented | Complete for the initial `1.0.0` stable surface. |
| Required breaking changes identified | None identified in the initial `1.0.0` review. |
| Package boundaries reviewed | Complete for the initial package family; `1.1.0` stable package-family addendum recorded below. |
| Dependency direction reviewed | Complete for the initial package family and still valid as historical boundary context. |
| Stable release dependency | Historical: the `1.0.0` tag was not to be cut until release validation passed and later review findings were resolved or intentionally deferred. For current releases, use [Stable Release Validation](release-validation.md). |

## Reviewed package family for `1.0.0`

The initial stable package family reviewed here is:

| Package | Reviewed role | Boundary decision |
| --- | --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives, actor context, constraints, decision results, audit contracts, acknowledgment primitives, capability-token abstractions, and operation results. | Stable candidate. Should remain independent of ASP.NET Core, EF Core, cloud, robotics, AI model, and host-template dependencies. |
| `AsiBackbone.Storage.InMemory` | Non-durable in-memory storage helpers for tests, samples, and local validation. | Stable candidate with a clear non-durable boundary. Should not be described as production storage. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and host-owned persistence integration. | Stable candidate as host-owned EF Core integration. Should not own migrations, database lifecycle, provider selection, or retention policy. |
| `AsiBackbone.AspNetCore` | Thin ASP.NET Core host adapters for actor context, request correlation, result mapping, and acknowledgment challenge support. | Stable candidate as host integration. Should not become the owner of host authentication, authorization, routing, policy evaluation, persistence, UI, or execution behavior. |

## `1.1.0` stable API review addendum

The `1.1.0` stable package family is an additive compatible expansion over the `1.0.0` baseline.

| Package | `1.1.0` stable role | Boundary decision |
| --- | --- | --- |
| `AsiBackbone.Core` | Adds provider-neutral governance emission contracts, durable outbox contracts, DLP/classification policy primitives, signing-ready metadata abstractions, canonical hashing/signing seams, verification-policy primitives, lifecycle events, and expanded audit/outbox vocabulary. | Stable additive Core expansion. Core must remain framework-neutral and provider-neutral. |
| `AsiBackbone.Storage.InMemory` | Adds non-durable lifecycle and outbox proof paths for tests, samples, local validation, and no-op proof flows. | Stable non-durable helper boundary. Not production storage. |
| `AsiBackbone.EntityFrameworkCore` | Adds host-owned persistence integration for decision receipt lifecycle and governance outbox records. | Stable EF Core integration boundary. Host still owns `DbContext`, provider, migrations, connection string, deployment, and retention. |
| `AsiBackbone.AspNetCore` | Adds endpoint governance and hosted outbox drain integration. | Stable ASP.NET Core host adapter boundary. Does not replace authentication, authorization, routing, persistence, UI, or execution controls. |
| `AsiBackbone.Analyzers` | Provides Roslyn analyzer safety rails for governance persistence and continuation flows. | Stable build-time guidance. Analyzer diagnostics are not runtime enforcement. |
| `AsiBackbone.OpenTelemetry` | Provides the concrete OpenTelemetry governance emission provider package. | Stable provider package. Projects provider-neutral governance envelopes into .NET diagnostics while exporter configuration remains host-owned. |
| `AsiBackbone.Signing.LocalDevelopment` | Provides local-development signing and verification for tests, samples, and wiring proof paths. | Stable local-only provider. Not production key custody, managed-key signing, immutability, non-repudiation, or tamper-evidence. |
| `AsiBackbone.Signing.ManagedKey` | Provides a managed-key signing adapter boundary. | Stable adapter boundary. The host supplies the managed-key client, credentials, key operations, monitoring, verification plan, and operational policy. |

The `1.1.0` addendum does not bring future Event Hubs, Purview, Azure-specific SDK adapters, robotics, immutable storage, external anchoring, Azure Key Vault-specific implementation packages, HSM-specific implementation packages, or other future providers into the stable contract. Those must complete their own review before being documented as stable packages.

## Historical `1.2.x` package-family note

The `1.2.x` package family carried forward the `1.0.0` stable baseline and the `1.1.0` additive package-family expansion.

Additional `1.2.x` package surfaces such as `AsiBackbone.DependencyInjection`, `AsiBackbone.Testing`, and `AsiBackbone.Templates` should be treated as stable only within the release lines where they were explicitly documented, validated, and released.

This historical API review remains the baseline record for the initial stable API shape and the `1.1.0` addendum, while current package status should be read from the current release documentation.

## Public naming review

The reviewed public naming pattern was acceptable for `1.0.0` and remains useful historical context:

- package names consistently use `AsiBackbone.*`;
- namespaces mirror package boundaries;
- public host integration types use the `AsiBackbone` prefix where they are package-specific;
- Core domain types such as `GovernanceDecision`, `OperationResult`, `DecisionReceipt`, and `AuditLedgerRecord` are acceptable without repeating the prefix because they are already under the `AsiBackbone.Core` namespace;
- ASP.NET Core, EF Core, OpenTelemetry, analyzer, and signing-provider types carry package-specific names where ambiguity is likely;
- extension method names are readable and host-oriented, such as `AddAsiBackboneAspNetCore`, `AddAsiBackboneOpenTelemetryGovernanceEmission`, and `ApplyAsiBackboneConfigurations`.

No required renames were identified for the stable `1.x` documentation review.

## Namespace review

The namespace layout was acceptable for `1.0.0` and is corrected here for the public API shape known at the time of the historical review:

| Namespace area | Review result |
| --- | --- |
| `AsiBackbone.Core.Actors` | Clear home for framework-neutral actor context abstractions and defaults. |
| `AsiBackbone.Core.Constraints` | Clear home for policy constraint abstractions and evaluation results. |
| `AsiBackbone.Core.Decisions` | Clear home for composed governance decisions and outcomes. |
| `AsiBackbone.Core.Evaluation` | Clear home for evaluator and decision policy contracts. |
| `AsiBackbone.Core.Audit` | Clear home for decision receipt, ledger records, lifecycle events, and ledger store contracts. |
| `AsiBackbone.Core.Handshakes` | Clear home for acknowledgment and responsibility-handshake primitives. |
| `AsiBackbone.Core.CapabilityTokens` | Clear home for capability token abstractions. |
| `AsiBackbone.Core.Emissions` | Clear home for provider-neutral governance emission contracts and envelopes. |
| `AsiBackbone.Core.Outbox` | Clear home for provider-neutral durable outbox contracts and records. |
| `AsiBackbone.Core.Classification` | Clear home for DLP/classification policy primitives. |
| `AsiBackbone.Core.Signing` | Clear home for signing-ready metadata, canonical hashing/signing seams, and verification policy primitives. |
| `AsiBackbone.Storage.InMemory` | Clear storage-provider package boundary for non-durable local validation. |
| `AsiBackbone.EntityFrameworkCore` | Clear EF Core integration boundary. |
| `AsiBackbone.AspNetCore` | Clear ASP.NET Core host adapter boundary. |
| `AsiBackbone.Analyzers` | Clear analyzer package boundary for build-time diagnostics. |
| `AsiBackbone.OpenTelemetry` | Clear OpenTelemetry provider package boundary. |
| `AsiBackbone.Signing.LocalDevelopment` | Clear local-development signing provider boundary. |
| `AsiBackbone.Signing.ManagedKey` | Clear managed-key signing adapter boundary. |

The stale `AsiBackbone.Core.Tokens` reference from the historical review is corrected to `AsiBackbone.Core.CapabilityTokens`.

## Dependency direction review

The dependency direction was acceptable for the initial stable package family and remains the rule for compatible package additions:

```text
AsiBackbone.Core
  <- AsiBackbone.Storage.InMemory
  <- AsiBackbone.EntityFrameworkCore
  <- AsiBackbone.AspNetCore
  <- AsiBackbone.Analyzers
  <- AsiBackbone.OpenTelemetry
  <- AsiBackbone.Signing.LocalDevelopment
  <- AsiBackbone.Signing.ManagedKey
```

Review notes:

- Core remains the root package and should not reference integration packages.
- In-memory storage depends on Core only.
- EF Core integration depends on Core and EF Core packages.
- ASP.NET Core integration depends on Core and ASP.NET Core framework services.
- Analyzer, OpenTelemetry, and signing-provider packages depend on Core and their own package-specific dependencies only.
- No reviewed package requires a dependency on a future Event Hubs provider, Purview provider, Azure Monitor-specific provider, robotics package, AI-model package, immutable-storage package, or external-anchoring package.

No dependency-direction breaking changes were identified.

## Extension point review

The reviewed extension points were acceptable for `1.0.0`:

| Extension point | Review result |
| --- | --- |
| `IGovernanceActorContext` | Acceptable framework-neutral actor abstraction. Keeps host identity/authentication ownership explicit. |
| `IGovernanceConstraint<TContext>` | Acceptable host-extensible constraint abstraction with async evaluation and cancellation support. |
| `IGovernancePolicyEvaluator<TContext>` | Acceptable composition point for converting constraint output into governance decisions. |
| `IGovernanceDecisionPolicy` | Acceptable customization point for post-composition decision policy. |
| `IDecisionReceiptSink` | Acceptable minimal audit emission contract. |
| `IGovernanceAuditLedgerStore` | Acceptable framework-neutral persisted ledger store contract. |
| ASP.NET Core actor/correlation/challenge services | Acceptable host adapter contracts. They should remain adapters, not host policy owners. |
| `AddAsiBackboneAspNetCore` | Acceptable service-registration entry point. |
| `ApplyAsiBackboneConfigurations` | Acceptable EF Core model configuration entry point for host-owned DbContexts. |

The `1.1.0` addendum recognizes additional stable extension seams:

| Extension point | Review result |
| --- | --- |
| `IGovernanceEmitter` | Acceptable provider-neutral governance emission seam. Concrete providers adapt this contract downstream. |
| `IGovernanceOutboxStore` | Acceptable provider-neutral durable outbox seam. Host/storage packages own persistence behavior. |
| `GovernanceOutboxDrain` | Acceptable provider-neutral drain helper. Concrete emission provider remains replaceable. |
| OpenTelemetry governance emitter registration | Acceptable provider package registration boundary. Exporter configuration remains host-owned. |
| Signing provider abstractions and verification policy seams | Acceptable provider-neutral trust-boundary seams. They do not imply production tamper-evidence by themselves. |
| Managed-key signing adapter boundary | Acceptable adapter boundary when the host supplies the actual managed-key client and operational policy. |

No required breaking changes were identified.

## Artifact and schema review

Stable persisted or exported artifacts should remain covered by schema-version guidance.

Reviewed artifact families include:

- decision receipt;
- audit ledger records;
- decision receipt lifecycle events;
- acknowledgment and responsibility-handshake records;
- capability token records;
- governance emission envelopes and payload descriptors;
- governance outbox records;
- signing-ready metadata and canonical payload/hash artifacts;
- policy version, policy hash, correlation ID, trace ID, actor ID, record ID, and schema-version fields.

Review decision:

- the stable direction remains acceptable because durable artifacts are documented separately from package versioning;
- future additive fields should remain compatible where possible;
- future incompatible durable shape changes should use schema-version guidance and migration documentation;
- signing and verification seams are stable, but production key management, immutable storage, external anchoring, and compliance certification remain host-owned or future-provider concerns unless separately implemented and released.

## Findings

### Required breaking changes

None identified in this historical review.

### Non-blocking follow-up considerations

The following were not required before `1.0.0`, but should remain visible for later milestones:

- Core architecture boundary checks are tracked by [API Baseline and Architecture Boundary Checks](api-baseline-and-boundary-checks.md) and should continue to protect Core from integration/provider dependencies;
- generated public API baseline files remain explicitly deferred to a later milestone so the project can choose one stable public API drift process;
- consider adding package-specific API review notes for future provider packages before those packages become stable;
- consider adding upgrade notes if any later provider package introduces stable serialized artifacts;
- keep release notes and package READMEs aligned when new stable packages are added.

For future release lines, Event Hubs, Purview, Azure-specific SDK adapters, robotics, immutable-storage, timestamping, or external-anchoring packages should remain design-only, strategy-only, preview, or sample-only until they complete their own stable package review.

## Release decision

The initial stable package family was acceptable to proceed toward `1.0.0` from an API-shape perspective, provided release validation passed and any new API concerns discovered before tagging were resolved or intentionally deferred with follow-up issues.

The `1.1.0` stable package-family expansion is additive and compatible with the original `1.0.0` package-family boundary. Its added stable package surfaces should be treated as part of the compatible contract once released.

This review does not replace CI, tests, package smoke validation, DocFX validation, or release version checks. It documents the historical stable public API and package-boundary review for issue #13 plus the `1.1.0` stable package-family addendum.

## Related documentation

- [3.0.0 Release Notes](release-notes-300.md)
- [2.3.0 Release Notes](release-notes-230.md)
- [1.2.1 Release Notes](release-notes-121.md)
- [1.2.0 Release Notes](release-notes-120.md)
- [1.1.x Release Notes](release-notes-110.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Historical Upgrade Guide: 1.0.0 to 1.1.0](upgrade-100-to-110.md)
- [Release Validation](release-validation.md)
- [API Baseline and Architecture Boundary Checks](api-baseline-and-boundary-checks.md)
- [Schema Versioning](schema-versioning.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [Developer Checklist](developer-checklist.md)
