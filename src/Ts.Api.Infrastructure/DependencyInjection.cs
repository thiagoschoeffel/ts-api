using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ts.Api.Application.Catalog;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Menus;
using Ts.Api.Application.Operations;
using Ts.Api.Application.Organizations;
using Ts.Api.Application.Production;
using Ts.Api.Application.Commerce;
using Ts.Api.Application.Logistics;
using Ts.Api.Application.Attendance;
using Ts.Api.Infrastructure.Messaging;
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
        services.AddScoped<ICatalogManagementStore, CatalogManagementStore>();
        services.AddScoped<IMenuStore, MenuStore>();
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
        services.AddScoped<IOrderQueryStore, OrderQueryStore>();
        services.AddScoped<IOperationsStore, OperationsStore>();
        services.AddScoped<IMembershipStore, MembershipStore>();
        services.AddSingleton<IInvitationEmailSender>(new ResendInvitationEmailSender(new HttpClient
        {
            BaseAddress = new Uri(configuration["Resend:ApiBaseUrl"] ?? "https://api.resend.com/"),
            Timeout = TimeSpan.FromSeconds(15),
        }, configuration));
        services.AddScoped<IPlatformRegistryStore, PlatformRegistryStore>();
        services.AddScoped<IPlatformLifecycleStore, PlatformLifecycleStore>();
        services.AddScoped<IPlatformOnboardingStore, PlatformOnboardingStore>();
        services.AddSingleton<IOnboardingInvitationTokenFactory, OnboardingInvitationTokenFactory>();
        services.AddScoped<ICommerceStore, CommerceStore>();
        services.AddScoped<ILogisticsStore, LogisticsStore>();
        services.AddScoped<IAttendanceStore, AttendanceStore>();
        services.AddSingleton(provider => new HttpClient
        {
            BaseAddress = new Uri(provider.GetRequiredService<IConfiguration>()["WhatsApp:GraphApiBaseUrl"] ?? "https://graph.facebook.com/v25.0/"),
            Timeout = TimeSpan.FromSeconds(15),
        });
        services.AddScoped<IWhatsAppCloudClient, WhatsAppCloudClient>();
        services.AddSingleton<IWhatsAppConfiguration, WhatsAppConfiguration>();
        return services;
    }
}
