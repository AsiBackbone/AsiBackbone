# 1.2.0 Release Notes

These notes describe the `1.2.0 - Adoption, Templates, Diagnostics, and Release Readiness` package-family boundary.

AsiBackbone is governance infrastructure for accountable software decision flow. It does not certify compliance, or provide production tamper-evidence by itself.

## Release summary

`1.2.0` is a backward-compatible minor release for the stable `1.x` line. It formalizes the additive adoption, diagnostics, testing, and developer-experience surfaces added after the `1.1.x` line while preserving the existing compatible API contract for current consumers.

The release centers on a clearer implementation-first path:

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

`AssemblyVersion` remains fixed at `1.0.0.0` for the compatible stable `1.x` line. Package `Version`, `FileVersion`, and `InformationalVersion` move to `1.2.0`.

## Stable package family

The released `1.2.0` package line covers the package family below.

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

Future Event Hubs, Purview, Azure-specific SDK adapters, gateway, robotics, immutable-storage, external anchoring, and additional provider packages are not part of the `1.2.0` stable contract unless separately reviewed and released as stable packages.

## What changed since 1.1.1

### Explicit builder facade

`AsiBackbone.DependencyInjection` gives hosts an explicit `AddAsiBackbone(...)` registration facade for coordinating provider-owned registrations. The facade keeps provider registration intentional: Core does not silently register infrastructure, persistence, exporters, signing providers, or execution behavior.

### Testing harness package

`AsiBackbone.Testing` provides test-only helpers for deterministic endpoint-governance decisions, policy-result shaping, capability validation, in-memory audit inspection, non-durable outbox storage, and no-signature signing seams. It is intended for tests and local validation, not production enforcement.

### Template package

`AsiBackbone.Templates` adds `dotnet new asibackbone-webapi` scaffolding so consumers can generate a governed ASP.NET Core host from a clean directory. The template package is a developer-experience package and not a runtime dependency.

### Endpoint governance diagnostics

ASP.NET Core endpoint governance now includes opt-in development diagnostics. Diagnostics are gated by explicit configuration and development-environment checks so production hosts do not accidentally expose additional governance metadata.

### Analyzer hardening

The analyzer package now includes stronger guidance around local-development signing usage in production-oriented code paths. Analyzer diagnostics remain build-time guidance; they do not replace code review, runtime policy evaluation, operational controls, or security review.

### Samples and reference evidence

The release adds a sample-first .NET Aspire AppHost path and reference-deployment evidence for the Plain ASP.NET Core host. These samples show wiring and validation patterns without turning Aspire, robotics, physical execution, or NetCoreApplicationTemplate into required runtime dependencies.

### Documentation alignment

The documentation set now emphasizes an implementation-first adoption path, visible package selection, searchable DocFX navigation, visual governance flow diagrams, release-readiness validation, and project-governance contribution docs.

## SemVer and compatibility

`1.2.0` is a compatible minor release in the stable `1.x` line.

Compatibility expectations:

- existing stable `1.x` consumers should be able to upgrade without required source-code changes for existing APIs;
- additive public APIs, options, packages, diagnostics, samples, templates, and documentation are grouped under the minor release;
- `AssemblyVersion` remains `1.0.0.0` for the compatible stable line;
- package `Version`, `FileVersion`, and `InformationalVersion` move to `1.2.0`;
- provider packages and design pages remain separate: documentation can describe a future provider direction without implying a stable package has shipped.

## Release validation posture

Before tagging `v1.2.0`, the release-candidate commit should pass the release-blocking validation path documented in [Stable Release Validation](release-validation.md) and the [1.2.0 Release Readiness Record](release-readiness-120.md).

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
- package artifact upload before publish.

## Boundary notes

`1.2.0` remains Accountable Systems Infrastructure and governance spine infrastructure, not an artificial superintelligence implementation, AI model host, robot controller, legal/compliance guarantee, production immutable ledger, or production tamper-evident storage provider.

The host application remains responsible for authentication, authorization, persistence, execution, deployment, DLP/classification scanner implementation, exporter configuration, key custody, verification policy, storage immutability, legal review, and operational accountability.

## Related documentation

- [1.2.0 Release Readiness Record](release-readiness-120.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Stable Release Validation](release-validation.md)
- [Implementation-First Adoption Path](implementation-first-adoption.md)
- [dotnet new Templates](templates.md)
- [Testing Harness](testing-harness.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
