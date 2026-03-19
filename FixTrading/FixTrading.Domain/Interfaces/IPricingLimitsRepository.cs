using FixTrading.Common.Dtos.Pricing;

namespace FixTrading.Domain.Interfaces;

//Bu interface, PostgreSQL’den pricing limitlerini çekmekle ilgili işlemleri tanımlar.
//Veriyi database’den okuyup uygulamaya getiren veya uygulamadan database’e yazan yapı.
public interface IPricingLimitsRepository
{
    Task<List<PricingLimit>> FetchAllAsync();    //Tüm pricing limitlerini asenkron olarak çekmek için bir metot tanımı.
}
