using FixTrading.Common.Dtos.Instrument;
using FixTrading.Domain.Entities;
using FixTrading.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Repositories;

// Instrument veritabanı işlemlerini yönetir; domain ile EF DtoInstrument arasında eşleme yapar.
public class InstrumentRepository : IInstrumentRepository
{
    private readonly AppDbContext _context;

    // AppDbContext, veritabanı bağlantısı ve işlemleri için kullanılır; dependency injection ile sağlanır.
    public InstrumentRepository(AppDbContext context)
    {
        _context = context;
    }

    // CRUD işlemleri: InsertAsync, FetchByIdAsync, FetchAllAsync, UpdateExistingAsync, RemoveByIdAsync
    public async Task InsertAsync(Instrument instrument)
    {
        await _context.Instruments.AddAsync(ToDto(instrument));
        await _context.SaveChangesAsync();
    }

    public async Task<Instrument?> FetchByIdAsync(Guid id)
    {
        var row = await _context.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        return row is null ? null : ToDomain(row);
    }

    public async Task<List<Instrument>> FetchAllAsync()
    {
        var rows = await _context.Instruments
            .AsNoTracking()
            .ToListAsync();
        return rows.ConvertAll(ToDomain);
    }

    public async Task UpdateExistingAsync(Guid id, Instrument instrument)
    {
        var existing = await _context.Instruments.FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
            return;

        var dto = ToDto(instrument);
        dto.Id = id;
        _context.Entry(existing).CurrentValues.SetValues(dto);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveByIdAsync(Guid id)
    {
        var existing = await _context.Instruments.FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
            return;

        _context.Instruments.Remove(existing);
        await _context.SaveChangesAsync();
    }

    private static DtoInstrument ToDto(Instrument d) => new()
    {
        Id = d.Id,
        Symbol = d.Symbol,
        Base = d.Base,
        Quote = d.Quote,
        RecordDate = d.RecordDate,
        RecordUser = d.RecordUser,
        RecordCreateDate = d.RecordCreateDate
    };

    private static Instrument ToDomain(DtoInstrument dto) => new()
    {
        Id = dto.Id,
        Symbol = dto.Symbol,
        Base = dto.Base,
        Quote = dto.Quote,
        RecordDate = dto.RecordDate,
        RecordUser = dto.RecordUser,
        RecordCreateDate = dto.RecordCreateDate
    };
}
