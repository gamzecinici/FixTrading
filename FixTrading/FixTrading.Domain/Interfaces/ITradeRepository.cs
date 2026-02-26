using FixTrading.Common.Dtos.Trade;

namespace FixTrading.Domain.Interfaces;

/// <summary>
/// Trade tablosu için repository arayüzü. Guid tabanlı kimlik kullanır.
/// </summary>
public interface ITradeRepository
{
    Task InsertAsync(DtoTrade dto);
    Task<DtoTrade?> FetchByIdAsync(Guid id);
    Task<List<DtoTrade>> FetchAllAsync();
    Task UpdateExistingAsync(Guid id, DtoTrade dto);
    Task RemoveByIdAsync(Guid id);
}
