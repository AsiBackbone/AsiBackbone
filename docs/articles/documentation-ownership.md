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

## Routing rule for new documentation

Use this test before creating or expanding a page:

> If the page primarily teaches a general architectural idea, comparison, tradeoff, pattern, or exercise that remains useful without the AsiBackbone packages, it belongs in Learning. If it primarily documents what a concrete AsiBackbone package, API, configuration surface, runtime path, provider, or release actually does, it belongs in AsiBackbone.

For mixed topics, keep the exact implementation contract and compile-ready product usage in AsiBackbone, then link to Learning for the broader explanation, alternatives, or teaching material. Do not maintain two independent canonical explanations of the same general concept.

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
