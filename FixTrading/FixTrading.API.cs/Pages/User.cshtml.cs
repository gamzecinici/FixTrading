using FixTrading.API.Controllers;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Domain.Interfaces;
using FixTrading.Infrastructure.MongoDb;
using FixTrading.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace FixTrading.API.cs.Pages;

//Authorize attribute'ü ile sadece "user" ve "admin" rollerine sahip kullanıcıların erişimine izin verilir.
[Authorize(Roles = "user,admin")]

// Kullanıcı paneli sayfa modeli. Veritabanından ve MongoDB'den verileri çekerek Razor Page'e sağlar.
public class UserModel : PageModel
{

    // DtoMarketData sınıfının MongoDB ile doğru şekilde serileştirilmesi için BsonClassMap kaydı yapılır.
    static UserModel()
    {
        // DtoMarketData için daha önce mapping tanımlanmış mı kontrol edilir
        if (!BsonClassMap.IsClassMapRegistered(typeof(DtoMarketData)))
        {
            BsonClassMap.RegisterClassMap<DtoMarketData>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }

    // Bağımlılıklar: Entity Framework DbContext, fiyat verilerini sağlayan handler, MongoDB koleksiyonu.
    private readonly AppDbContext _dbContext;
    private readonly LatestPriceHandler _latestPriceHandler;
    private readonly IMongoCollection<DtoMarketData> _marketDataCollection;


    // UserModel sınıfının constructor'ı, gerekli bağımlılıkları alır
    public UserModel(
        AppDbContext dbContext,
        LatestPriceHandler latestPriceHandler,
        MongoClient mongoClient,
        IOptions<MongoMarketDataOptions> mongoOptions)
    {
        _dbContext = dbContext;
        _latestPriceHandler = latestPriceHandler;
        
        var database = mongoClient.GetDatabase(mongoOptions.Value.DatabaseName);
        _marketDataCollection = database.GetCollection<DtoMarketData>(mongoOptions.Value.CollectionName);
    }

    // Aktif sekme, piyasa verileri, fiyat limitleri ve fiyat geçmişi gibi sayfa durumunu tutan özellikler.
    public string ActiveTab { get; private set; } = "home";
    public List<DtoMarketData> MarketRows { get; private set; } = [];
    public List<PricingLimitRowVm> PricingLimits { get; private set; } = [];
    public List<DtoMarketData> PriceHistory { get; private set; } = [];
    public string SelectedSymbol { get; private set; } = string.Empty;


    /// Sayfa ilk yüklendiğinde veya sekme/sembol değiştiğinde çağrılan metod. Aktif sekmeyi ve sembolü belirler, ardından verileri yükler.
    public async Task OnGetAsync(string? tab = null, string? symbol = null)
    {
        ActiveTab = NormalizeTab(tab);
        SelectedSymbol = (symbol ?? "").Trim().ToUpper().Replace("/", "");
        await LoadAllAsync();
    }

 
    //Canlı piyasa verilerini getiren API endpoint'i. Sadece limit tablosundaki enstrümanlara ait veriler döner.
    public async Task<IActionResult> OnGetLiveMarketAsync()
    {
        var activeSymbols = await _dbContext.PricingLimits
            .AsNoTracking()                                           // EF tracking kapatılır (performans için)
            .Include(x => x.Instrument)                               // Instrument ilişkisi dahil edilir
            .Where(x => x.Instrument != null && x.Instrument.Symbol.Trim() != "") // Geçerli semboller filtrelenir
            .Select(x => x.Instrument!.Symbol.Trim())                // Sadece sembol bilgisi alınır
            .Distinct()                                             // Tekrarlanan semboller kaldırılır
            .ToListAsync();                                        // Liste olarak alınır
        
        var activeSet = new HashSet<string>(activeSymbols, StringComparer.OrdinalIgnoreCase);

        var market = await _latestPriceHandler.GetAllLatestAsync();
        var rows = market
            .Where(x => activeSet.Contains(x.Symbol))
            .OrderBy(x => x.Symbol)
            .Select(x => new
            {
                x.Symbol,
                x.Bid,
                x.Ask,
                x.Mid,
                x.Spread
            })
            .ToList();
        return new JsonResult(rows);
    }

    // Seçilen sembole ait fiyat geçmişini getiren API endpoint'i.
    public async Task<IActionResult> OnGetPriceHistoryAsync(string symbol, string range = "1h")
    {
        if (string.IsNullOrWhiteSpace(symbol)) return new JsonResult(new List<object>());

        // Sembolü normalize et (slash varsa kaldır)
        symbol = symbol.Trim().ToUpper().Replace("/", "");

        DateTime startTime;
        switch (range.ToLower())
        {
            case "1d": startTime = DateTime.UtcNow.AddDays(-1); break;
            case "1w": startTime = DateTime.UtcNow.AddDays(-7); break;
            case "1m": startTime = DateTime.UtcNow.AddMonths(-1); break;
            case "all": startTime = DateTime.UtcNow.AddYears(-1); break;
            case "1h":
            default: startTime = DateTime.UtcNow.AddHours(-1); break;
        }

        // Frontend'de gruplama yapacağımız için ham veriyi (belli bir limit ile) çekiyoruz.
        var filter = Builders<DtoMarketData>.Filter.And(
            Builders<DtoMarketData>.Filter.Eq(x => x.Symbol, symbol),
            Builders<DtoMarketData>.Filter.Gte(x => x.Timestamp, startTime)
        );

        var history = await _marketDataCollection
            .Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Limit(100000) // Frontend'de anlamlı gruplama için yeterli veri.
            .ToListAsync();

        var result = history.Select(x => new
        {
            Time = x.Timestamp.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss"), // Ham zaman formatı
            x.Bid,
            x.Ask,
            x.Mid,
            x.Spread
        });

        return new JsonResult(result);
    }

    // Tüm verileri yükleyen yardımcı metod. Market verileri sadece limit tablosundaki enstrümanlarla sınırlıdır.
    private async Task LoadAllAsync()
    {
        var activeSymbols = await _dbContext.PricingLimits
            .AsNoTracking()
            .Include(x => x.Instrument)
            .Where(x => x.Instrument != null && x.Instrument.Symbol.Trim() != "")
            .Select(x => x.Instrument!.Symbol.Trim())
            .Distinct()
            .ToListAsync();
        var activeSet = new HashSet<string>(activeSymbols, StringComparer.OrdinalIgnoreCase);

        // Tüm güncel piyasa verileri alınır
        var allMarketData = await _latestPriceHandler.GetAllLatestAsync();

        // Sadece aktif sembollere ait veriler filtrelenir
        MarketRows = allMarketData.Where(x => activeSet.Contains(x.Symbol)).ToList();

        // Fiyat limitleri, enstrüman sembolüne göre sıralanarak alınır
        PricingLimits = await _dbContext.PricingLimits
            .AsNoTracking()
            .Include(x => x.Instrument)
            .OrderBy(x => x.Instrument != null ? x.Instrument.Symbol : string.Empty)
            .Select(x => new PricingLimitRowVm
            {
                Id = x.Id,
                Symbol = x.Instrument != null ? x.Instrument.Symbol : "-",
                MinMid = x.MinMid,
                MaxMid = x.MaxMid,
                MaxSpread = x.MaxSpread
            })
            .ToListAsync();

        // Eğer aktif sekme "history" ise ve geçerli bir sembol seçilmişse, o sembole ait fiyat geçmişi yüklenir
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

    // Tab parametresini normalize eden yardımcı metod. 
    //Guvenlik ve tutarlılık için sadece "home", "limits" ve "history" değerlerine izin verir, diğer tüm değerler "home" olarak varsayılanır.
    private static string NormalizeTab(string? tab)
    {
        var allowed = new[] { "home", "limits", "history" };
        return allowed.Contains(tab ?? "", StringComparer.OrdinalIgnoreCase)
            ? tab!.ToLowerInvariant()
            : "home";
    }
}
