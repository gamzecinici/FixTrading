namespace FixTrading.Application.Interfaces.Pricing;

//Fiyat limitlerini guncellemek icin kullanilan servis arayuzdur
public interface IPricingLimitMutationService
{
    //Fiyat limitlerini guncellemek icin kullanilir, eger limitId bulunamazsa false doner, guncelleme basariliysa true doner
    Task<bool> TryUpdatePricingLimitAsync(
        Guid limitId,
        decimal minMid,
        decimal maxMid,
        decimal maxSpread,
        string auditName,
        CancellationToken cancellationToken = default);
}
