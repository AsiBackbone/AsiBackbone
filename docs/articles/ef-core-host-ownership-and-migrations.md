# EF Core Host Ownership and Migration Guidance

`AsiBackbone.EntityFrameworkCore` contributes model configuration and persistence helpers for ASI Backbone accountability records. The host application owns the `DbContext`, database provider, connection string, migrations, schema deployment, and operational database lifecycle.

> [!IMPORTANT]
> ASI Backbone does not take over the host database. The EF Core package adds ASI Backbone persistence shapes to a host-owned EF Core model; it does not provide a required package-owned `AsiBackboneDbContext`, forced provider, forced connection string, or package-owned migration set.

## Ownership boundary

The integration boundary is intentionally simple:

```text
Host application
  owns DbContext
  owns database provider
  owns connection string
  owns migrations
  owns deployment workflow
  owns schema lifecycle
  owns backup, retention, encryption, and operational policy

ASI Backbone EF Core package
  contributes entity configurations
  contributes ModelBuilder extension methods
  contributes persistence entities and helpers
  preserves Core package independence from EF Core
```

This allows ASI Backbone accountability records to participate in the application database without forcing the application to adopt a specific infrastructure style.

## Minimal host-owned DbContext example

A host application integrates ASI Backbone persistence by applying the EF Core model configuration extension from its own `DbContext`.

```csharp
using AsiBackbone.EntityFrameworkCore;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AsiBackboneAuditLedgerRecordEntity> AsiBackboneAuditLedgerRecords =>
        Set<AsiBackboneAuditLedgerRecordEntity>();

    public DbSet<AsiBackboneGovernanceOutboxEntryEntity> AsiBackboneGovernanceOutboxEntries =>
        Set<AsiBackboneGovernanceOutboxEntryEntity>();

    public DbSet<AsiBackboneAuditResidueLifecycleEventEntity> AsiBackboneAuditResidueLifecycleEvents =>
        Set<AsiBackboneAuditResidueLifecycleEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyAsiBackboneConfigurations();
    }
}
```

The host may expose `DbSet<T>` properties for convenience, but the important integration point is the `ApplyAsiBackboneConfigurations()` call. That call adds the ASI Backbone persistence entities and mappings to the host model.

## Provider selection remains host-owned

The EF Core integration package should not decide whether the application uses SQL Server, PostgreSQL, SQLite, MySQL, or another EF Core provider.

Provider setup belongs in the host application composition root, for example:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});
```

A different host could choose a different provider without ASI Backbone changing its Core domain model:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});
```

Provider-specific details, including date/time conversions, schema names, retry settings, migrations assembly, and connection resiliency, should be handled by the host application or a provider-specific follow-up package if one is later introduced.

## Migration ownership

Migrations belong to the host application.

After applying ASI Backbone configurations in the host `DbContext`, the host creates and manages its own migrations the same way it manages the rest of its application schema.

```bash
dotnet ef migrations add AddAsiBackbonePersistence \
  --project src/MyApplication.Infrastructure \
  --startup-project src/MyApplication.Web

dotnet ef database update \
  --project src/MyApplication.Infrastructure \
  --startup-project src/MyApplication.Web
```

Those command paths are examples only. The actual projects, migration assembly, deployment strategy, and database update process belong to the host.

ASI Backbone should not ship required migrations for the host database because doing so would implicitly choose a provider, schema lifecycle, naming convention, deployment model, and operational policy for the host.

## Audit ledger signing metadata persistence

The EF Core audit ledger shape intentionally persists the full verification-relevant signing surface currently exposed by `AuditLedgerRecord`:

- `SigningHash`
- `SignatureKeyId`
- `SignatureKeyVersion`
- `SignatureAlgorithm`
- `SignatureValue`
- `SignatureProvider`
- `SignedUtc`

This decision keeps signing context durable when hosts later reconstruct a signed audit ledger record for verification, key-rotation investigation, incident review, or hash-chain analysis. `SignatureKeyId` and `SignatureKeyVersion` remain references only; they must not contain raw key material, private keys, credentials, managed identity tokens, connection strings, or provider SDK payloads.

Hosts upgrading an existing database should generate and review a host-owned migration after updating the package. The migration should add nullable columns for the new signing fields to `AsiBackboneAuditLedgerRecords` and include the configured indexes for signing hash, key ID, key version, provider, and signed timestamp. Regulated hosts should validate the generated migration against retention, encryption, access-control, legal hold, and audit-review requirements before deployment.

Persisted signing metadata does not make the database tamper-evident by itself. Verification still requires a concrete signing provider or verifier, protected key-management process, durable storage controls, retention policy, monitoring, and operational procedures supplied by the host environment.

## Persistence failure diagnostics

`EfCoreAuditLedgerStore.AppendAsync` returns a stable sanitized failure reason when EF Core reports an append failure:

- reason code: `asi_backbone.audit_ledger.append_failed`
- message: `The audit ledger record could not be persisted by the configured EF Core store.`

Raw `DbUpdateException` messages are not returned through public `OperationResult` values because provider messages may contain database names, schema details, connection descriptors, constraint names, SQL fragments, or other implementation details inappropriate for consumer-visible compliance artifacts, API responses, or forwarded telemetry.

If a host supplies an `ILogger<EfCoreAuditLedgerStore>`, exception details are logged inside the host-controlled diagnostics boundary. The host owns logger configuration, redaction, routing, retention, access control, and any decision to forward detailed persistence diagnostics outside the application boundary.

