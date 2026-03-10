using System.Collections.Concurrent;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Infrastructure.Stores;

// RAM’de fiyatları tutacak sistemin uygulaması
//Uygulama çalışırken, gelen piyasa verilerini RAM’de saklar ve istenildiğinde hızlıca erişim sağlar
public class InMemoryLastPriceStore : IInMemoryLastPriceStore
{
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);
    private readonly ConcurrentDictionary<string, DtoMarketData> _store = new(StringComparer.OrdinalIgnoreCase);

    public void SetLatest(string symbol, decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return;  
        symbol = symbol.Trim().ToUpper().Replace("/", "");  // "EUR/USD" -> "EURUSD"

        var utcNow = DateTime.UtcNow;
        var turkeyTime = utcNow + TurkeyOffset;

        _store[symbol] = new DtoMarketData       // RAM’de saklanacak veri modeli
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Mid = (bid + ask) / 2,
            Timestamp = utcNow,
            TimestampFormatted = turkeyTime.ToString("dd.MM.yyyy HH:mm")
        };
    }

    // Verilen sembolün en son fiyat bilgisini döner. Eğer sembol bulunamazsa null döner.
    public Task<DtoMarketData?> GetLatestAsync(string symbol)
    {
        symbol = symbol.Trim().ToUpper().Replace("/", "");
        var found = _store.TryGetValue(symbol, out var dto);
        return Task.FromResult(found ? dto : null);
    }

    // RAM’de saklanan tüm sembollerin en son fiyat bilgilerini döner. Liste sembol adına göre sıralanır.
    public Task<List<DtoMarketData>> GetAllLatestAsync()
    {
        var list = _store.Values.OrderBy(x => x.Symbol).ToList();
        return Task.FromResult(list);
    }
}
