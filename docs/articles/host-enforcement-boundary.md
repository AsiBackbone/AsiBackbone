# Host enforcement boundary

AsiBackbone evaluates governance and produces decision evidence. The host remains responsible for the consequential
side effect and for ensuring that the effect happens only after the supported governed seam permits execution.

The integration invariant is deliberately simple:

1. Evaluate the current request and continuation authority.
2. Persist required decision evidence.
3. Check `EndpointGovernanceResult.CanExecute` at the host-owned seam.
4. Only then invoke the protected executor.
5. Record execution outcome separately from governance decision evidence.

An `Allowed` decision means governance permitted an execution attempt. It does not mean the host side effect succeeded.
The host can fail after an allowed decision, and execution outcome evidence must not rewrite the governance decision to
hide that distinction.

## Explicit anti-patterns

The following shapes are unsupported integration patterns:

- execute the protected side effect before governance evaluation;
- call the protected executor directly from an alternate endpoint, background path, or service path that bypasses the
  governed seam;
- treat possession of a decision receipt or queued transport data as current execution authority;
- observe governance in middleware without using the result to control whether the effect happens;
- continue after denied, deferred, acknowledgment-required/unsatisfied, missing/invalid capability, or required evidence
  persistence failure paths.

Queued or delayed work must perform the current validation/claim required by the host design before the protected effect.
Transport possession alone is not authority.

## Executable evidence

`tests/AsiBackbone.AspNetCore.Tests/Endpoints/HostEnforcementMisuseConformanceTests.cs` is the dedicated conformance and
misuse specimen. It keeps a protected executor invocation count and demonstrates both the supported guarded seam and
explicit bypass anti-patterns. The tests also preserve the distinction between an allowed governance decision and a host
execution failure.
