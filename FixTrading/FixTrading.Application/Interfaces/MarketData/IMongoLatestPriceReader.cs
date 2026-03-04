using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.MarketData;

//Bu arayüz, MongoDB'den en son fiyat bilgisini okumak için kullanılır. Redis'te bulunmayan verileri almak için tasarlanmıştır.
public interface IMongoLatestPriceReader
{
    //Bu metot, belirtilen sembol için en son fiyat bilgisini asenkron olarak getirir. Eğer sembol bulunamazsa null döner.
    Task<DtoMarketData?> GetLatestAsync(string symbol);

    //Bu metot, MongoDB'den tüm semboller için en son fiyat bilgilerini asenkron olarak getirir.
    //Bu, Redis'te bulunmayan tüm sembollerin en son fiyat bilgilerini almak için kullanılır.
    Task<List<DtoMarketData>> GetAllLatestAsync();
}
