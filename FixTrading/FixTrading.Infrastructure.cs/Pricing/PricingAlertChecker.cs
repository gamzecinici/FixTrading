using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.Alerts;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.Alert;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Infrastructure.Pricing;

// Bu sınıf gelen market datayi (mid & spread) alir,
// cache'teki pricing limitlerle karsilastirir.
// Eger limit asimi varsa alert üretir.
public class PricingAlertChecker : IPricingAlertChecker
{
    public const string MidTooLow = "MID_TOO_LOW";
    public const string MidTooHigh = "MID_TOO_HIGH";
    public const string SpreadLimit = "SPREAD_LIMIT";

    private readonly IPricingLimitsProvider _limitsProvider;
    private readonly IAlertStore _alertStore;
    private readonly IAlertNotifier _alertNotifier;
    private readonly ISystemParameterService _sysParam;


    // Constructor, dependency injection ile IPricingLimitsProvider, IAlertStore ve IAlertNotifier arayüzlerini alir ve sinifinn alanlarina atar.
    public PricingAlertChecker(
        IPricingLimitsProvider limitsProvider, 
        IAlertStore alertStore, 
        IAlertNotifier alertNotifier,
        ISystemParameterService sysParam)
    {
        _limitsProvider = limitsProvider;
        _alertStore = alertStore;
        _alertNotifier = alertNotifier;
        _sysParam = sysParam;
    }

    // Bu metod, verilen DtoMarketData nesnesi için fiyat limitlerini kontrol eder.
    // Eger herhangi bir limit ihlali tespit edilirse, WriteAlert metodunu çağırarak bir alert olusturur ve true döner. Aksi halde false doner.
    public bool CheckAndLogIfBreach(DtoMarketData dto)
    {
        var limit = _limitsProvider.GetLimit(dto.Symbol);
        if (limit == null) return false;

        var u = DateTime.UtcNow;
        var time = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, u.Second, DateTimeKind.Utc);
        
        // Parametre tablosundan UtcOffset'i al, yoksa varsayılan 3 kullan
        double offset = 3;
        try
        {
            var config = _sysParam.GetConfigAsync("FinancialAnalytics").GetAwaiter().GetResult();
            if (config != null && config.TryGetValue("UtcOffset", out var val) && double.TryParse(val, out var res))
            {
                offset = res;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PricingAlertChecker] UtcOffset parametresi okunamadı: {ex.Message}");
        }

        var timeTurkey = time.AddHours(offset);

        //Alt sinir anlamlı ise ve Mid bu sınırın altındaysa ihlal var demektir.
        if (limit.MinMid > 0 && dto.Mid < limit.MinMid)
        {
            WriteAlert(dto.Symbol, MidTooLow, dto.Mid, limit.MinMid, time, timeTurkey);
            return true;
        }

        // üst sinir anlamli ise ve Mid bu sinirin üstündeyse ihlal var demektir
        if (limit.MaxMid > 0 && dto.Mid > limit.MaxMid)
        {
            WriteAlert(dto.Symbol, MidTooHigh, dto.Mid, limit.MaxMid, time, timeTurkey);
            return true;
        }

        // Spread üst siniri da 0 ise "tanımsız" kabul edilir
        if (limit.MaxSpread > 0 && dto.Spread > limit.MaxSpread)
        {
            WriteAlert(dto.Symbol, SpreadLimit, dto.Spread, limit.MaxSpread, time, timeTurkey);
            return true;
        }

        return false;
    }


    //  verilen parametrelerle bir DtoAlert nesnesi olusurur ve bunu asenkron olarak IAlertStore'a yazar ve IAlertNotifier ile bildirir.
    private void WriteAlert(string symbol, string type, decimal value, decimal limitValue, DateTime time, DateTime timeTurkey)
    {
        var alert = new DtoAlert     //alert objesi olusturulur
        {
            Symbol = symbol,
            Type = type,
            Value = value,
            Limit = limitValue,
            Time = time,
            TimeTurkey = timeTurkey
        };
        _ = Task.Run(async () =>     //Alert'i yaz ve mail at ama ana akisi durdurma
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
