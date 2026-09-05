# Testing Harness

`AsiBackbone.Testing` is a test-only package for host applications that need to test AsiBackbone-governed endpoints without wiring production persistence, signing, audit, outbox, or capability-grant infrastructure in every test fixture.

> [!IMPORTANT]
> The testing package is not a production enforcement provider. It exists for automated tests, local developer validation, and sample hosts only. Production hosts should register real policy evaluators, capability validators, audit sinks, durable outbox stores, signing providers, and storage providers.

## When to use it

Use the testing harness when an endpoint has governance metadata such as:

```csharp
app.MapPost("/robotics/execute", () => Results.Ok())
    .MarkGovernancePolicy<RobotExecutionPolicy>()
    .RequireCapabilityGrant("robotics.execute")
    .EmitGovernanceAudit();
```

Without the harness, the ASP.NET Core package intentionally fails closed when an endpoint declares governance metadata but required host-owned governance services are missing. That behavior remains correct for production. The harness gives tests an explicit one-call registration path for deterministic behavior.

## Basic service registration

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.Testing;

builder.Services
    .AddAsiBackboneAspNetCore()
    .AddAsiBackboneTestHarness(harness =>
    {
        harness.AllowAllPolicies();
        harness.AllowCapabilityGrants();
    });
```

The harness registers deterministic test substitutes for:

- policy evaluation;
- endpoint capability-grant validation;
- in-memory audit residue inspection;
- non-durable in-memory governance outbox storage;
- deterministic no-signature signing.

## Deterministic policy results

Configure a specific policy marker result when the test needs to prove denial, acknowledgment, escalation, or other policy behavior.

```csharp
builder.Services.AddAsiBackboneTestHarness(harness =>
{
    harness.AllowAllPolicies();
    harness.SetPolicyResult<RobotExecutionPolicy>(
        GovernanceDecision.Deny("test.denied", "Denied by test harness."));
});
```

> **`SetPolicyResult` simulates host policy, not framework behavior.** AsiBackbone does not select constraints or decisions from an endpoint's policy type; every registered constraint runs on every governed endpoint. Configuring different results for two policy types asserts against a host-supplied `IAsiBackboneDecisionPolicy` that reads `endpoint.policy_types`, which the application must actually implement. Without it, both endpoints evaluate identically at runtime. See [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md).

For stricter tests, require every endpoint policy marker to be explicitly configured:

```csharp
builder.Services.AddAsiBackboneTestHarness(harness =>
{
    harness.RequirePolicyResult<RobotExecutionPolicy>(GovernanceDecision.Allow());
});
```

If an endpoint contains an unconfigured policy marker in explicit mode, the harness returns a denied decision with reason code `test_harness.policy_result.missing`.

## WebApplicationFactory-style test setup

When using a host fixture, override test services with the harness inside the fixture configuration:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;

WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(builder =>
    {
        builder.ConfigureServices(services =>
        {
            services.AddAsiBackboneTestHarness(harness =>
            {
                harness.AllowAllPolicies();
                harness.AllowCapabilityGrants();
            });
        });
    });
```

This preserves production package behavior while making tests explicit about their deterministic governance path.

## Inspecting audit residue

When an endpoint calls `.EmitGovernanceAudit()`, the harness captures audit residue in `AsiBackboneTestAuditSink`:

```csharp
AsiBackboneTestAuditSink auditSink = services.GetRequiredService<AsiBackboneTestAuditSink>();

Assert.Single(auditSink.Entries);
Assert.Equal("robotics.execute", auditSink.Entries[0].OperationName);
```

The sink is in-memory and process-local. It is designed for test assertions, not durable storage or tamper-evidence.

## Boundary notes

The testing package does not change `AsiBackbone.AspNetCore` production behavior. Endpoints without AsiBackbone governance metadata remain opt-in/pass-through by default unless the host enables strict metadata enforcement with `RequireGovernanceMetadata`. Endpoints that do declare governance metadata still fail closed when required host-owned services are missing. The harness only supplies explicit deterministic test registrations when a test chooses to use them, and it does not make `AsiBackbone.Core` depend on ASP.NET Core, EF Core, or testing packages.
