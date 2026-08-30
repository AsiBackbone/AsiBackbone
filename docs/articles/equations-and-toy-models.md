# Equations and Toy Models: Product Boundary

The conceptual equation material that inspired parts of the architecture is now canonical in [ASI Backbone Learning](https://asibackbone.github.io/Learning/architecture/constraint-conditioned-decision-model.html).

This URL is retained for continuity.

## Product meaning

AsiBackbone does **not** compute or implement a physical or mathematical collapse law.

The software-relevant idea is simply:

> A proposed action becomes only what the active policy structure permits.

In product terms, that maps to:

~~~text
Intent
  -> Policy context
  -> Constraint evaluation
  -> Explicit GovernanceDecision
  -> Acknowledgment or escalation when required
  -> Optional scoped capability
  -> Host-owned execution
~~~

## Product-owned implementation references

- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Core API Domain Model](core-domain-language.md)
- [Constraint Exception Policy](constraint-exception-policy.md)
- [Policy Versioning and Decision Provenance](https://asibackbone.github.io/Learning/governance/policy-versioning-and-decision-provenance.html)
- [Generated API Reference](../api/)

The Learning article owns the general equation and toy-model explanation. This repository owns the concrete types, outcomes, runtime behavior, and package contracts.
