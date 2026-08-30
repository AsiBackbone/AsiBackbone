---
description: Stable entry point for the generated AsiBackbone public API reference.
---

# AsiBackbone API Reference

This page is the stable entry point for the generated AsiBackbone API documentation.

The API pages are produced by DocFX from the current public .NET surface. Use them for exact namespaces, types, members, signatures, and XML documentation. Product guides remain authoritative for runtime behavior, integration boundaries, security posture, compatibility, and host responsibilities.

## Core decision and governance APIs

- [GovernanceDecision](xref:AsiBackbone.Core.Decisions.GovernanceDecision)
- [GovernanceDecisionOutcome](xref:AsiBackbone.Core.Decisions.GovernanceDecisionOutcome)
- [IAsiBackbonePolicyEvaluator<TContext>](xref:AsiBackbone.Core.Evaluation.IAsiBackbonePolicyEvaluator`1)
- [IAsiBackboneConstraint<TContext>](xref:AsiBackbone.Core.Constraints.IAsiBackboneConstraint`1)
- [AsiBackboneConstraintEvaluationContext](xref:AsiBackbone.Core.Constraints.AsiBackboneConstraintEvaluationContext)

## Accountability and continuation APIs

- [LiabilityHandshakeRequest](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeRequest)
- [LiabilityHandshakeAcknowledgment](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeAcknowledgment)
- [AuditResidue](xref:AsiBackbone.Core.Audit.AuditResidue)
- [AuditLedgerRecord](xref:AsiBackbone.Core.Audit.AuditLedgerRecord)
- [CapabilityTokenGrant](xref:AsiBackbone.Core.CapabilityTokens.CapabilityTokenGrant)
- [OperationResult](xref:AsiBackbone.Core.Results.OperationResult)

## Read with the implementation guides

- [Core API Domain Model](articles/core-domain-language.md)
- [Policy Evaluator Pipeline](articles/policy-evaluator-pipeline.md)
- [Acknowledgment Workflow](articles/dynamic-liability-handshake.md)
- [Capability Grant Hardening](articles/capability-grant-hardening.md)
- [Host-Owned Execution Enforcement](articles/host-owned-execution-enforcement.md)

For architecture education and stack-neutral teaching, use [ASI Backbone Learning](https://asibackbone.github.io/Learning/).
