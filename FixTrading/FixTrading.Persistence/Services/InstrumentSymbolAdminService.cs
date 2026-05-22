using FixTrading.Application.Contracts;
using FixTrading.Application.Interfaces.Admin;
using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Services;

//Enstruman sembollerini yoneten servis sinifidir, bu servis enstruman sembollerinin eklenmesi, silinmesi ve listelenmesi islemlerini yapar,
//sadece admin paneli tarafindan kullanilir
public sealed class InstrumentSymbolAdminService : IInstrumentSymbolAdminService
{
    private const decimal MaxAllowed = 9_999_999_999m;                      //sistem tarafindan kabul edilen max numeric deger 
    private readonly AppDbContext _db;
    private readonly IFixSession _fixSession;
    private readonly IPricingLimitsSyncService _pricingLimitsSync;
    private readonly ILatestPriceStore _latestPriceStore;
    private readonly IInMemoryLastPriceStore _inMemoryLastPriceStore;
    private readonly IMarketHubService _marketHubService;

    //tum bagimliliklari alir
    public InstrumentSymbolAdminService(
        AppDbContext db,
        IFixSession fixSession,
        IPricingLimitsSyncService pricingLimitsSync,
        ILatestPriceStore latestPriceStore,
        IInMemoryLastPriceStore inMemoryLastPriceStore,
        IMarketHubService marketHubService)
    {
        _db = db;
        _fixSession = fixSession;
        _pricingLimitsSync = pricingLimitsSync;
        _latestPriceStore = latestPriceStore;
        _inMemoryLastPriceStore = inMemoryLastPriceStore;
        _marketHubService = marketHubService;
    }

    //sistemdeki tum limitleri ve sembolleri listeler. Admin tarafindaki tabloya veri saglamak icin kullanilir
    public async Task<List<AdminSymbolListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.PricingLimits
            .AsNoTracking()
            .Include(x => x.Instrument)
            .Where(x => x.Instrument != null)
            .OrderBy(x => x.Instrument!.Symbol)
            .Select(x => new AdminSymbolListItemDto(
                x.Instrument!.Id,
                x.Id,
                x.Instrument.Symbol,
                x.MinMid,
                x.MaxMid,
                x.MaxSpread))
            .ToListAsync(cancellationToken);
    }

    //yeni bir sembol ekler, sembolun validasyonunu yapar, sembol zaten varsa hata doner
    public async Task<(bool Ok, string? Error, AdminSymbolListItemDto? Created)> AddAsync(
        AddSymbolApiRequest request,
        string? recordUser,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(request.Symbol);

        //sembolun bos olmamasi ve 20 karakteri gecmemesi gerektigini kontrol eder
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 20)
            return (false, "Sembol boş olamaz ve 20 karakteri geçemez.", null);

        //limit degerlerinin 0 ile MaxAllowed arasinda olmasi ve MinMid'in MaxMid'den kucuk veya esit olmasi gerektigini kontrol eder
        if (request.MinMid < 0 || request.MinMid > MaxAllowed ||
            request.MaxMid < 0 || request.MaxMid > MaxAllowed ||
            request.MaxSpread < 0 || request.MaxSpread > MaxAllowed ||
            request.MinMid > request.MaxMid)
            return (false, "Geçersiz limit: değerler 0–9,999,999,999 arasında ve MinMid ≤ MaxMid olmalıdır.", null);

        //sembolun zaten var olup olmadigini kontrol eder, sembolun normalizasyonunu yaparak karsilastirir
        var exists = await _db.Instruments.AnyAsync(
            i => i.Symbol.Trim().ToUpper().Replace("/", "") == normalized,
            cancellationToken);
        if (exists)
            return (false, "Bu sembol zaten kayıtlı.", null);

        //kullanici bilgisinin bos olmamasi gerektigini kontrol eder
        var u = DateTime.UtcNow;
        var now = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, u.Second, DateTimeKind.Utc);
        var rawUser = (recordUser ?? "").Trim();
        if (string.IsNullOrEmpty(rawUser))
            return (false, "İşlemi yapan kullanıcı bilgisi alınamadı. Lütfen yeniden giriş yapın.", null);

        //yeni enstruman ve limit objeleri olusturur, veritabanina kaydeder, cache'i gunceller ve fix seansina subscribe olur
        var instrumentId = Guid.NewGuid();
        var limitId = Guid.NewGuid();

        var instrument = new DtoInstrument
        {
            Id = instrumentId,
            Symbol = normalized,
            Base = request.Base,
            Quote = request.Quote,
            RecordDate = now,
            RecordCreateDate = now,
            RecordUser = rawUser
        };

        var limit = new PricingLimitEntity
        {
            Id = limitId,
            InstrumentId = instrumentId,
            MinMid = request.MinMid,
            MaxMid = request.MaxMid,
            MaxSpread = request.MaxSpread,
            RecordDate = now,
            RecordCreateDate = now,
            RecordUser = rawUser
        };

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        //islemi ya tamamen yap ya da hic yapma prensibiyle veritabanina kaydeder, herhangi bir hata olursa islemi geri alir
        try
        {
            _db.Instruments.Add(instrument);
            _db.PricingLimits.Add(limit);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }

        await _pricingLimitsSync.RefreshCacheFromDatabaseAsync(cancellationToken);

        if (_fixSession.IsConnected)
            _fixSession.Subscribe(normalized);

        Console.WriteLine($"[ADMIN][SYMBOL][ADD] Symbol={normalized}, InstrumentId={instrumentId}, LimitId={limitId}, MinMid={request.MinMid}, MaxMid={request.MaxMid}, MaxSpread={request.MaxSpread}, User={rawUser}");

        var created = new AdminSymbolListItemDto(instrumentId, limitId, normalized, request.MinMid, request.MaxMid, request.MaxSpread);

        // UI'ya yeni enstruman eklendigini bildir
        await _marketHubService.NotifyInstrumentCreatedAsync(created, cancellationToken);

        return (true, null, created);
    }


    //var olan bir sembolun silinmesini saglar
    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var instrument = await _db.Instruments.FirstOrDefaultAsync(i => i.Id == instrumentId, cancellationToken);
        if (instrument is null)
            return (false, "Sembol bulunamadı.");

        var symbol = NormalizeSymbol(instrument.Symbol);

        var limits = await _db.PricingLimits.Where(p => p.InstrumentId == instrumentId).ToListAsync(cancellationToken);

        _fixSession.Unsubscribe(symbol);

        _db.PricingLimits.RemoveRange(limits);
        _db.Instruments.Remove(instrument);
        await _db.SaveChangesAsync(cancellationToken);

        await _latestPriceStore.RemoveLatestAsync(symbol);
        _inMemoryLastPriceStore.RemoveLatest(symbol);

        Console.WriteLine($"[ADMIN][SYMBOL][DELETE] Symbol={symbol}, InstrumentId={instrumentId}, RemovedLimits={limits.Count}");

        await _pricingLimitsSync.RefreshCacheFromDatabaseAsync(cancellationToken);

        // UI'ya enstruman silindigini bildir
        await _marketHubService.NotifyInstrumentDeletedAsync(instrumentId, symbol, cancellationToken);

        return (true, null);
    }

    // sembolun normalizasyonunu yapar, buyuk harfe cevirir, bosluklari ve slash karakterlerini kaldirir
    private static string NormalizeSymbol(string symbol) =>
        symbol.Trim().ToUpperInvariant().Replace("/", "").Replace(" ", "");
}
