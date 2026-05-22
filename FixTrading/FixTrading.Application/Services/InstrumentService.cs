using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Domain.Entities;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Application.Services;

// IInstrumentService: API/DTO ile domain repository arasında eşleme yapar.
public class InstrumentService : IInstrumentService
{
    private readonly IInstrumentRepository _instrumentRepository;

    public InstrumentService(IInstrumentRepository instrumentRepository)
    {
        _instrumentRepository = instrumentRepository;
    }

    public async Task<List<DtoInstrument>> RetrieveAllInstrumentsAsync()
    {
        var list = await _instrumentRepository.FetchAllAsync();
        return list.ConvertAll(ToDto);
    }

    public async Task<DtoInstrument?> RetrieveInstrumentByIdAsync(Guid id)
    {
        var row = await _instrumentRepository.FetchByIdAsync(id);
        return row is null ? null : ToDto(row);
    }

    public Task CreateNewInstrumentAsync(DtoInstrument instrument)
        => _instrumentRepository.InsertAsync(ToDomain(instrument));

    public Task UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument)
        => _instrumentRepository.UpdateExistingAsync(id, ToDomain(instrument));

    public Task DeleteInstrumentByIdAsync(Guid id)
        => _instrumentRepository.RemoveByIdAsync(id);

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
