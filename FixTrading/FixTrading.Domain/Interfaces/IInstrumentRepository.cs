using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.Domain.Interfaces;

/// <summary>
/// Instrument tablosu için repository arayüzü. Guid tabanlı kimlik kullanır.
/// </summary>
public interface IInstrumentRepository
{
    Task InsertAsync(DtoInstrument dto);
    Task<DtoInstrument?> FetchByIdAsync(Guid id);
    Task<List<DtoInstrument>> FetchAllAsync();
    Task UpdateExistingAsync(Guid id, DtoInstrument dto);
    Task RemoveByIdAsync(Guid id);
}
