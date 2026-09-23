# AsiBackbone 7.0.0 Release Notes

Release date: 2026-09-20

## Summary

`7.0.0` is a major release for the AsiBackbone package family. It carries two
security corrections that change stable contracts, completes the 6.0 naming work
by renaming the compatibility names that 6.0 retained, and renames five Entity
Framework Core columns and one JSON property name to match.

Package IDs and the `net10.0` target remain unchanged. Four namespaces and the
retained `AuditResidue*`, `LiabilityHandshake*`, `Handshake*`, and
`CapabilityToken*` types and members are renamed, with no `[Obsolete]`
forwarding aliases. Signed and telemetry contracts keep their `6.x` values:
canonical artifact tags, signed payload bytes, OpenTelemetry event and attribute
names, EF Core table names, reason codes, diagnostic IDs, and the
`AddAsiBackbone*` registration methods are unchanged. JSON produced by
serializing the renamed types uses the new property names, so `auditResidueId`
becomes `decisionReceiptId`.

The `GovernanceOutboxDrain` and `GovernanceOutboxDrainHostedService`
constructors are replaced by signatures with an optional trailing
`TimeProvider` parameter. Assemblies built against `6.x` must be rebuilt, and
most consumers need source changes for the renames. `AssemblyVersion` advances
to `7.0.0.0`; package and file versions advance to `7.0.0` and `7.0.0.0`
respectively.

Consumers moving from `6.x` must follow the
[Upgrade from 6.x to 7.0](upgrade-600-to-700.md) guide. Hosts that use
`AsiBackbone.EntityFrameworkCore` must add and review a database migration
before deploying, and hosts that store or parse decision receipt JSON must
update for the new key.

## Breaking changes

### Acknowledgment responses are bound to the challenged actor

`IAcknowledgmentChallengeService.HandleResponse` now verifies that the responding
actor is the actor the challenge was issued to before producing an
`AcknowledgmentResponse`.

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

### Retained 6.0 compatibility names are renamed

6.0 renamed most of the public surface but kept a set of names for
compatibility and planned to remove them at the next major version. 7.0 renames
them directly. The renames are mechanical: behavior and validation are
unchanged, and signed contract values keep their `6.x` form. JSON property
names follow the renamed properties, as described below.

| Area | `6.x` | `7.0` |
| --- | --- | --- |
| Namespaces | `AsiBackbone.Core.Handshakes`, `AsiBackbone.AspNetCore.Handshakes` | `AsiBackbone.Core.Acknowledgments`, `AsiBackbone.AspNetCore.Acknowledgments` |
| Namespaces | `AsiBackbone.Core.CapabilityTokens`, `AsiBackbone.Storage.InMemory.CapabilityTokens` | `AsiBackbone.Core.CapabilityGrants`, `AsiBackbone.Storage.InMemory.CapabilityGrants` |
| Acknowledgment types | `LiabilityHandshakeRequest`, `LiabilityHandshakeAcknowledgment`, `LiabilityHandshakeRiskLevel` | `AcknowledgmentRequest`, `AcknowledgmentResponse`, `AcknowledgmentRiskLevel` |
| Endpoint metadata | `RequireLiabilityHandshakeAttribute`, `IEndpointLiabilityHandshakeMetadata` | `RequireAcknowledgmentAttribute`, `IEndpointAcknowledgmentMetadata` |
| Capability grants | `CapabilityTokenGrant`, `CapabilityTokenValidationCategory` | `CapabilityGrant`, `CapabilityGrantValidationCategory` |
| Persistence entities | `HandshakeRequest*Entity`, `HandshakeAcknowledgment*Entity` | `AcknowledgmentRequest*Entity`, `AcknowledgmentResponse*Entity` |
| Decision receipt members | `AuditResidueId`, `WithAuditResidueId`, `FindByAuditResidueIdAsync`, `CanonicalArtifactTypes.AuditResidue`, `GovernanceEmissionEventType.AuditResidue`, `AuditResidueCreatedEventName` | `DecisionReceipt*` equivalents |

