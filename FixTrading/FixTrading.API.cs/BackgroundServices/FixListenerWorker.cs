using FixTrading.Application.Interfaces.Fix;
using FixTrading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FixTrading.API.BackgroundServices;

/// <summary>
/// FIX protokolü dinleyicisi; arka planda sürekli çalışır.
/// IFixSession üzerinden bağlantıyı yönetir.
/// </summary>
public class FixListenerWorker : BackgroundService
{
    private readonly IFixSession _fixSession;
    private readonly IServiceScopeFactory _scopeFactory;

    public FixListenerWorker(IFixSession fixSession, IServiceScopeFactory scopeFactory)
    {
        _fixSession = fixSession;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // FIX oturumunu başlat
            _fixSession.Start();

            Console.WriteLine("FIX başlatıldı.");

            // Bağlantı kurulana kadar bekle
            while (!_fixSession.IsConnected && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(500, stoppingToken);
            }

            Console.WriteLine("FIX bağlantısı hazır.");

            // instrument tablosundaki tüm semboller için otomatik subscribe at
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var symbols = await dbContext.Instruments
                    .AsNoTracking()
                    .Select(i => i.Symbol.Trim())
                    .Where(s => s != "")
                    .Distinct()
                    .ToListAsync(stoppingToken);

                foreach (var symbol in symbols)
                {
                    Console.WriteLine($"Instrument tablosundan subscribe: {symbol}");
                    _fixSession.Subscribe(symbol);
                }
            }

            // Uygulama kapanana kadar çalışmaya devam et
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Uygulama kapatılırken normal; sessizce çık
        }
        catch (Exception ex)
        {
            Console.WriteLine("FIX Worker hata verdi:");
            Console.WriteLine(ex.Message);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // FIX oturumunu temiz şekilde kapat
        _fixSession.Stop();
        Console.WriteLine("FIX durduruldu.");
        return base.StopAsync(cancellationToken);
    }
}
