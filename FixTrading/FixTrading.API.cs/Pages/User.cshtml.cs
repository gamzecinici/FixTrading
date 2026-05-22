using FixTrading.API.Controllers;
using FixTrading.API.Services;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.Arbitrage;
using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Dtos.Options;
using FixTrading.Common.Dtos.FinancialAnalytics;
using FixTrading.Common.ViewModels.Admin;
using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Infrastructure.MongoDb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Linq;

namespace FixTrading.API.cs.Pages;

//Authorize attribute'u ile sadece "user" ve "admin" rollerine sahip kullanicilarin erisimine izin verilir.
[Authorize(Roles = "user,admin")]

// Kullanici paneli sayfa modeli. Veritabanindan ve MongoDB'den verileri cekerek Razor Page'e saglar.
public class UserModel : PageModel
{

    // DtoMarketData sinifinin MongoDB ile dogru sekilde serilestirilmesi icin BsonClassMap kaydi yapilir.
    static UserModel()
    {
        // DtoMarketData icin daha once mapping tanimlanmis mi kontrol edilir
        if (!BsonClassMap.IsClassMapRegistered(typeof(DtoMarketData)))
        {
            BsonClassMap.RegisterClassMap<DtoMarketData>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }

    private readonly IPricingLimitsQueryService _pricingLimitsQuery;
    private readonly LatestPriceHandler _latestPriceHandler;
    private readonly IMongoCollection<DtoMarketData> _marketDataCollection;
    private readonly FinancialAnalyticsService _financialAnalytics;
    private readonly ISystemParameterService _sysParam;
    private readonly IInstrumentService _instrumentService;
    private readonly IArbitrageService _arbitrageService;


    // UserModel sinifinin constructor'i, gerekli bagimliliklari alir
    public UserModel(
        UserPageServices services,
        UserPageMongoCollections mongoCollections)
    {
        _pricingLimitsQuery = services.PricingLimitsQuery;
        _latestPriceHandler = services.LatestPriceHandler;
        _financialAnalytics = services.FinancialAnalytics;
        _sysParam = services.SysParam;
        _instrumentService = services.InstrumentService;
        _arbitrageService = services.ArbitrageService;
        _marketDataCollection = mongoCollections.MarketData;
    }

    // Aktif sekme, piyasa verileri, fiyat limitleri ve fiyat gecmisi gibi sayfa durumunu tutan ozellikler.
    public string ActiveTab { get; private set; } = "home";
    public List<DtoMarketData> MarketRows { get; private set; } = [];
    public List<PricingLimitRowVm> PricingLimits { get; private set; } = [];
    public List<DtoMarketData> PriceHistory { get; private set; } = [];
    public string SelectedSymbol { get; private set; } = string.Empty;


    /// Sayfa ilk yuklendiginde veya sekme/sembol degistiginde cagrilan metod. Aktif sekmeyi ve sembolu belirler, ardindan verileri yukler.
    public async Task OnGetAsync(string? tab = null, string? symbol = null)
    {
        ActiveTab = NormalizeTab(tab);
        SelectedSymbol = (symbol ?? "").Trim().ToUpper().Replace("/", "");
        await LoadAllAsync();
    }

 
    //Canli piyasa verilerini getiren API endpoint'i. Sadece limit tablosundaki enstrumanlara ait veriler doner.
    public async Task<IActionResult> OnGetLiveMarketAsync()
    {
        var activeSymbols = await _pricingLimitsQuery.GetDistinctActiveInstrumentSymbolsAsync(HttpContext.RequestAborted);
        var activeSet = new HashSet<string>(activeSymbols, StringComparer.OrdinalIgnoreCase);

        var market = await _latestPriceHandler.GetAllLatestAsync();
        var latestBySymbol = market
            .Where(x => activeSet.Contains(x.Symbol))
            .ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

        var rows = activeSymbols
            .OrderBy(x => x)
            .Select(sym =>
            {
                if (latestBySymbol.TryGetValue(sym, out var x))
                    return new { x.Symbol, x.Bid, x.Ask, x.Mid, x.Spread };
                return new { Symbol = sym, Bid = 0m, Ask = 0m, Mid = 0m, Spread = 0m };
            })
            .ToList();
        return new JsonResult(rows);
    }

    // Secilen sembole ait fiyat gecmisini getiren API endpoint'i.
    public async Task<IActionResult> OnGetPriceHistoryAsync(string symbol, string range = "1d")
    {
        if (string.IsNullOrWhiteSpace(symbol)) return new JsonResult(new List<object>());

        // Sembolu normalize et (slash varsa kaldir)
        symbol = symbol.Trim().ToUpper().Replace("/", "");

        // Uzun araliklarda Find().Limit() en yeni tick'leri keser; TR kovalariyla ($dateTrunc) sunucuda grupla.
        var buckets = await PriceHistoryQuery.LoadAggregatedHistoryAsync(
            _marketDataCollection,
            symbol,
            range,
            HttpContext.RequestAborted);

        var result = buckets.Select(x => new
        {
            Time = PriceHistoryQuery.FormatUtcForClient(x.BucketUtc),
            x.Bid,
            x.Ask,
            x.Mid,
            x.Spread
        });

        return new JsonResult(result);
    }

    /// Finansal Analizler: risk + volatilite (tek istek).
    public async Task<IActionResult> OnGetFinancialAnalyticsDataAsync()
    {
        try
        {
            var snap = await _financialAnalytics.GetSnapshotAsync(HttpContext.RequestAborted);
            return new JsonResult(snap);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    // Arbitraj: Tum ana pariteler icin default karsi parite (USDTRY veya ilk uygun) ile tablo snapshot'i.
    // Instrument tablosundaki TUM semboller satir olarak doner; karsi parite dropdown'u icin AvailableCounters da birlikte gelir.
    public async Task<IActionResult> OnGetArbitrageSnapshotAsync()
    {
        try
        {
            var instruments = await _instrumentService.RetrieveAllInstrumentsAsync();
            var prices = await _latestPriceHandler.GetAllLatestAsync();
            var cfg = await _sysParam.GetConfigAsync("FinancialAnalytics");
            var snap = _arbitrageService.BuildSnapshot(instruments, prices, cfg);
            return new JsonResult(snap);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    // Arbitraj: Kullanici dropdown'u degistirdiginde cagrilir. Secilen karsi parite ile tek satir yeniden hesaplanir.
    public async Task<IActionResult> OnGetArbitrageComputeAsync(string main, string counter)
    {
        if (string.IsNullOrWhiteSpace(main))
            return new JsonResult(new { error = "main gerekli" }) { StatusCode = 400 };
        try
        {
            var instruments = await _instrumentService.RetrieveAllInstrumentsAsync();
            var prices = await _latestPriceHandler.GetAllLatestAsync();
            var cfg = await _sysParam.GetConfigAsync("FinancialAnalytics");
            var row = _arbitrageService.Compute(main, counter ?? string.Empty, instruments, prices, cfg);
            return new JsonResult(row);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    // AI Finans Asistanı: Kullanıcının sorusuna göre yorum üretir.
    public async Task<IActionResult> OnPostAIAsistanYorumlaAsync([FromBody] AIAssistantRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserQuestion))
            return new JsonResult(new { error = "Soru boş olamaz." }) { StatusCode = 400 };

        try
        {
            var response = await _financialAnalytics.GetAIInterpretationAsync(request, HttpContext.RequestAborted);
            return new JsonResult(response);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    // Tum verileri yukleyen yardimci metod. Market verileri sadece limit tablosundaki enstrumanlarla sinirlidir.
    private async Task LoadAllAsync()
    {
        var activeSymbols = await _pricingLimitsQuery.GetDistinctActiveInstrumentSymbolsAsync();
        var activeSet = new HashSet<string>(activeSymbols, StringComparer.OrdinalIgnoreCase);

        // Tum guncel piyasa verileri alinir
        var allMarketData = await _latestPriceHandler.GetAllLatestAsync();

        // Sadece aktif sembollere ait veriler filtrelenir
        MarketRows = allMarketData.Where(x => activeSet.Contains(x.Symbol)).ToList();

        // Fiyat limitleri, enstruman sembolune gore siralanarak alinir
        PricingLimits = (await _pricingLimitsQuery.GetLimitRowsOrderedBySymbolAsync()).ToList();

        // Eger aktif sekme "history" ise ve gecerli bir sembol secilmisse, o sembole ait fiyat gecmisi yuklenir
        if (ActiveTab == "history" && !string.IsNullOrWhiteSpace(SelectedSymbol))
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument("Symbol", SelectedSymbol)),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", new BsonDocument
                        {
                            { "year", new BsonDocument("$year", "$Timestamp") },
                            { "month", new BsonDocument("$month", "$Timestamp") },
                            { "day", new BsonDocument("$dayOfMonth", "$Timestamp") },
                            { "hour", new BsonDocument("$hour", "$Timestamp") },
                            { "minute", new BsonDocument("$minute", "$Timestamp") }
                        }
                    },
                    { "lastDoc", new BsonDocument("$last", "$$ROOT") }
                }),
                new BsonDocument("$replaceRoot", new BsonDocument("newRoot", "$lastDoc")),
                new BsonDocument("$sort", new BsonDocument("Timestamp", -1)),
                new BsonDocument("$limit", 100)
            };

            PriceHistory = await _marketDataCollection.Aggregate<DtoMarketData>(pipeline).ToListAsync();
        }
    }

    // Tab parametresini normalize eden yardimci metod. 
    //Guvenlik ve tutarlilik icin sadece "home", "limits" ve "history" degerlerine izin verir, diger tum degerler "home" olarak varsayilanir.
    private static string NormalizeTab(string? tab)
    {
        var allowed = new[] { "home", "limits", "history", "financial" };
        return allowed.Contains(tab ?? "", StringComparer.OrdinalIgnoreCase)
            ? tab!.ToLowerInvariant()
            : "home";
    }
}