`GovernanceEmissionEventType.DecisionReceipt` keeps the numeric value `500`.
Consumers that persist that enum by name must map stored `"AuditResidue"`
values. See
[Legacy compatibility names are renamed](upgrade-600-to-700.md#legacy-compatibility-names-are-renamed)
for the complete inventory and the contracts that do not change.

Canonical signing payloads are a deliberate exception to that name migration.
Canonical v1 retains the 6.0 wire string `"AuditResidue"` for event type `500`,
so a 6.0 governance-emission payload reconstructs to the same bytes and hash
under 7.0. All enum-derived signed fields now use explicit stable mappings
rather than CLR `Enum.ToString()`: actor type, lifecycle stage, emission event
type, emission/outbox status, and governed-operation persistence outcome.

### Entity Framework Core columns are renamed

Five columns are renamed to match the renamed properties. The dependent indexes
and foreign keys are renamed with them; table names are unchanged.

| Table | `6.x` column | `7.0` column |
| --- | --- | --- |
| `AsiBackboneAuditLedgerRecords` | `AuditResidueId` | `DecisionReceiptId` |
| `AsiBackboneAuditResidueLifecycleEvents` | `AuditResidueId` | `DecisionReceiptId` |
| `AsiBackboneGovernanceOutboxEntries` | `EnvelopeAuditResidueId` | `EnvelopeDecisionReceiptId` |
| `AsiBackboneHandshakeRequestMetadata` | `HandshakeRequestId` | `AcknowledgmentRequestId` |
| `AsiBackboneHandshakeAcknowledgmentMetadata` | `HandshakeAcknowledgmentId` | `AcknowledgmentResponseId` |

Hosts own their migrations. Add one after upgrading and confirm that it renames
these columns rather than dropping and re-adding them, which would delete the
existing data. The
[reference migration](upgrade-600-to-700.md#ef-core-schema-changes-migration-required)
lists every operation for `Up` and `Down`.

### Decision receipt JSON uses `decisionReceiptId`

`System.Text.Json` names JSON properties after the C# property names, so JSON
produced by serializing `DecisionReceipt`, `AuditLedgerRecord`, or
`GovernanceEmissionEnvelope` now uses `decisionReceiptId` where `6.x` used
`auditResidueId` (`DecisionReceiptId` and `AuditResidueId` with default
serializer options). Canonical signed payloads use explicit key names and are
unaffected.

Consumers that read this JSON must use the new key. Stored `6.x` JSON
deserialized into a 7.0 type comes back with `DecisionReceiptId` set to `null`,
because unrecognized members are ignored by default, so stored documents must be
migrated or translated when read. See
[Decision receipt JSON uses `decisionReceiptId`](upgrade-600-to-700.md#decision-receipt-json-uses-decisionreceiptid).

## Additional changes

- **Fixed:** `EfCoreGovernanceOutboxStore` claim transitions no longer silently
  do nothing when the entry was enqueued through the same `DbContext`. Previously
  `MarkClaimDeliveredAsync` left such an entry `Pending`, so it could be
  re-claimed and emitted again after the lease expired.
- **Fixed:** `LocalDevelopmentSigningService` creates its key with
  `RSA.Create(int)` and reports a provider-unsupported key size as
  `InvalidOperationException`.
- **Added:** `AcknowledgmentChallengeResult.CanProceed`, which is `true` only
  for a handled acceptance. `Succeeded` is also `true` for a handled refusal.
- **Added:** `TimeProvider` support in `GovernanceOutboxDrain`,
  `GovernanceOutboxDrainHostedService`, and `LocalDevelopmentSigningService`,
  resolved from dependency injection when registered.
- **Changed:** local-development signing registration captures a snapshot of
  its options, and the outbox drain and worker constructors gained an optional
  `TimeProvider` parameter.

See [Other changes that affect hosts](upgrade-600-to-700.md#other-changes-that-affect-hosts)
for the migration details.

## Why this required a major release

The repository's
[API compatibility and SemVer contract](api-compatibility-and-semver.md) treats a
change to a public enum value, and the renaming of a public type, member, or
namespace, as affecting a stable package contract. The `6.x` line also pins
`AssemblyVersion` at `6.0.0.0` for every compatible release, so a consumer
compiled against `6.0.0` binds the same assembly identity regardless of package
version. Shipping the renumbering on `6.x` would have let that consumer load a
library that reinterprets the constants the compiler already inlined into its
own assembly, with nothing in the assembly identity to signal the change.

The [6.0 public API naming record](public-api-naming-600.md) planned to deprecate
the retained compatibility names during `6.x` and remove them at the next major
version. Because 7.0 is that version, they are renamed directly.

## Validation

Package validation continues to run against the `5.1.0` baseline so the complete
`5.1.0`-to-`7.0.0` compatibility surface remains checked. The intentional enum
value changes and the type, member, and namespace renames are recorded as exact
suppressions in each affected package's `CompatibilitySuppressions.xml`; all
other package compatibility checks remain enabled. The committed public API
baselines under `eng/api-baseline/` record the new enum values, the added
members, and the renamed surface. The EF Core model is checked so that the only
schema differences from `6.x` are the five documented column renames and their
dependent indexes and foreign keys.

## Related documentation

* [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
* [7.0.0 Release Readiness Record](release-readiness-700.md)
* [7.0.0 Consumer Verification Guide](consumer-verification-700.md)
* [API Compatibility and SemVer](api-compatibility-and-semver.md)
* [Public API Naming in 6.0](public-api-naming-600.md)
* [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
* [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
* [6.0.0 Release Notes](release-notes-600.md)
