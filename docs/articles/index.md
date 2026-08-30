# AsiBackbone Product Documentation

This section organizes the stable `3.x` Accountable Systems Infrastructure documentation around product implementation, API use, operations, compatibility, and release evidence.

> [!IMPORTANT]
> In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is governance infrastructure for accountable software decision flow, not an artificial superintelligence implementation. See [Project Boundaries and Non-Claims](project-boundaries.md) for the canonical boundary reference.

## Documentation ownership

This repository is authoritative for concrete AsiBackbone package, API, configuration, runtime, integration, security, operations, compatibility, release, and maintainer behavior. See [Documentation Ownership](documentation-ownership.md) for the cross-repository ownership matrix.

General architectural education belongs in [ASI Backbone Learning](https://asibackbone.github.io/Learning/) ([source](https://github.com/AsiBackbone/Learning)). Former conceptual URLs in this repository are retained as short product-mapping stubs for bookmark and inbound-link continuity; the left navigation now points directly to the canonical Learning treatments.

## Current stable package posture

Stable `3.2.x` package family. `3.2.2` is the current patch release.
The package family carries forward the governance-spine surface, including the
explicit capability-grant validation profiles introduced in `3.2.0` and the
organization-owned repository metadata established in `3.2.1`. `3.2.2` is a
maintenance patch that refreshes approved .NET dependencies and SHA-pinned
workflow/security tooling without changing the public API or runtime governance
contract.

The historical `3.0.0` release established the current major line and binary assembly identity while preserving the `AsiBackbone.*` package IDs and namespaces established by the `2.0.0` public rename from `CDCavell.AsiBackbone.*`.

Released stable package surfaces include Core, DependencyInjection,
Storage.InMemory, EntityFrameworkCore, AspNetCore, Testing, Templates,
Analyzers, OpenTelemetry, Signing.LocalDevelopment, and Signing.ManagedKey.

`3.2.2` carries forward the explicit capability-grant validation profiles
introduced in `3.2.0` and the canonical organization-owned repository metadata
established in `3.2.1`. The patch refreshes dependency and release-tooling
inputs without changing their runtime semantics.

Event Hubs, Purview, Azure-specific non-signing SDK adapters, Aspire runtime
packages, robotics, immutable storage, and additional non-signing provider
packages remain design-only, strategy-only, sample-only, host-owned, or
future-provider work unless a later stable release explicitly ships them.

The release process includes explicit [Release Cadence and Readiness](release-cadence-and-readiness.md) guidance for patch, minor, and major release selection, package metadata, Source Link, SBOM/provenance, documentation links, and future package identity or namespace changes. The [3.2.2 Consumer Verification Guide](consumer-verification-322.md) gives consumers a conservative package-source, package ID, dependency, Source Link, SBOM/provenance, and deferred-signing verification path.

## Search and navigation

Use the header search box for package names, API concepts, and article titles. Search is enabled for the published DocFX site; if a newly merged page is missing from results, wait for the documentation publish workflow to finish and refresh the browser cache. Source files live under `docs/` in the repository, and the site header includes a Repository link for source review or edits.

## Overview

Use these pages to establish the product boundary before integrating the packages.

* [Project Boundaries and Non-Claims](project-boundaries.md)
* [Package and Integration Boundaries](integration-boundaries.md)
* [Target Framework Support](target-framework-support.md)
* [Documentation Ownership](documentation-ownership.md)
* [AsiBackbone API Terminology Map](terminology-map.md)
* [AsiBackbone API Glossary](glossary.md)
* [Core Governance Flow Diagrams](core-governance-flow-diagrams.md)

## Get started

* [Implementation-First Adoption Path](implementation-first-adoption.md)
* [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
* [AddAsiBackbone Builder Facade](add-asibackbone-builder-facade.md)
* [Getting Started](getting-started.md)
* [Progressive Adoption Ladder](progressive-adoption.md)
* [dotnet new Templates](templates.md)
* [Reference Deployment: Plain ASP.NET Core Host Evidence](reference-deployment.md)

## Packages & API

Use the [Generated API Reference](../api-reference.md) for public types and members. These guides explain package-level integration boundaries and supported extension surfaces.

* [Core API Domain Model](core-domain-language.md)
* [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
* [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
* [EF Core Integration Boundary](ef-core-integration-boundary.md)
* [EF Core JSON Metadata Storage](ef-core-json-metadata-storage.md)
* [EF Core Host Ownership and Migration Guidance](ef-core-host-ownership-and-migrations.md)
* [Testing Harness](testing-harness.md)
* [Roslyn Analyzers](roslyn-analyzers.md)
* [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
* [Signing Provider Package Boundary](signing-provider-package-boundary.md)
* [Managed-Key Signing Provider](managed-key-signing-provider.md)
* [Schema Versioning](schema-versioning.md)
* [API Compatibility and SemVer](api-compatibility-and-semver.md)

## Implementation guides

These pages describe what the runtime does and how hosts participate in the governed decision flow.

* [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
* [Threat Model Contributors](threat-model-contributors.md)
* [Threat Outcome Reason Selection](threat-outcome-reason-selection.md)
* [Threat Metadata Provenance](threat-metadata-provenance.md)
* [Constraint Exception Policy](constraint-exception-policy.md)
* [Strict Governance Profile](strict-governance-profile.md)
* [Regulated Governance Profile](regulated-governance-profile.md)
* [Custom Decision Policy Examples](custom-decision-policy-examples.md)
* [Acknowledgment Workflow](dynamic-liability-handshake.md)
* [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
* [Host Mutation Accountability](host-mutation-accountability.md)
* [Actor-Type Claim Trust Boundary](actor-type-claim-trust-boundary.md)
* [Evaluator Concurrency Contract](evaluator-concurrency-contract.md)
* [High-Throughput Host Service Guidance](high-throughput-host-services.md)
* [Endpoint Governance Development Diagnostics](endpoint-governance-development-diagnostics.md)

## Security & operations

These pages cover production hardening, durable audit/outbox behavior, observability, DLP, signing, verification, capability proof, and regulated-system concerns.

* [Production Hardening: Evaluator and Outbox Configuration](production-hardening-evaluator-and-outbox.md)
* [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
* [Governance Outbox Delivery Semantics](governance-outbox-delivery-semantics.md)
* [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
* [Outbox Multi-Worker Concurrency](outbox-multi-worker-concurrency.md)
* [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)
* [Governance Outbox Poison-Message Controls](governance-outbox-poison-message-controls.md)
* [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
* [Governance Emission Contract](governance-emission-contract.md)
* [Safe Audit and Telemetry Data](safe-audit-telemetry-data.md)
* [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
* [DLP and Classification Scanner Integration](dlp-classification-scanner-integration.md)
* [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
* [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
* [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
* [Key Rotation and Retired-Key Verification](key-rotation-and-retired-key-verification.md)
* [Capability Grant Hardening](capability-grant-hardening.md)
* [Capability Proof Trust Pinning](capability-proof-trust-pinning.md)
* [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
* [Production Managed-Key Integration Guide](production-managed-key-integration.md)
* [Regulated Storage and Signing Verification Checklist](regulated-storage-and-signing-verification-checklist.md)

## Samples & scenarios

* [Plain ASP.NET Core Host Sample](plain-aspnetcore-host-sample.md)
* [Aspire AppHost Sample](aspire-apphost-sample.md)
* [NetCoreApplicationTemplate Host Validation](netcoreapplicationtemplate-host-validation.md)
* [NCAT Audit Completion Adapter](ncat-audit-completion-adapter.md)
* [AI Agent Gateway](scenarios/ai-agent-gateway.md)
* [Human Approval Before AI Tool Execution](scenarios/human-approval-before-ai-tool-execution.md)
* [High-Risk Administrative Action](scenarios/high-risk-administrative-action.md)
* [Sensitive Data Access Request](scenarios/sensitive-data-access-request.md)
* [Deployment or Infrastructure Change Gate](scenarios/deployment-or-infrastructure-change-gate.md)
* [Robotics Operational Gateway](scenarios/robotics-operational-gateway.md)

## Releases & compatibility

Start with the current release and compatibility rules. Older release notes, consumer-verification guides, and upgrade records are preserved under [Releases & Compatibility](../releases/) so they remain easy to find without dominating the implementation navigation.

* [3.2.2 Release Notes](release-notes-322.md)
* [3.2.2 Consumer Verification Guide](consumer-verification-322.md)
* [API Compatibility and SemVer](api-compatibility-and-semver.md)
* [Schema Versioning](schema-versioning.md)
* [Target Framework Support](target-framework-support.md)
* [Release and Upgrade History](../releases/)

## Maintainer / quality evidence

Release-readiness records, benchmark reviews, coverage/mutation/concurrency evidence, documentation-claim validation, and maintainer checklists are published under [Maintainer & Quality Evidence](../maintainers/). They remain searchable and public, but are intentionally outside the normal consumer implementation path.

## Historical records

Superseded design proposals, alpha-era package/readiness records, and historical API reviews are preserved under [Historical and Superseded Records](../history/). These records are retained for traceability and should not be read as current package behavior.

## Learn the architecture

For general architecture concepts, tutorials, comparisons, tradeoffs, labs, and broader governed-execution education, use [ASI Backbone Learning](https://asibackbone.github.io/Learning/).

Former conceptual URLs remain searchable as short continuity stubs, but canonical educational navigation points directly to Learning. Product-specific implementation, API, security, operations, compatibility, and release content remains authoritative here.

## Read next

- [Implementation-First Adoption Path](implementation-first-adoption.md)
- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
- [Generated API Reference](../api-reference.md)
