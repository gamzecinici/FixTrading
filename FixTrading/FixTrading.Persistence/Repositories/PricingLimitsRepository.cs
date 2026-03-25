using FixTrading.Common.Dtos.Pricing;
using FixTrading.Domain.Interfaces;
using FixTrading.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Repositories;

// Bu sınıf, IPricingLimitsRepository arayüzünü uygulayan bir repository sınıfıdır.
// Bu sınıf, PostgreSQL veritabanından pricing limitlerini çekmekle ilgili işlemleri gerçekleştirir.
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
        //Veritabanındaki limitleri, symbol ile birlikte çekip temizleyerek sistemin kullanacağı formata dönüştürür.
        return await _context.PricingLimits
            .AsNoTracking()    // Veritabanından çekilen verilerin izlenmemesi, sadece okunması için optimize edilir.
            .Include(p => p.Instrument)   // PricingLimits tablosundaki Instrument ilişkisi de dahil edilir. JOİN işlemi yapılır.
            .Where(p => p.Instrument != null)   //Null olmayan instrumentlara sahip kayıtlar filtrelenir.
            .Select(p => new PricingLimit       // Veritabanından çekilen her kaydı DtoPricingLimit nesnesine dönüştürür.
            {
                Symbol = p.Instrument!.Symbol.Trim().ToUpper().Replace("/", ""),
                MinMid = p.MinMid,
                MaxMid = p.MaxMid,
                MaxSpread = p.MaxSpread
            })
            .ToListAsync();
    }
}
