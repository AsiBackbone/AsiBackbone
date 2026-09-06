# Production Wording and Stable Signing Boundaries

This article defines documentation wording boundaries for the current stable `4.x` AsiBackbone package family. It keeps current behavior, host responsibilities, released signing surfaces, provider boundaries, and future-provider strategy clearly separated.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine for consequential software decision flow. It is not an AI model host, autonomous execution engine, key-management product, compliance certification product, immutable audit store, cloud governance platform, or operational guarantee by itself.

> [!IMPORTANT]
> Documentation should describe only implemented and released behavior as current behavior. The stable `4.x` family carries forward Core signing-ready and verification primitives, the local-development signing provider, and the managed-key adapter boundary. These surfaces still have important limits: local-development signing is for tests and samples, the managed-key package is an adapter boundary, and signing alone does not create production-grade audit guarantees without host-owned storage, key management, verification, monitoring, and retention controls.

For sensitive security concerns, use the repository [Security Policy and Vulnerability Disclosure](https://github.com/AsiBackbone/AsiBackbone/blob/main/SECURITY.md). This article is a public wording guide, not a private reporting channel or certification statement.

## Current-stage wording rules

| Topic | Preferred wording | Avoid wording |
| --- | --- | --- |
| Project identity | `Accountable Systems Infrastructure`, governance spine, decision governance infrastructure | artificial superintelligence implementation, AI model, ASI engine |
| Current audit records | audit residue, audit ledger record, durable local/outbox record when configured | immutable audit store or production-grade evidence by default |
| Stable Core signing primitives | signing-ready metadata, canonical payload hashing, provider-neutral signing and verification seams, verification-policy primitives | cryptographically signed by default or automatically verified by default |
| Local-development signing provider | released local-development signer for tests, samples, local validation, and wiring proof paths | production signer or production key custody |
| Managed-key adapter boundary | released managed-key signing adapter boundary with host-owned managed-key client and operational policy | built-in cloud key service or automatic production trust |
| Provider integrations | optional provider package, host adapter, downstream emission or enrichment | required platform, Core dependency, or authoritative audit store by default |
| Cloud observability | operational search, alerting, dashboards, and diagnostics after local persistence | substitute for durable local accountability |
| Purview | strategy-only governance, catalog, classification, and lineage enrichment unless later released | released Purview package in the current stable package family |
| Gateway or robotics work | future, sample, or host-owned gateway pattern unless released | direct physical-control implementation in the current package family |

## Released signing categories

Use these categories when documenting current stable signing-related behavior.

| Category | Current status | Safe wording |
| --- | --- | --- |
| Core signing-ready and verification primitives | Stable in `AsiBackbone.Core`. | Core provides canonical payload hashing, signing-ready metadata, signing request/result contracts, verification request/result contracts, and verification-policy primitives. |
| Local-development signing provider | Stable package: `AsiBackbone.Signing.LocalDevelopment`. | The local-development signer is for tests, samples, deterministic local validation, and host wiring proof paths. It is not production key custody. |
| Managed-key signing adapter | Stable package: `AsiBackbone.Signing.ManagedKey`. | The managed-key package provides an adapter boundary. The host supplies the actual managed-key client, credentials, key operations, verification path, monitoring, and operational policy. |
| Concrete cloud/HSM/KMS implementation packages | Future or host-owned unless separately released. | Cloud key services, certificate stores, and organization-specific clients remain host-owned implementations or future provider packages. |
| Production-grade audit integrity claims | Not provided by default. | Stronger claims require deployed signing, verification, protected key management, durable storage, retention policy, monitoring, incident response, and any external chain or anchor design the host depends on. |

Do not imply that signing and verification are purely future work. Do not imply that the released signing surfaces provide production-grade audit integrity by themselves.

## Required qualifiers for future or provider work

Use explicit qualifiers when behavior is not part of the currently released stable package surface:

- `planned`;
- `optional`;
- `preview`;
- `design-only`;
- `strategy-only`;
- `sample-only`;
- `host-owned`;
- `provider-specific`;
- `future provider package`;
- `when configured`;
- `after durable local persistence`;
- `after signing, verification, key-management, and storage guarantees are implemented`.

Avoid wording that implies future provider work is required for Core consumers.

## Durable local accountability

External providers can enrich, search, alert, stream, classify, catalog, or display governance events. They should not be described as replacing durable local accountability unless the host explicitly designs and documents them as the authoritative audit store.

Default wording should preserve this sequence:

```text
Decision or acknowledgment
  -> audit residue / governance record
  -> durable local store or outbox when configured
  -> optional signing or verification boundary when configured
  -> optional provider emission
  -> provider-specific enrichment, search, alerting, or cataloging
```

Recommended language:

- preserve local audit/outbox records before provider emission;
- emit minimized envelopes to external systems;
- treat provider failures as retry, defer, quarantine, or escalate conditions;
- keep provider packages optional and downstream of Core;
- describe signed artifacts as signed only after a configured signing provider returns signature metadata;
- describe verified artifacts as verified only after verification policy has run.

## Package dependency language

Core remains provider-neutral. Provider packages depend on Core. Core must not depend on OpenTelemetry, Azure Monitor, Event Hubs, Purview, SIEM, signing providers, cloud SDKs, robotics SDKs, HSM SDKs, KMS SDKs, certificate stores, or immutable storage systems.

Appropriate wording:

- `AsiBackbone.Core` defines provider-neutral governance primitives and seams.
- `AsiBackbone.Signing.LocalDevelopment` is a released local-development provider package for tests, samples, and wiring proof paths.
- `AsiBackbone.Signing.ManagedKey` is a released adapter boundary for host-owned managed-key clients.
- Provider packages or host adapters translate Core records into provider-specific systems.
- Provider packages are optional integration surfaces.
- Host applications choose whether to install and configure a provider package.

Avoid wording that implies:

- Core requires a cloud provider;
- Core requires a certificate, managed key, HSM, or KMS;
- the local-development signer is production-safe;
- the managed-key adapter includes a live cloud key implementation by default;
- OpenTelemetry, Azure Monitor, Event Hubs, Purview, or SIEM replaces local accountability;
- provider-specific packages define Core semantics.

## Preview, design, and sample language

Use preview, design-only, strategy-only, or sample wording for APIs, provider pages, or packages that are not part of the stable package family.

Do not imply that a preview, strategy-only, design-only, or sample package:

- is production-hardened;
- is covered by the same compatibility promise as stable packages;
- provides compliance or operational guarantees;
- is required to use Core;
- changes the project boundary from governance infrastructure into an intelligence engine.

Stable documentation may discuss earlier release history, but should keep historical limitations separate from current stable behavior and future provider planning.

## Release and PR checklist

Before merging documentation for provider or security work, confirm:

- current behavior is separated from planned or future behavior;
- stable Core signing-ready and verification primitives are not described as future-only;
- the local-development signing provider remains clearly non-production;
- the managed-key signing provider remains clearly an adapter boundary with host-owned client implementation;
- stronger audit-integrity claims are tied to implemented and deployed features;
- provider dependencies remain outside Core;
- durable local/outbox persistence remains the reliability baseline before provider emission;
- host responsibilities for security, privacy, retention, key management, verification, monitoring, and compliance are explicit;
- no wording claims AsiBackbone is an intelligence engine, controls physical systems, or replaces governance, security, or operational review.

## Related documentation

- [Security Policy and Vulnerability Disclosure](https://github.com/AsiBackbone/AsiBackbone/blob/main/SECURITY.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
- [Key Rotation and Retired-Key Verification](key-rotation-and-retired-key-verification.md)
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
