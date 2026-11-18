# Schema Versioning

AsiBackbone treats persisted or exported governance artifacts as durable contracts once a host application stores them, exports them, or exposes them to downstream consumers. Those artifacts should carry an explicit schema version so future package releases can add fields, rename shapes through migration adapters, or support parallel readers without guessing which serialized form produced the record.

## Current stable artifact schema

The initial stable artifact schema version is `1.0.0` and is exposed through `GovernanceSchemaVersions.StableArtifactsV1`.

Package-owned stable serialized artifacts that carry this version include:

- `AuditLedgerRecord`
- `AcknowledgmentRequest`
- `AcknowledgmentResponse`

The EF Core persistence entities for audit ledger records, handshake requests, and handshake acknowledgments also expose a `SchemaVersion` column so host-owned migrations can preserve the artifact shape that produced each durable row.

## Default behavior

When an `AuditLedgerRecord`, `AcknowledgmentRequest`, or `AcknowledgmentResponse` is created without an explicit schema version, the package defaults the record to `GovernanceSchemaVersions.StableArtifactsV1`.

Hosts or future package components may provide an explicit schema version when replaying, importing, migrating, or testing alternate artifact shapes. The value is trimmed and preserved.

## Capability grant note

A `CapabilityGrant` carries its own `SchemaVersion`, normalized like other governance artifacts, so signed grants can be migrated and verified independently of the records that reference them. Decision receipts reference a grant through `CapabilityGrantId`; audit ledger records retain the `CapabilityTokenId` identifier name from earlier releases. Bounded-use enforcement is persisted separately: `AsiBackbone.EntityFrameworkCore` provides `EfCoreCapabilityGrantUseStore`, which records grant uses in the host-owned `AsiBackboneCapabilityGrantUses` table. These grant and use-record shapes were added after the initial `1.0.0` schema version; see [Signed Capability Grants](signed-capability-grants.md) and [Capability Grant Hardening](capability-grant-hardening.md).

## Migration expectation

Schema versioning is not the same as provider-specific emission or external telemetry integration. It is the local artifact contract used by hosts to understand persisted or exported records.

When a future release changes a durable artifact shape, the release should:

1. define a new schema version constant,
2. keep readers tolerant of older schema versions where practical,
3. document migration behavior before changing stable payload expectations, and
4. avoid relying on package version alone to infer the serialized shape.

## Host guidance

Host applications that persist or export governance decisions, receipts, audit records, or acknowledgment records should include a schema/version field in any host-defined durable payloads. If the host wraps AsiBackbone records in a larger envelope, the host envelope may have its own version, but it should preserve the underlying artifact schema version as well.

This keeps decision receipts and other governance artifacts migratable across releases without pulling provider-specific emission work into the core package line.
