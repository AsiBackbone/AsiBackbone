using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Outbox;
using AsiBackbone.DependencyInjection;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.EntityFrameworkCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.EntityFrameworkCore;

/// <summary>
/// Provides explicit builder facade extension methods for EF Core host-owned persistence.
/// </summary>
/// <remarks>
/// Each method binds its stores to the <c>TDbContext</c> named at that call site. The open <see cref="DbContext" />
/// service is never registered, so a host can point different stores at different contexts, and none of these calls
/// collide with a <see cref="DbContext" /> registration the host owns.
/// </remarks>
public static class AsiBackboneEntityFrameworkCoreBuilderExtensions
{
    /// <summary>
    /// Adds EF Core audit ledger storage through the AsiBackbone builder facade.
    /// </summary>
    /// <typeparam name="TDbContext">The host-owned context holding the audit ledger set.</typeparam>
    /// <param name="builder">The AsiBackbone builder.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static IAsiBackboneBuilder UseEfCoreAuditLedger<TDbContext>(this IAsiBackboneBuilder builder)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Services.AddScoped<IAsiBackboneAuditLedgerStore>(serviceProvider =>
            ActivatorUtilities.CreateInstance<EfCoreAuditLedgerStore>(
                serviceProvider,
                serviceProvider.GetRequiredService<TDbContext>()));

        return builder;
    }

    /// <summary>
    /// Adds EF Core audit residue lifecycle storage through the AsiBackbone builder facade.
    /// </summary>
    /// <typeparam name="TDbContext">The host-owned context holding the lifecycle set.</typeparam>
    /// <param name="builder">The AsiBackbone builder.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static IAsiBackboneBuilder UseEfCoreAuditLifecycle<TDbContext>(this IAsiBackboneBuilder builder)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Services.AddScoped<IAsiBackboneAuditResidueLifecycleStore>(serviceProvider =>
            ActivatorUtilities.CreateInstance<EfCoreAuditResidueLifecycleStore>(
                serviceProvider,
                serviceProvider.GetRequiredService<TDbContext>()));

        return builder;
    }

    /// <summary>
    /// Adds outcome-aware EF Core durable governance outbox storage through the AsiBackbone builder facade.
    /// </summary>
    /// <typeparam name="TDbContext">The host-owned context holding the outbox set.</typeparam>
    /// <param name="builder">The AsiBackbone builder.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static IAsiBackboneBuilder UseEfCoreGovernanceOutbox<TDbContext>(this IAsiBackboneBuilder builder)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Services.AddScoped(serviceProvider =>
            ActivatorUtilities.CreateInstance<EfCoreGovernanceOutboxOutcomeStore>(
                serviceProvider,
                serviceProvider.GetRequiredService<TDbContext>()));
        _ = builder.Services.AddScoped<IAsiBackboneGovernanceOutboxClaimOutcomeStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfCoreGovernanceOutboxOutcomeStore>());
        _ = builder.Services.AddScoped<IAsiBackboneGovernanceOutboxClaimStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfCoreGovernanceOutboxOutcomeStore>());
        _ = builder.Services.AddScoped<IAsiBackboneGovernanceOutboxStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfCoreGovernanceOutboxOutcomeStore>());

        return builder;
    }
}
