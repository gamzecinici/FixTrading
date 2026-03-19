using FixTrading.Domain.Interfaces;

namespace FixTrading.API.BackgroundServices;

/// <summary>
/// Periyodik olarak PostgreSQL pricing_limits tablosunu okuyup in-memory cache'i günceller.
/// </summary>
public class PricingLimitsCacheRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private const int RefreshIntervalSeconds = 60;

    public PricingLimitsCacheRefreshWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // İlk refresh'i bloklamadan arka planda yap; uygulama hemen ayağa kalksın, veritabanı yoksa beklemeyelim
        _ = RefreshAsync(CancellationToken.None);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(RefreshIntervalSeconds), stoppingToken);
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPricingLimitsRepository>();
            var cache = scope.ServiceProvider.GetRequiredService<IPricingLimitsCache>();

            var limits = await repository.FetchAllAsync();
            cache.UpdateLimits(limits);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PricingLimitsCache] Refresh hatası: {ex.Message}");
        }
    }
}
