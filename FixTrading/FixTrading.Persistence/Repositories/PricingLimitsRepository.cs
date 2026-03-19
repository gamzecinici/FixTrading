using FixTrading.Common.Dtos.Pricing;
using FixTrading.Domain.Interfaces;
using FixTrading.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Repositories;

// Bu sınıf, IPricingLimitsRepository arayüzünü uygulayan bir repository sınıfıdır.
public class PricingLimitsRepository : IPricingLimitsRepository
{
    private readonly AppDbContext _context;

    // AppDbContext, veritabanı işlemlerini gerçekleştirmek için kullanılan bir sınıftır ve dependency injection ile sağlanır.
    public PricingLimitsRepository(AppDbContext context)
    {
        _context = context;
    }

    // FetchAllAsync metodu, veritabanından tüm pricing_limits kayıtlarını asenkron olarak çeker ve DtoPricingLimit listesi olarak döner.
    public async Task<List<PricingLimit>> FetchAllAsync()
    {
        return await _context.PricingLimits
            .AsNoTracking()
            .Include(p => p.Instrument)
            .Where(p => p.Instrument != null)
            .Select(p => new PricingLimit
            {
                Symbol = p.Instrument!.Symbol.Trim().ToUpper().Replace("/", ""),
                MinMid = p.MinMid,
                MaxMid = p.MaxMid,
                MaxSpread = p.MaxSpread
            })
            .ToListAsync();
    }
}
