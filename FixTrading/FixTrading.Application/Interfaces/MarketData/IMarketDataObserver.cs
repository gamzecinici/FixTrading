using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.MarketData;


// Konsol, MongoDB buffer, Redis gibi farklı dinleyiciler bu arayüzü uygular.
public interface IMarketDataObserver
{
    // Yeni market data tick'i geldiğinde çağrılır.
    void OnTick(DtoMarketData tick);
}
