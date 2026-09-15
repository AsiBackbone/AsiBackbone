---
description: Public API naming convention and 5.x-to-6.0 semantic-name migration map for AsiBackbone.
---

# Public API Naming in 6.0

AsiBackbone 6.0 treats namespaces and semantic domain qualifiers as separate concerns. A public type should not repeat
`AsiBackbone` merely because it lives in an AsiBackbone namespace. Product wording is retained when the type represents a
product integration boundary, package entry point, wire/schema identity, or a concrete product implementation where the
qualifier carries useful meaning.

## Naming rule

Use the smallest name that remains unambiguous at the call site:

- prefer `Governance`, `Decision`, `Capability`, `Acknowledgment`, `Execution`, `Receipt`, `Policy`, and similar domain
  qualifiers when they identify the concept;
- do not replace meaningful qualifiers with overly generic names such as `Context`, `Result`, `Options`, or `Service`;
- retain `AsiBackbone` for product-facing registration/builders, package adapters, schema/version identifiers, and concrete
  implementation names where removing it would erase useful ownership information;
- keep namespaces stable unless a namespace move materially improves the model. The 6.0 naming pass does not reorganize
  namespaces solely to shorten type names.

The resulting lifecycle should read as domain language first:

```text
GovernanceEvaluationContext
  -> IGovernanceConstraint<TContext>
  -> IGovernancePolicyEvaluator<TContext>
  -> GovernanceDecision
  -> acknowledgment when required
  -> CapabilityTokenGrant when execution authority is granted
  -> host-owned execution / execution receipt
  -> durable audit evidence
```

## Canonical 6.0 names

| 5.x / compatibility name | Preferred 6.0 name | Rationale |
| --- | --- | --- |
| `IAsiBackboneConstraint<TContext>` | `IGovernanceConstraint<TContext>` | The domain is governance constraint evaluation; the namespace already identifies the product. |
| `IAsiBackboneConstraintEvaluationContext` | `IGovernanceEvaluationContext` | Keeps the semantic qualifier and removes the product-as-namespace prefix. |
| `AsiBackboneConstraintEvaluationContext` | `GovernanceEvaluationContext` | Reads naturally beside constraints and policy evaluation. |
| `IAsiBackbonePolicyEvaluator<TContext>` | `IGovernancePolicyEvaluator<TContext>` | Makes the governed-decision role explicit without repeating the product name. |
| `IAsiBackboneDecisionPolicy<TContext>` | `IGovernanceDecisionPolicy<TContext>` | Distinguishes the post-composition decision policy from generic policy services. |
| `AsiBackbonePolicyEvaluatorOptions` | `GovernancePolicyOptions` | Keeps the governance domain while shortening the implementation detail. |
| `IAsiBackboneActorContext` | `IGovernanceActorContext` | Actor identity is contextual input to governance, not an AsiBackbone-specific identity system. |
| `AsiBackboneActorContext` | `GovernanceActorContext` | Matches the domain-qualified actor contract. |

The 6.0 semantic names are source-compatible bridges over the stable 5.x contracts. Existing product-prefixed contracts
remain available in 6.0 so package integrations and external implementations are not forced through a second migration while
the major-release cleanup is landing. New documentation and new application code should prefer the semantic names above.

## Names intentionally retained

The review intentionally keeps names where `AsiBackbone` conveys product ownership rather than duplicating the namespace.
Examples include `DefaultAsiBackbonePolicyEvaluator<TContext>`, `AsiBackbonePolicyEvaluatorBuilder<TContext>`,
`IAsiBackboneBuilder`, `AsiBackboneBuilder`, `AsiBackboneSchemaVersions`, and ASP.NET Core package registration/integration
surfaces. These are product implementation or integration entry points, not domain entities.

`AsiBackboneActorType` is also retained in this pass because it is already embedded in audit and integration contracts. The
actor context gets a semantic name without forcing a serialized classification rename into the same change.

## Audit terminology coordination

Issue #781 owns the audit-vocabulary decision around `AuditResidue`, `IAsiBackboneAuditSink`, and the proposed
`DecisionReceipt` terminology. This naming pass does not pre-empt that decision. Keeping audit changes together avoids a
sequence such as `IAsiBackboneAuditSink` -> `IGovernanceAuditSink` -> `IDecisionReceiptSink` within the same major-release
workstream.

## Guidance for new APIs

Before adding a public type, ask these questions in order:

1. What domain concept does the type represent?
2. Does the namespace already communicate the product/package owner?
3. Would removing `AsiBackbone` make the name ambiguous at a normal call site?
4. Is the type a product integration entry point rather than a domain concept?
5. Is the chosen name consistent with the governance lifecycle and neighboring types?

When a semantic qualifier answers the first question, prefer it over a product prefix.
