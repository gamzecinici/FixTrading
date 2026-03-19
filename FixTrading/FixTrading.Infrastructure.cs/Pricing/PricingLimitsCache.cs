using System.Collections.Concurrent;
using FixTrading.Common.Dtos.Pricing;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Infrastructure.Pricing;

//PricingLimitsCache, IPricingLimitsProvider ve IPricingLimitsCache arayüzlerini uygulayan bir sınıftır. 
// Bu sınıf, pricing_limits verilerini bellekte tutar ve hızlı erişim sağlar.
public class PricingLimitsCache : IPricingLimitsProvider, IPricingLimitsCache
{
    // _limits, sembol ve PricingLimit nesnelerini tutan bir ConcurrentDictionary'dir.
    //Thread-safe bir yapı sağlar.Aynı anda birden fazla thread güvenli şekilde okuyup yazabilir
    private readonly ConcurrentDictionary<string, PricingLimit> _limits = new(StringComparer.OrdinalIgnoreCase);


    // GetLimit metodu, verilen sembol için pricing limitini döner. Eğer sembol bulunamazsa null döner.
    public PricingLimit? GetLimit(string symbol)
    {
        return _limits.TryGetValue(symbol, out var limit) ? limit : null;
    }

    // UpdateLimits metodu, verilen pricing limitlerini cache'e günceller. Mevcut limitler temizlenir ve yeni limitler eklenir.
    public void UpdateLimits(IReadOnlyList<PricingLimit> limits)
    {
        _limits.Clear();
        foreach (var limit in limits)
            _limits[limit.Symbol.Trim().ToUpper().Replace("/", "")] = limit;
    }
}
