using System.Collections.Concurrent;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Pricing;

namespace FixTrading.Infrastructure.Stores;

// RAM’de fiyatları tutacak sistemin uygulaması
//Uygulama çalışırken, gelen piyasa verilerini RAM’de saklar ve istenildig inde hizlica erişim sağlar
public class InMemoryLastPriceStore : IInMemoryLastPriceStore
{
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);
    private readonly ConcurrentDictionary<string, DtoMarketData> _store = new(StringComparer.OrdinalIgnoreCase);


    // Verilen sembol, bid ve ask fiyatlarini RAM’de saklar. Gecersiz fiyatlar (0 veya negatif) islenmez.
    public void SetLatest(string symbol, decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return;  
        symbol = symbol.Trim().ToUpper().Replace("/", "");  // "EUR/USD" -> "EURUSD"

        var u = DateTime.UtcNow;
        var utcNow = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, u.Second, DateTimeKind.Utc);
        var turkeyTime = utcNow + TurkeyOffset;

        _store[symbol] = new DtoMarketData       // RAM’de saklanacak veri modeli
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Mid = PricingCalculator.Mid(bid, ask),                   // Mid fiyati hesaplanir
            Spread = PricingCalculator.Spread(bid, ask),            // Spread hesaplanır
            Timestamp = utcNow,
            TimestampFormatted = turkeyTime.ToString("dd.MM.yyyy HH:mm")
        };
    }

    // Verilen sembolün en son fiyat bilgisini döner. Eger sembol bulunamazsa null döner.
    public Task<DtoMarketData?> GetLatestAsync(string symbol)
    {
        symbol = symbol.Trim().ToUpper().Replace("/", "");
        var found = _store.TryGetValue(symbol, out var dto);
        return Task.FromResult(found ? dto : null);
    }

    // RAM’de saklanan tum sembollerin en son fiyat bilgilerini doner. Liste sembol adina göre siralanir.
    public Task<List<DtoMarketData>> GetAllLatestAsync()
    {
        var list = _store.Values.OrderBy(x => x.Symbol).ToList();
        return Task.FromResult(list);
    }

    // Verilen sembolün RAM’deki fiyat bilgisini siler. Eğer sembol bulunamazsa islem yapilmaz.
    public void RemoveLatest(string symbol)
    {
        symbol = symbol.Trim().ToUpper().Replace("/", "");
        _store.TryRemove(symbol, out _);      // Sembol RAM’den silinir
    }
}
