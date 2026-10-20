# Signed Capability Grants at ASP.NET Core Endpoints

Issue: [#890](https://github.com/AsiBackbone/AsiBackbone/issues/890).

This article documents the opt-in signed capability grant validator in `AsiBackbone.AspNetCore` and the Core pieces it is
built on: the capability grant signing helpers, the versioned `CapabilityGrantToken` encoding, the request-binding
provider abstraction, the grant identifier on decision receipts, and the configured-key mode of
`AsiBackbone.Signing.LocalDevelopment`.

AsiBackbone owns the orchestration and the fail-closed semantics. The host still owns every decision that is its own:
key material and the verification service, the use store, issuer and audience expectations, clock skew, what a request
binding covers, and issuance itself.

> [!IMPORTANT]
> Nothing described here is registered by default. Hosts that keep a claim-backed `IEndpointCapabilityGrantValidator`
> see no behavior or registration change.

## When to use signed grants, and when to use claims

| Use a claim-backed validator when | Use signed grants when |
| --- | --- |
| The capability is a standing entitlement of the authenticated user, such as a role or permission claim. | The capability is a one-time or short-lived authorization for a specific request, such as approving one order. |
| The authorization server and the endpoint already share trust through the authentication token. | The grant is issued by another step, service, or process, after a review, a second factor, or an acknowledgment. |
| Replay is not meaningful, because the claim may be used for every request. | The grant must be bound to the request content and consumed so it cannot be replayed. |
| You need no per-request audit identifier for the authority. | You want the audit ledger to name the grant that authorized each execution. |

A host can use only one `IEndpointCapabilityGrantValidator`. `AddAsiBackboneSignedCapabilityGrants` refuses to register
when another validator is already registered, so the choice is explicit.

## What the validator does

`SignedCapabilityGrantEndpointValidator` runs for every endpoint that declares `[RequireCapabilityGrant]` and whose
current decision is not already denied. For each request it:

1. returns an existing denial unchanged, without reading or consuming the grant;
2. resolves the actor through `IHttpGovernanceActorContextResolver`, and denies unless the actor is known, authenticated,
   and has a distinct identity;
3. reads exactly one token from the configured header (`AsiBackbone-Capability-Grant` by default) or form field;
4. decodes it with `CapabilityGrantToken.Decode`, which rebuilds the canonical payload and hash from the grant fields;
5. resolves the request binding from the registered `ICapabilityGrantRequestBindingProvider`;
6. validates with `CapabilityGrantValidationOptions.CreateBoundExecutionBoundary`, binding the actor identifier as the
   subject, the endpoint operation name, the endpoint scopes, the current decision's policy version and hash (unless
   `BindPolicy` is `false`), the request binding, and the configured issuer, audience, clock skew, and proof pins, with
   proof verification always required and a use consumed for every outcome except acknowledgment-required (see below);
7. denies **every** result other than `Valid`, whatever `VerificationPolicyAction` the core validator attached; and
8. on success returns the current decision unchanged and records the grant in `IEndpointCapabilityGrantFeature`.

Step 7 matters. `CapabilityGrantValidator` returns `Defer` for `NotYetValid` and `ReplayStoreUnavailable`, `Escalate` for
an unspecified use state or an unexpected failure, and `RequireAcknowledgment` for `MissingAcknowledgmentReference`. A
host that maps those actions literally turns an invalid grant into a deferral, an escalation, or an acknowledgment
challenge that a later flow could approve. At an execution boundary each of them is a denial.

Step 8 matters too. A valid grant preserves an allowed, warning, acknowledgment-required, deferred, or
escalation-recommended decision. It never upgrades a decision, so a grant cannot bypass an acknowledgment challenge.

An acknowledgment-required request is answered with a challenge and does not execute, so the validator verifies its grant
completely but does not consume a use. The acknowledged retry is validated again and consumes the use, so a single-use
grant survives the challenge round trip. Deferred and escalation-recommended decisions do consume a use, because a host
flow may later approve and execute them without the request passing through the validator again.

A missing verification service, use store, or binding provider denies rather than weakening validation. So does a
verifier or use store that throws.

