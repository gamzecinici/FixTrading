using FixTrading.Common.Dtos.Instrument;
using FixTrading.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Repositories;

/// <summary>
/// Instrument veritabanı işlemlerini yönetir. instruments tablosunu kullanır.
/// </summary>
public class InstrumentRepository : IInstrumentRepository
{
    private readonly AppDbContext _context;

    public InstrumentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(DtoInstrument dto)
    {
        await _context.Instruments.AddAsync(dto);
        await _context.SaveChangesAsync();
    }

    public async Task<DtoInstrument?> FetchByIdAsync(Guid id)
    {
        return await _context.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<DtoInstrument>> FetchAllAsync()
    {
        return await _context.Instruments
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateExistingAsync(Guid id, DtoInstrument dto)
    {
        var existing = await _context.Instruments.FirstOrDefaultAsync(x => x.Id == id);
        if (existing != null)
        {
            dto.Id = id;
            _context.Entry(existing).CurrentValues.SetValues(dto);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveByIdAsync(Guid id)
    {
        var existing = await _context.Instruments.FirstOrDefaultAsync(x => x.Id == id);
        if (existing != null)
        {
            _context.Instruments.Remove(existing);
            await _context.SaveChangesAsync();
        }
    }
}
