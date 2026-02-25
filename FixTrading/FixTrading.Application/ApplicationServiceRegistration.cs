using FixTrading.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FixTrading.Application;

/// <summary>
/// servisleri DI container'a kaydeder.
/// </summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // İstek başına bir FixSymbolService örneği (Scoped)
        services.AddScoped<IFixSymbolService, FixSymbolService>();

        return services;
    }
}

