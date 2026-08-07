![AsiBackbone governance spine icon](https://raw.githubusercontent.com/AsiBackbone/ASIBackbone/main/docs/images/social-preview.png)

# AsiBackbone

[![CI](https://github.com/AsiBackbone/AsiBackbone/actions/workflows/ci.yml/badge.svg)](https://github.com/AsiBackbone/AsiBackbone/actions/workflows/ci.yml)
[![Line Coverage Gate](https://img.shields.io/badge/line%20coverage%20gate-75%25-brightgreen)](https://AsiBackbone.github.io/AsiBackbone/coverage/index.html)
[![Core Branch Coverage Gate](https://img.shields.io/badge/core%20branch%20gate-90%25-brightgreen)](https://AsiBackbone.github.io/AsiBackbone/coverage/core/index.html)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/13630/badge)](https://www.bestpractices.dev/projects/13630)
[![Documentation](https://github.com/AsiBackbone/AsiBackbone/actions/workflows/publish-docs.yml/badge.svg)](https://github.com/AsiBackbone/AsiBackbone/actions/workflows/publish-docs.yml)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://AsiBackbone.github.io/AsiBackbone/)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE.txt)
[![Security Policy](https://img.shields.io/badge/security-policy-blue)](SECURITY.md)
[![GitHub Release](https://img.shields.io/github/v/release/AsiBackbone/AsiBackbone?sort=semver&display_name=tag&label=release)](https://github.com/AsiBackbone/AsiBackbone/releases)
[![Zenodo DOI](https://img.shields.io/badge/DOI-10.5281%2Fzenodo.20546032-blue)](https://doi.org/10.5281/zenodo.20546032)

**Accountable Systems Infrastructure for governed .NET decision flow.**

> AI may provide the intellect. AsiBackbone provides the accountable spine.

---

## The practical problem

Most software can tell you *what* happened. Far less can show that an action was evaluated before it executed: which rules shaped the decision, which policy version applied, whether acknowledgment was required, how follow-on authority was scoped, and where the host took responsibility for execution.

AsiBackbone is a .NET package family for that decision boundary. It helps a host application build safe policy context, evaluate constraints, return a structured decision, preserve a decision receipt, optionally scope continuation, and then let the host decide whether and how to execute.

In this software project, **ASI** means **Accountable Systems Infrastructure**.

## Implementation-first mental model

A normal API adoption path looks like this:

```text
HTTP request
  -> host builds safe evaluation context
  -> host-owned rules evaluate the request
  -> AsiBackbone returns a GovernanceDecision
  -> host writes audit residue / decision receipt
  -> host continues only when decision.CanProceed is true
```

Use plain engineering translations first:

| Project term | Practical meaning |
| --- | --- |
| Governance spine | Policy decision pipeline around consequential operations. |
| Audit residue | Decision receipt or audit-log payload. |
| Acknowledgment handshake | Confirmation workflow before a risky operation. |
| Capability grant | Short-lived scoped permission. |
| Governance outbox | Durable outbox pattern for governance events. |
| OpenTelemetry projection | Optional traces/metrics projection after local records exist. |
| Host-owned execution boundary | The application code that performs or refuses the protected operation. |

## First code path

The snippet below is an intentionally small README slice, not a complete `Program.cs`. It shows the primary governance path: build safe context, evaluate policy, write audit residue, and let the host execute only after the decision allows it. The full compile-ready walkthrough lives in [First 15 Minutes: Standard API Gating](https://AsiBackbone.github.io/AsiBackbone/articles/quickstart-api-gating.html).

```csharp
// Registration: Core evaluator + one host-owned rule + local in-memory audit sink.
builder.Services.AddAsiBackboneAspNetCore();
builder.Services.AddSingleton<InMemoryAuditLedger>();
builder.Services.AddSingleton<IAsiBackboneAuditSink>(sp =>
    sp.GetRequiredService<InMemoryAuditLedger>());
builder.Services.AddSingleton<IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>, AllowedRegionConstraint>();
builder.Services.AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>(sp =>
    new DefaultAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>(
        sp.GetServices<IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>>(),
        decisionPolicy: null,
        options: new AsiBackbonePolicyEvaluatorOptions()));

app.MapPost("/api/orders/{region}/approve", async (
    string region,
    HttpContext httpContext,
    IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext> evaluator,
    IAsiBackboneAuditSink auditSink,
    CancellationToken cancellationToken) =>
{
    var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["operation"] = "orders.approve",
        ["region"] = region,
        ["risk"] = "routine-api-write"
    };

    var context = new AsiBackboneConstraintEvaluationContext(
        correlationId: httpContext.TraceIdentifier,
        policyVersion: "policy-v1",
        policyHash: "policy-hash-v1",
        metadata: metadata);

    GovernanceDecision decision = await evaluator.EvaluateAsync(context, cancellationToken);

    AuditResidue residue = AuditResidue.FromDecision(
        AsiBackboneActorContext.Human("example-user", "Example User"),
        operationName: "orders.approve",
        decision,
        metadata: context.Metadata);

    await auditSink.WriteAsync(residue, cancellationToken);
    if (!decision.CanProceed)
    {
        return Results.Json(new
        {
            allowed = false,
            decision = decision.Outcome.ToString(),
            decision.ReasonCodes,
            auditEventId = residue.EventId
        }, statusCode: StatusCodes.Status403Forbidden);
    }

    // Host-owned execution starts here. AsiBackbone does not approve the order itself.
    return Results.Ok(new
    {
        allowed = true,
        message = "Host order approval would run after this governance decision.",
        auditEventId = residue.EventId
    });
});
```

The `3.x` evaluator defaults are intentionally fail-closed for governed surfaces: empty policy structures deny, eligible ordinary constraint exceptions deny with `asibackbone.policy.constraint_exception`, and threat-contributor exceptions deny. Set `TreatConstraintExceptionAsDenial = false` only when a host intentionally wants fail-fast exception propagation through its own exception, transaction, retry, telemetry, or incident-response boundary.

For production-style hosts, add durable audit/outbox persistence, signing or verification, DLP/classification, provider emission, and operational monitoring only where the host has explicitly chosen and configured those boundaries.

## Package family

Stable `3.2.x` package family. `3.2.1` is the current minor release. The package family carries forward the governance-spine surface and adds explicit capability-grant validation profiles for execution-boundary enforcement and intentional metadata-only inspection, while preserving the governed execution-to-mutation accountability receipts, optional capability-proof trust pinning, and conservative ASP.NET Core actor-type claim trust controls introduced earlier in the `3.x` line.

The historical `3.0.0` release established the current major release line and binary assembly identity while preserving the simplified `AsiBackbone.*` package IDs and namespaces established by `2.0.0`.

| Package | Role |
| --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives: decisions, constraints, threat-model contributor hooks, acknowledgments, audit residue, lifecycle events, governed execution receipts, capability-token abstractions, explicit capability-grant validation profiles and proof trust pinning, durable outbox contracts, provider-neutral emission contracts, DLP/classification policy primitives, signing-ready metadata, canonical hashing/signing seams, verification-policy primitives, policy evaluator options, metadata budget helpers, and builder-style audit residue construction. |
| `AsiBackbone.DependencyInjection` | Explicit `AddAsiBackbone(...)` builder facade for coordinating host-selected provider registrations without making Core own infrastructure. |
| `AsiBackbone.Storage.InMemory` | Non-durable in-memory storage helpers for tests, samples, local validation, lifecycle events, and outbox proof paths. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and host-owned persistence for audit ledger, acknowledgments, lifecycle events, JSON metadata storage, and governance outbox records. |
| `AsiBackbone.AspNetCore` | ASP.NET Core host adapters for actor context, conservative actor-type claim mapping, request correlation, audit enrichment, HTTP result mapping, acknowledgment challenge flows, endpoint governance, endpoint metadata mode, strict-governance profile helpers, endpoint fast-abort metadata, and hosted outbox drain integration. |
| `AsiBackbone.Testing` | Test-only harness helpers for deterministic endpoint governance, policy results, capability validation, in-memory audit inspection, non-durable outbox storage, and no-signature signing seams. |
| `AsiBackbone.Templates` | `dotnet new` templates for generating governed ASP.NET Core host scaffolds with endpoint governance, sample policies, local in-memory audit inspection, analyzers, and README guidance. |
| `AsiBackbone.Analyzers` | Roslyn analyzer safety rails for governance persistence and continuation flows. |
| `AsiBackbone.OpenTelemetry` | Released OpenTelemetry governance emission provider that projects provider-neutral envelopes into .NET diagnostics. |
| `AsiBackbone.Signing.LocalDevelopment` | Local-development signing and verification for tests, samples, and wiring proof paths only. Not for production key custody. |
| `AsiBackbone.Signing.ManagedKey` | Provider-neutral managed-key signing adapter boundary. The host supplies the actual managed-key client, credentials, key operations, verification path, monitoring, and operational policy. Production-oriented registration fails closed by default when signing cannot complete. |

Runtime governance-residue signing remains provider-neutral through `AsiBackbone.Signing.ManagedKey`. AsiBackbone does not ship first-party Azure Key Vault, AWS KMS, GCP Cloud KMS, HSM, certificate-store, enterprise KMS, or production-style signing sample-host implementations. Future Event Hubs, Purview, gateway, robotics, immutable-storage, or other non-signing provider packages are not part of the stable contract unless separately reviewed and released.

## Supported target framework

Stable `3.2.x` packages intentionally target `net10.0`. Consumers should plan on a .NET 10 SDK/runtime or later for the current package line.

The project is not multi-targeting .NET 8 for `3.2.x`. That is an explicit adoption decision, not a defect workaround. The current package family uses a single repository-wide `TargetFramework` of `net10.0`, the EF Core integration is aligned with centrally managed EF Core `10.0.x` dependencies, and backporting the full package, analyzer, template, CI, packaging, and smoke-test surface would add compatibility overhead for a short-lived adoption window.

If meaningful external consumer demand appears, additional TFM support can be reconsidered in a later release with CI, packaging validation, analyzer compatibility, template smoke tests, and documentation updated together. See the [Target Framework Support Decision Record](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/target-framework-support.md).

## Start here

For **implementation-first adoption**:

- [Implementation-First Adoption Path](https://AsiBackbone.github.io/AsiBackbone/articles/implementation-first-adoption.html) — plain engineering translations and the recommended first reading path.
- [First 15 Minutes: Standard API Gating](https://AsiBackbone.github.io/AsiBackbone/articles/quickstart-api-gating.html)
- [AddAsiBackbone Builder Facade](https://AsiBackbone.github.io/AsiBackbone/articles/add-asibackbone-builder-facade.html)
- [dotnet new Templates](https://AsiBackbone.github.io/AsiBackbone/articles/templates.html)
- [Target Framework Support Decision Record](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/target-framework-support.md)
- [Reference Deployment: Plain ASP.NET Core Host Evidence](https://AsiBackbone.github.io/AsiBackbone/articles/reference-deployment.html)
- [Testing Harness](https://AsiBackbone.github.io/AsiBackbone/articles/testing-harness.html)
- [Strict Governance Profile](https://AsiBackbone.github.io/AsiBackbone/articles/strict-governance-profile.html)
- [Threat Model Contributors](https://AsiBackbone.github.io/AsiBackbone/articles/threat-model-contributors.html)
- [Project Boundaries and Non-Claims](https://AsiBackbone.github.io/AsiBackbone/articles/project-boundaries.html)
- [Terminology Map](https://AsiBackbone.github.io/AsiBackbone/articles/terminology-map.html)
- [Progressive Adoption Ladder](https://AsiBackbone.github.io/AsiBackbone/articles/progressive-adoption.html)
- [Production Managed-Key Integration Guide](https://AsiBackbone.github.io/AsiBackbone/articles/production-managed-key-integration.html)

For **optional conceptual background**:

- [Intent to Execution: An Accountability Pattern](https://AsiBackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html)
- [Core Governance Flow Diagrams](https://AsiBackbone.github.io/AsiBackbone/articles/core-governance-flow-diagrams.html)
- [ASI Backbone Concept Synopsis](https://AsiBackbone.github.io/AsiBackbone/articles/asi-backbone-concept.html)
- [Dynamic Liability Handshake](https://AsiBackbone.github.io/AsiBackbone/articles/dynamic-liability-handshake.html)
- [Core Domain Language](https://AsiBackbone.github.io/AsiBackbone/articles/core-domain-language.html)
- [Host-Owned Execution Enforcement](https://AsiBackbone.github.io/AsiBackbone/articles/host-owned-execution-enforcement.html)

The full, categorized documentation set lives at the [documentation site](https://AsiBackbone.github.io/AsiBackbone/).

## Current status

Stable `3.2.x` is the current released line, with `3.2.1` as the current minor release candidate. This release preserves the simplified `AsiBackbone.*` package and namespace identity and the binary assembly identity `3.0.0.0`.

The stable API contract is documented in [API Compatibility and SemVer](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/api-compatibility-and-semver.md). The current release is recorded in [3.2.0 Release Notes](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/release-notes-320.md) and [3.2.0 Release Readiness Record](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/release-readiness-320.md). Consumers can use the [3.2.0 Consumer Verification Guide](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/consumer-verification-320.md) for package-source, package ID, capability-validation profile, Source Link, SBOM, provenance, and deferred-signing checks. Earlier release records remain available for historical traceability.

## Security and vulnerability reporting

Please report sensitive concerns through the repository [Security Policy](SECURITY.md).

## Relationship to NetCoreApplicationTemplate

NetCoreApplicationTemplate may be used as a preferred host baseline during development and validation, but AsiBackbone does not require it.

```text
NetCoreApplicationTemplate = preferred host baseline
AsiBackbone               = optional governance/module package family
Consumer application      = chooses whether to use either or both
```

A consumer should be able to use AsiBackbone in an application generated from NetCoreApplicationTemplate, in an existing ASP.NET Core application, or in a custom host that provides the required infrastructure. See [NetCoreApplicationTemplate Host Validation](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/netcoreapplicationtemplate-host-validation.md).

## Alignment boundary

AsiBackbone is a governance spine, not an intelligence engine. It implements governance-oriented software primitives for accountable decision flow and keeps execution authority with the host application. See [Project Boundaries and Non-Claims](https://AsiBackbone.github.io/AsiBackbone/articles/project-boundaries.html) for the full scope statement and safe wording guidance.

> **Current NuGet packages are intentionally published without package signing.** This is a deliberate governance decision while the project is independently maintained, balancing operational complexity against practical value. Instead, the project emphasizes transparent source code, GitHub releases, Source Link, SBOM generation, and package provenance. Package signing remains on the long-term roadmap and will be reconsidered as the project's community, governance, and operational needs evolve. For current package verification guidance, see the [**3.2.0 Consumer Verification Guide**](https://AsiBackbone.github.io/AsiBackbone/articles/consumer-verification-320.html).

## Design principles

- Keep Core small.
- Keep Core dependency-light.
- Avoid hidden host assumptions.
- Prefer explicit integration over magic.
- Let the host own infrastructure.
