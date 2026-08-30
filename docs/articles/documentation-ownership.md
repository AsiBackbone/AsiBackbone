# Documentation Ownership

This page defines the documentation ownership contract between `AsiBackbone/AsiBackbone` and `AsiBackbone/Learning`. The goal is to keep one canonical educational source and one canonical product-implementation source without allowing the two to drift into competing authorities.

> [!IMPORTANT]
> `AsiBackbone/AsiBackbone` remains authoritative for the released product. `AsiBackbone/Learning` is authoritative for organization-level education and architecture teaching. Learning does not define or override package APIs, configuration defaults, runtime semantics, compatibility, security posture, or release behavior.

## Canonical sources

- Product implementation and API documentation: [AsiBackbone documentation](https://asibackbone.github.io/AsiBackbone/) and [AsiBackbone repository](https://github.com/AsiBackbone/AsiBackbone).
- Architecture education and general teaching: [ASI Backbone Learning](https://asibackbone.github.io/Learning/) and [AsiBackbone/Learning repository](https://github.com/AsiBackbone/Learning).
- Canonical educational terminology: [Learning Architecture Glossary](https://asibackbone.github.io/Learning/architecture/glossary.html) and [Terminology and Established Architecture Concepts](https://asibackbone.github.io/Learning/architecture/terminology-and-established-concepts.html).
- Product/API terminology mappings: [AsiBackbone API Glossary](glossary.md), [AsiBackbone API Terminology Map](terminology-map.md), and [Core API Domain Model](core-domain-language.md).

## Ownership matrix

| Documentation type | Source of truth |
| --- | --- |
| Organization-level concepts and education | Learning |
| Architecture tutorials and tradeoff analysis | Learning |
| Canonical educational definitions and glossary | Learning |
| Terminology lineage and established-concept mapping | Learning |
| Exact terminology-to-API/type mappings | AsiBackbone |
| Labs and general teaching samples | Learning |
| General AI/governance education | Learning |
| Package installation and configuration | AsiBackbone |
| Public API/type behavior | AsiBackbone |
| Package-specific quickstarts and compile-ready examples | AsiBackbone |
| Runtime semantics and integration boundaries | AsiBackbone |
| Security/cryptographic implementation posture | AsiBackbone |
| Provider/package operational guidance | AsiBackbone |
| Release notes, compatibility, migration, consumer verification | AsiBackbone |
| Quality, benchmark, release-readiness, maintainer evidence | AsiBackbone |

## Sample and tutorial boundary

The ownership matrix above is applied concretely in [Sample Ownership and Implementation Boundary](sample-ownership-and-implementation-boundary.md). That page classifies the current quickstarts, runnable sample hosts, public API examples, templates, and integration scenarios.

Use the [Learning executable sample catalog](https://asibackbone.github.io/Learning/samples/) for standalone teaching samples, labs, exercises, and vendor-neutral runnable examples. Use this repository for examples whose purpose is to exercise the shipped AsiBackbone packages and APIs.

## Routing rule for new documentation

Use this test before creating or expanding a page:

> If the page primarily teaches a general architectural idea, comparison, tradeoff, pattern, or exercise that remains useful without the AsiBackbone packages, it belongs in Learning. If it primarily documents what a concrete AsiBackbone package, API, configuration surface, runtime path, provider, or release actually does, it belongs in AsiBackbone.

For mixed topics, keep the exact implementation contract and compile-ready product usage in AsiBackbone, then link to Learning for the broader explanation, alternatives, or teaching material. Do not maintain two independent canonical explanations of the same general concept.

## URL continuity and cross-repository link contract

Moving educational authority to Learning does not mean removing the established AsiBackbone URL.

For a page that becomes Learning-owned:

1. Keep the existing AsiBackbone path published when practical.
2. Replace the old body with a short transition or product-boundary page.
3. State the canonical Learning destination explicitly.
4. Keep concrete AsiBackbone implementation/API links on the transition page when they remain useful.
5. Point Learning directly to the relevant product, API, release, or implementation destination. Do not point Learning back to a transition page in a way that creates a circular redirect/pointer pattern.

The curated continuity contract is stored in `eng/docs/cross-repository-links.json`. It records:

- preserved transition-page paths and their canonical Learning destinations;
- critical published Learning and AsiBackbone destinations;
- selected reciprocal Learning-to-AsiBackbone links that must remain present.

After building the DocFX site, run:

```powershell
./scripts/Validate-DocumentationLinks.ps1
```

The validator confirms that transition source files still name the canonical Learning target, the old DocFX output paths still exist, critical published destinations resolve, and selected reciprocal links remain present. Stable release validation and the documentation publishing workflow run the same guardrail.

## Authority when content overlaps

Use the product repository as the controlling source for:

- package IDs, supported target frameworks, installation, and configuration;
- public types, members, signatures, defaults, reason codes, and serialized behavior;
- runtime policy, acknowledgment, capability, audit, outbox, signing, verification, and host-integration semantics;
- security and cryptographic implementation posture;
- provider status, operational requirements, compatibility, releases, migrations, and consumer verification;
- quality, benchmark, release-readiness, and maintainer evidence.

Use Learning as the controlling source for:

- organization-level terminology and concept explanation;
- architecture tutorials, pattern education, alternatives, and tradeoff analysis;
- vendor-neutral comparisons and established-concept mapping;
- labs, exercises, and general teaching samples;
- general governed-execution and AI/governance education.

A Learning example may point to AsiBackbone as a concrete implementation, but the example does not redefine the product contract. Conversely, product documentation should link to Learning for deeper general education rather than growing a parallel curriculum.

## Scope

This contract governs documentation placement and authority only. It does not change runtime behavior, public APIs, package versions, release status, or host responsibilities.
