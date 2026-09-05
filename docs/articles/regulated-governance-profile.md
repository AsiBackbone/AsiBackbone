# Regulated Governance Profile

`AddAsiBackboneRegulatedGovernance()` is the convenience registration API for hosts that want a repeatable, conservative ASP.NET Core governance posture.

> [!IMPORTANT]
> This is a governance posture profile, not a legal, regulatory, security, privacy, or compliance certification. A host must still validate its complete deployed system against the laws, standards, contracts, threat model, and operational controls that apply to it.

The profile preserves AsiBackbone's progressive-adoption model. Existing hosts can continue to use `AddAsiBackboneAspNetCore()` or `AddAsiBackboneStrictGovernance()` independently. The regulated helper is an explicit opt-in composition for hosts that prefer the strict settings and metadata sanitation seam to be visible in one startup call.

## Convenience registration

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsiBackboneRegulatedGovernance();
```

The same profile is available through the builder facade:

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.DependencyInjection;

builder.Services.AddAsiBackbone(backbone =>
    backbone.UseRegulatedGovernanceProfile());
```

The helper performs three framework registrations:

1. `AddAsiBackboneAspNetCore()` for the ASP.NET Core host adapter;
2. `AddAsiBackboneStrictGovernance()` for fail-closed evaluator and endpoint options;
3. a scoped `IGovernanceMetadataSanitizer` backed by `DefaultGovernanceMetadataSanitizer`, all registered `IGovernanceMetadataClassifier` instances, and `GovernanceMetadataBudget.Recommended`.

It does not add endpoint middleware automatically. The host must still place the governance middleware in the request pipeline:

```csharp
WebApplication app = builder.Build();

app.UseAsiBackboneEndpointGovernance();
```

## Exact option posture

| Option | Regulated value | Rationale |
| --- | --- | --- |
| `AsiBackbonePolicyEvaluatorOptions.DenyWhenNoConstraints` | `true` | Empty policy structure denies instead of silently allowing. |
| `AsiBackbonePolicyEvaluatorOptions.TreatConstraintExceptionAsDenial` | `true` | Eligible ordinary constraint failures become safe denied decisions. Cancellation and critical runtime failures still propagate. |
| `AsiBackbonePolicyEvaluatorOptions.TreatThreatContributorExceptionAsDenial` | `true` | Threat-contributor failures do not silently remove a screening layer. |
| `AsiBackbonePolicyEvaluatorOptions.PreventThreatAssessmentAllowDowngrade` | `true` | Actionable threat outcomes cannot be reduced to a pure allow decision. |
| `AsiBackboneEndpointGovernanceOptions.FailClosedWhenPolicyEvaluatorMissing` | `true` | Endpoints requesting policy evaluation fail closed when the evaluator is absent. |
| `AsiBackboneEndpointGovernanceOptions.FailClosedWhenCapabilityValidatorMissing` | `true` | Capability-gated endpoints fail closed when the host validator is absent. |
| `AsiBackboneEndpointGovernanceOptions.FailClosedWhenAuditSinkMissing` | `true` | Endpoints requesting governance audit fail closed when the host audit sink is absent. |
| `AsiBackboneEndpointGovernanceOptions.RequireGovernanceMetadata` | `true` | Endpoints must be governed or explicitly marked as intentionally exempt. |
| `AsiBackboneEndpointGovernanceOptions.IncludeDevelopmentDiagnosticsMetadataValues` | `false` | Development diagnostics may retain bounded keys, but metadata values remain redacted. |

The registered metadata sanitizer normalizes metadata into a new collection, applies host classifiers in registration order, performs redaction, dropping, warning, or denial, and then applies the recommended metadata budget. A denied sanitation result blocks endpoint governance before policy evaluation, audit emission, acknowledgment challenge construction, or endpoint execution.

## Host-owned metadata classification

The profile enables the sanitation pipeline, but it cannot infer which values are sensitive in a particular deployment. Regulated hosts should register one or more reviewed classifiers:

```csharp
builder.Services.AddSingleton<IGovernanceMetadataClassifier, HostAllowListMetadataClassifier>();
builder.Services.AddSingleton<IGovernanceMetadataClassifier, HostSensitiveValueClassifier>();
builder.Services.AddAsiBackboneRegulatedGovernance();
```

