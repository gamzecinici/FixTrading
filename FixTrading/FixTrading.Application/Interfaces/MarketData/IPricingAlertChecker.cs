using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.MarketData;

//Bu interface, pricing limit ihlali durumunda alert yazmakla ilgili işlemleri tanımlar
public interface IPricingAlertChecker
{
    //Bu method, verilen market data üzerinde pricing limit ihlali olup olmadığını kontrol eder ve ihlal varsa loglama işlemi yapar. 
    bool CheckAndLogIfBreach(DtoMarketData dto);   //İhlal durumunda true, aksi halde false döner.
}
