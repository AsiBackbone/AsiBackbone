using System.Reflection;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Outbox;
using AsiBackbone.DependencyInjection;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.EntityFrameworkCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="AsiBackboneEntityFrameworkCoreBuilderExtensions" /> class.
/// </summary>
public sealed class AsiBackboneEntityFrameworkCoreBuilderExtensionsTests
{
    /// <summary>
    /// Verifies that audit-ledger registration uses the host context, registers the EF Core store, and returns the same builder.
    /// </summary>
    [Fact]
    public void UseEfCoreAuditLedgerRegistersServicesAndReturnsSameBuilder()
    {
        ServiceCollection services = CreateServices();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseEfCoreAuditLedger<TestDbContext>();

        Assert.Same(builder, result);
        AssertResolvesTo<IAsiBackboneAuditLedgerStore, EfCoreAuditLedgerStore>(services);
        AssertOpenDbContextIsNotRegistered(services);
    }

    /// <summary>
    /// Verifies that audit-ledger registration rejects a null builder.
    /// </summary>
    [Fact]
    public void UseEfCoreAuditLedgerRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseEfCoreAuditLedger<TestDbContext>());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that audit-lifecycle registration uses the host context, registers the EF Core store, and returns the same builder.
    /// </summary>
    [Fact]
    public void UseEfCoreAuditLifecycleRegistersServicesAndReturnsSameBuilder()
    {
        ServiceCollection services = CreateServices();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseEfCoreAuditLifecycle<TestDbContext>();

        Assert.Same(builder, result);
        AssertResolvesTo<IAsiBackboneAuditResidueLifecycleStore, EfCoreAuditResidueLifecycleStore>(services);
        AssertOpenDbContextIsNotRegistered(services);
    }

    /// <summary>
    /// Verifies that audit-lifecycle registration rejects a null builder.
    /// </summary>
    [Fact]
    public void UseEfCoreAuditLifecycleRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseEfCoreAuditLifecycle<TestDbContext>());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that governance-outbox registration exposes one scoped outcome-aware store through all compatible contracts.
    /// </summary>
    [Fact]
    public void UseEfCoreGovernanceOutboxRegistersServicesAndReturnsSameBuilder()
    {
        ServiceCollection services = CreateServices();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseEfCoreGovernanceOutbox<TestDbContext>();

        Assert.Same(builder, result);
        AssertResolvesTo<EfCoreGovernanceOutboxOutcomeStore, EfCoreGovernanceOutboxOutcomeStore>(services);
        AssertScopedFactoryRegistration<IAsiBackboneGovernanceOutboxClaimOutcomeStore>(services);
        AssertScopedFactoryRegistration<IAsiBackboneGovernanceOutboxClaimStore>(services);
        AssertScopedFactoryRegistration<IAsiBackboneGovernanceOutboxStore>(services);
        AssertOpenDbContextIsNotRegistered(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        EfCoreGovernanceOutboxOutcomeStore concrete = scope.ServiceProvider.GetRequiredService<EfCoreGovernanceOutboxOutcomeStore>();

        Assert.Same(concrete, scope.ServiceProvider.GetRequiredService<IAsiBackboneGovernanceOutboxClaimOutcomeStore>());
        Assert.Same(concrete, scope.ServiceProvider.GetRequiredService<IAsiBackboneGovernanceOutboxClaimStore>());
        Assert.Same(concrete, scope.ServiceProvider.GetRequiredService<IAsiBackboneGovernanceOutboxStore>());
    }

    /// <summary>
    /// Verifies that governance-outbox registration rejects a null builder.
    /// </summary>
    [Fact]
    public void UseEfCoreGovernanceOutboxRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseEfCoreGovernanceOutbox<TestDbContext>());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that stores registered against different contexts each use the context named at their own call site.
    /// </summary>
    /// <remarks>
    /// Each registration previously added the open <see cref="DbContext" /> service, so the last call won and every
    /// store resolved the same context. A host using two contexts got stores bound to the wrong one, failing only at
    /// runtime when the store reached for a set the context does not have.
    /// </remarks>
    [Fact]
    public void StoresRegisteredAgainstDifferentContextsUseTheirOwnContext()
    {
        ServiceCollection services = CreateServices();
        _ = services.AddDbContext<SecondTestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        _ = builder.UseEfCoreAuditLedger<TestDbContext>();
        _ = builder.UseEfCoreGovernanceOutbox<SecondTestDbContext>();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        TestDbContext ledgerContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        SecondTestDbContext outboxContext = scope.ServiceProvider.GetRequiredService<SecondTestDbContext>();

        Assert.Same(ledgerContext, GetStoreDbContext(scope.ServiceProvider.GetRequiredService<IAsiBackboneAuditLedgerStore>()));
        Assert.Same(outboxContext, GetStoreDbContext(scope.ServiceProvider.GetRequiredService<IAsiBackboneGovernanceOutboxStore>()));
    }

    /// <summary>
    /// Verifies that registration does not collide with a host-owned open <see cref="DbContext" /> registration.
    /// </summary>
    [Fact]
    public void RegistrationLeavesAHostOwnedDbContextRegistrationIntact()
    {
        ServiceCollection services = CreateServices();
        _ = services.AddScoped<DbContext>(serviceProvider => serviceProvider.GetRequiredService<TestDbContext>());
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        _ = builder.UseEfCoreAuditLedger<TestDbContext>();

        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DbContext));
    }

    private static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));

        return services;
    }

    private static void AssertResolvesTo<TService, TImplementation>(IServiceCollection services)
        where TService : notnull
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService)
                && descriptor.Lifetime == ServiceLifetime.Scoped);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        _ = Assert.IsType<TImplementation>(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private static void AssertOpenDbContextIsNotRegistered(IServiceCollection services)
    {
        // Registering the open DbContext service made the last UseEfCore* call win for every store, so a host
        // pointing different stores at different contexts silently got one of them. It also collided with any
        // DbContext registration the host owned.
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(DbContext));
    }

    private static DbContext GetStoreDbContext(object store)
    {
        FieldInfo field = store.GetType().GetField("dbContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{store.GetType().Name} does not expose a dbContext field.");

        return (DbContext)(field.GetValue(store)
            ?? throw new InvalidOperationException($"{store.GetType().Name} has no context assigned."));
    }

    private static void AssertScopedFactoryRegistration<TService>(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationFactory is not null
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    private sealed class SecondTestDbContext(DbContextOptions<SecondTestDbContext> options) : DbContext(options);
}
