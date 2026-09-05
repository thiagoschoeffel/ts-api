using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ts.Api.Application.Catalog;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Production;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database não foi configurada.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ICatalogOfferStore, CatalogOfferStore>();
        services.AddScoped<IProducibleItemStore, ProducibleItemStore>();
        services.AddScoped<IFrozenConfigurationStore, FrozenConfigurationStore>();
        services.AddScoped<IFrozenProductionStore, FrozenProductionStore>();
        services.AddScoped<IFrozenStockManagementStore, FrozenStockManagementStore>();
        services.AddScoped<IOrderConfirmationStore, OrderConfirmationStore>();
        services.AddScoped<IOrderLifecycleStore, OrderLifecycleStore>();
        services.AddScoped<IOrderConfirmationSetupStore, OrderConfirmationSetupStore>();
        services.AddScoped<OrderManagementStore>();
        services.AddScoped<IOrderManagementStore>(provider => provider.GetRequiredService<OrderManagementStore>());
        services.AddScoped<IDailyCapacityManagementStore>(
            provider => provider.GetRequiredService<OrderManagementStore>());
        return services;
    }
}
