# Upgrade from 6.x to 7.0

Version 7.0 carries two security corrections that change stable contracts. It binds liability handshake acknowledgment responses to the actor the challenge was issued to, and it moves `DlpFailureBehavior` and `DlpIntentRiskLevel` off their permissive zero values, which changes the numeric value of every existing member of both enums. These are intentional major-version breaks. Rebuild consumers against the 7.0 packages after migrating.

No public type, member, namespace, or package was renamed or removed in this release. Code that compiles against 6.x continues to compile against 7.0 unless it relies on one of the behaviors below. The acknowledgment and DLP changes are the two security corrections that required the major boundary; [Other changes that affect hosts](#other-changes-that-affect-hosts) covers the remaining adjustments.

## Why these changes required a major release

The repository's [API compatibility and SemVer contract](api-compatibility-and-semver.md) treats a change to a public enum value as affecting a stable package contract. The `6.x` line also pins `AssemblyVersion` at `6.0.0.0` for every compatible release, so a consumer compiled against `6.0.0` binds the same assembly identity regardless of package version. Shipping the enum renumbering on `6.x` would have let that consumer load a library that reinterprets the constants the compiler already inlined into its own assembly, with nothing in the assembly identity to signal the change. `7.0.0` advances `AssemblyVersion` to `7.0.0.0`, so the boundary is explicit.

## Acknowledgment responses are bound to the challenged actor

`IAcknowledgmentChallengeService.HandleResponse` now verifies that the responding actor is the actor the challenge was issued to before it produces a `LiabilityHandshakeAcknowledgment`.

In 6.x the response was validated only against the handshake identifier and the required acknowledgment code. Any actor that could name an active challenge could satisfy a challenge issued to a different actor, and the resulting acknowledgment recorded whichever actor answered. For a package whose purpose is attributing accountability, that made the acknowledgment unreliable as evidence.

In 7.0 the `actor` argument must match both the `ActorId` and the `ActorType` that `CreateChallenge` recorded. `ActorId` is compared ordinally after trimming; `ActorType` must be equal, because the same identifier under a different actor type is a different principal. A mismatch returns a failed result carrying the new `acknowledgment.challenge.actor_mismatch` reason code, and no acknowledgment is created. A single reason code covers both comparisons so a caller cannot use the failure to determine which component differed.

### What to change

A host that resolves the current actor per request must resolve the same principal on both legs of the round trip:

```csharp
// Challenge leg.
AcknowledgmentChallenge challenge = challengeService.CreateChallenge(
    currentActor,
    "PublishEpisode",
    decision);

// Response leg: currentActor must resolve to the same ActorId and ActorType.
AcknowledgmentChallengeResult result = challengeService.HandleResponse(
    challenge,
    currentActor,
    response);
```

Hosts that stored the challenge and rehydrated it for a later request should confirm that their actor resolution is stable across those requests. If the host resolves an actor from an authentication ticket, the identifier and actor type must survive the round trip unchanged.

Handle the new reason code where response failures are surfaced:

| Reason code | Meaning |
| --- | --- |
| `acknowledgment.challenge.mismatch` | The response did not name the active challenge. |
| `acknowledgment.challenge.actor_mismatch` | The response was submitted by an actor other than the challenged actor. |
| `acknowledgment.challenge.code_mismatch` | The response did not carry the required acknowledgment code. |

### What this change does not do

Actor binding is not challenge expiry and not single-use enforcement. `AcknowledgmentChallenge` still carries no expiry, and nothing consumes a challenge when it is used, so a stored response payload remains replayable. Bounded-lifetime challenge state, revalidating authorization, and revalidating current policy before the consequential operation all remain host responsibilities. See [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md).

## DLP classification enums no longer default to a permissive value

`DlpFailureBehavior` and `DlpIntentRiskLevel` each gained an `Unspecified` member at zero, shifting every other member up by one.

In 6.x, `DlpFailureBehavior.Allow` and `DlpIntentRiskLevel.Low` occupied the zero slot, which is the value a .NET enum takes when it is never assigned. An unset property, an absent configuration value, a deserialized payload that omitted the field, or a database column default therefore resolved a screening failure to the most permissive outcome available: `Low` maps to `WarnAndAllow` under the default risk posture. A governance policy that was never fully configured failed open, and nothing reported it.

### New values

| Member | 6.x value | 7.0 value |
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

### What to change

**Source that names these members needs no change.** `DlpFailureBehavior.Deny` still means deny. Recompiling against 7.0 picks up the new numbers automatically.

**Persisted, serialized, or transmitted numeric values must be remapped carefully.** Enum constants are inlined at compile time, so any value written to a database column, cached payload, configuration file, message contract, or log by a 6.x build carries the old number. Non-zero values can be remapped with the table above, but legacy zeroes must be audited before migration because `0` could mean either a deliberate `Allow`/`Low` assignment or an unset/default value. Where that distinction cannot be recovered, reject the old zero or re-derive the value from the original member name or source input instead of blindly promoting it.

Storage that persisted the member *name* rather than its number needs no remapping.

**Configuration that binds these enums by number must be updated.** Configuration that binds by name is unaffected:

```jsonc
{
  // Unaffected: bound by name.
  "HighRiskBehavior": "Deny",

  // Must change: 2 meant Deny in 6.x and means Defer in 7.0.
  "MediumRiskBehavior": 3
}
```

Prefer binding by name, which is stable across future renumbering.

### Unspecified is rejected, not defaulted

Supplying `Unspecified` raises `ArgumentOutOfRangeException` at each boundary rather than resolving to a behavior:

| Boundary | Rejects |
| --- | --- |
| `DlpFailurePolicyContext.Create` | An unassigned `DlpIntentRiskLevel`. |
| `DlpFailurePolicyOptions.GetBehavior` | An unconfigured risk-tier behavior or `BehaviorOverrides` entry. |
| `DlpFailurePolicyResolution.Create` | An unresolved `DlpFailureBehavior`. |

An incomplete policy now fails loudly where it previously proceeded. Hosts that relied on an unset risk level being treated as `Low`, or an unset behavior being treated as `Allow`, must now assign those values explicitly. That is the intended effect of the change: the previous behavior was indistinguishable from a deliberate decision to allow.

## Other changes that affect hosts

### Gate acknowledged operations on `CanProceed`

`AcknowledgmentChallengeResult.Succeeded` is `true` whenever the response was handled, including when the actor explicitly declined, because a refusal is recorded as a `LiabilityHandshakeAcknowledgment` just like an acceptance. A host that gated the consequential operation on `Succeeded` therefore proceeded after a decline. Gate on the new `CanProceed` property instead, which is `true` only for a handled acceptance:

```csharp
AcknowledgmentChallengeResult result = challengeService.HandleResponse(challenge, currentActor, response);

if (result.CanProceed)
{
    // Revalidate authorization and current policy, then run the operation.
}
```

`Succeeded` keeps its meaning, so code that checked `Succeeded && Acknowledged` is already correct.

### Outbox drain and worker constructors take an optional clock

`GovernanceOutboxDrain` and `GovernanceOutboxDrainHostedService` each gained an optional trailing `TimeProvider? timeProvider = null` constructor parameter. Code that constructs them compiles unchanged, but the constructor signature changed, so rebuild any assembly compiled against 6.x. Hosts that register a `TimeProvider` now have it used for drain and claim-lease timestamps.

`GovernanceOutboxDrainWorkerOptions.RetryClock` still works. While it keeps its default, the worker reads the registered `TimeProvider`; a delegate you assign still takes precedence. Prefer registering a `TimeProvider`, which also drives the local-development signer's `SignedUtc`:

```csharp
builder.Services.AddSingleton(TimeProvider.System); // Or a fake clock in tests.
```

### Local-development signing options are captured at registration

`UseLocalDevelopmentSigning` and `LocalDevelopmentSigningService` now take a snapshot of the `LocalDevelopmentSigningOptions` you pass. Assigning to that instance afterward no longer changes the registered provider, and the options resolved from the container are a copy rather than your instance. Configure every option before registering. Code that asserted the resolved options were the same instance it registered must compare values instead.

A key size that passes validation but that the platform RSA provider cannot generate, such as `2049`, now raises `InvalidOperationException` instead of `CryptographicException`.

## Validation

Package validation continues to run against the `5.1.0` baseline so the complete `5.1.0`-to-`7.0.0` compatibility surface remains checked. The intentional enum value changes are recorded as exact `CP0011` suppressions in `src/AsiBackbone.Core/CompatibilitySuppressions.xml`; the exact suppressions for the reviewed 6.0 major-boundary changes remain in place and all other package compatibility checks remain enabled.

## Related documentation

* [API Compatibility and SemVer](api-compatibility-and-semver.md)
* [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
* [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
* [Upgrade from 5.x to 6.0](upgrade-500-to-600.md)
