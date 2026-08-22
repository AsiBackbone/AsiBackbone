# Documentation Articles

This section maps the AsiBackbone documentation set for the stable `3.x` Accountable Systems Infrastructure package family.

> [!IMPORTANT]
> In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is governance infrastructure for accountable software decision flow, not an artificial superintelligence implementation. See [Project Boundaries and Non-Claims](project-boundaries.md) for the canonical boundary reference.

## Search and navigation

Use the header search box for package names, API concepts, and article titles. Search is enabled for the published DocFX site; if a newly merged page is missing from results, wait for the documentation publish workflow to finish and refresh the browser cache. Source files live under `docs/` in the repository, and the site header includes a Repository link for source review or edits.

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

## Start here / implementation-first usage

* [Implementation-First Adoption Path](implementation-first-adoption.md)
* [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
* [Reference Deployment: Plain ASP.NET Core Host Evidence](reference-deployment.md)
* [Terminology Map](terminology-map.md)
* [Project Boundaries and Non-Claims](project-boundaries.md)
* [Progressive Adoption Ladder](progressive-adoption.md)
* [Getting Started](getting-started.md)
* [Core Governance Flow Diagrams](core-governance-flow-diagrams.md)
* [AddAsiBackbone Builder Facade](add-asibackbone-builder-facade.md)
* [dotnet new Templates](templates.md)
* [Why AsiBackbone?](why-asi-backbone.md)
* [3.2.2 Release Notes](release-notes-322.md)
* [3.2.2 Consumer Verification Guide](consumer-verification-322.md)
* [3.2.1 Release Notes](release-notes-321.md)
* [3.2.1 Consumer Verification Guide](consumer-verification-321.md)
* [3.2.0 Release Notes](release-notes-320.md)
* [3.2.0 Consumer Verification Guide](consumer-verification-320.md)
* [3.1.0 Release Notes](release-notes-310.md)
* [3.1.0 Consumer Verification Guide](consumer-verification-310.md)
* [3.0.1 Release Notes](release-notes-301.md)
* [3.0.1 Consumer Verification Guide](consumer-verification-301.md)
* [3.0.0 Release Notes](release-notes-300.md)
* [3.0.0 Consumer Verification Guide](consumer-verification-300.md)

## Core engineering concepts and domain language

* [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
* [Threat Model Contributors](threat-model-contributors.md)
* [Threat Outcome Reason Selection](threat-outcome-reason-selection.md)
* [Threat Metadata Provenance](threat-metadata-provenance.md)
* [Constraint Exception Policy](constraint-exception-policy.md)
* [Strict Governance Profile](strict-governance-profile.md)
* [Production Hardening: Evaluator and Outbox Configuration](production-hardening-evaluator-and-outbox.md)
* [Custom Decision Policy Examples](custom-decision-policy-examples.md)
* [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
* [High-Throughput Host Service Guidance](high-throughput-host-services.md)
* [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
* [Endpoint Governance Development Diagnostics](endpoint-governance-development-diagnostics.md)
* [Dynamic Liability Handshake](dynamic-liability-handshake.md)
* [Core Domain Language](core-domain-language.md)
* [Glossary](glossary.md)

## Optional conceptual background

These pages remain available for readers who want the broader framing. They are not required before using the packages.

* [Intent to Execution: An Accountability Pattern](intent-to-execution-pattern.md)
* [ASI Backbone Concept Synopsis](asi-backbone-concept.md)
* [Gateway and Regional Policy Flow](gateway-and-regional-policy-flow.md)
* [Equations and Toy Models](equations-and-toy-models.md)

## Adoption and use-case guidance

* [Governance Tool Comparisons](governance-tool-comparisons.md)
* [Adoption and Target Use Cases](use-cases.md)
* [Enterprise Adoption Personas](enterprise-adoption-personas.md)
* [Government and Regulated Systems](government-and-regulated-systems.md)

## Package integration guides

* [Reference Deployment: Plain ASP.NET Core Host Evidence](reference-deployment.md)
* [dotnet new Templates](templates.md)
* [Aspire AppHost Sample](aspire-apphost-sample.md)
* [EF Core Integration Boundary](ef-core-integration-boundary.md)
* [EF Core JSON Metadata Storage](ef-core-json-metadata-storage.md)
* [EF Core Host Ownership and Migration Guidance](ef-core-host-ownership-and-migrations.md)
* [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
* [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
* [Endpoint Governance Development Diagnostics](endpoint-governance-development-diagnostics.md)
* [Testing Harness](testing-harness.md)
* [Plain ASP.NET Core Host Sample](plain-aspnetcore-host-sample.md)
* [NetCoreApplicationTemplate Host Validation](netcoreapplicationtemplate-host-validation.md)
* [Schema Versioning](schema-versioning.md)
* [API Compatibility and SemVer](api-compatibility-and-semver.md)
* [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)

## Durability, observability, signing, diagnostics, and developer experience

* [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
* [Governance Emission Contract](governance-emission-contract.md)
* [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
* [Production Hardening: Evaluator and Outbox Configuration](production-hardening-evaluator-and-outbox.md)
* [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
* [Outbox Multi-Worker Concurrency](outbox-multi-worker-concurrency.md)
* [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)
* [High-Throughput Host Service Guidance](high-throughput-host-services.md)
* [Safe Audit and Telemetry Data](safe-audit-telemetry-data.md)
* [Audit Residue Observability Schema](audit-residue-observability-schema.md)
* [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
* [DLP and Classification Scanner Integration](dlp-classification-scanner-integration.md)

## Released provider package documentation

* [Roslyn Analyzers](roslyn-analyzers.md)
* [Released: OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
* [Signing Provider Package Boundary](signing-provider-package-boundary.md)
* [Managed-Key Signing Provider](managed-key-signing-provider.md)
* [Production Managed-Key Integration Guide](production-managed-key-integration.md)

## Security and cryptographic boundaries

These pages describe signing, verification, capability, and cryptographic-hardening posture. They do not imply production tamper-evidence unless a concrete signing, storage, verification, and key-management path is deployed by the host.

* [Project Boundaries and Non-Claims](project-boundaries.md)
* [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)
* [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
* [3.2.2 Consumer Verification Guide](consumer-verification-322.md)
* [3.2.1 Consumer Verification Guide](consumer-verification-321.md)
* [3.2.0 Consumer Verification Guide](consumer-verification-320.md)
* [3.1.0 Consumer Verification Guide](consumer-verification-310.md)
* [3.0.1 Consumer Verification Guide](consumer-verification-301.md)
* [3.0.0 Consumer Verification Guide](consumer-verification-300.md)
* [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
* [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
* [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
* [Key Rotation and Retired-Key Verification](key-rotation-and-retired-key-verification.md)
* [Capability Grant Hardening](capability-grant-hardening.md)
* [Capability Proof Trust Pinning](capability-proof-trust-pinning.md)
* [Audit Integrity Chain Model](audit-integrity-chain-model.md)
* [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
* [Cryptographic Security Hardening Roadmap](cryptographic-security-hardening-roadmap.md)

## Design-only and future provider strategy

These pages remain available as strategy or design material. They are not released provider packages unless a future release explicitly says so.

* [Design-Only: Event Hubs Governance Emission Provider](event-hubs-governance-emission-provider-design.md)
* [Strategy-Only: Purview Governance and Lineage Enrichment](purview-governance-lineage-enrichment-strategy.md)

## Advanced scenarios

* [AI Agent Gateway](scenarios/ai-agent-gateway.md)
* [Human Approval Before AI Tool Execution](scenarios/human-approval-before-ai-tool-execution.md)
* [High-Risk Administrative Action](scenarios/high-risk-administrative-action.md)
* [Sensitive Data Access Request](scenarios/sensitive-data-access-request.md)
* [Deployment or Infrastructure Change Gate](scenarios/deployment-or-infrastructure-change-gate.md)
* [Robotics Operational Gateway](scenarios/robotics-operational-gateway.md)

## Quality and release process

* [Quality Reports](../quality/index.md)
* [Performance Benchmark Baseline](performance-benchmark-baseline.md)
* [Release Cadence and Readiness](release-cadence-and-readiness.md)
* [Release Validation](release-validation.md)
* [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
* [3.2.2 Consumer Verification Guide](consumer-verification-322.md)
* [3.2.2 Release Readiness Record](release-readiness-322.md)
* [3.2.2 Release Notes](release-notes-322.md)
* [3.2.1 Consumer Verification Guide](consumer-verification-321.md)
* [3.2.1 Release Readiness Record](release-readiness-321.md)
* [3.2.0 Consumer Verification Guide](consumer-verification-320.md)
* [3.2.0 Release Readiness Record](release-readiness-320.md)
* [3.2.0 Release Notes](release-notes-320.md)
* [3.1.0 Consumer Verification Guide](consumer-verification-310.md)
* [3.1.0 Release Readiness Record](release-readiness-310.md)
* [3.1.0 Release Notes](release-notes-310.md)
* [3.0.1 Consumer Verification Guide](consumer-verification-301.md)
* [3.0.1 Release Readiness Record](release-readiness-301.md)
* [3.0.1 Release Notes](release-notes-301.md)
* [3.0.0 Consumer Verification Guide](consumer-verification-300.md)
* [3.0.0 Release Readiness Record](release-readiness-300.md)
* [3.0.0 Release Notes](release-notes-300.md)
* [2.3.0 Release Readiness Record](release-readiness-230.md)
* [2.3.0 Release Notes](release-notes-230.md)
* [API Baseline and Boundary Checks](api-baseline-and-boundary-checks.md)
* [Implicit Usings Posture](implicit-usings-posture.md)
* [Developer Checklist](developer-checklist.md)

## Historical design records

Historical pages are retained for traceability and are separated from current stable usage.

* [Historical Stable API Review](stable-api-review.md)
* [Historical Alpha Package Boundary](alpha-package-boundary.md)
* [Historical Core Alpha Readiness Review](core-alpha-readiness-review.md)

## Read next

- [Implementation-First Adoption Path](implementation-first-adoption.md)
- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
- [dotnet new Templates](templates.md)
