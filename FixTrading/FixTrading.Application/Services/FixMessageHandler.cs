using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Services;

//Bu sınıf, FIX mesajlarını işlemekle ilgili işlemleri gerçekleştirir.
//Gelen market data doğru mu? Nereye gidecek? Ne yapılacak?” kararını veren sınıf
public class FixMessageHandler : IFixMessageHandler
{
    private readonly IMarketDataObserver _consoleTickObserver;
    private readonly IMarketDataObserver _mongoBufferTickObserver;
    private readonly IMarketDataObserver _redisStoreTickObserver;
    private readonly IMarketDataObserver _inMemoryLastPriceObserver;
    private readonly IPricingAlertChecker _pricingAlertChecker;

    public FixMessageHandler(
        IMarketDataObserver consoleTickObserver,
        IMarketDataObserver mongoBufferTickObserver,
        IMarketDataObserver redisStoreTickObserver,
        IMarketDataObserver inMemoryLastPriceObserver,
        IPricingAlertChecker pricingAlertChecker)
    {
        _consoleTickObserver = consoleTickObserver;
        _mongoBufferTickObserver = mongoBufferTickObserver;
        _redisStoreTickObserver = redisStoreTickObserver;
        _inMemoryLastPriceObserver = inMemoryLastPriceObserver;
        _pricingAlertChecker = pricingAlertChecker;
    }


    // Bu metod, her gelen market data tick'ini işler. İşlem sırası:
    // 1. Konsola her tick'i gönder (limit ihlali ayrımı yok, akış izleme amaçlı)
    // 2. Fiyat limit ihlali kontrolü yap. İhlal varsa, Redis ve in-memory observer'lara gönderme (son doğru fiyat korunur)
    // 3. MongoDB market pipeline'ına tick'i gönder (bozuk veri dahil, devam eder; alerts collection ayrı)
    // 4. Redis ve in-memory observer'lara sadece limit içindeki (doğru) tick'i gönder
    //    (İhlal durumunda, önceki doğru fiyat korunur ve yeni tick Redis/in-memory observer'lara gönderilmez)
    public void Handle(DtoMarketData tick)
    {
        // Konsol: her tick (limit ihlali ayrımı yok; akış izleme)
        _consoleTickObserver.OnTick(tick);

        // Domain Rules: Fiyat limit ihlali kontrolü
        // İhlal varsa Redis/in-memory observer'lara gönderilmez (son doğru fiyat korunur)
        var breach = _pricingAlertChecker.CheckAndLogIfBreach(tick);

        // Mongo market pipeline: bozuk veri dahil devam (alerts collection ayrı)
        _mongoBufferTickObserver.OnTick(tick);

        // Redis + in-memory: sadece limit içindeki (doğru) veri; ihlalde önceki doğru değer korunur
        if (!breach)
        {
            _redisStoreTickObserver.OnTick(tick);
            _inMemoryLastPriceObserver.OnTick(tick);
        }
    }
}
