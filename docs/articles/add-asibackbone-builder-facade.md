# AddAsiBackbone Builder Facade

`AsiBackbone.DependencyInjection` provides a single discoverable dependency injection entry point for coordinating host-selected provider registrations:

```csharp
builder.Services.AddAsiBackbone(backbone =>
{
    backbone.UseAspNetCoreEndpointGovernance();
    backbone.UseEfCoreAuditLedger<ApplicationDbContext>();
    backbone.UseEfCoreAuditLifecycle<ApplicationDbContext>();
    backbone.UseEfCoreGovernanceOutbox<ApplicationDbContext>();
    backbone.UseGovernanceOutboxDrain();
    backbone.UseManagedKeySigning(options =>
    {
        options.KeyId = "host-owned-key";
        options.KeyVersion = "v1";
        options.ProviderName = "host-managed-key";
    });
    backbone.UseOpenTelemetryEmission();
});
```

The facade is intentionally a coordination surface, not a bootstrapper. It does not select persistence, signing, telemetry, endpoint governance, local-development storage, or outbox delivery by itself.

## Package home decision

The builder facade lives in `AsiBackbone.DependencyInjection` rather than Core or ASP.NET Core.

This keeps `AsiBackbone.Core` framework-neutral and avoids forcing ASP.NET Core, EF Core, OpenTelemetry, signing, storage, cloud, or hosting dependencies into the core governance primitives. It also avoids making ASP.NET Core the owner of a builder shape that is useful for worker services, console hosts, gateways, and other non-web applications.

Provider packages own their own builder extension methods:

| Provider package | Example builder calls |
| --- | --- |
| `AsiBackbone.AspNetCore` | `UseAspNetCoreEndpointGovernance()`, `UseGovernanceOutboxDrain()` |
| `AsiBackbone.EntityFrameworkCore` | `UseEfCoreAuditLedger<TDbContext>()`, `UseEfCoreAuditLifecycle<TDbContext>()`, `UseEfCoreGovernanceOutbox<TDbContext>()` |
| `AsiBackbone.Storage.InMemory` | `UseInMemoryAuditLedger()`, `UseInMemoryAuditLifecycle()`, `UseInMemoryGovernanceOutbox()` |
| `AsiBackbone.Signing.LocalDevelopment` | `UseLocalDevelopmentSigning()` |
| `AsiBackbone.Signing.ManagedKey` | `UseManagedKeySigning(...)` |
| `AsiBackbone.OpenTelemetry` | `UseOpenTelemetryEmission()` |

A host only sees the provider calls for packages it references.

## Manual registration remains the baseline

The builder does not replace manual dependency injection. Each `Use*` method maps to explicit service registrations the host could write directly.

Manual ASP.NET Core endpoint governance registration:

```csharp
builder.Services.AddAsiBackboneAspNetCore();
```

Builder equivalent:

```csharp
builder.Services.AddAsiBackbone(backbone =>
{
    backbone.UseAspNetCoreEndpointGovernance();
});
```

Manual EF Core audit ledger registration:

```csharp
builder.Services.AddScoped<IAsiBackboneAuditLedgerStore>(provider =>
    ActivatorUtilities.CreateInstance<EfCoreAuditLedgerStore>(
        provider,
        provider.GetRequiredService<ApplicationDbContext>()));
```

Bind each store to the context it should use rather than registering the open `DbContext` service. Registering `DbContext` makes the last such registration win for every store, so a host that points different stores at different contexts silently gets one of them, and it collides with any `DbContext` registration the host already owns.

Builder equivalent:

```csharp
builder.Services.AddAsiBackbone(backbone =>
{
    backbone.UseEfCoreAuditLedger<ApplicationDbContext>();
});
```

Manual OpenTelemetry emission provider registration:

```csharp
builder.Services.AddSingleton(new OpenTelemetryGovernanceEmitterOptions());
builder.Services.AddSingleton<OpenTelemetryGovernanceEmitter>();
builder.Services.AddSingleton<IAsiBackboneGovernanceEmitter>(provider =>
    provider.GetRequiredService<OpenTelemetryGovernanceEmitter>());
```

Builder equivalent:

```csharp
builder.Services.AddAsiBackbone(backbone =>
{
    backbone.UseOpenTelemetryEmission();
});
```

## Local-development example

Local-development storage and signing remain explicit. They are useful for samples and tests, not production accountability storage or production key custody.

```csharp
builder.Services.AddAsiBackbone(backbone =>
{
    backbone.UseInMemoryAuditLedger();
    backbone.UseInMemoryAuditLifecycle();
    backbone.UseInMemoryGovernanceOutbox();
    backbone.UseLocalDevelopmentSigning();
});
```

## Empty and partial configuration

An empty builder callback does not register hidden defaults:

```csharp
builder.Services.AddAsiBackbone(_ => { });
```

A partial callback only registers the provider calls named by the host:

```csharp
builder.Services.AddAsiBackbone(backbone =>
{
    backbone.UseOpenTelemetryEmission();
});
```

That example does not add EF Core, ASP.NET Core endpoint governance, signing, local-development storage, or outbox drain worker services.

## Boundary

Use the builder when a host wants one fluent place to coordinate explicit AsiBackbone provider selections. Use manual registration when a host wants maximum clarity or has custom persistence, signing, telemetry, or gateway infrastructure.

Both approaches preserve the same boundary: AsiBackbone provides accountable decision-flow infrastructure, while the host remains responsible for database configuration, key custody, telemetry exporters, execution behavior, deployment hardening, and compliance review.
