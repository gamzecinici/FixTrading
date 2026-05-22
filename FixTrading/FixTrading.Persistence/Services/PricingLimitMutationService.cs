using FixTrading.Application.Contracts;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Application.Interfaces.Pricing;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Services;

//Fiyat limitlerini guncellemek icin kullanilan servis sinifidir, bu servis veritabanindan ilgili limit kaydini bulur, eger kayit bulunamazsa false doner,
public sealed class PricingLimitMutationService : IPricingLimitMutationService
{
    //Veritabanindan ilgili limit kaydini bulmak ve guncellemek icin kullanilir
    private readonly AppDbContext _db;
    private readonly IMarketHubService _marketHubService;

    public PricingLimitMutationService(AppDbContext db, IMarketHubService marketHubService)
    {
        _db = db;
        _marketHubService = marketHubService;
    }

    //Fiyat limitlerini guncellemek icin kullanilir
    public async Task<bool> TryUpdatePricingLimitAsync(
        Guid limitId,
        decimal minMid,
        decimal maxMid,
        decimal maxSpread,
        string auditName,
        CancellationToken cancellationToken = default)
    {
        //Limit kaydi db den cekilir
        var limit = await _db.PricingLimits
            .Include(x => x.Instrument)
            .FirstOrDefaultAsync(x => x.Id == limitId, cancellationToken);
        if (limit is null)
            return false;

        //Guncelleme islemi yapilir
        var u = DateTime.UtcNow;
        var now = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, u.Second, DateTimeKind.Utc);
        limit.MinMid = minMid;
        limit.MaxMid = maxMid;
        limit.MaxSpread = maxSpread;
        limit.RecordDate = now;
        limit.RecordUser = auditName;

        //Eger limit kaydina bagli bir enstruman kaydi varsa, onun da audit bilgileri guncellenir
        if (limit.Instrument is not null)
        {
            limit.Instrument.RecordDate = now;
            limit.Instrument.RecordUser = auditName;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // UI'ya guncellemeyi bildir
        if (limit.Instrument is not null)
        {
            var updated = new AdminSymbolListItemDto(
                limit.Instrument.Id,
                limit.Id,
                limit.Instrument.Symbol,
                limit.MinMid,
                limit.MaxMid,
                limit.MaxSpread);
            
            await _marketHubService.NotifyInstrumentUpdatedAsync(updated, cancellationToken);
        }

        return true;
    }
}
