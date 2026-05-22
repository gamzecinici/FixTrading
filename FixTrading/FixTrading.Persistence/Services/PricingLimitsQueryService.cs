using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Common.Dtos.FinancialAnalytics;
using FixTrading.Common.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Services;

//Fiyat limitleriyle ilgili sadece okuma islemlerini yoneten servistir, bu servis veri ceker ve sadece okuma islemleri yapar,
//guncelleme islemleri IPricingLimitMutationService tarafindan yapilir, bu servis sadece sorgulama amacli kullanilir
public sealed class PricingLimitsQueryService : IPricingLimitsQueryService
{
    //Veritabanindan aktif enstruman sembollerini cekmek icin kullanilir, eger aktif sembol yoksa bos liste doner
    private readonly AppDbContext _db;

    //Veritabanindan aktif enstruman sembollerine gore fiyat limitlerini cekmek icin kullanilir, eger aktif sembol yoksa bos liste doner
    public PricingLimitsQueryService(AppDbContext db)
    {
        _db = db;
    }

    //Veritabanindan aktif fiyat limitlerini cekmek icin kullanilir, eger aktif limit yoksa bos liste doner
    public async Task<IReadOnlyList<string>> GetDistinctActiveInstrumentSymbolsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.PricingLimits
            .AsNoTracking()
            .Include(x => x.Instrument)
            .Where(x => x.Instrument != null && x.Instrument.Symbol.Trim() != "")
            .Select(x => x.Instrument!.Symbol.Trim())
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    //Veritabanindan aktif enstruman sembollerine gore fiyat limitlerini cekmek icin kullanilir. Tekrarli semboller varsa, sadece bir tanesi doner.
    public async Task<IReadOnlyList<PricingLimitRowVm>> GetLimitRowsOrderedBySymbolAsync(CancellationToken cancellationToken = default)
    {
        return await _db.PricingLimits
            .AsNoTracking()
            .Include(x => x.Instrument)
            .OrderBy(x => x.Instrument != null ? x.Instrument.Symbol : string.Empty)
            .Select(x => new PricingLimitRowVm
            {
                Id = x.Id,
                InstrumentId = x.Instrument != null ? x.Instrument.Id : Guid.Empty,
                Symbol = x.Instrument != null ? x.Instrument.Symbol : "-",
                MinMid = x.MinMid,
                MaxMid = x.MaxMid,
                MaxSpread = x.MaxSpread
            })
            .ToListAsync(cancellationToken);
    }

    //Finansal analiz icin gerekli olan aktif limitleri veritabanindan cekmek icin kullanilir
    public async Task<IReadOnlyList<FinancialActiveLimitRow>> GetActiveLimitsForFinancialAnalyticsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PricingLimits
            .AsNoTracking()
            .Include(p => p.Instrument)
            .Where(p => p.Instrument != null && p.Instrument.Symbol.Trim() != "")
            .Select(p => new FinancialActiveLimitRow
            {
                Symbol = p.Instrument!.Symbol.Trim(),
                MinMid = p.MinMid,
                MaxMid = p.MaxMid,
                MaxSpread = p.MaxSpread
            })
            .ToListAsync(cancellationToken);

        return rows;
    }
}
