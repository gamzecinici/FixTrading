using FixTrading.Application.Interfaces.Fix;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Domain.Interfaces;
using FixTrading.Persistence;
using FixTrading.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.API.Services;

//Bu servis, enstrüman sembolleri ve ilgili fiyatlama limitlerini yönetmek için kullanılır.
//Admin paneli üzerinden yeni semboller eklemek, mevcut sembolleri silmek ve sembol listesini görüntülemek için gerekli iş mantığını içerir.
public class InstrumentSymbolAdminService
{
    private const decimal MaxAllowed = 9_999_999_999m;
    private readonly AppDbContext _db;
    private readonly IFixSession _fixSession;
    private readonly IPricingLimitsRepository _pricingLimitsRepository;
    private readonly IPricingLimitsCache _pricingLimitsCache;
    private readonly ILatestPriceStore _latestPriceStore;
    private readonly IInMemoryLastPriceStore _inMemoryLastPriceStore;


    //Admin işlemleri sırasında sembol ekleme veya silme gibi durumlarda, FIX oturumunu güncellemek ve fiyatlama limitlerini önbelleğe almak için gerekli bağımlılıkları alır.
    public InstrumentSymbolAdminService(
        AppDbContext db,
        IFixSession fixSession,
        IPricingLimitsRepository pricingLimitsRepository,
        IPricingLimitsCache pricingLimitsCache,
        ILatestPriceStore latestPriceStore,
        IInMemoryLastPriceStore inMemoryLastPriceStore)
    {
        _db = db;
        _fixSession = fixSession;
        _pricingLimitsRepository = pricingLimitsRepository;
        _pricingLimitsCache = pricingLimitsCache;
        _latestPriceStore = latestPriceStore;
        _inMemoryLastPriceStore = inMemoryLastPriceStore;
    }

    //Mevcut sembollerin ve fiyatlama limitlerinin listesini döndürür. Bu, admin panelinde sembol yönetimi sayfasında görüntülenmek üzere kullanılır.
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

    //Yeni bir sembol eklerken, sembolün geçerli olup olmadığını kontrol eder, veritabanına kaydeder, 
    //FIX oturumunu günceller ve fiyatlama limitlerini önbelleğe alır. Ayrıca, eklenen sembolün bilgilerini döndürür.
    public async Task<(bool Ok, string? Error, AdminSymbolListItemDto? Created)> AddAsync(
        string symbol,
        decimal minMid,
        decimal maxMid,
        decimal maxSpread,
        string? recordUser,
        CancellationToken cancellationToken = default)
    {
        //veri doğrulama: sembolün boş olmaması, 20 karakteri geçmemesi ve fiyatlama limitlerinin geçerli aralıkta olması sağlanır.
        var normalized = NormalizeSymbol(symbol);
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 20)
            return (false, "Sembol boş olamaz ve 20 karakteri geçemez.", null);

        //fiyatlama limitlerinin geçerli aralıkta olup olmadığı kontrol edilir.
        if (minMid < 0 || minMid > MaxAllowed ||
            maxMid < 0 || maxMid > MaxAllowed ||
            maxSpread < 0 || maxSpread > MaxAllowed ||
            minMid > maxMid)
            return (false, "Geçersiz limit: değerler 0–9,999,999,999 arasında ve MinMid ≤ MaxMid olmalıdır.", null);

        //aynı sembolün zaten var olup olmadığı kontrol edilir. Bu, sembolün benzersiz olmasını sağlar.
        var exists = await _db.Instruments.AnyAsync(
            i => i.Symbol.Trim().ToUpper().Replace("/", "") == normalized,
            cancellationToken);
        if (exists)
            return (false, "Bu sembol zaten kayıtlı.", null);

        //yeni sembol ve fiyatlama limitleri oluşturulur, veritabanına kaydedilir ve FIX oturumu güncellenir.
        var now = DateTime.UtcNow;
        var user = string.IsNullOrWhiteSpace(recordUser) ? "admin" : recordUser.Trim();
        if (user.Length > 50)
            user = user[..50];

        //yeni sembol ve fiyatlama limitleri oluşturulur, veritabanına kaydedilir ve FIX oturumu güncellenir.
        var instrumentId = Guid.NewGuid();
        var limitId = Guid.NewGuid();

        
        var instrument = new DtoInstrument
        {
            Id = instrumentId,
            Symbol = normalized,
            Description = normalized,
            TickSize = 0.00001m,
            RecordDate = now,
            RecordCreateDate = now,
            RecordUser = user
        };

