# 1.2.1 Release Notes

These notes describe the `1.2.1 - Release Metadata, Source Link, and Validation Hardening` package-family patch boundary.

AsiBackbone is governance infrastructure for accountable software decision flow. It does not certify compliance, or provide production tamper-evidence by itself.

## Release summary

`1.2.1` is a backward-compatible patch release for the stable `1.2.x` line. It preserves the `1.2.0` package/API boundary while tightening release metadata, Source Link repository-commit metadata, package-signing wording, workflow hygiene, and validation guidance.

The release keeps the implementation-first path introduced in `1.2.0`:

```text
Host request
  -> host-selected provider registration
  -> endpoint governance metadata or explicit exclusion
  -> policy evaluation and decision receipt
  -> optional acknowledgment/capability continuation
  -> host-owned execution boundary
  -> local audit/outbox record
  -> optional provider projection
```

`AssemblyVersion` remains fixed at `1.0.0.0` for the compatible stable `1.x` line. Package `Version`, `FileVersion`, and `InformationalVersion` move to `1.2.1`.

## Stable package family

The released `1.2.1` package line covers the same package family as `1.2.0`.

| Package | Stable role |
| --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives: decisions, constraints, acknowledgments, audit residue, lifecycle events, capability-token abstractions, durable outbox contracts, provider-neutral emission contracts, DLP/classification policy primitives, signing-ready metadata, canonical hashing/signing seams, and verification-policy primitives. |
| `AsiBackbone.DependencyInjection` | Explicit `AddAsiBackbone(...)` builder facade for coordinating host-selected provider registrations without making Core own infrastructure. |
| `AsiBackbone.Storage.InMemory` | Non-durable in-memory storage helpers for tests, samples, local validation, lifecycle events, and outbox proof paths. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and host-owned persistence for audit ledger, acknowledgments, lifecycle events, and governance outbox records. |
| `AsiBackbone.AspNetCore` | ASP.NET Core host adapters for actor context, request correlation, audit enrichment, HTTP result mapping, acknowledgment challenge flows, endpoint governance, development diagnostics, and hosted outbox drain integration. |
| `AsiBackbone.Testing` | Test-only harness helpers for deterministic endpoint governance, policy results, capability validation, in-memory audit inspection, non-durable outbox storage, and no-signature signing seams. |
| `AsiBackbone.Templates` | `dotnet new` templates for generating governed ASP.NET Core host scaffolds with endpoint governance, sample policies, local in-memory audit inspection, analyzers, and README guidance. |
| `AsiBackbone.Analyzers` | Roslyn analyzer safety rails for governance persistence, continuation flows, and production-signing configuration mistakes. |
| `AsiBackbone.OpenTelemetry` | Released OpenTelemetry governance emission provider that projects provider-neutral envelopes into .NET diagnostics. |
| `AsiBackbone.Signing.LocalDevelopment` | Local-development signing and verification for tests, samples, and wiring proof paths only. Not for production key custody. |
| `AsiBackbone.Signing.ManagedKey` | Managed-key signing adapter boundary. The host supplies the actual managed-key client, credentials, key operations, verification path, monitoring, and operational policy. |

Future Event Hubs, Purview, Azure-specific SDK adapters, gateway, robotics, immutable-storage, external anchoring, and additional provider packages are not part of the `1.2.1` stable contract unless separately reviewed and released as stable packages.

## What changed since 1.2.0

### Source Link and package metadata hardening

`1.2.1` adds Source Link metadata support across package projects and samples so generated packages can include NuGet repository commit metadata when built with Source Link enabled.

The release also adds `scripts/Validate-Source-Link-commit-metadata.ps1` so maintainers can validate published package repository metadata and confirm that Source Link commit metadata is present after packages are published.

### Package-signing and .NET Foundation readiness wording

README and security documentation now make the current package-signing status explicit: published packages include repository metadata and Source Link commit metadata where applicable, but AsiBackbone NuGet packages are not currently signed release artifacts from the project maintainer.

The project also includes explicit .NET Foundation Code of Conduct alignment wording.

### Workflow dependency and line-ending hygiene

The GitHub Actions checkout dependency was refreshed to `actions/checkout` `v7.0.0`, and workflow YAML files were normalized to LF line endings to prevent repeated local modification noise after hard resets or checkout normalization.

### Endpoint-governance hardening

`1.2.1` carries forward small ASP.NET Core endpoint-governance hardening made after the `1.2.0` tag without broadening the stable public API surface.

## SemVer and compatibility

`1.2.1` is a compatible patch release in the stable `1.x` line.

Compatibility expectations:

- existing stable `1.2.0` consumers should be able to upgrade without required source-code changes;
- `1.2.1` focuses on release-readiness, documentation, workflow, package metadata, validation, and implementation hardening;
- `AssemblyVersion` remains `1.0.0.0` for the compatible stable line;
- package `Version`, `FileVersion`, and `InformationalVersion` move to `1.2.1`;
- provider packages and design pages remain separate: documentation can describe a future provider direction without implying a stable package has shipped.

## Release validation posture

Before tagging `v1.2.1`, the release-candidate commit should pass the release-blocking validation path documented in [Stable Release Validation](release-validation.md) and the [1.2.1 Release Readiness Record](release-readiness-121.md).

The expected release gate includes:

- version metadata validation;
- solution restore;
- Release build;
- formatting verification;
- full solution tests;
- DocFX build;
- package creation;
- generated package version validation;
- generated NuGet metadata validation;
- template package smoke validation;
- external consumer smoke tests;
- stable package integration smoke tests;
- package artifact upload before publish;
- Source Link repository commit metadata validation after packages are published.

## Boundary notes

`1.2.1` remains Accountable Systems Infrastructure and governance spine infrastructure, not an artificial superintelligence implementation, AI model host, robot controller, legal/compliance guarantee, production immutable ledger, or production tamper-evident storage provider.

The host application remains responsible for authentication, authorization, persistence, execution, deployment, DLP/classification scanner implementation, exporter configuration, key custody, verification policy, storage immutability, legal review, and operational accountability.

## Related documentation

- [1.2.1 Release Readiness Record](release-readiness-121.md)
- [1.2.0 Release Notes](release-notes-120.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Stable Release Validation](release-validation.md)
- [Implementation-First Adoption Path](implementation-first-adoption.md)
- [dotnet new Templates](templates.md)
- [Testing Harness](testing-harness.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
