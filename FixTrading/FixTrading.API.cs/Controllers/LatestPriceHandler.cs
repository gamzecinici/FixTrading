using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.API.Controllers;

// Controller ile Service arasındaki ara katman
public class LatestPriceHandler
{
    // ILatestPriceStore, en son fiyat bilgisini Redis'te saklamak ve okumak için kullanılan bir interface'dir.
    private readonly ILatestPriceStore _latestPriceStore;

    //Kurucu metot, ILatestPriceStore bağımlılığını alır ve sınıf içinde kullanılmak üzere saklar.
    public LatestPriceHandler(ILatestPriceStore latestPriceStore)
    {
        _latestPriceStore = latestPriceStore;
    }


    // Belirtilen sembol için en son fiyatı Redis'e kaydeder. Sembol, bid ve ask fiyatları alınır.
    public Task<DtoMarketData?> GetLatestAsync(string symbol)
        => _latestPriceStore.GetLatestAsync(symbol);


    // Tüm semboller için en son fiyatları Redis'ten alır ve bir liste olarak döndürür.
    public Task<List<DtoMarketData>> GetAllLatestAsync()
        => _latestPriceStore.GetAllLatestAsync();
}
