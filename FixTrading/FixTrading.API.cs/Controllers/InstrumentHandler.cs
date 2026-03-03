using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.API.Controllers;

// Controller ile Service arasındaki ara katman
public class InstrumentHandler
{
    // IInstrumentService, enstrümanlarla ilgili işlemleri gerçekleştiren bir servis arayüzüdür.
    private readonly IInstrumentService _instrumentService;

    // Kurucu metot, IInstrumentService bağımlılığını alır ve sınıf içinde kullanılmak üzere saklar.
    public InstrumentHandler(IInstrumentService instrumentService)
    {
        _instrumentService = instrumentService;
    }

    // Tüm enstrümanları asenkron olarak getirir ve bir liste olarak döndürür.
    public Task<List<DtoInstrument>> RetrieveAllAsync()
        => _instrumentService.RetrieveAllInstrumentsAsync();

    // Belirli bir ID'ye sahip enstrümanı asenkron olarak getirir ve DtoInstrument nesnesi olarak döndürür.
    public Task<DtoInstrument?> RetrieveByIdAsync(Guid id)
        => _instrumentService.RetrieveInstrumentByIdAsync(id);

    // Yeni bir enstrüman oluşturur ve asenkron olarak kaydeder.
    public Task CreateAsync(DtoInstrument instrument)
        => _instrumentService.CreateNewInstrumentAsync(instrument);

    // Belirli bir ID'ye sahip enstrümanı günceller ve asenkron olarak kaydeder.
    public Task UpdateAsync(Guid id, DtoInstrument instrument)
        => _instrumentService.UpdateExistingInstrumentAsync(id, instrument);

    // Belirli bir ID'ye sahip enstrümanı asenkron olarak siler.
    public Task DeleteAsync(Guid id)
        => _instrumentService.DeleteInstrumentByIdAsync(id);
}
