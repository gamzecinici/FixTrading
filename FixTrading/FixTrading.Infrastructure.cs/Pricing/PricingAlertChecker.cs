using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.Alert;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Infrastructure.Pricing;

// Bu sınıf gelen market datayı (mid & spread) alır,
// cache'teki pricing limitlerle karşılaştırır.
// Eğer limit aşımı varsa alert üretir.
public class PricingAlertChecker : IPricingAlertChecker
{
    public const string MidTooLow = "MID_TOO_LOW";
    public const string MidTooHigh = "MID_TOO_HIGH";
    public const string SpreadLimit = "SPREAD_LIMIT";

    private readonly IPricingLimitsProvider _limitsProvider;
    private readonly IAlertStore _alertStore;
    private readonly IAlertNotifier _alertNotifier;


    // Constructor, dependency injection ile IPricingLimitsProvider, IAlertStore ve IAlertNotifier arayüzlerini alır ve sınıfın alanlarına atar.
    public PricingAlertChecker(IPricingLimitsProvider limitsProvider, IAlertStore alertStore, IAlertNotifier alertNotifier)
    {
        _limitsProvider = limitsProvider;
        _alertStore = alertStore;
        _alertNotifier = alertNotifier;
    }

    /// <summary>
    /// Gelen tick'in RAM cache'teki min/mid/max spread limitlerine uyup uymadığını kontrol eder.
    /// </summary>
    /// <returns>
    /// <c>true</c> = ihlal var → üst katman (FixMessageHandler) bu tick'i Redis ve in-memory'e yazmaz;
    /// <c>false</c> = limit yok veya ihlal yok → fiyat yayına devam eder.
    /// </returns>
    /// <remarks>
    /// Yeni enstrüman eklenirken limit alanları 0 bırakılabiliyordu. Eski kodda <c>dto.Mid &gt; limit.MaxMid</c>
    /// ve <c>MaxMid == 0</c> iken her gerçek fiyat "MID_TOO_HIGH" sayılıyor, breach=true dönüyor ve
    /// canlı piyasa listesi hiç dolumuyordu. Bu yüzden her eşik için "limit gerçekten ayarlanmış mı?" diye
    /// ilgili değerin &gt; 0 olması şartı eklendi.
    /// </remarks>
    public bool CheckAndLogIfBreach(DtoMarketData dto)
    {
        var limit = _limitsProvider.GetLimit(dto.Symbol);
        if (limit == null) return false;

        var time = DateTime.UtcNow;
        var timeTurkey = time.AddHours(3);

        // MinMid &gt; 0: alt sınır anlamlıdır; aksi halde "girilmemiş limit" — kontrol yok
        if (limit.MinMid > 0 && dto.Mid < limit.MinMid)
        {
            WriteAlert(dto.Symbol, MidTooLow, dto.Mid, limit.MinMid, time, timeTurkey);
            return true;
        }

        // MaxMid &gt; 0: üst sınır anlamlıdır; MaxMid=0 iken Mid her zaman &gt; 0 olduğundan yanlış ihlal üretilirdi
        if (limit.MaxMid > 0 && dto.Mid > limit.MaxMid)
        {
            WriteAlert(dto.Symbol, MidTooHigh, dto.Mid, limit.MaxMid, time, timeTurkey);
            return true;
        }

        // Spread üst sınırı da 0 ise "tanımsız" kabul edilir
        if (limit.MaxSpread > 0 && dto.Spread > limit.MaxSpread)
        {
            WriteAlert(dto.Symbol, SpreadLimit, dto.Spread, limit.MaxSpread, time, timeTurkey);
            return true;
        }

        return false;
    }


    //  verilen parametrelerle bir DtoAlert nesnesi oluşturur ve bunu asenkron olarak IAlertStore'a yazar ve IAlertNotifier ile bildirir.
    private void WriteAlert(string symbol, string type, decimal value, decimal limitValue, DateTime time, DateTime timeTurkey)
    {
        var alert = new DtoAlert     //alert objesi oluşturulur
        {
            Symbol = symbol,
            Type = type,
            Value = value,
            Limit = limitValue,
            Time = time,
            TimeTurkey = timeTurkey
        };
        _ = Task.Run(async () =>     //Alert'i yaz ve mail at ama ana akışı durdurma
        {
            try
            {
                await _alertStore.WriteAsync(alert);    //MongoDB'ye yazılır
                await _alertNotifier.NotifyAsync(alert);   //mail atılır
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PricingAlertChecker] Alert yazma/bildirim hatası: {ex.Message}");
            }
        });
    }
}
