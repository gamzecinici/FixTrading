using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Infrastructure.Observers;

// Observer pattern: Her tick geldiğinde MongoDB buffer'a ekleyen Observer.
// 60 sn boyunca biriken tüm veriler toplu yazılacak (MongoMarketDataBuffer).
public class MongoBufferTickObserver : IMarketDataObserver
{
    private readonly IMarketDataBuffer _marketDataBuffer;   // MongoDB'ye yazma işlemini yöneten buffer arayüzü.

    // DI prensibini uygulayarak sınıfın çalışması için ihtiyaç duyduğu araçları dışarıdan temin eder
    public MongoBufferTickObserver(IMarketDataBuffer marketDataBuffer)  
    {
        _marketDataBuffer = marketDataBuffer;
    }

    // Yeni market data tick'i geldiğinde çağrılır.
    public void OnTick(DtoMarketData tick)
    {
        if (tick.Bid <= 0 || tick.Ask <= 0) return;
        try
        {
            _marketDataBuffer.Add(tick.Symbol, tick.Bid, tick.Ask);  // Direkt mongo ya yazmadan RAM e yazıyor
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MongoBufferTickObserver] Buffer ekleme hatası: {ex.Message}");
        }
    }
}
