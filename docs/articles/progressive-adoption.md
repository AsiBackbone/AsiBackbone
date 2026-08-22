# Progressive Adoption Ladder

This guide shows the smallest useful way to adopt AsiBackbone first, then how to add stable `3.x` capabilities only when a host actually needs them.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine for consequential software decision flow. It is not an intelligence engine, AI model host, robot controller, compliance product, observability backend, or signing appliance.

> [!IMPORTANT]
> You do not need to adopt the entire package family on day one. Start with the smallest governance boundary that solves the immediate host problem, then add persistence, outbox, provider emission, analyzers, DLP/classification, or signing only when the scenario requires them.

## Recommended first path

Most new users should start with one of these paths:

| Goal | First page | Packages |
| --- | --- | --- |
| Understand the absolute Core-only decision shape | This page, [Level 1](#level-1-core-decision-pipeline-only) | `AsiBackbone.Core` |
| Gate one ASP.NET Core endpoint and inspect audit residue locally | [First 15 Minutes: Standard API Gating](quickstart-api-gating.md) | `Core`, `AspNetCore`, `Storage.InMemory` |
| Review the current stable package family before broader adoption | [3.2.2 Release Notes](release-notes-322.md) | Install only the packages required by the selected boundary |

Everything else is an add-on.

## Adoption ladder

| Level | Capability | Use when | Typical packages |
| --- | --- | --- | --- |
| 1 | Core decision pipeline only | You need to ask whether a proposed action can proceed. | `AsiBackbone.Core` |
| 2 | Acknowledgment / handshake and audit residue | You need a human/system responsibility checkpoint or local decision evidence. | `Core`; optionally `Storage.InMemory` for samples/tests |
| 3 | Durable audit and outbox persistence | Governance records must survive restarts, provider outages, or retries. | `Core`, `EntityFrameworkCore` or a host-owned store |
| 4 | Hosted drain worker and provider emission | You want local outbox entries delivered to a provider after local persistence. | `Core`, `AspNetCore`, durable store, one emitter |
| 5 | OpenTelemetry / Azure Monitor / Purview-style integration | You want dashboards, diagnostics, alerting, or governance enrichment downstream. | `OpenTelemetry` for released provider projection; host OpenTelemetry exporters; Purview remains strategy-only in the stable `3.x` package family |
| 6 | Signing-ready or managed-key host integration | You need signed or verified governance artifacts and have key-management responsibilities defined. | `Core`, `Signing.LocalDevelopment` for local proof paths, `Signing.ManagedKey` for host-owned managed-key clients |

Cross-cutting add-ons:

| Add-on | Use when | Notes |
| --- | --- | --- |
| Roslyn analyzers | You want build-time safety rails for governance persistence and continuation flows. | Optional; not runtime enforcement. |
| DLP/classification failure policy | You need risk-sensitive behavior when a host-owned scanner is unavailable, times out, blocks, or classifies content. | AsiBackbone supplies the policy response; the host owns the scanner. |
| Capability-token hardening | You need short-lived, scoped execution grants after a decision. | Validate at the execution boundary. |

## Level 1: Core decision pipeline only

Level 1 answers one question:

> Given safe facts about a proposed action, should the host continue?

Install only Core:

```bash
dotnet new console -n AsiBackboneCoreOnly
cd AsiBackboneCoreOnly
dotnet add package AsiBackbone.Core
```

Minimal Core-only example:

```csharp
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;

IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>[] constraints =
[
    new AllowedOperationConstraint()
];

var evaluator = new DefaultAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>(
    constraints,
    decisionPolicy: null,
    options: new AsiBackbonePolicyEvaluatorOptions
    {
        DenyWhenNoConstraints = true
    });

var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["operation"] = "orders.approve",
    ["risk"] = "routine-api-write"
};

var context = new AsiBackboneConstraintEvaluationContext(
    correlationId: Guid.NewGuid().ToString("n"),
    policyVersion: "core-only-policy-v1",
    policyHash: "core-only-policy-hash-v1",
    metadata: metadata);

GovernanceDecision decision = await evaluator.EvaluateAsync(context);

var actor = AsiBackboneActorContext.Human(
    actorId: "demo-user",
    displayName: "Demo User");

AuditResidue residue = AuditResidue.FromDecision(
    actor,
    operationName: "orders.approve",
    decision,
    metadata: context.Metadata);

Console.WriteLine($"Decision: {decision.Outcome}");
Console.WriteLine($"Can proceed: {decision.CanProceed}");
Console.WriteLine($"Audit event: {residue.EventId}");

internal sealed class AllowedOperationConstraint : IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>
{
    public string Name => "demo.operation.allowed";

    public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        AsiBackboneConstraintEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool blocked = context.Metadata.TryGetValue("operation", out string? operation)
            && string.Equals(operation, "orders.delete", StringComparison.OrdinalIgnoreCase);

        return ValueTask.FromResult(blocked
            ? ConstraintEvaluationResult.Deny(
                "demo.operation.blocked",
                "This operation is blocked by the demo policy.")
            : ConstraintEvaluationResult.Allow());
    }
}
```

What this proves:

- Core can evaluate a host-owned rule.
- Core can produce a `GovernanceDecision`.
- The host can create audit residue without a web host, EF Core, outbox, OpenTelemetry, signing, or analyzers.
- The host still owns execution. The sample only decides whether the proposed action can continue.

## Level 2: acknowledgment / handshake and audit residue

Add Level 2 when a decision needs responsibility acknowledgment or when the host needs local evidence of the decision.

Use this for:

- high-risk administrative actions;
- human-in-the-loop approval;
- warning decisions that require explicit responsibility acceptance;
- local audit records for allow, deny, defer, acknowledgment-required, or escalation decisions.

Start with:

- [Dynamic Liability Handshake](dynamic-liability-handshake.md)
- [Core Domain Language](core-domain-language.md)
- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)

