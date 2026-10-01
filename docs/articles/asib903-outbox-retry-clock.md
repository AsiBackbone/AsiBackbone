# ASIB903: Outbox Drain Retry Clock

`ASIB903` is reported when source code reads or assigns `GovernanceOutboxDrainWorkerOptions.RetryClock`.

The property remains available in the `7.x` line, and a custom delegate is still honored. `ASIB903` is a compiler warning, but projects that enable `TreatWarningsAsErrors` will fail to build until the affected code is migrated or the diagnostic is narrowly suppressed.

## Why the property is deprecated

Since 7.0, the hosted outbox worker and `GovernanceOutboxDrain` use the registered `TimeProvider` when the worker option keeps its default value. Other components that consume the registered clock, such as `LocalDevelopmentSigningService`, can share that same time source. `RetryClock` predates that arrangement.

Assigning a custom delegate bypasses the registered `TimeProvider` for the entire hosted drain cycle. The worker resolves the delegate once and passes that value as the explicit `utcNow` argument to `GovernanceOutboxDrain.DrainAsync(...)`. The drain then reuses that caller-supplied timestamp for retry-ready checks, claim-page and lease timing, and the persisted transition timestamps produced during that cycle. Other `TimeProvider` consumers continue to use the registered provider, so the custom delegate can make drain time disagree with them.

## Migrate

Remove the `RetryClock` assignment and register a `TimeProvider`:

```csharp
// Before: compiles with ASIB903 in 7.x.
builder.Services.AddAsiBackboneGovernanceOutboxDrainWorker(options =>
{
    options.RetryClock = () => DateTimeOffset.UtcNow;
});

// After: one registered clock for the worker, the drain, and other TimeProvider-aware services.
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
