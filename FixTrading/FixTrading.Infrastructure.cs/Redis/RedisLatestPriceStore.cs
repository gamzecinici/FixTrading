using System.Text.Json;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Dtos.Options;
using FixTrading.Common.Pricing;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FixTrading.Infrastructure.Redis;

//bu sinif, ILatestPriceStore arayuzunu uygulayarak Redis'te en son fiyat bilgisini saklamak ve okumak icin kullanilir.
//Redis baglantisi ve ayarlari constructor'da alinir.  
public class RedisLatestPriceStore : ILatestPriceStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly RedisOptions _options;
    private readonly ISystemParameterService _systemParameterService;

    //kucuk-buyuk harf duyarliligi olmayan JSON serilestirme secenekleri
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Constructor, Redis baglantisi ve ayarlari alir
    public RedisLatestPriceStore(IConnectionMultiplexer redis, IOptions<RedisOptions> options, ISystemParameterService systemParameterService)
    {
        _redis = redis;
        _db = redis.GetDatabase();  
        _options = options.Value;
        _systemParameterService = systemParameterService;
    }


    // Belirtilen sembol icin en son fiyati Redis'e kaydeder. Sembol, bid ve ask fiyatlari alinir.  
    public async Task SetLatestAsync(string symbol, decimal bid, decimal ask)
    {
        if (!_redis.IsConnected) return; // Redis bagli degilse islem yapma
        if (bid <= 0 || ask <= 0) return;
        symbol = symbol.Trim().ToUpper().Replace("/", "");

        var runtimeOptions = await ResolveRuntimeOptionsAsync(includeTurkeyOffset: true);

        var u = DateTime.UtcNow;
        var utcNow = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, u.Second, DateTimeKind.Utc);
        var turkeyTime = utcNow + TimeSpan.FromHours(runtimeOptions.TurkeyOffsetHours);

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

        var key = runtimeOptions.KeyPrefix + symbol;   // Redis anahtari olusturulur
        var value = JsonSerializer.Serialize(dto, JsonOptions);   // DtoMarketData nesnesi JSON formatina serilestirilir

        var t1 = _db.StringSetAsync(key, value, _options.LatestPriceTtl ?? TimeSpan.MaxValue);
        var t2 = _db.SetAddAsync(runtimeOptions.KeySet, symbol);

        await Task.WhenAll(t1, t2);
    }


    // Belirtilen sembol icin Redis'ten en son fiyat bilgisini alir. Sembol, kucuk-buyuk harf duyarliligi olmayan sekilde islenir.
    public async Task<DtoMarketData?> GetLatestAsync(string symbol)
    {
        if (!_redis.IsConnected) return null;
        symbol = symbol.Trim().ToUpper().Replace("/", "");

        var runtimeOptions = await ResolveRuntimeOptionsAsync(includeTurkeyOffset: false);

        var key = runtimeOptions.KeyPrefix + symbol;   
        var value = await _db.StringGetAsync(key);
        if (!value.HasValue) return null;

        return JsonSerializer.Deserialize<DtoMarketData>(value.ToString() ?? "", JsonOptions);
    }


    // Redis'te saklanan tum sembollerin en son fiyat bilgilerini alir. Tum semboller icin anahtarlar olusturulur ve degerler alinir.
    // Sonuc, sembole gore siralanir.
    public async Task<List<DtoMarketData>> GetAllLatestAsync() 
    {
        if (!_redis.IsConnected) return [];
        var runtimeOptions = await ResolveRuntimeOptionsAsync(includeTurkeyOffset: false);
        var members =  await _db.SetMembersAsync(runtimeOptions.KeySet);  // Tum sembollerin listesi alinir
        if (members.Length == 0) return [];

        var keys = members  
            .Where(m => m.HasValue && !m.IsNullOrEmpty) 
            .Select(m => (RedisKey)(runtimeOptions.KeyPrefix + m!))  
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


    // Belirtilen sembol icin Redis'ten en son fiyat bilgisini siler. Sembol, kucuk-buyuk harf duyarliligi olmayan sekilde islenir.
    public async Task RemoveLatestAsync(string symbol)
    {
        if (!_redis.IsConnected) return;
        symbol = symbol.Trim().ToUpper().Replace("/", "");

        var runtimeOptions = await ResolveRuntimeOptionsAsync(includeTurkeyOffset: false);

        var key = runtimeOptions.KeyPrefix + symbol;
        //Redis'ten hem fiyat bilgisini siler hem de sembolun listeden kaldirir. Iki islem ayni anda yapilir.
        await Task.WhenAll(_db.KeyDeleteAsync(key), _db.SetRemoveAsync(runtimeOptions.KeySet, symbol));
    }

    private async Task<RedisRuntimeOptions> ResolveRuntimeOptionsAsync(bool includeTurkeyOffset)
    {
        var result = new RedisRuntimeOptions(_options.KeyPrefix, _options.KeySet, _options.TurkeyOffsetHours);

        try
        {
            var config = await _systemParameterService.GetConfigAsync("Redis");
            return ApplyRuntimeConfig(result, config, includeTurkeyOffset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RedisLatestPriceStore] Redis parametreleri okunamadı: {ex.Message}");
            return result;
        }
    }

    private static RedisRuntimeOptions ApplyRuntimeConfig(
        RedisRuntimeOptions current,
        Dictionary<string, string>? config,
        bool includeTurkeyOffset)
    {
        if (config == null)
            return current;

        var keyPrefix = current.KeyPrefix;
        var keySet = current.KeySet;
        var turkeyOffsetHours = current.TurkeyOffsetHours;

        if (config.TryGetValue("KeyPrefix", out var prefix) && prefix != null)
            keyPrefix = prefix + ":";
        if (config.TryGetValue("KeySet", out var set) && set != null)
            keySet = set;
        if (includeTurkeyOffset &&
            config.TryGetValue("TurkeyOffsetHours", out var offsetStr) &&
            int.TryParse(offsetStr, out var offset))
            turkeyOffsetHours = offset;

        return new RedisRuntimeOptions(keyPrefix, keySet, turkeyOffsetHours);
    }

    private sealed record RedisRuntimeOptions(string KeyPrefix, string KeySet, int TurkeyOffsetHours);
}
