using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.MarketData;

// Observer pattern: Her yeni tick geldiğinde bilgilendirilen arayüz.
// Konsol, MongoDB buffer, Redis gibi farklı dinleyiciler bu arayüzü uygular.
public interface IMarketDataObserver
{
    // Yeni market data tick'i geldiğinde çağrılır.
    void OnTick(DtoMarketData tick);
}
