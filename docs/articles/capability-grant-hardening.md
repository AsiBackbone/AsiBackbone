# Capability Grant Hardening

This article documents provider-neutral capability grant validation, proof handling, and bounded-use checks for AsiBackbone.

AsiBackbone can model short-lived, scoped grants for governed execution, but it does not replace host authentication, host authorization, resource authorization, or external execution controls.

> [!IMPORTANT]
> A capability grant is not broad authority. It should be short-lived, scoped, bound to policy and acknowledgment context when needed, and checked at the execution boundary before any consequential action proceeds.

## Grant metadata

`CapabilityGrant` models the metadata a host can protect, persist, and validate:

| Field | Purpose |
| --- | --- |
| Token ID | Stable grant identifier for validation and bounded-use checks. |
| Issuer | Host or service that created the grant. |
| Audience | Intended execution gateway, service, or host boundary. |
| Subject ID | Optional authenticated principal the issuer authorizes. It has effect only when the relying party compares it with the current subject. |
| Operation name | Optional operation the issuer authorizes. It has effect only when the relying party compares it with the requested operation. |
| Scopes | Least-privilege actions allowed by the grant. |
| Issued UTC | Timestamp when the grant was created. |
| Not-before UTC | Optional timestamp before which the grant is not valid. |
| Expires UTC | Timestamp after which the grant is no longer valid. |
| Policy version/hash | Binds the grant to the policy context that produced it. |
| Acknowledgment/handshake reference | Binds follow-on execution to the approval or acknowledgment flow that authorized it. |
| Gateway/resource binding | Limits the grant to a specific gateway or target resource when supplied. |

The grant model is not a wire format. Hosts decide whether they serialize it as JSON, wrap it in a signed envelope, store it server-side, or project it into another provider-owned format.

## Canonical payload for a signed grant

Use `CanonicalPayloadBuilder.ForCapabilityGrant` to build the payload a grant is signed over:

```csharp
CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityGrant(grant);
CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);
```

The builder covers every field the grant carries, so the hash binds the whole grant. A hand-rolled payload that signs only a few fields leaves the rest outside the proof while the validator still enforces them, which means a value that was changed after signing can still pass validation. Scopes are normalized to a sorted, de-duplicated, ordinal set, so grants that differ only in scope ordering hash identically; a different scope set does not.

> [!IMPORTANT]
> Grant metadata is filtered through `CanonicalPayloadOptions.AllowsMetadataKey`, and the default allow-list is empty. With default options **no grant metadata is included in the proof**. This keeps unbounded and potentially sensitive host data out of hashed payloads, but it also means security-relevant data placed in grant metadata is unbound until its key is allow-listed:
>
> ```csharp
> CanonicalPayloadOptions options = CanonicalPayloadOptions.Create(metadataKeyAllowList: ["region"]);
> CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityGrant(grant, options);
> ```
>
> Use the same options wherever the payload is rebuilt, or the hashes will not agree.

## Validation profiles

`CapabilityGrantValidationOptions` provides explicit profiles so callers can communicate whether validation is occurring at a consequential execution boundary or is intentionally limited to metadata and time-bound checks.

| Profile | Subject/operation binding | Proof | Acknowledgment reference | Bounded-use/replay check | Intended use |
| --- | --- | --- | --- | --- | --- |
| `CreateExecutionBoundary(...)` | Subject required; operation optional | Required | Optional; caller can require it | Required by default; caller must explicitly disable it when another boundary owns replay/use enforcement | Operational gateways and other consequential execution boundaries |
| `CreateMetadataValidation(...)` | Optional | Not performed | Optional; caller can require it | Not performed | Structural, temporal, policy, scope, and binding validation where proof/use enforcement is intentionally out of scope |
| `Create(...)` | Optional | Configurable; default is off | Configurable; default is off | Configurable; default is off | Fully configurable validation path |

> [!CAUTION]
> Calling `CapabilityGrantValidator.ValidateAsync(signedGrant)` without explicit options preserves the existing 3.x behavior. It validates metadata and temporal constraints using `CapabilityGrantValidationOptions.Create()` defaults, but it does **not** verify the signed artifact proof and does **not** perform a bounded-use/replay check. Do not treat the no-options path as execution-boundary validation.

The execution-boundary profile requires `CapabilityGrantBindingExpectations` built from the authenticated current subject
and always requires proof verification. Include the requested operation whenever the grant is intended for a specific
operation. Both
comparisons are ordinal and fail closed when the grant value is missing or differs. Bounded-use validation is enabled by
default with `maxUseCount: 1`, but the host can explicitly set `requireUseCheck: false` when replay/use enforcement is
performed atomically by another trusted execution boundary. That opt-out should be intentional and documented by the host.

The metadata-validation profile keeps `expectedSubjectId` and `expectedOperationName` optional and intentionally does not
expose proof or use-check switches. Omitting those expectations means that validation result makes no claim that the grant
belongs to the current subject or requested operation. If proof or bounded-use behavior is needed, use
`CreateExecutionBoundary(...)` or the fully configurable `Create(...)` factory instead.

