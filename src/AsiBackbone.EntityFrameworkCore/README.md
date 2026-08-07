# AsiBackbone.EntityFrameworkCore

Entity Framework Core model configuration and host-owned persistence helpers for Accountable Systems Infrastructure records.

This package contributes provider-neutral EF Core model configuration, persistence entities, EF Core-backed audit ledger storage, EF Core-backed audit residue lifecycle storage, and EF Core-backed durable governance outbox storage while preserving host ownership of the application database.

> **New to AsiBackbone?** Start with the concept, not this package: [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html) and the [documentation site](https://asibackbone.github.io/AsiBackbone/). This README covers one package in the family.

> **Important:**
> This package does not provide or require a package-owned `DbContext`, database provider, connection string, migration set, schema deployment workflow, tamper-evident storage, signing provider, downstream emission provider, or compliance guarantee. The host application owns those concerns.

## What this package provides

- `ApplyAsiBackboneConfigurations(this ModelBuilder modelBuilder)` for applying ASI Backbone persistence mappings from a host-owned `DbContext`.
- Persistence entities for audit ledger records, audit reason codes, audit metadata, handshake requests, handshake acknowledgments, governance outbox entries, audit residue lifecycle events, and related metadata rows.
- `EfCoreAuditLedgerStore`, an append-oriented audit ledger implementation that uses a host-owned `DbContext`.
- `EfCoreAuditResidueLifecycleStore`, an append-oriented lifecycle event implementation for durable progress records.
- `EfCoreGovernanceOutboxStore`, a durable local outbox implementation for provider-neutral governance emission envelopes.
- Provider-neutral configuration where practical.

## Minimal host-owned DbContext setup

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

    public DbSet<AsiBackboneGovernanceOutboxEntryEntity> GovernanceOutboxEntries =>
        Set<AsiBackboneGovernanceOutboxEntryEntity>();

    public DbSet<AsiBackboneAuditResidueLifecycleEventEntity> AuditResidueLifecycleEvents =>
        Set<AsiBackboneAuditResidueLifecycleEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyAsiBackboneConfigurations();
    }
}
```

The host application may expose `DbSet<T>` properties for convenience, but the package does not require a specific `DbContext` base type beyond EF Core's `DbContext`.

## Migration ownership

Migrations belong to the host application. After applying the ASI Backbone model configurations, create migrations in the same way the host creates the rest of its application schema.

```bash
dotnet ef migrations add AddAsiBackbonePersistence \
  --project src/MyApplication.Infrastructure \
  --startup-project src/MyApplication.Web

dotnet ef database update \
  --project src/MyApplication.Infrastructure \
  --startup-project src/MyApplication.Web
