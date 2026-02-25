using FixTrading.Domain.Interfaces;
using FixTrading.Common.Dtos.FixSymbol;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Repositories;

// Bu repository, FixSymbol veritabanı işlemlerini yönetir
// DtoFixSymbol burada doğrudan FixSymbol tablosu gibi kullanılıyor 
public class FixSymbolRepository : IBaseRepository<DtoFixSymbol>
{
    private readonly AppDbContext _context;

    // DbContext dependency injection ile alınır
    public FixSymbolRepository(AppDbContext context)
    {
        _context = context;
    }


    // Yeni bir FixSymbol kaydı ekler
    public async Task InsertAsync(DtoFixSymbol dto)
    {
        await _context.FixSymbols.AddAsync(dto);
        await _context.SaveChangesAsync();
    }

    // Id'ye göre FixSymbol kaydı getirir (yoksa null döner)
    public async Task<DtoFixSymbol?> FetchByIdAsync(long id)
    {
        return await _context.FixSymbols
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    // Tüm FixSymbol kayıtlarını listeler
    public async Task<List<DtoFixSymbol>> FetchAllAsync()
    {
        return await _context.FixSymbols
            .AsNoTracking()
            .ToListAsync();
    }

    // Var olan FixSymbol kaydını günceller
    public async Task UpdateExistingAsync(long id, DtoFixSymbol dto)
    {
        var existing = await _context.FixSymbols.FirstOrDefaultAsync(x => x.Id == id);

        if (existing != null)
        {
            dto.Id = id;
            _context.Entry(existing).CurrentValues.SetValues(dto);
            await _context.SaveChangesAsync();
        }
    }

    // Id'ye göre FixSymbol kaydını siler
    public async Task RemoveByIdAsync(long id)
    {
        var existing = await _context.FixSymbols.FirstOrDefaultAsync(x => x.Id == id);

        if (existing != null)
        {
            _context.FixSymbols.Remove(existing);
            await _context.SaveChangesAsync();
        }
    }
}

