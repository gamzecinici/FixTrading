using FixTrading.Application.Interfaces.Instrument;
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
        // İstek başına bir InstrumentService örneği (Scoped)
        services.AddScoped<IInstrumentService, InstrumentService>();

        return services;
    }
}

