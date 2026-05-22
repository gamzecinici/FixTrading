using FixTrading.API;
using FixTrading.API.Controllers;
using FixTrading.API.Services;
using FixTrading.Application.Contracts;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.Arbitrage;
using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Application.Interfaces.Users;
using FixTrading.Common.Dtos.Alert;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Dtos.Options;
using FixTrading.Common.Dtos.FinancialAnalytics;
using FixTrading.Common.ViewModels.Admin;
using FixTrading.Infrastructure.MongoDb;
using FixTrading.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Linq;

namespace FixTrading.API.cs.Pages;


// admin kullanicilari icin yonetim paneli
[Authorize(Roles = "admin")]
public class AdminModel : PageModel
{
    // MongoDB her dokumanda _id uretir; C# modelinde karsiligi yoksa deserialization patlar.
    // Razor Page handler'larinda aggregate sonucu okumadan once bir kez kayit yeterli.
    static AdminModel()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(DtoMarketData)))
        {
            BsonClassMap.RegisterClassMap<DtoMarketData>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }

    private readonly LatestPriceHandler _latestPriceHandler;
    private readonly HealthCheckService _healthCheckService;
    private readonly IMongoCollection<DtoAlert> _alertsCollection;
    private readonly IPricingLimitsSyncService _pricingLimitsSync;
    private readonly IPricingLimitsQueryService _pricingLimitsQuery;
    private readonly IPricingLimitMutationService _pricingLimitMutation;
    private readonly IUserAccountService _userAccountService;
    private readonly IMongoCollection<DtoMarketData> _marketDataCollection;
    private readonly ISystemParameterService _systemParameterService;
    private readonly FinancialAnalyticsService _financialAnalytics;
    private readonly IInstrumentService _instrumentService;
    private readonly IArbitrageService _arbitrageService;

    public AdminModel(
        AdminPageServices services,
        AdminPageMongoCollections mongoCollections,
        AdminPageOperations operations)
    {
        _latestPriceHandler = services.LatestPriceHandler;
        _healthCheckService = services.HealthCheckService;
        _financialAnalytics = services.FinancialAnalytics;
        _instrumentService = services.InstrumentService;
        _arbitrageService = services.ArbitrageService;
        _pricingLimitsSync = operations.PricingLimitsSync;
        _pricingLimitsQuery = operations.PricingLimitsQuery;
        _pricingLimitMutation = operations.PricingLimitMutation;
        _userAccountService = operations.UserAccountService;
        _systemParameterService = operations.SystemParameterService;
        _alertsCollection = mongoCollections.Alerts;
        _marketDataCollection = mongoCollections.MarketData;
    }

    public string ActiveTab { get; private set; } = "home";                     //Aktif sekmeyi tutan ozellik, varsayilan olarak "home" olarak ayarlanir
    public List<ServiceHealthVm> HealthServices { get; private set; } = [];    //Uygulamanin saglik durumunu gostermek icin kullanilan hizmetlerin listesini tutar
    public List<DtoMarketData> MarketRows { get; private set; } = [];          //Canli piyasa verilerini tutan liste
    public List<UserListRecord> Users { get; private set; } = [];                 //Kullanicilari tutan liste
    public List<PricingLimitRowVm> PricingLimits { get; private set; } = [];  //Fiyatlandirma limitlerini tutan liste
    public List<DtoAlert> Alerts { get; private set; } = [];                  //Uyarilari tutan liste
    public List<DtoMarketData> PriceHistory { get; private set; } = [];       //Fiyat gecmisini tutan liste
    public List<SystemParameterEntity> SystemParameters { get; private set; } = []; //Sistem parametrelerini tutan liste
    public string SelectedSymbol { get; private set; } = string.Empty;        //Secilen sembolu tutan ozellik


    //UI’dan gelen form verisini backend’e baglar
    [BindProperty]
    public AddUserInput NewUser { get; set; } = new();


    //Sayfa yuklendiginde calisir, aktif sekmeyi belirler ve tum verileri yukler
    public async Task OnGetAsync(string? tab = null, string? symbol = null)
    {
        ActiveTab = NormalizeTab(tab);
        SelectedSymbol = (symbol ?? "").Trim().ToUpper().Replace("/", "");
        await LoadAllAsync();
    }


   //Canli piyasa verilerini getiren API endpoint'i
    public async Task<IActionResult> OnGetLiveMarketAsync()
    {
        // Canli piyasa seridi icin sadece limit tablosundaki enstrumanlara ait veriler doner, boylece UI'da limit tanimli olmayan semboller gozukmez.
        var activeSymbols = await _pricingLimitsQuery.GetDistinctActiveInstrumentSymbolsAsync(HttpContext.RequestAborted);
        // O(1) sembol aramasi; LatestPriceHandler'daki Symbol ile OrdinalIgnoreCase eslesir
        var activeSet = new HashSet<string>(activeSymbols, StringComparer.OrdinalIgnoreCase);

        //tum guncel piyasa verilerini cekiyoruz
        var market = await _latestPriceHandler.GetAllLatestAsync();
        var latestBySymbol = market
            .Where(x => activeSet.Contains(x.Symbol))
            .ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

        // Limiti olan tum semboller doner; henuz tick gelmemisse sifir degerler (UI satir/strip olusturabilsin).
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

    //Belirli bir sembolun fiyat gecmisini getiren API endpoint'i, zaman araligina gore filtreleme yapar
    public async Task<IActionResult> OnGetPriceHistoryAsync(string symbol, string range = "1d")
    {
        if (string.IsNullOrWhiteSpace(symbol)) return new JsonResult(new List<object>());

        // Sembolu normalize et (slash varsa kaldir)
        symbol = symbol.Trim().ToUpper().Replace("/", "");

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
    // Partial (_FinancialAnalytics) hem User hem Admin tarafindan kullanildigi icin, Admin sayfasinin da ayni handler'a sahip olmasi gerekir.
    public async Task<IActionResult> OnGetArbitrageSnapshotAsync()
    {
        try
        {
            var instruments = await _instrumentService.RetrieveAllInstrumentsAsync();
            var prices = await _latestPriceHandler.GetAllLatestAsync();
            var cfg = await _systemParameterService.GetConfigAsync("FinancialAnalytics");
            var snap = _arbitrageService.BuildSnapshot(instruments, prices, cfg);
            return new JsonResult(snap);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    // Arbitraj: Dropdown degisiminde / periyodik yenilemede tek bir satiri yeniden hesaplar.
    public async Task<IActionResult> OnGetArbitrageComputeAsync(string main, string counter)
    {
        if (string.IsNullOrWhiteSpace(main))
            return new JsonResult(new { error = "main gerekli" }) { StatusCode = 400 };
        try
        {
            var instruments = await _instrumentService.RetrieveAllInstrumentsAsync();
            var prices = await _latestPriceHandler.GetAllLatestAsync();
            var cfg = await _systemParameterService.GetConfigAsync("FinancialAnalytics");
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


    //Yeni bir kullanici eklemek icin kullanilan endpoint,
    //Form verilerini dogrular, e-posta adresinin benzersiz oldugunu kontrol eder, sifreyi hash'ler ve kullaniciyi veritabanina ekler
    public async Task<IActionResult> OnPostAddUserAsync(string? tab = null)
    {

        //Form verilerini dogrular, gerekli alanlarin bos olmadigindan emin olur
        if (string.IsNullOrWhiteSpace(NewUser.FullName) ||
            string.IsNullOrWhiteSpace(NewUser.Email) ||
            string.IsNullOrWhiteSpace(NewUser.Password))
        {
            return RedirectToPage(new { tab = "users" });    //Gerekli alanlar saglanmazsa kullanici sekmesine yonlendirir
        }

        var normalizedEmail = NewUser.Email.Trim().ToLowerInvariant();    //Ayni e-posta adresinin farkli bicimlerde girilmesini onlemek icin e-posta adresini normallestirir
        var exists = await _userAccountService.IsEmailRegisteredAsync(normalizedEmail, HttpContext.RequestAborted);
        if (exists)
        {
            return RedirectToPage(new { tab = "users" });
        }

        await _userAccountService.AddUserAsync(
            NewUser.FullName.Trim(),
            normalizedEmail,
            BCrypt.Net.BCrypt.HashPassword(NewUser.Password),
            "user",
            HttpContext.RequestAborted);
        return RedirectToPage(new { tab = "users" });
    }


    //Bir kullaniciyi silmek icin kullanilan endpoint, kullaniciyi veritabanindan kaldirir
    public async Task<IActionResult> OnPostDeleteUserAsync(int id)
    {
        await _userAccountService.DeleteUserAsync(id, HttpContext.RequestAborted);

        return RedirectToPage(new { tab = "users" });                                //Kullaniciyi sildikten sonra kullanici sekmesine yonlendirir
    }


    //Bir kullanicinin rolunu degistirmek icin kullanilan endpoint, kullaniciyi veritabaninda bulur ve rolunu "admin" ile "user" arasinda gecis yapar
    public async Task<IActionResult> OnPostToggleUserRoleAsync(int id)
    {
        await _userAccountService.ToggleUserRoleAsync(id, HttpContext.RequestAborted);

        return RedirectToPage(new { tab = "users" });
    }

    //Fiyatlandirma limitlerini guncellemek icin kullanilan endpoint, limit kaydini veritabaninda bulur ve yeni degerlerle gunceller
    //Parametreler string alinir ve InvariantCulture ile parse edilir — sunucu locale'i (Turkce) decimal binding'i bozmasin diye
    public async Task<IActionResult> OnPostUpdateLimitAsync(Guid id, string? minMid, string? maxMid, string? maxSpread)
    {
        decimal ParseSafe(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            var raw = s.Trim().Replace(" ", "");

            // Son ayırıcıya göre format tespit et
            var lastDot   = raw.LastIndexOf('.');
            var lastComma = raw.LastIndexOf(',');

            if (lastComma > lastDot)
            {
                // TR formatı (1.234,56): noktaları sil, virgülü noktaya çevir
                raw = raw.Replace(".", "").Replace(",", ".");
            }
            else
            {
                // EN formatı (1,234.56) veya InvariantCulture: virgülleri sil
                raw = raw.Replace(",", "");
            }

            var style = System.Globalization.NumberStyles.AllowDecimalPoint |
                        System.Globalization.NumberStyles.AllowLeadingSign;

            return decimal.TryParse(raw, style, System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;
        }

        //Girilen degerleri guvenli bir sekilde parse eder, gecersiz girislerde 0 dondurur, boylece hatali veri girisi durumunda limitler sifirlanmaz
        var minVal = ParseSafe(minMid);
        var maxVal = ParseSafe(maxMid);
        var spreadVal = ParseSafe(maxSpread);

        if (minMid == null || maxMid == null || maxSpread == null)
        {
            TempData["LimitError"] = "Geçersiz veri girişi.";
            return RedirectToPage(new { tab = "limits" });
        }

        const decimal maxAllowed = 9_999_999_999m;
        if (minVal < 0 || minVal > maxAllowed ||
            maxVal < 0 || maxVal > maxAllowed ||
            spreadVal < 0 || spreadVal > maxAllowed ||
            minVal > maxVal)
        {
            TempData["LimitError"] = "Geçersiz değer: limitler 0–9,999,999,999 arasında olmalı ve MinMid <= MaxMid olmalıdır.";
            return RedirectToPage(new { tab = "limits" });
        }

        var auditName = CurrentUserAudit.GetDisplayNameForAudit(User);
        if (string.IsNullOrEmpty(auditName))
        {
            TempData["LimitError"] = "Oturum kullanıcı bilgisi alınamadı. Lütfen yeniden giriş yapın.";
            return RedirectToPage(new { tab = "limits" });
        }

        var updated = await _pricingLimitMutation.TryUpdatePricingLimitAsync(
            id, minVal, maxVal, spreadVal, auditName, HttpContext.RequestAborted);
        if (updated)
            await _pricingLimitsSync.RefreshCacheFromDatabaseAsync(HttpContext.RequestAborted);

        return RedirectToPage(new { tab = "limits" });
    }

    //Sistem parametrelerini guncellemek icin kullanilan endpoint, parametre adina gore ilgili kaydi gunceller
    public async Task<IActionResult> OnPostUpdateSystemParameterAsync(string fileName, string configJson)
    {
        //Oturum acmis kullanici bilgisini audit icin alir, eger kullanici bilgisi yoksa hata verir ve parametreler sekmesine yonlendirir
        var auditName = CurrentUserAudit.GetDisplayNameForAudit(User);
        if (string.IsNullOrEmpty(auditName))
        {
            TempData["ParamError"] = "Oturum kullanıcı bilgisi alınamadı.";
            return RedirectToPage(new { tab = "params" });
        }

        try
        {
            //Gelen JSON string'i parse ediyoruz (string → JSON obje)
            using var doc = System.Text.Json.JsonDocument.Parse(configJson);
            var dict = doc.RootElement.EnumerateObject()
                .ToDictionary(
                    p => p.Name,
                    p => p.Value.ValueKind == System.Text.Json.JsonValueKind.String
                        ? p.Value.GetString()!
                        : p.Value.GetRawText()
                );
            var ok = await _systemParameterService.UpdateConfigAsync(fileName, dict, auditName);
            if (!ok) TempData["ParamError"] = "Parametre güncellenemedi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ParamError"] = ex.Message;
        }
        catch (Exception ex)
        {
            TempData["ParamError"] = "Geçersiz JSON formatı: " + ex.Message;
        }

        //Guncelleme islemi tamamlandiktan sonra parametreler sekmesine yonlendirir, eger hata varsa TempData ile hata mesajini gosterir
        return RedirectToPage(new { tab = "params" });
    }

    // Birden fazla parametreyi toplu şekilde güncelleyen POST metodu 
    public async Task<IActionResult> OnPostBatchUpdateParamsAsync([FromBody] Dictionary<string, string> updates)
    {
        var auditName = CurrentUserAudit.GetDisplayNameForAudit(User);
        if (string.IsNullOrEmpty(auditName))
            return new JsonResult(new { ok = false, error = "Oturum kullanıcı bilgisi alınamadı." });

        if (updates == null || updates.Count == 0)
            return new JsonResult(new { ok = true });

        try
        {
            var ok = await _systemParameterService.BatchUpdateConfigsAsync(updates, auditName);
            return new JsonResult(new { ok });
        }
        catch (InvalidOperationException ex)
        {
            return new JsonResult(new { ok = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostRefreshParamsCacheAsync()
    {
        // Redis cache'ini temizlemek için BatchUpdateParams ile boş bir güncelleme göndermek 
        // veya servis üzerinden doğrudan temizlemek gerekebilir.
        // Mevcut BatchUpdateConfigsAsync zaten RedisAllFilesKey'i siliyor.
        var auditName = CurrentUserAudit.GetDisplayNameForAudit(User);
        await _systemParameterService.BatchUpdateConfigsAsync(new Dictionary<string, string>(), auditName ?? "System");
        
        return RedirectToPage(new { tab = "params" });
    }


    // Tum verileri yukleyen yardimci yontem, aktif sekmeye gore ilgili verileri ceker ve sayfa yenilendiginde UI'nin guncel kalmasini saglar
    private async Task LoadAllAsync()
    {
        await LoadHealthAsync();

        // Canli piyasa seridi + ilk tablo render'i: onbellekte veri olsa bile yalnizca "limiti tanimli" semboller.
        // instruments tablosunda yetim kayit kalsa bile (limit satiri yoksa) UI'da listelenmez.
        var activeSymbols = await _pricingLimitsQuery.GetDistinctActiveInstrumentSymbolsAsync();
        var activeSet = new HashSet<string>(activeSymbols, StringComparer.OrdinalIgnoreCase);

        var allMarketData = await _latestPriceHandler.GetAllLatestAsync();
        MarketRows = allMarketData.Where(x => activeSet.Contains(x.Symbol)).ToList();

        // Redis-first: UI katmanında tüm parametre listesini servis üzerinden çekiyoruz.
        // Bu sayede listenin kendisi de Redis'ten geliyor, gereksiz DB erişimi tamamen engelleniyor.
        var allParams = await _systemParameterService.GetAllParametersAsync();
        SystemParameters = allParams.Cast<SystemParameterEntity>().ToList();

        Users = (await _userAccountService.ListUsersOrderedByNameAsync()).ToList();

        PricingLimits = (await _pricingLimitsQuery.GetLimitRowsOrderedBySymbolAsync()).ToList();

        Alerts = await _alertsCollection
            .Find(Builders<DtoAlert>.Filter.Empty)      //Tum uyarilari getirir
            .SortByDescending(x => x.Time)              //Uyarilari zamana gore azalan sirada siralar, boylece en yeni uyarilar once gelir
            .Limit(150)
            .ToListAsync();

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

    //Uygulamanin saglik durumunu kontrol eder ve HealthServices listesini gunceller, her hizmetin saglikli olup olmadigini belirler
    private async Task LoadHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();                //Saglik durumunu kontrol eder ve raporu alir
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) 
        {

            //Saglik raporundaki anahtarlari kullanici dostu hizmet adlarina esler, boylece UI'da daha okunabilir hale gelir
            ["redis"] = "Redis",
            ["mongodb"] = "MongoDB",
            ["fix_session"] = "FIX Connection",
            ["postgresql"] = "PostgreSQL"
        };

        var order = new[] { "redis", "mongodb", "fix_session", "postgresql" };
        HealthServices = order

            //Saglik raporundaki her hizmet anahtarini kullanici dostu adiyla esler ve saglikli olup olmadigini belirler
            .Select(key =>
            {
                var exists = report.Entries.TryGetValue(key, out var entry);     
                var healthy = exists && entry.Status == HealthStatus.Healthy;  

                //Eger saglik raporunda hizmet anahtari varsa ve durumu saglikli ise, hizmetin saglikli oldugunu belirtir, aksi takdirde sagliksiz olarak kabul eder
                return new ServiceHealthVm
                {
                    Name = map[key],
                    IsHealthy = healthy
                };
            })
            .ToList();
    }


    //Aktif sekmeyi normallestirmek icin kullanilan yardimci yontem,
    //Kullanici tarafindan saglanan sekme adini izin verilen degerlerle karsilastirir ve gecerli degilse varsayilan olarak "home" dondurur
    private static string NormalizeTab(string? tab)
    {
        var allowed = new[] { "home", "market", "users", "limits", "alerts", "history", "params", "financial" };
        return allowed.Contains(tab ?? "", StringComparer.OrdinalIgnoreCase)
            ? tab!.ToLowerInvariant()
            : "home";
    }
}


