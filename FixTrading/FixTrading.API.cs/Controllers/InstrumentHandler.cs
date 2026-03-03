using FixTrading.Application.Interfaces.Instrument;
using FixTrading.Common.Dtos.Instrument;

namespace FixTrading.API.Controllers;

//Controller ile Service arasındaki ara katman
public class InstrumentHandler
{
    // IInstrumentService, enstrümanlarla ilgili temel operasyonları tanımlayan bir interface'dir.
    private readonly IInstrumentService _instrumentService;

    // Constructor, IInstrumentService bağımlılığını alır ve sınıf içinde kullanılmak üzere saklar.
    public InstrumentHandler(IInstrumentService instrumentService)
    {
        _instrumentService = instrumentService;
    }
    
    public Task<List<DtoInstrument>> RetrieveAllInstrumentsAsync()  // Tüm enstrümanları asenkron olarak getiren bir metot tanımlanır.
        => _instrumentService.RetrieveAllInstrumentsAsync();

    public Task<DtoInstrument?> RetrieveInstrumentByIdAsync(Guid id)  // Belirli bir enstrümanı ID'sine göre asenkron olarak getiren bir metot tanımlanır.
        => _instrumentService.RetrieveInstrumentByIdAsync(id);

    public Task CreateNewInstrumentAsync(DtoInstrument instrument)  // Yeni bir enstrüman oluşturmak için asenkron bir metot tanımlanır.
        => _instrumentService.CreateNewInstrumentAsync(instrument);

    public Task UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument)  // Var olan bir enstrümanı güncellemek için asenkron bir metot tanımlanır.
        => _instrumentService.UpdateExistingInstrumentAsync(id, instrument);

    public Task DeleteInstrumentByIdAsync(Guid id)  // ID'ye göre bir enstrümanı silmek için asenkron bir metot tanımlanır.
        => _instrumentService.DeleteInstrumentByIdAsync(id);
}
