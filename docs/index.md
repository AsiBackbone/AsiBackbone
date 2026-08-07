# ASI Backbone Documentation

Welcome to the AsiBackbone documentation site.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a .NET governance and policy-control package family implemented as practical software infrastructure. The project is a governance spine, not an intelligence engine.

> [!IMPORTANT]
> AsiBackbone provides framework-neutral building blocks and host integration seams for governing consequential actions in software systems. Host applications remain responsible for authentication, authorization, execution, persistence, deployment, monitoring, compliance review, and operational controls. For the canonical boundary reference, see [Project Boundaries and Non-Claims](articles/project-boundaries.md).

## Search, navigation, and source links

The documentation site uses the DocFX search box in the header. Source for every page lives in the repository under `docs/`, and the site header includes a Repository link for viewing or editing documentation files.

## Start here

These pages are the best first stops for implementation-first adoption.

* [Implementation-First Adoption Path](articles/implementation-first-adoption.md)
* [First 15 Minutes: Standard API Gating](articles/quickstart-api-gating.md)
* [AddAsiBackbone Builder Facade](articles/add-asibackbone-builder-facade.md)
* [dotnet new Templates](articles/templates.md)
* [Reference Deployment: Plain ASP.NET Core Host Evidence](articles/reference-deployment.md)
* [Terminology Map](articles/terminology-map.md)
* [Project Boundaries and Non-Claims](articles/project-boundaries.md)
* [Progressive Adoption Ladder](articles/progressive-adoption.md)
* [Getting Started](articles/getting-started.md)
* [Documentation Articles](articles/)
* [Security Policy and Vulnerability Disclosure](https://github.com/cdcavell/AsiBackbone/blob/main/SECURITY.md)

## Current stable package family

Stable `3.x` is the current release line, and `3.2.1` is the current minor release. The package family preserves the existing `AsiBackbone.*` package IDs and namespaces and the `3.0.0.0` binary assembly identity. This release adds explicit capability-grant validation profiles for consequential execution boundaries and intentional metadata-only inspection while preserving the existing configurable validation path for `3.x` compatibility.

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

## Core documentation areas

### Implementation-first adoption

* [Implementation-First Adoption Path](articles/implementation-first-adoption.md)
* [First 15 Minutes: Standard API Gating](articles/quickstart-api-gating.md)
* [AddAsiBackbone Builder Facade](articles/add-asibackbone-builder-facade.md)
* [dotnet new Templates](articles/templates.md)
* [Reference Deployment: Plain ASP.NET Core Host Evidence](articles/reference-deployment.md)
* [Plain ASP.NET Core Host Sample](articles/plain-aspnetcore-host-sample.md)
* [Aspire AppHost Sample](articles/aspire-apphost-sample.md)
* [NetCoreApplicationTemplate Host Validation](articles/netcoreapplicationtemplate-host-validation.md)

### Core concepts and domain language

* [Core Governance Flow Diagrams](articles/core-governance-flow-diagrams.md)
* [Core Domain Language](articles/core-domain-language.md)
* [Policy Evaluator Pipeline](articles/policy-evaluator-pipeline.md)
* [Threat Model Contributors](articles/threat-model-contributors.md)
* [Strict Governance Profile](articles/strict-governance-profile.md)
* [Custom Decision Policy Examples](articles/custom-decision-policy-examples.md)
* [Host-Owned Execution Enforcement](articles/host-owned-execution-enforcement.md)
* [Dynamic Liability Handshake](articles/dynamic-liability-handshake.md)
* [Glossary](articles/glossary.md)

### Package integration, observability, and signing

* [EF Core Integration Boundary](articles/ef-core-integration-boundary.md)
* [EF Core JSON Metadata Storage](articles/ef-core-json-metadata-storage.md)
* [ASP.NET Core Integration Boundary](articles/aspnetcore-integration-boundary.md)
* [ASP.NET Core Endpoint Governance](articles/aspnetcore-endpoint-governance.md)
* [Testing Harness](articles/testing-harness.md)
* [Schema Versioning](articles/schema-versioning.md)
* [API Compatibility and SemVer](articles/api-compatibility-and-semver.md)
* [Observability and Governance Emission Architecture](articles/observability-and-governance-emission-architecture.md)
* [Governance Emission Contract](articles/governance-emission-contract.md)
* [Durable Audit and Outbox Persistence](articles/durable-audit-outbox-persistence.md)
* [Hosted Governance Outbox Drain](articles/hosted-governance-outbox-drain.md)
* [EF Core Outbox Bulk-Claim Performance Evaluation](articles/efcore-outbox-bulk-claim-evaluation.md)
* [Claimed Outbox Transition Outcomes](articles/outbox-claim-transition-outcomes.md)
* [OpenTelemetry Governance Emission Provider](articles/opentelemetry-governance-emission-provider.md)
* [Signing Provider Package Boundary](articles/signing-provider-package-boundary.md)
* [Managed-Key Signing Provider](articles/managed-key-signing-provider.md)

### Optional conceptual and scenario background

* [Intent to Execution: An Accountability Pattern](articles/intent-to-execution-pattern.md)
* [ASI Backbone Concept Synopsis](articles/asi-backbone-concept.md)
* [Gateway and Regional Policy Flow](articles/gateway-and-regional-policy-flow.md)
* [Equations and Toy Models](articles/equations-and-toy-models.md)
* [Governance Tool Comparisons](articles/governance-tool-comparisons.md)
* [Adoption and Target Use Cases](articles/use-cases.md)
* [Enterprise Adoption Personas](articles/enterprise-adoption-personas.md)
* [Government and Regulated Systems](articles/government-and-regulated-systems.md)

### Security and cryptographic boundaries

* [Project Boundaries and Non-Claims](articles/project-boundaries.md)
* [Production Wording and Stable Signing Boundaries](articles/production-wording-and-alpha-limitations.md)
* [Supply-Chain Provenance and Package SBOMs](articles/supply-chain-provenance.md)
* [3.2.0 Consumer Verification Guide](articles/consumer-verification-320.md)
* [3.1.0 Consumer Verification Guide](articles/consumer-verification-310.md)
* [3.0.1 Consumer Verification Guide](articles/consumer-verification-301.md)
* [3.0.0 Consumer Verification Guide](articles/consumer-verification-300.md)
* [Signing-Ready Receipts and Key Handling](articles/signing-ready-receipts-and-key-handling.md)
* [Signed Audit and Outbox Records](articles/signed-audit-and-outbox-records.md)
* [Verification Policy and Result Handling](articles/verification-policy-and-result-handling.md)
* [Key Rotation and Retired-Key Verification](articles/key-rotation-and-retired-key-verification.md)
* [Capability Grant Hardening](articles/capability-grant-hardening.md)
* [Capability Proof Trust Pinning](articles/capability-proof-trust-pinning.md)
* [Audit Integrity Chain Model](articles/audit-integrity-chain-model.md)
* [Cryptographic Security Posture and Production Guidance](articles/cryptographic-security-posture.md)
* [Cryptographic Security Hardening Roadmap](articles/cryptographic-security-hardening-roadmap.md)

### Quality and release process

* [Quality Reports](quality/)
* [Performance Benchmark Baseline](articles/performance-benchmark-baseline.md)
* [Release Validation](articles/release-validation.md)
* [3.2.0 Consumer Verification Guide](articles/consumer-verification-320.md)
* [3.2.0 Release Readiness Record](articles/release-readiness-320.md)
* [3.2.0 Release Notes](articles/release-notes-320.md)
* [3.1.0 Consumer Verification Guide](articles/consumer-verification-310.md)
* [3.1.0 Release Readiness Record](articles/release-readiness-310.md)
* [3.1.0 Release Notes](articles/release-notes-310.md)
* [3.0.1 Consumer Verification Guide](articles/consumer-verification-301.md)
* [3.0.1 Release Readiness Record](articles/release-readiness-301.md)
* [3.0.1 Release Notes](articles/release-notes-301.md)
* [3.0.0 Consumer Verification Guide](articles/consumer-verification-300.md)
* [3.0.0 Release Readiness Record](articles/release-readiness-300.md)
* [3.0.0 Release Notes](articles/release-notes-300.md)
* [2.3.0 Release Readiness Record](articles/release-readiness-230.md)
* [2.3.0 Release Notes](articles/release-notes-230.md)
* [2.2.1 Release Readiness Record](articles/release-readiness-221.md)
* [2.2.1 Release Notes](articles/release-notes-221.md)
* [2.2.0 Release Readiness Record](articles/release-readiness-220.md)
* [2.2.0 Release Notes](articles/release-notes-220.md)
* [2.1.0 Release Readiness Record](articles/release-readiness-210.md)
* [2.1.0 Release Notes](articles/release-notes-210.md)
* [2.0.2 Release Readiness Record](articles/release-readiness-202.md)
* [2.0.2 Release Notes](articles/release-notes-202.md)
* [API Baseline and Boundary Checks](articles/api-baseline-and-boundary-checks.md)
* [Developer Checklist](articles/developer-checklist.md)
