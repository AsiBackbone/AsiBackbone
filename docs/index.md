---
title: "Governed Decisions for .NET Applications"
description: AsiBackbone is a .NET governance and policy-control package family for accountable software execution, explicit decisions, and decision records.
_disableAffix: true
_disableBreadcrumb: true
_disableToc: true
---

# AsiBackbone Documentation

<div class="home-hero">
  <p class="home-kicker">Governed decisions for .NET applications</p>
  <p class="home-summary">AsiBackbone is a .NET governance and policy-control package family for accountable software execution: policy context, explicit decision outcomes, decision records, scoped authority, and host-owned execution.</p>
  <p class="home-why">Most software can tell you what happened. Far less can show that an action was evaluated before it executed, under which policy, and with what evidence. Evaluate AsiBackbone when a consequential host-owned action needs policy provenance, acknowledgment, scoped continuation authority, or durable decision evidence.</p>
  <div class="home-actions" role="group" aria-label="Start with AsiBackbone">
    <a class="btn btn-primary" href="articles/implementation-first-adoption.md">Get Started</a>
    <a class="btn btn-outline-primary" href="articles/use-cases.md">Is It a Fit?</a>
    <a class="btn btn-outline-primary" href="articles/quickstart-api-gating.md">First 15 Minutes</a>
  </div>
  <p class="home-boundary">AsiBackbone is not an intelligence engine. It provides framework-neutral building blocks and host integration seams for governing consequential actions in software systems. Host applications remain responsible for authentication, authorization, execution, persistence, deployment, monitoring, compliance review, and operational controls. See <a href="articles/project-boundaries.md">Project Boundaries and Non-Claims</a> for the canonical boundary reference.</p>
</div>

## Start here

<div class="entry-point-grid">
  <a class="entry-point" href="articles/use-cases.md">
    <strong>Decide whether it fits</strong>
    <span>Compare the product's good and poor fits, including when a simpler design is enough.</span>
  </a>
  <a class="entry-point" href="articles/quickstart-api-gating.md">
    <strong>Gate an API in 15 minutes</strong>
    <span>Run the standard API gating path end to end in a minimal host.</span>
  </a>
  <a class="entry-point" href="articles/implementation-first-adoption.md">
    <strong>Adopt it in a host</strong>
    <span>Install, register, and configure the package family step by step.</span>
  </a>
  <a class="entry-point" href="articles/policy-evaluator-pipeline.md">
    <strong>Understand runtime decisions</strong>
    <span>See how the policy evaluator pipeline produces a governance decision.</span>
  </a>
  <a class="entry-point" href="articles/production-hardening-evaluator-and-outbox.md">
    <strong>Harden a deployment</strong>
    <span>Persist, observe, sign, and verify decisions in production.</span>
  </a>
  <a class="entry-point" href="api-reference.md">
    <strong>Find a package or type</strong>
    <span>Browse the generated API reference for every shipped package.</span>
  </a>
</div>

## Is AsiBackbone a fit?

<div class="fit-grid">
  <section class="fit-panel" aria-labelledby="fit-use">
    <h3 id="fit-use">Consider AsiBackbone when</h3>
    <p>a consequential host-owned action needs policy provenance, acknowledgment, scoped continuation authority, or durable decision evidence.</p>
    <p>See <a href="articles/use-cases.md#good-product-fit">Good product fit</a> for the capabilities that make it a reasonable candidate.</p>
  </section>
  <section class="fit-panel" aria-labelledby="fit-simpler">
    <h3 id="fit-simpler">Prefer a simpler design when</h3>
    <p>the action is low risk and ordinary framework authorization, application services, and logs already provide the evidence you need.</p>
    <p>See <a href="articles/use-cases.md#poor-product-fit">Poor product fit</a>, and the general comparisons <a href="https://asibackbone.github.io/Learning/architecture/when-aspnet-core-authorization-is-enough.html">When ASP.NET Core Authorization Is Enough</a> and <a href="https://asibackbone.github.io/Learning/architecture/when-a-simple-application-service-is-enough.html">When a Simple Application Service Is Enough</a>.</p>
  </section>
</div>

## Product documentation versus architecture education

