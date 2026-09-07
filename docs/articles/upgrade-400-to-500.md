# Upgrade Guide: 4.0.0 to 5.0.0

This guide covers the consumer-visible changes when upgrading the stable
AsiBackbone package family from `4.0.0` to `5.0.0`.

`5.0.0` is a security release. Every behavior change in it makes a governance
path fail closed where it previously returned a permissive result. A host whose
artifacts are produced and verified through the shipped factories, and whose
validation call sites already state what they are validating against, should
need only the package reference update and a rebuild. The remaining sections
describe the call sites that now require a decision.

## Update package references

Update every consumed `AsiBackbone.*` package together:

```xml
<PackageReference Include="AsiBackbone.Core" Version="5.0.0" />
<PackageReference Include="AsiBackbone.AspNetCore" Version="5.0.0" />
<PackageReference Include="AsiBackbone.EntityFrameworkCore" Version="5.0.0" />
```

Package IDs and namespaces have not changed. Rebuild the host because the
assembly identity advances from `4.0.0.0` to `5.0.0.0`.

## Migrate persisted enum values

`VerificationPolicyAction`, `SignatureVerificationCategory`, `GrantUseState`,
and `AuditIntegrityVerificationCategory` now reserve zero as an `Unspecified`
sentinel that is rejected wherever the value is consumed. The members that
previously held zero were renumbered:

| Enum | Member | `4.x` value | `5.0.0` value |
| --- | --- | --- | --- |
| `VerificationPolicyAction` | `Allow` | `0` | `7` |
| `SignatureVerificationCategory` | `Valid` | `0` | `11` |
| `GrantUseState` | `Accepted` | `0` | `5` |
| `AuditIntegrityVerificationCategory` | `Valid` | `0` | `12` |

This is the change most likely to need work. A default-constructed value, or a
database column that yields zero for input it does not recognize, previously
meant "allow", "valid", or "accepted". It now means "unspecified" and is
refused.

**If you persist any of these enums as integers** — which EF Core does by
default — existing rows holding `0` will read back as `Unspecified` after the
upgrade. Migrate those rows before deploying:

```sql
UPDATE VerificationOutcomes SET Action = 7 WHERE Action = 0;
UPDATE VerificationOutcomes SET Category = 11 WHERE Category = 0;
```

Adjust table and column names to your schema. If you persist these values as
strings, no migration is needed.

**If you compiled against `4.x`**, rebuild. Enum constants are inlined at
compile time, so an assembly built against the old numbering keeps the old
integers and will disagree with this release silently rather than failing to
load.

## State what capability validation is validating against

`CapabilityGrantValidator.ValidateAsync` no longer accepts omitted options. The
omitted-options path previously built permissive defaults — no proof, no use
check, and no issuer, audience, or scope expectations — and returned `Valid` for
any grant that had not expired. It now denies with
`capability.validation-options-required`.

Supply options describing what the grant must satisfy:

```csharp
CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
    signedGrant,
    CapabilityGrantValidationOptions.CreateExecutionBoundary(
        issuer: "https://issuer.example",
        audience: "gateway-1",
        scopes: ["robotics.execute"]),
    verificationService,
    useStore,
    cancellationToken);
```

Requiring proof or a bounded-use check without an audience expectation is also
refused, because proof establishes that a grant was signed rather than that it
was issued for this audience.

## Anchor partial audit chain verification

`AuditIntegrityVerifier.Verify` with `requireGenesis: false` now requires the
hash of the link preceding the supplied range:

```csharp
AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
    links,
    expectedChainId: chainId,
    requireGenesis: false,
    expectedPreviousLinkHash: precedingLink.LinkHash);
```

Without an anchor the call fails with
`integrity.expected-previous-hash-required`. Previously the verifier compared
the first supplied link against an empty previous hash, which rejected genuine
partial chains and accepted rewritten ones.

To establish that you hold a complete chain rather than a valid prefix, supply
`expectedTipLinkHash` or `expectedTipSequence`. A mismatch reports
`TruncatedChain`.

