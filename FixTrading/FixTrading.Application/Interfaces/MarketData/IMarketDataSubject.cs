using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.MarketData;

// Tick geldiğinde kayıtlı tüm Observer'lara bildirim gönderir.
//Fiyat verisi geldiğinde, hangi yollara (Observer'lara) girmesi gerektiğini ve o yolların açık olup olmadığını yöneten kuralları koyar.
public interface IMarketDataSubject
{
    // Yeni bir Observer ekler (dinlemeye başlar).
    void Attach(IMarketDataObserver observer);

    // Observer'ı listeden çıkarır.
    void Detach(IMarketDataObserver observer);

    // Tüm Observer'lara yeni tick bilgisini bildirir.
    void Notify(DtoMarketData tick);
}
