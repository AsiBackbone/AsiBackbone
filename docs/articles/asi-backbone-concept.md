# ASI Backbone Concept: Product Boundary

In this software project, **ASI** means **Accountable Systems Infrastructure**.

The broader architecture teaching for Accountable Systems Infrastructure is canonical in [ASI Backbone Learning](https://asibackbone.github.io/Learning/architecture/accountable-systems-infrastructure-and-governed-execution.html).

This page is retained at its existing URL for continuity and documents only the concrete product boundary.

## What AsiBackbone implements today

AsiBackbone is a .NET governance spine for consequential software actions. The stable package family provides implementation surfaces for:

- policy context and constraint evaluation;
- explicit governance decisions;
- acknowledgment/responsibility workflows;
- audit residue and lifecycle events;
- capability-scoped continuation authority;
- durable local audit/outbox persistence;
- optional governance emission;
- host-owned execution boundaries.

The generated API reference and implementation guides define the exact public types and runtime semantics.

## Package and API surface

Use these product-owned references:

- [Core API Domain Model](core-domain-language.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Acknowledgment Workflow](dynamic-liability-handshake.md)
- [Capability Grant Hardening](capability-grant-hardening.md)
- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [Generated API Reference](../api-reference.md)

## What the host still owns

The consuming application remains responsible for:

- authentication and ordinary authorization;
- authoritative actor/resource lookup;
- policy authorship and policy-source retention;
- persistence registration and migrations;
- UI and workflow presentation;
- secrets, credentials, and key custody;
- external or physical execution;
- operational safety and compliance review.

## Production and security boundaries

AsiBackbone does not implement artificial superintelligence, train or host AI models, control robots, certify compliance, or make an audit record tamper-evident by default.

Use the product security and operations documentation for exact signing, verification, outbox, persistence, and production-hardening behavior.

## ASIBackbone ↔ Eden Hypothesis terminology bridge

ASIBackbone is the **operational framework**: it implements governance, policy evaluation, evidence handling, acknowledgment, escalation, audit, capability gating, and constrained execution. The Eden Hypothesis is a **theoretical framework** that models collapse as relational, stochastic, path-dependent, and conditioned by an active structure.

The relationship below is structural and analogical, not literal. AsiBackbone does not implement a physical collapse law, establish the Eden Hypothesis as physics, or make runtime behavior depend on Eden terminology. Where an earlier conceptual name differs from the current 6.0 product API, the current product surface is shown explicitly.

| ASIBackbone terminology | Current 6.0 product surface | Eden Hypothesis term | Structural relationship |
| --- | --- | --- | --- |
| `BackboneDecision` | `GovernanceDecision` / `GovernanceDecisionOutcome` | Collapse outcome | Narrows multiple possible actions to one bounded governance disposition; the decision remains separate from execution. |
| `PolicyBundleSnapshot` | Effective policy identified by `GovernanceDecision.PolicyVersion` and `GovernanceDecision.PolicyHash` | Active constraint structure `Sτ` | Represents the effective rules and constraints shaping the allowed action space at decision time. |
| `RegionalContext` | Host-supplied governance/evaluation context | Relational/local conditioning | Supplies legal, cultural, jurisdictional, or other local facts that can change which outcomes are permissible. |
| `EvidenceItem` | Decision-relevant host facts, constraint results, reasons, and audit evidence | Observed relation / constraint evidence | Supplies observations that influence the current structure-conditioned decision without becoming execution authority by itself. |
| `Defer` | `GovernanceDecisionOutcome.Deferred` | Unresolved collapse | The decision cannot yet safely resolve because required evidence, conditions, or timing are incomplete. |
| `RequireAcknowledgment` | `GovernanceDecisionOutcome.AcknowledgmentRequired` plus the acknowledgment workflow | Reflexive collapse checkpoint | A consequential transition requires explicit human recognition before a later execution path may continue. Acknowledgment is not authorization. |
| `Escalate` | `GovernanceDecisionOutcome.EscalationRecommended` | Higher-order arbitration | Local policy cannot safely resolve the action without broader review or authority. |
| `DecisionReceipt` | `AuditResidue` / `AuditLedgerRecord` and correlated lifecycle evidence | Collapse record | Preserves auditable evidence explaining why a path was selected, rejected, deferred, or escalated. |
| `PolicySnapshotHash` | `GovernanceDecision.PolicyHash` | Constraint-state fingerprint | Identifies the effective policy material that conditioned the decision so the governing state can be correlated and verified. |
| Capability token | `CapabilityTokenGrant` plus execution-boundary validation | Bounded branch authorization | Grants narrow, scoped, time-limited continuation authority for only the approved branch of action. |
| Revocation / break-glass | Host and provider revocation, expiration, re-evaluation, and emergency-control paths | Return / re-opening path | Stops, expires, or reopens a previously narrowed path so changed evidence, policy, or emergency conditions can be reconsidered. |

The strongest alignment is **structure-conditioned narrowing**. In the revised Eden formulation, open possibility resolves only into states allowed by the active structure. In AsiBackbone, a requested action resolves only into outcomes allowed by active policy, evidence, regional or local context, acknowledgment requirements, and capability constraints. The analogy is useful because both models make the available branch set depend on the structure present at the point of decision.

Return matters as much as narrowing. Revocation, expiration, re-evaluation, and break-glass behavior are operational analogues for reopening or revising a previously narrowed path when the governing conditions change. They do not reverse history; they create a governed route for reconsidering what may happen next.

This bridge is documentation-only. It does not merge the Eden Hypothesis into the product namespace, rename public APIs, or change runtime behavior.

## Deeper Learning material

For the general architecture and conceptual lineage, use:

- [Accountable Systems Infrastructure and Governed Execution](https://asibackbone.github.io/Learning/architecture/accountable-systems-infrastructure-and-governed-execution.html)
- [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/Learning/architecture/intent-to-execution-accountability-pattern.html)
- [Constraint-Conditioned Decision Model](https://asibackbone.github.io/Learning/architecture/constraint-conditioned-decision-model.html)

Learning is the educational source of truth. This repository remains authoritative for package/API/runtime truth.