## Review verification policy overrides

Mapping a failure category to `VerificationPolicyAction.Allow` now requires
`allowUnsafeAllowOverrides: true` on `VerificationPolicyOptions.Create`. A
key-pin mismatch now reports `signature.key-not-trusted` under the new
`UntrustedKey` category, which denies by default rather than escalating, and a
stripped signature denies when proof is required rather than mapping to
`RequireAcknowledgment`.

If your host branched on `UnknownKeyVersion` for pin mismatches, branch on
`UntrustedKey` instead.

## Expect signing to throw when no signature is produced

`GovernanceArtifactSigner.Sign*Async` now throws when the provider returns no
signature. To accept and inspect an unsigned result, opt in explicitly:

```csharp
SignedGovernanceArtifact<AuditLedgerRecord> artifact =
    await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
        record,
        signingService,
        requireSignature: false,
        cancellationToken: cancellationToken);
```

## Replace local-development signing outside development

`UseLocalDevelopmentSigning` throws when the environment is Production. The
provider generates its key per process and never persists it, so artifacts it
signs stop verifying after a restart.

Register a managed key provider for production, or state the intent explicitly
when a non-production workload is deliberately using an ephemeral key:

```csharp
builder.UseLocalDevelopmentSigning(
    LocalDevelopmentSigningOptions.Create(allowInProduction: true));
```

The guard reads `DOTNET_ENVIRONMENT` and then `ASPNETCORE_ENVIRONMENT`. A host
that determines its environment another way can pass `environmentName`.

The new `ASIB003` analyzer diagnostic reports registrations that sit behind no
environment check at all. Guard the call, set `AllowInProduction`, or apply the
existing host review marker to accept it deliberately.

## Add email claims back if you want them

`ClaimTypes.Email` is no longer a default actor-id or display-name claim source,
because actor identifiers and display names are persisted verbatim and indexed
in durable audit rows. If your deployment intends to record email addresses
there, add the claim types back explicitly and accept the retention
consequences:

```csharp
services.Configure<AsiBackboneHttpActorContextOptions>(options =>
{
    options.ActorIdClaimTypes.Add(ClaimTypes.Email);
    options.DisplayNameClaimTypes.Add(ClaimTypes.Email);
});
```

## Bind capability use limits at issuance

A grant can now carry the use limit its issuer authorized. A limit supplied only
at the validation call site is local policy that nothing in the signature
covers; validation uses the narrower of the two.

To bind one, create the grant with a limit and record the schema version that
carries it:

```csharp
CapabilityTokenGrant grant = CapabilityTokenGrant.Create(
    tokenId: tokenId,
    issuer: issuer,
    audience: audience,
    scopes: scopes,
    issuedUtc: issuedUtc,
    expiresUtc: expiresUtc,
    maxUseCount: 1,
    schemaVersion: AsiBackboneSchemaVersions.StableArtifactsV2);
```

This is opt-in. A grant recording `StableArtifactsV1` canonicalizes exactly as
it did in `4.x`, so grants signed before this release keep verifying unchanged.

## Normalize canonical payload metadata keys

`CanonicalPayloadBuilder` now throws when metadata keys collide after trimming,
because `"k"` and `" k"` both normalize to `"k"` and the surviving value
depended on enumeration order. Normalize metadata keys before signing.

## Validate the upgrade

1. Rebuild every host assembly against `5.0.0.0`.
2. Migrate persisted enum columns that hold zero.
3. Run the host's denial-path tests and confirm blocked decisions still stop
   before the executor.
4. Re-verify a sample of retained signed artifacts and audit chains, supplying
   an anchor for partial chains.
5. Confirm no production configuration path registers local-development
   signing.

## Related documentation

* [API Compatibility and SemVer](api-compatibility-and-semver.md)
* [Regulated Storage and Signing Verification Checklist](regulated-storage-and-signing-verification-checklist.md)
* [Schema Versioning](schema-versioning.md)