        //yeni sembol ve fiyatlama limitleri oluşturulur, veritabanına kaydedilir ve FIX oturumu güncellenir.
        var limit = new PricingLimitEntity
        {
            Id = limitId,
            InstrumentId = instrumentId,
            MinMid = minMid,
            MaxMid = maxMid,
            MaxSpread = maxSpread,
            RecordDate = now,
            RecordCreateDate = now,
            RecordUser = user
        };

        //veritabanı işlemi sırasında herhangi bir hata oluşursa, işlemi geri alır ve hatayı döndürür.
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
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

        await RefreshPricingCacheAsync(cancellationToken);

        // Uygulama çalışırken yeni sembol eklenince FixListenerWorker tekrar çalışmaz;
        // bu yüzden burada anında FIX subscribe gerekir (FixApp._activeSymbols'a da eklenir).
        if (_fixSession.IsConnected)
            _fixSession.Subscribe(normalized);

        Console.WriteLine($"[ADMIN][SYMBOL][ADD] Symbol={normalized}, InstrumentId={instrumentId}, LimitId={limitId}, MinMid={minMid}, MaxMid={maxMid}, MaxSpread={maxSpread}, User={user}");

        var created = new AdminSymbolListItemDto(instrumentId, limitId, normalized, minMid, maxMid, maxSpread);
        return (true, null, created);
    }


    /// <summary>
    /// Admin panelinden "sil" ile enstrüman + limit satırlarını kaldırır ve dağıtık önbelleği temizler.
    /// </summary>
    /// <remarks>
    /// Sıra önemli: önce <see cref="IFixSession.Unsubscribe"/> — FixApp içinde <c>_activeSymbols</c> düşer ve
    /// sunucu gecikmeyle tick gönderse bile <see cref="FixApp"/> Render aşamasında elenir.
    /// Ardından DB commit, Redis <c>latest:price:SYMBOL</c> ve RAM <see cref="IInMemoryLastPriceStore"/> silinir.
    /// </remarks>
    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var instrument = await _db.Instruments.FirstOrDefaultAsync(i => i.Id == instrumentId, cancellationToken);
        if (instrument is null)
            return (false, "Sembol bulunamadı.");

        var symbol = NormalizeSymbol(instrument.Symbol);

        var limits = await _db.PricingLimits.Where(p => p.InstrumentId == instrumentId).ToListAsync(cancellationToken);

        // Market data iptali + yerel abonelik whitelist'ten çıkarma (FixApp)
        _fixSession.Unsubscribe(symbol);

        _db.PricingLimits.RemoveRange(limits);
        _db.Instruments.Remove(instrument);
        await _db.SaveChangesAsync(cancellationToken);

        await _latestPriceStore.RemoveLatestAsync(symbol);
        _inMemoryLastPriceStore.RemoveLatest(symbol);

        Console.WriteLine($"[ADMIN][SYMBOL][DELETE] Symbol={symbol}, InstrumentId={instrumentId}, RemovedLimits={limits.Count}");

        await RefreshPricingCacheAsync(cancellationToken);
        return (true, null);
    }


    //Fiyatlama limitlerini önbelleğe almak için kullanılan yardımcı bir yöntemdir.
    //Böylece, sembol ekleme veya silme işlemlerinden sonra önbellekteki limitler güncellenir ve uygulamanın diğer bölümlerinde doğru limitler kullanılır.
    private async Task RefreshPricingCacheAsync(CancellationToken cancellationToken)
    {
        var limits = await _pricingLimitsRepository.FetchAllAsync();
        _pricingLimitsCache.UpdateLimits(limits);
    }

    private static string NormalizeSymbol(string symbol) =>
        symbol.Trim().ToUpperInvariant().Replace("/", "").Replace(" ", "");
}


//Admin panelinde sembol yönetimi sayfasında görüntülenmek üzere kullanılan DTO'lardır.
public record AdminSymbolListItemDto(
    Guid InstrumentId,
    Guid LimitId,
    string Symbol,
    decimal MinMid,
    decimal MaxMid,
    decimal MaxSpread);

public record AddSymbolApiRequest(string Symbol, decimal MinMid, decimal MaxMid, decimal MaxSpread);