## Registration

```csharp
builder.Services.AddAsiBackboneAspNetCore();

builder.Services.AddAsiBackboneSignedCapabilityGrants(options =>
{
    options.Issuer = "https://approvals.example.test";
    options.Audience = "orders-api";
    options.AllowedClockSkew = TimeSpan.FromSeconds(30);
});

// Defines what a grant's ResourceBinding covers for this host.
builder.Services.AddAsiBackboneFormFieldCapabilityGrantBinding();

// Host-supplied: verification key material and replay storage.
builder.Services.AddSingleton<IGovernanceSignatureVerificationService>(verificationService);
builder.Services.AddSingleton<ICapabilityGrantUseStore, SharedCapabilityGrantUseStore>();
```

The builder facade offers the same registration through `UseSignedCapabilityGrants(...)` and
`UseFormFieldCapabilityGrantBinding(...)`.

| Option | Default | Meaning |
| --- | --- | --- |
| `Issuer` | required | Issuer every accepted grant must name. |
| `Audience` | required | Audience every accepted grant must name. |
| `AllowedClockSkew` | zero | Tolerance on not-before and expiry. |
| `MaxUseCount` | `1` | Local use limit; the narrower of this and the grant's signed `MaxUseCount` applies. |
| `BindPolicy` | `true` | Require the grant's policy version and hash to match the current decision. |
| `TokenHeaderName` | `AsiBackbone-Capability-Grant` | Header that carries the token, or `null`. |
| `TokenFormFieldName` | `null` | Form field that carries the token, or `null`. The header is read first. |
| `MaxTokenLength` | 8192 | Longest accepted token, in characters. |
| `ExpectedProofKeyId`, `ExpectedProofKeyVersion`, `RequiredProofProvider`, `RequiredProofHashAlgorithm` | unset | Optional proof pins; see [Capability Proof Trust Pinning](capability-proof-trust-pinning.md). |
| `ProofPayloadOptions` | defaults | Canonical payload options the issuer signed with, including the metadata allow-list. |

## Reason codes

Denied decisions carry exactly one reason code from `SignedCapabilityGrantReasonCodes`. These values are public contract
and do not change within a major version.

| Reason code | Cause |
| --- | --- |
| `endpoint.capability_grant.missing` | No token was presented. |
| `endpoint.capability_grant.malformed` | The token could not be decoded, or more than one token was presented. |
| `endpoint.capability_grant.actor_unresolved` | No known, authenticated actor to bind the grant to. |
| `endpoint.capability_grant.binding_unavailable` | No binding provider is registered, or it could not bind the request. |

Each `CapabilityGrantValidationCategory` other than `Valid` maps to one code:

| Category | Core action | Reason code |
| --- | --- | --- |
| `MissingProof` | Deny | `endpoint.capability_grant.proof_missing` |
| `InvalidProof` | Deny | `endpoint.capability_grant.proof_invalid` |
| `WrongIssuer` | Deny | `endpoint.capability_grant.issuer_mismatch` |
| `WrongAudience` | Deny | `endpoint.capability_grant.audience_mismatch` |
| `SubjectMismatch` | Deny | `endpoint.capability_grant.subject_mismatch` |
| `OperationMismatch` | Deny | `endpoint.capability_grant.operation_mismatch` |
| `Expired` | Deny | `endpoint.capability_grant.expired` |
| `NotYetValid` | Defer | `endpoint.capability_grant.not_yet_valid` |
| `WrongScope` | Deny | `endpoint.capability_grant.scope_missing` |
| `PolicyMismatch` | Deny | `endpoint.capability_grant.policy_mismatch` |
| `MissingAcknowledgmentReference` | RequireAcknowledgment | `endpoint.capability_grant.acknowledgment_missing` |
| `AcknowledgmentMismatch` | Deny | `endpoint.capability_grant.acknowledgment_mismatch` |
| `HandshakeMismatch` | Deny | `endpoint.capability_grant.handshake_mismatch` |
| `GatewayMismatch` | Deny | `endpoint.capability_grant.gateway_mismatch` |
| `ResourceMismatch` | Deny | `endpoint.capability_grant.resource_mismatch` |
| `ReuseLimitExceeded` | Deny | `endpoint.capability_grant.use_limit_exceeded` |
| `Revoked` | Deny | `endpoint.capability_grant.revoked` |
| `Cancelled` | Deny | `endpoint.capability_grant.cancelled` |
| `ReplayStoreUnavailable` | Defer | `endpoint.capability_grant.use_store_unavailable` |
| `Failed` | Escalate or Deny | `endpoint.capability_grant.failed` |

