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

    // CheckAndLogIfBreach metodu, verilen market data'yı alır ve cache'teki pricing limitlerle karşılaştırır.
    // Eğer limit aşımı varsa, WriteAlert metodunu çağırarak alert üretir ve true döner. Aksi halde false döner.
    public bool CheckAndLogIfBreach(DtoMarketData dto)
    {
        var limit = _limitsProvider.GetLimit(dto.Symbol);   //Limit çekiliyor
        if (limit == null) return false;

        var time = DateTime.UtcNow;
        var timeTurkey = time.AddHours(3);

        if (dto.Mid < limit.MinMid)
        {
            WriteAlert(dto.Symbol, MidTooLow, dto.Mid, limit.MinMid, time, timeTurkey);
            return true;
        }

        if (dto.Mid > limit.MaxMid)
        {
            WriteAlert(dto.Symbol, MidTooHigh, dto.Mid, limit.MaxMid, time, timeTurkey);
            return true;
        }

        if (dto.Spread > limit.MaxSpread)
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
        _ = Task.Run(async () =>     //Alert’i yaz ve mail at ama ana akışı durdurma
        {
            try
            {
                await _alertStore.WriteAsync(alert);    //MongoDB’ye yazılır
                await _alertNotifier.NotifyAsync(alert);   //mail atılır
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PricingAlertChecker] Alert yazma/bildirim hatası: {ex.Message}");
            }
        });
    }
}
