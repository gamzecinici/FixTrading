using FixTrading.Application.Interfaces.Pricing;

namespace FixTrading.API.BackgroundServices;

// Fiyat limitleri önbelleğini düzenli olarak veritabanından yenilemek için kullanılan arka plan hizmeti.
// Amaç: Her işlemde veritabanına gitmek yerine verileri RAM'de tutarak sistemi hızlandırmak.
public class PricingLimitsCacheRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private const int RefreshIntervalSeconds = 60;

    public PricingLimitsCacheRefreshWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // Arka plan hizmeti çalıştırıldığında ilk olarak bu metot çağrılır. Burada önbelleği düzenli aralıklarla yenilemek için bir döngü oluşturulur.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Uygulama açılışını bekletmeden ilk refresh başlatılır
        _ = RefreshAsync(CancellationToken.None);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(RefreshIntervalSeconds), stoppingToken);
            await RefreshAsync(stoppingToken);
        }
    }

    // Veritabanından fiyat limitlerini çekip önbelleği güncelleyen metot.
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<IPricingLimitsSyncService>();
            await sync.RefreshCacheFromDatabaseAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PricingLimitsCache] Refresh hatası: {ex.Message}");
        }
    }
}
