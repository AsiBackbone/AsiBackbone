# AsiBackbone 5.0.0 Release Notes

`5.0.0` starts the stable `5.x` AsiBackbone package family. It advances the
binary assembly identity to `5.0.0.0`. Package IDs, public namespaces, and the
`net10.0` target remain unchanged.

This is a security release. It resolves twelve findings from the September 2026
cross-repository review, including one critical and one high. Every behavior
change makes a governance path fail closed where it previously returned a
permissive result, which is why the work takes a major-version boundary rather
than a patch.

Consumers upgrading from `4.0.0` should follow the
[4.0.0 to 5.0.0 Migration Guide](upgrade-400-to-500.md). The enum renumbering
described there needs a data migration for hosts that persist those values as
integers.

## Verification binds artifact content to its signature

`GovernanceArtifactVerifier.VerifyAsync` recomputes the canonical payload hash
and denies when the payload does not hash to the signed value. Verification
previously compared two values the artifact carried about itself and forwarded
that self-reported hash to the provider, so the signature was checked against a
stored hash rather than against the content being evaluated. A caller that
rehydrated a signed artifact from storage or a queue could present modified
content beside an authentic hash and signature pair and receive a valid,
allowed outcome.

`SignedGovernanceArtifacts.Rehydrate` applies the same check at construction
for artifacts rebuilt from storage.

## Capability proofs bind to the grant being evaluated

`CapabilityGrantValidator.ValidateAsync` rebuilds the canonical payload from the
grant it is about to evaluate, compares that hash to the signed hash, and
asserts the signed artifact type and token identifier. Without those checks a
proof covering a narrower grant, or a validly signed hash from a different
artifact type, could be attached to a supplied grant whose issuer, audience,
scopes, expiry, and bindings the validator then read.

## Audit chains anchor and detect truncation

`AuditIntegrityVerifier.Verify` takes `expectedPreviousLinkHash` and fails
closed when a non-genesis partial chain supplies no anchor. It previously seeded
the expected previous hash with an empty string regardless of `requireGenesis`,
rejecting genuine partial chains while accepting a forged restart whose links
were rewritten to claim no predecessor.

`expectedTipLinkHash` and `expectedTipSequence` report a `TruncatedChain` when
the supplied links form a valid prefix that does not reach the expected tip.

## Permissive defaults now deny

* Omitted capability validation options deny instead of validating an unexpired
  grant against nothing.
* Requiring proof or a bounded-use check without an audience expectation is
  refused.
* A key outside the configured pin reports `signature.key-not-trusted` under the
  new `UntrustedKey` category and denies, rather than escalating more softly
  than a bad signature.
* A stripped signature denies when proof is required.
* Mapping a failure category to `Allow` requires an explicit unsafe opt-in.
* Zero is a rejected `Unspecified` sentinel in `VerificationPolicyAction`,
  `SignatureVerificationCategory`, `GrantUseState`, and
  `AuditIntegrityVerificationCategory`.

## Provider and host boundaries

* `ManagedKeySigningService` rejects a response whose key identifier, key
  version, or signature algorithm differs from the request.
* `UseLocalDevelopmentSigning` throws in Production unless `AllowInProduction`
  is set, and the new `ASIB003` analyzer diagnostic reports registrations with
  no environment guard at all.
* `GovernanceArtifactSigner.Sign*Async` throws when the provider returns no
  signature, unless `requireSignature: false` is passed.

## Personal data and durable rows

Email claims are no longer default actor-id or display-name sources. Actor
identifiers and display names are persisted verbatim and indexed in durable
audit rows, so the framework's own defaults placed personal data there while the
security guidance told hosts to keep it out.

## Capability use limits can be bound at issuance

`CapabilityTokenGrant` can carry the use limit its issuer authorized, and
validation uses the narrower of that limit and any limit the caller supplies. A
grant recording `AsiBackboneSchemaVersions.StableArtifactsV1` canonicalizes
exactly as it did in `4.x`, so grants signed before this release keep verifying.

## Other hardening

* Canonical payload metadata keys that collide after trimming are rejected
  rather than resolved by enumeration order.
* The web API template's sample capability validator compares the scopes a
  caller presented against the scopes an endpoint requires instead of comparing
  the endpoint's declaration against itself, and its audit-residue endpoint
  carries its own capability requirement.
* `InMemoryCapabilityGrantUseStore` keys use records by issuer and token
  identifier and evicts records for long-expired grants.

## Durable release evidence

The `v5.0.0` GitHub release retains its package SBOMs,
`sbom-manifest.json`, `release-evidence-manifest.json`, and a Markdown copy of
these release notes as durable public assets. See the
[release asset list](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v5.0.0)
and the [5.0.0 Consumer Verification Guide](consumer-verification-500.md) for
subject-digest provenance verification commands.

NuGet package signing remains deferred under the dated
[NuGet Package Signing Decision Record](nuget-package-signing-decision.md).

## Related documentation

* [4.0.0 to 5.0.0 Migration Guide](upgrade-400-to-500.md)
* [API Compatibility and SemVer](api-compatibility-and-semver.md)
* [Cryptographic Security Posture](cryptographic-security-posture.md)
* [5.0.0 Consumer Verification Guide](consumer-verification-500.md)
* [Regulated Storage and Signing Verification Checklist](regulated-storage-and-signing-verification-checklist.md)
