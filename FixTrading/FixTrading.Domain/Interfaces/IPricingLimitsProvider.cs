using FixTrading.Domain.Entities;

namespace FixTrading.Domain.Interfaces;

// Cache'te saklanan pricing limitlerini sembole göre okur.
public interface IPricingLimitsProvider
{
    SymbolPricingLimit? GetLimit(string symbol);
}
