# API Compatibility and Semantic Versioning

This article defines the public API compatibility promise for the stable AsiBackbone package family and documents how semantic versioning applies after stabilization.

It complements the historical stable API review tracked in [issue #13](https://github.com/cdcavell/AsiBackbone/issues/13). The `2.0.0` release moved public package IDs and namespaces from `CDCavell.AsiBackbone.*` to `AsiBackbone.*`. The `3.0.0` release established the current `3.x` stable line and binary assembly identity while preserving the existing `AsiBackbone.*` package IDs and namespaces.

> [!NOTE]
> Additive public API or package surface should use a minor version bump even when the change is backward-compatible. Patch releases should be reserved for fixes, documentation, packaging, tests, and implementation hardening that do not expand the stable public surface.

## Compatibility promise for the stable `3.x` line

Starting with `3.0.0`, packages identified as stable are expected to preserve their documented public API surface for consumers within the same major version.

The compatibility promise applies to:

- public types and members exposed by stable packages;
- public namespaces intended for consumer use;
- documented extension points and service registration methods;
- documented option shapes and default behavior;
- durable artifact shapes that are explicitly described as stable, including their schema version where applicable;
- package boundaries and dependency direction described as stable for the released package family.

The promise does not mean implementation internals will never change. Internal code, private members, tests, documentation wording, samples, and non-public implementation details may change in minor or patch releases when the public contract remains compatible.

## Stable package scope by release

Stable compatibility is package-specific. A package becomes part of the stable contract when it is released as a stable package and documented as part of the stable package family.

### Original `1.0.0` stable family

The initial stable `1.0.0` package family established the first compatible `1.x` baseline:

| Package | Stable role |
| --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives, decisions, constraints, actor context, audit residue, acknowledgment, capability-token abstractions, and operation results. |
| `AsiBackbone.AspNetCore` | ASP.NET Core host adapters for actor context, request correlation, HTTP result mapping, and acknowledgment challenge support. |
| `AsiBackbone.Storage.InMemory` | Non-durable in-memory storage helpers for tests, samples, and local validation. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and host-owned persistence integration. |

### Expanded `1.1.x` and `1.2.x` stable family

The `1.1.x` and `1.2.x` releases expanded the stable `1.x` contract with compatible additive package surfaces including DependencyInjection, Testing, Templates, Analyzers, OpenTelemetry, Signing.LocalDevelopment, and Signing.ManagedKey. `1.2.1` was the final stable patch release for the compatible `1.x` line before the package/namespace rename.

### `2.x` stable family

The `2.0.0` release established the simplified `AsiBackbone.*` package and namespace identity after the public rename from `CDCavell.AsiBackbone.*`. `2.0.1`, `2.0.2`, `2.1.0`, `2.1.1`, `2.2.0`, `2.2.1`, and `2.3.0` preserved that package/namespace boundary while adding compatible package and host-facing surfaces.

### Current `3.x` stable family

`3.2.0` is the current stable minor release. It preserves the `AsiBackbone.*` package IDs and namespaces and the binary assembly identity `3.0.0.0` established by `3.0.0`.

| Package | `3.x` stable role |
| --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives and durable artifact contracts for the current `3.x` line, including policy evaluation, governance decisions, threat-model contributor hooks, metadata budget validation helpers, constraint-exception denial behavior, governed execution-to-mutation accountability receipts, capability-proof trust pinning, explicit capability-grant validation profiles, and builder-style audit residue construction. |
| `AsiBackbone.DependencyInjection` | Explicit builder facade and host-selected provider registration composition path. |
| `AsiBackbone.Storage.InMemory` | Non-durable storage helpers for tests, samples, local validation, lifecycle events, and outbox proof paths. |
| `AsiBackbone.EntityFrameworkCore` | EF Core host-owned persistence helpers for audit, acknowledgment, lifecycle, JSON metadata storage guidance, and outbox records. |
| `AsiBackbone.AspNetCore` | ASP.NET Core host adapters, endpoint governance, endpoint-governance metadata mode, strict-governance profile helpers, development diagnostics, endpoint fast-abort metadata, conservative actor-type claim trust controls, startup/configured-options validation, and hosted outbox drain integration. |
| `AsiBackbone.Testing` | Test-only harness helpers for deterministic governance and package-wiring tests. |
| `AsiBackbone.Templates` | Developer-experience `dotnet new` templates for governed ASP.NET Core host scaffolding. |
| `AsiBackbone.Analyzers` | Build-time analyzer safety rails, including production-signing configuration guidance. |
| `AsiBackbone.OpenTelemetry` | Released OpenTelemetry governance emission provider. |
| `AsiBackbone.Signing.LocalDevelopment` | Local-development signing and verification for tests, samples, and wiring proof paths only. |
| `AsiBackbone.Signing.ManagedKey` | Managed-key signing adapter boundary where the host supplies the actual managed-key client and operational controls. Production-oriented registration fails closed by default when signing cannot complete. |

Stable package status does not imply that every future provider idea is stable. Event Hubs, Purview, Azure-specific SDK adapters, Aspire runtime packages, robotics, immutable storage, and additional provider packages remain outside the stable contract unless separately reviewed and released as stable packages.

## What counts as a breaking change

A breaking change should require a new major version when it affects a stable package contract. Examples include:

- removing or renaming a public type, member, enum value, interface, namespace, or package;
- changing public method signatures, constructor signatures, generic constraints, or return types;
- changing documented default behavior in a way that can alter consumer outcomes;
- making previously optional configuration required;
- changing stable serialized or persisted artifact shapes without a compatible reader, migration path, or schema-versioned transition;
- changing service registration behavior in a way that breaks existing host startup code;
- changing package dependency direction or adding a framework/provider dependency that violates a documented package boundary;
- changing documented exception behavior where callers are expected to handle that behavior;
- converting a host-owned integration responsibility into a package-owned requirement without a compatible opt-in path.

A change is not usually breaking when it only adds new optional APIs, adds new optional configuration, improves implementation behavior without changing the public contract, fixes a bug to match documented behavior, or updates documentation and samples.

## Semantic versioning expectations

AsiBackbone follows Semantic Versioning expectations after stabilization:

| Version segment | Expected behavior |
| --- | --- |
| Major | May include intentional breaking changes to stable public APIs, stable package boundaries, binary assembly identity, or stable artifact contracts. Breaking changes should be documented with migration notes. |
| Minor | Adds backward-compatible public APIs, options, adapters, packages, or features. Existing stable APIs should continue to compile and behave compatibly. |
| Patch | Fixes bugs, documentation issues, packaging issues, or implementation defects without adding breaking public API changes. Patch releases can also clarify documentation and strengthen tests. |
| Preview suffix | Indicates packages or features that are still under review and may change before stable release. |

For future releases, additive public API or package changes should be grouped into a minor release even when they are opt-in and backward-compatible.

## Assembly version policy

For the stable `3.x` package line, AsiBackbone keeps `AssemblyVersion` fixed at `3.0.0.0` for compatible minor and patch releases. NuGet package `Version`, `FileVersion`, and `InformationalVersion` continue to move with each package release.

Expected stable-line behavior:

| Release | Package `Version` | `AssemblyVersion` | `FileVersion` | `InformationalVersion` |
| --- | --- | --- | --- | --- |
| `1.0.0` | `1.0.0` | `1.0.0.0` | `1.0.0.0` | `1.0.0+...` |
| `1.1.0` | `1.1.0` | `1.0.0.0` | `1.1.0.0` | `1.1.0+...` |
| `1.2.0` | `1.2.0` | `1.0.0.0` | `1.2.0.0` | `1.2.0+...` |
| `1.2.1` | `1.2.1` | `1.0.0.0` | `1.2.1.0` | `1.2.1+...` |
| `2.0.0` | `2.0.0` | `2.0.0.0` | `2.0.0.0` | `2.0.0+...` |
| `2.0.1` | `2.0.1` | `2.0.0.0` | `2.0.1.0` | `2.0.1+...` |
| `2.0.2` | `2.0.2` | `2.0.0.0` | `2.0.2.0` | `2.0.2+...` |
| `2.1.0` | `2.1.0` | `2.0.0.0` | `2.1.0.0` | `2.1.0+...` |
| `2.1.1` | `2.1.1` | `2.0.0.0` | `2.1.1.0` | `2.1.1+...` |
| `2.2.0` | `2.2.0` | `2.0.0.0` | `2.2.0.0` | `2.2.0+...` |
| `2.2.1` | `2.2.1` | `2.0.0.0` | `2.2.1.0` | `2.2.1+...` |
| `2.3.0` | `2.3.0` | `2.0.0.0` | `2.3.0.0` | `2.3.0+...` |
| `3.0.0` | `3.0.0` | `3.0.0.0` | `3.0.0.0` | `3.0.0+...` |
| `3.0.1` | `3.0.1` | `3.0.0.0` | `3.0.1.0` | `3.0.1+...` |
| `3.1.0` | `3.1.0` | `3.0.0.0` | `3.1.0.0` | `3.1.0+...` |
| `3.2.0` | `3.2.0` | `3.0.0.0` | `3.2.0.0` | `3.2.0+...` |

Before cutting stable releases, release validation should verify that `AssemblyVersion`, `FileVersion`, `InformationalVersion`, package metadata, release notes, and repository tags match this policy.

## Durable artifact and schema-version policy

Stable persisted or exported governance artifacts should carry an explicit schema/version field when they may be stored or consumed outside the running process. See [Schema Versioning](schema-versioning.md) for artifact-specific guidance.

Schema versioning does not replace package versioning. Package versions describe the released library. Schema versions describe durable payload shapes that may outlive a single package release.

Additive artifact fields are normally acceptable in a compatible minor release when existing readers can continue operating or when schema-versioned handling is documented. Incompatible durable shape changes should use schema-version guidance, migration documentation, and a major-version boundary when needed.

## Provider and future package guidance

Released provider packages have their own stable contract within the compatible `3.x` line once they are published as stable packages. Documentation should state whether each package is stable, preview, experimental, design-only, strategy-only, sample-only, or host-owned integration guidance.

## Release readiness checklist reference

Before cutting a stable release or stable package-family expansion, the release readiness checklist should confirm that:

- the stable package list is identified;
- public APIs for stable packages are reviewed for naming, namespace, dependency direction, and extension-point clarity;
- breaking changes found during review are resolved before release or captured for a future major version;
- stable serialized artifacts have documented schema-version behavior;
- the `AssemblyVersion` strategy is resolved and reflected in build metadata;
- stable provider packages are clearly distinguished from future/design-only provider pages;
- preview, strategy-only, design-only, or sample-only packages are not implied to be part of the stable contract;
- the changelog and release notes include the compatibility promise and migration guidance where needed.

## Related documentation

- [3.2.0 Release Notes](release-notes-320.md)
- [3.2.0 Release Readiness Record](release-readiness-320.md)
- [3.2.0 Consumer Verification Guide](consumer-verification-320.md)
- [3.1.0 Release Notes](release-notes-310.md)
- [3.1.0 Release Readiness Record](release-readiness-310.md)
- [3.1.0 Consumer Verification Guide](consumer-verification-310.md)
- [3.0.1 Release Notes](release-notes-301.md)
- [3.0.1 Release Readiness Record](release-readiness-301.md)
- [3.0.1 Consumer Verification Guide](consumer-verification-301.md)
- [3.0.0 Release Notes](release-notes-300.md)
- [2.3.0 Release Notes](release-notes-230.md)
- [2.2.1 Release Notes](release-notes-221.md)
- [2.2.0 Release Notes](release-notes-220.md)
- [2.1.0 Release Notes](release-notes-210.md)
- [2.0.2 Release Notes](release-notes-202.md)
- [2.0.1 Release Notes](release-notes-201.md)
- [2.0.0 Release Notes](release-notes-200.md)
- [1.2.1 Release Notes](release-notes-121.md)
- [1.2.0 Release Notes](release-notes-120.md)
- [1.1.x Release Notes](release-notes-110.md)
- [Upgrade Guide: 1.0.0 to 1.1.0](upgrade-100-to-110.md)
- [Schema Versioning](schema-versioning.md)
- [API Baseline and Boundary Checks](api-baseline-and-boundary-checks.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
- [dotnet new Templates](templates.md)
- [Release Validation](release-validation.md)
