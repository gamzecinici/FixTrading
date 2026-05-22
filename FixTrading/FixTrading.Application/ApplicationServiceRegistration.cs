using FixTrading.Application.Interfaces.Arbitrage;
using FixTrading.Application.Interfaces.FinancialAnalytics;
using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Application.Interfaces.Pricing;
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

        services.AddScoped<IPricingLimitsSyncService, PricingLimitsSyncService>();

        // ArbitrageService saf hesaplama yapar, state tutmaz => Singleton guvenli ve ucuzdur.
        services.AddSingleton<IArbitrageService, ArbitrageService>();
        services.AddSingleton<IVolatilityAnalyticsService, VolatilityAnalyticsService>();
        services.AddSingleton<IRiskAnalyticsService, RiskAnalyticsService>();

        return services;
    }
}

