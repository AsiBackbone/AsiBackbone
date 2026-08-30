---
description: Concrete AsiBackbone.Core API domain model, type mappings, outcome contract, and implementation invariants.
---

# Core API Domain Model

This article documents the concrete domain model of `AsiBackbone.Core` and the host-neutral API boundary that the rest of the package family builds on.

For canonical architecture definitions, use the [ASI Backbone Learning Architecture Glossary](https://asibackbone.github.io/Learning/architecture/glossary.html). This page owns the **product mapping**: exact Core types, current outcome semantics, package boundaries, and implementation invariants.

In this software project, **ASI** means **Accountable Systems Infrastructure**. `AsiBackbone.Core` is governance infrastructure, not an intelligence engine or AI-model package.

> [!IMPORTANT]
> Learning terminology explains the architecture. The released `AsiBackbone.Core` API defines how this package implements that architecture. If a teaching example is intentionally smaller than the product surface, the product API and runtime documentation remain authoritative for implementation behavior.

## Core technical lane

`AsiBackbone.Core` supports this host-neutral decision lane:

```text
Proposed operation
  -> host builds policy/evaluation context
  -> constraints evaluate
  -> policy evaluator composes GovernanceDecision
  -> optional acknowledgment workflow
  -> audit residue / lifecycle evidence
  -> optional capability grant
  -> host or gateway decides whether to execute
```

Core defines the governance primitives for this lane. It does not own the external side effect.

## Architecture concept to Core API

| Learning concept | Core API mapping | Core contract |
| --- | --- | --- |
| Actor context | [`IAsiBackboneActorContext`](xref:AsiBackbone.Core.Actors.IAsiBackboneActorContext) | Framework-neutral actor data supplied by the host. Core does not authenticate the actor. |
| Policy context | [`IAsiBackboneConstraintEvaluationContext`](xref:AsiBackbone.Core.Constraints.IAsiBackboneConstraintEvaluationContext), [`AsiBackboneConstraintEvaluationContext`](xref:AsiBackbone.Core.Constraints.AsiBackboneConstraintEvaluationContext) | Carries the decision-relevant input used by constraints and evaluation. |
| Constraint | [`IAsiBackkboneConstraint<TContext>`](xref:AsiBackbone.Core.Constraints.IAsiBackboneConstraint`1) | Evaluates one policy condition without performing the governed side effect. |
| Constraint result | [`ConstraintEvaluationResult`](xref:AsiBackbone.Core.Constraints.ConstraintEvaluationResult) | Carries the constraint's product result/reasons into decision composition. |
| Policy evaluation | [`IAsiBackbonePolicyEvaluator<TContext>`](xref:AsiBackbone.Core.Evaluation.IAsiBackbonePolicyEvaluator`1) | Composes constraint results into a governance decision. |
| Decision policy | [`IAsiBackboneDecisionPolicy<TContext>`](xref:AsiBackbone.Core.Evaluation.IAsiBackboneDecisionPolicy`1) | Optional post-composition policy hook that can reshape or raise the final decision. |
| Decision outcome | [`GovernanceDecision`](xref:AsiBackbone.Core.Decisions.GovernanceDecision), [`GovernanceDecisionOutcome`](xref:AsiBackbone.Core.Decisions.GovernanceDecisionOutcome) | Structured product outcome, policy identity metadata, reason data, and correlation information. |
| Acknowledgment | [`LiabilityHandshakeRequest`](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeRequest), [`LiabilityHandshakeAcknowledgment`](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeAcknowledgment) | Product acknowledgment request/response primitives. Naming is preserved for API compatibility and does not create legal protection. |
| Audit residue | [`AuditResidue`](xref:AsiBackbone.Core.Audit.AuditResidue) | Structured evidence of the governance decision. |
| Audit ledger | [`AuditLedgerRecord`](xref:AsiBackbone.Core.Audit.AuditLedgerRecord), [`IAsiBackboneAuditLedgerStore`](xref:AsiBackbone.Core.Audit.IAsiBackboneAuditLedgerStore) | Storage-ready record and provider-neutral persistence contract. |
| Audit sink | [`IAsiBackboneAuditSink`](xref:AsiBackbone.Core.Audit.IAsiBackboneAuditSink) | Provider-neutral boundary for receiving audit residue. |
| Scoped capability | [`CapabilityTokenGrant`](xref:AsiBackbone.Core.CapabilityTokens.CapabilityTokenGrant), [`CapabilityGrantValidator`](xref:AsiBackbone.Core.CapabilityTokens.CapabilityGrantValidator) | Bounded grant data plus product validation logic. |
| Operation result | [`OperationResult`](xref:AsiBackbone.Core.Results.OperationResult) | Package-operation success/failure, deliberately separate from governance outcome. |

Not every architecture term maps to one class. **Governance spine**, **host-owned execution**, **operational gateway**, **decision provenance**, **active policy structure**, and similar terms describe relationships among APIs and host responsibilities rather than a required universal type.

## Decision outcome contract

The current [`GovernanceDecisionOutcome`](xref:AsiBackbone.Core.Decisions.GovernanceDecisionOutcome) surface is:

| Outcome | Product runtime meaning |
| --- | --- |
| `Allowed` | Governance permits continuation. The host still decides whether and how to execute. |
| `Warning` | Governance permits continuation while retaining warning reasons. |
| `Denied` | The governed operation must not proceed through the protected path. |
| `Deferred` | The host should pause or route the operation for later evaluation rather than treating the current decision as permission. |
| `AcknowledgmentRequired` | The host must complete the configured acknowledgment workflow before any later execution path that requires it. |
| `EscalationRecommended` | The request should move to a higher review/authority path before execution. |

Learning may omit `Warning` from a foundational example. That teaching simplification does not change the released enum.

## Core implementation invariants

### Decision is not execution

Policy evaluation produces `GovernanceDecision`; Core does not perform the protected external action. Hosts and gateways own the transition from decision data into real side effects.

### Acknowledgment is not authorization

`LiabilityHandshakeAcknowledgment` records acknowledgment state. It does not authenticate an actor, override authorization, certify compliance, or automatically create execution authority.

### Capability grant is bounded authority data

`CapabilityTokenGrant` is not a general-purpose command channel. Hosts remain responsible for validating the grant at the relevant execution boundary under the configured capability-validation policy.

### Audit evidence does not imply storage guarantees

`AuditResidue`, `AuditLedgerRecord`, and sink/store contracts define evidence shapes and persistence seams. Durability, retention, cryptographic signing, immutability, and tamper evidence depend on the selected implementation and host operations.

### Governance outcome is not operation result

A `GovernanceDecision` can deny or defer before execution begins. An `OperationResult` describes whether a package operation itself succeeded. The two result families must not be treated as interchangeable.

### Core remains host-neutral

Core does not depend on ASP.NET Core, EF Core, a specific identity provider, a database, a cloud platform, or an AI model runtime.

## Policy identity and explainability

The product decision model carries policy and correlation metadata so a host can explain which policy state produced a decision.

Current product concepts include:

- policy version through `GovernanceDecision.PolicyVersion`;
- policy fingerprint through `GovernanceDecision.PolicyHash`;
- reason codes and reason messages;
- correlation identifiers that connect decision, audit, lifecycle, and host records.

A policy version is a readable generation label. A policy hash/fingerprint identifies effective policy material more precisely. The product keeps them separate.

## Core boundary

### In scope for `AsiBackbone.Core`

- framework-neutral actor and evaluation-context abstractions;
- constraint contracts and constraint results;
- policy evaluation and decision-policy contracts;
- `GovernanceDecision` and `GovernanceDecisionOutcome`;
- operation-result primitives;
- acknowledgment/handshake primitives;
- audit residue, audit ledger record, sink/store, and lifecycle primitives;
- capability-grant primitives and validation;
- policy identity/version/hash and reason metadata;
- correlation support and shared value objects.

### Out of scope for `AsiBackbone.Core`

- ASP.NET Core middleware, endpoint metadata, HTTP result mapping, or challenge presentation;
- EF Core mappings, migrations, and concrete durable database behavior;
- concrete cloud/provider integrations;
- production key custody and host trust policy;
- application authentication and authorization systems;
- direct AI model hosting, training, inference, or orchestration;
- host startup logic;
- robotics or other physical-control implementation;
- the protected side effect itself.

## Related implementation documentation

- [AsiBackbone API Glossary](glossary.md)
- [AsiBackbone API Terminology Map](terminology-map.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [Capability Grant Hardening](capability-grant-hardening.md)
- [Generated API Reference](../api-reference.md)
