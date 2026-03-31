using FixTrading.API.Controllers;
using FixTrading.Common.Dtos.Alert;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Infrastructure.MongoDb;
using FixTrading.Persistence;
using FixTrading.Persistence.Entities;
using FixTrading.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FixTrading.API.cs.Pages;


// admin kullanıcıları için yönetim paneli
[Authorize(Roles = "admin")]
public class AdminModel : PageModel
{
    private readonly AppDbContext _dbContext;
    private readonly LatestPriceHandler _latestPriceHandler;
    private readonly HealthCheckService _healthCheckService;
    private readonly IMongoCollection<DtoAlert> _alertsCollection;
    private readonly IPricingLimitsRepository _pricingLimitsRepository;
    private readonly IPricingLimitsCache _pricingLimitsCache;

    public AdminModel(
        AppDbContext dbContext,
        LatestPriceHandler latestPriceHandler,
        HealthCheckService healthCheckService,
        MongoClient mongoClient,
        IOptions<MongoMarketDataOptions> mongoOptions,
        IPricingLimitsRepository pricingLimitsRepository,
        IPricingLimitsCache pricingLimitsCache)
    {
        _dbContext = dbContext;
        _latestPriceHandler = latestPriceHandler;
        _healthCheckService = healthCheckService;
        _pricingLimitsRepository = pricingLimitsRepository;
        _pricingLimitsCache = pricingLimitsCache;

        var database = mongoClient.GetDatabase(mongoOptions.Value.DatabaseName);
        _alertsCollection = database.GetCollection<DtoAlert>(MongoAlertStore.AlertsCollectionName);
    }

    public string ActiveTab { get; private set; } = "home";                     //Aktif sekmeyi tutan özellik, varsayılan olarak "home" olarak ayarlanır
    public List<ServiceHealthVm> HealthServices { get; private set; } = [];    //Uygulamanın sağlık durumunu göstermek için kullanılan hizmetlerin listesini tutar
    public List<DtoMarketData> MarketRows { get; private set; } = [];          //Canlı piyasa verilerini tutan liste
    public List<UserEntity> Users { get; private set; } = [];                 //Kullanıcıları tutan liste
    public List<PricingLimitRowVm> PricingLimits { get; private set; } = [];  //Fiyatlandırma limitlerini tutan liste
    public List<DtoAlert> Alerts { get; private set; } = [];                  //Uyarıları tutan liste


    //UI’dan gelen form verisini backend’e bağlar
    [BindProperty]
    public AddUserInput NewUser { get; set; } = new();


    //Sayfa yüklendiğinde çalışır, aktif sekmeyi belirler ve tüm verileri yükler
    public async Task OnGetAsync(string? tab = null)
    {
        ActiveTab = NormalizeTab(tab);
        await LoadAllAsync();
    }


    //Canlı piyasa verilerini JSON formatında döndüren bir endpoint
    public async Task<IActionResult> OnGetLiveMarketAsync()
    {
        var market = await _latestPriceHandler.GetAllLatestAsync();
        var rows = market
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


    //Yeni bir kullanıcı eklemek için kullanılan endpoint,
    //Form verilerini doğrular, e-posta adresinin benzersiz olduğunu kontrol eder, şifreyi hash'ler ve kullanıcıyı veritabanına ekler
    public async Task<IActionResult> OnPostAddUserAsync(string? tab = null)
    {

        //Form verilerini doğrular, gerekli alanların boş olmadığından emin olur
        if (string.IsNullOrWhiteSpace(NewUser.FullName) ||
            string.IsNullOrWhiteSpace(NewUser.Email) ||
            string.IsNullOrWhiteSpace(NewUser.Password))
        {
            return RedirectToPage(new { tab = "users" });    //Gerekli alanlar sağlanmazsa kullanıcı sekmesine yönlendirir
        }

        var normalizedEmail = NewUser.Email.Trim().ToLowerInvariant();    //Aynı e-posta adresinin farklı biçimlerde girilmesini önlemek için e-posta adresini normalleştirir
        var exists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);    //Veritabanında aynı e-posta adresine sahip başka bir kullanıcı olup olmadığını kontrol eder, varsa kullanıcı sekmesine yönlendirir
        if (exists)
        {
            return RedirectToPage(new { tab = "users" });
        }

        var entity = new UserEntity
        {
            FullName = NewUser.FullName.Trim(),
            Email = normalizedEmail,
            Password = BCrypt.Net.BCrypt.HashPassword(NewUser.Password),
            Role = "user" 
        };

        //Yeni kullanıcıyı veritabanına ekler ve değişiklikleri kaydeder, ardından kullanıcı sekmesine yönlendirir
        _dbContext.Users.Add(entity);
        await _dbContext.SaveChangesAsync();
        return RedirectToPage(new { tab = "users" });
    }


