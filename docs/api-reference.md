---
description: Stable entry point for the generated AsiBackbone public API reference.
---

# AsiBackbone API Reference

This page is the stable entry point for the generated AsiBackbone API documentation.

The API pages are produced by DocFX from the current public .NET surface. Use them for exact namespaces, types, members, signatures, and XML documentation. Product guides remain authoritative for runtime behavior, integration boundaries, security posture, compatibility, and host responsibilities.

## Core decision and governance APIs

- [GovernanceDecision](xref:AsiBackbone.Core.Decisions.GovernanceDecision)
- [GovernanceDecisionOutcome](xref:AsiBackbone.Core.Decisions.GovernanceDecisionOutcome)
- [IGovernancePolicyEvaluator<TContext>](xref:AsiBackbone.Core.Evaluation.IGovernancePolicyEvaluator`1)
- [IGovernanceConstraint<TContext>](xref:AsiBackbone.Core.Constraints.IGovernanceConstraint`1)
- [GovernanceEvaluationContext](xref:AsiBackbone.Core.Constraints.GovernanceEvaluationContext)

## Accountability and continuation APIs

- [LiabilityHandshakeRequest](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeRequest)
- [LiabilityHandshakeAcknowledgment](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeAcknowledgment)
- [DecisionReceipt](xref:AsiBackbone.Core.Audit.DecisionReceipt)
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
