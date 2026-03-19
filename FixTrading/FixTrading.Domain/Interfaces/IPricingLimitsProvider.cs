using FixTrading.Common.Dtos.Pricing;

namespace FixTrading.Domain.Interfaces;

//Cache’te saklanan pricing limitlerini sembole göre okumayı sağlar
//Örneğin, bir sembolün fiyatının limit ihlali yapıp yapmadığını kontrol etmek için kullanılabilir.
public interface IPricingLimitsProvider
{
    PricingLimit? GetLimit(string symbol);    //Belirtilen sembol için pricing limitlerini döndürür. Eğer sembol için limit bulunamazsa null döner.
}
