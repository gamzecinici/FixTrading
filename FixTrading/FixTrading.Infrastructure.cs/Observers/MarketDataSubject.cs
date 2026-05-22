using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Infrastructure.Observers;

// Tick geldiğinde kayıtlı tüm Observer'lara Notify ile bildirim gönderir.
public class MarketDataSubject : IMarketDataSubject
{
    private readonly List<IMarketDataObserver> _observers = new();   // Kayıtlı Observer'ların listesi.
    private readonly object _lock = new();                           // Çoklu thread ortamında güvenli erişim için lock objesi.- THREAD-SAFE

    // Yeni bir Observer ekler. Eğer zaten kayıtlı değilse listeye ekler.
    public void Attach(IMarketDataObserver observer)
    {
        lock (_lock)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
        }
    }

    // Observer'ı listeden çıkarır.
    public void Detach(IMarketDataObserver observer)
    {
        lock (_lock)
        {
            _observers.Remove(observer);
        }
    }

    // Tüm Observer'lara yeni tick bilgisini bildirir.
    public void Notify(DtoMarketData tick)
    {
        List<IMarketDataObserver> snapshot;    //O nesnenin o anki fotografını alır
        lock (_lock)
        {
            snapshot = new List<IMarketDataObserver>(_observers);
        }
        //Elindeki listede kim varsa(Redis mi? Mongo mu? Console mu?) sırayla hepsinin kapısını çalar ve OnTick(tick) diyerek yeni fiyatı onlara teslim eder.
        foreach (var observer in snapshot)       
        {
            try
            {
                observer.OnTick(tick);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MarketDataSubject] Observer hatası: {ex.Message}");
            }
        }
    }
}
