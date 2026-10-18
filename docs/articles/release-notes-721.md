# AsiBackbone 7.2.1 Release Notes

Release date: 2026-10-18

`7.2.1` is a backward-compatible patch release for the stable `7.x` package
family. It corrects endpoint-governance capability enforcement without changing
public APIs, package dependencies, durable schemas, canonical wire values,
package IDs, the `net10.0` target, or binary assembly identity `7.0.0.0`.
Package and file versions advance to `7.2.1` and `7.2.1.0`.

The public parameter-name changes introduced in the `7.2` line are described
in the [7.2.0 Release Notes](release-notes-720.md) and remain current.

## Endpoint-governance fixes

- Capability-grant validation now runs for every governance decision that is
  not already denied, including warning, acknowledgment-required, deferred,
  and escalation-recommended outcomes. A missing grant therefore produces a
  denial before the host begins a challenge, deferral, or escalation flow.
- When `FailClosedWhenCapabilityValidatorMissing` is `false`, a missing
  capability validator now skips only that validation stage. Evaluation
  continues through audit, acknowledgment, and final result handling instead
  of returning an allowed result early.
- The endpoint-governance documentation and validator contract remarks now
  describe the ordering requirement explicitly.

These fixes close the gap tracked by
[#887](https://github.com/AsiBackbone/AsiBackbone/issues/887).

## Compatibility

No public signature or durable contract changed. Existing compiled consumers
remain binary compatible.

Hosts that implement `IEndpointCapabilityGrantValidator` should deny a missing
grant and otherwise preserve the supplied `currentDecision`. The validator may
now receive acknowledgment-required, deferred, and escalation-recommended
decisions in addition to allowed and warning decisions. This is the intended
enforcement order for endpoints that combine capability requirements with
those governance outcomes.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.2.1` together.
2. If the host implements `IEndpointCapabilityGrantValidator`, confirm that it
   returns a denial for a missing grant and preserves `currentDecision` for a
   valid grant.
3. Exercise endpoints that combine `[RequireCapabilityGrant]` with
   acknowledgment, deferral, escalation, or audit requirements.

Consumers moving from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

The release candidate is validated through locked restore, Debug and Release
builds, the full test suite, formatting, public API baselines, package metadata
and contents, template and external-consumer smoke tests, documentation release
claims, and DocFX. Stable publication occurs only from the protected `v7.2.1`
tag after the release-preparation pull request is merged to `main`.

See also:

- [7.2.1 Release Readiness Record](release-readiness-721.md)
- [7.2.1 Consumer Verification Guide](consumer-verification-721.md)
- [7.2.0 Release Notes](release-notes-720.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
