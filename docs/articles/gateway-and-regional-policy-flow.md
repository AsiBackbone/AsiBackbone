# Gateway and Regional Policy Flow: Product Integration Note

The general regional-policy and operational-gateway architecture is canonical in [ASI Backbone Learning](https://asibackbone.github.io/Learning/advanced/regional-policy-and-operational-gateways.html).

This page documents how current AsiBackbone primitives fit that pattern.

> [!IMPORTANT]
> AsiBackbone is not a robot controller, cloud control plane, infrastructure orchestrator, or external execution engine. The host or gateway owns the real side effect.

## Product integration flow

~~~text
Upstream intent
  -> Host builds regional/local/tenant policy context
  -> AsiBackbone evaluates constraints
  -> GovernanceDecision
  -> Acknowledgment when required
  -> Optional scoped capability grant
  -> Host/gateway validates current execution conditions
  -> Host-owned external execution or safe rejection
  -> Audit/outbox/provider evidence
~~~

## Product surfaces involved

Depending on the integration, a host may use:

- policy/evaluation context and constraints;
- explicit governance outcomes and reason codes;
- acknowledgment workflow contracts;
- capability-grant issue/validation contracts;
- audit ledger and lifecycle events;
- durable outbox persistence;
- signing/verification providers where configured;
- ASP.NET Core or host-specific integration.

Use the [Generated API Reference](../api/) and the package-specific guides for exact contracts.

## What the gateway still owns

The gateway or executor must still enforce:

- current authentication/authorization;
- exact operation and resource validation;
- capability audience/scope/expiration/use limits;
- command or payload validation;
- current operational and safety state;
- rate/location/environment limits;
- idempotency and replay protection;
- physical or external-system safety;
- final refusal of stale, mismatched, or revoked authority.

## Robotics scenario

Robotics remains a scenario and integration example, not a shipped robotics controller. See [Robotics Operational Gateway](scenarios/robotics-operational-gateway.md) for the product-facing specimen.

## Related product documentation

- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [Capability Grant Hardening](capability-grant-hardening.md)
- [Capability Proof Trust Pinning](capability-proof-trust-pinning.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
- [AI Agent Gateway](scenarios/ai-agent-gateway.md)
