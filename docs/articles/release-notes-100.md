# 1.0.0 Release Notes

These notes describe the first stable release boundary for AsiBackbone. They are written for the `1.0.0` release and align with the repository version metadata, citation metadata, Zenodo metadata, and `v1.0.0` tag path.

AsiBackbone is governance infrastructure for accountable decision flow. It does not guarantee legal or regulatory compliance.

## Release identity

| Field | Value |
| --- | --- |
| Package version | `1.0.0` |
| Release tag | `v1.0.0` |
| Assembly version | `1.0.0.0` |
| File version | `1.0.0.0` |
| Citation version | `1.0.0` |
| Zenodo metadata version | `1.0.0` |

No preview or `-alpha` suffix is expected for the stable package artifacts.

## Release summary

`1.0.0` stabilizes the initial package family around a practical governance spine:

```text
Intent or request
  -> policy context
  -> constraint evaluation
  -> governance decision
  -> optional acknowledgment
  -> audit residue or ledger record
  -> optional capability boundary
  -> host-owned execution
```

The stable release is intended to make the implemented package surface safer for downstream consumers while keeping host ownership explicit.

## Stable package family

The `1.0.0` stable package line covers the implemented packages that complete the stable API review and release checklist.

| Package | Stable role |
| --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives, decisions, constraints, audit contracts, acknowledgment contracts, capability-token abstractions, and operation results. |
| `AsiBackbone.Storage.InMemory` | Non-durable in-memory audit sink and ledger helpers for tests, samples, and local validation. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and audit ledger persistence through a host-owned `DbContext`. |
| `AsiBackbone.AspNetCore` | Thin ASP.NET Core host adapters for actor context, request correlation, HTTP result mapping, and acknowledgment challenge support. |

Future or provider-specific packages are not automatically part of the `1.0.0` stable contract unless they are explicitly released as stable.

## Important changes since the initial alpha line

### Core governance primitives

The Core package now provides the shared public language for governed decision flow:

- actor context primitives for human, service, agent, system, and unknown actors;
- constraint evaluation contracts and result shapes;
- governance decision outcomes for allowed, warning, denied, deferred, acknowledgment-required, and escalation-recommended states;
- operation result primitives with reason codes and warnings;
- audit residue contracts and decision-derived audit residue helpers;
- audit ledger record shape and storage contract;
- acknowledgment and responsibility-handshake primitives;
- capability-token abstractions for scoped, time-bound permission boundaries;
- policy version, policy hash, correlation ID, trace ID, and schema-version fields where relevant.

### Policy evaluator pipeline

The default evaluator provides a framework-neutral way to compose constraint results into governance decisions. Hosts can define their own constraints and optionally apply a decision policy after constraint composition.

The evaluator remains independent of ASP.NET Core, EF Core, AI model packages, robotics packages, and host templates.

### Storage and persistence

The package family now includes two different storage-related paths:

- `Storage.InMemory` for non-durable tests, samples, and local validation;
- `EntityFrameworkCore` for host-owned persistence using EF Core model configuration and ledger storage helpers.

The EF Core package does not own the host database. The host owns the `DbContext`, provider, connection string, migrations, deployment, retention, access controls, and operational lifecycle.

### ASP.NET Core adapters

The ASP.NET Core package provides thin host adapters for web applications. It helps translate HTTP request context into Core-compatible governance context and translate Core outcomes into HTTP-friendly shapes when explicitly used by the host.

It does not register authentication, authorization, MVC, Razor Pages, Minimal API endpoints, EF Core, policy evaluators, persistence stores, middleware enforcement, UI rendering, or external execution behavior by default.

### Documentation and release boundary

The documentation now includes stable-release guidance for:

- getting started with the implemented package family;
- the `1.0.0` quickstart path;
- release readiness and release validation checks;
- API compatibility and Semantic Versioning expectations;
- schema versioning for stable serialized and persisted records;
- privacy and signing boundaries;
- EF Core host ownership and migrations;
- ASP.NET Core host integration boundaries;
- external consumer smoke-test validation.

### External consumer validation

The release path includes package-shaped smoke validation. The smoke tests pack local package artifacts, create a clean external xUnit project, install the packages from a local NuGet source, and verify that a consumer-style host can wire the package family without repository project references.