## Validation at the execution boundary

Use `CapabilityGrantValidator.ValidateAsync(...)` before follow-on execution. Validation can check:

- proof through the existing signing verification seam;
- issuer and audience;
- expiration and not-before time, with optional host-selected clock-skew tolerance;
- required scopes;
- policy version and policy hash;
- acknowledgment and handshake references;
- gateway and resource bindings;
- bounded-use state through an `ICapabilityGrantUseStore`.

Use the explicit execution-boundary profile for consequential execution:

```csharp
CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
    signedGrant,
    CapabilityGrantValidationOptions.CreateExecutionBoundary(
        CapabilityGrantBindingExpectations.Create(authenticatedSubjectId, "robotics.execute"),
        issuer: "policy-engine",
        audience: "robotics-gateway",
        scopes: ["robotics.execute"],
        policyVersion: "policy-v1",
        policyHash: "policy-hash",
        acknowledgmentId: "ack-123",
        requireAcknowledgmentReference: true,
        maxUseCount: 1),
    verificationService,
    useStore,
    cancellationToken);
```

Proceed only when `result.ShouldAllow` is true. The strict profile fails closed when proof verification is unavailable and defers when the required bounded-use store is unavailable.

## Intentional metadata-only validation

Some hosts need to inspect a grant before reaching the execution boundary, for example while routing a request, validating expected scope or policy bindings, or presenting diagnostic information. Use the explicitly named metadata profile for that case:

```csharp
CapabilityGrantValidationResult metadataResult = await CapabilityGrantValidator.ValidateAsync(
    signedGrant,
    CapabilityGrantValidationOptions.CreateMetadataValidation(
        issuer: "policy-engine",
        audience: "robotics-gateway",
        scopes: ["robotics.execute"],
        policyVersion: "policy-v1",
        policyHash: "policy-hash")
        .WithExpectedBindings(authenticatedSubjectId, "robotics.execute"),
    cancellationToken: cancellationToken);
```

A successful metadata-only result means only that the configured structural and temporal checks passed. It does not establish proof authenticity, replay resistance, single-use enforcement, authentication, authorization, or permission to execute an external action.

## 3.x migration guidance

The explicit profiles are additive and do not silently change existing 3.x behavior.

- Existing calls to `CapabilityGrantValidationOptions.Create(...)` continue to honor their current arguments and defaults.
- Existing calls to `ValidateAsync(signedGrant)` continue to use the legacy default options where proof, acknowledgment-reference, and bounded-use checks are disabled.
- New operational-gateway and consequential-execution code should prefer `CreateExecutionBoundary(...)`.
- Supply `CapabilityGrantBindingExpectations` built from the authenticated current subject; existing execution-boundary
  calls must add this required argument. The retained legacy overload throws instead of creating a subject-unbound
  profile. Include the requested operation when the issuer restricted the grant to an operation.
- Code that intentionally performs only structural or temporal validation should prefer `CreateMetadataValidation(...)` so the reduced validation contract is visible in code review.
- Hosts migrating an existing execution boundary should supply both an `IGovernanceSignatureVerificationService` and, when the profile keeps its default bounded-use requirement, an `ICapabilityGrantUseStore`.

The ambiguous no-options path remains available for 3.x compatibility. A future major version may tighten or remove that path; such a change would require explicit migration guidance rather than a silent behavioral change.

## Clock-skew tolerance

`CapabilityGrantValidationOptions.AllowedClockSkew` lets a host accommodate a small, bounded difference between the issuer clock and the validator clock. The default is `TimeSpan.Zero`, which preserves strict validation behavior.

A distributed host may opt into an explicit tolerance:

```csharp
CapabilityGrantValidationOptions options = CapabilityGrantValidationOptions.CreateMetadataValidation(
    validationUtc: hostClockUtc,
    allowedClockSkew: TimeSpan.FromSeconds(15));
```

The boundary semantics are:

- A not-before timestamp is accepted when it is no more than the configured skew ahead of the validator clock. The exact skew boundary is accepted.
- An expiration timestamp is accepted only while the elapsed time since expiration is less than the configured skew. The exact expiration-skew boundary is rejected.
- With zero skew, a grant is deferred before `NotBeforeUtc` and denied at or after `ExpiresUtc`, matching the strict default behavior.

Clock skew extends the effective acceptance window at both temporal boundaries. Keep it small, explicit, and selected by the host according to its deployment topology and risk model. It does not synchronize clocks, provide NTP infrastructure, or compensate for persistently incorrect system time. Production hosts should maintain reliable UTC clock synchronization and monitor clock drift rather than using a broad tolerance as a substitute.

Negative clock-skew values are rejected during option creation. The configured tolerance changes only the temporal checks; it does not relax proof, issuer, audience, scope, policy, acknowledgment, handshake, gateway, resource, replay, revocation, cancellation, or use-limit validation.

## Failure behavior

Capability grant validation maps failures to host-facing actions from the verification policy model.

