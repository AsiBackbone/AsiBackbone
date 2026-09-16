using AsiBackbone.Core.Audit;
using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Outbox;
using AsiBackbone.DependencyInjection;
using AsiBackbone.Storage.InMemory;
using AsiBackbone.Storage.InMemory.Audit;
using AsiBackbone.Storage.InMemory.CapabilityTokens;
using AsiBackbone.Storage.InMemory.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Core.Tests.Storage;

/// <summary>
/// Focused coverage tests for in-memory storage behavior and builder registrations.
/// </summary>
public sealed class InMemoryStorageCoverageTests
{
    /// <summary>
    /// Verifies lifecycle events can be appended and retrieved by event, correlation, and residue identifiers in stable order.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task LifecycleStoreAppendsAndQueriesEventsInStableOrder()
    {
        var store = new InMemoryDecisionReceiptLifecycleStore();
        DateTimeOffset later = new(2026, 7, 10, 12, 0, 1, TimeSpan.Zero);
        DateTimeOffset earlier = later.AddSeconds(-1);
        DecisionReceiptLifecycleEvent second = CreateLifecycleEvent("event-b", earlier, "correlation-1", "residue-1");
        DecisionReceiptLifecycleEvent first = CreateLifecycleEvent("event-a", earlier, "correlation-1", "residue-1");
        DecisionReceiptLifecycleEvent unrelated = CreateLifecycleEvent("event-c", later, "correlation-2", "residue-2");

        Assert.Same(second, await store.AppendAsync(second, TestContext.Current.CancellationToken));
        Assert.Same(first, await store.AppendAsync(first, TestContext.Current.CancellationToken));
        _ = await store.AppendAsync(unrelated, TestContext.Current.CancellationToken);

        Assert.Same(first, await store.FindByEventIdAsync(" event-a ", TestContext.Current.CancellationToken));
        Assert.Null(await store.FindByEventIdAsync("missing", TestContext.Current.CancellationToken));

        IReadOnlyList<DecisionReceiptLifecycleEvent> byCorrelation = await store.FindByCorrelationIdAsync(
            " correlation-1 ", TestContext.Current.CancellationToken);
        IReadOnlyList<DecisionReceiptLifecycleEvent> byResidue = await store.FindByAuditResidueIdAsync(
            " residue-1 ", TestContext.Current.CancellationToken);

        Assert.Equal(["event-a", "event-b"], byCorrelation.Select(item => item.EventId));
        Assert.Equal(["event-a", "event-b"], byResidue.Select(item => item.EventId));
        Assert.Empty(await store.FindByCorrelationIdAsync("missing", TestContext.Current.CancellationToken));
        Assert.Empty(await store.FindByAuditResidueIdAsync("missing", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies duplicate lifecycle event identifiers are rejected without replacing the original event.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task LifecycleStoreRejectsDuplicateEventIdentifiers()
    {
        var store = new InMemoryDecisionReceiptLifecycleStore();
        DecisionReceiptLifecycleEvent original = CreateLifecycleEvent(
            "duplicate-event", new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero), "correlation-1", "residue-1");
        DecisionReceiptLifecycleEvent duplicate = CreateLifecycleEvent(
            "duplicate-event", new DateTimeOffset(2026, 7, 10, 12, 0, 1, TimeSpan.Zero), "correlation-2", "residue-2");

        _ = await store.AppendAsync(original, TestContext.Current.CancellationToken);
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.AppendAsync(duplicate, TestContext.Current.CancellationToken));

        Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
        Assert.Same(original, await store.FindByEventIdAsync("duplicate-event", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies lifecycle store argument and cancellation guards.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task LifecycleStoreRejectsInvalidArgumentsAndCancellation()
    {
        var store = new InMemoryDecisionReceiptLifecycleStore();
        CancellationToken testCancellationToken = TestContext.Current.CancellationToken;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(testCancellationToken);
        source.Cancel();
        DecisionReceiptLifecycleEvent lifecycleEvent = CreateLifecycleEvent(
            "event-1", DateTimeOffset.UtcNow, "correlation-1", "residue-1");

        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await store.AppendAsync(null!, testCancellationToken));
        _ = await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await store.FindByEventIdAsync(" ", testCancellationToken));
        _ = await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await store.FindByCorrelationIdAsync(" ", testCancellationToken));
        _ = await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await store.FindByAuditResidueIdAsync(" ", testCancellationToken));
        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.AppendAsync(lifecycleEvent, source.Token));
        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.FindByEventIdAsync("event-1", source.Token));
        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.FindByCorrelationIdAsync("correlation-1", source.Token));
        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.FindByAuditResidueIdAsync("residue-1", source.Token));
    }

    /// <summary>
    /// Verifies all in-memory builder extensions register expected singleton services and return the same builder.
    /// </summary>
    [Fact]
    public void BuilderExtensionsRegisterExpectedSingletonServices()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        Assert.Same(builder, builder.UseInMemoryAuditLedger());
        Assert.Same(builder, builder.UseInMemoryAuditLifecycle());
        Assert.Same(builder, builder.UseInMemoryGovernanceOutbox());
        Assert.Same(builder, builder.UseInMemoryCapabilityGrantUseStore());

        AssertSingleton<InMemoryAuditLedger>(services);
        AssertSingleton<IDecisionReceiptSink>(services);
        AssertSingleton<IDecisionReceiptLifecycleStore>(services);
        AssertSingleton<IGovernanceOutboxStore>(services);
        AssertSingleton<InMemoryCapabilityGrantUseStore>(services);
        AssertSingleton<ICapabilityGrantUseStore>(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<InMemoryAuditLedger>(), provider.GetRequiredService<IDecisionReceiptSink>());
        _ = Assert.IsType<InMemoryDecisionReceiptLifecycleStore>(provider.GetRequiredService<IDecisionReceiptLifecycleStore>());
        _ = Assert.IsType<InMemoryGovernanceOutboxStore>(provider.GetRequiredService<IGovernanceOutboxStore>());
        Assert.Same(provider.GetRequiredService<InMemoryCapabilityGrantUseStore>(), provider.GetRequiredService<ICapabilityGrantUseStore>());
    }

    /// <summary>
    /// Verifies all in-memory builder extensions reject null builders.
    /// </summary>
    [Fact]
    public void BuilderExtensionsRejectNullBuilders()
    {
        IAsiBackboneBuilder? builder = null;

        Assert.Equal("builder", Assert.Throws<ArgumentNullException>(() => builder!.UseInMemoryAuditLedger()).ParamName);
        Assert.Equal("builder", Assert.Throws<ArgumentNullException>(() => builder!.UseInMemoryAuditLifecycle()).ParamName);
        Assert.Equal("builder", Assert.Throws<ArgumentNullException>(() => builder!.UseInMemoryGovernanceOutbox()).ParamName);
        Assert.Equal("builder", Assert.Throws<ArgumentNullException>(() => builder!.UseInMemoryCapabilityGrantUseStore()).ParamName);
    }

    private static DecisionReceiptLifecycleEvent CreateLifecycleEvent(
        string eventId,
        DateTimeOffset occurredUtc,
        string correlationId,
        string auditResidueId)
    {
        return DecisionReceiptLifecycleEvent.Create(
            DecisionReceiptLifecycleStage.DecisionEvaluated,
            correlationId,
            auditResidueId,
            eventId,
            occurredUtc);
    }

    private static void AssertSingleton<TService>(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
