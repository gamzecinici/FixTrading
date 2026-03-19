using System.Text.Json;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Pricing;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FixTrading.Infrastructure.Redis;

//bu sınıf, ILatestPriceStore arayüzünü uygulayarak Redis'te en son fiyat bilgisini saklamak ve okumak için kullanılır.
//Redis bağlantısı ve ayarları constructor'da alınır.  
public class RedisLatestPriceStore : ILatestPriceStore
{
    //// Redis'te verileri gruplamak için kullanılan ön ekler
    private const string KeyPrefix = "latest:price:";
    private const string KeySet = "latest:price:symbols"; // Tüm sembollerin listesi 

    private readonly IDatabase _db;
    private readonly RedisOptions _options;

    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    //küçük-büyük harf duyarlılığı olmayan JSON serileştirme seçenekleri
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Constructor, Redis bağlantısı ve ayarları alır
    public RedisLatestPriceStore(IConnectionMultiplexer redis, IOptions<RedisOptions> options)
    {
        _db = redis.GetDatabase();  
        _options = options.Value;
    }


    // Belirtilen sembol için en son fiyatı Redis'e kaydeder. Sembol, bid ve ask fiyatları alınır.  
    public async Task SetLatestAsync(string symbol, decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return;
        symbol = symbol.Trim().ToUpper().Replace("/", "");

        var utcNow = DateTime.UtcNow;
        var turkeyTime = utcNow + TurkeyOffset;

        var (mid, spread) = PricingCalculator.FromBidAsk(bid, ask);
        var dto = new DtoMarketData
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Mid = mid,
            Spread = spread,
            Timestamp = utcNow,
            TimestampFormatted = turkeyTime.ToString("dd.MM.yyyy HH:mm")
        };

        var key = KeyPrefix + symbol;   // Redis anahtarı oluşturulur
        var value = JsonSerializer.Serialize(dto, JsonOptions);   // DtoMarketData nesnesi JSON formatına serileştirilir

        var t1 = _db.StringSetAsync(key, value, _options.LatestPriceTtl);
        var t2 = _db.SetAddAsync(KeySet, symbol);

        await Task.WhenAll(t1, t2);
    }


    // Belirtilen sembol için Redis'ten en son fiyat bilgisini alır. Sembol, küçük-büyük harf duyarlılığı olmayan şekilde işlenir.
    public async Task<DtoMarketData?> GetLatestAsync(string symbol)
    {
        symbol = symbol.Trim().ToUpper().Replace("/", "");
        var key = KeyPrefix + symbol;   
        var value = await _db.StringGetAsync(key);
        if (!value.HasValue) return null;

        return JsonSerializer.Deserialize<DtoMarketData>(value.ToString() ?? "", JsonOptions);
    }


    // Redis'te saklanan tüm sembollerin en son fiyat bilgilerini alır. Tüm semboller için anahtarlar oluşturulur ve değerler alınır.
    // Sonuç, sembole göre sıralanır.
    public async Task<List<DtoMarketData>> GetAllLatestAsync() //*****
    {
        var members = await _db.SetMembersAsync(KeySet);  // Tüm sembollerin listesi alınır
        if (members.Length == 0) return [];

        var keys = members  
            .Where(m => m.HasValue && !m.IsNullOrEmpty) 
            .Select(m => (RedisKey)(KeyPrefix + m!))  
            .ToArray();

        if (keys.Length == 0) return [];

        var values = await _db.StringGetAsync(keys);
        var result = new List<DtoMarketData>();
        foreach (var val in values)
        {
            if (!val.HasValue) continue;
            var dto = JsonSerializer.Deserialize<DtoMarketData>(val.ToString() ?? "", JsonOptions);
            if (dto != null) result.Add(dto);
        }
        return result.OrderBy(x => x.Symbol).ToList();
    }
}

// Redis bağlantı ayarlarını tutan sınıf. ConnectionString, Redis sunucusunun adresini belirtir.
//LatestPriceTtl, en son fiyat bilgisinin Redis'te ne kadar süreyle saklanacağını belirler (null ise süresiz)
public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public TimeSpan? LatestPriceTtl { get; set; } = TimeSpan.FromDays(1);
}
