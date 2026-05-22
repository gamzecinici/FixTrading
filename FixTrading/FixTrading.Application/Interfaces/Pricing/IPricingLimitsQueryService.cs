using FixTrading.Common.Dtos.FinancialAnalytics;
using FixTrading.Common.ViewModels.Admin;

//Fiyat limitleriyle ilgili sadece okuma islemlerini yoneten servistir
namespace FixTrading.Application.Interfaces.Pricing;

//Bu servis veri ceker ve sadece okuma islemleri yapar, guncelleme islemleri IPricingLimitMutationService tarafindan yapilir, bu servis sadece sorgulama amacli kullanilir
public interface IPricingLimitsQueryService
{
    //Veritabanindan aktif enstruman sembollerini cekmek icin kullanilir, eger aktif sembol yoksa bos liste doner
    Task<IReadOnlyList<string>> GetDistinctActiveInstrumentSymbolsAsync(CancellationToken cancellationToken = default);

    //Veritabanindan aktif enstruman sembollerine gore fiyat limitlerini cekmek icin kullanilir, eger aktif sembol yoksa bos liste doner
    Task<IReadOnlyList<PricingLimitRowVm>> GetLimitRowsOrderedBySymbolAsync(CancellationToken cancellationToken = default);

    //Veritabanindan aktif fiyat limitlerini cekmek icin kullanilir, eger aktif limit yoksa bos liste doner
    Task<IReadOnlyList<FinancialActiveLimitRow>> GetActiveLimitsForFinancialAnalyticsAsync(CancellationToken cancellationToken = default);
}
