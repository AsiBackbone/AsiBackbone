# AsiBackbone.Testing

`AsiBackbone.Testing` provides test harness helpers for exercising AsiBackbone-governed endpoints and services without forcing host applications to wire production persistence, signing, outbox, audit, or capability-grant infrastructure in every automated test.

> [!IMPORTANT]
> This package is for tests only. It is not a production enforcement provider and should not be used to weaken host-owned governance, persistence, signing, audit, or capability validation in production applications.

## What it registers

`AddAsiBackboneTestHarness(...)` registers deterministic test-only substitutions for common host-owned seams:

- `IGovernancePolicyEvaluator<GovernanceEvaluationContext>`
- `IEndpointCapabilityGrantValidator`
- `IDecisionReceiptSink` backed by `GovernanceTestDecisionReceiptSink`
- `IGovernanceOutboxStore` backed by the non-durable in-memory outbox store
- `IGovernanceSigningService` backed by a deterministic no-signature service

The package does not change production package defaults. ASP.NET Core endpoint governance remains fail-closed when host-owned services are missing unless tests explicitly register this harness.

## Basic endpoint test setup

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

## Deterministic policy results

```csharp
builder.Services.AddAsiBackboneTestHarness(harness =>
{
    harness.AllowAllPolicies();
    harness.SetPolicyResult<MyStrictPolicy>(
        GovernanceDecision.Deny("test.denied", "Denied by test harness."));
});
```

For stricter tests, require every policy marker to have an explicit result:

```csharp
builder.Services.AddAsiBackboneTestHarness(harness =>
{
    harness.RequirePolicyResult<MyStrictPolicy>(GovernanceDecision.Allow());
});
```

When `RequirePolicyResult<TPolicy>(...)` is used and the selected endpoint has an unconfigured policy marker, the harness returns a deterministic denied decision with reason code `test_harness.policy_result.missing`.

## Inspecting decision receipt

```csharp
GovernanceTestDecisionReceiptSink auditSink = services.GetRequiredService<GovernanceTestDecisionReceiptSink>();

Assert.Single(auditSink.Entries);
```

The audit sink is in-memory and process-local. It is intended for assertion-friendly automated tests, not durable records or tamper-evidence.

## Reusable contract fixtures

`AsiBackbone.Testing.Contracts` contains framework-neutral contract fixtures and assertions for extension authors. The helpers throw `GovernanceContractViolationException` instead of depending on xUnit, NUnit, or MSTest directly, so test projects can reuse the same safe-collapse invariants from any test runner.

Available fixtures include:

- `GovernancePolicyEvaluatorContract<TContext>`
- `GovernanceDecisionPolicyContract<TContext>`
- `GovernanceConstraintContract<TContext>`
- `EndpointCapabilityGrantValidatorContract`
- `DecisionReceiptSinkContract`
- `GovernanceDecisionContract` assertion helpers

Example xUnit usage for a custom policy evaluator:

```csharp
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Testing.Contracts;
using Xunit;

public sealed class MyPolicyEvaluatorContractTests
{
    [Fact]
    public async Task Evaluator_returns_safe_decision()
    {
        var contract = new MyPolicyEvaluatorContract();

        await contract.VerifyEvaluatorReturnsSafeDecisionAsync(TestContext.Current.CancellationToken);
    }

    private sealed class MyPolicyEvaluatorContract
        : GovernancePolicyEvaluatorContract<GovernanceEvaluationContext>
    {
        protected override IGovernancePolicyEvaluator<GovernanceEvaluationContext> CreateEvaluator()
        {
            return new MyPolicyEvaluator();
        }

        protected override GovernanceEvaluationContext CreateEvaluationContext()
        {
            return new GovernanceEvaluationContext(
                correlationId: "contract-correlation",
                policyVersion: "policy-v1",
                policyHash: "policy-hash");
        }
    }
}
```

The default contract assertions verify portable invariants such as non-null decisions, reason codes for denied/deferred/acknowledgment/escalation paths, correlation propagation, policy telemetry presence when supplied or resolved by the implementation, invalid capability-grant scenarios not returning `Allow`, and valid decision receipt shape.

## WebApplicationFactory-style usage

```csharp
factory.WithWebHostBuilder(builder =>
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

Use this pattern when a host application already calls `AddAsiBackboneAspNetCore()` and exposes endpoints with metadata such as `.MarkGovernancePolicy<TPolicy>()`, `.RequireCapabilityGrant(...)`, or `.EmitGovernanceAudit()`.

## Production boundary

This package is intentionally scoped to automated tests, samples, and local developer validation. Production applications should register real policy evaluators, capability validators, audit sinks, durable outbox stores, signing providers, and storage providers appropriate to their risk profile.
