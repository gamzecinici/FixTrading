using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Application.Services;

/// <summary>
/// Instrument işlemlerini yöneten uygulama servisi.
/// Repository üzerinden Instrument tablosuna erişir.
/// </summary>
public class InstrumentService : IInstrumentService
{
    private readonly IInstrumentRepository _instrumentRepository;

    public InstrumentService(IInstrumentRepository instrumentRepository)
    {
        _instrumentRepository = instrumentRepository;
    }

    public Task<List<DtoInstrument>> RetrieveAllInstrumentsAsync()
        => _instrumentRepository.FetchAllAsync();

    public Task<DtoInstrument?> RetrieveInstrumentByIdAsync(Guid id)
        => _instrumentRepository.FetchByIdAsync(id);

    public Task CreateNewInstrumentAsync(DtoInstrument instrument)
        => _instrumentRepository.InsertAsync(instrument);

    public Task UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument)
        => _instrumentRepository.UpdateExistingAsync(id, instrument);

    public Task DeleteInstrumentByIdAsync(Guid id)
        => _instrumentRepository.RemoveByIdAsync(id);
}
