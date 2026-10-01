# ASIB903: Outbox Drain Retry Clock

`ASIB903` is reported when source code reads or assigns `GovernanceOutboxDrainWorkerOptions.RetryClock`.

The property remains available in the `7.x` line, and a custom delegate is still honored. `ASIB903` is a compiler warning, but projects that enable `TreatWarningsAsErrors` will fail to build until the affected code is migrated or the diagnostic is narrowly suppressed.

## Why the property is deprecated

Since 7.0, the hosted outbox worker, `GovernanceOutboxDrain`, and the signing providers read the time from the registered `TimeProvider`. `RetryClock` predates that change. While it keeps its default value, the worker ignores it and reads the registered `TimeProvider`.

Assigning a custom delegate gives the worker a second clock. Retry-ready lookups then use that delegate, while drain claim leases and signing timestamps use the registered `TimeProvider`. If the two disagree, an entry can be treated as retry-ready at a different time than its lease or its recorded timestamps imply.

## Migrate

Remove the `RetryClock` assignment and register a `TimeProvider`:

```csharp
// Before: compiles with ASIB903 in 7.x.
builder.Services.AddAsiBackboneGovernanceOutboxDrainWorker(options =>
{
    options.RetryClock = () => DateTimeOffset.UtcNow;
});

// After: one clock for the worker, the drain, and the signing providers.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAsiBackboneGovernanceOutboxDrainWorker(options =>
{
    options.BatchSize = 100;
});
```

Tests that pinned the drain time through `RetryClock` should register a fake `TimeProvider`, such as `FakeTimeProvider` from the `Microsoft.Extensions.TimeProvider.Testing` package, instead.

## Removal target

`RetryClock` is planned for removal in `8.0`, subject to the repository's [deprecation and major-release policy](api-compatibility-and-semver.md#deprecation-and-major-release-policy). It remains available throughout the required `7.x` deprecation window.

Do not suppress `ASIB903` globally. Keep any suppression scoped to the code that still assigns a custom delegate, and remove it once that code registers a `TimeProvider`.