    //Bir kullanıcıyı silmek için kullanılan endpoint, kullanıcıyı veritabanından kaldırır
    public async Task<IActionResult> OnPostDeleteUserAsync(int id)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);        //Veritabanında kullanıcıyı ID'sine göre bulur
        if (user is not null)
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage(new { tab = "users" });                                //Kullanıcıyı sildikten sonra kullanıcı sekmesine yönlendirir
    }


    //Bir kullanıcının rolünü değiştirmek için kullanılan endpoint, kullanıcıyı veritabanında bulur ve rolünü "admin" ile "user" arasında geçiş yapar
    public async Task<IActionResult> OnPostToggleUserRoleAsync(int id)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            user.Role = string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase) ? "user" : "admin";
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage(new { tab = "users" });
    }

    //Fiyatlandırma limitlerini güncellemek için kullanılan endpoint, limit kaydını veritabanında bulur ve yeni değerlerle günceller
    //Parametreler string alınır ve InvariantCulture ile parse edilir — sunucu locale'i (Türkçe) decimal binding'i bozmasın diye
    public async Task<IActionResult> OnPostUpdateLimitAsync(Guid id, string? minMid, string? maxMid, string? maxSpread)
    {
        // Binlik virgülleri temizle (20,005 -> 20005), noktayı ondalık al
        decimal ParseSafe(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            var raw = s.Trim().Replace(" ", "").Replace(",", ""); // Binlik virgülleri sil
            
            var style = System.Globalization.NumberStyles.AllowDecimalPoint | 
                        System.Globalization.NumberStyles.AllowLeadingSign;
            
            return decimal.TryParse(raw, style, System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;
        }

        //Girilen değerleri güvenli bir şekilde parse eder, geçersiz girişlerde 0 döndürür, böylece hatalı veri girişi durumunda limitler sıfırlanmaz
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
            TempData["LimitError"] = "Geçersiz değer: limitler 0–9,999,999,999 arasında olmalı ve MinMid ≤ MaxMid olmalıdır.";
            return RedirectToPage(new { tab = "limits" });
        }

        var limit = await _dbContext.PricingLimits.FirstOrDefaultAsync(x => x.Id == id);
        if (limit is not null)
        {
            limit.MinMid    = minVal;
            limit.MaxMid    = maxVal;
            limit.MaxSpread = spreadVal;
            await _dbContext.SaveChangesAsync();

            // Önemli: Veritabanı güncellendi, şimdi RAM'deki (Cache) limitleri de tazeleyelim!
            var allLimits = await _pricingLimitsRepository.FetchAllAsync();
            _pricingLimitsCache.UpdateLimits(allLimits);
        }

        return RedirectToPage(new { tab = "limits" });
    }


    //Yeni bir fiyatlandırma limiti eklemek için kullanılan endpoint, yeni limit kaydını oluşturur ve veritabanına ekler
    private async Task LoadAllAsync()
    {
        await LoadHealthAsync();                                                //Uygulamanın sağlık durumunu yükler ve HealthServices listesini günceller
        MarketRows = await _latestPriceHandler.GetAllLatestAsync();
        Users = await _dbContext.Users
            .AsNoTracking()                                                   //Sadece okuma işlemi yapacağımız için takip etmeyi devre dışı bırakır, performansı artırır
            .OrderBy(x => x.FullName)                                        //Kullanıcıları tam adına göre sıralar
            .ToListAsync();

        PricingLimits = await _dbContext.PricingLimits
            .AsNoTracking()                                 
            .Include(x => x.Instrument)             //Fiyatlandırma limitlerini ilgili enstrüman bilgisiyle birlikte yükler, böylece sembol bilgisi kullanılabilir
            .OrderBy(x => x.Instrument != null ? x.Instrument.Symbol : string.Empty)     
            .Select(x => new PricingLimitRowVm
            {
                Id = x.Id,
                InstrumentId = x.Instrument != null ? x.Instrument.Id : Guid.Empty,
                Symbol = x.Instrument != null ? x.Instrument.Symbol : "-",
                MinMid = x.MinMid,
                MaxMid = x.MaxMid,
                MaxSpread = x.MaxSpread
            })
            .ToListAsync();

        Alerts = await _alertsCollection
            .Find(Builders<DtoAlert>.Filter.Empty)      //Tüm uyarıları getirir
            .SortByDescending(x => x.Time)              //Uyarıları zamana göre azalan sırada sıralar, böylece en yeni uyarılar önce gelir
            .Limit(150)
            .ToListAsync();
    }

    //Uygulamanın sağlık durumunu kontrol eder ve HealthServices listesini günceller, her hizmetin sağlıklı olup olmadığını belirler
    private async Task LoadHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();                //Sağlık durumunu kontrol eder ve raporu alır
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) 
        {

            //Sağlık raporundaki anahtarları kullanıcı dostu hizmet adlarına eşler, böylece UI'da daha okunabilir hale gelir
            ["redis"] = "Redis",
            ["mongodb"] = "MongoDB",
            ["fix_session"] = "FIX Connection",
            ["postgresql"] = "PostgreSQL"
        };

        var order = new[] { "redis", "mongodb", "fix_session", "postgresql" };
        HealthServices = order

            //Sağlık raporundaki her hizmet anahtarını kullanıcı dostu adıyla eşler ve sağlıklı olup olmadığını belirler
            .Select(key =>
            {
                var exists = report.Entries.TryGetValue(key, out var entry);     
                var healthy = exists && entry.Status == HealthStatus.Healthy;  

                //Eğer sağlık raporunda hizmet anahtarı varsa ve durumu sağlıklı ise, hizmetin sağlıklı olduğunu belirtir, aksi takdirde sağlıksız olarak kabul eder
                return new ServiceHealthVm
                {
                    Name = map[key],
                    IsHealthy = healthy
                };
            })
            .ToList();
    }


    //Aktif sekmeyi normalleştirmek için kullanılan yardımcı yöntem,
    //Kullanıcı tarafından sağlanan sekme adını izin verilen değerlerle karşılaştırır ve geçerli değilse varsayılan olarak "home" döndürür
    private static string NormalizeTab(string? tab)
    {
        var allowed = new[] { "home", "market", "users", "limits", "alerts" };
        return allowed.Contains(tab ?? "", StringComparer.OrdinalIgnoreCase)
            ? tab!.ToLowerInvariant()
            : "home";
    }
}

//Uygulamanın sağlık durumunu göstermek için kullanılan ViewModel, her hizmetin adını ve sağlıklı olup olmadığını tutar
public class ServiceHealthVm
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
}


//Fiyatlandırma limitlerini göstermek için kullanılan ViewModel, her limit kaydının ID'sini, ilgili enstrümanın sembolünü ve limit değerlerini tutar
public class PricingLimitRowVm
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal MinMid { get; set; }
    public decimal MaxMid { get; set; }
    public decimal MaxSpread { get; set; }
}


//Yeni bir kullanıcı eklemek için kullanılan input modeli, form verilerini tutar ve doğrulama için kullanılır
public class AddUserInput
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
}

