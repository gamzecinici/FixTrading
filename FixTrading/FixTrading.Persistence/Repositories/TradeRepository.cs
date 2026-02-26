using FixTrading.Common.Dtos.Trade;
using FixTrading.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Repositories;

/// <summary>
/// Trade veritabanı işlemlerini yönetir. trades tablosunu kullanır.
/// </summary>
public class TradeRepository : ITradeRepository
{
    private readonly AppDbContext _context;

    public TradeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(DtoTrade dto)
    {
        await _context.Trades.AddAsync(dto);
        await _context.SaveChangesAsync();
    }

    public async Task<DtoTrade?> FetchByIdAsync(Guid id)
    {
        return await _context.Trades
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<DtoTrade>> FetchAllAsync()
    {
        return await _context.Trades
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateExistingAsync(Guid id, DtoTrade dto)
    {
        var existing = await _context.Trades.FirstOrDefaultAsync(x => x.Id == id);
        if (existing != null)
        {
            dto.Id = id;
            _context.Entry(existing).CurrentValues.SetValues(dto);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveByIdAsync(Guid id)
    {
        var existing = await _context.Trades.FirstOrDefaultAsync(x => x.Id == id);
        if (existing != null)
        {
            _context.Trades.Remove(existing);
            await _context.SaveChangesAsync();
        }
    }
}
