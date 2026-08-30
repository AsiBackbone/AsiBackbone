# ASI Backbone Documentation

Welcome to the AsiBackbone product documentation.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a .NET governance and policy-control package family implemented as practical software infrastructure. The project is a governance spine, not an intelligence engine.

> [!IMPORTANT]
> AsiBackbone provides framework-neutral building blocks and host integration seams for governing consequential actions in software systems. Host applications remain responsible for authentication, authorization, execution, persistence, deployment, monitoring, compliance review, and operational controls. See [Project Boundaries and Non-Claims](articles/project-boundaries.md) for the canonical boundary reference.

## Product documentation versus architecture education

This site is authoritative for concrete AsiBackbone package, API, configuration, runtime, integration, security, operations, compatibility, release, and maintainer behavior.

For architecture concepts, tutorials, comparisons, tradeoffs, labs, and general governed-execution education, use [ASI Backbone Learning](https://asibackbone.github.io/Learning/). Its [Architecture Glossary](https://asibackbone.github.io/Learning/architecture/glossary.html) is the canonical educational terminology reference, with lineage maintained in [Terminology and Established Architecture Concepts](https://asibackbone.github.io/Learning/architecture/terminology-and-established-concepts.html). Learning does not override product API or runtime truth documented here.

See [Documentation Ownership](articles/documentation-ownership.md) for the cross-repository ownership matrix and contribution routing rule.

## Choose a task

| Goal | Start here |
| --- | --- |
| Install and configure AsiBackbone | [Implementation-First Adoption Path](articles/implementation-first-adoption.md) and [First 15 Minutes: Standard API Gating](articles/quickstart-api-gating.md) |
| Register the package family in a host | [AddAsiBackbone Builder Facade](articles/add-asibackbone-builder-facade.md) and [Getting Started](articles/getting-started.md) |
| Find a package or public type | [Documentation Articles](articles/) and [API Reference](api/) |
| Understand runtime decision behavior | [Policy Evaluator Pipeline](articles/policy-evaluator-pipeline.md) |
| Persist, observe, sign, verify, or harden a deployment | [Production Hardening: Evaluator and Outbox](articles/production-hardening-evaluator-and-outbox.md) |
| Run a concrete integration path | [Plain ASP.NET Core Host Sample](articles/plain-aspnetcore-host-sample.md) and [Samples & Scenarios](articles/) |
| Check compatibility or release changes | [3.2.2 Release Notes](articles/release-notes-322.md) and [API Compatibility and SemVer](articles/api-compatibility-and-semver.md) |
| Review readiness, benchmarks, or quality evidence | [Quality Reports](quality/) and [3.2.2 Release Readiness Record](articles/release-readiness-322.md) |
| Learn the broader architecture | [ASI Backbone Learning](https://asibackbone.github.io/Learning/) |

The DocFX header search is available for package names, API concepts, and article titles. Source for every page lives under `docs/`, and the site header includes a Repository link for source review or edits.

## Current stable package family

Stable `3.2.x` package family. `3.2.2` is the current patch release.
The package family carries forward the governance-spine surface, including the
explicit capability-grant validation profiles introduced in `3.2.0` and the
organization-owned repository metadata established in `3.2.1`. `3.2.2` is a
maintenance patch that refreshes approved .NET dependencies and SHA-pinned
workflow/security tooling without changing the public API or runtime governance
contract.

The historical `3.0.0` release established the current major line and binary assembly identity.

```text
AsiBackbone.Core
AsiBackbone.DependencyInjection
AsiBackbone.Storage.InMemory
AsiBackbone.EntityFrameworkCore
AsiBackbone.AspNetCore
AsiBackbone.Testing
AsiBackbone.Templates
AsiBackbone.Analyzers
AsiBackbone.OpenTelemetry
AsiBackbone.Signing.LocalDevelopment
AsiBackbone.Signing.ManagedKey
```

Package-specific READMEs and release notes define which surfaces are stable, optional, local-only, or future-facing. A design page being present in the documentation does not mean the corresponding provider package has shipped as stable.

## Product documentation map

### Overview and boundaries

* [Project Boundaries and Non-Claims](articles/project-boundaries.md)
* [Package and Integration Boundaries](articles/integration-boundaries.md)
* [Target Framework Support](articles/target-framework-support.md)
* [Documentation Ownership](articles/documentation-ownership.md)
* [AsiBackbone API Terminology Map](articles/terminology-map.md)
* [AsiBackbone API Glossary](articles/glossary.md)

### Get started

* [Implementation-First Adoption Path](articles/implementation-first-adoption.md)
* [First 15 Minutes: Standard API Gating](articles/quickstart-api-gating.md)
* [AddAsiBackbone Builder Facade](articles/add-asibackbone-builder-facade.md)
* [dotnet new Templates](articles/templates.md)
* [Reference Deployment: Plain ASP.NET Core Host Evidence](articles/reference-deployment.md)

### Packages and API

* [Documentation Articles](articles/)
* [Generated API Reference](api/)
* [Core API Domain Model](articles/core-domain-language.md)
* [ASP.NET Core Integration Boundary](articles/aspnetcore-integration-boundary.md)
* [EF Core Integration Boundary](articles/ef-core-integration-boundary.md)
* [Testing Harness](articles/testing-harness.md)
* [Roslyn Analyzers](articles/roslyn-analyzers.md)
* [OpenTelemetry Governance Emission Provider](articles/opentelemetry-governance-emission-provider.md)
* [Signing Provider Package Boundary](articles/signing-provider-package-boundary.md)

### Implementation guides

* [Policy Evaluator Pipeline](articles/policy-evaluator-pipeline.md)
* [Custom Decision Policy Examples](articles/custom-decision-policy-examples.md)
* [Acknowledgment Workflow](articles/dynamic-liability-handshake.md)
* [Host-Owned Execution Enforcement](articles/host-owned-execution-enforcement.md)
* [Capability Grant Hardening](articles/capability-grant-hardening.md)

### Security and operations

* [Production Hardening: Evaluator and Outbox](articles/production-hardening-evaluator-and-outbox.md)
* [Durable Audit and Outbox Persistence](articles/durable-audit-outbox-persistence.md)
* [Observability and Governance Emission Architecture](articles/observability-and-governance-emission-architecture.md)
* [DLP and Classification Failure Policy](articles/dlp-classification-failure-policy.md)
* [Signing-Ready Receipts and Key Handling](articles/signing-ready-receipts-and-key-handling.md)
* [Verification Policy and Result Handling](articles/verification-policy-and-result-handling.md)
* [Key Rotation and Retired-Key Verification](articles/key-rotation-and-retired-key-verification.md)
* [Security Policy and Vulnerability Disclosure](https://github.com/AsiBackbone/AsiBackbone/blob/main/SECURITY.md)

### Releases and compatibility

* [3.2.2 Release Notes](articles/release-notes-322.md)
* [3.2.2 Consumer Verification Guide](articles/consumer-verification-322.md)
* [API Compatibility and SemVer](articles/api-compatibility-and-semver.md)
* [Schema Versioning](articles/schema-versioning.md)
* [Release Validation](articles/release-validation.md)

Historical release notes remain available under **Releases & Compatibility** in the article navigation without occupying the primary adoption path.

### Maintainer and quality evidence

* [Quality Reports](quality/)
* [Performance Benchmark Baseline](articles/performance-benchmark-baseline.md)
* [API Baseline and Boundary Checks](articles/api-baseline-and-boundary-checks.md)
* [Release Cadence and Readiness](articles/release-cadence-and-readiness.md)
* [3.2.2 Release Readiness Record](articles/release-readiness-322.md)

## Learn the architecture

General architecture teaching is maintained in [ASI Backbone Learning](https://asibackbone.github.io/Learning/). Retained conceptual articles in this repository remain available for continuity and historical context, but they are intentionally separated from the main implementation/API navigation.