This validation is a package-consumer confidence check. It is not a production host template.

## Package and API notes

### Compatibility promise

After `1.0.0`, stable packages should follow Semantic Versioning:

- patch releases fix defects without intentionally breaking stable public APIs;
- minor releases may add compatible APIs, options, adapters, and behavior;
- major releases are reserved for breaking changes;
- preview packages or preview APIs may change before being promoted to stable.

See [API Compatibility and SemVer](api-compatibility-and-semver.md) for the full compatibility statement.

### Stable API surface

The stable API surface includes public types, public members, documented extension points, documented service-registration methods, and stable persisted or serialized artifact shapes that are explicitly documented as stable.

Internal implementation details, samples, generated smoke-test hosts, local validation scripts, and preview provider packages are not part of the same compatibility promise unless they are explicitly documented as stable.

### Schema versioning

Stable persisted and serialized records should carry schema-version guidance where needed. Consumers should avoid assuming that future fields will never be added.

See [Schema Versioning](schema-versioning.md) for durable artifact guidance.

### Privacy and signing boundaries

The `1.0.0` shape includes signing-ready fields such as policy version, policy hash, schema version, timestamps, correlation IDs, and record IDs. These fields support future signing and verification providers, but they do not create cryptographic protection by themselves.

`1.0.0` does not provide built-in signing, key management, tamper-evident storage, privacy classification, or compliance guarantees.

See [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md) before using audit records or metadata in production systems.

## Known limitations

The first stable release intentionally keeps several responsibilities outside the package boundary:

- no built-in AI model hosting, training, inference, or orchestration;
- no robot or physical-system control implementation;
- no built-in signing provider or key-management provider;
- no built-in tamper-evident ledger or immutable storage provider;
- no package-owned database lifecycle or migrations;
- no automatic metadata classification, redaction, tokenization, encryption, or privacy scanning;
- no legal, regulatory, audit-framework, or compliance certification;
- no replacement for host authentication, authorization, claims transformation, or endpoint protection;
- no automatic enforcement middleware that executes or blocks host operations without host code;
- no guarantee that in-memory storage is durable or production-safe.

These are intentional boundaries, not omissions to hide. The package is designed to keep consequential execution and operational policy under host control.

## Planned follow-up work

Later milestones may include:

- signing and verification provider packages;
- key-management or certificate-provider integration;
- durable outbox support before external emission;
- cloud observability and governance enrichment providers;
- additional gateway/provider packages;
- richer sample hosts and deployment examples;
- provider-specific privacy or metadata review hooks;
- expanded external consumer validation;
- additional documentation for migration and upgrade paths;
- robotics or physical execution examples only after software gateway patterns are mature.

Later provider work should remain separate from the `1.0.0` stable contract until each provider completes its own API review, package-boundary review, documentation, and release checklist.

## Upgrade notes from alpha packages

Consumers moving from alpha versions should review:

- namespace and package references;
- documented public API names;
- host-owned EF Core setup and migrations;
- ASP.NET Core service registration and options;
- audit record schema-version expectations;
- metadata privacy and signing boundaries;
- external consumer smoke-test guidance.

Because alpha packages may have changed before `1.0.0`, consumers should test upgrades in a development environment before using the stable package line in production.

## Release readiness checklist

Before publishing the final stable release, confirm the detailed [1.0.0 Release Readiness Checklist](release-readiness-checklist.md), including:

- stable API review is complete or intentionally deferred with follow-up issues;
- package versions and release notes agree;
- the stable package list is final;
- public documentation links resolve;
- external consumer smoke tests pass;
- release artifacts build and pack successfully;
- generated `.nupkg` metadata validation passes;
- schema-version guidance is present for stable persisted artifacts;
- privacy and signing boundaries are documented;
- known limitations are included in release notes;
- later provider work is not implied to be part of the `1.0.0` stable contract.

## Related documentation

- [1.0.0 Quickstart](quickstart-100.md)
- [1.0.0 Release Readiness Checklist](release-readiness-checklist.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Schema Versioning](schema-versioning.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [EF Core Host Ownership and Migrations](ef-core-host-ownership-and-migrations.md)
- [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
