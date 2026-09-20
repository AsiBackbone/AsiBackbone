# AsiBackbone 7.0.0 Release Notes

Release date: 2026-09-20

## Summary

`7.0.0` is a major release for the AsiBackbone package family. It carries two
security corrections that change stable contracts, so both require a major
boundary rather than a `6.x` release.

Package IDs, public namespaces, and the `net10.0` target remain unchanged. No
public type, member, namespace, or package is renamed or removed.
`AssemblyVersion` advances to `7.0.0.0`; package and file versions advance to
`7.0.0` and `7.0.0.0` respectively.

Consumers moving from `6.x` must follow the
[Upgrade from 6.x to 7.0](upgrade-600-to-700.md) guide.

## Breaking changes

### Acknowledgment responses are bound to the challenged actor

`IAcknowledgmentChallengeService.HandleResponse` now verifies that the responding
actor is the actor the challenge was issued to before producing a
`LiabilityHandshakeAcknowledgment`.

Previously the response was validated only against the handshake identifier and
the required acknowledgment code, so any actor that could name an active
challenge could satisfy a challenge issued to a different actor, and the
resulting acknowledgment recorded whichever actor answered rather than whoever
was challenged.

The `actor` argument must now match both the `ActorId` and the `ActorType`
recorded by `CreateChallenge`. A mismatch returns a failed result carrying the
new `acknowledgment.challenge.actor_mismatch` reason code, and no acknowledgment
is created. A single reason code covers both comparisons so a caller cannot use
the failure to determine which component differed.

Hosts that resolve the current actor per request must resolve the same principal
on both legs of the round trip.

This change does not add challenge expiry or single-use enforcement. Bounded
challenge lifetime, revalidating authorization, and revalidating current policy
before the consequential operation remain host responsibilities.

### DLP classification enums no longer default to a permissive value

`DlpFailureBehavior` and `DlpIntentRiskLevel` each gained an `Unspecified` member
at zero, shifting every other member up by one.

Previously `DlpFailureBehavior.Allow` and `DlpIntentRiskLevel.Low` occupied the
zero slot, which is the value a .NET enum takes when it is never assigned. An
unset property, an absent configuration value, a deserialized payload that
omitted the field, or a database column default therefore resolved a screening
failure to the most permissive outcome available. A governance policy that was
never fully configured failed open, and nothing reported it.

| Member | `6.x` value | `7.0` value |
| --- | --- | --- |
| `DlpFailureBehavior.Unspecified` | — | `0` |
| `DlpFailureBehavior.Allow` | `0` | `1` |
| `DlpFailureBehavior.WarnAndAllow` | `1` | `2` |
| `DlpFailureBehavior.Deny` | `2` | `3` |
| `DlpFailureBehavior.Defer` | `3` | `4` |
| `DlpFailureBehavior.RequireAcknowledgment` | `4` | `5` |
| `DlpFailureBehavior.Escalate` | `5` | `6` |
| `DlpIntentRiskLevel.Unspecified` | — | `0` |
| `DlpIntentRiskLevel.Low` | `0` | `1` |
| `DlpIntentRiskLevel.Medium` | `1` | `2` |
| `DlpIntentRiskLevel.High` | `2` | `3` |

Supplying `Unspecified` now raises `ArgumentOutOfRangeException` rather than
resolving to a behavior. `DlpFailurePolicyContext.Create` rejects an unassigned
risk level, `DlpFailurePolicyOptions.GetBehavior` rejects an unconfigured
risk-tier behavior or `BehaviorOverrides` entry, and
`DlpFailurePolicyResolution.Create` rejects an unresolved behavior.

Source that refers to these members by name is unaffected. Consumers that
persisted, serialized, or transmitted the numeric values must remap them,
because enum constants compiled against `6.0.0` retain the previous numbers.

## Why this required a major release

The repository's
[API compatibility and SemVer contract](api-compatibility-and-semver.md) treats a
change to a public enum value as affecting a stable package contract. The `6.x`
line also pins `AssemblyVersion` at `6.0.0.0` for every compatible release, so a
consumer compiled against `6.0.0` binds the same assembly identity regardless of
package version. Shipping the renumbering on `6.x` would have let that consumer
load a library that reinterprets the constants the compiler already inlined into
its own assembly, with nothing in the assembly identity to signal the change.

## Validation

Package validation continues to run against the `5.1.0` baseline. The intentional
enum value changes are recorded as exact `CP0011` suppressions in
`src/AsiBackbone.Core/CompatibilitySuppressions.xml`; all other package
compatibility checks remain enabled. The committed public API baselines under
`eng/api-baseline/` record the new enum values and the two added members.

## Related documentation

* [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
* [API Compatibility and SemVer](api-compatibility-and-semver.md)
* [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
* [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
* [6.0.0 Release Notes](release-notes-600.md)
