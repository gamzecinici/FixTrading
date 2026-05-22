using FixTrading.Domain.Entities;
using FixTrading.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Repositories;

// IPricingLimitsRepository: PostgreSQL'den okur, domain modeline dönüştürür.
public class PricingLimitsRepository : IPricingLimitsRepository
{
    private readonly AppDbContext _context;

    public PricingLimitsRepository(AppDbContext context)
    {
        _context = context;
    }

    // Veritabanından tüm fiyatlandırma limitlerini çeker, domain modeline dönüştürür ve döndürür.
    public async Task<List<SymbolPricingLimit>> FetchAllAsync()
    {
        return await _context.PricingLimits
            .AsNoTracking()
            .Include(p => p.Instrument)
            .Where(p => p.Instrument != null)
            .Select(p => new SymbolPricingLimit
            {
                Symbol = p.Instrument!.Symbol.Trim().ToUpper().Replace("/", ""),
                MinMid = p.MinMid,
                MaxMid = p.MaxMid,
                MaxSpread = p.MaxSpread
            })
            .ToListAsync();
    }
}
