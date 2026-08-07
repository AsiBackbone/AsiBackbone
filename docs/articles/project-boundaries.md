# Project Boundaries and Non-Claims

This page is the canonical boundary reference for AsiBackbone documentation. Use it when a page needs to point readers to the full non-claim posture without repeating every disclaimer inline.

> [!IMPORTANT]
> In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is practical .NET governance infrastructure for accountable software decision flow, not an artificial superintelligence implementation.

## Short scope statement

AsiBackbone is a governance spine, not an intelligence engine. It helps host applications evaluate consequential actions before execution, preserve decision evidence, and keep operational authority with the host. It does not implement ASI, host AI models, control robots, certify compliance, or provide legal protection by itself.

Use this short statement when a page needs a boundary reminder but not the full non-claim list.

## What AsiBackbone is

AsiBackbone provides .NET building blocks and host integration seams for governing consequential software actions before the host executes them.

It helps hosts model and preserve:

- policy evaluation context,
- constraints and decision policy,
- structured `GovernanceDecision` outcomes,
- reason codes and policy metadata,
- acknowledgment workflows,
- audit residue and decision receipts,
- durable audit/outbox records where configured,
- optional governance emission,
- optional signing-ready or provider-signed artifacts,
- optional capability-scoped continuation.

## What AsiBackbone is not

AsiBackbone does not claim to be:

- an artificial superintelligence implementation,
- an AI model, model host, training system, prompt runtime, agent runtime, or orchestration engine,
- an autonomous execution engine,
- a robotics or physical-control system,
- a legal, regulatory, audit-framework, or compliance certification product,
- production tamper-evidence, immutability, non-repudiation, external anchoring, or legal protection by default,
- a replacement for authentication, authorization, security review, legal review, organizational governance, DLP review, key management, monitoring, backup, or operational controls.

## Host-owned responsibilities

The consuming host remains responsible for:

- authentication and authorization,
- business rules and execution behavior,
- policy authorship, policy review, and policy approval,
- database provider selection, migrations, retention, backup, and access control,
- UI or API response behavior,
- deployment and monitoring,
- exporter configuration and observability backend interpretation,
- data classification, privacy review, and DLP enforcement,
- key custody, signing provider deployment, verification policy, and rotation,
- operational approval, escalation, incident handling, and legal/compliance review.

## Documentation consolidation guidance

To keep documentation readable, not every page should repeat the full boundary list. Prefer the smallest boundary reminder that fits the page:

| Page type | Preferred boundary treatment |
| --- | --- |
| README, package READMEs, getting-started pages | Include the short scope statement and link here. Keep first-time readers oriented. |
| Implementation guides | Mention the host-owned boundary relevant to the guide, then link here for the complete non-claim posture. |
| Release notes and release-readiness records | State whether the release changes the public boundary; link here instead of repeating the full disclaimer block. |
| Historical pages | Preserve historically important boundary notes, but avoid adding new repeated boilerplate. |
| Scenario or conceptual pages | Translate conceptual language back into implementation language and link here for scope. |

Recommended concise reference:

> AsiBackbone is a governance spine, not an intelligence engine. See [Project Boundaries and Non-Claims](project-boundaries.md) for the full scope statement.

For repository-root or package README files, use the published documentation link:

> AsiBackbone is a governance spine, not an intelligence engine. See [Project Boundaries and Non-Claims](https://asibackbone.github.io/AsiBackbone/articles/project-boundaries.html) for the full scope statement.

## How to phrase the boundary

Prefer implementation-grounded wording:

| Say | Avoid implying |
| --- | --- |
| Accountable Systems Infrastructure | Artificial superintelligence |
| Policy decision pipeline | Autonomous decision-maker |
| Decision boundary | Physical or metaphysical collapse claim |
| Audit residue or decision receipt | Legal non-repudiation by default |
| Signing-ready or provider-signed artifact | Tamper-proof evidence without storage and key controls |
| Host-owned execution boundary | Package-owned execution authority |
| Optional provider emission | Mandatory cloud governance service |
| Sample-only, design-only, or strategy-only | Released stable package surface |

## Handling conceptual language

Some pages use broader Eden/Backbone or collapse language as optional conceptual background. Implementation pages should translate those terms back into ordinary software architecture language:

| Conceptual phrase | Implementation phrase |
| --- | --- |
| Collapse boundary | Decision boundary |
| Residue | Audit record, decision receipt, lifecycle event, or outbox record |
| Backbone | Governance spine or policy decision pipeline |
| Future provider path | Design-only, strategy-only, sample-only, or host-owned integration until released |

## Related documentation

- [Implementation-First Adoption Path](implementation-first-adoption.md)
- [Terminology Map](terminology-map.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