Every row produces a denied decision. The core failure code, such as `capability.proof-content-mismatch`, is logged at
information level with the reason code; token content is never logged.

## Request binding

The grant's `ResourceBinding` must equal the value the registered `ICapabilityGrantRequestBindingProvider` computes for
the request. When the provider returns `CapabilityGrantRequestBinding.None`, only grants with no resource binding are
accepted; when it returns `Unavailable`, the request is denied.

`FormFieldCapabilityGrantRequestBindingProvider` binds the submitted form fields. Its value is `form-sha256:` followed by
the SHA-256 hash of a canonical JSON encoding: field names lower-cased and sorted ordinally, each with its values in
submitted order. It leaves out the antiforgery field (`AntiforgeryOptions.FormFieldName`, plus
`__RequestVerificationToken` by default), the configured token form field, and any name added to
`FormFieldCapabilityGrantBindingOptions.ExcludedFieldNames`. A request that cannot carry a body binds the empty field set.
A form containing files, a non-form body, or an unreadable form is not bound and is denied.

The form provider covers only the form body. The query string and route values are not covered, so an endpoint that
takes a resource identifier from its route should carry the identifier as a form field or register a provider that
binds it. A JSON body provider, for example, can hash a canonical form of the body and return
`CapabilityGrantRequestBinding.Bound(value)`.

## Issuance responsibilities

AsiBackbone does not issue grants. The host decides who is entitled to a grant, how long it lives, and where the issuing
endpoint runs. An issuer composes three calls:

```csharp
CapabilityGrant grant = CapabilityGrant.Create(
    tokenId: Guid.NewGuid().ToString("N"),
    issuer: "https://approvals.example.test",
    audience: "orders-api",
    scopes: ["orders.approve"],
    issuedUtc: now,
    expiresUtc: now.AddMinutes(2),
    subjectId: actor.ActorId,            // the identifier the relying party's actor resolver returns
    operationName: "orders.approve",     // the relying endpoint's operation name
    policyVersion: decision.PolicyVersion,
    policyHash: decision.PolicyHash,
    resourceBinding: FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(fieldsToApprove),
    maxUseCount: 1);

SignedGovernanceArtifact<CapabilityGrant> signed =
    await GovernanceArtifactSigner.SignCapabilityGrantAsync(grant, signingService, cancellationToken: ct);

string token = CapabilityGrantToken.Encode(signed);
```

`SignCapabilityGrantAsync`, `CreateUnsignedCapabilityGrant`, and `CreateSigningReadyCapabilityGrant` sign under schema
`2.0.0` by default, so `MaxUseCount` is covered by the signature. They refuse to sign a grant that carries a use limit
under schema `1.0.0`, whose canonical payload omits the limit. The returned artifact carries the grant that was signed;
encode that one.

Keep grants short-lived. The lifetime bounds how long a leaked token is useful and how long a rotated key must stay
trusted.

## Token encoding

A token is `asibcg1.` followed by the unpadded base64url encoding of a UTF-8 JSON document:

```json
{
  "v": 1,
  "grant": { "tokenId": "...", "issuer": "...", "audience": "...", "scopes": ["..."], "issuedUtc": "...", "expiresUtc": "...", "schemaVersion": "2.0.0", "...": "..." },
  "proof": { "hashAlgorithm": "SHA-256", "signature": "...", "signatureAlgorithm": "...", "keyId": "...", "keyVersion": "...", "provider": "...", "signedUtc": "...", "metadata": { "...": "..." } }
}
```

