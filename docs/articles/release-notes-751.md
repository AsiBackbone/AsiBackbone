# AsiBackbone 7.5.1 Release Notes

Release date: 2026-10-24

`7.5.1` is a backward-compatible patch release for the stable `7.x` package
family. It strengthens production-signing guidance, host-enforcement
conformance evidence, and package dependency validation without changing
public APIs, package dependencies, durable schemas, canonical wire values,
package IDs, the `net10.0` target, or binary assembly identity `7.0.0.0`.
Package and file versions advance to `7.5.1` and `7.5.1.0`.

The trace-correlation APIs and behavior introduced in `7.5.0` remain unchanged.

## Production-signing guidance

- The existing `AsiBackbone.Signing.LocalDevelopment` Production guard now
  states more directly that local-development signing is not a production
  key-custody control and directs production hosts to
  `AsiBackbone.Signing.ManagedKey` with a reviewed host-owned implementation.
- `LocalDevelopmentSigningOptions.AllowInProduction` is documented as an
  explicit escape hatch for exceptional test or demonstration scenarios only.
  It does not make the local-development provider suitable for normal
  production use.
- Non-hosted and unit-test scenarios remain usable when no environment name is
  available. The guard behavior and public configuration surface are unchanged.

## Host-enforcement conformance evidence

- New executable conformance tests demonstrate that protected side effects run
  only after governance permits execution.
- Denial, deferral, unsatisfied acknowledgment, missing or invalid capability
  authority, and required evidence-persistence failure all result in zero
  protected-executor calls in the supported integration sequence.
- The tests and accompanying guidance identify side-effect-before-evaluation
  and alternate executor access as host integration anti-patterns while keeping
  authentication, authorization, transactions, execution, and side effects
  host-owned.

## Package and release validation

- NuGet central transitive pinning is now explicitly disabled for the reusable
  package family. Enabling it would promote repository implementation choices
  into consumer-visible package constraints.
- Stable package validation now compares each generated package's dependency
  IDs, dependency versions, and framework references with reviewed expectations.
  The generated `7.5.1` package dependency surface is unchanged from `7.5.0`.
- Contributor and supply-chain guidance records the lock-file, vulnerability
  lift, package-boundary, and review expectations for future dependency changes.
- Major-version history now records the reason and upgrade cost for each major
  transition from `1.x` through `7.x`.
- Repository workflow dependencies are refreshed without changing shipped
  package contents or runtime behavior.

## Compatibility

No public signature, package dependency, durable contract, documented default,
or runtime decision behavior changed. The only shipped-code adjustment clarifies
the exception text produced by the already-existing local-development Production
guard. Existing compiled and source consumers remain compatible.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.5.1` together.
2. Confirm production hosts use `AsiBackbone.Signing.ManagedKey` with a reviewed
   host-owned managed-key implementation rather than the local-development
   provider.
3. Keep the governed decision-before-side-effect boundary in host integration
   tests, including alternate and delayed execution paths.
4. Restore, rebuild with warnings enabled, and run the consumer's tests.

Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

Stable publication occurs only from the protected `v7.5.1` tag after the
release-preparation pull request is merged to `main` and required hosted checks
pass on the release candidate.

See also:

- [7.5.1 Release Readiness Record](release-readiness-751.md)
- [7.5.1 Consumer Verification Guide](consumer-verification-751.md)
- [7.5.0 Release Notes](release-notes-750.md)
- [Host Enforcement Boundary](host-enforcement-boundary.md)
- [Central Transitive Pinning Decision Record](central-transitive-pinning-decision.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
