using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.MarketData;

//En son fiyat bilgisini Redis'te saklamak ve okumak için kullanılan interface
public interface ILatestPriceStore
{
    //Belirtilen sembol için en son fiyatı Redis'e kaydeder.
    Task SetLatestAsync(string symbol, decimal bid, decimal ask);

    //Belirtilen sembol için Redis'ten en son fiyatı okur.
    Task<DtoMarketData?> GetLatestAsync(string symbol);

    //Tüm semboller için Redis'ten en son fiyatları okur.
    Task<List<DtoMarketData>> GetAllLatestAsync();
}
