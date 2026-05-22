using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Common.Dtos.Options;
using FixTrading.Infrastructure.Fix;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FixTrading.API.BackgroundServices;

// Bu BackgroundService, uygulama baslatildiginda FIX oturumunu yonetır.
public class FixListenerWorker : BackgroundService
{
    private readonly IFixSession _fixSession;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FixMarketDataOptions _fixOptions;
    private readonly ILogger<FixListenerWorker> _logger;


    // Constructor, gerekli bagimliliklari alir ve sinif icinde kullanilmak uzere saklar.
    public FixListenerWorker(IFixSession fixSession, IServiceScopeFactory scopeFactory, IOptions<FixMarketDataOptions> fixOptions, ILogger<FixListenerWorker> logger)
    {
        _fixSession = fixSession;
        _scopeFactory = scopeFactory;
        _fixOptions = fixOptions.Value;
        _logger = logger;
    }


    // Program acılınca çalışacak arka plan kodu burasıdır.
    //Gerekirse bekler(async), isi bitince tamamlanir(Task) ve program kapanirken düzgün şekilde durabilir(stoppingToken).
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // FIX oturumunu başlat
            _fixSession.Start();

            Console.WriteLine("FIX başlatıldı.");

            var waitOptions = await LoadWaitOptionsAsync();
            await WaitForInitialConnectionAsync(waitOptions, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                return;

            if (!_fixSession.IsConnected)
                await KeepAliveUntilDeferredSubscriptionAsync(stoppingToken);
            else
                await SubscribeAfterLogonDelayAsync(stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Uygulama kapatılırken normal; sessizce cık
        }
        catch (Exception ex)
        {
            Console.WriteLine("FIX Worker hata verdi:");
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            // Worker dusmesin, sonsuz bekleyerek process kalsin
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }

    private async Task<FixWaitOptions> LoadWaitOptionsAsync()
    {
        var options = new FixWaitOptions(600, 500);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var systemParamService = scope.ServiceProvider.GetRequiredService<ISystemParameterService>();
            var config = await systemParamService.GetConfigAsync("FixListenerWorker");
            return ApplyWaitConfig(options, config);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[FIX] FixListenerWorker parametreleri okunamadı; varsayılanlar kullanılacak.");
            return options;
        }
    }

    private static FixWaitOptions ApplyWaitConfig(FixWaitOptions options, Dictionary<string, string>? config)
    {
        if (config == null)
            return options;

        var maxWaitSeconds = options.MaxWaitSeconds;
        var waitIntervalMs = options.WaitIntervalMs;

        if (config.TryGetValue("MaxWaitSeconds", out var maxWaitStr) && int.TryParse(maxWaitStr, out var maxWait))
            maxWaitSeconds = maxWait;
        if (config.TryGetValue("WaitIntervalMs", out var waitIntervalStr) && int.TryParse(waitIntervalStr, out var waitInterval))
            waitIntervalMs = waitInterval;

        return new FixWaitOptions(maxWaitSeconds, waitIntervalMs);
    }

    private async Task WaitForInitialConnectionAsync(FixWaitOptions options, CancellationToken stoppingToken)
    {
        var waitCount = 0;
        var lastLogSecond = -1;
        var fixDiagnosticsLogged = false;

        while (ShouldContinueWaiting(waitCount, options, stoppingToken))
        {
            await Task.Delay(options.WaitIntervalMs, stoppingToken);
            waitCount++;
            var elapsedSec = waitCount * options.WaitIntervalMs / 1000;

            if (!ShouldLogWaitStatus(elapsedSec, lastLogSecond))
                continue;

            lastLogSecond = elapsedSec;
            Console.WriteLine($"[FIX] Bağlantı bekleniyor ({elapsedSec} sn). Detaylar datalog/FIX.4.4-FINTECHEE-SPOTEX.event.current.log dosyasina yaziliyor.");
            if (!fixDiagnosticsLogged)
            {
                fixDiagnosticsLogged = true;
                TryLogFixTcpDiagnostics();
            }
        }
    }

    private bool ShouldContinueWaiting(int waitCount, FixWaitOptions options, CancellationToken stoppingToken)
        => !_fixSession.IsConnected &&
           !stoppingToken.IsCancellationRequested &&
           waitCount * options.WaitIntervalMs < options.MaxWaitSeconds * 1000;

    private static bool ShouldLogWaitStatus(int elapsedSec, int lastLogSecond)
        => elapsedSec > lastLogSecond && elapsedSec >= 15 && elapsedSec % 15 == 0;

    private async Task KeepAliveUntilDeferredSubscriptionAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("[FIX] Bağlantı kurulamadı. Arka planda her 15 sn denenecek; bağlanınca otomatik subscribe yapılacak.");
        Console.WriteLine("[FIX] API (LatestPrice, Alerts/Simulate) FIX olmadan da kullanılabilir.");
        _ = DeferredSubscribeWhenConnectedAsync(stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task SubscribeAfterLogonDelayAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("FIX bağlantısı hazır.");
        var delaySec = Math.Max(0, _fixOptions.PostLogonDelaySeconds);
        if (delaySec > 0)
            await Task.Delay(TimeSpan.FromSeconds(delaySec), stoppingToken);
        await SubscribeInstrumentsAsync(stoppingToken);
    }


    // FIX oturumu kurulduktan sonra, veritabanından aktif enstrümanları cekip her biri için FIX aboneligi başlatir.
    private async Task SubscribeInstrumentsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var pricingQuery = scope.ServiceProvider.GetRequiredService<IPricingLimitsQueryService>();
            var symbols = (await pricingQuery.GetDistinctActiveInstrumentSymbolsAsync(stoppingToken)).ToList();

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


    // Eger başlangıcta baglanti kurulamazsa, bu metod arka planda çalışarak her 15 saniyede bir baglantiyi kontrol eder. Baglanti kuruldugunda sembolleri subscribe eder.
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


    // Uygulama kapanirken FIX oturumunu düzgün sekilde durdurur.
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _fixSession.Stop();
        Console.WriteLine("FIX durduruldu.");
        return base.StopAsync(cancellationToken);
    }

    // Event logdaki son "Connection failed" satirini ve olası TCP (ECONNREFUSED) anlamini aciklar.
    private void TryLogFixTcpDiagnostics()
    {
        try
        {
            var datalog = Path.Combine(AppContext.BaseDirectory, "datalog");
            if (!Directory.Exists(datalog))
            {
                _logger.LogWarning("[FIX] datalog klasoru yok; baglanti hatasi ayrintisi okunamadi.");
                return;
            }

            var logFile = Directory.GetFiles(datalog, "*.event.current.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (logFile != null)
            {
                var lastFail = File.ReadAllLines(logFile)
                    .LastOrDefault(l => l.Contains("Connection failed", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(lastFail))
                    _logger.LogWarning("[FIX] QuickFIX event log: {Line}", lastFail.Trim());
            }

            _logger.LogWarning(
                "[FIX] 'Hedef makine etkin olarak reddetti' genelde bu IP:portta dinleyen servis olmadigini veya guvenlik duvari/VPN eksikligini gosterir. " +
                "Fintechee dokumaninda sifreli baglanti icin Stunnel kullanimi anlatilir; bu durumda fix cfg yerine appsettings FixMarketData: SocketConnectHost/Port ile tunelin yerel adresine (ornegin 127.0.0.1) baglanin. " +
                "Guncel FIX IP/portu icin saglayici ile dogrulayin.");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[FIX] Event log okunurken hata");
        }
    }

    private sealed record FixWaitOptions(int MaxWaitSeconds, int WaitIntervalMs);
}
