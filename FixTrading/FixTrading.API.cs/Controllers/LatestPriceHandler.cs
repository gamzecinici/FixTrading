using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.API.Controllers;

// Bu sınıf, en son fiyat bilgilerini almak için kullanılan bir handler'dır.
//İlk olarak Redis'ten verileri almaya çalışır, eğer Redis'te veri bulunamazsa MongoDB'den verileri alır.
//Bu sayede hızlı erişim sağlanır ve Redis'te olmayan veriler için MongoDB'ye başvurulur.

public class LatestPriceHandler
{
    private readonly ILatestPriceStore _latestPriceStore;   
    private readonly IMongoLatestPriceReader _mongoReader;


    //Bu constructor, ILatestPriceStore ve IMongoLatestPriceReader arayüzlerini alır.
    //Bu arayüzler, Redis'te en son fiyat bilgisini saklamak ve okumak için kullanılır.
    public LatestPriceHandler(ILatestPriceStore latestPriceStore, IMongoLatestPriceReader mongoReader)
    {
        _latestPriceStore = latestPriceStore;
        _mongoReader = mongoReader;
    }

    //Bu metot, belirtilen sembol için en son fiyat bilgisini asenkron olarak getirir.
    public async Task<DtoMarketData?> GetLatestAsync(string symbol)
    {
        var fromRedis = await _latestPriceStore.GetLatestAsync(symbol);  // Redis'ten veriyi almaya çalışır.
        if (fromRedis != null) return fromRedis;                         // Eğer Redis'te veri bulunursa, bu veriyi döndürür.
        return await _mongoReader.GetLatestAsync(symbol);               // Eğer Redis'te veri bulunmazsa, MongoDB'den veriyi almaya çalışır ve döndürür.
    }


    //Bu metot, tüm semboller için en son fiyat bilgilerini asenkron olarak getirir.
    public async Task<List<DtoMarketData>> GetAllLatestAsync()
    {
        var fromRedis = await _latestPriceStore.GetAllLatestAsync();
        if (fromRedis.Count > 0) return fromRedis;
        return await _mongoReader.GetAllLatestAsync();
    }
}
