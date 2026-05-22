using FixTrading.Domain.Entities;

namespace FixTrading.Domain.Interfaces;

// Bu interface, enstrümanlarla ilgili temel veri erişim operasyonlarını tanımlar.

public interface IInstrumentRepository
{
    Task InsertAsync(Instrument instrument);

    Task<Instrument?> FetchByIdAsync(Guid id);

    Task<List<Instrument>> FetchAllAsync();

    Task UpdateExistingAsync(Guid id, Instrument instrument);

    Task RemoveByIdAsync(Guid id);
}