`Storage.InMemory` is useful here only for samples, tests, and local validation. It is not durable production storage.

## Level 3: durable audit and outbox persistence

Add Level 3 when local accountability must survive process restarts, provider outages, retries, or delayed provider delivery.

Use this when:

- audit/outbox records must be durable;
- provider emission should not be the first or only record;
- operations need retry/dead-letter state;
- records must support later review.

Start with:

- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [EF Core Host Ownership and Migration Guidance](ef-core-host-ownership-and-migrations.md)

The host still owns the `DbContext`, provider, connection string, migrations, deployment, retention, backups, and access controls.

## Level 4: hosted drain worker and provider emission

Add Level 4 when durable outbox records should be delivered to a downstream provider.

Use this when:

- governance emissions should retry after transient provider failures;
- provider delivery status needs to be recorded;
- external systems should receive minimized governance envelopes after local persistence.

Start with:

- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)

## Level 5: OpenTelemetry / Azure Monitor / Purview-style integration

Add Level 5 when downstream observability or governance enrichment is needed.

Use this when:

- operations teams need traces, metrics, alerts, or dashboards;
- governance events should be projected through OpenTelemetry;
- Azure Monitor is configured through the host OpenTelemetry pipeline;
- Purview-style lineage or catalog enrichment is a future design consideration.

Start with:

- [Released: OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Strategy-Only: Purview Governance and Lineage Enrichment](purview-governance-lineage-enrichment-strategy.md)

In the stable `3.x` package family, OpenTelemetry is the concrete released governance emission provider. Azure Monitor is reached through host-configured OpenTelemetry exporters. Purview remains strategy-only unless a later release ships a concrete provider package.

## Level 6: signing-ready or managed-key host integration

Add Level 6 only when the host has an actual signing and verification requirement.

Use this when:

- governance artifacts need signing-ready metadata;
- a local-development signer is useful for samples/tests;
- a managed-key client exists and the host owns credentials, key operations, verification, monitoring, and failure policy.

Start with:

- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)

Signing does not mean production tamper-evidence. Production claims require concrete signing, verification, protected key management, durable storage controls, retention, monitoring, and operational procedures.

## When do I need each package?

| Package | Install when | Do not install just because |
| --- | --- | --- |
| `AsiBackbone.Core` | You need decisions, constraints, audit residue, capability abstractions, provider-neutral outbox/emission/signing seams, or DLP failure-policy primitives. | You only want to read conceptual docs. |
| `AsiBackbone.AspNetCore` | You are integrating with ASP.NET Core endpoint metadata, request correlation, result mapping, acknowledgment challenge helpers, endpoint governance, or hosted outbox drain. | You are writing a console, worker, or library-only proof path. |
| `AsiBackbone.Storage.InMemory` | You need non-durable sample/test/local validation storage. | You need production audit durability. |
| `AsiBackbone.EntityFrameworkCore` | You want host-owned EF Core persistence for audit/outbox/lifecycle records. | You are not ready to own migrations and database lifecycle. |
| `AsiBackbone.Analyzers` | You want build-time diagnostics for governance persistence and continuation patterns. | You expect runtime enforcement. |
| `AsiBackbone.OpenTelemetry` | You want to project governance envelopes into .NET diagnostics and a host-configured OpenTelemetry pipeline. | You expect Azure Monitor or another backend to be configured automatically. |
| `AsiBackbone.Signing.LocalDevelopment` | You need a local/test/sample signing proof path. | You need production key custody. |
| `AsiBackbone.Signing.ManagedKey` | You have a host-owned managed-key client and need an adapter boundary. | You expect built-in Azure Key Vault, Managed HSM, cloud KMS, or HSM implementation by default. |

## What to avoid on day one

Avoid installing every package before proving the basic gate. In particular, do not start with EF Core, hosted workers, OpenTelemetry, DLP scanner integration, or signing unless that capability is the immediate reason for adoption.

A good first success is simply:

```text
Proposed action
  -> host-owned constraint
  -> GovernanceDecision
  -> AuditResidue
  -> host checks decision.CanProceed
```

After that works, choose the next level deliberately.

## Related documentation

- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
- [Getting Started](getting-started.md)
- [Historical Upgrade Guide: 1.0.0 to 1.1.0](upgrade-100-to-110.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [Released: OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
