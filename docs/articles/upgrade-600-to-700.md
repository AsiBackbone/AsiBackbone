# Upgrade from 6.x to 7.0

Version 7.0 is a major release with five groups of breaking changes:

* **Security corrections that change stable contracts.** It binds acknowledgment responses to the actor the challenge was issued to, binds capability grants to the expected subject and operation, and moves `DlpFailureBehavior` and `DlpIntentRiskLevel` off their permissive zero values, which changes the numeric value of every existing member of both enums.
* **Completion of the 6.0 naming work.** The compatibility names that 6.0 retained (`AuditResidue*`, `LiabilityHandshake*`, `Handshake*`, and `CapabilityToken*`) are renamed to the current vocabulary: decision receipt, acknowledgment, and capability grant. Two namespaces change with them. No `[Obsolete]` forwarding aliases are provided.
* **An EF Core schema change.** Five columns are renamed to match the new property names. Hosts that use `AsiBackbone.EntityFrameworkCore` must add and review a migration before deploying 7.0.
* **A JSON property name change.** Types serialized with `System.Text.Json` now emit the renamed property names, so `auditResidueId` becomes `decisionReceiptId`. Consumers that parse or store this JSON must update. See [Decision receipt JSON uses `decisionReceiptId`](#decision-receipt-json-uses-decisionreceiptid).

Signed and telemetry contracts are not renamed. Canonical artifact tags, signed payload bytes, OpenTelemetry event and attribute names, EF Core table names, reason codes, diagnostic IDs, and the `AddAsiBackbone*` registration methods keep their 6.x values, so artifacts signed by 6.x verify under 7.0 and existing dashboards keep working. See [What does not change](#what-does-not-change).

Two public constructors were also replaced: `GovernanceOutboxDrain` and `GovernanceOutboxDrainHostedService` each gained an optional trailing `TimeProvider` parameter. All assemblies compiled against 6.x must be rebuilt against 7.0, and most consumers will need source changes for the renames. [Other changes that affect hosts](#other-changes-that-affect-hosts) covers the remaining adjustments.

## Why these changes required a major release

The repository's [API compatibility and SemVer contract](api-compatibility-and-semver.md) treats a change to a public enum value, and the renaming or removal of a public type, member, or namespace, as affecting a stable package contract. The `6.x` line also pins `AssemblyVersion` at `6.0.0.0` for every compatible release, so a consumer compiled against `6.0.0` binds the same assembly identity regardless of package version. Shipping the enum renumbering on `6.x` would have let that consumer load a library that reinterprets the constants the compiler already inlined into its own assembly, with nothing in the assembly identity to signal the change. `7.0.0` advances `AssemblyVersion` to `7.0.0.0`, so the boundary is explicit.

The 6.0 naming record ([Public API Naming in 6.0](public-api-naming-600.md)) planned to deprecate the retained compatibility names during `6.x` and remove them at the next major version. Because 7.0 is that major version, the names are renamed directly rather than passing through a deprecation release.

## Acknowledgment responses are bound to the challenged actor

`IAcknowledgmentChallengeService.HandleResponse` now verifies that the responding actor is the actor the challenge was issued to before it produces an `AcknowledgmentResponse`.

In 6.x the response was validated only against the handshake identifier and the required acknowledgment code. Any actor that could name an active challenge could satisfy a challenge issued to a different actor, and the resulting acknowledgment recorded whichever actor answered. For a package whose purpose is attributing accountability, that made the acknowledgment unreliable as evidence.

In 7.0 the `actor` argument must match both the `ActorId` and the `ActorType` that `CreateChallenge` recorded. `ActorId` is compared ordinally after trimming; `ActorType` must be equal, because the same identifier under a different actor type is a different principal. A mismatch returns a failed result carrying the new `acknowledgment.challenge.actor_mismatch` reason code, and no acknowledgment is created. A single reason code covers both comparisons so a caller cannot use the failure to determine which component differed.

The challenged and responding actors must also be known and authenticated, must not use `GovernanceActorType.Unknown`,
and must not use the shared `GovernanceActorContext.UnknownActorId` (`"unknown"`) identifier. `CreateChallenge` rejects
an insufficient binding, while `HandleResponse` returns a failed result carrying
`acknowledgment.challenge.actor_unbound`. Default endpoint governance returns a coded `403` and does not issue a
challenge. This prevents two unrelated anonymous requests, which the default ASP.NET Core resolver deliberately maps to
the same sentinel identity, from satisfying each other's challenges.

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

The default `HttpContextGovernanceActorContextResolver` does not support acknowledgment challenges for anonymous
requests. Setting `UnauthenticatedDisplayName` changes only presentation; the actor remains unauthenticated and retains
the shared `"unknown"` identifier. A host that intentionally establishes a safer non-default binding must provide a
distinct, known, authenticated actor context through its actor resolver and document how that binding resists
cross-request impersonation. Do not derive the binding from user-controlled request data.

Handle the new reason code where response failures are surfaced:

| Reason code | Meaning |
| --- | --- |
| `acknowledgment.challenge.mismatch` | The response did not name the active challenge. |
| `acknowledgment.challenge.actor_unbound` | Challenge creation or response lacked a distinct, known, authenticated actor binding. |
| `acknowledgment.challenge.actor_mismatch` | The response was submitted by an actor other than the challenged actor. |
| `acknowledgment.challenge.code_mismatch` | The response did not carry the required acknowledgment code. |

### What this change does not do

Actor identity, challenge expiry, single-use enforcement, authorization, and current-policy validation are separate
controls. The known/authenticated actor requirement prevents the shared anonymous sentinel from acting as an identity;
it does not establish challenge lifetime or consume a challenge. `AcknowledgmentChallenge` still carries no expiry, and
nothing consumes a challenge when it is used, so a stored response payload remains replayable. Bounded-lifetime
challenge state, revalidating authorization, and revalidating current policy before the consequential operation all
remain host responsibilities. See [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md).

## Capability grants are bound to the expected subject and operation

`CapabilityGrantValidator` now compares the signed grant's normalized
`SubjectId` and `OperationName` with host-supplied expectations. A mismatch
fails closed as `capability.subject-mismatch` or
`capability.operation-mismatch` rather than allowing a valid grant issued for a
different subject or operation to authorize execution.

Use `CapabilityGrantValidationOptions.CreateBoundExecutionBoundary` and supply
`CapabilityGrantBindingExpectations` from authenticated host context. The
subject expectation is required; provide the requested operation whenever the
grant is operation-bound. Do not derive either value from the grant itself.

The retained `CreateExecutionBoundary` factory is marked obsolete with warning
`ASIB901` and throws instead of constructing an unbound execution profile. It
remains only for binary compatibility and is scheduled for removal at the next
permitted major version. Reduced validation paths created with `Create` or
`CreateMetadataValidation` may opt into the same checks with
`WithExpectedBindings`.

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

## Legacy compatibility names are renamed

6.0 renamed most of the public surface to the current vocabulary but kept a set of names for compatibility. 7.0 renames those as well. The renames are mechanical: behavior and validation are unchanged, and signed contract values keep their 6.x form. JSON produced by serializing these types uses the new property names; see [Decision receipt JSON uses `decisionReceiptId`](#decision-receipt-json-uses-decisionreceiptid).

### Namespaces

| 6.x namespace | 7.0 namespace |
| --- | --- |
| `AsiBackbone.Core.Handshakes` | `AsiBackbone.Core.Acknowledgments` |
| `AsiBackbone.AspNetCore.Handshakes` | `AsiBackbone.AspNetCore.Acknowledgments` |
| `AsiBackbone.Core.CapabilityTokens` | `AsiBackbone.Core.CapabilityGrants` |
| `AsiBackbone.Storage.InMemory.CapabilityTokens` | `AsiBackbone.Storage.InMemory.CapabilityGrants` |

Update `using` directives and fully qualified references. Types that stay in these namespaces without being renamed, such as `AcknowledgmentChallenge` and `AcknowledgmentChallengeResult`, still need the new `using`.

### Types

| 6.x type | 7.0 type |
| --- | --- |
| `LiabilityHandshakeRequest` | `AcknowledgmentRequest` |
| `LiabilityHandshakeAcknowledgment` | `AcknowledgmentResponse` |
| `LiabilityHandshakeRiskLevel` | `AcknowledgmentRiskLevel` |
| `RequireLiabilityHandshakeAttribute` (`[RequireLiabilityHandshake]`) | `RequireAcknowledgmentAttribute` (`[RequireAcknowledgment]`) |
| `IEndpointLiabilityHandshakeMetadata` | `IEndpointAcknowledgmentMetadata` |
| `CapabilityTokenGrant` | `CapabilityGrant` |
| `CapabilityTokenValidationCategory` | `CapabilityGrantValidationCategory` |
| `HandshakeRequestEntity` | `AcknowledgmentRequestEntity` |
| `HandshakeRequestMetadataEntity` | `AcknowledgmentRequestMetadataEntity` |
| `HandshakeAcknowledgmentEntity` | `AcknowledgmentResponseEntity` |
| `HandshakeAcknowledgmentMetadataEntity` | `AcknowledgmentResponseMetadataEntity` |

Other types whose names contained `LiabilityHandshake`, `HandshakeRequest`, `HandshakeAcknowledgment`, or `CapabilityTokenGrant` follow the same pattern, for example `HandshakeRequestEntityConfiguration` becomes `AcknowledgmentRequestEntityConfiguration`.

### Members and constants

| 6.x member | 7.0 member |
| --- | --- |
| `AuditResidueId` (on lifecycle events, envelopes, and ledger records) | `DecisionReceiptId` |
| `DecisionReceiptBuilder.WithAuditResidueId` | `DecisionReceiptBuilder.WithDecisionReceiptId` |
| `FindByAuditResidueIdAsync` | `FindByDecisionReceiptIdAsync` |
| `CanonicalArtifactTypes.AuditResidue` | `CanonicalArtifactTypes.DecisionReceipt` |
| `CanonicalArtifactTypes.AuditResidueLifecycleEvent` | `CanonicalArtifactTypes.DecisionReceiptLifecycleEvent` |
| `GovernanceEmissionEventType.AuditResidue` | `GovernanceEmissionEventType.DecisionReceipt` |
| `OpenTelemetryGovernanceInstrumentation.AuditResidueCreatedEventName` | `OpenTelemetryGovernanceInstrumentation.DecisionReceiptCreatedEventName` |
| `OpenTelemetryGovernanceAttributes.AuditResidueId` | `OpenTelemetryGovernanceAttributes.DecisionReceiptId` |
| `CanonicalPayloadBuilder.ForCapabilityTokenGrant` | `CanonicalPayloadBuilder.ForCapabilityGrant` |
| `EnvelopeAuditResidueId` (outbox entity) | `EnvelopeDecisionReceiptId` |
| `HandshakeRequestId` (request metadata entity) | `AcknowledgmentRequestId` |
| `HandshakeAcknowledgmentId` (acknowledgment metadata entity) | `AcknowledgmentResponseId` |

Parameters named `auditResidueId` are renamed to `decisionReceiptId`. Callers that pass that argument by name must update the argument name.

### What to change

1. Update `using` directives and replace the old names using the tables above. The compiler reports every remaining reference.
2. Search for old names that the compiler cannot see: string literals, reflection, `Type.GetType` calls, configuration keys, log queries, and test assertions that name these types or members.
3. `GovernanceEmissionEventType.DecisionReceipt` keeps the numeric value `500`. If you persist or transmit this enum by **name** outside canonical signing payloads, stored `"AuditResidue"` values must be mapped to `"DecisionReceipt"`. Values stored by number need no change. Canonical v1 is the deliberate exception: its stable wire value remains `"AuditResidue"`, as described below.
4. If your host serializes decision receipts, ledger records, or emission envelopes to JSON, follow [Decision receipt JSON uses `decisionReceiptId`](#decision-receipt-json-uses-decisionreceiptid).
5. If your host maps AsiBackbone entities into its own `DbContext`, follow [EF Core schema changes](#ef-core-schema-changes-migration-required) before deploying.

### What does not change

These values are wire, signature, or persistence contracts and keep their 6.x values in 7.0:

| Contract | Value kept |
| --- | --- |
| Decision receipt artifact tag | `asibackbone.audit-residue` |
| Decision receipt lifecycle artifact tag | `asibackbone.audit-residue-lifecycle-event` |
| OpenTelemetry event name | `asibackbone.audit_residue.created` |
| `GovernanceEmissionEventType.DecisionReceipt` numeric value | `500` |
| Canonical v1 `eventType` for value `500` | `AuditResidue` |
| EF Core table names | `AsiBackboneAuditResidueLifecycleEvents`, `AsiBackboneHandshake*`, `AsiBackboneAuditLedger*`, `AsiBackboneGovernanceOutboxEntries` |
| EF Core `HandshakeId` and `CapabilityTokenId` columns | Unchanged |
| Diagnostic IDs | `ASIB*` |
| Registration methods | `AddAsiBackbone*` |

Canonical payload bytes are unchanged, so artifacts signed under 6.x verify under 7.0 without re-signing. Canonical v1
does not derive enum wire strings from mutable CLR member names. The complete inventory of enum-derived signed fields is:

| Signed field | Enum family | Canonical v1 wire values |
| --- | --- | --- |
| `actorType` | `GovernanceActorType` | Explicit 6.0 member names |
| `stage`, `lifecycleStage`, lifecycle-derived `decisionStage` | `DecisionReceiptLifecycleStage` | Explicit 6.0 member names |
| `eventType` | `GovernanceEmissionEventType` | Explicit 6.0 member names, including `AuditResidue` for value `500` |
| `status` | `GovernanceEmissionStatus` | Explicit 6.0 member names |
| Typed receipt `outcome` | `GovernanceDecisionOutcome` | Explicit 6.0 member names, including ASP.NET correlation helpers |
| Typed receipt `outcome` | `ConstraintEvaluationOutcome` | Explicit 6.0 member names |
| `persistenceOutcome`, execution-receipt lifecycle `outcome` | `GovernedOperationPersistenceOutcome` | Explicit 6.0 member names, including lifecycle metadata |

These strings are signature protocol constants even when a public CLR member is renamed. This guarantee applies to
payloads built with the shipped canonical builders; ordinary JSON serialization of the enum by name follows the current
CLR member name. `HandshakeId` keeps its name because it identifies the acknowledgment handshake protocol itself, which
the [6.0 terminology guidance](terminology-600.md) permits.

## Decision receipt JSON uses `decisionReceiptId`

`System.Text.Json` names JSON properties after the C# property names. Because `AuditResidueId` is renamed to `DecisionReceiptId`, JSON produced by serializing a public type that exposes that property changes with it. This includes `DecisionReceipt`, `AuditLedgerRecord`, and `GovernanceEmissionEnvelope`.

| Serializer options | 6.x key | 7.0 key |
| --- | --- | --- |
| `JsonSerializerDefaults.Web` or a camel-case naming policy | `auditResidueId` | `decisionReceiptId` |
| Default options | `AuditResidueId` | `DecisionReceiptId` |

This does not affect signatures. Canonical signed payloads are built with explicit key names, not by serializing these types, so their bytes are unchanged.

### What to change

1. Update log queries, dashboards, SIEM rules, message consumers, and stored-document queries that read `auditResidueId` to read `decisionReceiptId`.
2. **Migrate or translate stored 6.x JSON before deserializing it with 7.0.** By default, `System.Text.Json` ignores members it does not recognize, so a 6.x document deserialized into a 7.0 type comes back with `DecisionReceiptId` set to `null` and no error. If your options set `UnmappedMemberHandling.Disallow`, deserialization throws instead.

   Rename the key in stored documents, or translate each document as you read it:

   ```csharp
   JsonObject node = JsonNode.Parse(storedJson)!.AsObject();
   if (node.Remove("auditResidueId", out JsonNode? value))
   {
       node["decisionReceiptId"] = value;
   }
   ```

3. Producers and consumers that exchange this JSON should upgrade together, or consumers should accept both keys during the transition.

## EF Core schema changes (migration required)

Five columns in the `AsiBackbone.EntityFrameworkCore` model are renamed to match the renamed properties. EF Core renames the dependent indexes and foreign keys with them. Table names do not change.

| Table | 6.x column | 7.0 column | Also renamed |
| --- | --- | --- | --- |
| `AsiBackboneAuditLedgerRecords` | `AuditResidueId` | `DecisionReceiptId` | — |
| `AsiBackboneAuditResidueLifecycleEvents` | `AuditResidueId` | `DecisionReceiptId` | 2 indexes |
| `AsiBackboneGovernanceOutboxEntries` | `EnvelopeAuditResidueId` | `EnvelopeDecisionReceiptId` | — |
| `AsiBackboneHandshakeRequestMetadata` | `HandshakeRequestId` | `AcknowledgmentRequestId` | 2 indexes, 1 foreign key |
| `AsiBackboneHandshakeAcknowledgmentMetadata` | `HandshakeAcknowledgmentId` | `AcknowledgmentResponseId` | 2 indexes, 1 foreign key |

Hosts own their migrations, so each host that maps these entities must add one.

### What to change

1. After updating the package references, add a migration in the host project:

   ```bash
   dotnet ef migrations add AsiBackbone700
   ```

2. **Review the generated migration before applying it.** EF Core can interpret a renamed property as a dropped column plus a new column. If the migration contains `DropColumn` or `AddColumn` for any of the five columns above, applying it deletes the existing decision receipt identifiers, acknowledgment metadata links, and the receipt identifiers of outbox entries that have not yet been drained. Replace those operations with the `RenameColumn` operations below.
3. Apply the migration to a copy of production data first, and confirm that the row counts in the five tables are unchanged.

### Reference migration

The generated migration should be equivalent to the following. If your host uses a schema other than the default, add the `schema:` argument to each operation.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropForeignKey(
        name: "FK_AsiBackboneHandshakeRequestMetadata_AsiBackboneHandshakeRequests_HandshakeRequestId",
        table: "AsiBackboneHandshakeRequestMetadata");
    migrationBuilder.DropForeignKey(
        name: "FK_AsiBackboneHandshakeAcknowledgmentMetadata_AsiBackboneHandshakeAcknowledgments_HandshakeAcknowledgmentId",
        table: "AsiBackboneHandshakeAcknowledgmentMetadata");

    migrationBuilder.RenameColumn(name: "AuditResidueId", table: "AsiBackboneAuditLedgerRecords", newName: "DecisionReceiptId");
    migrationBuilder.RenameColumn(name: "AuditResidueId", table: "AsiBackboneAuditResidueLifecycleEvents", newName: "DecisionReceiptId");
    migrationBuilder.RenameColumn(name: "EnvelopeAuditResidueId", table: "AsiBackboneGovernanceOutboxEntries", newName: "EnvelopeDecisionReceiptId");
    migrationBuilder.RenameColumn(name: "HandshakeRequestId", table: "AsiBackboneHandshakeRequestMetadata", newName: "AcknowledgmentRequestId");
    migrationBuilder.RenameColumn(name: "HandshakeAcknowledgmentId", table: "AsiBackboneHandshakeAcknowledgmentMetadata", newName: "AcknowledgmentResponseId");

    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneAuditResidueLifecycleEvents_AuditResidueId",
        table: "AsiBackboneAuditResidueLifecycleEvents",
        newName: "IX_AsiBackboneAuditResidueLifecycleEvents_DecisionReceiptId");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneAuditResidueLifecycleEvents_AuditResidueId_OccurredUtc",
        table: "AsiBackboneAuditResidueLifecycleEvents",
        newName: "IX_AsiBackboneAuditResidueLifecycleEvents_DecisionReceiptId_OccurredUtc");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneHandshakeRequestMetadata_HandshakeRequestId",
        table: "AsiBackboneHandshakeRequestMetadata",
        newName: "IX_AsiBackboneHandshakeRequestMetadata_AcknowledgmentRequestId");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneHandshakeRequestMetadata_HandshakeRequestId_MetadataKey",
        table: "AsiBackboneHandshakeRequestMetadata",
        newName: "IX_AsiBackboneHandshakeRequestMetadata_AcknowledgmentRequestId_MetadataKey");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneHandshakeAcknowledgmentMetadata_HandshakeAcknowledgmentId",
        table: "AsiBackboneHandshakeAcknowledgmentMetadata",
        newName: "IX_AsiBackboneHandshakeAcknowledgmentMetadata_AcknowledgmentResponseId");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneHandshakeAcknowledgmentMetadata_HandshakeAcknowledgmentId_MetadataKey",
        table: "AsiBackboneHandshakeAcknowledgmentMetadata",
        newName: "IX_AsiBackboneHandshakeAcknowledgmentMetadata_AcknowledgmentResponseId_MetadataKey");

    migrationBuilder.AddForeignKey(
        name: "FK_AsiBackboneHandshakeRequestMetadata_AsiBackboneHandshakeRequests_AcknowledgmentRequestId",
        table: "AsiBackboneHandshakeRequestMetadata",
        column: "AcknowledgmentRequestId",
        principalTable: "AsiBackboneHandshakeRequests",
        principalColumn: "Id",
        onDelete: ReferentialAction.Cascade);
    migrationBuilder.AddForeignKey(
        name: "FK_AsiBackboneHandshakeAcknowledgmentMetadata_AsiBackboneHandshakeAcknowledgments_AcknowledgmentResponseId",
        table: "AsiBackboneHandshakeAcknowledgmentMetadata",
        column: "AcknowledgmentResponseId",
        principalTable: "AsiBackboneHandshakeAcknowledgments",
        principalColumn: "Id",
        onDelete: ReferentialAction.Cascade);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropForeignKey(
        name: "FK_AsiBackboneHandshakeRequestMetadata_AsiBackboneHandshakeRequests_AcknowledgmentRequestId",
        table: "AsiBackboneHandshakeRequestMetadata");
    migrationBuilder.DropForeignKey(
        name: "FK_AsiBackboneHandshakeAcknowledgmentMetadata_AsiBackboneHandshakeAcknowledgments_AcknowledgmentResponseId",
        table: "AsiBackboneHandshakeAcknowledgmentMetadata");

    migrationBuilder.RenameColumn(name: "DecisionReceiptId", table: "AsiBackboneAuditLedgerRecords", newName: "AuditResidueId");
    migrationBuilder.RenameColumn(name: "DecisionReceiptId", table: "AsiBackboneAuditResidueLifecycleEvents", newName: "AuditResidueId");
    migrationBuilder.RenameColumn(name: "EnvelopeDecisionReceiptId", table: "AsiBackboneGovernanceOutboxEntries", newName: "EnvelopeAuditResidueId");
    migrationBuilder.RenameColumn(name: "AcknowledgmentRequestId", table: "AsiBackboneHandshakeRequestMetadata", newName: "HandshakeRequestId");
    migrationBuilder.RenameColumn(name: "AcknowledgmentResponseId", table: "AsiBackboneHandshakeAcknowledgmentMetadata", newName: "HandshakeAcknowledgmentId");

    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneAuditResidueLifecycleEvents_DecisionReceiptId",
        table: "AsiBackboneAuditResidueLifecycleEvents",
        newName: "IX_AsiBackboneAuditResidueLifecycleEvents_AuditResidueId");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneAuditResidueLifecycleEvents_DecisionReceiptId_OccurredUtc",
        table: "AsiBackboneAuditResidueLifecycleEvents",
        newName: "IX_AsiBackboneAuditResidueLifecycleEvents_AuditResidueId_OccurredUtc");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneHandshakeRequestMetadata_AcknowledgmentRequestId",
        table: "AsiBackboneHandshakeRequestMetadata",
        newName: "IX_AsiBackboneHandshakeRequestMetadata_HandshakeRequestId");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneHandshakeRequestMetadata_AcknowledgmentRequestId_MetadataKey",
        table: "AsiBackboneHandshakeRequestMetadata",
        newName: "IX_AsiBackboneHandshakeRequestMetadata_HandshakeRequestId_MetadataKey");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneHandshakeAcknowledgmentMetadata_AcknowledgmentResponseId",
        table: "AsiBackboneHandshakeAcknowledgmentMetadata",
        newName: "IX_AsiBackboneHandshakeAcknowledgmentMetadata_HandshakeAcknowledgmentId");
    migrationBuilder.RenameIndex(
        name: "IX_AsiBackboneHandshakeAcknowledgmentMetadata_AcknowledgmentResponseId_MetadataKey",
        table: "AsiBackboneHandshakeAcknowledgmentMetadata",
        newName: "IX_AsiBackboneHandshakeAcknowledgmentMetadata_HandshakeAcknowledgmentId_MetadataKey");

    migrationBuilder.AddForeignKey(
        name: "FK_AsiBackboneHandshakeRequestMetadata_AsiBackboneHandshakeRequests_HandshakeRequestId",
        table: "AsiBackboneHandshakeRequestMetadata",
        column: "HandshakeRequestId",
        principalTable: "AsiBackboneHandshakeRequests",
        principalColumn: "Id",
        onDelete: ReferentialAction.Cascade);
    migrationBuilder.AddForeignKey(
        name: "FK_AsiBackboneHandshakeAcknowledgmentMetadata_AsiBackboneHandshakeAcknowledgments_HandshakeAcknowledgmentId",
        table: "AsiBackboneHandshakeAcknowledgmentMetadata",
        column: "HandshakeAcknowledgmentId",
        principalTable: "AsiBackboneHandshakeAcknowledgments",
        principalColumn: "Id",
        onDelete: ReferentialAction.Cascade);
}
```

On SQLite, EF Core applies the foreign key changes by rebuilding the affected tables. The data is copied during the rebuild, but review the generated SQL (`dotnet ef migrations script`) before running it against a large database.

## Other changes that affect hosts

### Gate acknowledged operations on `CanProceed`

`AcknowledgmentChallengeResult.Succeeded` is `true` whenever the response was handled, including when the actor explicitly declined, because a refusal is recorded as an `AcknowledgmentResponse` just like an acceptance. A host that gated the consequential operation on `Succeeded` therefore proceeded after a decline. Gate on the new `CanProceed` property instead, which is `true` only for a handled acceptance:

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

Package validation continues to run against the `5.1.0` baseline so the complete `5.1.0`-to-`7.0.0` compatibility surface remains checked. The intentional enum value changes and the type, member, and namespace renames are recorded as exact suppressions in each affected package's `CompatibilitySuppressions.xml`; the exact suppressions for the reviewed 6.0 major-boundary changes remain in place, and all other package compatibility checks remain enabled.

## Related documentation

* [API Compatibility and SemVer](api-compatibility-and-semver.md)
* [Public API Naming in 6.0](public-api-naming-600.md)
* [Terminology in 6.0](terminology-600.md)
* [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
* [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
* [Upgrade from 5.x to 6.0](upgrade-500-to-600.md)