```

The project paths are examples only. The host application decides the provider, migration assembly, schema names, deployment workflow, retention policy, backup strategy, and environment-specific configuration.

## Audit ledger signing metadata persistence

`EfCoreAuditLedgerStore` persists the verification-relevant signing fields exposed by `AuditLedgerRecord` into the audit ledger row:

- `SigningHash`
- `SignatureKeyId`
- `SignatureKeyVersion`
- `SignatureAlgorithm`
- `SignatureValue`
- `SignatureProvider`
- `SignedUtc`

This preserves the signing hash, key reference, key version, provider descriptor, signature timestamp, algorithm, and signature value needed for later verification, key-rotation review, and hash-chain investigation. These values are still provider-neutral descriptors; they are not private keys, raw secrets, credentials, or proof of tamper-evidence by themselves.

Hosts upgrading an existing database should generate a host-owned migration after updating the package so the `AsiBackboneAuditLedgerRecords` table receives the new nullable signing columns and related indexes. Review the migration under the same change-control process used for the rest of the application schema, especially in regulated environments where historical audit review depends on durable signature context.

## Persistence failure diagnostics

`EfCoreAuditLedgerStore.AppendAsync` returns the stable reason code `asi_backbone.audit_ledger.append_failed` with the sanitized message `The audit ledger record could not be persisted by the configured EF Core store.` when EF Core reports an append failure. Raw provider exception messages are not returned through the public `OperationResult` because they may contain database, schema, connection, or provider implementation details.

When a host supplies an `ILogger<EfCoreAuditLedgerStore>`, exception details are logged inside the host-controlled diagnostics boundary. Hosts own log routing, redaction, access control, retention, and any decision to forward detailed persistence diagnostics outside the application boundary.

## Durable governance outbox storage

`EfCoreGovernanceOutboxStore` persists provider-neutral `GovernanceEmissionEnvelope` records before optional downstream provider delivery is attempted. It stores the envelope, status, retry count, next retry UTC, delivered timestamp, provider name, provider record ID, last provider-neutral error fields, dead-letter reason, and safe metadata.

Use the outbox store when a host needs local durability across process restarts before a later OpenTelemetry, SIEM, Event Hubs, Azure Monitor, Purview, custom, or internal provider drains and delivers pending entries.

The EF Core adapter is durable storage only. It does not drain the outbox by itself and does not add a cloud-provider SDK dependency to Core.

`EfCoreGovernanceOutboxStore` participates in EF Core optimistic concurrency through the configured `ConcurrencyStamp` token. That protects state updates from stale writes, but it is not an atomic claim-before-emit mechanism. Multiple workers can still read the same pending row before either worker saves the final delivered/failed state.

### Non-claim mutation concurrency ownership

The non-claim mutation methods `SaveAsync(...)`, `MarkDeliveredAsync(...)`, `MarkFailedAsync(...)`, and `MarkDeadLetteredAsync(...)` intentionally allow `DbUpdateConcurrencyException` to propagate unchanged. The store does not hide, translate, or automatically retry these conflicts.

The caller owns durable-state reload, conflict resolution, retry policy, and idempotency decisions. This prevents the package from silently returning another writer's state as though the current invocation applied it, and it avoids reapplying a stale transition after a newer durable writer has won.

For competing or scaled workers, prefer claim leasing and the outcome-aware claim contract so write ownership is explicit. See [EF Core Outbox Non-Claim Concurrency Ownership](https://asibackbone.github.io/AsiBackbone/articles/efcore-outbox-non-claim-concurrency-ownership.html) and [Outbox Multi-Worker Concurrency Guidance](https://asibackbone.github.io/AsiBackbone/articles/outbox-multi-worker-concurrency.html).

For scaled-out deployments, run one active worker per durable outbox partition unless the host adds provider-specific claim/lease behavior or downstream idempotency. See the DocFX article `Outbox Multi-Worker Concurrency` for the detailed deployment guidance.

## Registering EF Core stores

A host can register its concrete `DbContext` and expose it as EF Core's base `DbContext` for the stores.

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<DbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

builder.Services.AddScoped<IAsiBackboneAuditLedgerStore, EfCoreAuditLedgerStore>();
builder.Services.AddScoped<IAsiBackboneAuditResidueLifecycleStore, EfCoreAuditResidueLifecycleStore>();
builder.Services.AddScoped<IAsiBackboneGovernanceOutboxStore, EfCoreGovernanceOutboxStore>();
```

Provider-specific behavior belongs in the host application. For example, SQLite hosts may need provider-specific `DateTimeOffset` conversions for ordering or range queries. Multi-worker claiming patterns such as skip-locked row selection, update locks, read-past hints, or claim-lease columns also belong in the host/provider layer unless a future provider-specific package explicitly implements them.

## Privacy boundary

Durable rows should contain minimized governance metadata, not raw prompts, protected content, raw secrets, raw tokens, or provider SDK payloads. Prefer stable identifiers, content hashes, schema versions, policy versions, policy hashes, trace IDs, correlation IDs, lifecycle stages, and safe diagnostic metadata.

The host owns access control, encryption, retention, archival, deletion, and legal/compliance policy for the database.

## Boundary

Use this package when you want ASI Backbone accountability records persisted through a host-owned EF Core database.

Do not use this package as an AI model host, ASP.NET Core middleware package, migration owner, signing system, tamper-evident ledger provider, compliance system, downstream emission provider, database provider abstraction, or distributed lock manager.
