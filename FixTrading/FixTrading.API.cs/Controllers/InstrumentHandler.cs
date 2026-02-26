using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.API.Controllers;

/// <summary>
/// İç kullanım için Instrument işlemlerini yöneten handler.
/// IInstrumentService interface altında çalışır.
/// </summary>
public class InstrumentHandler
{
    private readonly IInstrumentService _instrumentService;

    public InstrumentHandler(IInstrumentService instrumentService)
    {
        _instrumentService = instrumentService;
    }

    public Task<List<DtoInstrument>> RetrieveAllInstrumentsAsync()
        => _instrumentService.RetrieveAllInstrumentsAsync();

    public Task<DtoInstrument?> RetrieveInstrumentByIdAsync(Guid id)
        => _instrumentService.RetrieveInstrumentByIdAsync(id);

    public Task CreateNewInstrumentAsync(DtoInstrument instrument)
        => _instrumentService.CreateNewInstrumentAsync(instrument);

    public Task UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument)
        => _instrumentService.UpdateExistingInstrumentAsync(id, instrument);

    public Task DeleteInstrumentByIdAsync(Guid id)
        => _instrumentService.DeleteInstrumentByIdAsync(id);
}
