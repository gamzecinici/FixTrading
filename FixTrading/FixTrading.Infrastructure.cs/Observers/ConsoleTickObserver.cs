using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Infrastructure.Observers;


// Anlık real-time akış - her tick'te hemen yazdırır (Mongo'dan bağımsız).
public class ConsoleTickObserver : IMarketDataObserver
{
    // Yeni market data tick'i geldiğinde çağrılır.
    public void OnTick(DtoMarketData tick)
    {
        var bidText = tick.Bid > 0 ? tick.Bid.ToString("0.####") : "-";
        var askText = tick.Ask > 0 ? tick.Ask.ToString("0.####") : "-";
        Console.WriteLine($"{tick.Symbol} - {bidText} / {askText}");
    }
}
