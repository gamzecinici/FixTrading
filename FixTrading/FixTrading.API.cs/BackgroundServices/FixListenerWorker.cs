using FixTrading.Application.Interfaces.Fix;
using FixTrading.Infrastructure.Fix;
using FixTrading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FixTrading.API.BackgroundServices;

// Bu BackgroundService, uygulama başlatıldığında FIX oturumunu yönetir.
public class FixListenerWorker : BackgroundService
{
    private readonly IFixSession _fixSession;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FixMarketDataOptions _fixOptions;

    public FixListenerWorker(IFixSession fixSession, IServiceScopeFactory scopeFactory, IOptions<FixMarketDataOptions> fixOptions)
    {
        _fixSession = fixSession;
        _scopeFactory = scopeFactory;
        _fixOptions = fixOptions.Value;
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

            var waitCount = 0;
            const int maxWaitSeconds = 600;
            var lastLogMinute = -1;
            while (!_fixSession.IsConnected && !stoppingToken.IsCancellationRequested && waitCount * 500 < maxWaitSeconds * 1000)
            {
                await Task.Delay(500, stoppingToken);
                waitCount++;
                var elapsedMin = (waitCount * 500) / 1000 / 60;
                if (elapsedMin > lastLogMinute && elapsedMin >= 1)
                {
                    lastLogMinute = elapsedMin;
                    Console.WriteLine($"[FIX] Bağlantı bekleniyor ({elapsedMin} dk)...");
                }
            }

            if (stoppingToken.IsCancellationRequested) return;

            if (!_fixSession.IsConnected)
            {
                Console.WriteLine("[FIX] Bağlantı kurulamadı. Arka planda her 15 sn denenecek; bağlanınca otomatik subscribe yapılacak.");
                Console.WriteLine("[FIX] API (LatestPrice, Alerts/Simulate) FIX olmadan da kullanılabilir.");
                _ = DeferredSubscribeWhenConnectedAsync(stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
                return;
            }

            Console.WriteLine("FIX bağlantısı hazır.");
            var delaySec = Math.Max(0, _fixOptions.PostLogonDelaySeconds);
            if (delaySec > 0)
                await Task.Delay(TimeSpan.FromSeconds(delaySec), stoppingToken);
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
            Console.WriteLine(ex.StackTrace);
            // Worker düşmesin, sonsuz bekleyerek process kalsın
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }


    /// <summary>
    /// Uygulama açılışında (veya gecikmeli bağlantı sonrası) FIX'e hangi sembollere abone olunacağını belirler.
    /// </summary>
    /// <remarks>
    /// Eskiden sorgu doğrudan <c>Instruments</c> tablosundaydı: orada kalmış ama limit satırı silinmiş
    /// "yetim" kayıtlar da abonelik listesine giriyordu (kullanıcı pricing_limits_view'da görmüyordu ama konsolda tick vardı).
    /// Şimdi kaynak tek: <c>PricingLimits</c> üzerinden ilişkili <c>Instrument</c> — yani admin panelindeki
    /// "aktif limit" listesi ile FIX abonelik listesi birebir uyumludur.
    /// </remarks>
    private async Task SubscribeInstrumentsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // pricing_limits satırı olan her enstrümanın sembolünü çek (Instrument navigation ile).
            var symbols = await dbContext.PricingLimits
                .AsNoTracking()
                .Include(p => p.Instrument)
                .Where(p => p.Instrument != null && p.Instrument.Symbol.Trim() != "")
                .Select(p => p.Instrument!.Symbol.Trim())
                .Distinct()
                .ToListAsync(stoppingToken);

            if (symbols.Count == 0)
                Console.WriteLine("[FIX] UYARI: Aktif enstrüman (limit tanımlı) bulunamadı.");
            else
                Console.WriteLine($"[FIX] {symbols.Count} sembol aboneliği başlatılıyor: {string.Join(", ", symbols)}");

            // Her sembol QuickFixSession.Subscribe → FixApp.Subscribe zincirine gider
            foreach (var symbol in symbols)
                _fixSession.Subscribe(symbol);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FIX] instruments okuma hatası: {ex.Message}");
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
                var delaySec = Math.Max(0, _fixOptions.PostLogonDelaySeconds);
                if (delaySec > 0)
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), stoppingToken);
                try
                {
                    await SubscribeInstrumentsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FIX] Deferred subscribe hatası: {ex.Message}");
                }
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
