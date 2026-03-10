using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.MarketData;


//RAM’de fiyatları tutacak sistemin sözleşmesi
public interface IInMemoryLastPriceStore
{
    Task<DtoMarketData?> GetLatestAsync(string symbol);  //Tek sembolün son fiyatını döner
    Task<List<DtoMarketData>> GetAllLatestAsync();   //Tüm sembollerin son fiyatlarını döner
    void SetLatest(string symbol, decimal bid, decimal ask);    // Bellekte güncelleme yapar (senkron)
}