With no classifiers, the sanitizer still applies normalization, reserved-key checks, and `GovernanceMetadataBudget.Recommended`. That is useful shape enforcement, but it is not DLP, privacy classification, secret detection, encryption, or proof that metadata is safe.

Classifier failures are not converted into permissive results by the default sanitizer. Hosts should keep that fail-closed behavior at every durable or external boundary and must ensure background jobs, audit builders, outbox producers, and telemetry paths also use sanitized metadata rather than the original caller-owned dictionary.

## Capability proof and replay controls

The profile makes a missing endpoint capability validator fail closed. It does not invent a token format or automatically configure proof verification and replay state because issuer, audience, key custody, storage, and transaction semantics are host-specific.

A regulated capability validator should use strict validation settings where applicable:

```csharp
CapabilityGrantValidationOptions validationOptions =
    CapabilityGrantValidationOptions.Create(
        issuer: "governance-authority",
        audience: "regulated-api",
        scopes: ["payments.approve"],
        requireProof: true,
        requireAcknowledgmentReference: true,
        requireUseCheck: true,
        maxUseCount: 1);

CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
    signedGrant,
    validationOptions,
    verificationService,
    durableUseStore,
    cancellationToken);
```

Production replay protection should be durable, concurrency-safe, and atomic for the host's execution model. The in-memory use store is suitable only for tests, samples, and local validation.

## Audit, persistence, and signing

The profile requires an audit sink only when an endpoint requests governance audit. The host still owns:

- durable audit and outbox persistence before external emission;
- transaction boundaries and failure recovery;
- retention, access control, residency, legal hold, and deletion policy;
- verification of signed artifacts before relying on them;
- managed-key or HSM-backed production signing when signed governance artifacts are required;
- key rotation, revocation, monitoring, and incident response.

Do not use `AsiBackbone.Signing.LocalDevelopment` as production key custody. Use the provider-neutral managed-key boundary with a reviewed host implementation, or another production signing path appropriate to the deployment.

## Endpoint posture

Because governance metadata is globally required, intentionally public endpoints must opt out explicitly:

```csharp
app.MapGet("/health", () => Results.Ok())
    .AllowMissingGovernanceMetadata();
```

Governed endpoints should state their required policy, capability, acknowledgment, and audit behavior explicitly:

```csharp
app.MapPost("/payments/{id}/approve", ApprovePayment)
    .MarkGovernancePolicy<PaymentApprovalPolicy>()
    .RequireCapabilityGrant("payments.approve")
    .RequireLiabilityHandshake()
    .EmitGovernanceAudit();
```

A missing policy evaluator, capability validator, or audit sink blocks the request when the corresponding endpoint metadata is present.

## Progressive-adoption alternative

Hosts that are not ready for the full profile can continue with the narrower registration:

```csharp
builder.Services.AddAsiBackboneAspNetCore();
```

They may then add strict options, sanitation, capability validation, durable storage, and signing incrementally. Choosing the regulated profile does not change the defaults for consumers who do not call it.

## Deployment checklist

- [ ] Register `AddAsiBackboneRegulatedGovernance()` or `UseRegulatedGovernanceProfile()`.
- [ ] Add `UseAsiBackboneEndpointGovernance()` before governed endpoints execute.
- [ ] Mark every endpoint as governed or explicitly exempt.
- [ ] Register real policy constraints, threat contributors, and a DI-configured evaluator.
- [ ] Register a capability validator that verifies proof, scope, expiry, binding, and replay/use state where applicable.
- [ ] Register reviewed metadata classifiers and use sanitation before every durable or external boundary.
- [ ] Register a durable audit sink and outbox strategy for production evidence.
- [ ] Use managed-key/provider-neutral production signing rather than local-development signing.
- [ ] Keep expanded diagnostics restricted to Development and keep metadata values redacted.
- [ ] Validate the complete deployed host against its legal, regulatory, privacy, security, and operational obligations.

## Related documentation

- [Strict Governance Profile](strict-governance-profile.md)
- [Governance Metadata Sanitization and Classification](governance-metadata-sanitization.md)
- [Capability Grant Hardening](capability-grant-hardening.md)
- [Regulated Development Diagnostics](regulated-development-diagnostics.md)
- [Regulated Storage and Signing Verification Checklist](regulated-storage-and-signing-verification-checklist.md)
- [Production Managed-Key Integration Guide](production-managed-key-integration.md)
- [Progressive Adoption Ladder](progressive-adoption.md)