- **Format version.** `v` is the token format version, currently `1`, and the prefix names it too. A decoder rejects any
  other version rather than guessing. A future incompatible format uses a new prefix and version.
- **Schema version.** `grant.schemaVersion` is the grant's canonical payload schema, separate from the token format. Use
  `2.0.0` for new grants.
- **No payload or hash on the wire.** `Decode` rebuilds the canonical payload and hash from the grant fields, so editing
  any field, including `resourceBinding`, produces a hash the signature does not cover. Canonical descriptors in the
  proof metadata (`artifact_id`, `artifact_type`, `canonicalization_version`, `payload_schema_version`) are derived on
  decode, not read.
- **Metadata.** Only grant metadata keys allowed by the canonical payload options are transported, because only those are
  signed. Issuer and relying party must use the same `CanonicalPayloadOptions`.
- **Strict and bounded.** Unknown or duplicate properties, nesting deeper than the document needs, and tokens longer than
  the limit fail with a stable code (`capability.token-missing`, `-too-large`, `-format-unsupported`, `-malformed`,
  `-grant-invalid`, or `-hash-algorithm-unsupported`). Failure messages never echo token content.
- **Not encrypted.** Anyone holding a token can read every field. Do not put secrets in a grant.

A successful decode is not verification. Validate the result before trusting any field.

## Audit evidence

When the validator accepts a grant, it sets `IEndpointCapabilityGrantFeature` on the request. `DefaultEndpointGovernanceService`
clears that feature before calling the validator and, when audit is requested and the final decision is not denied, writes
the grant identifier to the receipt with `DecisionReceipt.WithCapabilityGrantId`. `AuditLedgerRecord.FromDecisionReceipt`
copies `IDecisionReceipt.CapabilityGrantId` into `AuditLedgerRecord.CapabilityTokenId` when no explicit identifier is
passed, and the ledger record's canonical payload binds it as `capabilityGrantId`.

The canonical decision receipt payload includes `capabilityGrantId` only when a receipt carries one, so existing receipts,
and receipts without a grant, hash exactly as before. A claim-backed validator can set the same feature to record the
grant it accepted.

## Key rotation

The validator accepts any key the registered `IGovernanceSignatureVerificationService` trusts, unless
`ExpectedProofKeyId` or `ExpectedProofKeyVersion` pins one. To rotate:

1. make the verification service trust the new key alongside the old one;
2. move issuers to the new key;
3. keep trusting the old key for at least the longest grant lifetime plus the allowed clock skew; then
4. retire the old key.

Do not pin `ExpectedProofKeyVersion` across a rotation, because grants signed under the previous version would be denied
as `proof_invalid` until they expire. Pin a key identifier only when every issuer uses it. See
[Key Rotation and Retired-Key Verification](key-rotation-and-retired-key-verification.md) for the provider-side guidance.

## Multi-replica replay storage

Replay protection is only as wide as the `ICapabilityGrantUseStore`. `InMemoryCapabilityGrantUseStore` is process-local:
behind a load balancer, a single-use grant can be accepted once by each replica. Hosts with more than one replica need a
shared store whose consume operation is atomic across replicas, for example a database row with a unique key on the
token identifier and an update guarded by the use count. The store must also retain a consumed grant at least until the
grant expires plus the allowed clock skew, following the retention rule in
[Capability Grant Hardening](capability-grant-hardening.md#retention-horizon-and-clock-skew). A store that cannot
answer returns `Unavailable`, which the validator denies.

## Signing provider with a configured key

`AsiBackbone.Signing.LocalDevelopment` generates a new RSA key in each process by default, so a token issued before a
restart, or by another process, never verifies. Setting `LocalDevelopmentSigningOptions.RsaKeyPem` gives every process
configured with the same PEM key the same signing identity. A process configured with only the public key can verify but
not sign. The provider remains a local-development provider: the key lives in process memory with no custody, rotation,
or revocation story, and registration is still refused in the Production environment unless `AllowInProduction` is set.
Production hosts should supply a managed key provider; see [Managed Key Signing Provider](managed-key-signing-provider.md).
