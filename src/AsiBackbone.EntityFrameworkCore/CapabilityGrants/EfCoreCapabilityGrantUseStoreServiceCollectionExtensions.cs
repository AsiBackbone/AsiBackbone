using AsiBackbone.Core.CapabilityGrants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.EntityFrameworkCore.CapabilityGrants;

/// <summary>Registers durable EF Core capability-grant replay protection.</summary>
public static class EfCoreCapabilityGrantUseStoreServiceCollectionExtensions
{
    /// <summary>Registers the durable store against a host-owned context.</summary>
    public static IServiceCollection AddAsiBackboneEfCoreCapabilityGrantUseStore<TDbContext>(
        this IServiceCollection services,
        Action<EfCoreCapabilityGrantUseStoreOptions>? configure = null)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        EfCoreCapabilityGrantUseStoreOptions options = new();
        configure?.Invoke(options);
        options.Validate();
        _ = services.AddSingleton(options);
        _ = services.AddScoped<EfCoreCapabilityGrantUseStore>(provider =>
            new(provider.GetRequiredService<TDbContext>(), provider.GetRequiredService<EfCoreCapabilityGrantUseStoreOptions>()));
        _ = services.AddScoped<ICapabilityGrantUseStore>(provider =>
            provider.GetRequiredService<EfCoreCapabilityGrantUseStore>());
        return services;
    }
}