| Failure | Category | Default action |
| --- | --- | --- |
| Missing proof when required | `MissingProof` | `Deny` |
| Invalid proof | `InvalidProof` | `Deny` |
| Wrong issuer or audience | `WrongIssuer`, `WrongAudience` | `Deny` |
| Wrong or missing subject | `SubjectMismatch` | `Deny` |
| Wrong or missing operation | `OperationMismatch` | `Deny` |
| Expired grant | `Expired` | `Deny` |
| Not yet valid | `NotYetValid` | `Defer` |
| Required scope missing | `WrongScope` | `Deny` |
| Policy mismatch | `PolicyMismatch` | `Deny` |
| Missing acknowledgment reference | `MissingAcknowledgmentReference` | `RequireAcknowledgment` |
| Acknowledgment or handshake mismatch | `AcknowledgmentMismatch`, `HandshakeMismatch` | `Deny` |
| Gateway or resource mismatch | `GatewayMismatch`, `ResourceMismatch` | `Deny` |
| Use limit exceeded | `ReuseLimitExceeded` | `Deny` |
| Grant stopped or cancelled | `Revoked`, `Cancelled` | `Deny` |
| Use store unavailable | `ReplayStoreUnavailable` | `Defer` |

High-risk workflows should not fall back to broad authority when validation fails.

## Bounded-use expectations

`ICapabilityGrantUseStore` is the provider-neutral seam for single-use or bounded-use workflows. Hosts own the production implementation because durable state, concurrency control, distributed locks, cache consistency, retention, and storage schema are deployment-specific.

Recommended use-store behavior:

```text
Validate metadata and proof
  -> check use state using grant ID
  -> atomically consume one use when accepted
  -> return use-limit, stopped, cancelled, or unavailable state when not accepted
```

For high-risk workflows, use checks should be atomic at the host storage boundary. Production use needs durable, concurrency-safe state that matches the host's transaction and retry model.

## Reference in-memory use store

`AsiBackbone.Storage.InMemory` includes `InMemoryCapabilityGrantUseStore` as a reference implementation for tests, samples, and local validation.

The following sample intentionally isolates bounded-use behavior and does not represent the complete execution-boundary profile:

```csharp
using AsiBackbone.Storage.InMemory.CapabilityGrants;

var useStore = new InMemoryCapabilityGrantUseStore();

CapabilityGrantValidationResult first = await CapabilityGrantValidator.ValidateAsync(
    signedGrant,
    CapabilityGrantValidationOptions.Create(requireUseCheck: true, maxUseCount: 1),
    useStore: useStore,
    cancellationToken: cancellationToken);

CapabilityGrantValidationResult replay = await CapabilityGrantValidator.ValidateAsync(
    signedGrant,
    CapabilityGrantValidationOptions.Create(requireUseCheck: true, maxUseCount: 1),
    useStore: useStore,
    cancellationToken: cancellationToken);
```

The first valid use is accepted. A second use of the same grant ID returns `ReuseLimitExceeded` with `capability.use-limit-exceeded` when `maxUseCount` is `1`.

Hosts that use the builder facade can register the reference store explicitly:

```csharp
services.AddAsiBackbone(builder =>
    builder.UseInMemoryCapabilityGrantUseStore());
```

The in-memory store is thread-safe inside one process and can represent stopped and cancelled local-validation states through its public helpers. It is **not** durable, distributed, replicated, or production replay protection. It does not coordinate across replicas, survive process restarts, or replace a host-owned database/cache/lock strategy.

### Retention horizon and clock skew

The store evicts a grant's use record once the grant has been expired for longer than `EvictionGracePeriod` (default five minutes), measured from the latest use time the store has observed. A grant past that horizon is refused with `ReuseLimitExceeded` and `capability.use-retention-elapsed` instead of being given a fresh count, because its earlier uses may already have been evicted.

Set `EvictionGracePeriod` to at least the largest `AllowedClockSkew` any validator uses with the store:

```csharp
TimeSpan skew = TimeSpan.FromMinutes(2);
var useStore = new InMemoryCapabilityGrantUseStore { EvictionGracePeriod = skew };
```

If the grace period is shorter than the skew, grants that are expired but still inside the skew are denied rather than accepted. That is fail-closed, but it rejects uses the validator would otherwise allow. Durable implementations of `ICapabilityGrantUseStore` should apply the same rule: never discard a use record while the grant it describes can still pass validation, and refuse a grant whose record may have been discarded.

### Stopping and cancelling grants

Use records are keyed by issuer and token ID. `StopGrant(issuer, grantId)` and `CancelGrant(issuer, grantId)` affect only that issuer's grant. The identifier-only overloads `StopGrant(grantId)` and `CancelGrant(grantId)` affect every issuer's grant that uses the identifier.

## Core boundary

Core provides metadata, validation result categories, bounded-use interfaces, and signing verification integration. Core does not provide:

- a bearer-token format;
- host authentication or authorization;
- automatic proof issuance;
- durable replay storage;
- distributed locking;
- external system execution;
- legal or compliance guarantees.

Use safe wording such as "the grant was validated for this execution context." Avoid wording such as "the grant replaces authorization" or "single-use is guaranteed" unless the host store provides that guarantee under documented assumptions.
