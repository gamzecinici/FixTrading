using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.API.Controllers;

// Controller ile Service arasındaki ara katman
public class InstrumentHandler
{
    // IInstrumentService, enstrümanlarla ilgili islemleri gerçeklestiren bir servis arayuzudur
    private readonly IInstrumentService _instrumentService;

    // Kurucu metot, IInstrumentService bağimliligini alir ve sinif içinde kullanilmak uzere saklar.
    public InstrumentHandler(IInstrumentService instrumentService)
    {
        _instrumentService = instrumentService;
    }

    // Tüm enstrumanlari asenkron olarak getirir ve bir liste olarak dondurur.
    public Task<List<DtoInstrument>> RetrieveAllAsync()
        => _instrumentService.RetrieveAllInstrumentsAsync();

    // Belirli bir ID'ye sahip enstrumanı asenkron olarak getırır ve DtoInstrument nesnesi olarak dondurur.
    public Task<DtoInstrument?> RetrieveByIdAsync(Guid id)
        => _instrumentService.RetrieveInstrumentByIdAsync(id);

    // Yeni bir enstruman oluşturur ve asenkron olarak kaydeder.
    public Task CreateAsync(DtoInstrument instrument)
        => _instrumentService.CreateNewInstrumentAsync(instrument);

    // Belirli bir ID'ye sahip enstrumanı günceller ve asenkron olarak kaydeder.
    public Task UpdateAsync(Guid id, DtoInstrument instrument)
        => _instrumentService.UpdateExistingInstrumentAsync(id, instrument);

    // Belirli bir ID'ye sahip enstrumani asenkron olarak siler.
    public Task DeleteAsync(Guid id)
        => _instrumentService.DeleteInstrumentByIdAsync(id);
}
