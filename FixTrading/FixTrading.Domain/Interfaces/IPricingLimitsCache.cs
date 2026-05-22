using FixTrading.Domain.Entities;

namespace FixTrading.Domain.Interfaces;

// PostgreSQL'den alınan pricing limitlerinin uygulama belleğindeki cache yapısına yazılmasını sağlar.
public interface IPricingLimitsCache
{
    void UpdateLimits(IReadOnlyList<SymbolPricingLimit> limits);
}
