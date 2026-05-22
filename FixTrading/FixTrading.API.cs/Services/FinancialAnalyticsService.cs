using FixTrading.API.Controllers;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.Arbitrage;
using FixTrading.Application.Interfaces.FinancialAnalytics;
using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Common.Dtos.Arbitrage;
using FixTrading.Common.Dtos.FinancialAnalytics;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Dtos.Options;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Linq;

namespace FixTrading.API.Services;


// Canli fiyat verilerine dayali risk, volatilite ve arbitraj metrikleri ureten servis.
public sealed class FinancialAnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LatestPriceHandler _latestPrice;
    private readonly IMongoCollection<DtoMarketData> _ticks;
    private readonly ISystemParameterService _sysParam;
    private readonly IArbitrageService _arbitrage;
    private readonly IVolatilityAnalyticsService _volatility;
    private readonly IRiskAnalyticsService _risk;
    private readonly IAIFinanceAssistantService _aiAssistant;

    static FinancialAnalyticsService()
    {
        // Servise ozgu defansif kayit — sayfa model static ctor'undan bagimsiz calisir
        if (!BsonClassMap.IsClassMapRegistered(typeof(DtoMarketData)))
        {
            //Mongodan gelen veri class a otomatik mapler, fazladan gelen alanlari yoksayar
            BsonClassMap.RegisterClassMap<DtoMarketData>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }

    public FinancialAnalyticsService(
        IServiceScopeFactory scopeFactory,
        LatestPriceHandler latestPrice,
        ISystemParameterService sysParam,
        FinancialAnalyticsMongoSource mongoSource,
        FinancialAnalyticsCalculators calculators)
    {
        _scopeFactory = scopeFactory;
        _latestPrice = latestPrice;
        _sysParam = sysParam;
        _arbitrage = calculators.Arbitrage;
        _volatility = calculators.Volatility;
        _risk = calculators.Risk;
        _aiAssistant = calculators.AiAssistant;
        _ticks = mongoSource.Ticks;

        // Arka planda sembol ve zamana gore index olustur (sorgu hizi icin kritik)
        _ = _ticks.Indexes.CreateOneAsync(
            new CreateIndexModel<DtoMarketData>(
                Builders<DtoMarketData>.IndexKeys.Ascending(x => x.Symbol).Descending(x => x.Timestamp)
            )
        );
    }

    // Risk, volatilite ve arbitraj metriklerini hesaplayip tek bir snapshot olarak dondurur.
    public async Task<FinancialAnalyticsSnapshotDto> GetSnapshotAsync(CancellationToken ct = default)
    {
        try
        {
            var (risk, vol, arb) = await BuildSnapshotAsync(ct);
            var riskList = risk.ToList();
            var volList  = vol.ToList();
            return new FinancialAnalyticsSnapshotDto
            {
                Risk       = riskList,
                Volatility = volList,
                Arbitrage  = arb,
                Anomalies  = BuildAnomalyCards(riskList, volList, arb)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FinancialAnalytics] HATA: {ex.Message}");
            return new FinancialAnalyticsSnapshotDto();
        }
    }

    // Anlık risk, volatilite ve arbitraj verisinden gerçek anomali kartları üretir.
    private static List<AnomalyCardDto> BuildAnomalyCards(
        IReadOnlyList<FinancialRiskRowDto> riskRows,
        IReadOnlyList<FinancialVolatilityRowDto> volRows,
        ArbitrageSnapshotDto arb)
    {
        var cards = new List<AnomalyCardDto>();
        var now   = DateTime.Now.ToString("HH:mm");

        // Yüksek riskli semboller (en yüksek skordan başlayarak max 2 kart)
        foreach (var r in riskRows.Where(r => r.LevelKey == "yuksek")
                                  .OrderByDescending(r => r.RiskScore)
                                  .Take(2))
        {
            cards.Add(new AnomalyCardDto
            {
                Type        = "risk",
                Title       = "Risk Seviyesi Yüksek",
                Symbol      = r.Symbol,
                Description = $"Risk skoru {r.RiskScore:F0} seviyesinde. {r.RecommendedAction}",
                Badge       = "Yüksek Risk",
                Time        = now
            });
        }

        // Yüksek volatiliteli semboller (max 1 kart, risk kartından farklı sembol tercih edilir)
        var riskSymbols = cards.Select(c => c.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var highVol = volRows.Where(v => v.LevelKey == "yuksek")
                             .OrderByDescending(v => v.VolatilityValue)
                             .FirstOrDefault(v => !riskSymbols.Contains(v.Symbol))
                  ?? volRows.Where(v => v.LevelKey == "yuksek")
                             .OrderByDescending(v => v.VolatilityValue)
                             .FirstOrDefault();
        if (highVol is not null)
        {
            cards.Add(new AnomalyCardDto
            {
                Type        = "vol",
                Title       = "Yüksek Volatilite",
                Symbol      = highVol.Symbol,
                Description = $"Volatilite değeri {highVol.VolatilityValue:F2} seviyesine ulaştı. {highVol.RecommendedAction}",
                Badge       = "Dikkat",
                Time        = now
            });
        }

        // Aktif arbitraj fırsatları (max 1 kart)
        var arbOp = arb?.Rows?.Where(r => r.SignalKey != "none")
                               .OrderByDescending(r => Math.Abs(r.DiffPercent ?? 0))
                               .FirstOrDefault();
        if (arbOp is not null)
        {
            cards.Add(new AnomalyCardDto
            {
                Type        = "arb",
                Title       = "Arbitraj Fırsatı",
                Symbol      = arbOp.MainSymbol,
                Description = $"{arbOp.MainSymbol}/{arbOp.CounterSymbol} çiftinde %{arbOp.DiffPercent:F2} fark tespit edildi. Sinyal: {arbOp.Signal}",
                Badge       = "Fırsat",
                Time        = now
            });
        }

        return cards;
    }

    // Kullanıcı sorusunu alır, güncel finansal analiz snapshot'u ile birleştirir ve yapay zeka asistanından anlamlı bir yanıt üretmesini ister.
    public async Task<AIAssistantResponseDto> GetAIInterpretationAsync(AIAssistantRequestDto request, CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(ct);
        return await _aiAssistant.GenerateResponseAsync(request.UserQuestion, snapshot, request.SelectedSymbol);
    }


    // Veritabanindan aktif sembolleri, son fiyatlari ve son ticklerden hesaplanan volatilite metriklerini alip
    // risk, volatilite ve arbitraj satirlari olusturur.
    private async Task<(IReadOnlyList<FinancialRiskRowDto> Risk, IReadOnlyList<FinancialVolatilityRowDto> Volatility, ArbitrageSnapshotDto Arbitrage)> BuildSnapshotAsync(
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var limitsQuery = scope.ServiceProvider.GetRequiredService<IPricingLimitsQueryService>();

        // Parametre tablosundan konfigürasyonu çek
        var cfg = await _sysParam.GetConfigAsync("FinancialAnalytics") ?? new Dictionary<string, string>();

        var limitRows = await limitsQuery.GetActiveLimitsForFinancialAnalyticsAsync(ct);

        if (limitRows.Count == 0)
            return ([], [], new ArbitrageSnapshotDto());

        var limitBySymbol = new Dictionary<string, FinancialActiveLimitRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var l in limitRows)
        {
            var sym = l.Symbol.Trim();
            if (!limitBySymbol.ContainsKey(sym))
                limitBySymbol[sym] = l;
        }

        //Tum sembolleri alfabetik siraya gore isleriz, bu sayede risk ve volatilite tablolarinda ayni siralama olur
        var symbols = limitBySymbol.Keys.OrderBy(s => s).ToList();

        var latestAll = await _latestPrice.GetAllLatestAsync();
        var latestBySymbol = latestAll.ToDictionary(x => x.Symbol.Trim(), StringComparer.OrdinalIgnoreCase);


        var volInputs = await BuildVolatilityInputsAsync(symbols, cfg, ct);
        var volRows = BuildVolatilityRows(symbols, volInputs, cfg);
        var riskRows = BuildRiskRows(symbols, limitBySymbol, latestBySymbol, volInputs, cfg);

        // Arbitraj: IInstrumentService scoped oldugu icin scope uzerinden cozulur.
        var instrumentService = scope.ServiceProvider.GetRequiredService<IInstrumentService>();
        var instruments = await instrumentService.RetrieveAllInstrumentsAsync();
        var prices = await _latestPrice.GetAllLatestAsync();
        var arbSnap = _arbitrage.BuildSnapshot(instruments, prices, cfg);

        return (riskRows, volRows, arbSnap);
    }

    // Arbitraj servisindeki "tek satir hesaplama" yaklasimina benzer sekilde
    // once tum volatility girdilerini toplayip sonra satirlari tek bir helper ile uretir.
    private async Task<Dictionary<string, VolatilityMetrics>> BuildVolatilityInputsAsync(
        IReadOnlyList<string> symbols,
        Dictionary<string, string> cfg,
        CancellationToken ct)
    {
        int sampleSize = TryGet(cfg, "VolatilitySampleSize", 50);
        var result = new Dictionary<string, VolatilityMetrics>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in symbols)
        {
            var mids = await FetchLastMidsAsync(symbol, sampleSize, ct);
            var metrics = _volatility.ComputeMetrics(mids, cfg);
            result[symbol] = metrics;
        }

        return result;
    }

    private List<FinancialVolatilityRowDto> BuildVolatilityRows(
        IReadOnlyList<string> symbols,
        IReadOnlyDictionary<string, VolatilityMetrics> volInputs,
        Dictionary<string, string> cfg)
    {
        var rows = new List<FinancialVolatilityRowDto>(symbols.Count);
        foreach (var symbol in symbols)
        {
            volInputs.TryGetValue(symbol, out var input);
            rows.Add(_volatility.BuildRow(symbol, input, cfg));
        }

        return rows;
    }

    private List<FinancialRiskRowDto> BuildRiskRows(
        IReadOnlyList<string> symbols,
        IReadOnlyDictionary<string, FinancialActiveLimitRow> limitBySymbol,
        IReadOnlyDictionary<string, DtoMarketData> latestBySymbol,
        IReadOnlyDictionary<string, VolatilityMetrics> volInputs,
        Dictionary<string, string> cfg)
    {
        var rows = new List<FinancialRiskRowDto>(symbols.Count);
        foreach (var symbol in symbols)
        {
            limitBySymbol.TryGetValue(symbol, out var lim);
            latestBySymbol.TryGetValue(symbol, out var latest);
            volInputs.TryGetValue(symbol, out var input);
            rows.Add(_risk.BuildRow(symbol, latest, lim, input, cfg));
        }

        return rows;
    }


    // MongoDB'den son tickleri alip orta fiyatlari ceker, volatilite hesaplamasi icin kullanilir.
    private async Task<List<decimal>> FetchLastMidsAsync(string symbol, int take, CancellationToken ct)
    {
        var filter = Builders<DtoMarketData>.Filter.Eq(x => x.Symbol, symbol);
        var docs = await _ticks
            .Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Limit(take)
            .ToListAsync(ct);

        if (docs.Count == 0)
            return [];

        docs.Reverse();
        return docs.Select(d => d.Mid).ToList();
    }


    private static int TryGet(Dictionary<string, string> cfg, string key, int defaultValue)
    {
        if (cfg.TryGetValue(key, out var val) && int.TryParse(val, out var res)) return res;
        return defaultValue;
    }
}
