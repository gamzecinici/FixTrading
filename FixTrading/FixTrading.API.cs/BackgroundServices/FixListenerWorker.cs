using FixTrading.Application.Interfaces.Fix;
using FixTrading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FixTrading.API.BackgroundServices;

// Bu BackgroundService, uygulama başlatıldığında FIX oturumunu yönetir.
public class FixListenerWorker : BackgroundService
{
    private readonly IFixSession _fixSession;
    private readonly IServiceScopeFactory _scopeFactory;

    // Constructor, IFixSession ve IServiceScopeFactory bağımlılıklarını alır.
    public FixListenerWorker(IFixSession fixSession, IServiceScopeFactory scopeFactory)
    {
        _fixSession = fixSession;
        _scopeFactory = scopeFactory;
    }


    // Program açılınca çalışacak arka plan kodu burasıdır.
    //Gerekirse bekler(async), işi bitince tamamlanır(Task) ve program kapanırken düzgün şekilde durabilir(stoppingToken).
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // FIX oturumunu başlat
            _fixSession.Start();

            Console.WriteLine("FIX başlatıldı.");

            // Bağlantı kurulana kadar bekle (max 10 dakika, sonra arka planda tekrar dener)
            var waitCount = 0;
            const int maxWaitSeconds = 600; // 10 dakika
            while (!_fixSession.IsConnected && !stoppingToken.IsCancellationRequested && waitCount * 500 < maxWaitSeconds * 1000)
            {
                await Task.Delay(500, stoppingToken);
                waitCount++;
                if (waitCount % 6 == 0)
                    Console.WriteLine("[FIX] Sunucuya bağlantı bekleniyor... (fix.cfg: SocketConnectHost/Port kontrol edin)");
            }

            if (stoppingToken.IsCancellationRequested) return;

            if (!_fixSession.IsConnected)
            {
                Console.WriteLine("[FIX] Bağlantı zaman aşımı. Arka planda bağlantı bekleniyor, bağlanınca otomatik subscribe yapılacak.");
                _ = DeferredSubscribeWhenConnectedAsync(stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
                return;
            }

            Console.WriteLine("FIX bağlantısı hazır.");
            await SubscribeInstrumentsAsync(stoppingToken);

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


    // Bu metod, veritabanından sembolleri okuyup FIX oturumuna subscribe eder.
    private async Task SubscribeInstrumentsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var symbols = await dbContext.Instruments
            .AsNoTracking()
            .Select(i => i.Symbol.Trim())
            .Where(s => s != "")
            .Distinct()
            .ToListAsync(stoppingToken);

        Console.WriteLine($"[FIX] instruments tablosundan {symbols.Count} sembol okundu.");
        if (symbols.Count == 0)
            Console.WriteLine("[FIX] UYARI: instruments tablosu boş - subscribe gönderilmeyecek, market data gelmez.");

        foreach (var symbol in symbols)
        {
            Console.WriteLine($"Instrument tablosundan subscribe: {symbol}");
            _fixSession.Subscribe(symbol);
        }
    }


    // Eğer başlangıçta bağlantı kurulamazsa, bu metod arka planda çalışarak her 15 saniyede bir bağlantıyı kontrol eder. Bağlantı kurulduğunda sembolleri subscribe eder.
    private async Task DeferredSubscribeWhenConnectedAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            if (_fixSession.IsConnected)
            {
                Console.WriteLine("[FIX] Bağlantı kuruldu, semboller subscribe ediliyor...");
                await SubscribeInstrumentsAsync(stoppingToken);
                return;
            }
        }
    }


    // Uygulama kapanırken FIX oturumunu düzgün şekilde durdurur.
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _fixSession.Stop();
        Console.WriteLine("FIX durduruldu.");
        return base.StopAsync(cancellationToken);
    }
}
