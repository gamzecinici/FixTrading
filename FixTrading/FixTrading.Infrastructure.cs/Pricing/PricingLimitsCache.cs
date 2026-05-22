using System.Collections.Concurrent;
using FixTrading.Domain.Entities;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Infrastructure.Pricing;

// Bellekte sembol → limit sözlüğü; IPricingLimitsProvider ve IPricingLimitsCache.
public class PricingLimitsCache : IPricingLimitsProvider, IPricingLimitsCache
{
    private readonly ConcurrentDictionary<string, SymbolPricingLimit> _limits = new(StringComparer.OrdinalIgnoreCase);

    public SymbolPricingLimit? GetLimit(string symbol)
    {
        return _limits.TryGetValue(symbol, out var limit) ? limit : null;
    }

    public void UpdateLimits(IReadOnlyList<SymbolPricingLimit> limits)
    {
        _limits.Clear();
        foreach (var limit in limits)
            _limits[limit.Symbol.Trim().ToUpper().Replace("/", "")] = limit;
    }
}
