# EF Core JSON Metadata Storage Strategy

This article records the current decision for outbox metadata storage in `AsiBackbone.EntityFrameworkCore`.

## Decision

For the current provider-neutral EF Core package, AsiBackbone keeps outbox metadata in explicit string-backed JSON columns:

- `MetadataJson`
- `EnvelopeMetadataJson`
- `EnvelopePayloadMetadataJson`

The package should **not** move these fields to native EF Core JSON aggregate or complex-type mapping in the current release line.

This is an intentional portability choice, not a rejection of native JSON support. EF Core supports JSON column mapping through `ToJson()` for aggregates and, in newer EF Core versions, complex types. SQL Server and Azure SQL also have newer JSON data-type support. Those features are valuable, but they are not the best default for the AsiBackbone EF Core package while the package is trying to stay provider-neutral across SQLite, SQL Server, PostgreSQL, and host-owned database strategies.

References for future review:

- [EF Core 7 JSON columns](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#json-columns)
- [EF Core 10 JSON type support and complex-type JSON mapping](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew#json-type-support)

## Why manual string JSON remains the default

The outbox metadata fields are intentionally small, minimized, provider-neutral dictionaries. Their main job is to preserve safe diagnostic context and stable identifiers, not to become a provider-specific document database surface.

Manual string JSON remains the chosen baseline because it gives the package:

| Concern | Current string JSON behavior |
| --- | --- |
| Provider portability | Works with SQLite, SQL Server, PostgreSQL, MySQL/MariaDB, and other EF Core relational providers as ordinary text columns. |
| Host-owned migrations | Does not force a provider-specific JSON column type, generated migration, or compatibility level. |
| Stable package model | Keeps the public persistence shape simple and backward-compatible. |
| Metadata flexibility | Allows unknown future metadata keys to round-trip as dictionary entries. |
| Safe default querying | Encourages hosts to query stable first-class columns such as `EnvelopeId`, `EnvelopeCorrelationId`, `EnvelopeAuditResidueId`, `EnvelopePolicyVersion`, `EnvelopePolicyHash`, `EnvelopeTraceId`, status, and timestamps. |
| Native JSON optionality | Allows provider-specific hosts to add computed columns, JSON indexes, check constraints, generated columns, or custom migrations without changing the package contract. |

## Why native EF Core JSON mapping is not adopted yet

Native JSON mapping is most useful when an application owns a structured aggregate or complex type and expects to query into that document with provider translation.

The outbox metadata fields are different:

- they are `IReadOnlyDictionary<string, string>` surfaces, not strongly typed domain aggregates;
- they are intentionally minimized and open-ended;
- the most important query dimensions already have first-class indexed columns;
- provider JSON support differs by provider, version, compatibility level, and migration strategy;
- switching column mapping could create migration churn for hosts that already adopted the EF Core package;
- moving to JSON mapping would not by itself improve the outbox delivery semantics, retry semantics, or idempotency semantics.

For those reasons, native JSON mapping should be treated as an opt-in future enhancement or provider-specific extension, not a silent change to the current provider-neutral model.

## Current persistence shape

`EfCoreGovernanceOutboxStore` serializes minimized dictionaries into string JSON when saving an entry and deserializes them back into read-only dictionaries when loading an entry.

```text
GovernanceOutboxEntry.Metadata
  -> MetadataJson

GovernanceEmissionEnvelope.Metadata
  -> EnvelopeMetadataJson

GovernanceEmissionPayload.Metadata
  -> EnvelopePayloadMetadataJson
```

Empty or missing metadata should be represented to callers as an empty dictionary. Unknown future metadata keys should be preserved as ordinary key/value pairs when the JSON shape remains a string dictionary.

## Query guidance

Do not query governance behavior by parsing arbitrary metadata JSON as the first choice.

Prefer first-class columns for operational, compliance, and dashboard queries:

- `OutboxEntryId`
- `Status`
- `CreatedUtc`
- `UpdatedUtc`
- `DeliveredUtc`
- `NextRetryUtc`
- `EnvelopeId`
- `EnvelopeEventId`
- `EnvelopeCorrelationId`
- `EnvelopeAuditResidueId`
- `EnvelopePolicyVersion`
- `EnvelopePolicyHash`
- `EnvelopeTraceId`
- `EnvelopeOutboxSequence`
- `ProviderName`
- `ProviderRecordId`
- `LastErrorCode`

When a host needs provider-native JSON filtering, it may add host-owned migrations or database-specific projections without changing the package model. Examples include computed columns, generated columns, JSON indexes, materialized views, or provider-specific SQL in the host application.

## Migration guidance

The current package does not require a migration from string JSON columns to native JSON columns.

Hosts should not expect AsiBackbone to silently change these columns to provider-native JSON types. If a future release adds native JSON support, it should be additive and explicit, with migration guidance that explains:

- supported EF Core versions;
- supported database providers and provider versions;
- compatibility-level requirements where applicable;
- schema diff implications;
- downgrade/rollback behavior;
- whether the existing string-backed model remains supported.

## Future adoption criteria

Native JSON mapping may be reconsidered when there is a concrete package need that outweighs the portability cost.

A future issue should answer:

1. Which metadata structure is strongly typed enough to justify JSON aggregate or complex-type mapping?
2. Which database providers are officially supported by the package-level feature?
3. What happens for SQLite and other providers used in tests, templates, and samples?
4. What migrations are required for existing hosts?
5. Which JSON paths must be queryable, and why are existing indexed columns insufficient?
6. How will unknown future metadata keys be preserved?
7. How will round-trip behavior be tested across provider versions?

Until those questions are settled, string-backed JSON is the safer default.

## Related documentation

- [EF Core Integration Boundary](ef-core-integration-boundary.md)
- [EF Core Host Ownership and Migrations](ef-core-host-ownership-and-migrations.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Governance Outbox Delivery Semantics](governance-outbox-delivery-semantics.md)
- [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md)