This site is authoritative for concrete AsiBackbone package, API, configuration, runtime, integration, security, operations, compatibility, release, and maintainer behavior.

For architecture concepts, tutorials, comparisons, tradeoffs, labs, and general governed-execution education, use [AsiBackbone Learning](https://asibackbone.github.io/Learning/). Its [Architecture Glossary](https://asibackbone.github.io/Learning/architecture/glossary.html) is the canonical educational terminology reference, with lineage maintained in [Terminology and Established Architecture Concepts](https://asibackbone.github.io/Learning/architecture/terminology-and-established-concepts.html). Learning does not override product API or runtime truth documented here.

See [Documentation Ownership](articles/documentation-ownership.md) for the cross-repository ownership matrix and contribution routing rule.

## More tasks

| Goal | Start here |
| --- | --- |
| Register the package family in a host | [AddAsiBackbone Builder Facade](articles/add-asibackbone-builder-facade.md) and [Getting Started](articles/getting-started.md) |
| Find articles by package or topic | [Documentation Articles](articles/index.md) |
| Run a concrete integration path | [Plain ASP.NET Core Host Sample](articles/plain-aspnetcore-host-sample.md) and [Samples & Scenarios](articles/index.md) |
| Check compatibility, release changes, or upgrade history | [Releases & Compatibility](releases/index.md) |
| Review readiness, benchmarks, or quality evidence | [Maintainer & Quality Evidence](maintainers/index.md) |

The DocFX header search is available for package names, API concepts, and article titles. Source for every page lives under `docs/`, and the site header's AsiBackbone menu includes a Framework Repository link for source review or edits.

## Current stable package family

Stable `7.x` package family. The current release is `7.2.0`.

`7.2.0` aligns public parameter names with decision-receipt terminology and
refreshes supported dependencies. Existing compiled consumers, positional
callers, persisted data, and runtime behavior remain compatible; named-argument
callers must use the new names when rebuilding. Package IDs, the `net10.0`
target, and binary assembly identity `7.0.0.0` are unchanged.

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
* [6.0 Product Terminology](articles/terminology-600.md)
* [AsiBackbone API Glossary](articles/glossary.md)

### Get started

* [Adoption and Target Use Cases](articles/use-cases.md)
* [Implementation-First Adoption Path](articles/implementation-first-adoption.md)
* [First 15 Minutes: Standard API Gating](articles/quickstart-api-gating.md)
* [AddAsiBackbone Builder Facade](articles/add-asibackbone-builder-facade.md)
* [dotnet new Templates](articles/templates.md)
* [Reference Deployment: Plain ASP.NET Core Host Evidence](articles/reference-deployment.md)

### Packages and API

* [Documentation Articles](articles/index.md)
* [Generated API Reference](api-reference.md)
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

* [7.2.0 Release Notes](articles/release-notes-720.md)
* [7.2.0 Consumer Verification Guide](articles/consumer-verification-720.md)
* [7.1.1 Release Notes](articles/release-notes-711.md)
* [7.1.1 Consumer Verification Guide](articles/consumer-verification-711.md)
* [7.1.0 Release Notes](articles/release-notes-710.md)
* [7.1.0 Consumer Verification Guide](articles/consumer-verification-710.md)
* [7.0.0 Release Notes](articles/release-notes-700.md)
* [6.0.0 Release Readiness Record](articles/release-readiness-600.md)
* [6.0.0 Consumer Verification Guide](articles/consumer-verification-600.md)
* [Upgrade from 6.x to 7.0](articles/upgrade-600-to-700.md)
* [API Compatibility and SemVer](articles/api-compatibility-and-semver.md)
* [Schema Versioning](articles/schema-versioning.md)
* [Release Validation](articles/release-validation.md)

Historical release notes and verification records remain available under [Releases & Compatibility](releases/index.md) without occupying the primary adoption path.

### Maintainer and quality evidence

* [Maintainer & Quality Evidence](maintainers/index.md)
* [Quality Reports](quality/index.md)
* [Performance Benchmark Baseline](articles/performance-benchmark-baseline.md)
* [API Baseline and Boundary Checks](articles/api-baseline-and-boundary-checks.md)
* [Release Cadence and Readiness](articles/release-cadence-and-readiness.md)
* [6.0.0 Release Readiness Record](articles/release-readiness-600.md)

### Historical records

* [Historical and Superseded Records](history/index.md)

Historical and superseded records remain public and searchable for traceability without being presented as current implementation guidance.

## Learn the architecture

General architecture teaching is maintained in [AsiBackbone Learning](https://asibackbone.github.io/Learning/). Retained conceptual articles in this repository remain available for continuity and historical context, but they are intentionally separated from the main implementation/API navigation.

## Related projects

<div class="project-grid">
  <article class="project-card">
    <span class="project-role">Learn the patterns</span>
    <h3><a href="https://asibackbone.github.io/Learning/">AsiBackbone Learning</a></h3>
    <p>Practical .NET architecture tutorials, labs, runnable samples, and reference patterns for governed execution, secure applications, AI integration, and policy-driven systems, including comparisons that show when these patterns are unnecessary.</p>
    <p class="project-links">
      <a href="https://asibackbone.github.io/Learning/" aria-label="AsiBackbone Learning documentation">Documentation</a>
      <a href="https://github.com/AsiBackbone/Learning" aria-label="AsiBackbone Learning repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.21938556" aria-label="AsiBackbone Learning archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
  <article class="project-card" aria-current="page">
    <span class="project-role">Add governed decisions</span>
    <h3>AsiBackbone <span class="current-site">Current site</span></h3>
    <p>A .NET governance and policy-control framework for auditable decision flows: policy evaluation, auditable decision records, acknowledgment workflows, capability-based authorization, host-owned execution boundaries, and ASP.NET Core integration.</p>
    <p class="project-links">
      <a href="https://github.com/AsiBackbone/AsiBackbone" aria-label="AsiBackbone repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.20546032" aria-label="AsiBackbone archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
  <article class="project-card">
    <span class="project-role">Start a secure application</span>
    <h3><a href="https://asibackbone.github.io/NetCoreApplicationTemplate/">.NET Core Application Template</a></h3>
    <p>A secure-by-default ASP.NET Core application template with authentication and authorization, structured logging, security headers, rate limiting, EF Core data access, reverse proxy support, and Problem Details error handling.</p>
    <p class="project-links">
      <a href="https://asibackbone.github.io/NetCoreApplicationTemplate/" aria-label=".NET Core Application Template documentation">Documentation</a>
      <a href="https://github.com/AsiBackbone/NetCoreApplicationTemplate" aria-label=".NET Core Application Template repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.20373042" aria-label=".NET Core Application Template archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
  <article class="project-card">
    <span class="project-role">Follow the integration</span>
    <h3><a href="https://asibackbone.github.io/ReferenceApplication/">AsiBackbone Reference Application</a></h3>
    <p>An NCAT-generated ASP.NET Core host that records the step-by-step integration of AsiBackbone, with host-specific API documentation and runnable governance examples added as the walkthrough progresses.</p>
    <p class="project-links">
      <a href="https://asibackbone.github.io/ReferenceApplication/" aria-label="AsiBackbone Reference Application documentation">Documentation</a>
      <a href="https://github.com/AsiBackbone/ReferenceApplication" aria-label="AsiBackbone Reference Application repository">Repository</a>
    </p>
  </article>
</div>

## Design philosophy

<div class="philosophy-callout">
  <p class="philosophy-statement">We favor architectures where consequential operations are explicit, constrained, reviewable, and auditable.</p>
  <p>We also document when these patterns are unnecessary: see <a href="articles/use-cases.md#poor-product-fit">Poor product fit</a>, <a href="https://asibackbone.github.io/Learning/architecture/when-aspnet-core-authorization-is-enough.html">When ASP.NET Core Authorization Is Enough</a>, and <a href="https://asibackbone.github.io/Learning/architecture/when-a-simple-application-service-is-enough.html">When a Simple Application Service Is Enough</a>.</p>
  <p>The projects focus on practical .NET engineering. They do not claim regulatory certification or automatic compliance. See <a href="articles/project-boundaries.md">Project Boundaries and Non-Claims</a>.</p>
</div>
