# AsiBackbone 7.4.0 Release Notes

Release date: 2026-10-21

`7.4.0` is a backward-compatible minor release for the stable `7.x` package
family. It completes the production path for request-bound signed capability
grants by adding provider-neutral public-key verification and durable, atomic
EF Core replay storage suitable for multi-replica hosts.

Package IDs, public namespaces, the `net10.0` target, existing canonical wire
values, documented defaults, and binary assembly identity are unchanged.
`AssemblyVersion` remains `7.0.0.0`; package and file versions advance to
`7.4.0` and `7.4.0.0`.

## Production capability-grant verification

- `AsiBackbone.Signing.ManagedKey` adds opt-in public-key verification through
  `AddAsiBackboneKeySignatureVerification(...)`.
- Trusted key versions support rotation and revocation, with explicit RSA-PSS
  SHA-256 and ECDSA P-256 SHA-256 descriptors.
- Public keys can be configured from PEM or JWK material, or resolved through a
  host-owned `IGovernanceVerificationKeyResolver`.
- Stable verification failure codes preserve a reviewable fail-closed boundary.
- Hosts continue to own key distribution, refresh, trust policy, monitoring,
  credentials, and the concrete managed-key signing client.

## Durable capability-grant replay storage

- `AsiBackbone.EntityFrameworkCore` adds `EfCoreCapabilityGrantUseStore`.
- Consumption is atomic across replicas and is scoped to issuer and token.
- The store represents stopped and cancelled states and supports
  expiry-plus-skew retention and purging.
- `AddAsiBackboneEfCoreCapabilityGrantUseStore<TDbContext>(...)` registers the
  store while preserving host ownership of the `DbContext`, provider, schema,
  migrations, connection string, and deployment workflow.

## Dependency maintenance

- Updated `Aspire.Hosting.AppHost` from `13.6.1` to `13.6.2`.
- Updated `FsCheck` from `3.4.0` to `3.5.0`.
- Refreshed affected package lock files.

## Compatibility

- The new verification and replay-storage paths are opt-in and additive.
- Existing hosts that do not register them see no registration or runtime
  behavior change.
- The signed capability-grant token and canonical payload remain the formats
  introduced in `7.3.0`; this release supplies production-oriented verification
  and shared replay storage around those contracts.
- Package IDs, public namespaces, target framework, and assembly identity remain
  compatible across the `7.x` line.
- Hosts adopting the EF Core use store own the corresponding migration and
  database rollout.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.4.0` together.
2. For production signed grants, configure trusted public keys or a host-owned
   key resolver and register `AddAsiBackboneKeySignatureVerification(...)`.
3. For multi-replica endpoints, register
   `AddAsiBackboneEfCoreCapabilityGrantUseStore<TDbContext>(...)`, apply the
   AsiBackbone model configuration, create and review a host-owned migration,
   and deploy it through the host's normal database process.
4. Keep the previous public-key version trusted for the longest relevant
   artifact lifetime plus allowed clock skew unless that version is revoked.
5. Restore, rebuild with warnings enabled, and run the consumer's tests.

Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

Stable publication occurs only from the protected `v7.4.0` tag after the
release-preparation pull request is merged to `main` and the release-candidate
checks pass.

See also:

- [7.4.0 Release Readiness Record](release-readiness-740.md)
- [7.4.0 Consumer Verification Guide](consumer-verification-740.md)
- [Signed Capability Grants at ASP.NET Core Endpoints](signed-capability-grants.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
