using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.Application.Interfaces.Instrument;

/// <summary>
/// Instrument işlemleri için uygulama katmanı arayüzü.
/// FIX market data akışı Instrument tablosu üzerinden yönetilir.
/// </summary>
public interface IInstrumentService
{
    Task<List<DtoInstrument>> RetrieveAllInstrumentsAsync();
    Task<DtoInstrument?> RetrieveInstrumentByIdAsync(Guid id);
    Task CreateNewInstrumentAsync(DtoInstrument instrument);
    Task UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument);
    Task DeleteInstrumentByIdAsync(Guid id);
}