## Durable governance outbox tables

The EF Core adapter now includes durable local storage for provider-neutral governance outbox entries and audit residue lifecycle events. The durable tables are intended to prove local persistence before optional downstream provider emission is attempted.

The host migration generated from `ApplyAsiBackboneConfigurations()` should include, among the existing audit ledger and handshake tables:

- `AsiBackboneGovernanceOutboxEntries`
- `AsiBackboneAuditResidueLifecycleEvents`

`AsiBackboneGovernanceOutboxEntries` stores the minimized governance emission envelope plus operational delivery state, including status, retry count, max retry count, next retry UTC, delivered UTC, provider name, provider record ID, last provider-neutral error fields, dead-letter reason, and safe metadata JSON.

`AsiBackboneAuditResidueLifecycleEvents` stores append-oriented lifecycle progress such as decision evaluated, external emission queued, delivered, failed, or dead-lettered. These rows let hosts correlate the local outbox with original audit residue without rewriting the original decision residue.

## Durable outbox and downstream providers

The EF Core adapter is durable storage. It is not a telemetry exporter, SIEM integration, Event Hubs producer, Azure Monitor provider, Purview integration, robotics adapter, or AI model host.

A recommended production flow is:

1. Create provider-neutral audit residue or lifecycle event in Core.
2. Persist the lifecycle event through `IAsiBackboneAuditResidueLifecycleStore`.
3. Enqueue the governance emission envelope through `IAsiBackboneGovernanceOutboxStore`.
4. Let a downstream provider drain pending or retry-ready entries.
5. Mark entries as delivered, failed/retryable, deferred, or dead-lettered based on provider-neutral results.

This keeps provider failures from erasing accountability records and keeps Core independent of OpenTelemetry, Azure Monitor, Event Hubs, Purview, SIEM, robotics, AI model, or cloud-provider SDK dependencies.

## Privacy and retention notes

The EF Core tables should store minimized governance metadata, not raw prompts, protected content, raw secrets, raw tokens, or provider SDK payloads. Use content hashes, stable IDs, schema versions, policy versions, policy hashes, trace IDs, correlation IDs, lifecycle stages, and safe diagnostic metadata instead of sensitive source payloads.

Hosts should define retention, encryption, archival, deletion, and access-control policies that match their environment. Durable outbox storage improves local accountability, but it is not by itself a compliance guarantee, tamper-evident ledger, signing system, or legal hold process.

## What ASI Backbone adds to the host model

The EF Core package contributes provider-neutral mappings for ASI Backbone accountability records, including:

- audit ledger records
- audit ledger reason codes
- audit ledger metadata
- handshake requests
- handshake request metadata
- handshake acknowledgments
- handshake acknowledgment metadata
- governance outbox entries
- audit residue lifecycle events

These records are part of the governance spine. They are intended to preserve durable accountability snapshots such as actor identity, actor type, operation name, outcome, reason codes, metadata, correlation ID, trace ID, policy version, policy hash, handshake identifiers, acknowledgment identifiers, capability-token identifiers, signing hashes, key references, signature descriptors, outbox status, retry posture, lifecycle stage, and provider-neutral delivery diagnostics.

## What the package does not do automatically

The EF Core package does not automatically:

- create or register the host `DbContext`
- select a database provider
- select or store a connection string
- create a production database
- run migrations
- deploy schema changes
- configure backup, retention, archival, or encryption policy
- emit rows to downstream providers
- drain the outbox on a schedule
- replace the host application's existing persistence architecture
- require NetCoreApplicationTemplate

That separation is intentional. ASI Backbone provides the accountability model and integration hooks; the host owns infrastructure.

## NetCoreApplicationTemplate usage

`AsiBackbone/NetCoreApplicationTemplate` can be a preferred validation host because it already emphasizes secure defaults, logging, deployment-friendly documentation, and enterprise-ready ASP.NET Core structure.

It is not required.

A consuming application can integrate ASI Backbone EF Core persistence from:

- a plain ASP.NET Core application
- a worker service
- a console validation host
- a NetCoreApplicationTemplate-based application
- another host-owned EF Core application

Documentation and package behavior should avoid assuming NetCoreApplicationTemplate conventions unless a sample explicitly says it is demonstrating that host.

## Practical checklist for host applications

When integrating ASI Backbone EF Core persistence, the host application should decide:

1. Which `DbContext` owns the ASI Backbone entities.
2. Which EF Core provider is used.
3. Which connection string and environment-specific configuration are used.
4. Which migration assembly owns schema changes.
5. How migrations are reviewed, deployed, rolled back, and audited.
6. How accountability records are retained, archived, protected, or purged.
7. Whether provider-specific conversions or conventions are needed.
8. Which background service, hosted worker, or provider package drains the governance outbox.
9. How failed, deferred, retryable, and dead-lettered entries are monitored and escalated.
10. How signing metadata is protected, reviewed, verified, and correlated during key rotation or audit-chain investigation.
11. How sanitized persistence failures are surfaced while detailed EF Core diagnostics remain inside host-owned logging and operational review boundaries.

The ASI Backbone package should remain a clean integration layer inside that host-owned plan.

## Related documentation

See [EF Core Integration Boundary](ef-core-integration-boundary.md) for the broader package boundary and non-goals.
