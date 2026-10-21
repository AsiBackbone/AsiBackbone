# AsiBackbone 7.5.0 Release Notes

Release date: 2026-10-21

`7.5.0` is a backward-compatible minor release for the stable `7.x` package
family. It restores end-to-end request trace correlation for policy-evaluated
ASP.NET Core endpoints and adds the Core surface needed for trace-aware custom
evaluation contexts and immutable decision copying.

Package IDs, public namespaces, the `net10.0` target, canonical wire values,
documented defaults, and binary assembly identity are unchanged.
`AssemblyVersion` remains `7.0.0.0`; package and file versions advance to
`7.5.0` and `7.5.0.0`.

## Trace-aware policy evaluation

- `GovernanceEvaluationContext.TraceId` carries the host trace identifier into
  Core policy evaluation.
- A new five-argument `GovernanceEvaluationContext` constructor accepts the
  trace identifier while the existing four-argument constructor remains
  available for binary compatibility.
- `IGovernanceEvaluationContext.TraceId` has a default `null` implementation,
  so existing host-defined evaluation contexts continue to compile unchanged.
- `DefaultGovernancePolicyEvaluator` stamps every decision it creates with the
  context trace identifier, including allow, warning, denial, deferral,
  acknowledgment-required, escalation, threat, and exception decisions.

## Endpoint decision and receipt correlation

- `DefaultEndpointGovernanceService` fills a missing trace identifier on a
  custom evaluator decision before capability validation and audit emission.
- The service repeats the missing-value backfill after capability validation,
  so a host capability validator cannot accidentally detach the final decision
  from the request trace.
- `GovernanceDecision.WithTraceId(...)` preserves the decision outcome,
  reasons, correlation identifier, policy version, and policy hash while
  normalizing only the replacement trace identifier.
- A trace identifier supplied by a policy or capability evaluator remains
  authoritative and is never overwritten.
- When no activity exists, endpoint decisions and receipts continue to use
  `HttpContext.TraceIdentifier` as the request trace fallback.

## Compatibility

- The new Core APIs are additive.
- Existing `IGovernanceEvaluationContext` implementations and calls to the
  existing `GovernanceEvaluationContext` constructor remain compatible.
- Decision outcomes, reasons, policy identifiers, receipt schemas, canonical
  wire values, and persistence schemas are unchanged.
- Package IDs, public namespaces, target framework, and assembly identity remain
  compatible across the `7.x` line.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.5.0` together.
2. If a custom evaluation context can supply a request trace, implement
   `TraceId`; otherwise the default `null` behavior remains valid.
3. If a custom endpoint policy or capability evaluator supplies its own trace
   identifier, confirm it is the intended authoritative value.
4. Query decision receipts by the request activity identifier and confirm the
   expected governed endpoint records are returned.
5. Restore, rebuild with warnings enabled, and run the consumer's tests.

Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

Stable publication occurs only from the protected `v7.5.0` tag after the
release-preparation pull request is merged to `main` and the release-candidate
checks pass.

See also:

- [7.5.0 Release Readiness Record](release-readiness-750.md)
- [7.5.0 Consumer Verification Guide](consumer-verification-750.md)
- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
