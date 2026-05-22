using FixTrading.Domain.Entities;

namespace FixTrading.Domain.Interfaces;

// PostgreSQL'den pricing limitlerini okumak için port.
public interface IPricingLimitsRepository
{
    Task<List<SymbolPricingLimit>> FetchAllAsync();
}
