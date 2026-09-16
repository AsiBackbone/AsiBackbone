# EF Core Integration Package Boundary

This article defines the intended boundary for the implemented `AsiBackbone.EntityFrameworkCore` package.

The EF Core integration package provides persistence configuration and storage helpers for AsiBackbone accountability records while preserving host ownership of the application database.

> [!IMPORTANT]
> AsiBackbone should not own the host application's `DbContext`, database provider, connection string, migrations, deployment process, or schema lifecycle. The host application remains responsible for those concerns.

## Boundary statement

The EF Core package should answer this question:

> How can a host-owned EF Core application persist AsiBackbone accountability records without surrendering database ownership to the package?

It should not answer:

> Which database provider must the host use?
> Which `DbContext` must the host inherit from?
> Which migration strategy must the host adopt?
> Which application template must the host use?

The package contributes EF Core model configuration, persistence-facing entities, and storage implementations where appropriate. The host application remains the composition root.

## Package responsibility

`AsiBackbone.EntityFrameworkCore` provides:

* EF Core entity type configurations for AsiBackbone persistence models
* `ModelBuilder` extension methods for applying AsiBackbone configurations
* EF Core-backed implementations of Core storage contracts
* persistence models for audit records, handshake records, reason codes, metadata, policy trace fields, lifecycle events, and governance outbox records
* provider-neutral EF Core configuration where practical
* integration tests proving the package works inside a host-owned `DbContext`

The package should avoid:

* a required package-owned `AsiBackboneDbContext`
* provider-specific assumptions in the core configuration layer
* forced migrations
* forced connection-string ownership
* hidden dependency on NetCoreApplicationTemplate
* ASP.NET Core middleware or endpoint behavior
* AI model hosting, inference, training, or orchestration
* robotics or physical execution concerns

## Host-owned `DbContext` strategy

The host application owns the `DbContext`.

A consuming application should be able to integrate AsiBackbone persistence using a pattern similar to:

```csharp
using AsiBackbone.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyAsiBackboneConfigurations();
    }
}
```

This keeps the database boundary clear:

```text
Host application
  owns DbContext
  owns provider
  owns connection string
  owns migrations
  owns deployment

AsiBackbone EF Core package
  contributes model configuration
  contributes persistence contracts/implementations
  does not own the database
```

## ModelBuilder extension location

The extension method lives in the EF Core integration package under:

```csharp
namespace AsiBackbone.EntityFrameworkCore;
```

The public contract is intentionally simple: the host calls one extension method from `OnModelCreating`.

```csharp
public static class AsiBackboneModelBuilderExtensions
{
    public static ModelBuilder ApplyAsiBackboneConfigurations(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AsiBackboneModelBuilderExtensions).Assembly);

        return modelBuilder;
    }
}
```

## Migration ownership

Migrations belong to the host application.

The EF Core integration package does not ship required migrations for the host database. This avoids forcing the package to choose a database provider, schema naming convention, migration assembly, deployment model, or operational process.

The host application should decide:

* database provider
* schema name
* table naming conventions where configurable
* migration assembly
* migration cadence
* deployment workflow
* data retention and archival rules
* encryption and key-management policy
* backup and restore strategy

AsiBackbone can document recommended schema shapes, but the host should own the actual migration files.

## Provider-neutral design

The initial EF Core integration favors provider-neutral configuration.

Provider-specific features should be avoided unless there is a clear extension point. For example, the first milestone should avoid relying on SQL Server-specific, PostgreSQL-specific, or SQLite-specific behavior in the package core.

Provider-specific examples may appear later in samples or documentation, but the package boundary should remain host-neutral.

## Metadata JSON storage strategy

The governance outbox currently stores minimized metadata dictionaries in string-backed JSON columns such as `MetadataJson`, `EnvelopeMetadataJson`, and `EnvelopePayloadMetadataJson`.

That remains the selected provider-neutral strategy for the current release line. Native EF Core JSON mapping can be valuable for strongly typed aggregates or complex types, but it is not adopted for the open-ended outbox metadata dictionaries because provider support, column types, compatibility levels, query translation, and migrations remain host/provider concerns.

Hosts that need provider-native JSON filtering may add host-owned migrations, computed/generated columns, JSON indexes, views, or provider-specific SQL over the existing columns. The package baseline stays portable and does not silently change text-backed metadata columns to provider-native JSON column types.

See [EF Core JSON Metadata Storage Strategy](ef-core-json-metadata-storage.md) for the evaluation record and future adoption criteria.

## Relationship to Core

`AsiBackbone.Core` remains free of EF Core references.

Dependency direction is:

```text
AsiBackbone.Core
        ▲
        │
AsiBackbone.EntityFrameworkCore
```

Core defines the domain contracts and governance primitives. The EF Core package adapts those contracts into persistence behavior.

The Core package should not know that EF Core exists.

## Relationship to NetCoreApplicationTemplate

NetCoreApplicationTemplate may be a preferred validation host, but it must not be required.

The EF Core package should support:

* plain ASP.NET Core applications
* worker services
* console validation hosts
* NetCoreApplicationTemplate-based applications
* other host-owned EF Core applications

The integration should be documented in a way that works without assuming NetCoreApplicationTemplate conventions.

## Implemented EF Core model configuration surface

The EF Core package contributes provider-neutral model configurations for:

- audit ledger records
- audit ledger reason codes
- audit ledger metadata
- handshake requests
- handshake request metadata
- handshake acknowledgments
- handshake acknowledgment metadata
- decision receipt lifecycle events
- governance outbox entries
- governance outbox metadata JSON columns
- governance outbox envelope and payload projection columns

The host application still owns the DbContext, provider, migrations, schema lifecycle, retention policy, and deployment workflow.

## Initial persistence focus

The first EF Core milestone focuses on accountability records, not broad application persistence.

Implemented persistence areas include:

* audit ledger records
* liability/responsibility handshake requests
* liability/responsibility handshake acknowledgments
* decision receipt lifecycle events
* governance outbox entries
* reason codes
* actor identifiers and actor type
* policy version and policy hash
* correlation ID and trace ID
* metadata snapshots

This aligns persistence with the AsiBackbone governance spine: decisions, acknowledgments, and audit trails should be durable and queryable.

## Non-goals for the first EF Core milestone

The first EF Core integration does not include:

* a package-owned application `DbContext`
* package-owned migrations
* provider-specific schema assumptions
* provider-native JSON column requirements
* package-owned JSON migration strategy
* ASP.NET Core middleware
* endpoint mapping
* authentication or claims translation
* signing or cryptographic verification
* robotics command persistence
* AI model output storage
* event sourcing infrastructure beyond simple audit/accountability persistence
* distributed ledger implementation

Those may be considered later if justified by follow-up issues.
