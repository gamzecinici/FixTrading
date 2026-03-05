using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Infrastructure.Observers;

// Observer pattern: Her tick geldiğinde Redis'e yazan Observer.
// En son fiyat her tick'te güncellenir (Latest Price API için).
public class RedisStoreTickObserver : IMarketDataObserver
{
    private readonly ILatestPriceStore _latestPriceStore;

    // Redis'e erişimi sağlayan ILatestPriceStore arayüzünü dışarıdan alır.
    // Sınıf içinde Redis'e doğrudan bağlanmak yerine, bu işi yapan bir servisi kullanır
    public RedisStoreTickObserver(ILatestPriceStore latestPriceStore)
    {
        _latestPriceStore = latestPriceStore;
    }

    // Yeni market data tick'i geldiğinde çağrılır.
    public void OnTick(DtoMarketData tick)
    {
        if (tick.Bid <= 0 || tick.Ask <= 0) return;
        _ = Task.Run(async () =>   // Arka planda Redis'e yazma işlemi yapılır. Hata olursa yakalanır ve konsola yazdırılır.
        {
            try
            {
                await _latestPriceStore.SetLatestAsync(tick.Symbol, tick.Bid, tick.Ask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RedisStoreTickObserver] Redis yazma hatası: {ex.Message}");
            }
        });
    }
}
