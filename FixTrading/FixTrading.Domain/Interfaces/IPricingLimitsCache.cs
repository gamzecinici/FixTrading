using FixTrading.Common.Dtos.Pricing;

namespace FixTrading.Domain.Interfaces;

//PostgreSQL’den alınan pricing limitlerinin uygulama belleğindeki cache yapısına yazılmasını sağlar
public interface IPricingLimitsCache
{
    void UpdateLimits(IReadOnlyList<PricingLimit> limits);  //Cache'i güncellemek için kullanılan metot
}
