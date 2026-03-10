using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using Microsoft.Extensions.Logging;

namespace FixTrading.API.Controllers;

// Bu sınıf, en son fiyat bilgilerini almak için kullanılan bir handler'dır.
// Okuma sırası: Redis → MongoDB → In-Memory (Last Known Price).
// FIX bağlantısı kopsa bile in-memory'deki son bilinen fiyat döndürülür, API boş dönmez.

public class LatestPriceHandler
{
    private readonly ILatestPriceStore _latestPriceStore;
    private readonly IMongoLatestPriceReader _mongoReader;
    private readonly IInMemoryLastPriceStore _inMemoryStore;
    private readonly ILogger<LatestPriceHandler> _logger;

    public LatestPriceHandler(ILatestPriceStore latestPriceStore, IMongoLatestPriceReader mongoReader, IInMemoryLastPriceStore inMemoryStore, ILogger<LatestPriceHandler> logger)
    {
        _latestPriceStore = latestPriceStore;
        _mongoReader = mongoReader;
        _inMemoryStore = inMemoryStore;
        _logger = logger;
    }


    // Bu metod, belirli bir sembol için en son fiyat bilgisini döndürür.
    //Öncelikle Redis'ten, eğer orada yoksa MongoDB'den ve her iki veri kaynağına da erişilemezse in-memory store'dan veri almaya çalışır.
    public async Task<DtoMarketData?> GetLatestAsync(string symbol)
    {
        try
        {
            var fromRedis = await _latestPriceStore.GetLatestAsync(symbol);  
            if (fromRedis != null) return fromRedis;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis erişilemedi, fallback: {Symbol}", symbol);
        }

        try
        {
            var fromMongo = await _mongoReader.GetLatestAsync(symbol);
            if (fromMongo != null)
            {
                _ = WriteBackToRedisAsync(fromMongo.Symbol, fromMongo.Bid, fromMongo.Ask);
                return fromMongo;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB erişilemedi, in-memory fallback: {Symbol}", symbol);
        }

        return await _inMemoryStore.GetLatestAsync(symbol);
    }

    public async Task<List<DtoMarketData>> GetAllLatestAsync()
    {
        try
        {
            var fromRedis = await _latestPriceStore.GetAllLatestAsync();  // Redis'ten tüm sembollerin en son fiyat bilgilerini almaya çalışır.
            if (fromRedis.Count > 0) return fromRedis;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis erişilemedi, fallback: GetAllLatest");
        }

        try
        {
            var fromMongo = await _mongoReader.GetAllLatestAsync();
            if (fromMongo.Count > 0)
            {
                foreach (var item in fromMongo)
                    _ = WriteBackToRedisAsync(item.Symbol, item.Bid, item.Ask);
                return fromMongo;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB erişilemedi, in-memory fallback: GetAllLatest");
        }

        return await _inMemoryStore.GetAllLatestAsync();
    }

    // Bu metod, MongoDB'den alınan fiyat bilgisini Redis'e geri yazar. 
    // Bu sayede sonraki isteklerde Redis'ten hızlıca veri alınabilir.
    private async Task WriteBackToRedisAsync(string symbol, decimal bid, decimal ask)
    {
        try     // Redis'e yazma işlemi sırasında hata oluşursa loglar, ancak uygulamanın çalışmasına engel olmaz
        {
            await _latestPriceStore.SetLatestAsync(symbol, bid, ask);
        }
        catch (Exception ex)  
        {
            _logger.LogDebug(ex, "Redis write-back başarısız (beklenebilir Redis down iken): {Symbol}", symbol);
        }
    }
}
