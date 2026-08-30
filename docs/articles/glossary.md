---
description: Implementation/API glossary mapping ASI Backbone Learning terminology to concrete AsiBackbone packages, types, runtime semantics, and host responsibilities.
---

# AsiBackbone API Glossary

This page is the **implementation-side glossary** for the AsiBackbone package family. It maps the architecture vocabulary taught in ASI Backbone Learning to concrete `AsiBackbone.*` APIs and product behavior.

For canonical educational definitions, terminology lineage, and comparisons with established architecture concepts, use the [ASI Backbone Learning Architecture Glossary](https://asibackbone.github.io/Learning/architecture/glossary.html) and [Terminology and Established Architecture Concepts](https://asibackbone.github.io/Learning/architecture/terminology-and-established-concepts.html).

> [!IMPORTANT]
> Learning owns the teaching definition of the architecture concepts. This repository owns exact type names, namespaces, API contracts, package-specific semantics, implementation invariants, and released runtime behavior. Learning does not override the product contract documented here or in the generated API reference.

Terms such as responsibility handshake or liability handshake describe accountability workflows in the software model. They do not create legal protection, legal advice, regulatory compliance, or a substitute for organizational review.

## Concept-to-API mapping

| Learning concept | Current AsiBackbone API / product mapping | Product-specific semantics |
| --- | --- | --- |
| Governance spine | [`IAsiBackbonePolicyEvaluator<TContext>`](xref:AsiBackbone.Core.Evaluation.IAsiBackbonePolicyEvaluator`1), [`IAsiBackboneConstraint<TContext>`](xref:AsiBackbone.Core.Constraints.IAsiBackboneConstraint`1), [`GovernanceDecision`](xref:AsiBackbone.Core.Decisions.GovernanceDecision), audit and capability surfaces | There is no single `GovernanceSpine` type. The term describes the composition of product APIs around a host-owned execution boundary. |
| Intent / request | [`AsiBackboneConstraintEvaluationContext`](xref:AsiBackbone.Core.Constraints.AsiBackboneConstraintEvaluationContext) carries proposed operation data | The product does not require one universal `Intent` base type. Hosts map application-specific proposal data into the evaluation context. |
| Actor context | [`IAsiBackboneActorContext`](xref:AsiBackbone.Core.Actors.IAsiBackboneActorContext) | Core remains host-neutral. Authentication stays host-owned; integrations adapt authoritative host identity into the Core abstraction. |
| Policy context | [`IAsiBackboneConstraintEvaluationContext`](xref:AsiBackbone.Core.Constraints.IAsiBackboneConstraintEvaluationContext), [`AsiBackboneConstraintEvaluationContext`](xref:AsiBackbone.Core.Constraints.AsiBackboneConstraintEvaluationContext) | The context is the product evaluation input. Hosts remain responsible for supplying trustworthy actor, operation, resource, region, risk, and policy metadata. |
| Constraint | [`IAsiBackboneConstraint<TContext>`](xref:AsiBackbone.Core.Constraints.IAsiBackboneConstraint`1), [`ConstraintEvaluationResult`](xref:AsiBackbone.Core.Constraints.ConstraintEvaluationResult) | Constraints contribute decision information; they do not own the governed side effect. |
| Policy evaluation | [`IAsiBackbonePolicyEvaluator<TContext>`](xref:AsiBackbone.Core.Evaluation.IAsiBackbonePolicyEvaluator`1) | Evaluation composes constraint results into a product decision and does not execute the protected operation. |
| Decision policy | [`IAsiBackboneDecisionPolicy<TContext>`](xref:AsiBackbone.Core.Evaluation.IAsiBackboneDecisionPolicy`1) | Optional host policy can reshape or raise the composed decision after constraint evaluation. |
| Decision outcome | [`GovernanceDecision`](xref:AsiBackbone.Core.Decisions.GovernanceDecision), [`GovernanceDecisionOutcome`](xref:AsiBackbone.Core.Decisions.GovernanceDecisionOutcome) | The released enum includes `Allowed`, `Warning`, `Denied`, `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended`. A decision is not execution authority. |
| Acknowledgment | [`LiabilityHandshakeRequest`](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeRequest), [`LiabilityHandshakeAcknowledgment`](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeAcknowledgment); ASP.NET Core challenge support includes [`AsiBackboneAcknowledgmentChallenge`](xref:AsiBackbone.AspNetCore.Handshakes.AsiBackboneAcknowledgmentChallenge) | Acknowledgment records acceptance of a challenge or responsibility statement. It is not authentication, authorization, legal protection, or an automatic execution grant. |
| Audit residue | [`AuditResidue`](xref:AsiBackbone.Core.Audit.AuditResidue) | The type carries structured decision evidence. Durability, retention, signing, integrity protection, and backend storage remain separate concerns. |
| Audit ledger | [`AuditLedgerRecord`](xref:AsiBackbone.Core.Audit.AuditLedgerRecord), [`IAsiBackboneAuditLedgerStore`](xref:AsiBackbone.Core.Audit.IAsiBackboneAuditLedgerStore) | The product exposes storage-ready records and contracts; concrete persistence behavior depends on the selected provider and host configuration. |
| Audit sink | [`IAsiBackboneAuditSink`](xref:AsiBackbone.Core.Audit.IAsiBackboneAuditSink) | A sink receives audit residue. It does not by itself imply durable or tamper-evident storage. |
| Scoped capability / capability grant | [`CapabilityTokenGrant`](xref:AsiBackbone.Core.CapabilityTokens.CapabilityTokenGrant), [`CapabilityGrantValidator`](xref:AsiBackbone.Core.CapabilityTokens.CapabilityGrantValidator) | The product models bounded execution authority through grant data and validation. A token format alone does not create least privilege. |
| Host-owned execution | [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md) and host/gateway code | There is intentionally no universal executor owned by Core. The host retains control of the real side effect. |
| Operational gateway | [AI Agent Gateway Scenario](scenarios/ai-agent-gateway.md), [Robotics Operational Gateway](scenarios/robotics-operational-gateway.md) | A gateway is an integration pattern, not one mandatory base type. It validates current decision/authority state before external execution. |
| Policy version | [`GovernanceDecision`](xref:AsiBackbone.Core.Decisions.GovernanceDecision) | `PolicyVersion` is the product's readable policy-generation label. |
| Policy fingerprint | [`GovernanceDecision`](xref:AsiBackbone.Core.Decisions.GovernanceDecision) | `PolicyHash` is the current product field used for the effective-policy fingerprint. |
| Reason codes | [`GovernanceDecision`](xref:AsiBackbone.Core.Decisions.GovernanceDecision), [`ConstraintEvaluationResult`](xref:AsiBackbone.Core.Constraints.ConstraintEvaluationResult) | Reason codes are machine-readable product explanations and should remain safe to persist or expose according to host policy. |
| Correlation ID | [`AsiBackboneHttpRequestCorrelation`](xref:AsiBackbone.AspNetCore.Correlation.AsiBackboneHttpRequestCorrelation) plus correlation fields on governance records | Correlation connects request, decision, audit, lifecycle, and telemetry records; it is not an authorization credential. |
| Operation result | [`OperationResult`](xref:AsiBackbone.Core.Results.OperationResult) | Product operation success/failure is deliberately separate from governance outcome. A policy denial is not the same thing as infrastructure failure. |

## Implementation-specific additions

Some public product concepts are intentionally more detailed than the smallest Learning examples.

### `Warning` outcome

The current [`GovernanceDecisionOutcome`](xref:AsiBackbone.Core.Decisions.GovernanceDecisionOutcome) enum includes `Warning`. A warning permits continuation through the governance layer while retaining warning reasons. Learning may use a five-outcome teaching model when that keeps a tutorial focused; that simplification does not change the released enum.

### Audit lifecycle, outbox, emission, and signing

The package family includes implementation surfaces for audit lifecycle records, durable outbox handling, governance emission, signing, and verification. These are product/runtime concerns documented in this repository. Learning may teach the architectural reason for those boundaries, but exact provider behavior, configuration, retries, storage semantics, and cryptographic posture remain product-owned.

## Product invariants

The following statements are implementation boundaries, not replacement teaching definitions:

- An `Allowed` decision means the governance layer permits continuation; the host still owns whether and how execution occurs.
- Acknowledgment does not grant access-control permission and does not automatically become execution authority.
- Audit residue is structured evidence, not a claim of durability, immutability, or tamper evidence by itself.
- Capability grants must be validated according to the host's configured execution-boundary rules.
- Policy version and policy fingerprint are distinct product fields with different meanings.
- Governance outcome and package operation result remain separate concepts.

## Read next

- [Core API Domain Model](core-domain-language.md)
- [AsiBackbone API Terminology Map](terminology-map.md)
- [Generated API Reference](../api-reference.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
- [Documentation Ownership](documentation-ownership.md)
