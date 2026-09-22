# Strict Governance Profile

`AddAsiBackboneStrictGovernance()` is the explicit fail-closed governance profile for hosts that want production-oriented defaults to be visible in startup configuration and code review.

The helper does not register authentication, authorization, host policy rules, audit persistence, endpoint middleware, or business execution. It configures the high-consequence AsiBackbone options that decide whether missing policy structure, policy exceptions, threat-contributor failures, and ungoverned endpoints fail open or fail closed.

## Why this exists

The `3.x` line now defaults the Core evaluator toward fail-closed behavior for empty policy structure, eligible ordinary constraint exceptions, threat-contributor exceptions, and threat-assessment downgrade protection. The strict profile still matters because it applies the same posture explicitly through DI and also configures ASP.NET Core endpoint-governance fail-closed options.

Use it when production hosts want a single, discoverable registration call for the complete fail-closed governance posture while keeping final execution authority with the host application.

## Registration

For ASP.NET Core hosts, register the strict profile near the rest of the AsiBackbone service setup:

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsiBackboneStrictGovernance();
builder.Services.AddAsiBackboneAspNetCore();
```

The same profile is also available through the explicit builder facade:

```csharp
builder.Services.AddAsiBackbone(backbone =>
{
    backbone.UseStrictGovernanceProfile();
    backbone.UseAspNetCoreEndpointGovernance();
});
```

Configuration order still matters. Later `Configure<TOptions>(...)` calls can intentionally override the strict profile. Calling the strict profile later re-applies the fail-closed settings.

## Options applied

| Option | Strict value | Reason |
| --- | --- | --- |
| `GovernancePolicyOptions.DenyWhenNoConstraints` | `true` | Empty policy structure becomes a denied governance decision instead of an allowed decision. |
| `GovernancePolicyOptions.TreatConstraintExceptionAsDenial` | `true` | Eligible policy constraint exceptions become safe denied decisions with stable reason codes. Cancellation and critical host/runtime failures still propagate. |
| `GovernancePolicyOptions.TreatThreatContributorExceptionAsDenial` | `true` | Threat-model contributor failures fail closed when contributors are registered. |
| `GovernancePolicyOptions.PreventThreatAssessmentAllowDowngrade` | `true` | Actionable threat assessment outcomes remain protected from being downgraded to pure allow decisions. |
| `EndpointGovernanceOptions.FailClosedWhenPolicyEvaluatorMissing` | `true` | Endpoints that require policy evaluation fail closed when no evaluator is configured. |
| `EndpointGovernanceOptions.FailClosedWhenCapabilityValidatorMissing` | `true` | Capability-gated endpoints fail closed when no capability validator is configured. |
| `EndpointGovernanceOptions.FailClosedWhenAuditSinkMissing` | `true` | Audit-emitting endpoints fail closed when no host-owned decision receipt sink is configured. |
| `EndpointGovernanceOptions.RequireGovernanceMetadata` | `true` | Selected endpoints without governance metadata are blocked unless explicitly marked as allowed to omit governance metadata. |

## Empty-policy behavior

With the `3.x` defaults or the strict profile applied, empty-policy evaluation returns a denied decision with the default `asibackbone.policy.no_constraints` reason code.

If a host intentionally needs an unconstrained local validation flow, it must opt out explicitly with `DenyWhenNoConstraints = false`. That opt-out should be limited to tests, samples, migration steps, or separately protected local flows.

```csharp
builder.Services.AddSingleton<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<GovernancePolicyOptions>>()
        .Value;

    return DefaultGovernancePolicyEvaluator.CreateBuilder<GovernanceEvaluationContext>()
        .AddConstraints(serviceProvider.GetServices<IGovernanceConstraint<GovernanceEvaluationContext>>())
        .WithOptions(options)
        .Build();
});
```

If a host constructs `DefaultGovernancePolicyEvaluator<TContext>` manually with `new GovernancePolicyOptions`, that manually supplied object owns the behavior. The strict registration helper configures DI options; it does not rewrite explicitly constructed option instances.

## Constraint-exception behavior

With the `3.x` defaults or the strict profile applied, eligible ordinary constraint failures are converted into denied governance decisions using the default `asibackbone.policy.constraint_exception` reason code and safe public reason message. Exception details remain in host logging/exception handling paths and are not copied into public decision reasons.

`OperationCanceledException` and critical host/runtime failures continue to propagate. The strict profile is a governance failure policy, not a corrupted-process recovery mechanism.

Hosts that intentionally need fail-fast propagation can set `TreatConstraintExceptionAsDenial = false`, but should document that exception-to-audit handling is owned by the host boundary.

## Threat-contributor behavior

Threat-model contributor failures are also configured to fail closed. When a registered contributor fails with an eligible ordinary exception, the evaluator produces a denied decision with a stable reason code rather than silently continuing to execution.

This is especially important when a contributor screens replay indicators, capability-token mismatch, region-policy mismatch, prompt-injection-like tool requests, or unsafe external commands.

## Endpoint metadata behavior

When `RequireGovernanceMetadata` is enabled, endpoint governance treats unmarked endpoints as configuration failures. Public or intentionally ungoverned endpoints should opt out explicitly with the endpoint metadata helper provided by the ASP.NET Core package.

Use this to distinguish intentional public surface area from accidentally ungoverned execution paths.

## 3.x adoption path

Hosts upgrading to or starting with `3.0.0` can use this path:

1. Add `AddAsiBackboneStrictGovernance()` in non-production or staging first.
2. Confirm every governed endpoint has policy, capability, and audit metadata where expected.
3. Mark intentionally public endpoints with the explicit missing-governance-metadata opt-out.
4. Make policy evaluator registrations consume `IOptions<GovernancePolicyOptions>` instead of creating unrelated option instances.
5. Monitor denied decisions for `asibackbone.policy.no_constraints`, `asibackbone.policy.constraint_exception`, and `asibackbone.threat.contributor_exception` reason codes.
6. Remove accidental empty-policy flows before enabling the profile in production.
7. Keep the helper in production when the host wants the fail-closed posture to be visible in code review and startup configuration.

This path keeps strict governance intentional while preserving the package boundary: AsiBackbone provides the evaluation posture, and the host owns execution enforcement, persistence, authentication, authorization, monitoring, and operational response.
