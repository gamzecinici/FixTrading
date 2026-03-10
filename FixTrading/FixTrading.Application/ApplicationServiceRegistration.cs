using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FixTrading.Application;

//Bu sınıf, Application katmanındaki servislerin Dependency Injection (DI) konteynerine kaydedilmesi için kullanılır.
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // İstek başına bir InstrumentService örneği (Scoped)
        services.AddScoped<IInstrumentService, InstrumentService>();

        return services;
    }
}

