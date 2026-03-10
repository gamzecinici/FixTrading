using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Infrastructure.Observers;

// Gelen piyasa verilerini RAM’de tutacak sistemin gözlemcisi
//Her tick geldiğinde, geçerli fiyatları RAM’de günceller
public class InMemoryLastPriceObserver : IMarketDataObserver
{
    private readonly IInMemoryLastPriceStore _store;

    public InMemoryLastPriceObserver(IInMemoryLastPriceStore store)
    {
        _store = store;
    }

    // Her tick geldiğinde çağrılır
    public void OnTick(DtoMarketData tick)
    {
        if (tick.Bid <= 0 || tick.Ask <= 0) return;
        try
        {
            _store.SetLatest(tick.Symbol, tick.Bid, tick.Ask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InMemoryLastPriceObserver] Güncelleme hatası: {ex.Message}");
        }
    }
}
