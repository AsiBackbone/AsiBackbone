# AsiBackbone.DependencyInjection

[![NuGet](https://img.shields.io/nuget/v/AsiBackbone.DependencyInjection?label=NuGet)](https://www.nuget.org/packages/AsiBackbone.DependencyInjection)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AsiBackbone.DependencyInjection?label=downloads)](https://www.nuget.org/packages/AsiBackbone.DependencyInjection)

Explicit `AddAsiBackbone(...)` builder facade for coordinating host-selected AsiBackbone provider registrations.

> **Important:**
> This package is a configuration convenience only. It does not register persistence, signing, telemetry, endpoint governance, outbox workers, local-development providers, authorization, execution behavior, or operational defaults unless the host explicitly calls a named provider method.

## Why this package exists

`AsiBackbone.Core` remains framework-neutral. Provider packages own their own `Use*` extension methods so ASP.NET Core, EF Core, OpenTelemetry, signing, and storage dependencies remain optional.

This package contains only the shared builder abstraction and the single discoverable service-collection entry point:

```csharp
builder.Services.AddAsiBackbone(backbone =>
{
    // Provider packages light up named Use* calls when referenced.
});
```

An empty callback does not select hidden defaults.

## Design rule

Every `Use*` call should be explainable as a small set of explicit service registrations the host could have written manually.

Manual registration remains supported and documented as the canonical baseline. The builder only improves discoverability and sequencing for hosts that prefer a fluent configuration shape.
