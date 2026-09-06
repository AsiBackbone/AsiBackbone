# Upgrade Guide: 3.2.3 to 4.0.0

This guide covers the consumer-visible changes when upgrading the stable
AsiBackbone package family from `3.2.3` to `4.0.0`.

## Update package references

Update every consumed `AsiBackbone.*` package together:

```xml
<PackageReference Include="AsiBackbone.Core" Version="4.0.0" />
<PackageReference Include="AsiBackbone.AspNetCore" Version="4.0.0" />
<PackageReference Include="AsiBackbone.EntityFrameworkCore" Version="4.0.0" />
```

Package IDs and namespaces have not changed. Rebuild the host because the
assembly identity advances from `3.0.0.0` to `4.0.0.0`.

## Review governance outbox claim leasing

Claim leasing is enabled by default in `4.0.0`. The in-memory and EF Core
stores shipped by AsiBackbone support the required
`IAsiBackboneGovernanceOutboxClaimStore` contract.

If the host supplies a custom outbox store, implement the claim-store contract
before upgrading. A custom store that only implements
`IAsiBackboneGovernanceOutboxStore` causes the drain to fail instead of silently
falling back to duplicate-prone multi-host delivery.

As a temporary compatibility path, a host may opt out explicitly:

```csharp
services.Configure<AsiBackboneGovernanceOutboxOptions>(options =>
{
    options.UseClaimLeases = false;
});
```

Opting out permits concurrent hosts to emit the same envelope more than once.
Use it only when that delivery behavior is understood and accepted.

## Review claim recovery and dead-letter policy

The default claim page size is `10`, the maximum claim-attempt threshold is
`5`, and entries beyond the threshold are dead-lettered before another
emission attempt. Review these options against the host's worker count, lease
duration, emitter latency, incident response, and dead-letter operations:

```csharp
services.Configure<AsiBackboneGovernanceOutboxOptions>(options =>
{
    options.ClaimWorkerId = "orders:worker-1";
    options.ClaimPageSize = 10;
    options.MaxClaimAttempts = 5;
    options.DeadLetterOnMaxClaimAttempts = true;
});
```

Hosts running several drain workers inside one process should assign each
worker a distinct `ClaimWorkerId`. A stable identifier across restarts should
be supplied when the host's operational model requires it.

## Rename endpoint policy markers

`RequireGovernancePolicy` is obsolete because the framework records a policy
type marker but does not select an evaluator or constraint set from it. Rename
calls to `MarkGovernancePolicy`:

```csharp
app.MapPost("/high-risk-action", handler)
    .MarkGovernancePolicy<MyStrictPolicy>();
```

The replacement records the same endpoint metadata. A host-supplied decision
policy must still read `endpoint.policy_types` when behavior varies by marker.

## Review correlation and endpoint ordering

Inbound correlation-ID headers are no longer trusted unless
`TrustInboundCorrelationIdHeaders` is enabled. Keep the default for untrusted
clients; opt in only when a trusted upstream controls and validates the header.

Endpoint governance now reports an unresolved-endpoint stage when middleware
runs before routing has selected an endpoint. Verify the host orders routing
and endpoint-governance middleware as documented.

## Validate the upgrade

- rebuild every application and plugin that references AsiBackbone assemblies;
- exercise multi-worker outbox claim, reclaim, retry, and dead-letter paths;
- verify custom DbContext registrations resolve the intended audit and outbox
  stores;
- update obsolete endpoint policy-marker calls;
- verify trusted correlation-header configuration;
- rerun authorization, policy, persistence, signing, and operational tests.

## Related documentation

- [4.0.0 Release Notes](release-notes-400.md)
- [Governance Outbox Delivery Semantics](governance-outbox-delivery-semantics.md)
- [Outbox Multi-Worker Concurrency](outbox-multi-worker-concurrency.md)
- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
