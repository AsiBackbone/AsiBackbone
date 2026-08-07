# Endpoint Governance Development Diagnostics

Endpoint governance development diagnostics make local failures easier to understand without turning on a telemetry backend or digging through logs.

> [!IMPORTANT]
> Development diagnostics are opt-in and development-gated. They are intended for local debugging only. Do not enable rich diagnostics in production, public preview, shared staging, or environments where response bodies may be visible to untrusted callers.

## Enable diagnostics locally

Configure endpoint governance options from the ASP.NET Core host:

```csharp
builder.Services.Configure<AsiBackboneEndpointGovernanceOptions>(options =>
{
    options.EnableDevelopmentDiagnostics = builder.Environment.IsDevelopment();
    options.DevelopmentDiagnosticsDocumentationBaseUrl = "https://asibackbone.github.io/AsiBackbone/articles/";
});
```

Diagnostics are emitted only when both conditions are true:

1. `EnableDevelopmentDiagnostics` is `true`.
2. The request service provider exposes an `IWebHostEnvironment` whose environment name is `Development`.

If either condition is false, endpoint governance keeps its conservative defaults, including the bodyless generic `403 Forbidden` path for ordinary blocked decisions.

## What gets included

When enabled, local ProblemDetails responses may include safe diagnostic fields such as:

| Field | Meaning |
| --- | --- |
| `outcome` | The `GovernanceDecision` outcome, when available. |
| `reasonCodes` | Machine-readable reason codes. |
| `reasonMessages` | Human-readable reason messages for local debugging. |
| `endpointOperationName` | The resolved endpoint operation/display name. |
| `endpointPolicyTypes` | Policy marker type names attached to the endpoint. |
| `capabilityScopes` | Capability scopes requested by endpoint metadata. |
| `decisionStage` | The stage that produced or surfaced the failure. |
| `correlationId` / `traceId` | Decision identifiers when available. |
| `policyVersion` / `policyHash` | Policy metadata when available. |
| `metadataMode` | The configured endpoint metadata mode used for the evaluated metadata dictionary. |
| `metadataKeys` | Evaluated metadata keys. |
| `metadata` | Evaluated metadata values after redaction. |
| `documentationUrl` | Link back to this troubleshooting page when configured. |

## Example response

A missing capability validator in local development may return a response shaped like this:

```json
{
  "title": "Endpoint capability grant validation failed.",
  "status": 403,
  "detail": "Endpoint capability metadata was present, but no host-owned endpoint capability validator was registered.",
  "outcome": "Denied",
  "reasonCodes": [
    "endpoint.capability_validator.missing"
  ],
  "endpointOperationName": "sample.execute",
  "capabilityScopes": [
    "sample.execute"
  ],
  "decisionStage": "aspnetcore.endpoint.governance.capability.configuration",
  "metadataMode": "Full",
  "metadataKeys": [
    "endpoint.capability_scopes",
    "endpoint.emit_governance_audit",
    "endpoint.operation_name",
    "endpoint.requires_liability_handshake"
  ],
  "documentationUrl": "https://asibackbone.github.io/AsiBackbone/articles/endpoint-governance-development-diagnostics.html"
}
```

Exact fields depend on the failing stage, configured metadata mode, and the decision produced by the host-owned evaluator or validator. When `MetadataMode` is `Reduced`, `metadataKeys` and `metadata` include only the reduced metadata dictionary, while descriptor-derived fields such as `endpointPolicyTypes` and `capabilityScopes` may still appear in local diagnostics.

## Common failures and fixes

### `endpoint.policy_evaluator.missing`

The endpoint has `[RequireGovernancePolicy]` or `.RequireGovernancePolicy<TPolicy>()`, but no `IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>` is registered.

Register a host-owned evaluator:

```csharp
builder.Services.AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>, MyPolicyEvaluator>();
```

### `endpoint.capability_validator.missing`

The endpoint has `[RequireCapabilityGrant]` or `.RequireCapabilityGrant(...)`, but no host-owned endpoint capability validator is registered.

Register a validator that checks scope, expiry, replay, actor binding, and downstream authorization for your host:

```csharp
builder.Services.AddSingleton<IAsiBackboneEndpointCapabilityGrantValidator, MyCapabilityGrantValidator>();
```

### `endpoint.audit_sink.missing`

The endpoint requested `.EmitGovernanceAudit()` or `[EmitGovernanceAudit]`, but no `IAsiBackboneAuditSink` is registered.

Register a host-owned audit sink. For local-only validation, `AsiBackbone.Storage.InMemory` can provide non-durable inspection. Production hosts should use durable host-owned persistence when records must survive restart.

### Bodyless `403 Forbidden`

A bodyless `403` is expected when development diagnostics are not enabled or when the environment is not `Development`. It is also the conservative default for ordinary policy denials so production hosts do not expose policy internals by accident.

Enable development diagnostics locally when the failure needs a richer explanation.

## Redaction rules

Development diagnostics never read raw request bodies, cookies, or headers through endpoint governance diagnostics. They only report governance metadata already present in the endpoint descriptor or decision object.

Metadata values are redacted when:

- `IncludeDevelopmentDiagnosticsMetadataValues` is `false`;
- the metadata key appears in `DevelopmentDiagnosticsRedactedMetadataKeys`;
- the metadata key name contains common sensitive-key terms such as credential-like, cookie-like, token-like, authorization-like, or secret-like identifiers.

The `metadataKeys` list remains useful even when values are redacted because it shows which facts were available to the endpoint governance flow.

## Local-only posture

Development diagnostics do not replace:

- structured logging,
- OpenTelemetry provider emission,
- durable audit/outbox persistence,
- host-owned security controls,
- production incident response,
- legal or compliance review.

They are a developer-experience aid for understanding local endpoint governance failures quickly and safely.

## Related documentation

- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
- [Testing Harness](testing-harness.md)
- [Safe Audit and Telemetry Data](safe-audit-telemetry-data.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
